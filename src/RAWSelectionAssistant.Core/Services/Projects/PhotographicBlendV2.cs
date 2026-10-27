using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Explicit adjustment-result blend in encoded sRGB, NOT a RAW scene blend or Photoshop pixel-layer alpha.
/// New modes clamp the blend inputs to SDR 0..1. Opacity*mask interpolates with original HDR input outside here.
/// Alpha stays owned by the existing caller; no Fill parameter or extra source-alpha multiplication.</summary>
public static class PhotographicBlendV2
{
 public static ColorVector3 Blend(ColorVector3 before,ColorVector3 effect,StudioLayerBlend mode)
 {
  double[] a=[before.X,before.Y,before.Z],b=[effect.X,effect.Y,effect.Z];if(a.Concat(b).Any(x=>!double.IsFinite(x)))throw new InvalidDataException("Nonfinite blend pixel");for(int c=0;c<3;c++){a[c]=Math.Clamp(a[c],0,1);b[c]=Math.Clamp(b[c],0,1);}
  if(mode is StudioLayerBlend.Color or StudioLayerBlend.Luminosity){var v=SetLum(mode==StudioLayerBlend.Color?b:a,Lum(mode==StudioLayerBlend.Color?a:b));return new(v[0],v[1],v[2]);}
  double Channel(double x,double y)=>mode switch{StudioLayerBlend.Overlay=>x<=.5?2*x*y:1-2*(1-x)*(1-y),StudioLayerBlend.SoftLight=>y<=.5?x-(1-2*y)*x*(1-x):x+(2*y-1)*((x<=.25?((16*x-12)*x+4)*x:Math.Sqrt(x))-x),StudioLayerBlend.Darken=>Math.Min(x,y),StudioLayerBlend.Lighten=>Math.Max(x,y),_=>throw new ArgumentException("Only V2 photographic modes")};
  return new(Channel(a[0],b[0]),Channel(a[1],b[1]),Channel(a[2],b[2]));
 }
 private static double Lum(double[] c)=>.3*c[0]+.59*c[1]+.11*c[2];
 private static double[] SetLum(double[] input,double target){var c=input.Select(x=>x+target-Lum(input)).ToArray();double l=Lum(c),n=c.Min(),x=c.Max();if(n<0){double d=l-n;for(int i=0;i<3;i++)c[i]=l+(c[i]-l)*l/d;}if(x>1){double d=x-l;for(int i=0;i<3;i++)c[i]=l+(c[i]-l)*(1-l)/d;}return c;}
}
