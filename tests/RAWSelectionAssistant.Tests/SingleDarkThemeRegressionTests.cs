namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class SingleDarkThemeRegressionTests
{
    [TestMethod]
    public void ProductTheme_HasNoUserThemeSelector()
    {
        var main = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        StringAssert.Contains(main, "PixelTart Dark Theme");
        Assert.DoesNotContain("跟随 Windows", main);
        Assert.DoesNotContain("浅色模式", main);
        Assert.DoesNotContain("深色模式", main);
        Assert.DoesNotContain("ThemeOptions", main);
        Assert.DoesNotContain("AccentOptions", main);
    }

    [TestMethod]
    public void AppearanceService_ResolvesDarkExceptOsHighContrast()
    {
        var source = Text("src/RAWSelectionAssistant/Services/AppearanceService.cs");
        StringAssert.Contains(source, "var highContrast = SystemParameters.HighContrast");
        StringAssert.Contains(source, "highContrast ? \"HighContrast\" : ResolveTheme");
        StringAssert.Contains(source, "private static string ResolveTheme(AppThemeMode mode) => \"Dark\"");
    }

    [TestMethod]
    public void AssetLibrarySurfaces_DoNotUseSystemWindowOrControlBrushes()
    {
        var files = new[] { "src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml", "src/PixelTart.Modules.AssetLibrary/AssetLibraryP3Styles.xaml", "src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Menu.xaml", "src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml" };
        foreach (var file in files)
        {
            var source = Text(file);
            Assert.DoesNotContain("SystemColors.WindowBrush", source, file);
            Assert.DoesNotContain("SystemColors.ControlBrush", source, file);
            Assert.DoesNotContain("Background=\"White\"", source, file);
        }
    }

    [TestMethod]
    public void CalendarAndPopupResources_UseDarkSurfaceContract()
    {
        var inputs = Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml");
        StringAssert.Contains(inputs, "PixelTartCalendarNativeStyle");
        StringAssert.Contains(inputs, "SurfaceElevatedBrush");
        StringAssert.Contains(inputs, "CalendarDayButton");
        var menu = Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Menu.xaml");
        StringAssert.Contains(menu, "MenuPopupBackgroundBrush");
        StringAssert.Contains(menu, "MenuItemHoverBrush");
    }

    [TestMethod]
    public void NewLibraryDialog_UsesNativeDarkSurface()
    {
        var source = Text("src/PixelTart.Modules.AssetLibrary/NewAssetLibraryDialog.cs");
        StringAssert.Contains(source, "WindowBackgroundBrush");
        StringAssert.Contains(source, "RaisedSurfaceBrush");
        StringAssert.Contains(source, "BorderStrongBrush");
        StringAssert.Contains(source, "WindowStyle = WindowStyle.None");
    }

    [TestMethod]
    public void ThumbnailContract_NeverAddsBlackMatteOrStretchFill()
    {
        var page = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml");
        var thumb = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryThumbnail.xaml");
        Assert.DoesNotContain("Stretch=\"Fill\"", page);
        Assert.DoesNotContain("Background=\"Black\"", page);
        Assert.DoesNotContain("Background=\"#000000\"", page);
        StringAssert.Contains(thumb, "Value=\"Transparent\"");
        StringAssert.Contains(page, "Stretch=\"Uniform\"");
    }

    private static string Text(string relativePath)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "RAWSelectionAssistant.sln"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(root!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}

