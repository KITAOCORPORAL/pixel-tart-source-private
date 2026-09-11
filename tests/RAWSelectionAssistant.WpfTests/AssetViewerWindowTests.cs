using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetViewerWindowTests
{
    [TestMethod]
    public void ViewerImplementsSourceSafeNavigationZoomAndPanContract()
    {
        var root = RepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetViewerWindow.cs"));
        foreach (var token in new[] { "上一张", "下一张", "适应", "100%", "MouseWheel", "Key.Escape", "BeginPan", "ContinuePan", "PanningMode.Both", "AsyncThumbnail.Provider" })
            StringAssert.Contains(source, token);
        Assert.DoesNotContain("File.Delete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Write", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Move", source, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
