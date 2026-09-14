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
        foreach (var token in new[] { "Header=\"查看\"", "Header=\"整理\"", "Header=\"摄影工作流\"", "Header=\"创作\"", "Header=\"导出\"", "Header=\"生命周期\"", "复制路径", "加入灵感集" })
            StringAssert.Contains(xaml, token);
        Assert.DoesNotContain("永久删除", xaml, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
