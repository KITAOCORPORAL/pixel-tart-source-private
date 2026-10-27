using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class R53LargeBrushRenderTests
{
 [TestMethod]public async Task TwentyFourMpMultilayerLongStrokeActualRenderAndExportMeasurement()
 {
  var root=Environment.GetEnvironmentVariable("KITAO_R53_LARGE_BRUSH_RENDER");if(root is null){Assert.Inconclusive("Explicit large performance output required");return;}Directory.CreateDirectory(root);const int width=6000,height=4000;var data=new float[width*height*3];for(int i=0;i<data.Length;i++)data[i]=.02f+(i%491)*.0015f;using var source=new HighBitDepthImageBuffer(width,height,data,"16");var rows=new List<object>();var requested=Environment.GetEnvironmentVariable("KITAO_R53_BRUSH_CASE");
  foreach(var (name,strokes,points,layers) in new[]{("short",1,20,1),("long",1,2000,1),("multi",20,1000,2)}){
   if(requested is not null&&requested!=name)continue;
   var mask=new StudioLocalMask(Enumerable.Range(0,strokes).Select(n=>new StudioMaskStroke(Enumerable.Range(0,points).Select(i=>new StudioMaskPoint(.05+.9*i/(points-1),.5+.32*Math.Sin(i*6d/(points-1)+n))).ToArray(),.025,.5,.65,.8,n%7==6)).ToArray());var nodes=Enumerable.Range(0,layers).Select(n=>new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"layer"+n,NumericParameters:new Dictionary<string,double>{{"exposure",.25}}){CompositeVersion=1,Opacity=.7,LocalMask=mask.Freeze()}).ToArray();var stack=new ColorAdjustmentStack(nodes,ProcessingVersion:2);GC.Collect();GC.WaitForPendingFinalizers();var before=GC.GetTotalAllocatedBytes(true);var process=Process.GetCurrentProcess();process.Refresh();var baseline=process.WorkingSet64;long peak=baseline;using var stop=new CancellationTokenSource();var sampler=Task.Run(async()=>{while(!stop.IsCancellationRequested){process.Refresh();peak=Math.Max(peak,process.WorkingSet64);try{await Task.Delay(5,stop.Token);}catch(OperationCanceledException){}}});var clock=Stopwatch.StartNew();using var output=BoundedLegacyRawRenderer.Render(source,null,stack,null);var renderMs=clock.Elapsed.TotalMilliseconds;clock.Restart();var path=Path.Combine(root,name+".tif");await AtomicTiffWriter.WriteRgb48Async(path,output,new(TiffBitDepth.Sixteen),overwrite:false);var encodeMs=clock.Elapsed.TotalMilliseconds;stop.Cancel();await sampler;
   rows.Add(new{name,width,height,strokes,points=strokes*points,layers,renderMs,encodeMs,productInputBytes=(long)data.Length*4,productOutputBytes=(long)output.Rgb32.Length*4,baselineWorkingSetBytes=baseline,sampledPeakWorkingSetBytes=peak,allocatedDuringRenderAndEncode=GC.GetTotalAllocatedBytes(true)-before,scope="Actual 24MP render and TIFF encode; source/output retained plus row-band scratch. Sampling5ms, managed runtime/codec overhead included. No full-output extra assertion copy."});File.WriteAllText(Path.Combine(root,"LARGE_BRUSH_RENDER.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
  }
 }
}
