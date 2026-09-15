using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class InspirationBoardUnifiedUxTests
{
    [TestMethod]
    public void TrayAndCollectionsShareOneInspirationBoardSurface()
    {
        var root = RepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.P2Browser.cs"));
        StringAssert.Contains(xaml, "AutomationProperties.AutomationId=\"InspirationBoardPanel\"");
        StringAssert.Contains(xaml, "Text=\"临时收集\"");
        StringAssert.Contains(xaml, "Text=\"已保存\"");
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding InspirationTrayCards}\"");
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding InspirationCollections}\"");
        StringAssert.Contains(code, "打开灵感板");
        Assert.DoesNotContain("打开灵感托盘", code, StringComparison.Ordinal);
        Assert.DoesNotContain("打开灵感集", code, StringComparison.Ordinal);
        StringAssert.Contains(viewModel, "SqliteInspirationTrayService");
        StringAssert.Contains(viewModel, "IsTemporaryInspirationSelected");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
