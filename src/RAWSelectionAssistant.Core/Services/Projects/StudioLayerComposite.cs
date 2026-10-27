using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
public enum StudioLayerBlend { Normal, Multiply, Screen, Overlay, SoftLight, Darken, Lighten, Color, Luminosity }
public sealed record StudioMaskPoint(double X,double Y);
/// <summary>Normalized upright pre-output-crop coordinates; radius is fraction of the smaller source dimension.</summary>
public sealed record StudioMaskStroke(IReadOnlyList<StudioMaskPoint> Points,double Radius,double Feather,double Flow,double Density,bool Erase=false)
{
    public StudioMaskStroke Freeze()
    {
        if(Points is null||Points.Count is <1 or >20000||Points.Any(p=>!double.IsFinite(p.X+p.Y)||p.X<0||p.X>1||p.Y<0||p.Y>1)||!double.IsFinite(Radius+Feather+Flow+Density)||Radius<=0||Radius>1||Feather<0||Feather>1||Flow<0||Flow>1||Density<0||Density>1)throw new InvalidDataException("局部蒙版笔迹无效或超限。");return this with{Points=Points.Select(p=>p with{}).ToArray()};
    }
}
public sealed record StudioLocalMask(IReadOnlyList<StudioMaskStroke> Strokes,bool Inverted=false,int Version=1)
{
    public StudioLocalMask Freeze(){if(Version!=1||Strokes is null||Strokes.Count>2000||Strokes.Sum(s=>s.Points?.Count??0)>100000)throw new InvalidDataException("不支持的蒙版版本/笔迹数。");return this with{Strokes=Strokes.Select(s=>s.Freeze()).ToArray()};}
    public Func<double,double,double> Compile(double aspect,CancellationToken token=default)
    {
        if(!double.IsFinite(aspect)||aspect<=0)throw new ArgumentOutOfRangeException(nameof(aspect));
        var sx=aspect>=1?aspect:1;var sy=aspect>=1?1:1/aspect;
        var prepared=Strokes.Select(s=>{token.ThrowIfCancellationRequested();return new StrokeIndex(s,sx,sy,token);}).ToArray();
        return (x,y)=>{x*=sx;y*=sy;double weight=0;foreach(var entry in prepared){var s=entry.Stroke;var squared=entry.DistanceSquared(x,y,s.Radius*s.Radius);if(squared>=s.Radius*s.Radius)continue;var distance=Math.Sqrt(squared);var inner=s.Radius*(1-s.Feather);var coverage=distance<=inner?1:(s.Radius-distance)/Math.Max(1e-12,s.Radius-inner);var alpha=coverage*s.Flow;weight=s.Erase?weight*(1-alpha):weight+Math.Max(0,s.Density-weight)*alpha;}return Inverted?1-weight:weight;};
    }
    public double Weight(double x,double y,double aspect)=>Compile(aspect)(x,y);
    // Immutable bounded BVH. Exact segment-distance semantics; no raster or mutable cross-version mask cache.
    private sealed class StrokeIndex
    {
        public StudioMaskStroke Stroke{get;}private readonly Segment[] segments;private readonly Branch root;private readonly CancellationToken cancellation;
        private readonly record struct Segment(double X,double Y,double Dx,double Dy,double Length,double Left,double Right,double Top,double Bottom);
        private sealed record Branch(double Left,double Right,double Top,double Bottom,int Start,int Count,Branch? A=null,Branch? B=null);
        public StrokeIndex(StudioMaskStroke stroke,double sx,double sy,CancellationToken token){cancellation=token;Stroke=stroke;segments=new Segment[stroke.Points.Count];for(int i=0;i<segments.Length;i++){var a=stroke.Points[Math.Max(0,i-1)];var b=stroke.Points[i];double ax=a.X*sx,ay=a.Y*sy,bx=b.X*sx,by=b.Y*sy;segments[i]=new(ax,ay,bx-ax,by-ay,(bx-ax)*(bx-ax)+(by-ay)*(by-ay),Math.Min(ax,bx),Math.Max(ax,bx),Math.Min(ay,by),Math.Max(ay,by));}root=Build(0,segments.Length);}
        private Branch Build(int start,int count){cancellation.ThrowIfCancellationRequested();double l=double.MaxValue,r=double.MinValue,t=double.MaxValue,b=double.MinValue;for(int i=start;i<start+count;i++){var s=segments[i];l=Math.Min(l,s.Left);r=Math.Max(r,s.Right);t=Math.Min(t,s.Top);b=Math.Max(b,s.Bottom);}if(count<=8)return new(l,r,t,b,start,count);bool horizontal=r-l>=b-t;Array.Sort(segments,start,count,Comparer<Segment>.Create((a,b)=>horizontal?((a.Left+a.Right).CompareTo(b.Left+b.Right)):((a.Top+a.Bottom).CompareTo(b.Top+b.Bottom))));var half=count/2;return new(l,r,t,b,start,count,Build(start,half),Build(start+half,count-half));}
        public double DistanceSquared(double x,double y,double limit){if(x<root.Left-Stroke.Radius||x>root.Right+Stroke.Radius||y<root.Top-Stroke.Radius||y>root.Bottom+Stroke.Radius)return limit;return Distance(root,x,y,limit);}
        private double Distance(Branch node,double x,double y,double best){var dx=Math.Max(node.Left-x,Math.Max(0,x-node.Right));var dy=Math.Max(node.Top-y,Math.Max(0,y-node.Bottom));if(dx*dx+dy*dy>=best)return best;if(node.A is not null){best=Distance(node.A,x,y,best);return Distance(node.B!,x,y,best);}for(int i=node.Start;i<node.Start+node.Count;i++){var s=segments[i];var u=s.Length==0?0:Math.Clamp(((x-s.X)*s.Dx+(y-s.Y)*s.Dy)/s.Length,0,1);var ex=x-s.X-u*s.Dx;var ey=y-s.Y-u*s.Dy;best=Math.Min(best,ex*ex+ey*ey);}return best;}
    }

}
/// <summary>LayerCompositeV1: straight RGB in existing display-referred nonlinear sRGB float domain. Alpha untouched.</summary>
public static class StudioLayerComposite
{
    public static bool Active(ColorAdjustmentStackNode node)=>node.CompositeVersion is 1 or 2;
    public static bool Identity(ColorAdjustmentStackNode node)=>!Active(node)||node.Opacity==1&&node.Blend==StudioLayerBlend.Normal&&node.LocalMask is null;
    public static HighBitDepthImageBuffer Apply(HighBitDepthImageBuffer before,HighBitDepthImageBuffer effect,ColorAdjustmentStackNode node,CancellationToken token=default,FilmTileCoordinates? tile=null,Func<int,int,(double X,double Y)>? sourceCoordinate=null,Func<double,double,double>? compiledMask=null)
    {
        if(Identity(node))return effect;if(before.Width!=effect.Width||before.Height!=effect.Height)throw new InvalidDataException("混合输入尺寸不一致。");if(node.Opacity==0)return before.Clone();var a=before.Rgb32.Span;var b=effect.Rgb32.Span;var result=new float[a.Length];var fullW=tile?.FullWidth??before.Width;var fullH=tile?.FullHeight??before.Height;var sample=compiledMask??node.LocalMask?.Compile((double)fullW/fullH,token);
        for(int y=0;y<before.Height;y++)
        {
            token.ThrowIfCancellationRequested();for(int x=0;x<before.Width;x++)
            {
                var coordinate=sourceCoordinate?.Invoke(x,y);var weight=node.Opacity*(sample?.Invoke(coordinate?.X??(x+(tile?.Left??0)+.5)/fullW,coordinate?.Y??(y+(tile?.Top??0)+.5)/fullH)??1);
                if(node.CompositeVersion==2&&(int)node.Blend>=3){int p=(y*before.Width+x)*3;var mixed=PhotographicBlendV2.Blend(new(a[p],a[p+1],a[p+2]),new(b[p],b[p+1],b[p+2]),node.Blend);double[] channels=[mixed.X,mixed.Y,mixed.Z];for(int c=0;c<3;c++)result[p+c]=weight==0?a[p+c]:(float)(a[p+c]+(channels[c]-a[p+c])*weight);continue;}
                for(int c=0;c<3;c++){int i=(y*before.Width+x)*3+c;var mixed=node.Blend switch{StudioLayerBlend.Multiply=>a[i]*b[i],StudioLayerBlend.Screen=>1-(1-a[i])*(1-b[i]),_=>b[i]};result[i]=weight==0?a[i]:(float)(a[i]+(mixed-a[i])*weight);}
            }
        }
        return new(before.Width,before.Height,result,before.SourceBitDepth,before.WorkingColorSpace,before.Orientation,before.Metadata);
    }
}
