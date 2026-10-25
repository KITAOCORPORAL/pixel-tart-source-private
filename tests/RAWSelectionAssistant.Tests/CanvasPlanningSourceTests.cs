using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class CanvasPlanningSourceTests
{
    [TestMethod]public async Task ReceiptPersistsAndRebindsWithoutSerializingTransientCopyRootOrUsingOriginal()
    {
        using var t=new TempDirectory();var store=new PlanningProjectStore(t.Combine("planning"));var project=Guid.NewGuid();var id=Guid.NewGuid();var original=t.CreateFile("source.png",new byte[]{137,80,78,71,13,10,26,10,1});
        await store.ImportProjectCopiesAsync(project,[new(new(id,ShotReferenceKind.General,ExternalReference:original))],[]);var copy=(await store.LoadAsync(project)).Document!.References.Single().ProjectCopy!;
        var canvas=new CanvasDocument{ProjectId=project};canvas=canvas with{Objects=[new(){CanvasId=canvas.CanvasId,Name="原名称",PlanningSource=CanvasPlanningSource.From(copy),ContentHash=copy.Sha256,X=35,Y=60}]};
        var canvases=new CanvasDocumentStore(t.Combine("canvas"));await canvases.SaveAsync(canvas);File.Delete(original);var loaded=(await canvases.LoadAsync(canvas.CanvasId))!;var obj=loaded.Objects.Single();Assert.AreEqual("",obj.SourcePath);Assert.AreEqual("原名称",obj.Name);Assert.AreEqual(35d,obj.X);Assert.AreEqual(id,obj.PlanningSource!.Copy.ReferenceId);Assert.IsNull(obj.PlanningSource.Copy.StorageRoot);
        using(var verified=obj.PlanningSource.OpenVerified())Assert.AreEqual(copy.Bytes,verified.Length);
        await File.WriteAllBytesAsync(PlanningProjectCopyStorage.Resolve(copy),new byte[]{1,2,3});Assert.ThrowsExactly<InvalidDataException>(()=>obj.PlanningSource.OpenVerified());
        File.Delete(PlanningProjectCopyStorage.Resolve(copy));await Assert.ThrowsAsync<FileNotFoundException>(()=>Task.Run(()=>obj.PlanningSource.OpenVerified()));
    }
    [TestMethod]public void ConflictingFallbackAndNonReferenceReceiptAreRejectedBeforeSave()
    {
        var project=Guid.NewGuid();var document=Guid.NewGuid();var id=Guid.NewGuid();var original=Path.GetFullPath("synthetic.png");var copy=new PlanningProjectCopy(project,document,id,"reference",original,"",new string('A',64),10,DateTimeOffset.UtcNow);copy=copy with{RelativePath=PlanningProjectCopyStorage.Relative(copy)};var source=new CanvasPlanningSource(Path.GetTempPath(),copy);var canvas=new CanvasDocument();canvas=canvas with{Objects=[new(){CanvasId=canvas.CanvasId,PlanningSource=source,ContentHash=copy.Sha256,SourcePath=original}]};Assert.ThrowsExactly<InvalidDataException>(()=>CanvasDocumentStore.Validate(canvas));
        Assert.ThrowsExactly<InvalidDataException>(()=>(source with{Copy=copy with{Kind="attachment"}}).Validate());
        Assert.ThrowsExactly<InvalidDataException>(()=>(source with{PlanningRoot="relative"}).Validate());
    }
}
