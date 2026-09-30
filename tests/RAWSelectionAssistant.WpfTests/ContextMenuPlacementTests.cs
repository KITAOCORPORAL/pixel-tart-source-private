using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PixelTart.Modules.AssetLibrary;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ContextMenuPlacementTests
{
    [TestMethod]
    public void PlacementFlipsLeftAtRightEdgeAndClampsVerticalBounds()
    {
        var result = ContextMenuPlacement.Calculate(new Rect(900, 580, 80, 30), new Size(240, 180), new Rect(0, 0, 1000, 700));
        Assert.IsTrue(result.OpensLeft);
        Assert.AreEqual(660, result.Left, .001);
        Assert.AreEqual(520, result.Top, .001);
    }

    [TestMethod]
    public void PlacementUsesRightWhenThereIsRoom()
    {
        var result = ContextMenuPlacement.Calculate(new Rect(200, 200, 80, 30), new Size(240, 180), new Rect(0, 0, 1000, 700));
        Assert.IsFalse(result.OpensLeft);
        Assert.AreEqual(280, result.Left, .001);
        Assert.AreEqual(200, result.Top, .001);
    }
}
