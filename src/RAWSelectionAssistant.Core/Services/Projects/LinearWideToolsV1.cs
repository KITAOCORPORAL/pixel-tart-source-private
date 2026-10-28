using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Explicit tools for linear ROMM D50. Relative white adaptation is not native camera Kelvin/WB.</summary>
public static class LinearWideToolsV1
{
 private static readonly double[,] RgbToXyz={{.7976749,.1351917,.0313534},{.2880402,.7118741,.0000857},{0,0,.82521}};
 private static readonly double[,] XyzToRgb={{1.3459433,-.2556075,-.0511118},{-.5445989,1.5081673,.0205351},{0,0,1.2118128}};
 private static ColorVector3 Transform(double[,] m,ColorVector3 v)=>new(m[0,0]*v.X+m[0,1]*v.Y+m[0,2]*v.Z,m[1,0]*v.X+m[1,1]*v.Y+m[1,2]*v.Z,m[2,0]*v.X+m[2,1]*v.Y+m[2,2]*v.Z);
 public static bool Supported(ColorAdjustmentStackNode n)=>n.Blend==StudioLayerBlend.Normal&&(n.WideToolVersion==0?n.Type==ColorStudioNodeType.BasicTone&&n.NumericParameters.All(p=>p.Key=="exposure"||p.Value==0):n.WideToolVersion==1&&(n.Type==ColorStudioNodeType.WhiteBalance?n.NumericParameters.All(p=>p.Key is "temperature" or "tint"||p.Value==0):n.Type==ColorStudioNodeType.BasicTone&&n.NumericParameters.All(p=>p.Key is "exposure" or "contrast" or "highlights" or "shadows"||p.Value==0)));
 public sealed class Prepared
 {
  private readonly ColorAdjustmentStackNode node;private readonly double[,]? adaptation;private readonly double exposure,contrast,highlights,shadows;
  public Prepared(ColorAdjustmentStackNode n){if(!Supported(n))throw new NotSupportedException("此线性工作域尚未支持该工具或混合模式。");foreach(var parameter in n.NumericParameters){if(!double.IsFinite(parameter.Value)||Math.Abs(parameter.Value)>(parameter.Key=="exposure"?10:100))throw new InvalidDataException("线性工具参数超出版本范围");}node=n;double P(string key)=>n.NumericParameters.GetValueOrDefault(key);exposure=Math.Pow(2,P("exposure"));contrast=P("contrast")/100;highlights=P("highlights")/100;shadows=P("shadows")/100;
   if(n.Type==ColorStudioNodeType.WhiteBalance&&(P("temperature")!=0||P("tint")!=0)){double x=.3457+Math.Clamp(P("temperature"),-100,100)*.00025,y=.3585+Math.Clamp(P("tint"),-100,100)*.0002;var source=new ColorVector3(x/y,1,(1-x-y)/y);var destination=new ColorVector3(.3457/.3585,1,(1-.3457-.3585)/.3585);using var cms=new LcmsRgbEngine();adaptation=new double[3,3];for(int c=0;c<3;c++){var basis=c==0?new ColorVector3(1,0,0):c==1?new ColorVector3(0,1,0):new ColorVector3(0,0,1);var result=Transform(XyzToRgb,cms.AdaptWhite(Transform(RgbToXyz,basis),source,destination));adaptation[0,c]=result.X;adaptation[1,c]=result.Y;adaptation[2,c]=result.Z;}}
  }
  public ColorVector3 Apply(ColorVector3 v){if(node.Type==ColorStudioNodeType.WhiteBalance)return adaptation is null?v:Transform(adaptation,v);if(node.WideToolVersion==0)return new(v.X*exposure,v.Y*exposure,v.Z*exposure);double y=.2880402*v.X+.7118741*v.Y+.0000857*v.Z;double gain=exposure;if(y>0){double stops=Math.Log2(y/.18),bounded=stops/Math.Sqrt(1+stops*stops);double shadowWeight=1/(1+Math.Pow(y/.18,2)),highlightWeight=y*y/(y*y+.75*.75);double ev=contrast*bounded+2*shadows*shadowWeight+2*highlights*highlightWeight;gain*=Math.Pow(2,ev);}return new(v.X*gain,v.Y*gain,v.Z*gain);}
 }
}
