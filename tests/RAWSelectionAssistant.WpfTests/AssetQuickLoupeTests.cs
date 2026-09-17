using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetQuickLoupeTests
{
    [TestMethod]
    public void QuickLoupeUsesExplicitHoverButtonAndSharedHighQualityPreview()
    {
        var root = RepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var provider = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetThumbnailProvider.cs"));
        Assert.DoesNotContain("UpdateQuickLoupeCandidate", page, StringComparison.Ordinal);
        StringAssert.Contains(page, "ShowQuickLoupeAsync");
        StringAssert.Contains(page, "HideQuickLoupe");
        StringAssert.Contains(page, "AssetPreviewPurpose.QuickLoupe");
        StringAssert.Contains(xaml, "AssetQuickLoupePopup");
        StringAssert.Contains(xaml, "AssetQuickLoupeImage");
        StringAssert.Contains(xaml, "AssetGridQuickLoupeButton");
        StringAssert.Contains(xaml, "QuickLoupeRevealHost");
        StringAssert.Contains(xaml, "DoubleAnimation");
        StringAssert.Contains(xaml, "AssetAction.Search");
        Assert.DoesNotContain("Content=\"&#xE71E;\"", xaml, StringComparison.Ordinal);
        StringAssert.Contains(xaml, "MouseEnter=\"QuickLoupeButton_MouseEnter\"");
        StringAssert.Contains(provider, "IAssetPreviewProvider");
        StringAssert.Contains(provider, "DefaultMemoryBudgetBytes");
        Assert.DoesNotContain("CacheLimit", page, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
