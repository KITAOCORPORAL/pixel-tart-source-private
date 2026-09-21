using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using System.Text.Json;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PixelTartFilmTests
{
    [TestMethod]
    public void DisabledFilmAndZeroParametersAreExactIdentity()
    {
        var source = Fixture();
        var disabled = PixelTartFilmPipeline.Apply(source, new(Enabled: false, GrainAmount: 80, VignetteAmount: 90));
        var zero = PixelTartFilmPipeline.Apply(source, new(Enabled: true));
        CollectionAssert.AreEqual(source.Rgb24.ToArray(), disabled.Rgb24.ToArray());
        CollectionAssert.AreEqual(source.Rgb24.ToArray(), zero.Rgb24.ToArray());
    }

    [TestMethod]
    public void GrainIsDeterministicAndSeedChangesOnlyOnExplicitChange()
    {
        var source = Fixture();
        var settings = new PixelTartFilmSettings(Enabled: true, GrainAmount: 70, Seed: 42);
        var first = PixelTartFilmPipeline.Apply(source, settings).Rgb24.ToArray();
        var second = PixelTartFilmPipeline.Apply(source, settings).Rgb24.ToArray();
        CollectionAssert.AreEqual(first, second);
        CollectionAssert.AreNotEqual(first, PixelTartFilmPipeline.Apply(source, settings with { Seed = 43 }).Rgb24.ToArray());
    }

    [TestMethod]
    public void HalationIsLocalizedToHighlightsAndBlackStaysBlack()
    {
        var source = new VisualPixelBuffer(3, 1, new byte[] { 0, 0, 0, 255, 255, 255, 80, 80, 80 });
        var result = PixelTartFilmPipeline.Apply(source, new(Enabled: true, HalationAmount: 100));
        Assert.AreEqual(new VisualRgb24(0, 0, 0), new(result.Rgb24.Span[0], result.Rgb24.Span[1], result.Rgb24.Span[2]));
        Assert.IsGreaterThanOrEqualTo(result.Rgb24.Span[3], result.Rgb24.Span[4]);
        Assert.IsLessThanOrEqualTo((byte)82, result.Rgb24.Span[6]);
        Assert.IsLessThanOrEqualTo((byte)82, result.Rgb24.Span[7]);
        Assert.IsLessThanOrEqualTo((byte)82, result.Rgb24.Span[8]);
    }

    [TestMethod]
    public void ProfilesArePixelTartOwnedAndValidateBounds()
    {
        CollectionAssert.AreEquivalent(new[] { "PT-N01", "PT-W01", "PT-C01" }, PixelTartFilmProfiles.All.Select(x => x.Id).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelTartFilmSettings(GrainAmount: 101).Validate());
    }

    [TestMethod]
    public void BloomSpreadsNeutralLightBeyondHighlight()
    {
        var source = new VisualPixelBuffer(5, 1, new byte[] { 80, 80, 80, 255, 255, 255, 80, 80, 80, 80, 80, 80, 80, 80, 80 });
        var result = PixelTartFilmPipeline.Apply(source, new(Enabled: true, BloomAmount: 100));
        Assert.IsGreaterThan((byte)source.Rgb24.Span[6], result.Rgb24.Span[6]);
        Assert.AreEqual(result.Rgb24.Span[6], result.Rgb24.Span[7]);
        Assert.AreEqual(result.Rgb24.Span[7], result.Rgb24.Span[8]);
    }

    [TestMethod]
    public void HalationIsWarmEdgeAndBloomIsIndependent()
    {
        var source = new VisualPixelBuffer(5, 1, new byte[] { 120, 120, 120, 255, 255, 255, 120, 120, 120, 120, 120, 120, 120, 120, 120 });
        var halo = PixelTartFilmPipeline.Apply(source, new(Enabled: true, HalationAmount: 100));
        var bloom = PixelTartFilmPipeline.Apply(source, new(Enabled: true, BloomAmount: 100));
        Assert.IsGreaterThanOrEqualTo(halo.Rgb24.Span[4], halo.Rgb24.Span[3]);
        Assert.IsGreaterThanOrEqualTo(halo.Rgb24.Span[7], halo.Rgb24.Span[6]);
        Assert.AreNotEqual(halo.Rgb24.Span[6], bloom.Rgb24.Span[6]);
    }

    [TestMethod]
    public void GrainSizeIsContinuousAndTextureIsDeterministic()
    {
        var source = Fixture();
        var fine = PixelTartFilmPipeline.Apply(source, new(Enabled: true, GrainAmount: 100, GrainSize: 5, Seed: 9));
        var coarse = PixelTartFilmPipeline.Apply(source, new(Enabled: true, GrainAmount: 100, GrainSize: 95, Seed: 9));
        Assert.AreNotEqual(fine.Rgb24.ToArray(), coarse.Rgb24.ToArray());
        var texture = new PixelTartFilmSettings(true, SurfaceAmount: 100, TextureId: "Paper", TextureAmount: 100, Seed: 10);
        CollectionAssert.AreEqual(PixelTartFilmPipeline.Apply(source, texture).Rgb24.ToArray(), PixelTartFilmPipeline.Apply(source, texture).Rgb24.ToArray());
    }

    [TestMethod]
    public void FilmSettingsAndReferenceLookRoundTripThroughJson()
    {
        var now = DateTimeOffset.UtcNow;
        var look = new ReferenceLook(Guid.NewGuid(), "film", null, [new ReferenceLookSource(Guid.NewGuid(), null, "source", "source.png", "hash", 1, VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "source", Fixture())))], new(), now, now, Film: new(true, "PT-W01", GrainAmount: 42, TextureId: "Paper", TextureAmount: 80, Seed: 12));
        var json = JsonSerializer.Serialize(look);
        var roundTrip = JsonSerializer.Deserialize<ReferenceLook>(json);
        Assert.AreEqual(look.Film, roundTrip?.Film);
    }

    private static VisualPixelBuffer Fixture() => new(8, 8, Enumerable.Range(0, 64).SelectMany(i => new[] { (byte)(i * 3), (byte)(255 - i * 2), (byte)(i * 2) }).ToArray());
}
