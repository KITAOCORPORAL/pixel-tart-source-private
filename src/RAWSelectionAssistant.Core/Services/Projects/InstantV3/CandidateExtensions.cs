using RAWSelectionAssistant.Core.Services.Color;
namespace Kitao.InstantFilm.Research;
public sealed record CandidateCreativeControls(int Version=1,string Stock="Base",double ShadowCast=0,double HighlightWarmth=0,double Fog=0,double TemperatureAppearance=0,double FrameDrift=0,double Fade=0,double LensVignette=0,double DevelopmentEdge=0);
public sealed record CandidatePaperControls(int Version=1,string Pattern="Plain",string Window="Rectangle",double Emboss=0,double EdgeLine=0,double Reflection=0,double ReflectionAngle=0,double ReflectionWidth=.15,double Zoom=1);
public sealed record CandidateDefectControls(int Version=1,string Kind="None",double Amount=0,double Crystal=0,double Leak=0,string Streams="V3IndependentStreams1");
/// <summary>Calculation reproduction from user supplied formula summary; NOT legacy application output or stock chemistry.</summary>
public static class Womb01351Reference
{
 public static ColorVector3 Apply(ColorVector3 c,string identity)
 {
  if(new[]{c.X,c.Y,c.Z}.Any(x=>!double.IsFinite(x)))throw new InvalidDataException("Non-finite legacy formula input");
  var l=.2126*c.X+.7152*c.Y+.0722*c.Z;
  ColorVector3 Mix(double saturation)=>new(l+(c.X-l)*saturation,l+(c.Y-l)*saturation,l+(c.Z-l)*saturation);
  return identity switch
  {
   "Womb01351.Stock0"=>new(Math.Pow(Math.Max(0,c.X),.96)*.94+.032,Math.Pow(Math.Max(0,c.Y),1.035)*.94+.031,Math.Pow(Math.Max(0,c.Z),1.07)*.89+.020),
   "Womb01351.Stock1"=>Affine(Mix(.90),new(.91,.92,.88),new(.026,.031,.036)),
   "Womb01351.Stock2"=>new((c.X-.5)*1.06+.5,(c.Y-.5)*1.06+.5,(c.Z-.5)*1.06+.5),
   "Womb01351.Stock3"=>Scale(new(1,.996,.95),Math.Pow(Math.Max(0,l),.96)*.94+.018),
   "Womb01351.Stock4"=>CandidateTone.Mix(new(.015,.075,.14),new(.67,.87,.89),Math.Pow(Math.Max(0,l),.85)),
   "Womb01351.Stock5"=>Affine(Mix(.87),new(.93,.93,.93),new(.033,.036,.030)),
   _=>throw new InvalidDataException("Unknown Womb calculation identity")
  };
 }
 private static ColorVector3 Scale(ColorVector3 c,double k)=>new(c.X*k,c.Y*k,c.Z*k);
 private static ColorVector3 Affine(ColorVector3 c,ColorVector3 gain,ColorVector3 offset)=>new(c.X*gain.X+offset.X,c.Y*gain.Y+offset.Y,c.Z*gain.Z+offset.Z);
}
public static class CandidateExtensions
{
 public static ColorVector3 Creative(ColorVector3 c,double x,double y,CandidateRecipe recipe,string palette)
 {
  var p=recipe.Creative;if(p is null)return c;double l=CandidateTone.Luma(c);
  if(p.ShadowCast!=0||p.HighlightWarmth!=0){var shadow=(1-l)*(1-l)*p.ShadowCast*.025;var warm=l*l*p.HighlightWarmth*.025;c=new(c.X-shadow+warm,c.Y+shadow*.5,c.Z+shadow-warm);}
  if(p.Fog!=0){var v=p.Fog*.04*(1-l)*(1-l);c=new(c.X+v,c.Y+v,c.Z+v);}
  if(p.TemperatureAppearance!=0&&palette!="NeutralBW"){var t=p.TemperatureAppearance*.04;c=new(c.X+t*(.3+l),c.Y-t*.25,c.Z-t*(.3+l));}
  if(p.FrameDrift!=0){var n=CandidateMaterial.Noise(recipe.Random.RootSeed,.371,.739,7,401)*p.FrameDrift*.004;c=new(c.X+n,c.Y+n*.7,c.Z+n*.3);}
  if(p.Fade!=0){var n=CandidateTone.Luma(c);c=CandidateTone.Mix(c,new(.15+.72*n,.15+.72*n,.15+.72*n),p.Fade);}
  if(p.DevelopmentEdge!=0){double edge=Math.Clamp(4*Math.Min(Math.Min(x,1-x),Math.Min(y,1-y)),0,1),gain=1-p.DevelopmentEdge*.2*(1-edge)*(1-edge);c=new(c.X*gain,c.Y*gain,c.Z*gain);}
  c=Clamp(c);return palette=="NeutralBW"?new(CandidateTone.Luma(c),CandidateTone.Luma(c),CandidateTone.Luma(c)):c;
 }
 public static ColorVector3 Defects(ColorVector3 c,double x,double y,CandidateRecipe recipe,string palette)
 {
  var d=recipe.Defects;if(d is null||d.Amount==0&&d.Crystal==0&&d.Leak==0)return c;
  var seed=recipe.Random.RootSeed;double n=CandidateMaterial.Noise(seed,x,y,9,501),edge=Math.Min(Math.Min(x,1-x),Math.Min(y,1-y));double mask=0;ColorVector3 tint=new(.2,.13,.07);
  if(d.Amount!=0)
  {
   switch(d.Kind)
   {
    case "Unspread":mask=Smooth(.08,.02,y-.05*Math.Sin(x*19)-.03*n);break;
    case "Bands":mask=Math.Exp(-Math.Pow((y-.35-.02*n)/.03,2))*.7+Math.Pow(.5+.5*Math.Sin(y*170+n),18)*.25;break;
    case "Fog":mask=Math.Exp(-x*9)*(.7+.3*n);tint=new(.98,.85,.72);break;
    case "Islands":mask=Smooth(.15,.55,n);break;
    case "Crescent":var r=Math.Sqrt(Math.Pow((x-.3)*1.4,2)+Math.Pow(y-.4,2));mask=Math.Exp(-Math.Pow((r-.12)/.007,2))*(x>.3?1:0);tint=new(.77,.82,.67);break;
    case "BlueFailure":mask=Math.Exp(-Math.Pow((x-.4-.04*n)/.08,2))*Math.Exp(-y*5);tint=new(.055,.18,.36);break;
    case "None":break;
    default:throw new InvalidDataException("Unknown defect version/kind");
   }
   c=CandidateTone.Mix(c,tint,Math.Clamp(mask*d.Amount,0,.85));
  }
  if(d.Crystal!=0){var ridge=1-Math.Abs(CandidateMaterial.Noise(seed,x,y,91,502));var cluster=CandidateMaterial.Noise(seed,x,y,7,503);var m=Smooth(.86,.99,ridge)*Smooth(-.1,.6,cluster)*d.Crystal;c=CandidateTone.Mix(c,new(.87,.89,.84),m*.85);}
  if(d.Leak!=0){var distance=edge+.025*n;var m=Math.Exp(-Math.Pow(distance/.045,2))*d.Leak;var t=distance<.015?new ColorVector3(.035,.12,.27):new(.48,.24,.06);c=CandidateTone.Mix(c,t,Math.Clamp(m,0,.85));}
  c=Clamp(c);return palette=="NeutralBW"?new(CandidateTone.Luma(c),CandidateTone.Luma(c),CandidateTone.Luma(c)):c;
 }
 public static ColorVector3 Surface(ColorVector3 c,double x,double y,CandidateRecipe recipe,bool photo,int width,int height)
 {
  var p=recipe.Paper;if(p is null)return c;double footprint=Math.Max(1d/width,1d/height),frequency=160;var attenuation=Math.Clamp(1-footprint*frequency,0,1);double texture=0;
  if(!photo&&p.Pattern!="Plain")texture=p.Pattern switch{"Leather"=>CandidateMaterial.Noise(recipe.Random.RootSeed,x,y,85,601)*.012,"Dots"=>Math.Cos(x*frequency)*Math.Cos(y*frequency)*.006*attenuation,"Stripes"=>Math.Sin(x*frequency)*.008*attenuation,"Foil"=>(.5+.5*Math.Sin(x*53+y*41))*.014*attenuation,_=>0};
  if(!photo&&p.Emboss!=0)texture+=p.Emboss*.012*Math.Sin(x*120)*Math.Sin(y*120)*attenuation;
  if(texture!=0)c=new(c.X+texture,c.Y+texture,c.Z+texture);
  if(p.EdgeLine!=0){var edge=Math.Min(Math.Min(x,1-x),Math.Min(y,1-y));var line=Math.Exp(-edge*900)*p.EdgeLine*.08;c=new(c.X+line,c.Y+line,c.Z+line);}
  if(p.Reflection!=0){var angle=p.ReflectionAngle*Math.PI/180;var axis=(x-.5)*Math.Cos(angle)+(y-.5)*Math.Sin(angle);var weight=Math.Exp(-Math.Pow(axis/p.ReflectionWidth,2))*p.Reflection*.2;var linear=CandidateTone.Decode(c);c=CandidateTone.Encode(CandidateTone.Mix(linear,new(1,1,1),weight));}
  return Clamp(c);
 }
 private static double Smooth(double a,double b,double v){var t=Math.Clamp((v-a)/(b-a),0,1);return t*t*(3-2*t);}
 private static ColorVector3 Clamp(ColorVector3 c)=>new(Math.Clamp(c.X,0,1),Math.Clamp(c.Y,0,1),Math.Clamp(c.Z,0,1));
}
