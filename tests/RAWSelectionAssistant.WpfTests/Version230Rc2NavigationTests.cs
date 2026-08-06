using System.IO;
using RAWSelectionAssistant.Core.Services.Tethering;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc2NavigationTests
{
    [TestMethod]
    public void Navigation_ExposesExplicitPageLifecycleAndSingleTaskCenter()
    {
        var main = Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        var window = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        StringAssert.Contains(main, "TetherPage?.OnDeactivated()");
        StringAssert.Contains(main, "TetherPage?.OnActivated()");
        StringAssert.Contains(main, "PageChanged?.Invoke");
        Assert.AreEqual(1, Count(window, "x:Name=\"TaskCenterPanel\""));
        Assert.DoesNotContain("UnifiedTaskCenterPanel", window, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Toolbox_ClosesBeforeNavigationAndPinUsesGlyphWithTooltip()
    {
        var code = Text("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var xaml = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        var method = Slice(code, "private void ToolboxItem_Click", "private void QuickToolsOverflowButton_Click");
        var close = method.IndexOf("WorkbenchToolboxPopup.IsOpen = false;", StringComparison.Ordinal);
        var navigate = method.IndexOf("NavigateCommand.Execute(targetPage)", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, close);
        Assert.IsGreaterThan(close, navigate);
        StringAssert.Contains(xaml, "Content=\"{Binding PinGlyph}\"");
        StringAssert.Contains(xaml, "ToolTip=\"{Binding PinToolTip}\"");
    }

    [TestMethod]
    public void TetherDeactivation_ReleasesPageImagesWithoutStoppingSession()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs");
        var method = Slice(source, "public void OnDeactivated()", "public async ValueTask DisposeAsync()");
        foreach (var value in new[] { "CancelCurrent", "ReleaseExcept(null)", "CurrentImage = null", "Histogram = null", "ExifInfo = null", "ReleaseThumbnail", "ReleasePageImageResources" })
            StringAssert.Contains(method, value);
        Assert.DoesNotContain("StopAsync", method, StringComparison.Ordinal);
        Assert.DoesNotContain("DisposeAsync", method, StringComparison.Ordinal);
    }

    [TestMethod]
    public void LutCoordinator_ExposesBoundedCancellationForPageDeactivation()
    {
        using var coordinator = new LutRenderRequestCoordinator();
        var request = coordinator.Begin();
        coordinator.CancelCurrent();
        Assert.IsTrue(request.Token.IsCancellationRequested);
        Assert.IsFalse(coordinator.IsCurrent(request.Version));
    }

    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
    private static int Count(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
    private static string Slice(string text, string start, string end)
    {
        var startIndex = text.IndexOf(start, StringComparison.Ordinal);
        var endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex);
        Assert.IsGreaterThan(startIndex, endIndex);
        return text[startIndex..endIndex];
    }
}
