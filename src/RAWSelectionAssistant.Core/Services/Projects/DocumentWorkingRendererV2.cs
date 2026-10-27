using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Minimum admitted scene-independent linear RGB path: D50 ROMM primaries, gamma1, float32. No legacy SDR tools.</summary>
public static class DocumentWorkingRendererV2
{
 public const string Space="LinearProPhotoD50V2";
 public sealed record Prepared(ProfessionalRawDefinition Definition,(ColorAdjustmentStackNode Node,Func<double,double,double>? Mask)[] Nodes);
 public static Prepared Prepare(ProfessionalRawMaster m,ProfessionalRawDefinition d,CancellationToken token){ProfessionalRawPipeline.ValidateMaster(m,d);Validate(d);var size=ProfessionalRawPipeline.OrientedSize(m);return new(d with{LocalEdits=d.LocalEdits?.Freeze()},(d.LocalEdits?.Stack.Nodes.Where(n=>n.Enabled)??[]).Select(n=>(n,n.LocalMask?.Compile((double)size.Width/size.Height,token))).ToArray());}
 public static void Validate(ProfessionalRawDefinition d)
 {
  if(d.ColorState?.Version!=2)return;
  foreach(var n in d.LocalEdits?.Stack.Nodes??[])
   if(n.Type!=ColorStudioNodeType.BasicTone||n.Blend!=StudioLayerBlend.Normal||n.NumericParameters.Any(p=>p.Key!="exposure"&&p.Value!=0)||Math.Abs(n.NumericParameters.GetValueOrDefault("exposure"))>10)
    throw new NotSupportedException("宽色域编辑V2仅支持线性曝光、Normal强度/蒙版与几何；其他encoded-sRGB工具尚未适配。");
 }
 public static HighBitDepthImageBuffer Region(ProfessionalRawMaster m,ProfessionalRawDefinition d,int left,int top,int w,int h,CancellationToken token,Prepared? prepared=null)
 {
  prepared??=Prepare(m,d,token);d=prepared.Definition;var size=ProfessionalRawLocalRenderer.Size(m,d);if(left<0||top<0||w<=0||h<=0||left+w>size.Width||top+h>size.Height)throw new ArgumentOutOfRangeException();
  var oriented=ProfessionalRawPipeline.OrientedSize(m);var g=d.Geometry??new();var data=new float[w*h*3];var input=m.Image.Rgb32.Span;
  var nodes=prepared.Nodes;
  for(int y=0;y<h;y++){token.ThrowIfCancellationRequested();for(int x=0;x<w;x++){
   var p=g.SourceTuple((left+x+.5)/size.Width,(top+y+.5)/size.Height,(double)oriented.Width/oriented.Height);
   double sx=p.X*oriented.Width-.5,sy=p.Y*oriented.Height-.5;int x0=(int)Math.Floor(sx),y0=(int)Math.Floor(sy);double fx=sx-x0,fy=sy-y0;
   for(int c=0;c<3;c++){
    double Read(int ox,int oy){if(ox<0||oy<0||ox>=oriented.Width||oy>=oriented.Height)return 0;int iw=m.Image.Width,ih=m.Image.Height;var (ax,ay)=m.Image.Orientation switch{2=>(iw-1-ox,oy),3=>(iw-1-ox,ih-1-oy),4=>(ox,ih-1-oy),5=>(oy,ox),6=>(oy,ih-1-ox),7=>(iw-1-oy,ih-1-ox),8=>(iw-1-oy,ox),_=>(ox,oy)};return m.Image.Rgb32.Span[(ay*iw+ax)*3+c];}
    double value=g.Version==2?(Read(x0,y0)*(1-fx)+Read(x0+1,y0)*fx)*(1-fy)+(Read(x0,y0+1)*(1-fx)+Read(x0+1,y0+1)*fx)*fy:Read(Math.Clamp((int)(p.X*oriented.Width),0,oriented.Width-1),Math.Clamp((int)(p.Y*oriented.Height),0,oriented.Height-1));
    foreach(var item in nodes){double amount=item.Node.Opacity*(item.Mask?.Invoke(p.X,p.Y)??1);double ev=item.Node.NumericParameters.GetValueOrDefault("exposure");value+=value*(Math.Pow(2,ev)-1)*amount;}
    data[(y*w+x)*3+c]=(float)value;
   }
  }}return new(w,h,data,"32f",Space,1);
 }
 public static HighBitDepthImageBuffer Preview(ProfessionalRawMaster m,ProfessionalRawDefinition d,int edge,CancellationToken token)
 {
  var prepared=Prepare(m,d,token);var size=ProfessionalRawLocalRenderer.Size(m,d);double scale=edge>0?Math.Min(1,edge/(double)Math.Max(size.Width,size.Height)):1;int w=Math.Max(1,(int)Math.Round(size.Width*scale)),h=Math.Max(1,(int)Math.Round(size.Height*scale));var averaged=new float[w*h*3];double rx=size.Width/(double)w,ry=size.Height/(double)h;
  // Bounded full-resolution bands, same linear exposure/mask/geometry first; average in the declared linear working domain.
  for(int py=0;py<h;py++){token.ThrowIfCancellationRequested();double ya=py*ry,yb=(py+1)*ry;int top=(int)ya,bottom=Math.Min(size.Height,(int)Math.Ceiling(yb));using var band=Region(m,d,0,top,size.Width,bottom-top,token,prepared);var sums=new double[w*3];for(int y=0;y<band.Height;y++){int sy=top+y;double wy=Math.Max(0,Math.Min(sy+1,yb)-Math.Max(sy,ya));for(int sx=0;sx<size.Width;sx++)for(int px=(int)(sx/rx);px<=Math.Min(w-1,(int)((sx+1)/rx));px++){double wx=Math.Max(0,Math.Min(sx+1,(px+1)*rx)-Math.Max(sx,px*rx));if(wx<=0)continue;for(int c=0;c<3;c++)sums[px*3+c]+=band.Rgb32.Span[(y*size.Width+sx)*3+c]*wx*wy;}}for(int i=0;i<sums.Length;i++)averaged[py*w*3+i]=(float)(sums[i]/(rx*ry));}
  using var cms=new LcmsRgbEngine();using var t=cms.CreateTransform(d.ColorState!.Working,cms.StandardProfile("sRGB"));return new(w,h,t.Convert(averaged,token),"32f","sRGB",1);

 }
}
