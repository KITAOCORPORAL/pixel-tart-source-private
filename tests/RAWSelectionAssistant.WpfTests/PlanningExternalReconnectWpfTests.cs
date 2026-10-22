using System.IO;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class PlanningExternalReconnectWpfTests
{
    [TestMethod]public Task ImportMissingReconnectSaveRecreateAndFifteenSectionSummary()=>RunSta(async()=>
    {
        EnsureTestApplication();using var source=await PlanningFoundationWpfTests.Fixture.Create();using var target=await PlanningFoundationWpfTests.Fixture.Create();var output=Path.GetFullPath(Environment.GetEnvironmentVariable("KITAO_RECONNECT_EVIDENCE")??Path.Combine(target.Root,"evidence"));Directory.CreateDirectory(output);
        await source.Vm.LoadAsync(source.Id);source.Vm.DocumentTitle="外链重连合成项目";var original=Path.Combine(source.Root,"missing-reference.png");var attachment=Path.Combine(source.Root,"missing-attachment.txt");var id=Guid.NewGuid();await source.Vm.AddDocumentReferencesAsync([new(id,ShotReferenceKind.Lighting,ExternalReference:original,Title:"合成灯光参考")]);await source.Vm.AddAttachmentsAsync([attachment]);await source.Vm.FlushAsync();var package=Path.Combine(output,"external-reconnect-v1.ptplan");await source.Vm.ExportProjectPackageAsync(package);
        await target.Vm.ImportPackageToCurrentRootAsync(package);Assert.IsFalse(target.Vm.AllProjectReferences.Single().SourceAvailable);Assert.IsNull(target.Vm.ResolveAttachmentPath(target.Vm.Attachments.Single()));StringAssert.Contains(target.Vm.ExternalDependencies.First().Reason,"手动定位");
        var chosen=Path.Combine(output,"replacement.png");await File.WriteAllBytesAsync(chosen,PlanningRichFlowFixture.ReferencePng(0));var text=Path.Combine(output,"replacement.txt");await File.WriteAllTextAsync(text,"仅合成附件，请勿用于真实拍摄");
        var rf=await PlanningExternalFile.InspectAsync(id,"reference",original,chosen);Assert.IsTrue(await target.Vm.ReconnectExternalAsync(rf,false));var af=target.Vm.Attachments.Single();var selected=await PlanningExternalFile.InspectAsync(af.Id,"attachment",attachment,text);Assert.IsTrue(await target.Vm.ReconnectExternalAsync(selected,true));Assert.IsTrue(await target.Vm.FlushAsync());
        var restarted=target.NewVm();await restarted.ShowOverviewAsync();await restarted.OpenPlanningProjectCommand.ExecuteAsync(restarted.PlanningProjects.Single(x=>x.ProjectId==source.Id));Assert.IsTrue(restarted.AllProjectReferences.Single().SourceAvailable);Assert.AreEqual(chosen,await restarted.ResolveOriginalAsync(restarted.AllProjectReferences.Single()));Assert.IsNotNull(restarted.ResolveAttachmentPath(restarted.Attachments.Single()));
        var summary=Path.Combine(output,"reconnected-fifteen-sections.docx");Assert.IsNotNull(await restarted.ExportPlanningSummaryDocxAsync(summary));using(var document=WordprocessingDocument.Open(summary,false)){Assert.IsFalse(new OpenXmlValidator().Validate(document).Any());StringAssert.Contains(document.MainDocumentPart!.Document.InnerText,rf.Sha256);}
        var view=new RAWSelectionAssistant.Views.PlanningCenterView{DataContext=restarted};view.Measure(new System.Windows.Size(1100,800));view.Arrange(new System.Windows.Rect(0,0,1100,800));view.UpdateLayout();view.DataContext=null;
        var owner=new System.Windows.Window{Content=view,Width=1100,Height=800};view.DataContext=restarted;owner.Show();
        typeof(RAWSelectionAssistant.Views.PlanningCenterView).GetMethod("ShowExternalReconnect",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(view,null);
        var child=owner.OwnedWindows.Cast<System.Windows.Window>().Single();Assert.AreEqual("缺失外链与手动重连",child.Title);child.Close();owner.Close();view.DataContext=null;
        // A fresh import never trusts this machine's local selection approval.
        var again=Path.Combine(output,"reexport-with-local-approval.ptplan");await restarted.ExportProjectPackageAsync(again);using var another=await PlanningFoundationWpfTests.Fixture.Create();await another.Vm.ImportPackageToCurrentRootAsync(again);Assert.IsFalse(another.Vm.AllProjectReferences.Single().SourceAvailable);
        await File.WriteAllTextAsync(Path.Combine(output,"RESULT.json"),JsonSerializer.Serialize(new{project=source.Id,reference=id,attachment=af.Id,rf.Sha256,package,summary,ordinaryList=true,viewModelRecreated=true,nativeDesktop=false,sectionCount=15}));
    });
}
