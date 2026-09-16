using System.IO;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetNoLetterboxTests
{
    [TestMethod]
    public void GridAllocatesEachPhotoItsOwnAspectRatioAndUsesTransparentImageStage()
    {
        var layout = AssetLayoutEngine.Arrange(AssetLibraryViewMode.Grid, [2d, .5d], 500, 200);
        Assert.HasCount(2, layout.Items);
        Assert.AreEqual(2d, layout.Items[0].Width / (layout.Items[0].Height - 40d), .01d);
        Assert.AreEqual(.5d, layout.Items[1].Width / (layout.Items[1].Height - 40d), .01d);
        Assert.AreNotEqual(layout.Items[0].Height, layout.Items[1].Height);

        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        StringAssert.Contains(xaml, "AssetGridThumbnail");
        StringAssert.Contains(xaml, "Background=\"Transparent\" ClipToBounds=\"True\"");
        StringAssert.Contains(xaml, "Stretch=\"Uniform\"");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
