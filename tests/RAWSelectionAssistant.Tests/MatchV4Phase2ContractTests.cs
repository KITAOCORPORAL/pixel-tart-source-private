using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class MatchV4Phase2ContractTests
{
    [TestMethod]
    public async Task TileExecutorPreservesIdentityTransformWithoutSeams()
    {
        var values = Enumerable.Range(0, 64 * 48 * 3).Select(i => (i % 257) / 256f).ToArray();
        var source = new HighBitDepthImageBuffer(64, 48, values);
        var settings = new ReferenceMatchV4Settings(TileSize: 32, TileOverlap: 0);
        var result = await new MatchV4TileExecutor().ExecuteAsync(source, settings,
            (tile, token) => Task.FromResult<ReadOnlyMemory<float>>(tile.ToArray()));
        CollectionAssert.AreEqual(values, result.Rgb32.ToArray());
    }

    [TestMethod]
    public void VramTierChangesThroughputDescriptorOnly()
    {
        var four = GpuMemoryBudget.FromBytes(4L * 1000 * 1000 * 1000);
        var eight = GpuMemoryBudget.FromBytes(8L * 1000 * 1000 * 1000);
        var sixteen = GpuMemoryBudget.FromBytes(16L * 1000 * 1000 * 1000);
        Assert.AreEqual("LOW", four.Tier);
        Assert.AreEqual("STANDARD", eight.Tier);
        Assert.AreEqual("ULTRA", sixteen.Tier);
        Assert.AreNotEqual(four.TileSize, sixteen.TileSize);
    }

    [TestMethod]
    public async Task PlatformNeutralCpuBackendReportsTimingsAndSameMapping()
    {
        var source = new[] { new OklabColor(.4, .01, .02), new OklabColor(.6, -.02, .01) };
        var reference = new[] { new OklabColor(.5, .02, -.01), new OklabColor(.7, -.01, .02) };
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 16, SinkhornIterations: 2);
        var backend = new MatchV4CpuBackend();
        var result = await backend.ExecuteAsync(source, reference, settings);
        Assert.HasCount(2, result.MappedSamples);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.Zero, result.ComputeTime);
        Assert.AreEqual(ReferenceMatchV4BackendKind.Cpu, backend.Kind);
    }
}
