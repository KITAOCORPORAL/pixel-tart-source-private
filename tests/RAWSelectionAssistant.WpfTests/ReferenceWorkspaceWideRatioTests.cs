using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ReferenceWorkspaceWideRatioTests
{
    [TestMethod]
    public void WideLayoutKeepsTwentyOneSixtyOneEighteenColumns()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs"));
        StringAssert.Contains(source, "compact ? .24 : .21, GridUnitType.Star");
        StringAssert.Contains(source, "new GridLength(.61, GridUnitType.Star)");
        StringAssert.Contains(source, "new GridLength(.18, GridUnitType.Star)");
        Assert.IsFalse(source.Contains("CenterColumn.Width = new GridLength(1, GridUnitType.Star)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CompactLayoutCollapsesContextUntilUserOpensIt()
    {
        var viewModel = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs"));
        var code = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs"));
        StringAssert.Contains(viewModel, "SetResponsiveContext(bool compact)");
        StringAssert.Contains(code, "if (compact && !_wasCompact) _editor?.SetResponsiveContext(true)");
        StringAssert.Contains(viewModel, "private bool _contextRailOpen = true");
    }

    [TestMethod]
    public void FilmSurfaceUsesChineseTextureGridAndProfiles()
    {
        var film = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant.Core/Services/Projects/PixelTartFilm.cs"));
        var view = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml"));
        foreach (var value in new[] { "中性胶片", "暖调柔化", "冷银灰", "细纤维", "纸面颗粒", "柔雾", "扫描细纹" }) StringAssert.Contains(film, value);
        StringAssert.Contains(view, "SelectedValuePath=\"Id\"");
        StringAssert.Contains(view, "DisplayName");
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
