using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Explicit post-PV4-display editing extension. Never a linear-RAW blend or PV7 profile engine.</summary>
public sealed record ProfessionalRawLocalEdits(ColorAdjustmentStack Stack,int Version=1)
{
 public ProfessionalRawLocalEdits Freeze(){if(Version!=1||Stack is null)throw new InvalidDataException("Unknown professional local edit version");var stack=Stack.Normalize();if(stack.Nodes.Any(n=>!ProfessionalRawLocalRenderer.Supports(n.Type)))throw new NotSupportedException("专业RAW局部层当前支持白平衡、影调、色阶、曲线、色彩平衡、肤色和基础预设；不支持参考匹配或空间细节层。");return this with{Stack=stack.DeepClone()};}
}
/// <summary>Output-only source-normalized crop/quarter-turn. Mask coordinates remain upright uncropped source.</summary>
public sealed record ProfessionalRawGeometry(double X=0,double Y=0,double Width=1,double Height=1,int QuarterTurns=0,bool FlipHorizontal=false,int Version=1)
{
 public ProfessionalRawGeometry Validate(){if(Version!=1||!double.IsFinite(X+Y+Width+Height)||X<0||Y<0||Width<=0||Height<=0||X+Width>1.000000001||Y+Height>1.000000001||QuarterTurns is <0 or >3)throw new InvalidDataException("Invalid RAW output geometry");return this;}
 public (int Width,int Height) Size(int w,int h){Validate();var cw=Math.Max(1,(int)Math.Round(w*Width));var ch=Math.Max(1,(int)Math.Round(h*Height));return QuarterTurns%2==0?(cw,ch):(ch,cw);}
 public StudioMaskPoint Source(double u,double v){var p=SourceTuple(u,v);return new(p.X,p.Y);}
 public (double X,double Y) SourceTuple(double u,double v){if(FlipHorizontal)u=1-u;(u,v)=QuarterTurns switch{1=>(v,1-u),2=>(1-u,1-v),3=>(1-v,u),_=>(u,v)};return (X+u*Width,Y+v*Height);}
}
public static class ProfessionalRawLocalRenderer
{
 public sealed class Prepared
 {
  public ProfessionalRawDefinition Definition{get;}public IReadOnlyDictionary<Guid,Func<double,double,double>> Masks{get;}
  public Prepared(ProfessionalRawMaster master,ProfessionalRawDefinition definition,CancellationToken token=default){ProfessionalRawPipeline.ValidateMaster(master,definition);Definition=definition with{LocalEdits=definition.LocalEdits?.Freeze()};var size=ProfessionalRawPipeline.OrientedSize(master);Masks=Definition.LocalEdits?.Stack.Nodes.Where(n=>n.LocalMask is not null).ToDictionary(n=>n.Id,n=>n.LocalMask!.Compile((double)size.Width/size.Height,token))??[];}
 }
 public static bool Active(ProfessionalRawDefinition d)=>d.LocalEdits is not null||d.Geometry is not null;
 public static bool Supports(ColorStudioNodeType t)=>t is ColorStudioNodeType.WhiteBalance or ColorStudioNodeType.BasicTone or ColorStudioNodeType.Levels or ColorStudioNodeType.Curve or ColorStudioNodeType.ColorBalance or ColorStudioNodeType.SkinTone or ColorStudioNodeType.Preset;
 public static (int Width,int Height) Size(ProfessionalRawMaster m,ProfessionalRawDefinition d){var s=ProfessionalRawPipeline.OrientedSize(m);return (d.Geometry??new()).Size(s.Width,s.Height);}
 public static HighBitDepthImageBuffer Region(ProfessionalRawMaster m,ProfessionalRawDefinition d,int left,int top,int width,int height,CancellationToken token=default,Prepared? prepared=null)
 {
  prepared??=new Prepared(m,d,token);d=prepared.Definition;
  var size=Size(m,d);if(left<0||top<0||width<=0||height<=0||left+width>size.Width||top+height>size.Height)throw new ArgumentOutOfRangeException(nameof(left));
  var oriented=ProfessionalRawPipeline.OrientedSize(m);var geometry=d.Geometry??new();var values=new float[checked(width*height*3)];
  (double X,double Y) Coordinate(int x,int y){var u=(left+x+.5)/size.Width;var v=(top+y+.5)/size.Height;var p=geometry.SourceTuple(u,v);return ((Math.Clamp((int)(p.X*oriented.Width),0,oriented.Width-1)+.5)/oriented.Width,(Math.Clamp((int)(p.Y*oriented.Height),0,oriented.Height-1)+.5)/oriented.Height);}
  for(int y=0;y<height;y++){token.ThrowIfCancellationRequested();for(int x=0;x<width;x++){
   var p=Coordinate(x,y);int sx=Math.Clamp((int)(p.X*oriented.Width),0,oriented.Width-1),sy=Math.Clamp((int)(p.Y*oriented.Height),0,oriented.Height-1);var w=m.Image.Width;var h=m.Image.Height;
   var(px,py)=m.Image.Orientation switch{2=>(w-1-sx,sy),3=>(w-1-sx,h-1-sy),4=>(sx,h-1-sy),5=>(sy,sx),6=>(sy,h-1-sx),7=>(w-1-sy,h-1-sx),8=>(w-1-sy,sx),_=>(sx,sy)};
   var v=ProfessionalRawPipeline.RenderPixel(m,d,px,py);var i=(y*width+x)*3;values[i]=(float)v.X;values[i+1]=(float)v.Y;values[i+2]=(float)v.Z;
  }}
  var current=new HighBitDepthImageBuffer(width,height,values,"16","sRGB",1);
  if(d.LocalEdits is{} edits){foreach(var node in edits.Stack.Nodes.Where(n=>n.Enabled)){
   token.ThrowIfCancellationRequested();if(node.CompositeVersion==1&&node.Opacity==0)continue;
   var stack=new ColorAdjustmentStack([node with{CompositeVersion=0,Opacity=1,Blend=StudioLayerBlend.Normal,LocalMask=null}],ProcessingVersion:2);var analysis=ColorStudioProcessingAnalysis.Create(new VisualPixelBuffer(1,1,new byte[3]),stack,null,token);
   var effect=new ColorStudioRenderPipeline().Render(current,analysis,null,stack,token,captureNodeDiagnostics:false).ProcessingPixels!;
   var next=StudioLayerComposite.Apply(current,effect,node,token,new(0,0,oriented.Width,oriented.Height),Coordinate,prepared.Masks.GetValueOrDefault(node.Id));if(!ReferenceEquals(next,effect))effect.Dispose();current.Dispose();current=next;
  }}return current;
 }
 public static HighBitDepthImageBuffer Preview(ProfessionalRawMaster m,ProfessionalRawDefinition d,int edge,CancellationToken token=default)
 {
  var prepared=new Prepared(m,d,token);d=prepared.Definition;var size=Size(m,d);var scale=edge>0?Math.Min(1,edge/(double)Math.Max(size.Width,size.Height)):1;var w=Math.Max(1,(int)Math.Round(size.Width*scale));var h=Math.Max(1,(int)Math.Round(size.Height*scale));if(w==size.Width&&h==size.Height)return Region(m,d,0,0,w,h,token,prepared);
  // Same full-resolution display + local effects first, then exact area average in display-linear sRGB.
  var sums=new double[w*h*3];double rx=size.Width/(double)w,ry=size.Height/(double)h;
  for(int top=0;top<size.Height;top+=32){using var band=Region(m,d,0,top,size.Width,Math.Min(32,size.Height-top),token,prepared);var rgb=band.Rgb32.Span;for(int y=0;y<band.Height;y++){token.ThrowIfCancellationRequested();var sy=top+y;for(int py=(int)(sy/ry);py<=Math.Min(h-1,(int)((sy+1)/ry));py++){var wy=Math.Max(0,Math.Min(sy+1,(py+1)*ry)-Math.Max(sy,py*ry));if(wy==0)continue;for(int sx=0;sx<size.Width;sx++)for(int px=(int)(sx/rx);px<=Math.Min(w-1,(int)((sx+1)/rx));px++){var wx=Math.Max(0,Math.Min(sx+1,(px+1)*rx)-Math.Max(sx,px*rx));if(wx==0)continue;var i=(y*size.Width+sx)*3;var o=(py*w+px)*3;for(int c=0;c<3;c++){var v=rgb[i+c];sums[o+c]+=(v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4))*wx*wy;}}}}}
  var data=new float[sums.Length];for(int i=0;i<data.Length;i++){var v=sums[i]/(rx*ry);data[i]=(float)(v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055);}return new(w,h,data,"16","sRGB",1);
 }
}
