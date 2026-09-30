using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class HomeThumbnailLayoutTests
{
    [TestMethod]
    [DataRow(420d)]
    [DataRow(980d)]
    [DataRow(1640d)]
    public void MaximumThumbnailUsesViewportAndPreservesPortraitAndLandscape(double width)
    {
        foreach (var mode in new[] { AssetLibraryViewMode.Grid, AssetLibraryViewMode.Masonry, AssetLibraryViewMode.Justified })
        {
            var ratios = new[] { 1.5, 2d / 3, 1d };
            var result = AssetLayoutEngine.Arrange(mode, ratios, width, width - 24);
            for (var i = 0; i < ratios.Length; i++)
            {
                Assert.AreEqual(width, result.Items[i].Width, .01);
                Assert.AreEqual(ratios[i], result.Items[i].Width / (result.Items[i].Height - 40), .01);
                Assert.IsLessThanOrEqualTo(width, result.Items[i].Right);
                if (i > 0) Assert.IsGreaterThanOrEqualTo(result.Items[i - 1].Bottom, result.Items[i].Top);
            }
        }
    }
}
