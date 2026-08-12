using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class WorkbenchOverviewClosureTests
{
    [TestMethod]
    public void ProjectOverview_RendersOnlyFourProductStates()
    {
        var xaml = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var overview = Slice(xaml, "x:Name=\"ProjectOverviewCard\"", "x:Name=\"ProcessingTasksCard\"");

        StringAssert.Contains(overview, "Columns=\"4\" Rows=\"1\"");
        foreach (var label in new[] { "进行中", "待确认", "后期待处理", "已完成" })
            StringAssert.Contains(overview, $"Text=\"{label}\"");
        foreach (var removed in new[] { "待处理", "待匹配", "已匹配", "已导出", "本地项目", "需要确认" })
            Assert.IsFalse(overview.Contains($"Text=\"{removed}\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ProjectOverview_UsesExistingDataAndRaisesTargetedNotifications()
    {
        var source = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(source, "WorkbenchInProgressCount => ProjectHistory.Count");
        StringAssert.Contains(source, "WorkbenchAttentionCount => AttentionCount");
        StringAssert.Contains(source, "WorkbenchAwaitingReturnCount => WorkCalendarPage.AwaitingReturnCount");
        StringAssert.Contains(source, "WorkbenchCompletedCount => ProjectHistory.Count");
        StringAssert.Contains(source, "ProjectHistory.CollectionChanged += (_, _) => NotifyWorkbenchProjectOverview()");
        StringAssert.Contains(source, "nameof(WorkCalendarViewModel.AwaitingReturnCount)");
        StringAssert.Contains(source, "OnPropertyChanged(nameof(WorkbenchAttentionCount))");

        var acceptance = Read("src/RAWSelectionAssistant/MainWindow.AutomatedDpiAcceptance.cs");
        StringAssert.Contains(acceptance, "workbenchOverview");
        StringAssert.Contains(acceptance, "awaitingReturn = _viewModel.WorkbenchAwaitingReturnCount");
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex);
        Assert.IsGreaterThan(startIndex, endIndex);
        return source[startIndex..endIndex];
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
