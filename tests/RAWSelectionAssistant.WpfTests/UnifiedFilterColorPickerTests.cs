using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class UnifiedFilterColorPickerTests
{
    [TestMethod]
    public void ColorPickerLivesInsideFilterAndUsesBoundedDebounce()
    {
        var root = RepositoryRoot();
        var composer = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetQueryComposerView.xaml"));
        var pageCode = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.cs"));
        StringAssert.Contains(composer, "AssetFilterColorPicker");
        foreach (var color in new[] { "#D75A45", "#D9822B", "#D6B94C", "#3E9B68", "#467DC2", "#8A65B8", "#808080" }) StringAssert.Contains(composer, color);
        StringAssert.Contains(composer, "SearchPaletteColorCommand");
        StringAssert.Contains(composer, "SearchColorCommand");
        StringAssert.Contains(viewModel, "TimeSpan.FromMilliseconds(180)");
        StringAssert.Contains(pageCode, "按颜色…");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
