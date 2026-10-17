namespace RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;
public sealed record RenderedSrgbExposure(int Version=1,double ExposureEv=0,string Input="RenderedSrgbD65",string Working="LinearSrgbD65Relative",string Output="RoundedSrgbD65Clip01")
{
 public RenderedSrgbExposure Validate(){if(Version!=1||Input!="RenderedSrgbD65"||Working!="LinearSrgbD65Relative"||Output!="RoundedSrgbD65Clip01"||!double.IsFinite(ExposureEv)||ExposureEv is < -5 or >5)throw new InvalidDataException("Unsupported rendered sRGB exposure definition");return this;}
 public ColorVector3 Apply(ColorVector3 encoded)
 {
  double gain=Math.Pow(2,ExposureEv);
  double Channel(double x)=>Math.Clamp(PhotoColorMathV1.EncodeSrgb(PhotoColorMathV1.DecodeSrgb(x)*gain),0,1);
  return new(Channel(encoded.X),Channel(encoded.Y),Channel(encoded.Z));
 }
}
