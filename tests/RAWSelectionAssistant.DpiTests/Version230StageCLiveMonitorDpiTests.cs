namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class Version230StageCLiveMonitorDpiTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    [DataRow(100)]
    [DataRow(125)]
    [DataRow(150)]
    [DataRow(175)]
    [DataRow(200)]
    public void StageC_DeclaresAllRequiredLogicalDpiScales(int scale)
    {
        CollectionAssert.Contains(new[] { 100, 125, 150, 175, 200 }, scale);
        var view = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        StringAssert.Contains(view, "DynamicResource"); Assert.DoesNotContain("DpiScale", view, StringComparison.Ordinal); Assert.DoesNotContain("LayoutTransform", view, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("Theme.Dark.xaml")]
    [DataRow("Theme.Light.xaml")]
    [DataRow("Theme.HighContrast.xaml")]
    public void StageC_UsesSharedDarkLightAndHighContrastThemes(string theme)
    {
        Assert.IsTrue(File.Exists(Path.Combine(Root, "src", "RAWSelectionAssistant", "Resources", "DesignSystem", theme)));
        var view = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        Assert.DoesNotContain("#FFFFFF", view, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("Background=\"White\"", view, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Compact1280_CollapsesInspectorAndKeepsCentralMinimum()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"); var code = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs");
        foreach (var token in new[] { "MinWidth=\"640\"", "MinWidth=\"220\"", "windowWidth < 1350", "InspectorColumn.Width = compact ? new GridLength(0)", "InspectorDrawer" }) StringAssert.Contains(xaml + code, token);
    }

    [TestMethod]
    public void Full1600Workspace_UsesRecommendedColumnBounds()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        foreach (var token in new[] { "Width=\"270\" MinWidth=\"220\" MaxWidth=\"300\"", "MinWidth=\"640\"", "Width=\"320\" MinWidth=\"280\" MaxWidth=\"340\"" }) StringAssert.Contains(xaml, token);
    }

    [TestMethod]
    public void Inspector_IsScrollableAndHistogramHasStableLogicalHeight()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        StringAssert.Contains(xaml, "VerticalScrollBarVisibility=\"Auto\""); StringAssert.Contains(xaml, "TetherHistogramView"); StringAssert.Contains(xaml, "Height=\"132\"");
    }

    [TestMethod]
    public void StarsColorsAndNotesRemainThemeAwareAndComplete()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        foreach (var star in new[] { "CommandParameter=\"0\"", "CommandParameter=\"1\"", "CommandParameter=\"2\"", "CommandParameter=\"3\"", "CommandParameter=\"4\"", "CommandParameter=\"5\"" }) StringAssert.Contains(xaml, star);
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"颜色标签\""); StringAssert.Contains(xaml, "AutomationProperties.Name=\"摄影师备注\""); StringAssert.Contains(xaml, "AutomationProperties.Name=\"客户备注\"");
    }

    [TestMethod]
    public void FullscreenExitAndKeyboardFocusPathsAreVisible()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"); var code = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs") + Text("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        foreach (var token in new[] { "FullScreenButtonText", "Key.F11", "Key.Escape", "Key.Left", "Key.Right", "Key.Up", "Key.Down", "Key.Enter", "Key.D5" }) StringAssert.Contains(xaml + code, token);
    }

    [TestMethod]
    public void StageC_HasRequiredAutomationNamesAndHelpText()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        foreach (var token in new[] { "AutomationProperties.Name=\"联机拍摄现场监看工作区\"", "AutomationProperties.Name=\"联机文件列表\"", "AutomationProperties.Name=\"RGB直方图\"", "AutomationProperties.Name=\"构图辅助线\"", "AutomationProperties.HelpText=\"只设置拒绝标记，不删除、不移动照片\"" }) StringAssert.Contains(xaml, token);
    }

    [TestMethod]
    public void StageC_ReleaseSafetyStillUsesWinExeNoneProviderAndNoLocalhost()
    {
        var project = Text("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj"); var app = Text("src/RAWSelectionAssistant/App.xaml.cs"); var settings = Text("src/RAWSelectionAssistant/appsettings.license.json"); var monitoring = Text("src/RAWSelectionAssistant/Services/TetherMonitoringImageServices.cs");
        StringAssert.Contains(project, "<OutputType>WinExe</OutputType>"); StringAssert.Contains(settings, "\"Provider\": \"None\""); StringAssert.Contains(app, "allowMockProvider: false"); Assert.DoesNotContain("FakeCamera", app, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("localhost", monitoring, StringComparison.OrdinalIgnoreCase);
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string FindRepositoryRoot() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}
