using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Default-off research variant. Converts the existing clipped LibRaw RGB16 master, never recovers clipped sensor data.</summary>
public sealed record RawWideOutputV1(int Version=1,string InputDomain="LibRaw021ProPhotoD65MatrixV1",string Conversion="LCMS212RelativeD65ToLinearROMMD50V1")
{
 public RawWideOutputV1 Validate(){if(Version!=1||InputDomain!="LibRaw021ProPhotoD65MatrixV1"||Conversion!="LCMS212RelativeD65ToLinearROMMD50V1")throw new InvalidDataException("Unknown RAW wide output research version");return this;}
 public static ProfessionalRawDefinition BaseDefinition(ProfessionalRawDefinition d)=>d with{RawWideOutput=null,ColorState=null,LocalEdits=null,Geometry=null};
 public static ProfessionalRawMaster ConvertMaster(ProfessionalRawMaster master,ProfessionalRawDefinition definition,CancellationToken token=default)
 {
  definition.RawWideOutput?.Validate();if(definition.RawWideOutput is null||definition.ColorState?.Version!=2)throw new InvalidDataException("Explicit RAW wide state required");var original=BaseDefinition(definition);ProfessionalRawPipeline.ValidateMaster(master,original);using var cms=new LcmsRgbEngine();var source=cms.StandardProfile("LibRawProPhotoD65LinearV1");if(definition.ColorState.Input.Sha256!=source.Sha256)throw new InvalidDataException("RAW source profile does not match frozen LibRaw matrix");using var transform=cms.CreateTransform(source,definition.ColorState.Working);var input=master.Image.Rgb32.ToArray();if(definition.ExposureEv!=0){var gain=Math.Pow(2,definition.ExposureEv);for(int i=0;i<input.Length;i++)input[i]=(float)(input[i]*gain);}var rgb=transform.Convert(input,token);return new(new HighBitDepthImageBuffer(master.Image.Width,master.Image.Height,rgb,"32f",DocumentWorkingRendererV2.Space,master.Image.Orientation,master.Image.Metadata),master.Contract with{DecodeKey=definition.DecodeKey,OutputSpace=DocumentWorkingRendererV2.Space,Gamma="linear gamma1 D50",Characterization="RawWideOutputV1: existing LibRaw RGB16 -> exact derived D65 matrix ICC -> LCMS D50 linear working; frontend clipping retained, no C1 profile"});
 }
}
