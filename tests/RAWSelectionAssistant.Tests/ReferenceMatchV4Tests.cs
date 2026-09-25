using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ReferenceMatchV4Tests
{
    [TestMethod]
    public void CpuFoundationIsDeterministicAndUsesBoundedSamples()
    {
        var source = Fixture(96, 64, 35, 70, 120); var reference = Fixture(96, 64, 180, 125, 70);
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 64, SinkhornIterations: 8, ResidualIterations: 1);
        var first = new ReferenceMatchV4Engine().Match(source, reference, settings: settings, preferGpu: false);
        var second = new ReferenceMatchV4Engine().Match(source, reference, settings: settings, preferGpu: false);
        CollectionAssert.AreEqual(first.Pixels.Rgb24.ToArray(), second.Pixels.Rgb24.ToArray());
        Assert.IsLessThanOrEqualTo(64, first.RepresentativeSourceCount);
        Assert.AreEqual(ReferenceMatchV4BackendKind.Cpu, first.Backend);
    }

    [TestMethod]
    public void UnavailableGpuFallsBackToCpuAndCacheKeyTracksSettings()
    {
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 32);
        var result = new ReferenceMatchV4Engine().Match(Fixture(12, 8, 20, 20, 20), Fixture(12, 8, 220, 180, 100),
            sourceIdentity: "a", referenceIdentity: "b", settings: settings, preferGpu: true);
        Assert.AreEqual(ReferenceMatchV4BackendKind.Cpu, result.Backend);
        Assert.IsTrue(result.UsedCpuFallback);
        StringAssert.Contains(result.CacheKey, "a|b|v4|");
        Assert.AreNotEqual(result.CacheKey, ReferenceMatchV4Cache.CreateKey("a", "b", settings with { TileSize = 256 }));
    }

    [TestMethod]
    public void CancellationStopsOtAndTilePlannerProvidesOverlap()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => new ReferenceMatchV4Engine().Match(Fixture(64, 64, 1, 2, 3), Fixture(64, 64, 220, 200, 180), token: cancellation.Token));
        var tiles = ColorMatchTilePlanner.Plan(1024, 768, new ReferenceMatchV4Settings(TileSize: 256, TileOverlap: 8));
        Assert.IsGreaterThan(1, tiles.Count); Assert.IsTrue(tiles.Any(tile => tile.SourceX < tile.X || tile.SourceY < tile.Y));
    }

    [TestMethod]
    public void ProtectionAndGamutMappingStayFinite()
    {
        var result = new ReferenceMatchV4Engine().Match(Fixture(32, 32, 128, 128, 128), Fixture(32, 32, 255, 0, 255),
            settings: new ReferenceMatchV4Settings(MaximumChromaDisplacement: .02, SinkhornIterations: 4), preferGpu: false);
        Assert.IsTrue(result.Pixels.Rgb24.ToArray().All(value => value <= 255));
        Assert.IsLessThanOrEqualTo(2, result.ResidualIterations);
        Assert.HasCount(32 * 32 * 3, result.Pixels.Rgb24.ToArray());
    }

    private static VisualPixelBuffer Fixture(int width, int height, byte r, byte g, byte b)
    {
        var bytes = new byte[width * height * 3];
        for (var i = 0; i < bytes.Length; i += 3) { bytes[i] = (byte)Math.Clamp(r + (i / 3) % 17, 0, 255); bytes[i + 1] = (byte)Math.Clamp(g + (i / 5) % 13, 0, 255); bytes[i + 2] = (byte)Math.Clamp(b + (i / 7) % 11, 0, 255); }
        return new(width, height, bytes);
    }
}
