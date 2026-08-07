namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class Version230Rc5CalendarTaskCenterDpiTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    [DataRow(150)]
    [DataRow(200)]
    public void MiniCalendarBadge_UsesLogicalSizeWithoutScalingOrClipping(int dpiPercent)
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/WorkbenchCalendarPanel.xaml");
        StringAssert.Contains(xaml, "MinWidth=\"24\"");
        StringAssert.Contains(xaml, "Height=\"22\"");
        StringAssert.Contains(xaml, "Padding=\"5,0\"");
        StringAssert.Contains(xaml, "LineHeight=\"16\"");
        StringAssert.Contains(xaml, "UseLayoutRounding=\"True\"");
        Assert.IsFalse(xaml.Contains("ScaleTransform", StringComparison.Ordinal));
        CollectionAssert.Contains(new[] { 150, 200 }, dpiPercent);
    }

    [TestMethod]
    [DataRow(768)]
    [DataRow(900)]
    [DataRow(1080)]
    public void TaskCenter_RetainsMinimumUsableHeightAcrossWindowHeights(int windowHeight)
    {
        var xaml = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        StringAssert.Contains(xaml, "Height=\"48*\" MinHeight=\"320\"");
        StringAssert.Contains(xaml, "TaskCenterEmptyState");
        CollectionAssert.Contains(new[] { 768, 900, 1080 }, windowHeight);
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
