namespace RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;
public sealed record RenderedSrgbExposure(int Version=1,double ExposureEv=0,string Input="RenderedSrgbD65",string Working="LinearSrgbD65Relative",string Output="RoundedSrgbD65Clip01")
{
 public RenderedSrgbExposure Validate(){if(Version!=1||Input!="RenderedSrgbD65"||Working!="LinearSrgbD65Relative"||Output!="RoundedSrgbD65Clip01"||!double.IsFinite(ExposureEv)||ExposureEv is < -5 or >5)throw new InvalidDataException("Unsupported rendered sRGB exposure definition");return this;}
 public ColorVector3 Apply(ColorVector3 encoded)
 {
  return Apply(encoded,null);
 }
 // Read-only observer. The same arithmetic and historical clip boundary serve preview/export.
 public ColorVector3 Apply(ColorVector3 encoded,Action<string,ColorVector3>? observe)
 {
  double gain=Math.Pow(2,ExposureEv);
  var decoded=new ColorVector3(PhotoColorMathV1.DecodeSrgb(encoded.X),PhotoColorMathV1.DecodeSrgb(encoded.Y),PhotoColorMathV1.DecodeSrgb(encoded.Z));
  observe?.Invoke("decoded-display-linear",decoded);
  var exposed=new ColorVector3(decoded.X*gain,decoded.Y*gain,decoded.Z*gain);observe?.Invoke("exposed-linear",exposed);
  var oetf=new ColorVector3(PhotoColorMathV1.EncodeSrgb(exposed.X),PhotoColorMathV1.EncodeSrgb(exposed.Y),PhotoColorMathV1.EncodeSrgb(exposed.Z));observe?.Invoke("encoded-preclip",oetf);
  var output=new ColorVector3(Math.Clamp(oetf.X,0,1),Math.Clamp(oetf.Y,0,1),Math.Clamp(oetf.Z,0,1));observe?.Invoke("output-clipped",output);
  return output;
 }
}
