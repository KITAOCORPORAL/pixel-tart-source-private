using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class R53BrushPerformanceTests
{
 [TestMethod]public void LargeSourceLongStrokeOverlaySamplingMeasurement()
 {
  var path=Environment.GetEnvironmentVariable("KITAO_R53_BRUSH_MEASURE");if(path is null){Assert.Inconclusive("Explicit performance output required");return;}
  var rows=new List<object>();foreach(var count in new[]{1,20}){
   var strokes=Enumerable.Range(0,count).Select(n=>new StudioMaskStroke(Enumerable.Range(0,1000).Select(i=>new StudioMaskPoint(.05+.9*i/999,.1+.8*((Math.Sin(i*.015+n)*.5)+.5))).ToArray(),.025,.5,.5,1,n%7==6)).ToArray();var mask=new StudioLocalMask(strokes);var timer=Stopwatch.StartNew();var sample=mask.Compile(6000d/4000);var compileMs=timer.Elapsed.TotalMilliseconds;timer.Restart();double sum=0;for(int y=0;y<342;y++)for(int x=0;x<512;x++)sum+=sample((x+.5)/512,(y+.5)/342);var elapsed=timer.Elapsed.TotalMilliseconds;rows.Add(new{sourceWidth=6000,sourceHeight=4000,overlayWidth=512,overlayHeight=342,strokes=count,points=count*1000,compileMs,sampleMs=elapsed,sum,peakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64});}
  File.WriteAllText(path,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 }
}
