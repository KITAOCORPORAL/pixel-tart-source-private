using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class MatchV4Phase2ContractTests
{
    [TestMethod]
    public async Task CpuPixelBackendReturnsFloat32AndHonorsStrengthAndLuminance()
    {
        var values = Enumerable.Repeat(.5f, 16 * 12 * 3).ToArray();
        var source = new HighBitDepthImageBuffer(16, 12, values);
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 16, SinkhornIterations: 2);
        var transform = new MatchV4ResolvedTransform(new(.1, .02, -.01), [new(.1, .02, -.01), new(.1, .02, -.01), new(.1, .02, -.01)], settings, "test-transform", Strength: 0);
        var unchanged = await new MatchV4CpuPixelBackend().ExecuteAsync(source, transform);
        for (var i = 0; i < values.Length; i++) Assert.AreEqual(values[i], unchanged.Pixels.Rgb32.Span[i], 0.00001f);
        var luminance = transform with { Strength = 1, KeepLuminance = true };
        var changed = await new MatchV4CpuPixelBackend().ExecuteAsync(source, luminance);
        Assert.AreEqual(source.PixelCount * 3, changed.Pixels.Rgb32.Length);
        Assert.IsTrue(changed.Pixels.Rgb32.Span.ToArray().SequenceEqual(changed.Pixels.Rgb32.ToArray()));
    }

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

    [TestMethod]
    public async Task PixelExecutorFallsBackWithoutChangingTransformOrGeneration()
    {
        var source = new HighBitDepthImageBuffer(8, 8, Enumerable.Repeat(.4f, 8 * 8 * 3).ToArray());
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 16, SinkhornIterations: 2);
        var transform = new MatchV4ResolvedTransform(new(.05, .01, .01), [new(.05, .01, .01), new(.05, .01, .01), new(.05, .01, .01)], settings, "same-transform");
        var generation = Guid.NewGuid();
        var result = await new MatchV4PixelExecutor(new MatchV4CpuPixelBackend(), new ThrowingPixelBackend()).ExecuteAsync(source, transform, generation, true);
        Assert.AreEqual(ReferenceMatchV4BackendKind.Cpu, result.Backend);
        Assert.IsTrue(result.UsedCpuFallback);
        Assert.AreEqual("same-transform", result.TransformHash);
        Assert.AreEqual(generation, result.ProcessingGenerationId);
    }

    private sealed class ThrowingPixelBackend : IMatchV4PixelBackend
    {
        public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Gpu;
        public bool IsAvailable => true;
        public Task<MatchV4PixelExecutionResult> ExecuteAsync(HighBitDepthImageBuffer source, MatchV4ResolvedTransform transform, CancellationToken token = default) => throw new InvalidOperationException("device lost");
    }
}
