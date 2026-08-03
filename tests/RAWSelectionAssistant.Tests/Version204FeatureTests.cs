using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version204FeatureTests
{
    [TestMethod] public void QuickTools_Move_PreservesOrder() => CollectionAssert.AreEqual(new[] { "PhotoOrganize", "Workflow", "BatchCompress" }, QuickToolsService.Move(QuickToolsService.DefaultPinnedTools, "PhotoOrganize", -1));
    [TestMethod] public void QuickTools_Remove_Works() => CollectionAssert.AreEqual(new[] { "Workflow", "BatchCompress" }, QuickToolsService.Remove(QuickToolsService.DefaultPinnedTools, "PhotoOrganize"));
    [TestMethod] public void QuickTools_Add_RejectsFourthTool() => Assert.HasCount(3, QuickToolsService.Add(QuickToolsService.DefaultPinnedTools, "Collage"));
    [TestMethod] public void QuickToolLayout_HasSchemaVersion() => Assert.AreEqual("1.0", new QuickToolLayout().SchemaVersion);

    [TestMethod]
    public async Task QuickTools_OrderPersistsAcrossRestart()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        var service = new SettingsService(new TestLogService(), path);
        var settings = new AppSettings { QuickToolLayout = new QuickToolLayout { OrderedToolIds = ["Collage", "Workflow"] }, PinnedQuickTools = ["Collage", "Workflow"] };
        await service.SaveAsync(settings);
        var loaded = await new SettingsService(new TestLogService(), path).LoadAsync();
        CollectionAssert.AreEqual(new[] { "Collage", "Workflow" }, loaded.PinnedQuickTools);
    }

    [TestMethod]
    public async Task QuickTools_OldSettingsMigrateExistingPinnedOrder()
    {
        using var temp = new TempDirectory();
        var path=temp.Combine("settings.json");
        await File.WriteAllTextAsync(path,"{\"PinnedQuickTools\":[\"Collage\",\"Workflow\"]}");
        var loaded=await new SettingsService(new TestLogService(),path).LoadAsync();
        CollectionAssert.AreEqual(new[]{"Collage","Workflow"},loaded.QuickToolLayout.OrderedToolIds);
    }

    [TestMethod]
    public async Task Organize_ScanAcceptsFilesAndReportsMissingMetadata()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("中文 照片/a.jpg", [1, 2, 3]);
        var photos = await new OrganizeService().ScanAsync([source]);
        Assert.HasCount(1, photos);
        Assert.IsTrue(photos[0].MetadataMissing);
    }

    [TestMethod]
    public async Task Organize_CaptureDateMissing_UsesMetadataMissingGroup()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("a.jpg", [1]);
        var service = new OrganizeService();
        var photos = await service.ScanAsync([source]);
        var groups = service.Group(photos, new OrganizeRule(OrganizeRuleType.CaptureDate));
        Assert.AreEqual("元数据缺失", groups[0].Name);
    }

    [TestMethod]
    public async Task Organize_DefaultCopy_PreservesSourceAndCreatesTarget()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("source/one.jpg", [1, 2, 3, 4]);
        var service = new OrganizeService();
        var photos = await service.ScanAsync([source]);
        var groups = service.Group(photos, new OrganizeRule(OrganizeRuleType.FileFormat));
        var plan = service.BuildPlan(photos, groups, [temp.Combine("source")], temp.Combine("output"), new OrganizeRule(OrganizeRuleType.FileFormat));
        var result = await service.ExecuteAsync(plan);
        Assert.AreEqual(1, result.Succeeded);
        Assert.IsTrue(File.Exists(source));
        Assert.IsTrue(File.Exists(result.Manifest.Items[0].DestinationPath));
    }

    [TestMethod]
    public async Task Organize_AutoNumber_DoesNotOverwriteExistingTarget()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("source/one.jpg", [1, 2, 3]);
        var existing = temp.CreateFile("output/JPG/one.jpg", [9]);
        var service = new OrganizeService();
        var photos = await service.ScanAsync([source]);
        var groups = service.Group(photos, new OrganizeRule(OrganizeRuleType.FileFormat));
        var result = await service.ExecuteAsync(service.BuildPlan(photos, groups, [temp.Combine("source")], temp.Combine("output"), new OrganizeRule(OrganizeRuleType.FileFormat)));
        CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(existing));
        StringAssert.EndsWith(result.Manifest.Items[0].DestinationPath, "one_2.jpg");
    }

    [TestMethod]
    public async Task Organize_Move_RequiresSecondConfirmation()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("source/one.jpg", [1]);
        var service = new OrganizeService();
        var photos = await service.ScanAsync([source]);
        var groups = service.Group(photos, new OrganizeRule(OrganizeRuleType.FileFormat));
        var plan = service.BuildPlan(photos, groups, [temp.Combine("source")], temp.Combine("output"), new OrganizeRule(OrganizeRuleType.FileFormat), OrganizeOperationType.Move);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.ExecuteAsync(plan));
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task Organize_MoveThenUndo_RestoresSourceWithoutOverwrite()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("source/one.jpg", [1, 2, 3]);
        var service = new OrganizeService();
        var photos = await service.ScanAsync([source]);
        var groups = service.Group(photos, new OrganizeRule(OrganizeRuleType.FileFormat));
        var plan = service.BuildPlan(photos, groups, [temp.Combine("source")], temp.Combine("output"), new OrganizeRule(OrganizeRuleType.FileFormat), OrganizeOperationType.Move);
        var result = await service.ExecuteAsync(plan, moveConfirmed: true);
        Assert.IsFalse(File.Exists(source));
        Assert.IsTrue(await service.UndoMoveAsync(result.Manifest));
        Assert.IsTrue(File.Exists(source));
        Assert.IsFalse(File.Exists(result.Manifest.Items[0].DestinationPath));
    }

    [TestMethod]
    public async Task Organize_Cancel_LeavesNoCopiedOutputs()
    {
        using var temp = new TempDirectory();
        var source = temp.CreateFile("source/one.jpg", new byte[1024]);
        var service = new OrganizeService();
        var photos = await service.ScanAsync([source]);
        var groups = service.Group(photos, new OrganizeRule(OrganizeRuleType.FileFormat));
        var plan = service.BuildPlan(photos, groups, [temp.Combine("source")], temp.Combine("output"), new OrganizeRule(OrganizeRuleType.FileFormat));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var result = await service.ExecuteAsync(plan, cancellationToken: cancellation.Token);
        Assert.IsTrue(result.Cancelled);
        Assert.IsFalse(File.Exists(plan.Items[0].DestinationPath));
    }

    [TestMethod]
    public async Task Organize_MoveCancellation_PreservesCompletedMovedTarget()
    {
        using var temp=new TempDirectory();
        var first=temp.CreateFile("source/one.jpg",new byte[1024]);
        var second=temp.CreateFile("source/two.jpg",new byte[1024*1024]);
        var service=new OrganizeService();
        var photos=await service.ScanAsync([first,second]);
        var groups=service.Group(photos,new OrganizeRule(OrganizeRuleType.FileFormat));
        var plan=service.BuildPlan(photos,groups,[temp.Combine("source")],temp.Combine("output"),new OrganizeRule(OrganizeRuleType.FileFormat),OrganizeOperationType.Move);
        using var cancellation=new CancellationTokenSource();
        var progress=new CallbackProgress<OrganizeExecutionProgress>(value=>{if(value.Completed==1)cancellation.Cancel();});
        var result=await service.ExecuteAsync(plan,moveConfirmed:true,cancellationToken:cancellation.Token,progress:progress);
        var completed=result.Manifest.Items.Single(x=>x.State==OrganizeItemState.Moved);
        Assert.IsTrue(File.Exists(completed.DestinationPath));
        Assert.IsFalse(File.Exists(completed.SourcePath));
        Assert.IsTrue(File.Exists(second));
    }

    [TestMethod]
    public async Task Organize_OutputInsideSource_IsRejected()
    {
        using var temp=new TempDirectory();
        var source=temp.CreateFile("source/one.jpg",[1]);
        var service=new OrganizeService();
        var photos=await service.ScanAsync([source]);
        var groups=service.Group(photos,new OrganizeRule(OrganizeRuleType.FileFormat));
        Assert.ThrowsExactly<IOException>(()=>service.BuildPlan(photos,groups,[temp.Combine("source")],temp.Combine("source","output"),new OrganizeRule(OrganizeRuleType.FileFormat)));
    }

    [TestMethod]
    public async Task Organize_ExportsCsvJsonAndTxtReports()
    {
        using var temp = new TempDirectory();
        var manifest = new OrganizeManifest { OperationId = Guid.NewGuid(), OperationType = OrganizeOperationType.Copy, ConflictPolicy = OrganizeConflictPolicy.AutoNumber };
        await new OrganizeService().ExportReportsAsync(manifest, temp.Path);
        Assert.HasCount(3, Directory.GetFiles(temp.Path, "整理报告_*.*"));
    }

    [TestMethod]
    public async Task Organize_SavePlan_WritesVersionedJsonWithoutCopyingPhotos()
    {
        using var temp=new TempDirectory();
        var source=temp.CreateFile("source/one.jpg",[1,2,3]);
        var service=new OrganizeService();
        var photos=await service.ScanAsync([source]);
        var groups=service.Group(photos,new OrganizeRule(OrganizeRuleType.FileFormat));
        var plan=service.BuildPlan(photos,groups,[temp.Combine("source")],string.Empty,new OrganizeRule(OrganizeRuleType.FileFormat),OrganizeOperationType.SavePlan);
        var planPath=await service.SavePlanAsync(plan,temp.Combine("plans","organize.json"));
        Assert.IsTrue(File.Exists(planPath));
        StringAssert.Contains(await File.ReadAllTextAsync(planPath),"\"SchemaVersion\": \"1.0\"");
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod] public void OrganizePlan_ContainsRequiredSafetyFields() { var item = new OrganizeManifestItem(); Assert.AreEqual("1.0", item.SchemaVersion); Assert.AreEqual(OrganizeItemState.Pending, item.State); Assert.AreEqual(OrganizeConflictPolicy.AutoNumber, item.ConflictPolicy); }
    [TestMethod] public void Organize_Defaults_AreCopyAndAutoNumber() { var plan = new OrganizePlan(); Assert.AreEqual(OrganizeOperationType.Copy, plan.OperationType); Assert.AreEqual(OrganizeConflictPolicy.AutoNumber, plan.ConflictPolicy); }
    [TestMethod] public void CollageCatalog_HasTemplatesForTwoThroughSix() { foreach(var count in Enumerable.Range(2,5)) Assert.IsTrue(CollageTemplateCatalog.All.Any(x=>x.ImageCount==count)); }
    [TestMethod] public void CollageCatalog_IsDataDrivenAndSlotsNormalized() { Assert.IsGreaterThanOrEqualTo(23, CollageTemplateCatalog.All.Count); Assert.IsTrue(CollageTemplateCatalog.All.SelectMany(x=>x.Slots).All(x=>x.X>=0&&x.Y>=0&&x.Width>0&&x.Height>0&&x.X+x.Width<=1.001&&x.Y+x.Height<=1.001)); }
    [TestMethod] public void CollageProject_HasSchemaAndSafeDefaults() { var project=new CollageProject(); Assert.AreEqual("1.0",project.SchemaVersion); Assert.AreEqual("JPG",project.Export.Format); Assert.AreEqual(95,project.Export.JpegQuality); }
    [TestMethod] public void CollageModels_SupportTransformsAndLongStrips() { var state=new CollageImageState{Zoom=2,Rotation=90,FlipHorizontal=true}; Assert.AreEqual(2,state.Zoom); Assert.IsTrue(Enum.IsDefined(CollageMode.VerticalStrip)); Assert.IsTrue(Enum.IsDefined(CollageMode.HorizontalStrip)); }

    [TestMethod]
    public void UiAndSafetySource_ContainRequired204Interactions()
    {
        var root=Root();
        var main=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/MainWindow.xaml"));
        var code=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/MainWindow.xaml.cs"));
        var organize=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant.Core/Services/OrganizeService.cs"));
        var collage=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/Services/CollageExportService.cs"));
        foreach(var value in new[]{"管理快捷工具","PinnedQuickTools_Drop","PreviewKeyDown"}) Assert.IsTrue(main.Contains(value)||code.Contains(value));
        foreach(var value in new[]{"FileMode.CreateNew","Flush(true)","UndoMoveAsync","ExportReportsAsync"}) StringAssert.Contains(organize,value);
        foreach(var value in new[]{"JpegBitmapEncoder","PngBitmapEncoder","ResolveAutoNumberedPath","FileMode.CreateNew"}) StringAssert.Contains(collage,value);
    }

    [TestMethod]
    public void ThemeResources_CoverDarkInteractiveControls()
    {
        var root=Root();
        var inputs=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml"));
        var menu=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Menu.xaml"));
        var tables=File.ReadAllText(Path.Combine(root,"src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Tables.xaml"));
        foreach(var value in new[]{"TargetType=\"CheckBox\"","TargetType=\"RadioButton\"","TargetType=\"ToggleButton\"","TargetType=\"Slider\"","TargetType=\"ProgressBar\"","TargetType=\"DatePicker\"","TargetType=\"ToolTip\""})StringAssert.Contains(inputs,value);
        foreach(var value in new[]{"TargetType=\"ContextMenu\"","TargetType=\"MenuItem\"","TargetType=\"TabControl\""})StringAssert.Contains(menu,value);
        foreach(var value in new[]{"TargetType=\"ScrollBar\"","TargetType=\"DataGrid\"","TargetType=\"DataGridColumnHeader\"","TargetType=\"DataGridCell\""})StringAssert.Contains(tables,value);
    }

    private static string Root()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"RAWSelectionAssistant.sln")))directory=directory.Parent;
        return directory?.FullName??throw new DirectoryNotFoundException();
    }

    private sealed class CallbackProgress<T>(Action<T> callback):IProgress<T>{public void Report(T value)=>callback(value);}
}
