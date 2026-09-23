using System.Globalization;
using System.IO;
using System.Xml.Linq;
using System.Threading;
using System.Windows;
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
        var rating = document.Descendants().Single(element => element.Name.LocalName == "PixelTartRatingControl");
        StringAssert.Contains((string?)rating.Attribute("Command") ?? string.Empty, "RateCommand");
        StringAssert.Contains((string?)rating.Attribute("Rating") ?? string.Empty, "SelectedAsset.Rating");
        var control = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "PixelTartRatingControl.cs"));
        foreach (var key in new[] { "Key.D0", "Key.D1", "Key.D2", "Key.D3", "Key.D4", "Key.D5", "Key.Left", "Key.Right", "Key.Enter" }) StringAssert.Contains(control, key);
        StringAssert.Contains(control, "AutomationProperties.SetItemStatus");
    }

    [TestMethod]
    public void SharedRatingHoverIsPreviewOnlyAndAllThreeSurfacesUseTheControl()
    {
        foreach (var relative in new[] { "src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml", "src/RAWSelectionAssistant/Views/TetherCaptureView.xaml", "src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml" })
        {
            var document = XDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), relative)));
            Assert.IsTrue(document.Descendants().Any(element => element.Name.LocalName == "PixelTartRatingControl"), relative);
        }
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var control = new PixelTartRatingControl { Rating = 3 };
                control.HoverRating = 5;
                Assert.AreEqual(3, control.Rating);
                Assert.AreEqual(5, control.PreviewRating);
                control.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
                Assert.IsNull(control.PreviewRating);
                Assert.AreEqual(3, control.Rating);
            }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        if (error is not null) throw error;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
