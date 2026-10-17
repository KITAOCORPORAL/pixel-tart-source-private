using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research.Tests;
[TestClass][DoNotParallelize]
public sealed class ProductOwnershipTests
{
 [TestMethod] public void RegisteredMasterBorrowDoesNotDuplicateChargeAndRetiresUntilTaskCompletes()
 {
  var before=StudioGlobalResources.AccountedBytes;var image=new HighBitDepthImageBuffer(10,8,new float[240],"16","sRGB");var owner=Guid.NewGuid();var bytes=240L*4;
  Assert.IsTrue(StudioGlobalResources.Store(owner,"Master",bytes,()=>{}));StudioGlobalResources.BindRetainedBuffer(image,owner,"Master");
  using var first=StudioGlobalResources.RetainBuffer(image,bytes);using var second=StudioGlobalResources.RetainBuffer(image,bytes);Assert.AreEqual(before+bytes,StudioGlobalResources.AccountedBytes);
  StudioGlobalResources.Release(owner,"Master");Assert.AreEqual(before+bytes,StudioGlobalResources.AccountedBytes);first.Dispose();Assert.AreEqual(before+bytes,StudioGlobalResources.AccountedBytes);second.Dispose();Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
 }
 [TestMethod] public void TransferredPreviewIsChargedUntilExplicitBufferDispose()
 {
  var before=StudioGlobalResources.AccountedBytes;using var source=CandidateImageLease.Adopt(new(16,12,Enumerable.Repeat(.2f,576).ToArray(),"16","sRGB"));using var session=new CandidateRenderSession(source,CandidateRecipe.Create("modern-itype-v3"));using var rendered=session.RenderPreview(8);var image=rendered.TransferToBuffer();var expected=before+source.Bytes+image.Rgb32.Length*4L;Assert.AreEqual(expected,StudioGlobalResources.AccountedBytes);rendered.Dispose();Assert.AreEqual(expected,StudioGlobalResources.AccountedBytes);image.Dispose();Assert.AreEqual(before+source.Bytes,StudioGlobalResources.AccountedBytes);Assert.Throws<ObjectDisposedException>(()=>image.Rgb32);
 }
 [TestMethod] public async Task ConcurrentSessionCloseDoesNotReleaseActiveSourceAndPreCancelDoesNotReplaceRunningTest()
 {
  var before=StudioGlobalResources.AccountedBytes;using var source=CandidateImageLease.Adopt(new(800,600,Enumerable.Repeat(.2f,800*600*3).ToArray(),"16","sRGB"));var session=new CandidateRenderSession(source,CandidateRecipe.Create("modern-itype-v3"));
  var task=Task.Run(()=>session.RenderRegion(0,0,session.Layout.Width,session.Layout.Height));var timer=System.Diagnostics.Stopwatch.StartNew();while(StudioGlobalResources.AccountedBytes<=before+source.Bytes&&!task.IsCompleted){if(timer.Elapsed>TimeSpan.FromSeconds(5))Assert.Fail("No active work");await Task.Delay(1);}
  Assert.IsFalse(task.IsCompleted);session.Dispose();source.Dispose();Assert.IsTrue(StudioGlobalResources.AccountedBytes>before);using var result=await task;result.Dispose();Assert.AreEqual(before,StudioGlobalResources.AccountedBytes);
 }
 [TestMethod] public void OriginalComparisonPreservesZoomCircleAndPaperGeometry()
 {
  var r=CandidateRecipe.Create("modern-itype-v3") with{Paper=new(Version:2,Window:"Circle",Zoom:2,Reflection:1,Emboss:1),Creative=new(Fog:1)};
  var definition=new ProfessionalRawDefinition(){InstantFilm=new(true,AlgorithmVersion:3){V3=r}};
  var original=InstantV3ProductPipeline.Original(definition);Assert.AreEqual(0d,original.InstantFilm!.V3!.Amount);Assert.AreEqual(2d,original.InstantFilm.V3.Paper!.Zoom);Assert.AreEqual("Circle",original.InstantFilm.V3.Paper.Window);Assert.AreEqual(0d,original.InstantFilm.V3.Paper.Reflection);
 }
}
