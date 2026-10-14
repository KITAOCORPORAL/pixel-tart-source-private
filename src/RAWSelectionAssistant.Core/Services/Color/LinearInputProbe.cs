using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.ColorScience;
public sealed record InputRangeProbe(double[] BlackLevels,double WhiteLevel,long[] AtSensorWhite,PixelRangeStatistics BeforeBlack,PixelRangeStatistics AfterBlack,PixelRangeStatistics AfterWhiteBalance,PixelRangeStatistics AfterMatrix,string Authority);
public static class LinearInputProbe
{
 public static InputRangeProbe Measure(ReadOnlySpan<float> sensor,double[] black,double white,ColorVector3 gains,ColorMatrix3x3 cameraMatrix)
 {
  if(sensor.Length%3!=0||black.Length!=3||black.Any(x=>!double.IsFinite(x)||x<0)||!double.IsFinite(white)||black.Any(x=>white<=x)||!double.IsFinite(gains.X)||!double.IsFinite(gains.Y)||!double.IsFinite(gains.Z)||gains.X<=0||gains.Y<=0||gains.Z<=0)throw new ArgumentException("Explicit valid black/white/WB contract required.");
  var adjusted=new float[sensor.Length];var balanced=new float[sensor.Length];var transformed=new float[sensor.Length];var saturated=new long[3];double[] g=[gains.X,gains.Y,gains.Z];
  for(int i=0;i<sensor.Length;i++){if(!float.IsFinite(sensor[i]))throw new ArgumentException("Non-finite input");int c=i%3;if(sensor[i]>=white)saturated[c]++;adjusted[i]=(float)((sensor[i]-black[c])/(white-black[c]));balanced[i]=(float)(adjusted[i]*g[c]);}
  for(int i=0;i<sensor.Length;i+=3){var v=cameraMatrix.Transform(new(balanced[i],balanced[i+1],balanced[i+2]));transformed[i]=(float)v.X;transformed[i+1]=(float)v.Y;transformed[i+2]=(float)v.Z;}
  return new((double[])black.Clone(),white,saturated,PixelRangeStatistics.Measure(sensor),PixelRangeStatistics.Measure(adjusted),PixelRangeStatistics.Measure(balanced),PixelRangeStatistics.Measure(transformed),"Synthetic or explicitly supplied sensor samples only; not LibRaw hidden intermediate observation. Sensor saturation cannot prove recoverable texture.");
 }
}
