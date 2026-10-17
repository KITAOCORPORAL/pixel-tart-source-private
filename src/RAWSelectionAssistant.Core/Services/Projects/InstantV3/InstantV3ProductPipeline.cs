using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research;
public static class InstantV3ProductPipeline
{
 public static bool IsV3(ProfessionalRawDefinition definition)=>definition.InstantFilm?.AlgorithmVersion==3;
 private sealed class Source(ProfessionalRawMaster master,ProfessionalRawDefinition basis):ICandidatePixelSource
 {
  public int Width=>ProfessionalRawPipeline.OrientedSize(master).Width;
  public int Height=>ProfessionalRawPipeline.OrientedSize(master).Height;
  public ColorVector3 Read(int x,int y)=>ProfessionalRawPipeline.RenderOutputPixel(master,basis,x,y);
 }
 public static CandidateRenderSession Open(ProfessionalRawMaster master,ProfessionalRawDefinition definition)
 {
  ProfessionalRawPipeline.ValidateMaster(master,definition);
  var recipe=definition.InstantFilm?.V3?.Freeze()??throw new InvalidDataException("V3 full recipe required");
  var bytes=checked((long)master.Image.Rgb32.Length*4);
  // Borrow existing exact registered source when possible; otherwise explicitly reserve caller-retained master.
  var pin=StudioGlobalResources.RetainBuffer(master.Image,bytes);
  return new(new Source(master,definition with{InstantFilm=null}),recipe,pin);
 }
 public static (int Width,int Height) Size(ProfessionalRawMaster master,ProfessionalRawDefinition definition)
 {var size=ProfessionalRawPipeline.OrientedSize(master);var r=definition.InstantFilm!.V3!;var l=InstantPaperLayout.Create(size.Width,size.Height,new InstantFilmDefinition(true,AlgorithmVersion:2,PaperFrame:r.Surface.Frame){PaperGeometry=r.Geometry});return(l.Width,l.Height);}
 public static HighBitDepthImageBuffer Preview(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int edge,CancellationToken token)
 {using var session=Open(master,definition);using var owner=session.RenderPreview(edge,token);return owner.TransferToBuffer();}
 public static HighBitDepthImageBuffer Region(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int x,int y,int w,int h,CancellationToken token)
 {using var session=Open(master,definition);using var owner=session.RenderRegion(x,y,w,h,token);return owner.TransferToBuffer();}
}
