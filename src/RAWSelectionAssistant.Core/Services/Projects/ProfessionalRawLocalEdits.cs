using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Explicit post-PV4-display editing extension. Never a linear-RAW blend or PV7 profile engine.</summary>
public sealed record ProfessionalRawLocalEdits(ColorAdjustmentStack Stack,int Version=1)
{
 public ProfessionalRawLocalEdits Freeze(){if(Version is not(1 or 2)||Stack is null)throw new InvalidDataException("Unknown professional local edit version");var stack=Stack.Normalize();if(stack.Nodes.Any(n=>!ProfessionalRawLocalRenderer.Supports(n.Type)))throw new NotSupportedException("专业RAW局部层当前支持白平衡、影调、色阶、曲线、色彩平衡、肤色和基础预设；不支持参考匹配或空间细节层。");return this with{Stack=stack.DeepClone()};}
}
/// <summary>Output-only source-normalized crop/quarter-turn. Mask coordinates remain upright uncropped source.</summary>
public sealed record ProfessionalRawGeometry(double X=0,double Y=0,double Width=1,double Height=1,int QuarterTurns=0,bool FlipHorizontal=false,int Version=1)
{
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
 public double StraightenDegrees{get;init;}
 public ProfessionalRawGeometry Validate(){if(Version is not(1 or 2)||Version==1&&StraightenDegrees!=0||!double.IsFinite(StraightenDegrees)||Math.Abs(StraightenDegrees)>15||!double.IsFinite(X+Y+Width+Height)||X<0||Y<0||Width<=0||Height<=0||X+Width>1.000000001||Y+Height>1.000000001||QuarterTurns is <0 or >3)throw new InvalidDataException("Invalid RAW output geometry");return this;}
 public (int Width,int Height) Size(int w,int h){Validate();var cw=Math.Max(1,(int)Math.Round(w*Width));var ch=Math.Max(1,(int)Math.Round(h*Height));return QuarterTurns%2==0?(cw,ch):(ch,cw);}
 public StudioMaskPoint Source(double u,double v){var p=SourceTuple(u,v);return new(p.X,p.Y);}
 public (double X,double Y) SourceTuple(double u,double v,double sourceAspect=1){if(FlipHorizontal)u=1-u;(u,v)=QuarterTurns switch{1=>(v,1-u),2=>(1-u,1-v),3=>(1-v,u),_=>(u,v)};var x=X+u*Width;var y=Y+v*Height;if(StraightenDegrees!=0){double a=StraightenDegrees*Math.PI/180,dx=(x-.5)*sourceAspect,dy=y-.5;return (.5+(dx*Math.Cos(a)-dy*Math.Sin(a))/sourceAspect,.5+dx*Math.Sin(a)+dy*Math.Cos(a));}return (x,y);}
}
public static class ProfessionalRawLocalRenderer
{
 public sealed class Prepared
 {
  public ProfessionalRawDefinition Definition{get;}public IReadOnlyDictionary<Guid,Func<double,double,double>> Masks{get;}public Func<double,double,double>? SceneMask{get;}
  public Prepared(ProfessionalRawMaster master,ProfessionalRawDefinition definition,CancellationToken token=default){var clock=RawLocalProfile.Begin();ProfessionalRawPipeline.ValidateMaster(master,definition);Definition=definition with{LocalEdits=definition.LocalEdits?.Freeze(),SceneExposure=definition.SceneExposure?.Validate()};var size=ProfessionalRawPipeline.OrientedSize(master);SceneMask=definition.SceneExposure?.Mask?.Compile((double)size.Width/size.Height,token);Masks=Definition.LocalEdits?.Stack.Nodes.Where(n=>n.LocalMask is not null).ToDictionary(n=>n.Id,n=>n.LocalMask!.Compile((double)size.Width/size.Height,token))??[];RawLocalProfile.End("freeze-and-index",clock);}
 }
 public static bool Active(ProfessionalRawDefinition d)=>d.ColorState?.Version==2||d.LocalEdits is not null||d.Geometry is not null||d.SceneExposure is not null;
 public static bool Supports(ColorStudioNodeType t)=>t is ColorStudioNodeType.GraphMix or ColorStudioNodeType.WhiteBalance or ColorStudioNodeType.BasicTone or ColorStudioNodeType.Levels or ColorStudioNodeType.Curve or ColorStudioNodeType.ColorBalance or ColorStudioNodeType.SkinTone or ColorStudioNodeType.Preset;
 public static (int Width,int Height) Size(ProfessionalRawMaster m,ProfessionalRawDefinition d){var s=ProfessionalRawPipeline.OrientedSize(m);return (d.Geometry??new()).Size(s.Width,s.Height);}
 public static HighBitDepthImageBuffer Region(ProfessionalRawMaster m,ProfessionalRawDefinition d,int left,int top,int width,int height,CancellationToken token=default,Prepared? prepared=null)
 {
  if(d.ColorState?.Version==2)return DocumentWorkingRendererV2.Region(m,d,left,top,width,height,token);
  prepared??=new Prepared(m,d,token);d=prepared.Definition;
  var size=Size(m,d);if(left<0||top<0||width<=0||height<=0||left+width>size.Width||top+height>size.Height)throw new ArgumentOutOfRangeException(nameof(left));
  var oriented=ProfessionalRawPipeline.OrientedSize(m);var geometry=d.Geometry??new();var values=GraphPixelAllocation.Allocate(checked(width*height*3));
  (double X,double Y) Coordinate(int x,int y){var u=(left+x+.5)/size.Width;var v=(top+y+.5)/size.Height;var p=geometry.SourceTuple(u,v,(double)oriented.Width/oriented.Height);if(geometry.Version==2)return p;return ((Math.Clamp((int)(p.X*oriented.Width),0,oriented.Width-1)+.5)/oriented.Width,(Math.Clamp((int)(p.Y*oriented.Height),0,oriented.Height-1)+.5)/oriented.Height);}
  var baseClock=RawLocalProfile.Begin();
  for(int y=0;y<height;y++){token.ThrowIfCancellationRequested();for(int x=0;x<width;x++){
   var p=Coordinate(x,y);int sx=Math.Clamp((int)(p.X*oriented.Width),0,oriented.Width-1),sy=Math.Clamp((int)(p.Y*oriented.Height),0,oriented.Height-1);var w=m.Image.Width;var h=m.Image.Height;
   var(px,py)=m.Image.Orientation switch{2=>(w-1-sx,sy),3=>(w-1-sx,h-1-sy),4=>(sx,h-1-sy),5=>(sy,sx),6=>(sy,h-1-sx),7=>(w-1-sy,h-1-sx),8=>(w-1-sy,sx),_=>(sx,sy)};
   var v=ProfessionalRawPipeline.RenderPixel(m,d,px,py,prepared.SceneMask);
   if(geometry.Version==2){double fx=p.X*oriented.Width-.5,fy=p.Y*oriented.Height-.5;int x0=(int)Math.Floor(fx),y0=(int)Math.Floor(fy);double dx=fx-x0,dy=fy-y0;
    RAWSelectionAssistant.Core.Services.Color.ColorVector3 Read(int ox,int oy){if(ox<0||oy<0||ox>=oriented.Width||oy>=oriented.Height)return new(0,0,0);var(ax,ay)=m.Image.Orientation switch{2=>(w-1-ox,oy),3=>(w-1-ox,h-1-oy),4=>(ox,h-1-oy),5=>(oy,ox),6=>(oy,h-1-ox),7=>(w-1-oy,h-1-ox),8=>(w-1-oy,ox),_=>(ox,oy)};return ProfessionalRawPipeline.RenderPixel(m,d,ax,ay,prepared.SceneMask);}
    var a=Read(x0,y0);var b=Read(x0+1,y0);var c=Read(x0,y0+1);var e=Read(x0+1,y0+1);double Mix(double aa,double bb,double cc,double ee)=>(aa*(1-dx)+bb*dx)*(1-dy)+(cc*(1-dx)+ee*dx)*dy;v=new(Mix(a.X,b.X,c.X,e.X),Mix(a.Y,b.Y,c.Y,e.Y),Mix(a.Z,b.Z,c.Z,e.Z));
   }var i=(y*width+x)*3;values[i]=(float)v.X;values[i+1]=(float)v.Y;values[i+2]=(float)v.Z;
  }}
  RawLocalProfile.End("base-raw-coordinate-display",baseClock);
  var current=new HighBitDepthImageBuffer(width,height,values,"16","sRGB",1);
  if(d.LocalEdits?.Stack.Graph?.Version==2){using var original=current;return StudioGraphRenderer.Render(original,d.LocalEdits.Stack,(input,node)=>ColorStudioRenderPipeline.ApplyPointEffect(input,node,token),token,new(0,0,oriented.Width,oriented.Height),Coordinate,prepared.Masks);}
  if(d.LocalEdits is{} edits){foreach(var node in StudioGraphRouting.Nodes(edits.Stack).Where(n=>n.Enabled)){
   token.ThrowIfCancellationRequested();if(node.CompositeVersion is 1 or 2&&node.Opacity==0)continue;
   var toolClock=RawLocalProfile.Begin();var effect=ColorStudioRenderPipeline.ApplyPointEffect(current,node,token);RawLocalProfile.End("point-effect",toolClock);var maskClock=RawLocalProfile.Begin();
   var next=StudioLayerComposite.Apply(current,effect,node,token,new(0,0,oriented.Width,oriented.Height),Coordinate,prepared.Masks.GetValueOrDefault(node.Id));RawLocalProfile.End("mask-composite",maskClock);if(!ReferenceEquals(next,effect))effect.Dispose();current.Dispose();current=next;
  }}return current;
 }
 /// <summary>Interactive approximation: cached area-averaged display base then same frozen point-node definitions. Never used for export.</summary>
 public static HighBitDepthImageBuffer FastBase(ProfessionalRawMaster m,ProfessionalRawDefinition d,int edge=800,CancellationToken token=default)
 {
  // Cache the unchanged full-display area base, not an aliased nearest RGB source.
  // This one-time cold cost is measured separately; interactive parameter changes reuse it.
  return Preview(m,d with{LocalEdits=null},edge,token);
 }
 public static HighBitDepthImageBuffer Fast(ProfessionalRawMaster m,ProfessionalRawDefinition d,HighBitDepthImageBuffer? basis=null,CancellationToken token=default)
 {
  if(d.ColorState?.Version==2)return DocumentWorkingRendererV2.Preview(m,d,800,token);
  var prepared=new Prepared(m,d,token);d=prepared.Definition;var current=basis?.Clone()??FastBase(m,d,800,token);var oriented=ProfessionalRawPipeline.OrientedSize(m);var geometry=d.Geometry??new();var w=current.Width;var h=current.Height;
  (double X,double Y) Coordinate(int x,int y){var p=geometry.SourceTuple((x+.5)/w,(y+.5)/h,(double)oriented.Width/oriented.Height);if(geometry.Version==2)return p;return ((Math.Clamp((int)(p.X*oriented.Width),0,oriented.Width-1)+.5)/oriented.Width,(Math.Clamp((int)(p.Y*oriented.Height),0,oriented.Height-1)+.5)/oriented.Height);}
  if(d.LocalEdits?.Stack.Graph?.Version==2){using var original=current;return StudioGraphRenderer.Render(original,d.LocalEdits.Stack,(input,node)=>ColorStudioRenderPipeline.ApplyPointEffect(input,node,token),token,new(0,0,oriented.Width,oriented.Height),Coordinate,prepared.Masks);}
  foreach(var node in (d.LocalEdits is{} local?StudioGraphRouting.Nodes(local.Stack).Where(n=>n.Enabled):[])){if(node.CompositeVersion is 1 or 2&&node.Opacity==0)continue;var effect=ColorStudioRenderPipeline.ApplyPointEffect(current,node,token);var next=StudioLayerComposite.Apply(current,effect,node,token,new(0,0,oriented.Width,oriented.Height),Coordinate,prepared.Masks.GetValueOrDefault(node.Id));if(!ReferenceEquals(effect,next))effect.Dispose();current.Dispose();current=next;}return current;
 }
 public static HighBitDepthImageBuffer Preview(ProfessionalRawMaster m,ProfessionalRawDefinition d,int edge,CancellationToken token=default)
 {
  if(d.ColorState?.Version==2)return DocumentWorkingRendererV2.Preview(m,d,edge,token);
  var prepared=new Prepared(m,d,token);d=prepared.Definition;var size=Size(m,d);var scale=edge>0?Math.Min(1,edge/(double)Math.Max(size.Width,size.Height)):1;var w=Math.Max(1,(int)Math.Round(size.Width*scale));var h=Math.Max(1,(int)Math.Round(size.Height*scale));if(w==size.Width&&h==size.Height)return Region(m,d,0,0,w,h,token,prepared);
  // Same full-resolution display + local effects first, then exact area average in display-linear sRGB.
  var data=new float[w*h*3];double rx=size.Width/(double)w,ry=size.Height/(double)h;
  // Up to four independent destination-row bands; immutable prepared mask indexes are shared.
  // Each destination pixel sees exactly the same source-y/source-x summation order as V1 serial area integration.
  Parallel.For(0,Math.Min(4,h),new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},worker=>{
   using var graphAllocation=d.LocalEdits?.Stack.Graph?.Version==2?GraphPixelAllocation.Enter():null;var sums=new double[w*3];
   for(int py=worker*h/Math.Min(4,h);py<(worker+1)*h/Math.Min(4,h);py++){
    token.ThrowIfCancellationRequested();double ya=py*ry,yb=(py+1)*ry;int top=(int)ya,bottom=Math.Min(size.Height,(int)Math.Ceiling(yb));using var band=Region(m,d,0,top,size.Width,bottom-top,token,prepared);var rgb=band.Rgb32.Span;Array.Clear(sums);
    for(int y=0;y<band.Height;y++){int sy=top+y;double wy=Math.Max(0,Math.Min(sy+1,yb)-Math.Max(sy,ya));if(wy==0)continue;for(int sx=0;sx<size.Width;sx++)for(int px=(int)(sx/rx);px<=Math.Min(w-1,(int)((sx+1)/rx));px++){double wx=Math.Max(0,Math.Min(sx+1,(px+1)*rx)-Math.Max(sx,px*rx));if(wx==0)continue;int i=(y*size.Width+sx)*3,o=px*3;for(int c=0;c<3;c++){double v=rgb[i+c];sums[o+c]+=(v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4))*wx*wy;}}}
    for(int i=0;i<sums.Length;i++){double v=sums[i]/(rx*ry);data[py*w*3+i]=(float)(v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055);}
   }
  });return new(w,h,data,"16","sRGB",1);
 }
}
