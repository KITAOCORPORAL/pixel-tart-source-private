using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorStudioRealCorpusTests
{
    [TestMethod]
    public async Task AuthorizedRealRawCorpusRecordsNewToolPixelsAndDerivedTiff16()
    {
        var inputRoot = Environment.GetEnvironmentVariable("PIXEL_TART_NEW_STUDIO_CORPUS");
        var outputRoot = Environment.GetEnvironmentVariable("PIXEL_TART_NEW_STUDIO_CORPUS_OUTPUT");
        if (string.IsNullOrEmpty(inputRoot) || string.IsNullOrEmpty(outputRoot))
        { Assert.Inconclusive("Explicitly authorized real corpus is opt-in; not a synthetic RAW replacement."); return; }
        inputRoot = Path.GetFullPath(inputRoot); outputRoot = Path.GetFullPath(outputRoot);
        if (outputRoot.StartsWith(inputRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Evidence must be outside originals.");
        Directory.CreateDirectory(outputRoot); var pipeline = new RawMatchTiff16ProductPipeline(new LibRawDecoder());
        var rows = new List<object>();
        // Named fixture list prevents implicit traversal of private unrelated photographs.
        var requested = Environment.GetEnvironmentVariable("PIXEL_TART_NEW_STUDIO_CORPUS_FILE");
        foreach (var name in new[] { "DSC04831.ARW", "DSC04832.ARW", "DSC04833.ARW", "DSC04834.ARW", "DSC04835.ARW" }.Where(n => string.IsNullOrEmpty(requested) || n == requested))
        {
            var path = Path.Combine(inputRoot, name); var watch = Stopwatch.StartNew();
            var master = await pipeline.DecodeFrozenMasterAsync(path); var decodeMs = watch.Elapsed.TotalMilliseconds;
            var proxy = pipeline.PreviewMaster(master.Image, 1600);
            var stack = PointTools(); watch.Restart();
            var preview = pipeline.Render(proxy, null, stack).ProcessingPixels!; var previewMs = watch.Elapsed.TotalMilliseconds;
            var interactiveProxy = pipeline.PreviewMaster(master.Image, 800); watch.Restart(); _ = pipeline.Render(interactiveProxy, null, stack).ProcessingPixels!; var interactive800Ms = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var full = pipeline.Render(master.Image, null, stack).ProcessingPixels!; var fullMs = watch.Elapsed.TotalMilliseconds;
            var metrics = CompareNearest(preview, full); Assert.IsLessThanOrEqualTo(1e-6, metrics.MaxOklabDelta, "Point operations must commute with the declared nearest proxy.");
            var output = Path.Combine(outputRoot, Path.GetFileNameWithoutExtension(name) + "-derived-new-tools.tif");
            watch.Restart();
            await AtomicTiffWriter.WriteRgb48Async(output, full, new(TiffBitDepth.Sixteen, Software: "Pixel Tart real corpus", Orientation: full.Orientation), overwrite: true);
            TiffReadBackResult tiff; using (var stream = File.OpenRead(output)) tiff = TiffReadBack.Read(stream);
            var expected = full.ToRgb48(); CollectionAssert.AreEqual(expected, tiff.Rgb48Samples.ToArray()); var encodeMs = watch.Elapsed.TotalMilliseconds;
            await master.ValidateSourceAsync();
            var toolRows = new List<object>();
            foreach (var (type, key, value) in new[] { (ColorStudioNodeType.WhiteBalance, "temperature", 35d), (ColorStudioNodeType.BasicTone, "exposure", .5), (ColorStudioNodeType.BasicTone, "highlights", -50d), (ColorStudioNodeType.BasicTone, "shadows", 50d), (ColorStudioNodeType.SkinTone, "hue_uniformity", 50d), (ColorStudioNodeType.Levels, "rgb_gamma", 1.2), (ColorStudioNodeType.Curve, "rgb_y2", .6), (ColorStudioNodeType.Details, "luma_noise", 35d), (ColorStudioNodeType.Details, "sharpen", 35d) })
            {
                watch.Restart(); var result = ColorStudioToolProcessor.Apply(proxy, Node(type, (key, value))); var elapsed = watch.Elapsed.TotalMilliseconds; var change = CompareNearest(proxy, result);
                toolRows.Add(new { tool = type.ToString(), parameter = key, value, elapsedMs = elapsed, metrics = change });
            }
            rows.Add(new { sourceFile = name, sourceSha256 = master.SourceSha256, dimensions = new { master.Image.Width, master.Image.Height }, master.Image.SourceBitDepth, master.Image.WorkingColorSpace, derivedTiff = Path.GetFileName(output), derivedFromRaw = true, decodeMs, previewMs, interactive800Ms, fullMs, encodeMs, proxyPointToolsParity = metrics, encodedTiffMaxU16Delta = 0, tools = toolRows, status = "PASS_PROGRAMMATIC_ONLY", limitations = "Full-resolution details/proxy parity and UI/visual acceptance not covered by this fixture." });
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "REAL_RAW_TOOL_MATRIX.json"), JsonSerializer.Serialize(new { scope = "Authorized real Sony RAW cases listed below; same nearest-center proxy/full point-tool stack; real derived TIFF16 files", sourceHead = Environment.GetEnvironmentVariable("PIXEL_TART_TEST_SOURCE_HEAD") ?? "WORKTREE", cases = rows }, new JsonSerializerOptions { WriteIndented = true }));
            GC.Collect(); GC.WaitForPendingFinalizers();
        }
    }

    private static ColorAdjustmentStackNode Node(ColorStudioNodeType type, params (string Key, double Value)[] parameters) => new(Guid.NewGuid(), type, type.ToString(), NumericParameters: parameters.ToDictionary(v => v.Key, v => v.Value));
    private static ColorAdjustmentStack PointTools() => new([
        Node(ColorStudioNodeType.WhiteBalance, ("temperature", 15), ("tint", -5)), Node(ColorStudioNodeType.BasicTone, ("exposure", .3), ("highlights", -20), ("shadows", 15)),
        Node(ColorStudioNodeType.ColorBalance, ("shadows_hue", 220), ("shadows_amount", 8)), Node(ColorStudioNodeType.Levels, ("rgb_gamma", 1.05)),
        Node(ColorStudioNodeType.Curve, ("rgb_y2", .53)), Node(ColorStudioNodeType.SkinTone, ("hue_uniformity", 10))
    ], ProcessingVersion: 2);
    private sealed record Metrics(double MeanOklabDelta, double MaxOklabDelta, double MeanToneDelta, double MeanChromaDelta);
    private static Metrics CompareNearest(HighBitDepthImageBuffer proxy, HighBitDepthImageBuffer full)
    {
        double sum = 0, max = 0, tone = 0, chroma = 0; var count = 0; var a = proxy.Rgb32.Span; var b = full.Rgb32.Span;
        // Bound diagnostic work to ~65k points. Rendering itself still processed every pixel.
        var stride = Math.Max(1, proxy.PixelCount / 65536);
        for (var pixel = 0; pixel < proxy.PixelCount; pixel += stride)
        {
            var x = pixel % proxy.Width; var y = pixel / proxy.Width; var sx = Math.Min(full.Width - 1, (int)((x + .5) * full.Width / proxy.Width)); var sy = Math.Min(full.Height - 1, (int)((y + .5) * full.Height / proxy.Height));
            var offset = (sy * full.Width + sx) * 3; var left = OklabColorSpace.FromSrgb(a[pixel * 3], a[pixel * 3 + 1], a[pixel * 3 + 2]); var right = OklabColorSpace.FromSrgb(b[offset], b[offset + 1], b[offset + 2]);
            var dl = Math.Abs(left.L - right.L); var dc = Math.Abs(left.Chroma - right.Chroma); var d = Math.Sqrt(dl * dl + Math.Pow(left.A - right.A, 2) + Math.Pow(left.B - right.B, 2)); sum += d; max = Math.Max(max, d); tone += dl; chroma += dc; count++;
        }
        return new(sum / count, max, tone / count, chroma / count);
    }
}
