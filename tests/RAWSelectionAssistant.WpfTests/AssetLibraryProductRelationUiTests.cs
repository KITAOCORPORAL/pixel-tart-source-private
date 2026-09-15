using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryProductRelationUiTests
{
    [TestMethod]
    public void InspectorAndPickersExposeCurrentRelationProductFlow()
    {
        var root = RepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.P2Browser.cs"));

        foreach (var token in new[]
        {
            "ProjectPickerSearch", "BookingPickerSearch", "当前拍摄对应项目", "当前项目下拍摄",
            "InspectorProjectLinks", "InspectorBookingLinks", "ClientDisplayResolver",
            "SetInspectorWorkflowCommand", "WorkflowUnprocessedCommand"
        }) StringAssert.Contains(viewModel, token);

        foreach (var token in new[]
        {
            "搜索项目", "搜索拍摄", "＋ 关联项目", "＋ 关联拍摄", "AssetInspectorWorkflowPicker",
            "Header=\"未处理\"", "Header=\"客户选择\"", "Header=\"待精修\"", "Header=\"已精修\"", "Header=\"已交付\""
        }) StringAssert.Contains(xaml, token);
    }

    [TestMethod]
    public void ContextMenuHasOnlyTheSixProductGroupsAndNoPermanentDelete()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        foreach (var token in new[] { "Header=\"查看\"", "Header=\"整理\"", "Header=\"摄影工作流\"", "Header=\"创作与导出\"", "Header=\"管理\"", "复制文件", "加入灵感板" })
            StringAssert.Contains(xaml, token);
        Assert.DoesNotContain("永久删除", xaml, StringComparison.Ordinal);
    }

    [TestMethod]
    public void InspirationUiConnectsGalleryTrayAndCollectionDragDrop()
    {
        var root = RepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var behavior = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "InspirationDragDropBehavior.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.P2Browser.cs"));

        foreach (var token in new[]
        {
            "TargetKind=\"Tray\"", "TargetKind=\"Collection\"", "TargetKind=\"TrayOrder\"",
            "TargetKind=\"CollectionOrder\"", "SelectionMode=\"Extended\"", "CloseCollectionPanelCommand"
        }) StringAssert.Contains(xaml, token);
        foreach (var token in new[]
        {
            "PixelTart.AssetLibrary.AssetIds.v1", "PixelTart.Inspiration.TrayEntryIds.v1",
            "ResolveSelectedEntryIds", "AddAssetIdsToCollectionAsync", "MoveEntriesToCollectionAsync",
            "ReorderCollectionEntriesAsync", "ReorderTrayEntriesAsync"
        }) Assert.IsTrue(behavior.Contains(token, StringComparison.Ordinal) || viewModel.Contains(token, StringComparison.Ordinal), token);
        Assert.DoesNotContain("File.Move", behavior, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete", behavior, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
