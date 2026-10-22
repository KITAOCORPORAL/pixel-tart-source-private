using RAWSelectionAssistant.Core.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class PlanningShotExternalReconnectWpfTests
{
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static string Plan(PlanningFoundationWpfTests.Fixture f)=>Path.Combine(f.Planning.StorageRoot,f.Id.ToString("N")+".planning.json");
    [TestMethod]public Task ShotOnlyEntryReconnectRestartPreviewAndPackagePermissionReset()=>RunSta(async()=>
    {
        EnsureTestApplication();using var source=await PlanningFoundationWpfTests.Fixture.Create();using var target=await PlanningFoundationWpfTests.Fixture.Create();
        var output=Path.GetFullPath(Environment.GetEnvironmentVariable("KITAO_SHOT_RECONNECT_EVIDENCE")??Path.Combine(target.Root,"evidence"));Directory.CreateDirectory(output);
        await source.Vm.LoadAsync(source.Id);source.Vm.DocumentTitle="镜头独有外链合成项目";source.Vm.DocumentCustomBody="策划正文不可被重连改写";await source.Vm.NewShotCommand.ExecuteAsync(null);await source.Vm.FlushAsync();
        var shot=(await source.Shots.LoadAsync(source.Id)).Shots.Single();var reference=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Pose,ExternalReference:Path.Combine(source.Root,"missing-shot.png"),Title:"镜头独有参考");
        shot=shot with{Name="合成特写镜头",References=[reference]};await source.Shots.SaveAsync(shot);await source.Vm.LoadAsync(source.Id);Assert.IsEmpty((await source.Planning.LoadAsync(source.Id)).Document!.References);
        var package=Path.Combine(output,"shot-only-v1.ptplan");await source.Vm.ExportProjectPackageAsync(package);await target.Vm.ImportPackageToCurrentRootAsync(package);
        var entry=target.Vm.ExternalDependencies.Single();StringAssert.Contains(entry.Section,"合成特写镜头");StringAssert.Contains(entry.Section,shot.ShotId.ToString());StringAssert.Contains(entry.Reason,"手动定位");Assert.AreEqual(reference.ExternalReference,entry.OriginalPath);Assert.IsFalse(target.Vm.AllProjectReferences.Single().SourceAvailable);
        var image=Path.Combine(output,"replacement-shot.png");await File.WriteAllBytesAsync(image,PlanningRichFlowFixture.ReferencePng(0));var selected=await PlanningExternalFile.InspectAsync(reference.ReferenceId,"reference",reference.ExternalReference!,image);
        var shotPath=Path.Combine(target.Shots.StorageRoot,source.Id.ToString("N")+".shots.json");var shotHash=Hash(shotPath);Assert.IsTrue(await target.Vm.ReconnectExternalAsync(selected,false,expectedShotIds:entry.ShotIds));Assert.IsTrue(target.Vm.AllProjectReferences.Single().SourceAvailable);Assert.IsNotNull(target.Vm.AllProjectReferences.Single().PreviewPath);Assert.IsTrue(await target.Vm.FlushAsync());
        var restarted=target.NewVm();await restarted.ShowOverviewAsync();await restarted.OpenPlanningProjectCommand.ExecuteAsync(restarted.PlanningProjects.Single(p=>p.ProjectId==source.Id));Assert.AreEqual(image,await restarted.ResolveOriginalAsync(restarted.AllProjectReferences.Single()));Assert.IsNotNull(restarted.AllProjectReferences.Single().PreviewPath);Assert.AreEqual(shotHash,Hash(shotPath));Assert.IsEmpty((await target.Planning.LoadAsync(source.Id)).Document!.References);Assert.AreEqual("策划正文不可被重连改写",restarted.DocumentCustomBody);
        // A changed selected file must not fall back to a now-existing old path or resolver.
        await File.WriteAllBytesAsync(reference.ExternalReference!,PlanningRichFlowFixture.ReferencePng(1));
        restarted.ReferencePathResolver=_=>Task.FromResult<string?>(reference.ExternalReference);
        await File.WriteAllBytesAsync(image,PlanningRichFlowFixture.ReferencePng(1));await restarted.RefreshDocumentReferencesAsync();
        Assert.IsFalse(restarted.AllProjectReferences.Single().SourceAvailable);Assert.IsNull(restarted.AllProjectReferences.Single().PreviewPath);Assert.IsNull(await restarted.ResolveOriginalAsync(restarted.AllProjectReferences.Single()));
        await File.WriteAllBytesAsync(image,PlanningRichFlowFixture.ReferencePng(0));await restarted.RefreshDocumentReferencesAsync();
        var externalPackage=Path.Combine(output,"shot-external-approved-v1.ptplan");await restarted.ExportProjectPackageAsync(externalPackage);using(var other=await PlanningFoundationWpfTests.Fixture.Create()){await other.Vm.ImportPackageToCurrentRootAsync(externalPackage);Assert.IsFalse(other.Vm.AllProjectReferences.Single().SourceAvailable);Assert.IsEmpty((await other.Planning.LoadAsync(source.Id)).Document!.ShotExternalLinks);}
        Assert.IsTrue(await restarted.ReconnectExternalAsync(selected,true));var owned=(await target.Planning.LoadAsync(source.Id)).Document!.ShotExternalLinks.Single().ProjectCopy!;Assert.AreEqual(Hash(image),Hash(PlanningProjectCopyStorage.Resolve(owned)));var ownedPackage=Path.Combine(output,"shot-owned-v1.ptplan");await restarted.ExportProjectPackageAsync(ownedPackage);
        using(var other=await PlanningFoundationWpfTests.Fixture.Create()){await other.Vm.ImportPackageToCurrentRootAsync(ownedPackage);Assert.IsTrue(other.Vm.AllProjectReferences.Single().SourceAvailable);var receipt=(await other.Planning.LoadAsync(source.Id)).Document!.ShotExternalLinks.Single();Assert.IsNull(receipt.ReconnectedFile);Assert.IsNotNull(receipt.ProjectCopy);}
        var view=new RAWSelectionAssistant.Views.PlanningCenterView{DataContext=restarted};view.Measure(new Size(1100,800));view.Arrange(new Rect(0,0,1100,800));view.UpdateLayout();view.DataContext=null;
        await File.WriteAllTextAsync(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(new{project=source.Id,shot=shot.ShotId,reference=reference.ReferenceId,original=reference.ExternalReference,replacement=image,shotHash,package,externalPackage,ownedPackage,referenceCount=0,scope="WPF integration/VM recreation; NOT native process restart"},new JsonSerializerOptions{WriteIndented=true}));
    });
    [TestMethod]public Task SharedIdDisplayedOnceConflictDisabledAndCancelOrChangedConfirmationDoesNotWrite()=>RunSta(async()=>
    {
        EnsureTestApplication();using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);await f.Vm.FlushAsync();var shot=(await f.Shots.LoadAsync(f.Id)).Shots.Single();var original=Path.Combine(f.Root,"missing.png");var reference=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.Pose,ExternalReference:original,Title:"共享参考");await f.Shots.SaveAsync(shot with{References=[reference]});await f.Vm.AddDocumentReferencesAsync([reference]);await f.Vm.LoadAsync(f.Id);
        Assert.HasCount(1,f.Vm.ExternalDependencies);var selected=Path.Combine(f.Root,"selected.png");await File.WriteAllBytesAsync(selected,PlanningRichFlowFixture.ReferencePng(0));
        var dialogs=new Decisions();var vm=new PlanningCenterViewModel(f.Projects,f.Bookings,dialogs,f.Shots,f.Planning);await vm.LoadAsync(f.Id);var before=Hash(Plan(f));var entry=vm.ExternalDependencies.Single();await vm.LocateExternalAsync(entry,false);Assert.AreEqual(before,Hash(Plan(f)),"file picker cancel");
        dialogs.File=selected;dialogs.Accept=false;await vm.LocateExternalAsync(entry,true);Assert.AreEqual(before,Hash(Plan(f)),"confirmation cancel");
        dialogs.Accept=true;dialogs.OnConfirm=()=>File.WriteAllBytes(selected,PlanningRichFlowFixture.ReferencePng(1));await vm.LocateExternalAsync(entry,true);Assert.AreEqual(before,Hash(Plan(f)));Assert.IsTrue(dialogs.Errors.Any(e=>e.Contains("SHA-256变化")));
        await f.Shots.SaveAsync(shot with{References=[reference with{ExternalReference=Path.Combine(f.Root,"other.png")}]});await vm.LoadAsync(f.Id);Assert.IsFalse(vm.ExternalDependencies.Single().CanReconnect);Assert.IsFalse(vm.AllProjectReferences.Single().SourceAvailable);
    });
    sealed class Decisions:IDialogService
    {
        public string? File;public bool Accept;public Action? OnConfirm;public List<string> Errors=[];
        public IReadOnlyList<string> ChooseFiles(string t,string f,bool m)=>File is null?[]:[File];public bool Confirm(string m,string t){OnConfirm?.Invoke();return Accept;}public void ShowError(string m)=>Errors.Add(m);public void ShowInfo(string m){}public string? ChooseFolder(string t,string? d=null)=>null;public string? ChooseSaveFile(string t,string f,string e,string? n=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> x)=>x;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> x)=>null;public bool ShowMediaDetails(MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}
    }
}

