using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class PlanningOwnedCanvasTests
{
    sealed class ForbiddenProvider:IAssetPreviewProvider
    {public int Calls;public Task<AssetPreviewResult> GetAsync(AssetPreviewRequest request,CancellationToken token=default){Calls++;throw new InvalidOperationException("Owned source must never consult offline cache provider");}}
    sealed class DelayedProvider:IAssetPreviewProvider
    {
        public TaskCompletionSource<AssetPreviewResult> Pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AssetPreviewResult> GetAsync(AssetPreviewRequest request,CancellationToken token=default)=>Pending.Task;
    }
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    static byte[] Pixels(BitmapSource bitmap){var b=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap,System.Windows.Media.PixelFormats.Bgra32,null,0).CopyPixels(b,bitmap.PixelWidth*4,0);return b;}
    [TestMethod]public Task OwnedReferenceSendSaveReopenCacheDeletionMissingChangedAndRepeatedSend()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);f.Vm.DocumentTitle="自有画布合成";var original=Path.Combine(f.Root,"original.jpg");File.Copy(Path.Combine(TestResources.Root,"tests/fixtures/planning-owned-jpeg/orientation-6.jpg"),original);
        await f.Vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:original,Title:"保持原参考标题")],true);await f.Vm.FlushAsync();var reference=f.Vm.AllProjectReferences.Single();var copy=reference.Link.ProjectCopy!;var expected=PlanningOwnedReferenceImage.Read(copy,f.Id,copy.DocumentId,copy.ReferenceId);var cached=reference.PreviewPath!;File.Delete(original);if(File.Exists(cached))File.Delete(cached);if(File.Exists(cached+".sha256"))File.Delete(cached+".sha256");
        await f.Vm.SendToCanvasAsync(reference);await f.Vm.SendToCanvasAsync(reference);Assert.HasCount(2,f.Vm.ProjectCanvases);var canvas=f.Vm.ProjectCanvases.Last();var item=canvas.Objects.Single();Assert.AreEqual(Path.Combine(f.Root,"canvas"),canvas.StorageRoot);Assert.AreEqual("",item.SourcePath);Assert.IsNotNull(item.PlanningSource);Assert.AreEqual(reference.Id,item.PlanningSource.Copy.ReferenceId);Assert.AreEqual("保持原参考标题",item.Name);Assert.AreEqual(0d,item.X);Assert.AreEqual(0d,item.Y);
        var store=new CanvasDocumentStore(Path.Combine(f.Root,"canvas"));var reopened=(await store.LoadAsync(canvas.CanvasId))!;Assert.AreEqual(store.DirectoryPath,reopened.StorageRoot);var forbidden=new ForbiddenProvider();var surface=new FreeCanvasSurface(new(reopened),forbidden);await surface.LoadPreviewsAsync();Assert.HasCount(1,surface.LoadedPreviews);CollectionAssert.AreEqual(Pixels(expected),Pixels(surface.LoadedPreviews.Single().Value));Assert.IsEmpty(surface.SourceErrors);Assert.IsNotNull(PlanningCanvasSnapshot.Render(reopened));
        // A provider with an old cached bitmap must never be consulted for owned sources.
        var actual=PlanningProjectCopyStorage.Resolve(copy);var bytes=File.ReadAllBytes(actual);File.WriteAllBytes(actual,bytes.Concat(new byte[]{3}).ToArray());await surface.LoadPreviewsAsync();Assert.IsEmpty(surface.LoadedPreviews);StringAssert.Contains(surface.SourceErrors[item.ObjectId],"未回退");Assert.ThrowsExactly<InvalidDataException>(()=>PlanningCanvasSnapshot.Render(reopened));
        File.WriteAllBytes(actual,bytes);await surface.LoadPreviewsAsync();Assert.HasCount(1,surface.LoadedPreviews);File.Delete(actual);await surface.LoadPreviewsAsync();Assert.IsEmpty(surface.LoadedPreviews);Assert.HasCount(1,surface.SourceErrors);Assert.AreEqual(0,forbidden.Calls);await store.SaveAsync(reopened);Assert.IsNotNull(await store.LoadAsync(canvas.CanvasId));
        var output=Environment.GetEnvironmentVariable("KITAO_CANVAS_OWNED_EVIDENCE");if(!string.IsNullOrWhiteSpace(output)){Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"OWNED_CANVAS_RESULT.json"),JsonSerializer.Serialize(new{canvas=canvas.CanvasId,reference=reference.Id,source=item.PlanningSource,originalDeleted=true,thumbnailDeleted=true,savedReopened=true,changedAndMissingCleared=true,scope="WPF real stores; not native desktop"},new JsonSerializerOptions{WriteIndented=true}));}
    });
    [TestMethod]public Task DifferentProjectsAndStaleOwnedCardDoNotCrossSourcesOrExpandPackageBoundary()=>RunSta(async()=>
    {
        EnsureTestApplication();using var a=await PlanningFoundationWpfTests.Fixture.Create();using var b=await PlanningFoundationWpfTests.Fixture.Create();
        foreach(var f in new[]{a,b}){await f.Vm.LoadAsync(f.Id);var source=Path.Combine(f.Root,"image.png");await File.WriteAllBytesAsync(source,PlanningRichFlowFixture.ReferencePng(f==a?0:1));await f.Vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:source,Title:f==a?"A蓝橙":"B不同图")],true);}
        var card=a.Vm.AllProjectReferences.Single();await a.Vm.SendToCanvasAsync(card);await b.Vm.SendToCanvasAsync(b.Vm.AllProjectReferences.Single());var ca=a.Vm.ProjectCanvases.Single();var cb=b.Vm.ProjectCanvases.Single();var editor=new CanvasEditor(ca);var surface=new FreeCanvasSurface(editor,new WpfAssetThumbnailProvider());await surface.LoadPreviewsAsync();var pa=Pixels(surface.LoadedPreviews.Single().Value);editor.SetProject(cb.ProjectId);editor.SelectAll();editor.Transform(x=>cb.Objects.Single() with{ObjectId=x.ObjectId,CanvasId=ca.CanvasId});await surface.LoadPreviewsAsync();var pb=Pixels(surface.LoadedPreviews.Single().Value);Assert.IsFalse(pa.SequenceEqual(pb));editor.SetProject(ca.ProjectId);editor.SelectAll();editor.Transform(x=>ca.Objects.Single() with{ObjectId=x.ObjectId,CanvasId=ca.CanvasId});await surface.LoadPreviewsAsync();CollectionAssert.AreEqual(pa,Pixels(surface.LoadedPreviews.Single().Value));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>b.Vm.SendToCanvasAsync(card));var package=Path.Combine(a.Root,"canvas-boundary.ptplan");await a.Vm.ExportProjectPackageAsync(package);var checkedPackage=PlanningProjectPackage.Validate(package);Assert.IsFalse(checkedPackage.Data.Keys.Any(k=>k.Contains("canvas",StringComparison.OrdinalIgnoreCase)));Assert.IsTrue(checkedPackage.Manifest.ExcludedDependencies.Any(x=>x.Contains("自由画布")));
    });
    [TestMethod]public Task LateExternalCompletionCannotOverwriteOwnedSourceAfterSwitch()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var path=Path.Combine(f.Root,"new.png");await File.WriteAllBytesAsync(path,PlanningRichFlowFixture.ReferencePng(0));await f.Vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:path)],true);var copy=f.Vm.AllProjectReferences.Single().Link.ProjectCopy!;
        var document=new CanvasDocument();var item=new CanvasObject{CanvasId=document.CanvasId,SourcePath="old-external.png"};document=document with{Objects=[item]};var editor=new CanvasEditor(document);var provider=new DelayedProvider();var surface=new FreeCanvasSurface(editor,provider);var old=surface.LoadPreviewsAsync();editor.Selection.Add(item.ObjectId);editor.Transform(x=>x with{SourcePath="",PlanningSource=CanvasPlanningSource.From(copy),ContentHash=copy.Sha256});await surface.LoadPreviewsAsync();var expected=Pixels(surface.LoadedPreviews[item.ObjectId]);
        var wrong=PlanningOwnedReferenceImage.Decode(PlanningRichFlowFixture.ReferencePng(1),".png");provider.Pending.SetResult(new(AssetThumbnailState.Available,AssetPreviewPurpose.Canvas,AssetPreviewQuality.High,wrong));await old;CollectionAssert.AreEqual(expected,Pixels(surface.LoadedPreviews[item.ObjectId]));Assert.IsEmpty(surface.SourceErrors);
    });
    [TestMethod]public Task CreateDistinctNativeSyntheticPackages()=>RunSta(async()=>
    {
        var output=Environment.GetEnvironmentVariable("KITAO_CANVAS_OWNED_EVIDENCE");if(string.IsNullOrWhiteSpace(output)){Assert.Inconclusive("Explicit isolated synthetic output required");return;}Directory.CreateDirectory(output);EnsureTestApplication();
        foreach(var index in new[]{0,1})
        {
            using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);f.Vm.DocumentTitle=index==0?"家用JPEG A · 四象限":"家用PNG B · 蓝橙";await f.Vm.NewShotCommand.ExecuteAsync(null);await f.Vm.FlushAsync();var shot=f.Vm.Shots.Single();
            var jpeg=Path.Combine(TestResources.Root,"tests/fixtures/planning-owned-jpeg/orientation-6.jpg");var png=Path.Combine(output,"project-b.png");if(!File.Exists(png))await File.WriteAllBytesAsync(png,PlanningRichFlowFixture.ReferencePng(0));
            await f.Vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:index==0?jpeg:png,Title:index==0?"JPEG方向6 A":"蓝橙PNG B")],true);
            await f.Shots.SaveAsync(shot with{Name=index==0?"镜头A JPEG":"镜头B PNG",References=[new(Guid.NewGuid(),ShotReferenceKind.Pose,ExternalReference:Path.Combine(f.Root,index==0?"missing-shot.jpg":"missing-shot.png"),Title:index==0?"镜头A待定位JPEG":"镜头B待定位PNG")]});await f.Vm.LoadAsync(f.Id);
            await f.Vm.ExportProjectPackageAsync(Path.Combine(output,index==0?"native-project-a-v1.ptplan":"native-project-b-v1.ptplan"));
        }
    });
    [TestMethod]public Task DecoderMovePreservesAllEightCompanyOrientations()=>RunSta(async()=>
    {
        foreach(var i in Enumerable.Range(1,8)){var file=Path.Combine(TestResources.Root,$"tests/fixtures/planning-owned-jpeg/orientation-{i}.jpg");var bytes=File.ReadAllBytes(file);CollectionAssert.AreEqual(Pixels(PlanningOwnedReferenceImage.Decode(bytes,".jpg")),Pixels(OwnedPlanningReferenceImage.Decode(bytes,".jpg")));}await Task.CompletedTask;
    });
}
