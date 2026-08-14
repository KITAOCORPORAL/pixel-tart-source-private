using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryPreviewWpfTests
{
    [TestMethod]
    public void PreviewDefinesThreeColumnLibraryGridAndInspector()
    {
        var xaml = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml");
        foreach (var token in new[] { "AssetFolders", "SmartFolders", "AssetTags", "AssetGrid", "检查器", "素材网格", "加入文件夹", "添加标签" })
            StringAssert.Contains(xaml, token);
        StringAssert.Contains(xaml, "ColumnDefinition Width=\"250\"");
        StringAssert.Contains(xaml, "ColumnDefinition Width=\"310\"");
    }

    [TestMethod]
    public void PreviewUsesBoundedPagingAndVirtualizedItemContainers()
    {
        var xaml = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml");
        var viewModel = Read("src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs");
        StringAssert.Contains(xaml, "VirtualizingPanel.IsVirtualizing=\"True\"");
        StringAssert.Contains(xaml, "VirtualizationMode=\"Recycling\"");
        StringAssert.Contains(viewModel, "PageSize: 80");
        StringAssert.Contains(viewModel, "LoadMoreCommand");
    }

    [TestMethod]
    public void PreviewStatesReferenceSafetyAndDoesNotUseEagleBranding()
    {
        var all = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml") + Read("src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs");
        StringAssert.Contains(all, "默认不移动、不改名、不删除源文件");
        StringAssert.Contains(all, "未修改源文件");
        Assert.DoesNotContain("Eagle", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Delete(", all, StringComparison.Ordinal);
    }

    [TestMethod]
    public void PreviewIsIsolatedFromFormalDatabaseAndP0Shell()
    {
        var host = Read("src/PixelTart.AssetLibrary.Preview/MainWindow.xaml.cs") + Read("src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs");
        StringAssert.Contains(host, "KitaoPhotoSelector.AssetLibraryPreview");
        Assert.DoesNotContain("pixel-tart.db", host, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAWSelectionAssistant.MainWindow", host, StringComparison.Ordinal);
        Assert.DoesNotContain("InputRouting", host, StringComparison.Ordinal);
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
