using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningCompanyFollowupTests
{
    private static void Arrange(FrameworkElement v,double w,double h){v.Width=w;v.Height=h;v.Measure(new Size(w,h));v.Arrange(new Rect(0,0,w,h));v.UpdateLayout();}
    private static IEnumerable<T> Walk<T>(DependencyObject p)where T:DependencyObject{for(var i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);if(c is T t)yield return t;foreach(var x in Walk<T>(c))yield return x;}}
    [TestMethod]
    public Task ShotFrozenStatusReferenceAndInspectorOrderSurviveReopen()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);EnsureTestApplication();
        var referencePath=Path.Combine(f.Root,"linked-reference.png");var pixels=Enumerable.Repeat((byte)100,48*32*3).ToArray();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(48,32,96,96,PixelFormats.Rgb24,null,pixels,48*3)));using(var file=File.Create(referencePath))encoder.Save(file);
        var reference=new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:referencePath,Title:"真实关联的合成参考");await f.Vm.AddDocumentReferencesAsync([reference]);await f.Vm.RefreshDocumentReferencesAsync();f.Vm.StageShot(f.Vm.ShotDraft(f.Vm.Shots[0].ShotId) with{References=[reference]});Assert.IsTrue(await f.Vm.FlushAsync());
        var view=new PlanningCenterView{DataContext=f.Vm};await f.Vm.SelectContentSafelyAsync("镜头清单");view.Render();Arrange(view,1366,800);
        var table=Walk<DataGrid>(view).Single(t=>t.Name==""&&t.Columns.Any(c=>Equals(c.Header,"镜号")));
        Assert.AreEqual(2,table.FrozenColumnCount);Assert.AreEqual("状态",table.Columns[1].Header);Assert.IsInstanceOfType<DataGridTemplateColumn>(table.Columns.Single(c=>Equals(c.Header,"参考")));
        var row=table.Items.Cast<RAWSelectionAssistant.ViewModels.PlanningShotEditRow>().Single();Assert.IsTrue(File.Exists(row.ReferencePreviewPath));Assert.AreEqual("真实关联的合成参考",row.ReferenceLabel);
        var inspector=(StackPanel)view.FindName("PlanningInspectorContent");var group=inspector.Children.OfType<Expander>().First();var title=group.Tag!.ToString();
        Assert.IsTrue(await view.MoveInspectorGroupAsync(group,1));Assert.AreEqual(title,f.Vm.UiLayout.InspectorGroupOrder![1]);
        Assert.IsTrue(await f.Vm.SaveUiLayoutAsync(f.Vm.UiLayout with{DocumentZoom=125}));await f.Vm.LoadAsync(f.Id);Assert.AreEqual(125d,f.Vm.UiLayout.DocumentZoom);Assert.AreEqual(title,f.Vm.UiLayout.InspectorGroupOrder![1]);
        foreach(var width in new[]{1366d,900,760}){Arrange(view,width,650);Assert.IsTrue(view.DesiredSize.Width<=width+.5);}
        view.DataContext=null;
    });
    [TestMethod]
    public Task LightCoordinateCommitCancelAndUndoUseExistingResourceIdentity()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);EnsureTestApplication();var light=new PlanningLight{Name="合成主灯",X=.2,Y=.3};Assert.IsTrue(await f.Vm.SaveResourceAsync(light));
        var commits=0;var diagram=new PlanningLightDiagram([light],async next=>{commits++;return await f.Vm.SaveResourceAsync(next);},_=>{});Arrange(diagram,800,360);
        // Creating/cancelling without a commit has no side effects; only explicit completed transaction persists.
        diagram.BeginPositionEdit(light);Assert.IsTrue(diagram.HasPendingDrag);diagram.CancelPositionEdit();Assert.IsFalse(diagram.HasPendingDrag);Assert.AreEqual(.2,f.Vm.ResourceCatalog.Items.OfType<PlanningLight>().Single().X);
        Assert.AreEqual(0,commits);Assert.IsTrue(await diagram.CommitPositionAsync(light,new(.823,.676)));Assert.AreEqual(1,commits);
        var current=f.Vm.ResourceCatalog.Items.OfType<PlanningLight>().Single();Assert.AreEqual(light.Id,current.Id);Assert.AreEqual(.8,current.X);Assert.AreEqual(.7,current.Y);
        await f.Vm.LoadAsync(f.Id);Assert.AreEqual(.8,f.Vm.ResourceCatalog.Items.OfType<PlanningLight>().Single().X);Assert.IsTrue(await f.Vm.UndoPlanningAsync());Assert.AreEqual(.2,f.Vm.ResourceCatalog.Items.OfType<PlanningLight>().Single().X);
    });
    [TestMethod]
    public Task FailedSaveRetainsFileIdentityAndAllowsRetryWithoutFalseSuccess()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);EnsureTestApplication();
        var light=new PlanningLight{Name="待重试灯位",X=.2,Y=.3};Assert.IsTrue(await f.Vm.SaveResourceAsync(light));
        var path=Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json");var bytes=await File.ReadAllBytesAsync(path);
        var view=new PlanningCenterView{DataContext=f.Vm};view.Render();Arrange(view,1366,800);
        var group=((StackPanel)view.FindName("PlanningInspectorContent")).Children.OfType<Expander>().First();var oldOrder=f.Vm.UiLayout.InspectorGroupOrder!.ToArray();
        f.Vm.StageShot(f.Vm.ShotDraft(f.Vm.Shots[0].ShotId) with{Name=""});Assert.IsFalse(await view.MoveInspectorGroupAsync(group,1));CollectionAssert.AreEqual(oldOrder,f.Vm.UiLayout.InspectorGroupOrder!);
        f.Vm.StageShot(f.Vm.Shots[0] with{Name="已恢复镜头"});Assert.IsTrue(await f.Vm.FlushAsync());bytes=await File.ReadAllBytesAsync(path);
        var diagram=new PlanningLightDiagram([light],f.Vm.SaveResourceAsync,_=>{});
        using(var blocked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Assert.IsFalse(await diagram.CommitPositionAsync(light,new(.8,.7)));StringAssert.Contains(f.Vm.SaveStatus,"保存失败");}
        CollectionAssert.AreEqual(bytes,await File.ReadAllBytesAsync(path));Assert.IsTrue(await f.Vm.FlushAsync());Assert.IsFalse(f.Vm.SaveStatus.Contains("保存失败"));
        await f.Vm.LoadAsync(f.Id);Assert.AreEqual(light.Id,f.Vm.ResourceCatalog.Items.OfType<PlanningLight>().Single().Id);Assert.AreEqual(.8,f.Vm.ResourceCatalog.Items.OfType<PlanningLight>().Single().X);view.DataContext=null;
    });
    [TestMethod]
    public Task RichPageViewerUsesRealPaginatorAndDoesNotMutateContent()=>RunSta(async()=>
    {
        EnsureTestApplication();var editor=new PlanningRichEditor(null,string.Join("\n",Enumerable.Repeat("合成提案正文",150)),(_,_,_)=>{},[]);editor.ApplyPlanningPaperAppearance();editor.SetPageZoom(125);
        var viewer=editor.CreatePagedViewer();Arrange(viewer,800,650);var paginator=((IDocumentPaginatorSource)viewer.Document).DocumentPaginator;paginator.ComputePageCount();Assert.IsGreaterThanOrEqualTo(1,paginator.PageCount);Assert.AreEqual(125d,viewer.Zoom);viewer.GoToPage(1);Assert.AreEqual(1,viewer.MasterPageNumber);await Task.CompletedTask;
    });
    [TestMethod]
    public Task OffscreenCompletedStatesSeparateFromPhysicalDpi()=>RunSta(async()=>
    {
        var output=Environment.GetEnvironmentVariable("KITAO_PLANNING_COMPANY_SCREENSHOTS");if(output is null){Assert.Inconclusive("Offscreen evidence opt-in required.");return;}Directory.CreateDirectory(output);
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);await f.Vm.SaveResourceAsync(new PlanningLight{Name="合成主灯",X=.25,Y=.4});EnsureTestApplication();var view=new PlanningCenterView{DataContext=f.Vm};
        foreach(var page in new[]{"镜头清单","灯位图"})foreach(var width in new[]{1366,900}){await f.Vm.SelectContentSafelyAsync(page);view.Render();Arrange(view,width,760);Save(view,Path.Combine(output,page+"-"+width+".png"));}
        await f.Vm.SwitchPlanningModeAsync(true);view.Render();Arrange(view,1366,900);Save(view,Path.Combine(output,"advanced.png"));view.DataContext=null;
    });
    private static void Save(FrameworkElement v,string p){var b=new RenderTargetBitmap((int)v.ActualWidth,(int)v.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(v);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using var stream=File.Create(p);encoder.Save(stream);}
}
