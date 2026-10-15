namespace RAWSelectionAssistant.Core.Services.ColorScience;
public sealed record CameraChartCaptureV1(string SampleId,string RawSha256,string Camera,string RawMode,string Lens,string Illuminant,string IlluminantMeasurement,double ExposureEv,string ChartVersion,string ReferenceSha256,string ReferenceWhitePoint,int ObserverDegrees,string RoiExclusionEvidence,string Split,string DeltaEConditions,string Uncertainty);
public sealed record CaptureProtocolResult(bool MetadataComplete,bool CalibrationEligible,IReadOnlyList<string> Errors);
public static class CameraChartCaptureValidator
{
 public static CaptureProtocolResult Validate(CameraChartCaptureV1 sample)
 {
  var errors=new List<string>();
  if(new[]{sample.SampleId,sample.Camera,sample.RawMode,sample.Lens,sample.Illuminant,sample.IlluminantMeasurement,sample.ChartVersion,sample.ReferenceWhitePoint,sample.RoiExclusionEvidence,sample.DeltaEConditions,sample.Uncertainty}.Any(string.IsNullOrWhiteSpace))errors.Add("Missing acquisition/measurement/ROI/uncertainty metadata.");
  bool Hash(string v)=>v is not null&&v.Length==64&&v.All(Uri.IsHexDigit);
  if(!Hash(sample.RawSha256)||!Hash(sample.ReferenceSha256))errors.Add("Missing immutable artifact identity.");
  if(!double.IsFinite(sample.ExposureEv)||sample.Split is not("Training" or "Holdout")||sample.ObserverDegrees is not(2 or 10))errors.Add("Invalid exposure/split/observer.");
  return new(errors.Count==0,false,errors);
 }
}
