using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PixelTart.Modules.AssetLibrary;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ContextMenuPlacementTests
{
    [TestMethod]
    public void InspectionStaysWithOwnerOnNegativeOriginMonitorAtEveryDpi()
    {
        foreach (var dpi in new[] { 1d, 1.25, 1.5, 2d })
        {
            var work = new Rect(-2560 / dpi, 80 / dpi, 2560 / dpi, 1360 / dpi);
            var owner = new Rect(-2400 / dpi, 120 / dpi, 1600 / dpi, 920 / dpi);
            var result = ContextMenuMonitor.InspectionBounds(owner, work, new Size(620, 700));
            Assert.IsTrue(work.Contains(result), $"Inspection escaped the owner's monitor at {dpi}");
            Assert.IsLessThan(0d, result.Right);
            Assert.IsTrue(result.IntersectsWith(owner));
            var oversized = ContextMenuMonitor.InspectionBounds(owner, work, new Size(4000, 4000));
            Assert.AreEqual(work, oversized);
        }
    }
    [TestMethod]
    public void PlacementClampsOversizedMenuAndSupportsNegativeMonitorOrigins()
    {
        var result = ContextMenuPlacement.Calculate(new Rect(-90, -20, 80, 30), new Size(240, 180), new Rect(-1000, 0, 1000, 700));
        Assert.IsTrue(result.OpensLeft);
        Assert.AreEqual(-330, result.Left);
        Assert.AreEqual(0, result.Top);
        var oversized = ContextMenuPlacement.Calculate(new Rect(40, 10, 80, 30), new Size(600, 800), new Rect(0, 0, 500, 700));
        Assert.AreEqual(0, oversized.Left);
        Assert.AreEqual(0, oversized.Top);
    }
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
