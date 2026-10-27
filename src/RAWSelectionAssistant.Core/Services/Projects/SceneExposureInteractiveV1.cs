using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Preview-only V1, bounded 2x display-grid scene samples before scene EV; export is unchanged.
/// Not pixel-equivalent to the full-resolution area integral. No averaging in scene space.</summary>
public static class SceneExposureInteractiveV1
{
 public static HighBitDepthImageBuffer Prepare(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int edge=800,CancellationToken token=default)
 {
  ProfessionalRawPipeline.ValidateMaster(master,definition);var size=ProfessionalRawLocalRenderer.Size(master,definition);var scale=Math.Min(1,edge*2d/Math.Max(size.Width,size.Height));int w=Math.Max(1,(int)Math.Round(size.Width*scale)),h=Math.Max(1,(int)Math.Round(size.Height*scale));var a=new float[w*h*3];var oriented=ProfessionalRawPipeline.OrientedSize(master);var g=definition.Geometry??new();var input=master.Image.Rgb32;
  Parallel.For(0,h,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},y=>{for(int x=0;x<w;x++){var p=g.SourceTuple((x+.5)/w,(y+.5)/h,(double)oriented.Width/oriented.Height);int sx=Math.Clamp((int)(p.X*oriented.Width),0,oriented.Width-1),sy=Math.Clamp((int)(p.Y*oriented.Height),0,oriented.Height-1);var(mw,mh)=(master.Image.Width,master.Image.Height);var(px,py)=master.Image.Orientation switch{2=>(mw-1-sx,sy),3=>(mw-1-sx,mh-1-sy),4=>(sx,mh-1-sy),5=>(sy,sx),6=>(sy,mh-1-sx),7=>(mw-1-sy,mh-1-sx),8=>(mw-1-sy,sx),_=>(sx,sy)};int i=(py*mw+px)*3,j=(y*w+x)*3;for(int c=0;c<3;c++){float v=input.Span[i+c];if(!float.IsFinite(v))throw new InvalidDataException("Non finite scene sample");a[j+c]=v;}}});
  // Bounded decimation is unsafe on dense high-frequency content. Detect local source variation
  // independently of EV; retain original fine rendering for that image rather than relax the error gate.
  int risk=0,checks=0;for(int y=0;y<master.Image.Height-1;y+=Math.Max(1,master.Image.Height/96))for(int x=0;x<master.Image.Width-1;x+=Math.Max(1,master.Image.Width/96)){int i=(y*master.Image.Width+x)*3;double difference=0;for(int c=0;c<3;c++)difference+=Math.Abs(input.Span[i+c]-input.Span[i+3+c]);if(difference>.15)risk++;checks++;}
  return new(w,h,a,definition.Geometry?.Version==2||checks>0&&risk>.2*checks?"SceneExactFallbackV1":"SceneSamples2xV1",ProfessionalRawPipeline.WorkingSpace,1);
 }
 public static HighBitDepthImageBuffer Render(ProfessionalRawMaster master,ProfessionalRawDefinition definition,HighBitDepthImageBuffer samples,int edge=800,CancellationToken token=default)
 {
  if(samples.SourceBitDepth=="SceneExactFallbackV1")return ProfessionalRawLocalRenderer.Preview(master,definition,edge,token);
  var prepared=new ProfessionalRawLocalRenderer.Prepared(master,definition,token);var d=prepared.Definition;var oriented=ProfessionalRawPipeline.OrientedSize(master);var g=d.Geometry??new();int sw=samples.Width,sh=samples.Height;var scale=Math.Min(1,edge/(double)Math.Max(sw,sh));int w=Math.Max(1,(int)Math.Round(sw*scale)),h=Math.Max(1,(int)Math.Round(sh*scale));var output=new float[w*h*3];double rx=sw/(double)w,ry=sh/(double)h;var source=samples.Rgb32;
  Parallel.For(0,h,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},y=>{double ya=y*ry,yb=(y+1)*ry;for(int x=0;x<w;x++){double xa=x*rx,xb=(x+1)*rx;double r=0,green=0,b=0;for(int sy=(int)ya;sy<Math.Min(sh,(int)Math.Ceiling(yb));sy++)for(int sx=(int)xa;sx<Math.Min(sw,(int)Math.Ceiling(xb));sx++){double area=Math.Max(0,Math.Min(sy+1,yb)-Math.Max(sy,ya))*Math.Max(0,Math.Min(sx+1,xb)-Math.Max(sx,xa));int i=(sy*sw+sx)*3;var p=g.SourceTuple((sx+.5)/sw,(sy+.5)/sh,(double)oriented.Width/oriented.Height);var v=ProfessionalRawPipeline.ApplyExposure(new(source.Span[i],source.Span[i+1],source.Span[i+2]),d.ExposureEv);if(d.SceneExposure is{} scene)v=scene.Apply(v,prepared.SceneMask?.Invoke(p.X,p.Y)??1);v=ProfessionalRawPipeline.Display(v);r+=Decode(v.X)*area;green+=Decode(v.Y)*area;b+=Decode(v.Z)*area;}int j=(y*w+x)*3;output[j]=(float)Encode(r/(rx*ry));output[j+1]=(float)Encode(green/(rx*ry));output[j+2]=(float)Encode(b/(rx*ry));}});
  using var basis=new HighBitDepthImageBuffer(w,h,output,"32f","sRGB",1);return ProfessionalRawLocalRenderer.Fast(master,d,basis,token);
 }
 static double Decode(double v)=>v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);
 static double Encode(double v)=>v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055;
}
