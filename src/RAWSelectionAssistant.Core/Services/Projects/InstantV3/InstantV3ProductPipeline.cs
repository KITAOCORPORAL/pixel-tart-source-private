using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research;
public static class InstantV3ProductPipeline
{
 public static ProfessionalRawDefinition Original(ProfessionalRawDefinition definition)
 {
  if(definition.InstantFilm?.V3 is not{} recipe)return definition with{ExposureEv=0};
  return definition with{LightPrint=definition.LightPrint is{} l?l with{WindowAdjustments=null}:null,InstantFilm=definition.InstantFilm with{V3=recipe with{Amount=0,Creative=null,Defects=null,Paper=recipe.Paper is{} paper?paper with{Pattern="Plain",Emboss=0,EdgeLine=0,Reflection=0}:null}}};
 }
 private sealed class PixelLease(IDisposable first,ISecondExposureSource second):IDisposable{public void Dispose(){second.Dispose();first.Dispose();}}
 private sealed class PairLease(IDisposable first,IDisposable second,HighBitDepthImageBuffer pixels):IDisposable{public void Dispose(){pixels.Dispose();second.Dispose();first.Dispose();}}
 public static bool IsV3(ProfessionalRawDefinition definition)=>definition.InstantFilm?.AlgorithmVersion==3;
 private sealed class Source(ProfessionalRawMaster master,ProfessionalRawDefinition basis):ICandidatePixelSource
 {
  public int Width=>ProfessionalRawPipeline.OrientedSize(master).Width;
  public int Height=>ProfessionalRawPipeline.OrientedSize(master).Height;
  public ColorVector3 Read(int x,int y)
  {
   var w=master.Image.Width;var h=master.Image.Height;
   var(px,py)=master.Image.Orientation switch{2=>(w-1-x,y),3=>(w-1-x,h-1-y),4=>(x,h-1-y),5=>(y,x),6=>(y,h-1-x),7=>(w-1-y,h-1-x),8=>(w-1-y,x),_=>(x,y)};
   return ProfessionalRawPipeline.RenderPixel(master,basis,px,py);
  }
 }
 public static CandidateRenderSession Open(ProfessionalRawMaster master,ProfessionalRawDefinition definition,CancellationToken token=default)
 {
  ProfessionalRawPipeline.ValidateMaster(master,definition);
  var recipe=definition.InstantFilm?.V3?.Freeze()??throw new InvalidDataException("V3 full recipe required");
  var bytes=checked((long)master.Image.Rgb32.Length*4);
  // Borrow existing exact registered source when possible; otherwise explicitly reserve caller-retained master.
  var pin=StudioGlobalResources.RetainBuffer(master.Image,bytes);
  if(recipe.MultipleInput is not{} second)return new(new Source(master,definition with{InstantFilm=null}),recipe,pin){WindowAdjustments=definition.LightPrint?.WindowAdjustments};
  IDisposable? secondLease=null;HighBitDepthImageBuffer? pixels=null;
  try
  {
   token.ThrowIfCancellationRequested();var decoder=SecondExposureInputs.Decoder??throw new NotSupportedException("SECOND_SOURCE_DECODER_UNAVAILABLE");
   if(second.Version==3&&decoder is ISecondExposurePixelDecoder bounded)
   {
    var source=bounded.OpenPixels(second,token);
    try{return new(new MultiplePixelSource(new Source(master,definition with{InstantFilm=null}),source,second),recipe,new PixelLease(pin,source)){WindowAdjustments=definition.LightPrint?.WindowAdjustments};}
    catch{source.Dispose();throw;}
   }
   var verified=second.Version>=2&&decoder.ValidatesSourceIdentity;if(!verified)second.ValidateSource(token);secondLease=StudioGlobalResources.ReserveWork(checked((long)second.Width*second.Height*12));
   pixels=decoder.Read(second,token);
   if(pixels.Width!=second.Width||pixels.Height!=second.Height||pixels.Orientation!=1||pixels.WorkingColorSpace!="sRGB")throw new InvalidDataException("SECOND_SOURCE_DOMAIN_CHANGED");if(!verified)second.ValidateSource(token);
   return new(new MultiplePixelSource(new Source(master,definition with{InstantFilm=null}),new BufferSecondSource(pixels),second),recipe,new PairLease(pin,secondLease,pixels)){WindowAdjustments=definition.LightPrint?.WindowAdjustments};
  }
  catch{pixels?.Dispose();secondLease?.Dispose();pin.Dispose();throw;}
 }
 public static (int Width,int Height) Size(ProfessionalRawMaster master,ProfessionalRawDefinition definition)
 {var size=ProfessionalRawPipeline.OrientedSize(master);var r=definition.InstantFilm!.V3!;var l=InstantPaperLayout.Create(size.Width,size.Height,new InstantFilmDefinition(true,AlgorithmVersion:2,PaperFrame:r.Surface.Frame){PaperGeometry=r.Geometry});return(l.Width,l.Height);}
 public static HighBitDepthImageBuffer Preview(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int edge,CancellationToken token)
 {using var session=Open(master,definition,token);using var owner=session.RenderPreview(edge,token);return owner.TransferToBuffer();}
 public static HighBitDepthImageBuffer Region(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int x,int y,int w,int h,CancellationToken token)
 {using var session=Open(master,definition,token);using var owner=session.RenderRegion(x,y,w,h,token);return owner.TransferToBuffer();}
}
