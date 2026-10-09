using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ReferencePixelStatisticsTests
{
    [TestMethod]
    public void TransparentPixelsAreExcludedAndDomainIsOklabLightness()
    {
        var image = new VisualPixelBuffer(2, 1, new byte[] { 255, 0, 0, 128, 128, 128 }, new byte[] { 0, 255 });
        var result = ReferencePixelStatistics.FromPixels(image);
        Assert.AreEqual(1, result.SampleCount);
        Assert.AreEqual(1, result.LightnessHistogram.Sum());
        var expected = OklabColorSpace.FromSrgb(new VisualRgb24(128, 128, 128)).L;
        Assert.AreEqual(expected, result.Colors.Global.Center.L, 1e-12);
        Assert.AreEqual(1, result.LightnessHistogram[(int)Math.Round(expected * 255)]);
        Assert.Throws<ArgumentException>(() => ReferencePixelStatistics.FromPixels(new VisualPixelBuffer(1, 1, new byte[3], new byte[1])));
        Assert.Throws<ArgumentException>(() => ReferencePixelStatistics.FromPixels(HighBitDepthImageBuffer.FromVisualRgb24(image), alpha: new byte[1]));
    }
    [TestMethod]
    public void CorrectedVersionUsesCurrentPixelsRatherThanStaleOriginalAnalysis()
    {
        var (look, original, analysis) = Fixtures.Look(1);
        var stats = ReferencePixelStatistics.FromPixels(original);
        var reference = look.ReferenceSources[0] with { PixelStatistics = stats };
        look = look with { ReferenceSources = [reference], AlgorithmVersion = 2 };
        var changed = new VisualPixelBuffer(16, 16, Enumerable.Repeat((byte)200, 256 * 3).ToArray());
        var actualAnalysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "changed", changed));
        var matcher = new ReferenceLookMatcher();
        var stale = matcher.Match(changed, analysis, look).Preview;
        var fresh = matcher.Match(changed, actualAnalysis, look).Preview;
        CollectionAssert.AreEqual(stale.Rgb24.ToArray(), fresh.Rgb24.ToArray());
        var curve = matcher.BuildTransform(changed, analysis, look).ToneCurve;
        CollectionAssert.AreEqual(curve.ToArray(), matcher.Match(changed, analysis, look).ToneCurve.LuminanceKnots.ToArray());
        Assert.IsTrue(curve.Zip(curve.Skip(1)).All(pair => pair.First <= pair.Second));
        var identity = matcher.BuildTransform(HighBitDepthImageBuffer.FromVisualRgb24(changed), analysis,
            look with { Parameters = look.Parameters with { MatchStrength = 0 } });
        Assert.AreEqual((.123456f, .234567f, .345678f), identity.ApplyFloat(.123456f, .234567f, .345678f));
        Assert.AreEqual(new VisualRgb24(17, 99, 203), identity.Apply(new VisualRgb24(17, 99, 203)));
        var transparent = new VisualPixelBuffer(1, 1, new byte[] { 17, 99, 203 }, new byte[] { 0 });
        var unchanged = matcher.Match(transparent, analysis, look with { Parameters = look.Parameters with { MatchStrength = 0 } });
        CollectionAssert.AreEqual(transparent.Rgb24.ToArray(), unchanged.Preview.Rgb24.ToArray());
        CollectionAssert.AreEqual(transparent.Alpha.ToArray(), unchanged.Preview.Alpha.ToArray());
    }
    [TestMethod]
    public async Task AlgorithmVersionAndStatisticsRoundTripWithoutUpgradingOldLooks()
    {
        var (look, pixels, _) = Fixtures.Look(1); var root = Fixtures.Temp();
        try
        {
            Assert.AreEqual(1, look.AlgorithmVersion);
            var corrected = look with { AlgorithmVersion = 2, ReferenceSources = [look.ReferenceSources[0] with { PixelStatistics = ReferencePixelStatistics.FromPixels(pixels) }] };
            await new ReferenceLookStore(root).SaveAsync(corrected);
            var restored = (await new ReferenceLookStore(root).LoadAsync()).Looks.Single().Normalize();
            Assert.AreEqual(2, restored.AlgorithmVersion);
            CollectionAssert.AreEqual(corrected.ReferenceSources[0].PixelStatistics!.LightnessHistogram, restored.ReferenceSources[0].PixelStatistics!.LightnessHistogram);
            Assert.Throws<ArgumentException>(() => (look with { AlgorithmVersion = 2 }).Normalize());
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public void SkinColorWeightIsContinuousAndStatisticsCancelable()
    {
        OklabColor Color(double hue) => new(.5, .08 * Math.Cos(hue * Math.PI / 180), .08 * Math.Sin(hue * Math.PI / 180));
        Assert.IsLessThan(.001, Math.Abs(ReferencePixelStatistics.SkinColorWeight(Color(25 - .001)) - ReferencePixelStatistics.SkinColorWeight(Color(25 + .001))));
        Assert.AreEqual(0, ReferencePixelStatistics.SkinColorWeight(new(.5, 0, 0)));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => ReferencePixelStatistics.FromPixels(new VisualPixelBuffer(1, 1, new byte[3]), cancel.Token));
    }
}
