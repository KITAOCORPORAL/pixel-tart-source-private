using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
public enum StudioLayerBlend { Normal, Multiply, Screen }
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
    public Func<double,double,double> Compile(double aspect)
    {
        if(!double.IsFinite(aspect)||aspect<=0)throw new ArgumentOutOfRangeException(nameof(aspect));
        var sx=aspect>=1?aspect:1;var sy=aspect>=1?1:1/aspect;
        var prepared=Strokes.Select(s=>new PreparedStroke(s,s.Points.Min(p=>p.X)*sx-s.Radius,s.Points.Max(p=>p.X)*sx+s.Radius,s.Points.Min(p=>p.Y)*sy-s.Radius,s.Points.Max(p=>p.Y)*sy+s.Radius)).ToArray();
        return (x,y)=>
        {
            x*=sx;y*=sy;double weight=0;
            foreach(var entry in prepared)
            {
                if(x<entry.Left||x>entry.Right||y<entry.Top||y>entry.Bottom)continue;
                var s=entry.Stroke;double squared=double.MaxValue;
                for(int i=0;i<s.Points.Count;i++)
                {
                    var a=s.Points[Math.Max(0,i-1)];var b=s.Points[i];double ax=a.X*sx,ay=a.Y*sy,dx=(b.X-a.X)*sx,dy=(b.Y-a.Y)*sy;var length=dx*dx+dy*dy;
                    var t=length==0?0:Math.Clamp(((x-ax)*dx+(y-ay)*dy)/length,0,1);var ex=x-ax-t*dx;var ey=y-ay-t*dy;squared=Math.Min(squared,ex*ex+ey*ey);
                }
                if(squared>=s.Radius*s.Radius)continue;var distance=Math.Sqrt(squared);var inner=s.Radius*(1-s.Feather);
                var coverage=distance<=inner?1:(s.Radius-distance)/Math.Max(1e-12,s.Radius-inner);var alpha=coverage*s.Flow;
                weight=s.Erase?weight*(1-alpha):weight+Math.Max(0,s.Density-weight)*alpha;
            }
            return Inverted?1-weight:weight;
        };
    }
    public double Weight(double x,double y,double aspect)=>Compile(aspect)(x,y);
    private sealed record PreparedStroke(StudioMaskStroke Stroke,double Left,double Right,double Top,double Bottom);

}
/// <summary>LayerCompositeV1: straight RGB in existing display-referred nonlinear sRGB float domain. Alpha untouched.</summary>
public static class StudioLayerComposite
{
    public static bool Active(ColorAdjustmentStackNode node)=>node.CompositeVersion==1;
    public static bool Identity(ColorAdjustmentStackNode node)=>!Active(node)||node.Opacity==1&&node.Blend==StudioLayerBlend.Normal&&node.LocalMask is null;
    public static HighBitDepthImageBuffer Apply(HighBitDepthImageBuffer before,HighBitDepthImageBuffer effect,ColorAdjustmentStackNode node,CancellationToken token=default,FilmTileCoordinates? tile=null)
    {
        if(Identity(node))return effect;if(before.Width!=effect.Width||before.Height!=effect.Height)throw new InvalidDataException("混合输入尺寸不一致。");if(node.Opacity==0)return before.Clone();var a=before.Rgb32.Span;var b=effect.Rgb32.Span;var result=new float[a.Length];var fullW=tile?.FullWidth??before.Width;var fullH=tile?.FullHeight??before.Height;var sample=node.LocalMask?.Compile((double)fullW/fullH);
        for(int y=0;y<before.Height;y++)
        {
            token.ThrowIfCancellationRequested();for(int x=0;x<before.Width;x++)
            {
                var weight=node.Opacity*(sample?.Invoke((x+(tile?.Left??0)+.5)/fullW,(y+(tile?.Top??0)+.5)/fullH)??1);
                for(int c=0;c<3;c++){int i=(y*before.Width+x)*3+c;var mixed=node.Blend switch{StudioLayerBlend.Multiply=>a[i]*b[i],StudioLayerBlend.Screen=>1-(1-a[i])*(1-b[i]),_=>b[i]};result[i]=weight==0?a[i]:(float)(a[i]+(mixed-a[i])*weight);}
            }
        }
        return new(before.Width,before.Height,result,before.SourceBitDepth,before.WorkingColorSpace,before.Orientation,before.Metadata);
    }
}
