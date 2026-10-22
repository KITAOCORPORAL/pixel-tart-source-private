using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.Services;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class PlanningProjectPackageWpfTests
{
    [TestMethod]public Task UserCommandsExportImportFreshRootReopenResolveCopiesAndSummary()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();var outRoot=Path.GetFullPath(Environment.GetEnvironmentVariable("KITAO_PROJECT_PACKAGE_EVIDENCE")??Path.Combine(f.Root,"package-evidence"));Directory.CreateDirectory(outRoot);
        var dialogs=new Dialogs();var vm=new PlanningCenterViewModel(f.Projects,f.Bookings,dialogs,f.Shots,f.Planning);vm.NewPlanningName="项目包闭环合成项目";await vm.CreatePlanningCommand.ExecuteAsync(null);Assert.IsNotNull(vm.ProjectId);var projectId=vm.ProjectId!.Value;vm.DocumentTitle="项目包闭环合成策划";Assert.IsTrue(await vm.FlushAsync());await vm.NewShotCommand.ExecuteAsync(null);
        var image=Path.Combine(f.Root,"reference.png");await File.WriteAllBytesAsync(image,PlanningRichFlowFixture.ReferencePng(0));var attachment=Path.Combine(f.Root,"call-sheet.txt");await File.WriteAllTextAsync(attachment,"合成通告单，不改变原文件");var ext=Path.Combine(f.Root,"external-only.png");await File.WriteAllBytesAsync(ext,PlanningRichFlowFixture.ReferencePng(1));
        dialogs.Files=[image];dialogs.Answers.Enqueue(true);await vm.AddDocumentImagesCommand.ExecuteAsync(null);dialogs.Files=[attachment];dialogs.Answers.Enqueue(true);await vm.AddAttachmentCommand.ExecuteAsync(null);dialogs.Files=[ext];dialogs.Answers.Enqueue(false);dialogs.Answers.Enqueue(true);await vm.AddDocumentImagesCommand.ExecuteAsync(null);
        var before=await f.Planning.LoadAsync(projectId);var sourceHash=Hash(image);var fileHash=Hash(attachment);var oldPlanning=Path.Combine(f.Planning.StorageRoot,projectId.ToString("N")+".planning.json");var oldHash=Hash(oldPlanning);var refId=before.Document!.References.Single(r=>r.ProjectCopy is not null).Source.ReferenceId;
        dialogs.Save=Path.Combine(outRoot,"synthetic-project.ptplan");dialogs.Answers.Enqueue(true);await vm.ExportProjectPackageCommand.ExecuteAsync(null);Assert.IsTrue(File.Exists(dialogs.Save));StringAssert.Contains(dialogs.Messages.Last(m=>m.Contains("未包含")),"外链");Assert.AreEqual(sourceHash,Hash(image));Assert.AreEqual(fileHash,Hash(attachment));Assert.AreEqual(oldHash,Hash(oldPlanning));
        await vm.ShowCopyRecoveryCommand.ExecuteAsync(null);
        vm.CancelProjectPackageCommand.Execute(null);
        var manifest=PlanningProjectPackage.Validate(dialogs.Save).Manifest;Assert.AreEqual(projectId,manifest.ProjectId);Assert.IsTrue(manifest.Files.All(x=>x.Sha256.Length==64));
        dialogs.Files=[dialogs.Save];dialogs.Folder=Path.Combine(outRoot,"fresh-data-root");dialogs.Answers.Enqueue(true);await vm.ImportProjectPackageCommand.ExecuteAsync(null);Assert.IsNotNull(vm.ImportedPackageRoot);var importedRoot=vm.ImportedPackageRoot;Assert.AreEqual(projectId,vm.ProjectId);
        var view=new PlanningCenterView{DataContext=vm};Assert.IsNotNull(view.FindName("ProjectSearchBox"));await vm.SelectContentSafelyAsync("参考图");view.Render();view.Measure(new Size(1280,900));view.Arrange(new Rect(0,0,1280,900));view.UpdateLayout();view.DataContext=null;
        var reopened=f.NewVm();await reopened.OpenImportedProjectRootAsync(importedRoot!,projectId);Assert.IsFalse(reopened.AllProjectReferences.Single(x=>x.Link.Source.ExternalReference==ext).SourceAvailable,"Imported project does not auto-read excluded external originals.");
        var reference=reopened.AllProjectReferences.Single(x=>x.Id==refId);Assert.IsNotNull(await reopened.ResolveOriginalAsync(reference));Assert.AreEqual(sourceHash,Hash((await reopened.ResolveOriginalAsync(reference))!));var ownedAttachment=reopened.Attachments.Single();Assert.AreEqual(fileHash,Hash(reopened.ResolveAttachmentPath(ownedAttachment)!));reopened.RevealAttachment(ownedAttachment);
        Assert.IsTrue(await reopened.FlushAsync());var summary=Path.Combine(outRoot,"imported-summary.docx");Assert.IsNotNull(await reopened.ExportPlanningSummaryDocxAsync(summary));using(var package=WordprocessingDocument.Open(summary,false)){Assert.IsFalse(new OpenXmlValidator().Validate(package).Any());Assert.IsTrue(package.MainDocumentPart!.ImageParts.Any());StringAssert.Contains(package.MainDocumentPart.Document.InnerText,"call-sheet.txt");}
        dialogs.Folder=Path.Combine(outRoot,"other-import");dialogs.Answers.Enqueue(true);await vm.ImportProjectPackageCommand.ExecuteAsync(null);Assert.AreEqual(oldHash,Hash(oldPlanning));Assert.AreEqual(sourceHash,Hash(image));Assert.AreEqual(fileHash,Hash(attachment));
        // A fresh VM can also open the imported root through the visible user command.
        var openDialogs=new Dialogs{Folder=importedRoot};var opened=new PlanningCenterViewModel(f.Projects,f.Bookings,openDialogs,f.Shots,f.Planning);await opened.OpenImportedProjectCommand.ExecuteAsync(null);Assert.AreEqual(projectId,opened.ProjectId);Assert.IsNotNull(await opened.ResolveOriginalAsync(opened.AllProjectReferences.Single(x=>x.Id==refId)));
        await File.WriteAllTextAsync(Path.Combine(outRoot,"RESULT.json"),JsonSerializer.Serialize(new{project=projectId,document=manifest.DocumentId,referenceId=refId,sourceHash,fileHash,originalPlanningHash=oldHash,package=dialogs.Save,packageSha256=Hash(dialogs.Save),importedRoot,summary,summarySha256=Hash(summary),manifest,closedReopened=true,copyResolved=true,sourceAndOldPlanningUnchanged=true,userCommands=true,scope="Production commands with synthetic dialog decisions, real stores/SQLite, WPF layout, NOT native mouse/Word/WPS"},new JsonSerializerOptions{WriteIndented=true}));Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    });
    static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    sealed class Dialogs:IDialogService
    {
        public IReadOnlyList<string> Files=[];public string? Save,Folder;public Queue<bool> Answers=new();public List<string> Messages=[];
        public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect)=>Files;public string? ChooseFolder(string title,string? initialDirectory=null)=>Folder;public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>Save;public bool Confirm(string message,string title){Messages.Add(message);return Answers.Dequeue();}public void ShowInfo(string message)=>Messages.Add(message);public void ShowError(string message)=>Assert.Fail(message);
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> c)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){Assert.IsTrue(File.Exists(path));}
    }
}
