using System.IO;
using System.Diagnostics;
using System.Text.Json;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class CanvasPreviewQueueMeasurementTests
{
    sealed class Forbidden:IAssetPreviewProvider{public int Calls;public Task<AssetPreviewResult> GetAsync(AssetPreviewRequest r,CancellationToken t=default){Calls++;throw new InvalidOperationException("owned path must not fall back");}}
    [TestMethod]public Task MeasureOwnedLayoutEventsAndExplicitReloadIntegrity()=>RunSta(async()=>
    {
        var output=Environment.GetEnvironmentVariable("KITAO_CANVAS_QUEUE_EVIDENCE");if(string.IsNullOrWhiteSpace(output)){Assert.Inconclusive("Explicit fresh queue measurement output required");return;}EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var file=Path.Combine(f.Root,"own.jpg");File.Copy(Path.Combine(TestResources.Root,"tests/fixtures/planning-owned-jpeg/orientation-6.jpg"),file);await f.Vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:file)],true);await f.Vm.SendToCanvasAsync(f.Vm.AllProjectReferences.Single());var canvas=f.Vm.ProjectCanvases.Single();var one=canvas.Objects.Single();canvas=canvas with{Objects=Enumerable.Range(0,12).Select(i=>one with{ObjectId=Guid.NewGuid(),X=i*20,Y=i*15}).ToArray()};var editor=new CanvasEditor(canvas);var forbidden=new Forbidden();var surface=new FreeCanvasSurface(editor,forbidden);await surface.LoadPreviewsAsync();Assert.AreEqual(12,surface.LoadedPreviews.Count);editor.SelectAll();await Task.Delay(30);
        var assembly=typeof(FreeCanvasSurface).Assembly;var methods=assembly.GetType("PixelTart.Modules.AssetLibrary.FreeCanvas.OwnedPlanningReferenceImage")!.GetMethods().Single(m=>m.Name=="ReadBounded");var copy=one.PlanningSource!.Bind();var gateType=assembly.GetType("PixelTart.Modules.AssetLibrary.FreeCanvas.OwnedPlanningReferenceImage")!;var gate=(SemaphoreSlim)gateType.GetField("DecodeGate",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;await gate.WaitAsync();
        var started=Stopwatch.StartNew();int maxWorkers=0;using var stop=new CancellationTokenSource();var sampling=Task.Run(async()=>{while(!stop.IsCancellationRequested){maxWorkers=Math.Max(maxWorkers,ThreadPool.ThreadCount);try{await Task.Delay(5,stop.Token);}catch(OperationCanceledException){}}});
        for(int i=0;i<100;i++){editor.Move(1,1);surface.SetZoom(1+(i%4)*.1);}var eventMs=started.Elapsed.TotalMilliseconds;await Task.Delay(300);gate.Release();await surface.LoadPreviewsAsync();await Task.Delay(200);stop.Cancel();await sampling;Assert.AreEqual(0,forbidden.Calls);Assert.AreEqual(12,surface.LoadedPreviews.Count);
        var actual=PlanningProjectCopyStorage.Resolve(copy);var bytes=File.ReadAllBytes(actual);File.WriteAllBytes(actual,bytes.Concat(new byte[]{9}).ToArray());await surface.LoadPreviewsAsync();Assert.IsEmpty(surface.LoadedPreviews);Assert.IsNotEmpty(surface.SourceErrors);var count=typeof(FreeCanvasSurface).GetProperty("OwnedDecodeCount");if(count is not null)Assert.IsTrue((long)count.GetValue(surface)!<=48,"Layout burst must not enqueue one verified decode per event/object");
        Directory.CreateDirectory(output);var p=typeof(FreeCanvasSurface).GetProperty("OwnedDecodeCount");await File.WriteAllTextAsync(Path.Combine(output,"MEASUREMENT.json"),JsonSerializer.Serialize(new{events=200,ownedObjects=12,eventDispatchMs=eventMs,maxThreadPoolThreads=maxWorkers,ownedDecodeCount=p?.GetValue(surface),genericProviderCalls=forbidden.Calls,integrityReloadCleared=true,scope="Synthetic editor move/zoom events and real verified decode; not native dragging"},new JsonSerializerOptions{WriteIndented=true}));
    });
}
