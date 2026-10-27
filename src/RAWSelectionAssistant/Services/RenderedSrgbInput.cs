using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;
public static class RenderedSrgbInput
{
 public static async Task<ProfessionalRawMaster> ReadAsync(string path,ProfessionalRawDefinition definition,CancellationToken token)
 {
  definition.Validate();token.ThrowIfCancellationRequested();if(definition.ColorState is not null)return await DocumentColorWorkflowV1.ReadAsync(path,definition,token);
  var input=await Task.Run(()=>SecondExposureDecoder.Describe(path,token),token);
  var lease=StudioGlobalResources.ReserveWork(checked((long)input.Width*input.Height*12));
  try
  {
   var image=await Task.Run(()=>new SecondExposureDecoder().Read(input,token),token);token.ThrowIfCancellationRequested();lease.Dispose();
   var contract=new ProfessionalRawContract("RenderedSrgbInput2","Rendered","Raster",input.Sha256,definition.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB",Characterization:"Rendered ICC interpreted by WIC; not scene RAW");
   return new(image,contract);
  }
  catch{lease.Dispose();throw;}
 }
}
