using System.Globalization;
using System.IO;
using System.Xml.Linq;
using PixelTart.Modules.AssetLibrary;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class InspectorStarRatingTests
{
    [TestMethod]
    public void InspectorHasFiveAccessibleCommandBoundStars()
    {
        var converter = new RatingAtLeastConverter();
        Assert.IsTrue((bool)converter.Convert(4, typeof(bool), "4", CultureInfo.InvariantCulture));
        Assert.IsFalse((bool)converter.Convert(4, typeof(bool), "5", CultureInfo.InvariantCulture));

        var document = XDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml")));
        var stars = document.Descendants().Where(element => element.Name.LocalName == "ToggleButton" &&
            ((string?)element.Attribute("Content") ?? string.Empty) == "★").ToArray();
        Assert.HasCount(5, stars);
        CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5" }, stars.Select(element => (string?)element.Attribute("CommandParameter")).ToArray());
        Assert.IsTrue(stars.All(element => ((string?)element.Attribute("Command") ?? string.Empty).Contains("RateCommand", StringComparison.Ordinal)));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
