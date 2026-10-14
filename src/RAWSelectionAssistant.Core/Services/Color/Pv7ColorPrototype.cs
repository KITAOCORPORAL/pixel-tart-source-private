using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.ColorScience;
public enum Pv7InputDomain { LinearSrgbD65, EncodedSrgbD65 }
public sealed record Pv7Recipe(int ProcessingVersion=7,int AlgorithmVersion=1,Pv7InputDomain InputDomain=Pv7InputDomain.LinearSrgbD65,bool WhiteBalanceAlreadyApplied=true,double ExposureEv=0,double RedGain=1,double GreenGain=1,double BlueGain=1,string OutputProfileId="SyntheticSrgbMatrixTrcV1",string MatrixId="IdentityLinearSrgbD65V1")
{
 [System.Text.Json.Serialization.JsonIgnore] public string CacheKey=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this))));
 public void Validate()
 {
  if(ProcessingVersion!=7||AlgorithmVersion!=1||!Enum.IsDefined(InputDomain)||OutputProfileId!="SyntheticSrgbMatrixTrcV1"||MatrixId!="IdentityLinearSrgbD65V1")throw new NotSupportedException("Unknown PV7 domain, algorithm, matrix or profile.");
  if(!double.IsFinite(ExposureEv)||ExposureEv is <-10 or >10||new[]{RedGain,GreenGain,BlueGain}.Any(x=>!double.IsFinite(x)||x<=0))throw new ArgumentException("Invalid exposure/WB.");
  if(WhiteBalanceAlreadyApplied&&(RedGain!=1||GreenGain!=1||BlueGain!=1))throw new InvalidDataException("WB already baked; repeated raw gains refused.");
 }
}
public sealed record Pv7Result(float[] DisplayEncoded,PixelRangeStatistics Input,PixelRangeStatistics WorkingLinear,PixelRangeStatistics Output,string RecipeKey);
/// <summary>Laboratory-only callable prototype, not wired to project versions, decoder or default UI.</summary>
public static class Pv7ColorPrototype
{
 public static double DecodeSrgb(double x)=>x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);
 public static double EncodeSrgb(double x)=>x<=.0031308?12.92*x:1.055*Math.Pow(x,1/2.4)-.055;
 public static ColorVector3 MapToUnitGamut(ColorVector3 rgb)
 {
  var y=.2126*rgb.X+.7152*rgb.Y+.0722*rgb.Z;if(y<=0)return new(0,0,0);
  var mapped=y<=.75?y:.75+.25*(-Math.Exp(-(y-.75)/.25)+1);var ratio=mapped/y;var r=rgb.X*ratio;var g=rgb.Y*ratio;var b=rgb.Z*ratio;var chroma=1d;
  foreach(var c in new[]{r,g,b}){var delta=c-mapped;if(delta>0)chroma=Math.Min(chroma,(1-mapped)/delta);else if(delta<0)chroma=Math.Min(chroma,-mapped/delta);}
  double C(double c)=>Math.Clamp(mapped+(c-mapped)*chroma,0,1);return new(C(r),C(g),C(b));
 }
 public static Pv7Result Render(ReadOnlySpan<float> input,Pv7Recipe recipe)
 {
  recipe.Validate();if(input.Length%3!=0)throw new ArgumentException("RGB triples required.");var work=new float[input.Length];var output=new float[input.Length];var gain=Math.Pow(2,recipe.ExposureEv);double[] gains=[recipe.RedGain,recipe.GreenGain,recipe.BlueGain];
  for(int i=0;i<input.Length;i++)
  {
   if(!float.IsFinite(input[i]))throw new ArgumentException("Non-finite input refused.");
   if(recipe.InputDomain==Pv7InputDomain.EncodedSrgbD65&&(input[i]<0||input[i]>1))throw new InvalidDataException("Encoded input outside declared range.");
   var linear=recipe.InputDomain==Pv7InputDomain.EncodedSrgbD65?DecodeSrgb(input[i]):input[i];work[i]=(float)(linear*(recipe.WhiteBalanceAlreadyApplied?1:gains[i%3])*gain);
  }
  for(int i=0;i<work.Length;i+=3){var mapped=MapToUnitGamut(new(work[i],work[i+1],work[i+2]));output[i]=(float)EncodeSrgb(mapped.X);output[i+1]=(float)EncodeSrgb(mapped.Y);output[i+2]=(float)EncodeSrgb(mapped.Z);}
  return new(output,PixelRangeStatistics.Measure(input),PixelRangeStatistics.Measure(work),PixelRangeStatistics.Measure(output),recipe.CacheKey);
 }
 public static ushort[] Export16(Pv7Result frozen)=>frozen.DisplayEncoded.Select(x=>(ushort)Math.Clamp(Math.Round(x*65535,MidpointRounding.ToEven),0,65535)).ToArray();
}
public sealed record SyntheticMatrixProfile(string Id,double WhiteX,double WhiteY,double Gamma,string Provenance);
public static class SyntheticProfileBoundary
{
 public static ColorVector3 ToLinearSrgb(ColorVector3 encoded,SyntheticMatrixProfile profile)
 {
  if(profile.Id!="SyntheticLinearProPhotoD50V1"||profile.WhiteX!=.3457||profile.WhiteY!=.3585||profile.Gamma!=1||profile.Provenance!="AnalystSyntheticFixtureV1")throw new NotSupportedException("Only explicit synthetic profile fixture; not general ICC parser.");
  return LinearRgbColorScience.Convert(encoded,WorkingColorSpace.ProPhotoRgb,WorkingColorSpace.LinearSRgb);
 }
}
