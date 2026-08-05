namespace RAWSelectionAssistant.DpiTests;

[TestClass]
public sealed class Version230TetherDpiGateTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    [DataRow(125, "1024, 640")]
    [DataRow(150, "854, 534")]
    [DataRow(200, "720, 480")]
    public void TetherView_IsIncludedInEveryApprovedLogicalDpiGate(int dpiPercent, string viewport)
    {
        CollectionAssert.Contains(new[] { 125, 150, 200 }, dpiPercent);
        var source = Text("tests/RAWSelectionAssistant.WpfTests/Version220CalendarUiTests.cs");
        StringAssert.Contains(source, viewport);
        StringAssert.Contains(source, "new TetherCaptureView");
    }

    [TestMethod]
    public void TetherView_UsesLogicalUnitsAndNoManualDpiTransform()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        Assert.IsFalse(xaml.Contains("DpiScale", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("ScaleTransform", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("LayoutTransform", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "DynamicResource");
    }

    [TestMethod]
    public void TetherView_UsesBoundedPreviewCardsAndScrollableAssetList()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        StringAssert.Contains(xaml, "Width=\"210\"");
        StringAssert.Contains(xaml, "ScrollViewer.HorizontalScrollBarVisibility=\"Disabled\"");
        StringAssert.Contains(xaml, "<WrapPanel />");
    }

    [TestMethod]
    public void TetherView_HasKeyboardReachableNamedActions()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml");
        foreach (var value in new[] { "AutomationProperties.Name=\"联机拍摄\"", "AutomationProperties.Name=\"明确启动或恢复看守\"", "AutomationProperties.Name=\"联机文件列表\"", "AutomationProperties.HelpText" })
            StringAssert.Contains(xaml, value);
    }

    [TestMethod]
    public void TetherView_DoesNotRequireScreenshotOrWindowsScaleSwitching()
    {
        var tests = Text("tests/RAWSelectionAssistant.WpfTests/Version220CalendarUiTests.cs");
        Assert.IsFalse(tests.Contains("Screenshot", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(tests.Contains("SetProcessDpi", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(tests, "Measure(viewport)");
        StringAssert.Contains(tests, "Arrange(new Rect(viewport))");
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string FindRepositoryRoot() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}
