using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Controls;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class PlanningProjectCopyWpfTests
{
    [TestMethod] public Task UserChoiceSaveReopenMoveReopenAndSummaryUseOnlyOwnedCopies()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();
        var output=Environment.GetEnvironmentVariable("KITAO_PROJECT_COPY_EVIDENCE")??Path.Combine(f.Root,"evidence");output=Path.GetFullPath(output);Directory.CreateDirectory(output);
        var external=Path.Combine(f.Root,"external");Directory.CreateDirectory(external);var source=Path.Combine(external,"owned-reference.png");await File.WriteAllBytesAsync(source,PlanningRichFlowFixture.ReferencePng(0));
        var attachment=Path.Combine(external,"call-sheet.txt");await File.WriteAllTextAsync(attachment,"合成附件：09:00开拍；源文件不修改。");var externalOnly=Path.Combine(external,"external-only.png");await File.WriteAllBytesAsync(externalOnly,PlanningRichFlowFixture.ReferencePng(1));
        var sourceHash=Hash(source);var attachmentHash=Hash(attachment);var dialog=new Choices();var vm=Vm(f,f.Root,dialog);await vm.LoadAsync(f.Id);await vm.SelectContentSafelyAsync("灯位图");
        dialog.Files=[source];dialog.Answers.Enqueue(true);await vm.AddDocumentImagesCommand.ExecuteAsync(null);
        dialog.Files=[attachment];dialog.Answers.Enqueue(true);await vm.AddAttachmentCommand.ExecuteAsync(null);
        dialog.Files=[externalOnly];dialog.Answers.Enqueue(false);dialog.Answers.Enqueue(true);await vm.AddDocumentImagesCommand.ExecuteAsync(null);
        Assert.AreEqual(3,dialog.ImportPrompts);Assert.IsTrue(await vm.FlushAsync());
        var state=await new PlanningProjectStore(Path.Combine(f.Root,"planning")).LoadAsync(f.Id);var reference=state.Document!.References.Single(r=>r.Source.ExternalReference==source);var file=state.Document.Attachments.Single();
        Assert.IsNotNull(reference.ProjectCopy);Assert.IsNotNull(file.ProjectCopy);Assert.IsNull(state.Document.References.Single(r=>r.Source.ExternalReference==externalOnly).ProjectCopy);Assert.AreEqual(sourceHash,Hash(PlanningProjectCopyStorage.Resolve(reference.ProjectCopy)));Assert.AreEqual(attachmentHash,Hash(PlanningProjectCopyStorage.Resolve(file.ProjectCopy)));
        var refId=reference.Source.ReferenceId;var attachmentId=file.Id;
        Assert.IsTrue(await vm.SaveResourceAsync(new PlanningProp{Name="合成道具"}));Assert.IsTrue(await vm.UndoPlanningAsync());Assert.IsTrue(await vm.RedoPlanningAsync());Assert.IsNotNull(await vm.ResolveOriginalAsync(vm.AllProjectReferences.Single(r=>r.Id==refId)));
        var closed=Vm(f,f.Root,new Choices());await closed.LoadAsync(f.Id);Assert.IsTrue(closed.AllProjectReferences.Single(r=>r.Id==refId).SourceAvailable);Assert.IsNotNull(closed.ResolveAttachmentPath(closed.Attachments.Single()));
        var plan=Path.Combine(output,"complete-synthetic-project");Directory.CreateDirectory(plan);
        // Copy complete synthetic project directory (database and stores), never a personal project.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();CopyTree(f.Root,plan,output);var moved=Path.Combine(output,"relocated-synthetic-project");Directory.Move(plan,moved);
        // Remove synthetic external originals so success cannot be an accidental fallback.
        File.Delete(source);File.Delete(attachment);File.Delete(externalOnly);
        var db=new RAWSelectionAssistant.Core.Services.Database.PixelTartDatabase(Path.Combine(moved,"synthetic.db"));var projects=new RAWSelectionAssistant.Core.Services.Database.SqliteProjectRepository(db);var bookingsRepo=new RAWSelectionAssistant.Core.Services.Database.SqliteShootBookingRepository(db);var bookings=new RAWSelectionAssistant.Core.Services.Bookings.ShootBookingService(bookingsRepo,new RAWSelectionAssistant.Core.Services.Bookings.BookingConflictDetector(bookingsRepo));
        var relocated=new PlanningCenterViewModel(projects,bookings,new Choices(),new ProjectShotStore(Path.Combine(moved,"shots")),new PlanningProjectStore(Path.Combine(moved,"planning")),new ProjectVisualReferenceStore(Path.Combine(moved,"visuals")),new ReferenceLookStore(Path.Combine(moved,"looks")),new CanvasDocumentStore(Path.Combine(moved,"canvas")));
        await relocated.LoadAsync(f.Id);var selected=relocated.AllProjectReferences.Single(r=>r.Id==refId);Assert.IsTrue(selected.SourceAvailable);var resolved=await relocated.ResolveOriginalAsync(selected);Assert.IsNotNull(resolved);Assert.AreEqual(sourceHash,Hash(resolved));Assert.IsFalse(relocated.AllProjectReferences.Single(r=>r.Link.Source.ExternalReference==externalOnly).SourceAvailable);
        var metadata=Path.Combine(moved,"planning",f.Id.ToString("N")+".planning.json");var beforeExport=Hash(metadata);
        Assert.AreEqual(attachmentId,relocated.Attachments.Single().Id);Assert.AreEqual(attachmentHash,Hash(relocated.ResolveAttachmentPath(relocated.Attachments.Single())!));
        var view=new PlanningCenterView{DataContext=relocated};await relocated.SelectContentSafelyAsync("灯位图");view.Render();Layout(view);Assert.IsTrue(((StackPanel)view.FindName("DocumentContent")).Children.Count>0);await relocated.SelectContentSafelyAsync("文件");view.Render();Layout(view);view.DataContext=null;
        var docx=Path.Combine(output,"relocated-project-summary.docx");Assert.IsNotNull(await relocated.ExportPlanningSummaryDocxAsync(docx));
        using(var package=WordprocessingDocument.Open(docx,false))
        {
            Assert.IsFalse(new OpenXmlValidator().Validate(package).Any());var text=package.MainDocumentPart!.Document.InnerText;StringAssert.Contains(text,"项目自有副本");StringAssert.Contains(text,"call-sheet.txt");StringAssert.Contains(text,sourceHash);StringAssert.Contains(text,"缺失文件");
            var drawing=package.MainDocumentPart.Document.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>().Count();Assert.AreEqual(2,drawing,"Same owned reference appears in reference and lighting sections; external-only source never embedded.");
        }
        var imported=PlanningDocxAdapter.Import(docx);PlanningProposalPdf.ExportDocument(imported.Document,Path.Combine(output,"summary-product-layout.pdf"),144);
        Assert.AreEqual(beforeExport,Hash(metadata),"Summary must not mutate saved planning content.");
        var portable=(await new PlanningProjectStore(Path.Combine(moved,"planning")).LoadAsync(f.Id)).Document!;
        await File.WriteAllTextAsync(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(new{project=f.Id,document=portable.Package!.DocumentId,refId,attachmentId,sourceHash,attachmentHash,copies=portable.References.Where(r=>r.ProjectCopy is not null).Select(r=>r.ProjectCopy).Concat(portable.Attachments.Select(a=>a.ProjectCopy)),saveReopen=true,completeSyntheticDirectoryMoved=true,sourceOriginalsDeletedAfterHash=true,externalOnlyStillExternal=true,docxSha256=Hash(docx),scope="Current real stores/SQLite, production choice commands and WPF layout; not native mouse, WPS or general project portability release"},new JsonSerializerOptions{WriteIndented=true}));
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    });
    [TestMethod] public Task CancelExternalChoiceAndMissingImportDoNotAddReferences()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();var dialog=new Choices();var vm=Vm(f,f.Root,dialog);await vm.LoadAsync(f.Id);
        var path=Path.Combine(f.Root,"missing.png");dialog.Files=[path];dialog.Answers.Enqueue(false);dialog.Answers.Enqueue(false);await vm.AddDocumentImagesCommand.ExecuteAsync(null);Assert.HasCount(0,vm.AllProjectReferences);
        dialog.Answers.Enqueue(true);await vm.AddDocumentImagesCommand.ExecuteAsync(null);Assert.HasCount(0,vm.AllProjectReferences);Assert.HasCount(1,dialog.Errors);
        var saved=await f.Planning.LoadAsync(f.Id);Assert.IsTrue(saved.Document is null || saved.Document.References.Count==0);
    });
    [TestMethod] public Task CorruptCopyAndUnsupportedImageAreDisclosedWithoutSourceFallback()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();var vm=f.Vm;await vm.LoadAsync(f.Id);var png=Path.Combine(f.Root,"png.png");await File.WriteAllBytesAsync(png,PlanningRichFlowFixture.ReferencePng(0));var text=Path.Combine(f.Root,"not-png.jpg");await File.WriteAllTextAsync(text,"not an image");
        await vm.AddDocumentReferencesAsync([new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:png),new(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:text)],true);
        var unsupported=vm.AllProjectReferences.Single(r=>r.Link.Source.ExternalReference==text);Assert.IsTrue(unsupported.SourceAvailable);Assert.IsNull(unsupported.PreviewPath);Assert.IsNotNull(await vm.ResolveOriginalAsync(unsupported));
        var doc=(await f.Planning.LoadAsync(f.Id)).Document!;var copy=doc.References.Single(r=>r.Source.ExternalReference==png).ProjectCopy!;await File.WriteAllBytesAsync(PlanningProjectCopyStorage.Resolve(copy),[9,9,9]);await vm.RefreshDocumentReferencesAsync();Assert.IsFalse(vm.AllProjectReferences.Single(r=>r.Id==copy.ReferenceId).SourceAvailable);Assert.IsNull(await vm.ResolveOriginalAsync(vm.AllProjectReferences.Single(r=>r.Id==copy.ReferenceId)));
        var path=Path.Combine(f.Root,"unavailable.docx");var result=await vm.ExportPlanningSummaryDocxAsync(path);Assert.IsNotNull(result);Assert.IsTrue(result.Warnings.Count>=2);
        using var package=WordprocessingDocument.Open(path,false);Assert.AreEqual(0,package.MainDocumentPart!.ImageParts.Count());StringAssert.Contains(package.MainDocumentPart.Document.InnerText,"未读取外部原件");Assert.IsTrue(File.Exists(png));
    });
    static void Layout(System.Windows.FrameworkElement view){view.Measure(new System.Windows.Size(1280,900));view.Arrange(new System.Windows.Rect(0,0,1280,900));view.UpdateLayout();}
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static PlanningCenterViewModel Vm(PlanningFoundationWpfTests.Fixture f,string root,Choices dialogs)=>new(f.Projects,f.Bookings,dialogs,new ProjectShotStore(Path.Combine(root,"shots")),new PlanningProjectStore(Path.Combine(root,"planning")),new ProjectVisualReferenceStore(Path.Combine(root,"visuals")),new ReferenceLookStore(Path.Combine(root,"looks")),new CanvasDocumentStore(Path.Combine(root,"canvas")));
    static void CopyTree(string source,string destination,string exclude)
    {
        Directory.CreateDirectory(destination);foreach(var file in Directory.GetFiles(source))File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));
        foreach(var dir in Directory.GetDirectories(source))if(!Path.GetFullPath(dir).Equals(Path.GetFullPath(exclude),StringComparison.OrdinalIgnoreCase))CopyTree(dir,Path.Combine(destination,Path.GetFileName(dir)),exclude);
    }
    sealed class Choices:IDialogService
    {
        public IReadOnlyList<string> Files=[];public Queue<bool> Answers=new();public List<string> Errors=[];public int ImportPrompts;
        public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect)=>Files;
        public bool Confirm(string message,string title){if(title=="资源归属"){ImportPrompts++;StringAssert.Contains(message,"导入项目副本");}return Answers.Dequeue();}
        public void ShowError(string message)=>Errors.Add(message);public void ShowInfo(string message){}public string? ChooseFolder(string title,string? initialDirectory=null)=>null;public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds)=>currentToolIds;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool showAdvancedDetails)=>false;public void RevealFile(string path){}
    }
}
