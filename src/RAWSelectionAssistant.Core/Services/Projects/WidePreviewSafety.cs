namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Conservative preflight for proxy approximation; unsupported/high-risk edits use exact full-resolution preview.</summary>
public static class WidePreviewSafety
{
 public static bool SourceHasHighFrequencyBoundary(RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer image){var span=image.Rgb32.Span;int step=Math.Max(1,image.PixelCount/32768);for(int p=0;p<image.PixelCount-1;p+=step){if(p%image.Width==image.Width-1)continue;int a=p*3,b=a+3;for(int c=0;c<3;c++)if(Math.Abs(span[a+c]-span[b+c])>.35)return true;}return false;}
 public static bool CanApproximate(ProfessionalRawDefinition d){if(d.LocalEdits?.Stack.Graph?.Version==2)return false;if(d.Geometry is{QuarterTurns:not 0} or{StraightenDegrees:not 0})return false;var nodes=d.LocalEdits?.Stack.Nodes??[];if(nodes.Count>2)return false;foreach(var n in nodes){if(Math.Abs(n.NumericParameters.GetValueOrDefault("temperature"))>30||Math.Abs(n.NumericParameters.GetValueOrDefault("tint"))>30||Math.Abs(n.NumericParameters.GetValueOrDefault("contrast"))>35||Math.Abs(n.NumericParameters.GetValueOrDefault("highlights"))>40||Math.Abs(n.NumericParameters.GetValueOrDefault("shadows"))>40)return false;if(n.LocalMask is{} mask&&mask.Strokes.Any(s=>s.Radius<.02||s.Feather<.3))return false;}return true;}
}
