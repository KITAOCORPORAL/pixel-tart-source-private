namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Interactive-only admission. Fine/export never consume this predicate or proxy.</summary>
public static class NodeProxySafety
{
 public const double MeanBudget=1.5,P95Budget=4,MaximumBudget=32;
 public const int Edge=800;
 public static bool SourceHasHighFrequencyBoundary(RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer image)
 {
  var rgb=image.Rgb32.Span;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){int i=(y*image.Width+x)*3;for(int c=0;c<3;c++){if(x+1<image.Width&&Math.Abs(rgb[i+c]-rgb[i+3+c])>.35)return true;if(y+1<image.Height&&Math.Abs(rgb[i+c]-rgb[i+image.Width*3+c])>.35)return true;}}return false;
 }
 public static bool CanApproximate(ProfessionalRawDefinition d,out string reason)
 {
  if(d.ColorState?.Version==2){reason="线性宽域沿既有独立准入";return false;}
  if(d.SceneExposure is not null){reason="场景曝光沿既有独立准入";return false;}
  if(d.Geometry is{QuarterTurns:not 0} or{StraightenDegrees:not 0} or{FlipHorizontal:true}||d.Geometry is{} g&&(g.X!=0||g.Y!=0||g.Width!=1||g.Height!=1)){reason="几何风险";return false;}
  var nodes=d.LocalEdits?.Stack is{} stack?StudioGraphRouting.Nodes(stack):[];
  foreach(var n in nodes.Where(n=>n.Enabled))
  {
   if(n.Type is not(ColorStudioNodeType.BasicTone or ColorStudioNodeType.WhiteBalance or ColorStudioNodeType.Levels or ColorStudioNodeType.Curve or ColorStudioNodeType.ColorBalance or ColorStudioNodeType.SkinTone or ColorStudioNodeType.Preset or ColorStudioNodeType.GraphMix)){reason="空间或未准入工具";return false;}
   if(n.Type==ColorStudioNodeType.Curve&&n.NumericParameters.Any(p=>p.Key.Contains("_y")&&int.TryParse(p.Key[^1..],out var i)&&Math.Abs(p.Value-i/4d)>.15)){reason="强曲线";return false;}
   if(n.Type==ColorStudioNodeType.Levels&&n.NumericParameters.Any(p=>p.Key.EndsWith("black")&&p.Value>.05||p.Key.EndsWith("white")&&p.Value<.95||p.Key.EndsWith("gamma")&&Math.Abs(p.Value-1)>.2)){reason="强色阶";return false;}
   if(n.LocalMask is{} m&&m.Strokes.Any(s=>s.Radius<.02||s.Feather<.3)){reason="细蒙版";return false;}
   if(Math.Abs(n.NumericParameters.GetValueOrDefault("temperature"))>30||Math.Abs(n.NumericParameters.GetValueOrDefault("tint"))>30||Math.Abs(n.NumericParameters.GetValueOrDefault("contrast"))>35||Math.Abs(n.NumericParameters.GetValueOrDefault("highlights"))>40||Math.Abs(n.NumericParameters.GetValueOrDefault("shadows"))>40||Math.Abs(n.NumericParameters.GetValueOrDefault("exposure"))>1){reason="强影调/白平衡";return false;}
  }
  reason="有界点工具图";return true;
 }
}
