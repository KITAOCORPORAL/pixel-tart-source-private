using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class UnifiedFilterColorPickerTests
{
    [TestMethod]
    public void ColorPickerHasDedicatedPopupAndUsesBoundedDebounce()
    {
        var root = RepositoryRoot();
        var composer = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetQueryComposerView.xaml"));
        var picker = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetColorFilterPicker.xaml"));
        var popup = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.Popups.cs"));
        var pageCode = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.cs"));
        StringAssert.Contains(picker, "AssetFilterColorPicker");
        foreach (var color in new[] { "#D75A45", "#D9822B", "#D6B94C", "#3E9B68", "#467DC2", "#8A65B8", "#808080" }) StringAssert.Contains(picker, color);
        StringAssert.Contains(picker, "SearchPaletteColorCommand");
        StringAssert.Contains(picker, "AssetFilterColorPlane");
        StringAssert.Contains(picker, "AssetFilterHueSlider");
        StringAssert.Contains(picker, "AssetFilterColorRange");
        Assert.DoesNotContain("AssetFilterColorPicker", composer);
        StringAssert.Contains(popup, "PopupContent(new AssetColorFilterPicker");
        StringAssert.Contains(composer, "P3QueryRuleTree");
        StringAssert.Contains(viewModel, "TimeSpan.FromMilliseconds(80)");
        Assert.DoesNotContain("视觉筛选", pageCode, StringComparison.Ordinal);
        Assert.DoesNotContain("DeltaE", composer, StringComparison.Ordinal);
        var queryModel = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant.Core", "Models", "AssetQueryModels.cs"));
        StringAssert.Contains(queryModel, "Camera");
        StringAssert.Contains(queryModel, "Lens");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
