namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class Version220StageDDpiGateTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    [DataRow(125)]
    [DataRow(150)]
    [DataRow(200)]
    public void ApprovedLogicalDpi_IsAnAutomatedHardGate(int dpiPercent)
    {
        CollectionAssert.Contains(new[] { 125, 150, 200 }, dpiPercent);
        var wpfTest = Text("tests/RAWSelectionAssistant.WpfTests/Version220CalendarUiTests.cs");
        var expectedViewport = dpiPercent switch { 125 => "1024, 640", 150 => "854, 534", _ => "720, 480" };
        StringAssert.Contains(wpfTest, expectedViewport);
        StringAssert.Contains(wpfTest, "new BookingRemindersPanel(), new BookingWeatherPanel(), new WorkbenchCalendarSummaryView(), new ReminderNotificationHost()");
    }

    [TestMethod] public void PhysicalDpiManualTesting_RemainsAnAllowedKnownLimitation()
    {
        var existing = Text("tests/RAWSelectionAssistant.DpiTests/AutomatedDpiEvidenceTests.cs");
        StringAssert.Contains(existing, "PhysicalDpiManuallyTested");
        StringAssert.Contains(existing, "Assert.IsFalse");
    }

    [TestMethod] public void StageDViews_HaveScrollOrBoundedNotificationSurfaces()
    {
        var reminders = Text("src/RAWSelectionAssistant/Views/BookingRemindersPanel.xaml");
        var workbench = Text("src/RAWSelectionAssistant/Views/WorkbenchScheduleView.xaml");
        var notifications = Text("src/RAWSelectionAssistant/Views/ReminderNotificationHost.xaml");
        StringAssert.Contains(workbench, "VerticalScrollBarVisibility=\"Auto\"");
        StringAssert.Contains(notifications, "Width=\"390\"");
        Assert.IsFalse(reminders.Contains("MinWidth=\"", StringComparison.Ordinal));
    }

    [TestMethod] public void StageDViews_UseLogicalUnitsAndThemeResources()
    {
        var text = Text("src/RAWSelectionAssistant/Views/BookingRemindersPanel.xaml") + Text("src/RAWSelectionAssistant/Views/WorkbenchScheduleView.xaml") + Text("src/RAWSelectionAssistant/Views/ReminderNotificationHost.xaml");
        StringAssert.Contains(text, "DynamicResource");
        Assert.IsFalse(text.Contains("DpiScale", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("ScaleTransform", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(125)]
    [DataRow(150)]
    [DataRow(200)]
    public void WeatherPanels_AreIncludedInEveryLogicalDpiGate(int dpiPercent)
    {
        var source = Text("tests/RAWSelectionAssistant.WpfTests/Version220CalendarUiTests.cs");
        StringAssert.Contains(source, "new BookingWeatherPanel()");
        StringAssert.Contains(source, dpiPercent switch { 125 => "1024, 640", 150 => "854, 534", _ => "720, 480" });
        var weather = Text("src/RAWSelectionAssistant/Views/BookingWeatherPanel.xaml");
        Assert.IsFalse(weather.Contains("ScaleTransform", StringComparison.Ordinal));
        Assert.IsFalse(weather.Contains("MinWidth=", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WeatherPanels_UseDynamicThemesHighContrastResourcesAndAccessibilityNames()
    {
        var weather = Text("src/RAWSelectionAssistant/Views/BookingWeatherPanel.xaml");
        StringAssert.Contains(weather, "DynamicResource");
        StringAssert.Contains(weather, "AutomationProperties.Name");
        StringAssert.Contains(weather, "AutomationProperties.HelpText");
        Assert.IsFalse(weather.Contains("#FFFFFF", StringComparison.OrdinalIgnoreCase));
        var theme = Text("src/RAWSelectionAssistant/Resources/DesignSystem/Theme.HighContrast.xaml");
        StringAssert.Contains(theme, "ResourceDictionary");
    }

    [TestMethod] public void MainWorkbench_PreservesResponsiveTaskCenterAndScheduleRows()
    {
        var xaml = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        StringAssert.Contains(xaml, "x:Name=\"WorkbenchTaskColumn\" Width=\"320\"");
        StringAssert.Contains(xaml, "WorkbenchCalendarSummaryView Grid.Row=\"4\"");
        StringAssert.Contains(xaml, "RecentProjectsArea");
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
