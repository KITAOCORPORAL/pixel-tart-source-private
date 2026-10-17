using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research.Tests;
[TestClass]
[DoNotParallelize]
public sealed class OwnershipTests
{
 private static HighBitDepthImageBuffer Input(int w=16,int h=12)=>new(w,h,Enumerable.Repeat(.2f,w*h*3).ToArray(),"16","sRGB",1);
 [TestMethod] public void RetainedOutputsStayChargedAndDisposeIsIdempotent()
 {
  var before=StudioGlobalResources.AccountedBytes;var recipe=CandidateRecipe.Create("modern-bw-v3") with{Surface=new(Frame:false)};
  using var first=CandidateSpatial.RenderRegion(Input(),recipe,0,0,16,12);
  Assert.AreEqual(before+first.Bytes,StudioGlobalResources.AccountedBytes);
  using var second=CandidateRenderer.Render(Input(),recipe);
  Assert.AreEqual(before+first.Bytes+second.Bytes,StudioGlobalResources.AccountedBytes);
  first.Dispose();first.Dispose();Assert.AreEqual(before+second.Bytes,StudioGlobalResources.AccountedBytes);
  Assert.Throws<ObjectDisposedException>(()=>first.Borrow());second.Dispose();Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
 }
 [TestMethod] public void BorrowedTaskKeepsRetiredCacheEntryChargedUntilLastUse()
 {
  var before=StudioGlobalResources.AccountedBytes;using var cache=new CandidateImageCache();
  var first=CandidateImageLease.Adopt(Input());cache.Replace(first);using var active=cache.Borrow();
  var second=CandidateImageLease.Adopt(Input(8,8));cache.Replace(second);
  Assert.AreEqual(before+first.Bytes+second.Bytes,StudioGlobalResources.AccountedBytes);
  Assert.AreEqual(16,active.Image.Width);cache.Dispose();Assert.AreEqual(before+first.Bytes,StudioGlobalResources.AccountedBytes);
  active.Dispose();Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);Assert.Throws<ObjectDisposedException>(()=>active.Image);
 }
 [TestMethod] public void SnapshotDetachesEvidenceAndRandomGeometryFromExternalMutation()
 {
  var recipe=CandidateRecipe.Create("modern-itype-v3");var ids=new[]{"original"};var geometry=recipe.Random.Geometry.ToList();
  recipe=recipe with{Provenance=recipe.Provenance with{SampleIds=ids},Random=recipe.Random with{Geometry=geometry}};
  var frozen=recipe.Freeze();ids[0]="mutated";geometry.Clear();
  Assert.AreEqual("original",frozen.Provenance.SampleIds[0]);Assert.AreNotSame(geometry,frozen.Random.Geometry);
  var list=(IList<string>)frozen.Provenance.SampleIds;Assert.Throws<NotSupportedException>(()=>list[0]="other");
 }
 [TestMethod] public async Task RunningCancellationDoesNotLeakOutputOrScratch()
 {
  var before=StudioGlobalResources.AccountedBytes;using var source=CandidateImageLease.Adopt(Input(1024,1024));using var borrow=source.Borrow();
  using var cancel=new CancellationTokenSource();var task=Task.Run(()=>CandidateRenderer.Render(borrow.Image,CandidateRecipe.Create("modern-itype-v3"),cancel.Token));
  var deadline=System.Diagnostics.Stopwatch.StartNew();while(StudioGlobalResources.AccountedBytes<=before+source.Bytes&&!task.IsCompleted)
  {if(deadline.Elapsed>TimeSpan.FromSeconds(5))Assert.Fail("Task never entered accounted work");await Task.Delay(1);}
  Assert.IsFalse(task.IsCompleted,"Cancellation must happen during work, not before start");cancel.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(async()=>{using var output=await task;});
  Assert.AreEqual(before+source.Bytes,StudioGlobalResources.AccountedBytes);borrow.Dispose();source.Dispose();Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
 }
}
