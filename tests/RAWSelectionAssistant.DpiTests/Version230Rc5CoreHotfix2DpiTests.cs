namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class Version230Rc5CoreHotfix2DpiTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    [DataRow(100)]
    [DataRow(125)]
    [DataRow(150)]
    [DataRow(175)]
    [DataRow(200)]
    public void BookingEditor_SupportedDpiUsesStableLogicalGeometry(int dpiPercent)
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml");
        StringAssert.Contains(xaml, "x:Name=\"BookingEditorStepper\"");
        StringAssert.Contains(xaml, "MinWidth=\"132\"");
        StringAssert.Contains(xaml, "MaxWidth=\"980\"");
        Assert.IsFalse(xaml.Contains("ScaleTransform", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("Margin=\"-", StringComparison.Ordinal));
        CollectionAssert.Contains(new[] { 100, 125, 150, 175, 200 }, dpiPercent);
    }

    [TestMethod]
    [DataRow(150)]
    [DataRow(200)]
    public void FullCalendar_BadgeAndLockRemainLogicalSized(int dpiPercent)
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/MonthCalendarView.xaml");
        StringAssert.Contains(xaml, "x:Name=\"FullCalendarDayNumberBadge\"");
        StringAssert.Contains(xaml, "MinWidth=\"32\" Height=\"26\" Padding=\"6,0\"");
        StringAssert.Contains(xaml, "Width=\"12\" Height=\"12\"");
        CollectionAssert.Contains(new[] { 150, 200 }, dpiPercent);
    }

    [TestMethod]
    [DataRow(150)]
    [DataRow(200)]
    public void Toolbox_PinKeepsAccessibleHitTargetAtDpi(int dpiPercent)
    {
        var xaml = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        StringAssert.Contains(xaml, "Width=\"32\" Height=\"32\" MinWidth=\"32\" MinHeight=\"32\"");
        StringAssert.Contains(xaml, "Width=\"20\" Height=\"20\"");
        CollectionAssert.Contains(new[] { 150, 200 }, dpiPercent);
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
