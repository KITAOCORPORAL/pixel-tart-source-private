using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class V4TelemetryTests
{
    [TestMethod]
    public void TilePolicyAdaptsToBudgetAndResolution()
    {
        var low = GpuMemoryBudget.FromBytes(4L * 1000 * 1000 * 1000);
        var ultra = GpuMemoryBudget.FromBytes(16L * 1000 * 1000 * 1000);
        Assert.IsLessThanOrEqualTo(V4TilePolicy.SelectTileSize(10000, 8000, low), 384);
        Assert.IsGreaterThan(0, V4TilePolicy.SelectTileSize(1000, 1000, low));
        Assert.IsGreaterThan(1, V4TilePolicy.CountTiles(10000, 8000, V4TilePolicy.SelectTileSize(10000, 8000, ultra)));
    }

    [TestMethod]
    public void TelemetryTracksDispatchFallbackAndPeakAllocation()
    {
        var telemetry = new V4GpuTelemetry();
        telemetry.RecordDispatch(V4GpuStage.PairwiseOt, TimeSpan.FromMilliseconds(2), 100, 3);
        telemetry.RecordDispatch(V4GpuStage.PairwiseOt, TimeSpan.FromMilliseconds(1), 80, 1);
        telemetry.RecordFallback(true);
        Assert.AreEqual(2, telemetry.DispatchCounts[V4GpuStage.PairwiseOt]);
        Assert.AreEqual(100, telemetry.PeakTrackedAllocationBytes);
        Assert.AreEqual(1, telemetry.OomCount);
        Assert.AreEqual(4, telemetry.TileCount);
    }
}
