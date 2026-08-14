using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class VisualAnalysisTests
{
    [TestMethod]
    [DataRow(0, ToneKeyTendency.Low, true, false)]
    [DataRow(128, ToneKeyTendency.Mid, false, false)]
    [DataRow(255, ToneKeyTendency.High, false, true)]
    public void SolidNeutralImagesProduceTruthfulHistogramToneAndClipping(int value, ToneKeyTendency expectedKey, bool blackClip, bool whiteClip)
    {
        var buffer = Solid(32, 24, (byte)value, (byte)value, (byte)value);
        var result = VisualAnalysisEngine.Analyze(Request(buffer, value.ToString(), 5));
        Assert.AreEqual(buffer.PixelCount, result.HistogramR.Sum(x => (long)x));
        Assert.AreEqual(buffer.PixelCount, result.HistogramLuma.Sum(x => (long)x));
        Assert.AreEqual(expectedKey, result.ToneKey);
        Assert.AreEqual(blackClip, result.BlackClipRatio > .99);
        Assert.AreEqual(whiteClip, result.WhiteClipRatio > .99);
        Assert.AreEqual(1d, result.Palette.Sum(x => x.Weight), 0.000001);
        Assert.AreEqual(ColorHarmonyTendency.LowSaturationNeutral, result.Harmony);
    }

    [TestMethod]
    public void PrimaryColorsUseRealRgbBinsAndLabPalette()
    {
        var pixels = new byte[90 * 3];
        for (var index = 0; index < 90; index++) { var offset = index * 3; pixels[offset + index / 30] = 255; }
        var result = VisualAnalysisEngine.Analyze(Request(new(90, 1, pixels), "rgb", 3));
        Assert.AreEqual(30u, result.HistogramR[255]); Assert.AreEqual(60u, result.HistogramR[0]);
        Assert.HasCount(3, result.Palette);
        Assert.IsTrue(result.Palette.Any(x => x.Rgb.R > 240 && x.Rgb.G < 10 && x.Rgb.B < 10));
        Assert.IsTrue(result.Palette.All(x => x.Lab.L is >= 0 and <= 100));
    }

    [TestMethod]
    public void GradientAndHighContrastUseFixedZonesAndRobustPercentiles()
    {
        var gradient = new byte[256 * 3];
        for (var value = 0; value < 256; value++) gradient[value * 3] = gradient[value * 3 + 1] = gradient[value * 3 + 2] = (byte)value;
        var result = VisualAnalysisEngine.Analyze(Request(new(256, 1, gradient), "gradient", 5));
        Assert.AreEqual(1d, result.ToneZones.Sum, .000001);
        Assert.AreEqual(ContrastTendency.High, result.Contrast);
        Assert.AreEqual(LuminanceSpanTendency.Wide, result.LuminanceSpan);
        Assert.IsGreaterThan(100, result.HistogramLuma.Count(x => x > 0));

        var highContrast = new byte[100 * 3];
        for (var pixel = 50; pixel < 100; pixel++) highContrast.AsSpan(pixel * 3, 3).Fill(255);
        var contrast = VisualAnalysisEngine.Analyze(Request(new(100, 1, highContrast), "contrast", 3));
        Assert.AreEqual(ContrastTendency.High, contrast.Contrast);
        Assert.AreEqual(.5, contrast.BlackClipRatio, .0001); Assert.AreEqual(.5, contrast.WhiteClipRatio, .0001);
    }

    [TestMethod]
    public void PaletteSizesSortingDerivativesAndHarmonyAreDeterministic()
    {
        var stripes = Stripes(210, 30, [(220, 30, 30), (30, 210, 45), (35, 60, 220), (220, 190, 30), (200, 40, 190), (25, 190, 195), (120, 70, 30)]);
        foreach (var size in new[] { 3, 5, 7 })
        {
            var first = VisualAnalysisEngine.Analyze(Request(stripes, "stable-seed", size));
            var second = VisualAnalysisEngine.Analyze(Request(stripes, "stable-seed", size));
            CollectionAssert.AreEqual(first.Palette.Select(x => x.Hex).ToArray(), second.Palette.Select(x => x.Hex).ToArray());
            Assert.IsLessThanOrEqualTo(size, first.Palette.Count);
            Assert.AreEqual(1d, first.Palette.Sum(x => x.Weight), .000001);
        }
        var result = VisualAnalysisEngine.Analyze(Request(stripes, "stable-seed", 7));
        var derived = VisualAnalysisEngine.Derive(result.Palette[0].Rgb);
        Assert.HasCount(2, derived.Analogous); Assert.HasCount(4, derived.Monochrome);
    }

    [TestMethod]
    public async Task CacheUsesHashAndVersionAndNeverStoresCancelledPartialResult()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-VisualAnalysis", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var database = new AssetLibraryDatabase(Path.Combine(root, "library.db"));
            await using var repository = new SqliteAssetLibraryRepository(database); await repository.InitializeAsync();
            var source = Path.Combine(root, "asset.jpg"); await File.WriteAllBytesAsync(source, [1]); await repository.ImportAsync([new(source)]);
            var asset = (await repository.QueryAsync(new())).Items.Single();
            var cache = new SqliteAssetVisualAnalysisCache(database); var service = new AssetVisualAnalysisService(cache);
            var request = Request(Solid(24, 24, 80, 100, 120), "hash-a", 5) with { AssetId = asset.AssetId };
            var miss = await service.AnalyzeAsync(request); var hit = await service.AnalyzeAsync(request);
            Assert.IsFalse(miss.CacheHit); Assert.IsTrue(hit.CacheHit);
            Assert.IsNull(await cache.TryGetAsync(asset.AssetId, "hash-b"));
            Assert.IsNull(await cache.TryGetAsync(asset.AssetId, "hash-a", "visual-analysis-v2"));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => service.AnalyzeAsync(request with { ContentHash = "cancelled" }, cancelled.Token));
            Assert.IsNull(await cache.TryGetAsync(asset.AssetId, "cancelled"));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [TestMethod]
    public async Task SelectionCoordinatorCancelsStaleAnalysisAndPublishesOnlyCurrentAsset()
    {
        var coordinator = new AssetVisualAnalysisSelectionCoordinator(); var published = new List<Guid>();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var slow = coordinator.AnalyzeSelectionAsync(first, async token => { await Task.Delay(300, token); return VisualAnalysisEngine.Analyze(Request(Solid(4, 4, 1, 1, 1), "one", 3) with { AssetId = first }, token); }, x => published.Add(x.AssetId));
        await Task.Delay(20);
        var fast = coordinator.AnalyzeSelectionAsync(second, _ => Task.FromResult(VisualAnalysisEngine.Analyze(Request(Solid(4, 4, 2, 2, 2), "two", 3) with { AssetId = second })), x => published.Add(x.AssetId));
        Assert.IsFalse(await slow); Assert.IsTrue(await fast);
        CollectionAssert.AreEqual(new[] { second }, published);
    }

    [TestMethod]
    public void UnconvertedPixelContractIsRejectedInsteadOfFakingSrgb()
    {
        var request = Request(Solid(2, 2, 1, 2, 3), "profile", 3) with { SourceProfile = "AdobeRGB", PixelsConvertedToAnalysisProfile = false };
        Assert.Throws<InvalidOperationException>(() => VisualAnalysisEngine.Analyze(request));
    }

    [TestMethod]
    public void GeneratedHundredAndThousandImagePerformanceRunsCompleteAndReportCacheIndependentSamples()
    {
        foreach (var count in new[] { 100, 1000 })
        {
            var durations = new List<double>(count);
            for (var index = 0; index < count; index++)
            {
                var started = System.Diagnostics.Stopwatch.StartNew();
                var color = (byte)(index % 256); var result = VisualAnalysisEngine.Analyze(Request(Solid(24, 16, color, (byte)(255 - color), (byte)((index * 17) % 256)), index.ToString(), 3));
                started.Stop(); durations.Add(started.Elapsed.TotalMilliseconds); Assert.AreEqual(24 * 16, result.HistogramLuma.Sum(x => (long)x));
            }
            durations.Sort(); var sample = new VisualAnalysisPerformanceSample(count, durations.Average(), durations[(int)Math.Floor((durations.Count - 1) * .95)], 0, count);
            Assert.AreEqual(count, sample.CacheMisses); Assert.IsGreaterThanOrEqualTo(0, sample.MeanMilliseconds); Assert.IsGreaterThanOrEqualTo(0, sample.P95Milliseconds);
            TestContext.WriteLine($"VisualAnalysis {count}: mean={sample.MeanMilliseconds:F3}ms p95={sample.P95Milliseconds:F3}ms");
        }
    }

    public TestContext TestContext { get; set; } = null!;

    private static AssetVisualAnalysisRequest Request(VisualPixelBuffer buffer, string hash, int size) => new(Guid.NewGuid(), hash, buffer, size);
    private static VisualPixelBuffer Solid(int width, int height, byte r, byte g, byte b) { var bytes = new byte[width * height * 3]; for (var pixel = 0; pixel < width * height; pixel++) { bytes[pixel * 3] = r; bytes[pixel * 3 + 1] = g; bytes[pixel * 3 + 2] = b; } return new(width, height, bytes); }
    private static VisualPixelBuffer Stripes(int width, int height, IReadOnlyList<(byte R, byte G, byte B)> colors) { var bytes = new byte[width * height * 3]; for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) { var color = colors[x * colors.Count / width]; var offset = (y * width + x) * 3; bytes[offset] = color.R; bytes[offset + 1] = color.G; bytes[offset + 2] = color.B; } return new(width, height, bytes); }
}
