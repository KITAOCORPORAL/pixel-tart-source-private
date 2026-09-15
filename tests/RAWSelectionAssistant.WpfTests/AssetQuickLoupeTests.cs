using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetQuickLoupeTests
{
    [TestMethod]
    public void QuickLoupeUsesDelayedHighQualityPreviewAndBoundedCache()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var provider = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetQuickLoupePreviewProvider.cs"));
        StringAssert.Contains(page, "QuickLoupeDelayMilliseconds = 420");
        StringAssert.Contains(page, "ShowQuickLoupeAsync");
        StringAssert.Contains(page, "HideQuickLoupe");
        StringAssert.Contains(xaml, "AssetQuickLoupePopup");
        StringAssert.Contains(xaml, "AssetQuickLoupeImage");
        StringAssert.Contains(provider, "DecodePixelWidth = 1600");
        StringAssert.Contains(provider, "CacheLimit = 4");
        Assert.DoesNotContain("File.Write", provider, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete", provider, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Move", provider, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
