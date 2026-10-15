using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Core.Services.ColorScience;
public sealed record PixelRangeStatistics(int Pixels,double Minimum,double Maximum,long[] Negative,long[] AboveOne,long[] Zero,long[] AtOrAboveOne,long NonFinite)
{
 public static PixelRangeStatistics Measure(ReadOnlySpan<float> values)
 {
  if(values.Length%3!=0)throw new ArgumentException("RGB length");var min=double.PositiveInfinity;var max=double.NegativeInfinity;long invalid=0;var negative=new long[3];var over=new long[3];var zero=new long[3];var saturated=new long[3];
  for(int i=0;i<values.Length;i++){var v=values[i];var c=i%3;if(!float.IsFinite(v)){invalid++;continue;}min=Math.Min(min,v);max=Math.Max(max,v);if(v<0)negative[c]++;if(v>1)over[c]++;if(v==0)zero[c]++;if(v>=1)saturated[c]++;}
  return new(values.Length/3,min,max,negative,over,zero,saturated,invalid);
 }
}
public sealed record PixelStageObservation(string Stage,string Domain,string WhitePoint,string Transfer,bool WhiteBalanceAlreadyApplied,PixelRangeStatistics Range,string Authority,string Limitations);
public sealed record VersionedPixelExplanation(int ContractVersion,string InputType,string Decoder,string MatrixSource,string WorkingDomain,string OutputProfile,string ProfileSha256,int RenderingVersion,string SourceSha256,IReadOnlyList<PixelStageObservation> Stages,bool CalibrationEligible=false);
/// <summary>Read-only diagnostics. Does not reprocess PV4/PV5/PV6 or claim sensor values from decoded output.</summary>
public static class PixelPipelineDiagnostics
{
 public const int ContractVersion=1;
 public static VersionedPixelExplanation Explain(ProfessionalRawMaster master,ProfessionalRawDefinition recipe,string outputProfileSha256)
 {
  recipe.Validate();if(master.Contract.SourceSha256.Length!=64||!master.Contract.SourceSha256.All(Uri.IsHexDigit))throw new InvalidDataException("Missing immutable source hash.");
  if(recipe.ProcessingVersion==4 && (master.Image.WorkingColorSpace!=ProfessionalRawPipeline.WorkingSpace||!master.Contract.WhiteBalanceApplied))throw new InvalidDataException("PV4 domain/WB mismatch.");
  var stages=new List<PixelStageObservation>{new("DecodedMaster",master.Image.WorkingColorSpace,recipe.ProcessingVersion==4?"LibRaw D65 convention, NOT ICC ProPhoto D50":"D65",recipe.ProcessingVersion==4?"Linear":"Already encoded sRGB",true,PixelRangeStatistics.Measure(master.Image.Rgb32.Span),"Observed decoded buffer","Sensor/pre-demosaic/pre-matrix extrema unavailable; uint16 decoder clipping already irreversible.")};
  var output=ProfessionalRawPipeline.RenderOutput(master,recipe);stages.Add(new("DisplayOutput",output.WorkingColorSpace,"D65","sRGB OETF already applied exactly once",true,PixelRangeStatistics.Measure(output.Rgb32.Span),"Observed renderer buffer","Statistics at this boundary do not recover saturated raw channels."));
  if(outputProfileSha256.Length!=64||!outputProfileSha256.All(Uri.IsHexDigit))throw new InvalidDataException("Missing output profile identity.");
  return new(1,recipe.ProcessingVersion==4?"PV4 Bayer RAW":recipe.ProcessingVersion==5?"PV5 rendered JPEG":"PV6 legacy rendered RAW",master.Contract.DecoderVersion,master.Contract.Characterization,master.Image.WorkingColorSpace,"Explicit caller-supplied sRGB output profile identity",outputProfileSha256,recipe.ProcessingVersion,master.Contract.SourceSha256,stages);
 }
 public static PixelStageObservation ProbeTransform(ReadOnlySpan<float> input,ColorMatrix3x3 matrix,string stage)
 {
  var output=new float[input.Length];for(int i=0;i<input.Length;i+=3){var v=matrix.Transform(new(input[i],input[i+1],input[i+2]));output[i]=(float)v.X;output[i+1]=(float)v.Y;output[i+2]=(float)v.Z;}
  return new(stage,"Synthetic linear RGB","Explicit fixture","Linear",false,PixelRangeStatistics.Measure(output),"Synthetic controlled probe","Not an observation of LibRaw sensor or camera matrix intermediates.");
 }
}
