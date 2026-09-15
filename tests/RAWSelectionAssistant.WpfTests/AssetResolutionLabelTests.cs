using System.IO;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetResolutionLabelTests
{
    [TestMethod]
    public void PhotoCardsExposeResolutionInsteadOfRatingMetadata()
    {
        var asset = new AssetItem(Guid.NewGuid(), "C:\\photos\\portrait.jpg", "portrait.jpg", ".jpg", "image", 1, null, 6048, 4024, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Assert.AreEqual("6048 × 4024", new AssetVisualMatchView(asset).DimensionsText);

        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        Assert.AreEqual(3, Count(xaml, "Text=\"{Binding DimensionsText}\" Style=\"{DynamicResource PixelTart.Type.Metadata}\" AutomationProperties.Name=\"素材分辨率\""));
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        for (var index = 0; (index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0; index += token.Length) count++;
        return count;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
