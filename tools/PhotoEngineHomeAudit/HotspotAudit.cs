using System.IO;using System.Diagnostics;using System.Text.Json;using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Services;using RAWSelectionAssistant.Core.Services.Projects;
internal static class HotspotAudit
{
 public static async Task Run(string project,string output)
 {
  if(File.Exists(output))throw new IOException("Fresh profile path required");
  var document=await ColorStudioSessionStore.LoadAsync(project);var target=document.Targets.Single(x=>x.Id==document.ActiveTargetId);var definition=target.RawDefinition!;
  SecondExposureDecoder.EnsureRegistered();
  using var trace=StudioPreviewDiagnostics.Begin();var master=await RenderedSrgbInput.ReadAsync(target.Path,definition,CancellationToken.None);
  using var profile=new CandidatePerformanceProfile();var watch=Stopwatch.StartNew();
  using var image=ProfessionalRawPipeline.RenderPreview(master,definition,1600);
  File.WriteAllText(output,JsonSerializer.Serialize(new{elapsedMs=watch.Elapsed.TotalMilliseconds,source=master.Contract.SourceSha256,definition,profile=profile.Results(),io=trace.Observations,width=image.Width,height=image.Height,scope="One sampled x=0 per tile row; sampled stage times not full totals. Downsample spans are full strips; no pixel math changes."},new JsonSerializerOptions{WriteIndented=true}));
 }
}
