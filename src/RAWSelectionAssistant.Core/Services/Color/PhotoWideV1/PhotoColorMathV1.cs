namespace RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;
public enum PhotoPrimariesV1 { SrgbBt709D65, Bt2020D65, DaVinciWideGamutD65, AcesAp1D60 }
public enum PhotoTransferV1 { Linear, Srgb, Bt709Oetf, Bt2020Oetf, DaVinciIntermediate, AcesCct, PqNits }
public enum CdlClampV1 { ClampSopAndOutput, NoClampNonNegativePower }
public sealed record PhotoColorDomainV1(PhotoPrimariesV1 Primaries,PhotoTransferV1 Transfer,string Version="PhotoWideV1",string LuminanceUnit="RelativeLinear")
{
 public PhotoColorDomainV1 Validate(){if(Version!="PhotoWideV1"||!Enum.IsDefined(Primaries)||!Enum.IsDefined(Transfer)||Transfer==PhotoTransferV1.AcesCct&&Primaries!=PhotoPrimariesV1.AcesAp1D60||Transfer==PhotoTransferV1.DaVinciIntermediate&&Primaries!=PhotoPrimariesV1.DaVinciWideGamutD65||Transfer==PhotoTransferV1.PqNits&&LuminanceUnit!="cd/m2")throw new InvalidDataException("Unsupported photo colour identity");return this;}
}
/// <summary>Explicit photo-math library, not connected to historical display-sRGB V3 and never relabels clipped RAW.</summary>
public static class PhotoColorMathV1
{
 public static readonly ColorMatrix3x3 DwgToXyz=new(.70062239,.14877482,.10105872,.27411851,.87363190,-.14775041,-.09896291,-.13789533,1.32591599);
 public static readonly ColorMatrix3x3 XyzToDwg=new(1.51667204,-.28147805,-.14696363,-.46491710,1.25142378,.17488461,.06484905,.10913934,.76141462);
 public static readonly ColorMatrix3x3 SrgbToXyz=new(.4123907992659595,.357584339383878,.1804807884018343,.2126390058715104,.715168678767756,.0721923153607337,.0193308187155919,.1191947797946259,.9505321522496607);
 public static readonly ColorMatrix3x3 Bt2020ToXyz=new(.6369580483012914,.1446169035862083,.1688809751641721,.2627002120112671,.6779980715188708,.059301716469862,.0,.0280726930490874,1.060985057710791);
 public static readonly ColorMatrix3x3 Ap1ToXyzD60=new(.6624541811,.1340042065,.156187687,.2722287168,.6740817658,.0536895174,-.0055746495,.0040607335,1.0103391003);
 private static void Finite(double x){if(!double.IsFinite(x))throw new ArgumentOutOfRangeException(nameof(x),"Finite colour sample required");}
 public static double EncodeDi(double x){Finite(x);return x>.00262409?(Math.Log2(x+.0075)+7)*.07329248:x*10.44426855;}
 public static double DecodeDi(double x){Finite(x);return x>.02740668?Math.Pow(2,x/.07329248-7)-.0075:x/10.44426855;}
 public static double EncodeAcesCct(double x){Finite(x);return x<=.0078125?10.5402377416545*x+.0729055341958355:(Math.Log2(x)+9.72)/17.52;}
 public static double DecodeAcesCct(double x){Finite(x);return x<=.155251141552511?(x-.0729055341958355)/10.5402377416545:Math.Pow(2,17.52*x-9.72);}
 public static double EncodeSrgb(double x){Finite(x);return x<=.0031308?12.92*x:1.055*Math.Pow(x,1/2.4)-.055;}
 public static double DecodeSrgb(double x){Finite(x);return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
 public static double Encode709(double x){Finite(x);return x<.018?4.5*x:1.099*Math.Pow(x,.45)-.099;}
 public static double Decode709(double x){Finite(x);return x<.081?x/4.5:Math.Pow((x+.099)/1.099,1/.45);}
 public static double Encode2020(double x){Finite(x);const double a=1.0993,b=.0181;return x<b?4.5*x:a*Math.Pow(x,.45)-(a-1);}
 public static double Decode2020(double x){Finite(x);const double a=1.0993,b=.0181;return x<4.5*b?x/4.5:Math.Pow((x+a-1)/a,1/.45);}
 public static double EncodePqNits(double nits){Finite(nits);if(nits<0||nits>10000)throw new ArgumentOutOfRangeException(nameof(nits));double y=Math.Pow(nits/10000,2610d/16384);return Math.Pow((3424d/4096+2413d/128*y)/(1+2392d/128*y),2523d/32);}
 public static double DecodePqNits(double encoded){Finite(encoded);if(encoded<0||encoded>1)throw new ArgumentOutOfRangeException(nameof(encoded));double p=Math.Pow(encoded,32d/2523);return 10000*Math.Pow(Math.Max(p-3424d/4096,0)/(2413d/128-2392d/128*p),16384d/2610);}
 public static ColorVector3 Cdl(ColorVector3 input,ColorVector3 slope,ColorVector3 offset,ColorVector3 power,double saturation,CdlClampV1 mode)
 {
  foreach(var x in new[]{input.X,input.Y,input.Z,slope.X,slope.Y,slope.Z,offset.X,offset.Y,offset.Z,power.X,power.Y,power.Z,saturation})Finite(x);
  if(!Enum.IsDefined(mode)||power.X<=0||power.Y<=0||power.Z<=0||saturation<0)throw new ArgumentException("ASC-CDL v1.2 explicit mode/domain");
  double Apply(double x,double s,double o,double p){double v=x*s+o;if(mode==CdlClampV1.ClampSopAndOutput)v=Math.Clamp(v,0,1);else if(v<0)return v;return Math.Pow(v,p);}
  var c=new ColorVector3(Apply(input.X,slope.X,offset.X,power.X),Apply(input.Y,slope.Y,offset.Y,power.Y),Apply(input.Z,slope.Z,offset.Z,power.Z));if(saturation==1)return mode==CdlClampV1.ClampSopAndOutput?new(Math.Clamp(c.X,0,1),Math.Clamp(c.Y,0,1),Math.Clamp(c.Z,0,1)):c;double l=.2126*c.X+.7152*c.Y+.0722*c.Z;var result=new ColorVector3(l+saturation*(c.X-l),l+saturation*(c.Y-l),l+saturation*(c.Z-l));return mode==CdlClampV1.ClampSopAndOutput?new(Math.Clamp(result.X,0,1),Math.Clamp(result.Y,0,1),Math.Clamp(result.Z,0,1)):result;
 }
}
