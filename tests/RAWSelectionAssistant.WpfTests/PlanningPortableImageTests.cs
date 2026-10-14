using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using DocumentFormat.OpenXml.Packaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningPortableImageTests
{
    private static void CopyTree(string source,string destination)
    {
        Directory.CreateDirectory(destination);
        foreach(var file in Directory.EnumerateFiles(source))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));
        foreach(var directory in Directory.EnumerateDirectories(source))CopyTree(directory,Path.Combine(destination,Path.GetFileName(directory)));
    }
    private static PlanningCenterViewModel Reopen(PlanningFoundationWpfTests.Fixture f,string root)=>new(f.Projects,f.Bookings,f.Dialogs,new ProjectShotStore(f.ShotDirectory),new PlanningProjectStore(root),new ProjectVisualReferenceStore(Path.Combine(f.Root,"visuals")),new ReferenceLookStore(Path.Combine(f.Root,"looks")),new CanvasDocumentStore(Path.Combine(f.Root,"canvas")));
    private static string[] Positions(FlowDocument doc)=>PlanningDocumentImages.Images(doc).Select(i=>PlanningDocumentImages.Position(i)).ToArray();
    [TestMethod]
    public Task RealRootMoveKeepsRepeatedPicturesCellCaptionsModesHistoryPaginationAndDocx()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);
        var bytes=PlanningRichFlowFixture.ReferencePng(1);var a=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,bytes,null,"参考图","合成A","说明A",0);var b=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,a.DocumentId,bytes,null,"情绪板","合成B","说明B",1);
        Assert.AreNotEqual(a.DocumentImageId,b.DocumentImageId);Assert.IsTrue(await f.Vm.InsertSimpleImagesAsync([a,b]));
        var doc=PlanningDocumentLayout.Load(f.Vm.RichTextPackage!,f.Vm.RichLayout);doc.Blocks.Add(PlanningR7TableTests.Make(2,2));
        var editor=new PlanningRichEditor(PlanningDocumentImages.Serialize(doc),"",f.Vm.SaveRichText,[],PlanningDocumentLayout.Capture(doc,new(),[]));var box=editor.Children.OfType<RichTextBox>().Single();var cell=box.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[1];box.CaretPosition=cell.ContentStart.GetInsertionPosition(LogicalDirection.Forward)!;
        var c=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,a.DocumentId,bytes,null,"光标","表格图片","表格图注",2);editor.InsertOwnedImages([c],editor.CaptureImageInsertion());editor.FlushPending();Assert.IsTrue(await f.Vm.FlushAsync());
        var expected=Positions(box.Document);var ids=f.Vm.DocumentImages.Select(i=>i.DocumentImageId).ToArray();var old=Path.Combine(f.Root,"planning");var moved=Path.Combine(f.Root,"moved-planning");CopyTree(old,moved);
        // End use of the source VM/editor before taking the original root away.
        editor.FlushPending();Assert.IsTrue(await f.Vm.FlushAsync());Directory.Move(old,Path.Combine(f.Root,"withdrawn-original-planning"));Assert.IsFalse(Directory.Exists(old));
        var reopened=Reopen(f,moved);await reopened.LoadAsync(f.Id);CollectionAssert.AreEqual(ids,reopened.DocumentImages.Select(i=>i.DocumentImageId).ToArray());Assert.IsTrue(reopened.DocumentImages.All(i=>!Path.IsPathRooted(i.CopyPath)));
        var restored=PlanningDocumentLayout.Load(reopened.RichTextPackage!,reopened.RichLayout);Assert.HasCount(3,PlanningDocumentImages.Images(restored));CollectionAssert.AreEqual(expected,Positions(restored));StringAssert.Contains(new TextRange(restored.ContentStart,restored.ContentEnd).Text,"表格图注");
        Assert.IsTrue(await reopened.SwitchPlanningModeAsync(true));Assert.IsTrue(await reopened.SwitchPlanningModeAsync(false));
        Assert.IsTrue(await reopened.EditDocumentImageAsync(c.DocumentImageId,"改表格标题","改图注",width:130));Assert.IsTrue(await reopened.UndoPlanningAsync());Assert.AreEqual("表格图片",reopened.DocumentImages.Single(i=>i.DocumentImageId==c.DocumentImageId).Title);Assert.IsTrue(await reopened.RedoPlanningAsync());
        var historyDoc=PlanningDocumentLayout.Load(reopened.RichTextPackage!,reopened.RichLayout);var rich=new PlanningRichEditor(reopened.RichTextPackage!,"",reopened.SaveRichText,[],reopened.RichLayout);var richBox=rich.Children.OfType<RichTextBox>().Single();var count=PlanningDocumentImages.Images(richBox.Document).Count();
        richBox.CaretPosition=richBox.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward)!;var extra=PlanningDocumentImages.Copy(reopened.DocumentImageDirectory,a.DocumentId,bytes,null,"光标","历史测试","图注",3);rich.InsertOwnedImages([extra],rich.CaptureImageInsertion());rich.FlushPending();
        System.Windows.Input.ApplicationCommands.Undo.Execute(null,rich);Assert.AreEqual(count,PlanningDocumentImages.Images(richBox.Document).Count());System.Windows.Input.ApplicationCommands.Redo.Execute(null,rich);Assert.AreEqual(count+1,PlanningDocumentImages.Images(richBox.Document).Count());Assert.IsTrue(await reopened.FlushAsync());
        var paginator=new PlanningPaginator(PlanningDocumentLayout.Load(reopened.RichTextPackage!,reopened.RichLayout));paginator.ComputePageCount();Assert.IsTrue(paginator.PageCount>0);for(int i=0;i<paginator.PageCount;i++)Assert.AreNotEqual(DocumentPage.Missing,paginator.GetPage(i));
        var output=Path.Combine(f.Root,"moved.docx");Assert.IsTrue(await reopened.ExportPlanningDocxAsync(output));using(var package=WordprocessingDocument.Open(output,false)){Assert.HasCount(4,package.MainDocumentPart!.ImageParts);Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(package));Assert.IsTrue(package.MainDocumentPart.Document.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().Any(t=>t.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>().Any()));}
        var exported=PlanningDocxAdapter.Import(output);Assert.HasCount(4,PlanningDocumentImages.Images(exported.Document));StringAssert.Contains(new TextRange(exported.Document.ContentStart,exported.Document.ContentEnd).Text,"改图注");
        await reopened.LoadAsync(f.Id);Assert.HasCount(4,reopened.DocumentImages);Assert.IsFalse(Directory.Exists(old));
        var evidence=Environment.GetEnvironmentVariable("KITAO_PORTABLE_IMAGE_EVIDENCE");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);await File.WriteAllTextAsync(Path.Combine(evidence,"relative-after.json"),JsonSerializer.Serialize(new{DocumentId=a.DocumentId,Images=reopened.DocumentImages.Select(i=>new{i.DocumentImageId,i.CopyPath,i.CopyHash,i.Position,i.Title,i.Caption})},new JsonSerializerOptions{WriteIndented=true}));File.Copy(output,Path.Combine(evidence,"moved-synthetic.docx"),true);}
    });
    [TestMethod]
    public Task LegacyAbsoluteRootMoveAndLockedMigrationReplayNeverReplaceWrongImage()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var image=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,PlanningRichFlowFixture.ReferencePng(0),null,"正文","旧绝对图","图注",0);Assert.IsTrue(await f.Vm.InsertSimpleImagesAsync([image]));
        var old=Path.Combine(f.Root,"planning");var file=Path.Combine(old,$"{f.Id:N}.planning.json");var tree=JsonNode.Parse(await File.ReadAllTextAsync(file))!;tree["Document"]!["Package"]!["RichLayout"]!["Images"]![0]!["CopyPath"]=PlanningImageStorage.Resolve(image);await File.WriteAllTextAsync(file,tree.ToJsonString());
        var validLegacy=await new PlanningProjectStore(old).LoadAsync(f.Id);Assert.AreEqual(image.CopyPath,validLegacy.Document!.Package!.RichLayout!.Images[0].CopyPath);
        var moved=Path.Combine(f.Root,"legacy-moved");CopyTree(old,moved);Directory.Move(old,Path.Combine(f.Root,"old-retired"));var movedFile=Path.Combine(moved,Path.GetFileName(file));var before=await File.ReadAllBytesAsync(movedFile);
        var vm=Reopen(f,moved);await vm.LoadAsync(f.Id);Assert.AreEqual(image.CopyPath,vm.DocumentImages.Single().CopyPath);CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(movedFile));
        using(var lockFile=new FileStream(movedFile,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){vm.DocumentTitle="迁移中的草稿";Assert.IsFalse(await vm.FlushAsync());}
        CollectionAssert.AreEqual(before,await File.ReadAllBytesAsync(movedFile));var draft=await new PlanningProjectStore(moved).LoadDraftAsync(f.Id);Assert.AreEqual(image.CopyPath,draft!.Document.Package!.RichLayout!.Images.Single().CopyPath);
        var replay=Reopen(f,moved);await replay.LoadAsync(f.Id);Assert.AreEqual("迁移中的草稿",replay.DocumentTitle);Assert.IsTrue(await replay.FlushAsync());
        using(var json=JsonDocument.Parse(await File.ReadAllTextAsync(movedFile)))Assert.AreEqual(image.CopyPath,json.RootElement.GetProperty("Document").GetProperty("Package").GetProperty("RichLayout").GetProperty("Images")[0].GetProperty("CopyPath").GetString());
        var copy=PlanningImageStorage.Resolve(replay.DocumentImages.Single());var verified=await File.ReadAllBytesAsync(copy);File.WriteAllBytes(copy,[1,2,3]);var error=await Assert.ThrowsAsync<InvalidDataException>(()=>new PlanningProjectStore(moved).LoadAsync(f.Id));StringAssert.Contains(error.Message,image.DocumentImageId.ToString("D"));await File.WriteAllBytesAsync(copy,verified);File.Delete(copy);
        var missing=await Assert.ThrowsAsync<InvalidDataException>(()=>Reopen(f,moved).LoadAsync(f.Id));StringAssert.Contains(missing.Message,image.Title);
        var evidence=Environment.GetEnvironmentVariable("KITAO_PORTABLE_IMAGE_EVIDENCE");if(!string.IsNullOrWhiteSpace(evidence)){Directory.CreateDirectory(evidence);await File.WriteAllTextAsync(Path.Combine(evidence,"legacy-before.json"),System.Text.Encoding.UTF8.GetString(before));await File.WriteAllTextAsync(Path.Combine(evidence,"legacy-after.json"),await File.ReadAllTextAsync(movedFile));}
    });
    [TestMethod]
    public Task UndoRedoAndDiskRecoveryCopiesAreListedButNeverDeleted()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);var meta=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,f.Vm.PlanningDocumentId!.Value,PlanningRichFlowFixture.ReferencePng(0),null,"正文","可重做图片","",0);Assert.IsTrue(await f.Vm.InsertSimpleImagesAsync([meta]));Assert.IsTrue(await f.Vm.UndoPlanningAsync());Assert.IsEmpty(f.Vm.DocumentImages);
        var inventory=f.Vm.ListUnreferencedImageCopies();Assert.IsFalse(inventory.DeletionAllowed);Assert.IsTrue(inventory.Files.Any(i=>i.RelativePath==meta.CopyPath&&i.Disposition=="REFERENCED_OR_RECOVERY_KEEP"));Assert.IsTrue(File.Exists(PlanningImageStorage.Resolve(meta)));Assert.IsTrue(await f.Vm.RedoPlanningAsync());
        var orphan=PlanningDocumentImages.Copy(f.Vm.DocumentImageDirectory,meta.DocumentId,PlanningRichFlowFixture.ReferencePng(2),null,"正文","孤立测试副本","",1);inventory=f.Vm.ListUnreferencedImageCopies();Assert.IsTrue(inventory.Files.Any(i=>i.RelativePath==orphan.CopyPath&&i.Disposition=="UNREFERENCED_CANDIDATE_KEEP"));Assert.IsTrue(File.Exists(PlanningImageStorage.Resolve(orphan)));
        await f.Vm.ExportPlanningDocxAsync(Path.Combine(f.Root,"recovery.docx"));var review=f.Vm.PreviewPlanningDocxImport(Path.Combine(f.Root,"recovery.docx"));Assert.IsTrue(await f.Vm.ApplyPlanningDocxImportAsync(review,[],true));Assert.IsTrue(f.Vm.ListUnreferencedImageCopies().Files.Any(i=>i.RelativePath==meta.CopyPath&&i.Disposition=="REFERENCED_OR_RECOVERY_KEEP"));
    });
}
