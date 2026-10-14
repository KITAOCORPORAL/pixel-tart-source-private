using System.IO;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using W=DocumentFormat.OpenXml.Wordprocessing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningRichFlowTests
{
    [TestMethod]
    public Task ConcurrentRowAndInspectorChangesMergeOnlyEditedFields()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);
        var before=f.Vm.Shots.Single();
        var row=new PlanningShotEditRow(before,f.Vm.StageShot,()=>f.Vm.ShotDraft(before.ShotId));row.BeginEdit();row["Name"]="表格长名称改动";
        f.Vm.StageShot(before with{Action="检查器新动作\n第二行转身",Props="检查器新道具"});row.EndEdit();
        Assert.IsTrue(await f.Vm.FlushAsync());Assert.AreEqual("检查器新动作\n第二行转身",f.Vm.Shots.Single().Action);
        Assert.AreEqual("检查器新道具",f.Vm.Shots.Single().Props);Assert.AreEqual("表格长名称改动",f.Vm.Shots.Single().Name);
    });
    [TestMethod]
    public Task RerenderCapturesRichDraftBeforeRetiringEditor()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);EnsureTestApplication();await f.Vm.SwitchPlanningModeAsync(true);
        var view=new PlanningCenterView{DataContext=f.Vm};var content=(StackPanel)view.FindName("DocumentContent");
        var old=content.Children.OfType<PlanningRichEditor>().Single();var box=old.Children.OfType<RichTextBox>().Single();
        box.Document.Blocks.Add(new Paragraph(new Run("未等定时器的重绘前正文")));
        view.Render();Assert.IsTrue(await f.Vm.FlushAsync());StringAssert.Contains(f.Vm.DocumentBody,"未等定时器的重绘前正文");
        old.FlushPending();StringAssert.Contains(f.Vm.DocumentBody,"未等定时器的重绘前正文");view.DataContext=null;
    });


    [TestMethod]
    public Task FailedImportRetainsNewDraftAndRecoveryThenRetryUndo()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);EnsureTestApplication();
        f.Vm.DocumentBody="原正文";await f.Vm.SwitchPlanningModeAsync(true);var view=new PlanningCenterView{DataContext=f.Vm};var content=(StackPanel)view.FindName("DocumentContent");
        var path=Path.Combine(f.Root,"locked.docx");Assert.IsTrue(await f.Vm.ExportPlanningDocxAsync(path));
        using(var package=DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(path,true)){package.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().First().Descendants<W.Text>().First().Text="失败后仍须保留的导回正文";package.MainDocumentPart.Document.Save();}
        var review=f.Vm.PreviewPlanningDocxImport(path);var old=content.Children.OfType<PlanningRichEditor>().Single();
        var file=Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json");var bytes=await File.ReadAllBytesAsync(file);
        using(var held=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Assert.IsFalse(await f.Vm.ApplyPlanningDocxImportAsync(review,[],true));
        old.FlushPending();StringAssert.Contains(f.Vm.DocumentBody,"失败后仍须保留的导回正文");CollectionAssert.AreEqual(bytes,await File.ReadAllBytesAsync(file));Assert.HasCount(1,Directory.GetFiles(Path.Combine(f.Root,"planning"),"*.docx-recovery-*.json"));
        Assert.IsTrue(await f.Vm.FlushAsync());Assert.IsTrue(await f.Vm.UndoPlanningAsync());StringAssert.Contains(f.Vm.DocumentBody,"原正文");view.DataContext=null;
    });
    [TestMethod]
    public Task RemovedStructureStaysRemovedAndStaleReviewCannotReplaceEdits()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);await f.Vm.SwitchPlanningModeAsync(true);
        var removed=f.Vm.StructureBlocks.First().Id;Assert.IsTrue(await f.Vm.RemoveStructureBlockAsync(removed));
        for(var i=0;i<3;i++){await f.Vm.SwitchPlanningModeAsync(false);await f.Vm.SwitchPlanningModeAsync(true);Assert.HasCount(1,f.Vm.StructureBlocks);Assert.IsFalse(f.Vm.StructureBlocks.Any(b=>b.Id==removed));}
        var path=Path.Combine(f.Root,"stale.docx");await f.Vm.ExportPlanningDocxAsync(path);var review=f.Vm.PreviewPlanningDocxImport(path);
        f.Vm.DocumentTitle="预览之后的新标题";Assert.IsTrue(await f.Vm.FlushAsync());await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Vm.ApplyPlanningDocxImportAsync(review,[],true));
        Assert.AreEqual("预览之后的新标题",f.Vm.DocumentTitle);Assert.IsEmpty(Directory.GetFiles(Path.Combine(f.Root,"planning"),"*.docx-recovery-*.json"));
    });
    [TestMethod]
    public Task RowCancelAndInvalidDraftRecoveryPreserveOtherFields()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);var shot=f.Vm.Shots.Single();
        var row=new PlanningShotEditRow(shot,f.Vm.StageShot,()=>f.Vm.ShotDraft(shot.ShotId));row.BeginEdit();row["Name"]="取消的名称";row.CancelEdit();row.EndEdit();Assert.IsTrue(await f.Vm.FlushAsync());Assert.AreEqual(shot.Name,f.Vm.Shots.Single().Name);
        row.BeginEdit();row["Name"]="保留的非法分钟草稿";row["EstimatedMinutes"]="长文本非数字";row.EndEdit();Assert.IsFalse(await f.Vm.FlushAsync());
        var recovered=f.NewVm();await recovered.LoadAsync(f.Id);var draft=recovered.ShotDraft(shot.ShotId);Assert.AreEqual("保留的非法分钟草稿",draft.Name);Assert.AreEqual("长文本非数字",draft.CustomValues["draft:EstimatedMinutes"]);
        recovered.StageShot(draft with{EstimatedMinutes=12,CustomValues=new()});Assert.IsTrue(await recovered.FlushAsync());f.Vm.StageShot(draft with{EstimatedMinutes=12,CustomValues=new()});Assert.IsTrue(await f.Vm.FlushAsync());
    });
    private static void Arrange(FrameworkElement view,double width,double height)
    {view.Width=width;view.Height=height;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();}
    private static IEnumerable<T> Walk<T>(DependencyObject root) where T:DependencyObject
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Walk<T>(child))yield return nested;}}
    private static void Save(FrameworkElement view,string path)
    {var bmp=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(path);png.Save(stream);}
    [TestMethod]
    public Task RichSyntheticContinuousFlowAndOffscreenEvidence()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();EnsureTestApplication();
        await PlanningRichFlowFixture.Populate(f);Assert.HasCount(24,f.Vm.Shots);Assert.HasCount(4,f.Vm.ResourceCatalog.Items.OfType<PlanningLight>());
        Assert.HasCount(3,f.Vm.ResourceCatalog.Items.OfType<PlanningScheduleSlot>().Select(s=>s.Date).Distinct());
        Assert.AreEqual(7800m,f.Vm.ResourceCatalog.Items.OfType<PlanningBudgetLine>().Sum(b=>b.Planned));
        PlanningRichFlowFixture.SaveProposal(f.Vm,PlanningRichFlowFixture.Proposal());Assert.IsTrue(await f.Vm.FlushAsync());
        await f.Vm.RefreshPlanningListAsync();
        var output=Environment.GetEnvironmentVariable("KITAO_RICH_FLOW_EVIDENCE");if(output is not null)Directory.CreateDirectory(output);
        var evidence=new List<object>();
        var view=new PlanningCenterView{DataContext=f.Vm};
        void Shot(string name,double width=1720,double height=1000)
        {Arrange(view,width,height);if(output is not null)Save(view,Path.Combine(output,name+".png"));evidence.Add(new{name,width,height,kind="WPF_WORKSPACE_OFFSCREEN_96DPI",nativeInput="NOT_RUN"});}
        Assert.IsTrue(await f.Vm.SelectContentSafelyAsync("项目概览"));Shot("01-overview-wide");Shot("02-overview-narrow",900,820);
        await f.Vm.SelectContentSafelyAsync("镜头清单");f.Vm.SelectedShot=f.Vm.Shots[0];view.Render();Shot("03-shots-wide");
        var table=((StackPanel)view.FindName("DocumentContent")).Children.OfType<DataGrid>().Single();
        var rows=table.Items.Cast<PlanningShotEditRow>().ToArray();Assert.HasCount(24,rows);
        foreach(var row in rows){var shot=f.Vm.Shots.Single(s=>s.ShotId==row.ShotId);Assert.AreEqual(f.Vm.AllProjectReferences.Single(r=>r.Id==shot.References.Single().ReferenceId).PreviewPath,row.ReferencePreviewPath);Assert.IsTrue(File.Exists(row.ReferencePreviewPath));}
        var first=rows[0];first.BeginEdit();first["Name"]="表格修改："+first["Name"];
        var inspector=(StackPanel)view.FindName("PlanningInspectorContent");
        var action=Walk<TextBox>(inspector).Single(b=>AutomationProperties.GetName(b)=="人物动作");action.Text="检查器动作第一行\n检查器动作第二行\n检查器动作第三行";
        f.Vm.CapturePendingEditorDraft!.Invoke();first.EndEdit();Assert.IsTrue(await f.Vm.FlushAsync());
        StringAssert.StartsWith(f.Vm.Shots[0].Name,"表格修改：");StringAssert.Contains(f.Vm.Shots[0].Action,"检查器动作第二行");
        view.Render();Shot("04-shots-narrow",900,820);
        table=((StackPanel)view.FindName("DocumentContent")).Children.OfType<DataGrid>().Single();
        var scroll=Walk<ScrollViewer>(table).First();Assert.IsGreaterThan(0d,scroll.ScrollableWidth);scroll.ScrollToHorizontalOffset(scroll.ScrollableWidth);view.UpdateLayout();Shot("05-shots-horizontal-end",900,820);
        Assert.AreEqual(2,table.FrozenColumnCount);Assert.IsGreaterThan(0d,scroll.HorizontalOffset);
        view.OpenPlanningDock((FrameworkElement)view.FindName("PlanningInspectorPane"),true);Shot("06-inspector-drawer",900,820);Assert.IsFalse(((Grid)view.FindName("WorkspaceLayout")).IsEnabled);view.ClosePlanningDock();Assert.IsTrue(((Grid)view.FindName("WorkspaceLayout")).IsEnabled);
        foreach(var page in new[]{"服化道与道具","设备","人员","当天时间表","预算","灯位图"})
        {await f.Vm.SelectContentSafelyAsync(page);Shot("resource-"+page+"-wide");Shot("resource-"+page+"-narrow",900,820);}
        Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(true));var blocks=f.Vm.StructureBlocks.Select(b=>b.Id).ToArray();
        Shot("07-advanced-wide");Shot("08-advanced-narrow",900,820);
        var content=(StackPanel)view.FindName("DocumentContent");var editor=content.Children.OfType<PlanningRichEditor>().Single();var box=editor.Children.OfType<RichTextBox>().Single();
        var end=box.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);box.CaretPosition=end;editor.SetPageZoom(150);var offset=box.Document.ContentStart.GetOffsetToPosition(box.CaretPosition);box.CaretPosition.InsertTextInRun("缩放150后的编辑位置标记");editor.FlushPending();
        Assert.IsGreaterThan(0,offset);StringAssert.Contains(f.Vm.DocumentBody,"缩放150后的编辑位置标记");Assert.IsTrue(await f.Vm.SaveUiLayoutAsync(f.Vm.UiLayout with{DocumentZoom=150}));
        box.ScrollToEnd();view.UpdateLayout();Shot("09-zoom150-edit-position",1720,1000);
        Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(false));Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(true));CollectionAssert.AreEqual(blocks,f.Vm.StructureBlocks.Select(b=>b.Id).ToArray());
        await f.Vm.PreviewDocumentCommand.ExecuteAsync(null);Shot("10-paged-preview-first",1366,960);
        var viewer=Walk<FlowDocumentPageViewer>(content).Single();var paginator=((IDocumentPaginatorSource)viewer.Document).DocumentPaginator;paginator.ComputePageCount();Assert.IsGreaterThanOrEqualTo(5,paginator.PageCount);var pageCount=paginator.PageCount;
        viewer.GoToPage(3);view.UpdateLayout();Assert.AreEqual(3,viewer.MasterPageNumber);Shot("11-paged-preview-third",1366,960);
        await f.Vm.PreviewDocumentCommand.ExecuteAsync(null);
        editor=content.Children.OfType<PlanningRichEditor>().Single();var oldEditor=editor;
        var beforeBody=f.Vm.DocumentBody;var beforeName=f.Vm.Shots[0].Name;var beforeRtf=f.Vm.RichTextPackage;
        var path=Path.Combine(f.Root,"rich-flow.docx");Assert.IsTrue(await f.Vm.ExportPlanningDocxAsync(path));
        using(var package=DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(path,true))
        {
            Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(package).ToArray());
            Assert.IsGreaterThanOrEqualTo(1,package.MainDocumentPart!.ImageParts.Count());Assert.HasCount(2,package.MainDocumentPart.Document.Body!.Elements<W.Table>());
            var cell=package.MainDocumentPart.Document.Body.Elements<W.Table>().Last().Elements<W.TableRow>().Skip(1).First().Elements<W.TableCell>().Skip(1).First();
            cell.Descendants<W.Text>().First().Text="DOCX导回的合成镜头名称";package.MainDocumentPart.Document.Body.Elements<W.Paragraph>().First().Descendants<W.Text>().First().Text="DOCX导回正文标记";package.MainDocumentPart.Document.Save();
        }
        var review=f.Vm.PreviewPlanningDocxImport(path);Assert.HasCount(1,review.Differences);
        Assert.IsTrue(PlanningTextSearch.Paragraphs(PlanningDocxAdapter.Import(path).Document.Blocks).SelectMany(p=>p.Inlines).OfType<InlineUIContainer>().Any(i=>i.Child is Image),"DOCX image survives inline import in original position");
        Assert.IsFalse(await f.Vm.ApplyPlanningDocxImportAsync(review,[f.Vm.Shots[0].ShotId],false));Assert.IsEmpty(Directory.GetFiles(Path.Combine(f.Root,"planning"),"*.docx-recovery-*.json"));
        Assert.IsTrue(await f.Vm.ApplyPlanningDocxImportAsync(review,[f.Vm.Shots[0].ShotId],true));StringAssert.Contains(f.Vm.DocumentBody,"DOCX导回正文标记");Assert.AreEqual("DOCX导回的合成镜头名称",f.Vm.Shots[0].Name);
        oldEditor.FlushPending();Assert.IsTrue(await f.Vm.FlushAsync());StringAssert.Contains(f.Vm.DocumentBody,"DOCX导回正文标记");
        var recoveryFiles=Directory.GetFiles(Path.Combine(f.Root,"planning"),"*.docx-recovery-*.json");Assert.HasCount(1,recoveryFiles);
        var recovery=JsonSerializer.Deserialize<PlanningDocumentDraft>(await File.ReadAllTextAsync(recoveryFiles[0]))!;Assert.AreEqual(beforeBody,recovery.Document.Body);Assert.AreEqual(beforeName,recovery.Shots![0].Name);
        var again=f.Vm.PreviewPlanningDocxImport(path);Assert.IsEmpty(again.Differences);Assert.IsTrue(await f.Vm.ApplyPlanningDocxImportAsync(again,[],true));Assert.HasCount(1,Directory.GetFiles(Path.Combine(f.Root,"planning"),"*.docx-recovery-*.json"));
        CollectionAssert.AreEqual(blocks,f.Vm.StructureBlocks.Select(b=>b.Id).ToArray());Assert.HasCount(1,PlanningDocumentLayout.Load(f.Vm.RichTextPackage!,f.Vm.RichLayout).Blocks.OfType<Table>());
        var importedEditor=content.Children.OfType<PlanningRichEditor>().Single();Assert.IsTrue(await f.Vm.UndoPlanningAsync());importedEditor.FlushPending();Assert.IsTrue(await f.Vm.FlushAsync());Assert.AreEqual(beforeBody,f.Vm.DocumentBody);Assert.AreEqual(beforeName,f.Vm.Shots[0].Name);Assert.AreEqual(beforeRtf,f.Vm.RichTextPackage);
        view.DataContext=null;var reopened=f.NewVm();await reopened.LoadAsync(f.Id);Assert.AreEqual(beforeBody,reopened.DocumentBody);Assert.HasCount(24,reopened.Shots);Assert.AreEqual(150d,reopened.UiLayout.DocumentZoom);CollectionAssert.AreEqual(blocks,reopened.StructureBlocks.Select(b=>b.Id).ToArray());
        view.DataContext=reopened;Shot("12-reopened-advanced",1720,1000);Assert.AreEqual(150,Walk<ComboBox>(view).Single(c=>AutomationProperties.GetName(c)=="页面缩放百分比").SelectedItem);
        if(output is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(output,"flow-summary.json"),JsonSerializer.Serialize(new{shots=24,resources=reopened.ResourceCatalog.Items.Count,lights=4,references=3,budgetCny=7800,previewPages=pageCount,windows="NOT_RUN",wps="NOT_RUN",scope="IN_PROCESS_WPF_NOT_NATIVE_INPUT",screenshots=evidence},new JsonSerializerOptions{WriteIndented=true}));
            await File.WriteAllTextAsync(Path.Combine(output,"fixture-snapshot.json"),JsonSerializer.Serialize(new{planning=await f.Planning.LoadAsync(f.Id),shots=await f.Shots.LoadAsync(f.Id)},new JsonSerializerOptions{WriteIndented=true}).Replace(f.Root.Replace("\\","\\\\"),output.Replace("\\","\\\\")));
            File.Copy(path,Path.Combine(output,"synthetic-roundtrip.docx"),true);File.Copy(recoveryFiles[0],Path.Combine(output,"synthetic-recovery.json"),true);
            for(var i=0;i<3;i++)await File.WriteAllBytesAsync(Path.Combine(output,$"synthetic-reference-{i}.png"),PlanningRichFlowFixture.ReferencePng(i));
        }
        view.DataContext=null;
        if(output is not null)
        {
            // Instantiate production XAML only. Never Show(), EnsureHandle(), or start App.
            var shell=new MainWindow {DataContext=new ShellFixtureContext(reopened)};
            var shellPlanning=LogicalWalk<PlanningCenterView>(shell).Single();shellPlanning.DataContext=reopened;
            var host=(Panel)LogicalTreeHelper.GetParent(shellPlanning);
            foreach(var sibling in host.Children.OfType<FrameworkElement>())sibling.Visibility=ReferenceEquals(sibling,shellPlanning)?Visibility.Visible:Visibility.Collapsed;
            var root=(FrameworkElement)shell.Content;Arrange(root,1800,1040);shellPlanning.Render();root.UpdateLayout();
            Assert.IsGreaterThan(0d,shellPlanning.ActualWidth);Assert.HasCount(10,((StackPanel)shell.FindName("PrimaryNavigationGroup")).Children.OfType<Button>());
            Save(root,Path.Combine(output,"13-production-shell-offscreen.png"));
            await reopened.SwitchPlanningModeAsync(false);await reopened.SelectContentSafelyAsync("镜头清单");Arrange(root,1366,900);shellPlanning.Render();root.UpdateLayout();Save(root,Path.Combine(output,"14-production-shell-narrow-offscreen.png"));
            Assert.AreEqual(IntPtr.Zero,new System.Windows.Interop.WindowInteropHelper(shell).Handle);
            shellPlanning.DataContext=null;shell.DataContext=null;shell.Content=null;
        }
    });
    private static IEnumerable<T> LogicalWalk<T>(DependencyObject root) where T:DependencyObject
    {foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()){if(child is T value)yield return value;foreach(var nested in LogicalWalk<T>(child))yield return nested;}}
    public sealed record ShellFixtureContext(PlanningCenterViewModel PlanningPage)
    {
        public bool IsPlanningPage=>true;
        public bool IsSettingsModalOpen=>false;
        public bool IsTutorialOpen=>false;
        public bool IsSidebarExpanded=>true;
        public double SidebarWidth=>180;
        public bool IsTaskCenterOpen=>false;
        public bool IsWorkbenchPage=>false;
        public bool IsAssetLibraryPage=>false;
        public bool IsOrganizePage=>false;
        public bool IsWorkCalendarPage=>false;
        public bool IsOnlineSelectionPage=>false;
        public bool IsFinancePage=>false;
        public bool IsHistoryPage=>false;
        public bool IsTetherPage=>false;
        public bool IsReferenceColorPage=>false;
    }
}
