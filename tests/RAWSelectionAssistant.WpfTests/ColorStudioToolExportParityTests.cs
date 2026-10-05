using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorStudioToolExportParityTests
{
    public TestContext? TestContext { get; set; }
    private static ColorAdjustmentStack Stack() => new([
        new(Guid.NewGuid(), ColorStudioNodeType.WhiteBalance, "白平衡", NumericParameters: new Dictionary<string, double>{{"temperature", 15},{"tint", -8}}),
        new(Guid.NewGuid(), ColorStudioNodeType.BasicTone, "影调", NumericParameters: new Dictionary<string, double>{{"exposure", .3},{"highlights", -20},{"shadows", 15},{"saturation", 12}}),
        new(Guid.NewGuid(), ColorStudioNodeType.ColorBalance, "平衡", NumericParameters: new Dictionary<string, double>{{"shadows_hue", 225},{"shadows_amount", 8}}),
        new(Guid.NewGuid(), ColorStudioNodeType.Levels, "色阶", NumericParameters: new Dictionary<string, double>{{"rgb_gamma", 1.05}}),
        new(Guid.NewGuid(), ColorStudioNodeType.Curve, "曲线", NumericParameters: new Dictionary<string, double>{{"rgb_y2", .53}}),
        new(Guid.NewGuid(), ColorStudioNodeType.Details, "细节", NumericParameters: new Dictionary<string, double>{{"clarity", 10},{"luma_noise", 15}})
    ], ProcessingVersion: 2);

    [TestMethod]
    public Task EncodedPngAndTiffUseTheWholeFrozenStackAndPreserveBitDepthAlphaAndIcc() => RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ToolParity-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var rows = new List<object>();
        try
        {
            foreach (var type in new[] { "png", "tif" })
            {
                const int w = 128, h = 64;
                var raw = new ushort[w * h * 4];
                for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                { var i = (y * w + x) * 4; raw[i] = (ushort)(2000 + x * 400); raw[i + 1] = (ushort)(3000 + y * 700); raw[i + 2] = (ushort)(12000 + x * 180 + y * 150); raw[i + 3] = (ushort)(x % 3 == 0 ? 0 : x % 3 == 1 ? 32767 : 65535); }
                BitmapSource input = BitmapSource.Create(w, h, 96, 96, PixelFormats.Rgba64, null, raw, w * 8);
                if (type == "png") input = new FormatConvertedBitmap(input, PixelFormats.Bgra32, null, 0);
                input.Freeze(); var sourcePath = Path.Combine(root, "source." + type); StudioQuickExport.Encode(input, sourcePath, sourcePath);
                var decoded = StudioQuickExport.Load(sourcePath); var stack = Stack();
                var preview = ColorStudioBitmapRenderer.Render(decoded, stack, null);
                using var editor = new TetherReferenceModeViewModel();
                var exportPixels = await editor.ProcessForExportAsync(sourcePath, null, null, CancellationToken.None, stack);
                var bytesPreview = Bytes(preview); var bytesExport = Bytes(exportPixels);
                CollectionAssert.AreEqual(bytesPreview, bytesExport, "Preview/export must share frozen complete stack.");
                var output = Path.Combine(root, "processed." + type); StudioQuickExport.Encode(exportPixels, sourcePath, output);
                var reloaded = StudioQuickExport.Load(output); CollectionAssert.AreEqual(bytesExport, Bytes(reloaded), "Lossless output must preserve processing pixels.");
                var beforeAlpha = Alpha(decoded); CollectionAssert.AreEqual(beforeAlpha, Alpha(reloaded));
                using var stream = File.OpenRead(output); var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]; Assert.IsNotNull(frame.ColorContexts); Assert.IsGreaterThan(0, frame.ColorContexts.Count);
                Assert.AreEqual(type == "png" ? 32 : 64, reloaded.Format.BitsPerPixel);
                Assert.IsFalse(Bytes(decoded).SequenceEqual(bytesExport), "Fixture must actually exercise color changes.");
                rows.Add(new { inputType = type, dimensions = $"{w}x{h}", bitDepth = reloaded.Format.BitsPerPixel / 4, alphaPreserved = true, iccCount = frame.ColorContexts.Count, stage = "frozen full stack", meanDelta = 0, maxDelta = 0, tolerance = "lossless exact", status = "PASS" });
            }
            var evidence = Path.Combine(TestContext!.ResultsDirectory!, "new-tools-encoded-parity"); Directory.CreateDirectory(evidence);
            await File.WriteAllTextAsync(Path.Combine(evidence, "MATRIX.json"), JsonSerializer.Serialize(new { scope = "synthetic gradients, equal-resolution actual encoded PNG/TIFF16; no real RAW or proxy claim", sourceHead = Environment.GetEnvironmentVariable("PIXEL_TART_TEST_SOURCE_HEAD") ?? "WORKTREE", cases = rows }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(root, true); }
    });

    [TestMethod]
    public Task JpegOutputDeltaIsBoundedAndFloatAdapterDoesNotQuantizeNeutralInputToSixteenBits() => RunSta(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-JpegTools-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            const int w = 128, h = 64; var rgb = new byte[w * h * 3];
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) { var i = (y * w + x) * 3; rgb[i] = (byte)(40 + x); rgb[i + 1] = (byte)(50 + y * 2); rgb[i + 2] = (byte)(70 + x / 2 + y / 2); }
            var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Rgb24, null, rgb, w * 3); bitmap.Freeze();
            var path = Path.Combine(root, "source.jpg"); StudioQuickExport.Encode(bitmap, path, path); var loaded = StudioQuickExport.Load(path);
            var processed = ColorStudioBitmapRenderer.Render(loaded, Stack(), null); var output = Path.Combine(root, "processed.jpg"); StudioQuickExport.Encode(processed, path, output);
            var decoded = StudioQuickExport.Load(output); var expected = Bgr(processed); var actual = Bgr(decoded); var delta = expected.Zip(actual, (a, b) => Math.Abs(a - b)).ToArray();
            // Establish an independent actual codec oracle. A different valid detail
            // kernel can move the maximum DCT tail by one count without changing
            // export correctness. This opaque input needs no alpha compositing.
            var codecPath = Path.Combine(root, "codec-oracle.jpg");
            var codec = new JpegBitmapEncoder { QualityLevel = 95 };
            codec.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(processed, PixelFormats.Bgr24, null, 0)));
            using (var stream = File.Create(codecPath)) codec.Save(stream);
            CollectionAssert.AreEqual(Bgr(StudioQuickExport.Load(codecPath)), actual, "Export pixels must exactly match encoding the complete stack at declared JPEG quality.");
            Assert.IsLessThan(1.5, delta.Average());
            TestContext!.WriteLine($"JPEG actual-encoded whole-stack mean={delta.Average():F4}, max={delta.Max()}, independent quality95 codec pixel delta=0; mean tolerance<1.5, maximum lossy tail recorded");
            var floats = new float[] { .1234567f, .2345678f, .3456789f, .4567891f };
            var high = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Rgba128Float, null, floats, 16); high.Freeze();
            var neutral = ColorStudioBitmapRenderer.Render(high, new([new(Guid.NewGuid(), ColorStudioNodeType.WhiteBalance, "中性")], ProcessingVersion: 2), null);
            Assert.AreEqual(PixelFormats.Rgba128Float, neutral.Format); var restored = new float[4]; neutral.CopyPixels(restored, 16, 0); CollectionAssert.AreEqual(floats, restored);
            await Task.CompletedTask;
        }
        finally { Directory.Delete(root, true); }
    });

    [TestMethod]
    public Task ScRgbFloatPixelsAndEquivalentEncodedSrgbUseTheSameToolColors() => RunSta(() =>
    {
        var encoded = new float[] { .2f, .45f, .7f, .75f, .5f, .3f };
        static float Decode(float v) => (float)(v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4));
        var linear = new float[] { Decode(encoded[0]), Decode(encoded[1]), Decode(encoded[2]), 1, Decode(encoded[3]), Decode(encoded[4]), Decode(encoded[5]), .5f };
        var source = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Rgba128Float, null, linear, 32); source.Freeze();
        var stack = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.WhiteBalance, "白平衡", NumericParameters: new Dictionary<string, double>{{"temperature", 22}})], ProcessingVersion: 2);
        var rendered = ColorStudioBitmapRenderer.Render(source, stack, null); var actual = new float[8]; rendered.CopyPixels(actual, 32, 0);
        var expected = ColorStudioToolProcessor.Apply(new RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer(2, 1, encoded), stack.Nodes[0]).Rgb32.ToArray();
        for (var p = 0; p < 2; p++) for (var c = 0; c < 3; c++) Assert.AreEqual(Decode(expected[p * 3 + c]), actual[p * 4 + c], 2e-6, "WPF float values are linear scRGB, not encoded sRGB.");
        Assert.AreEqual(1f, actual[3]); Assert.AreEqual(.5f, actual[7]);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AuthorizedRealJpegSamplesRecordActualEncodedFullStackPixels() => RunSta(async () =>
    {
        var inputRoot = Environment.GetEnvironmentVariable("PIXEL_TART_NEW_STUDIO_CORPUS"); var outputRoot = Environment.GetEnvironmentVariable("PIXEL_TART_NEW_STUDIO_CORPUS_OUTPUT");
        if (string.IsNullOrEmpty(inputRoot) || string.IsNullOrEmpty(outputRoot)) { Assert.Inconclusive("Authorized real JPEG corpus requires explicit opt-in."); return; }
        Directory.CreateDirectory(outputRoot); var rows = new List<object>();
        foreach (var name in new[] { "DSC04831.JPG", "DSC04832.JPG", "DSC04833.JPG", "DSC04834.JPG", "DSC04835.JPG" })
        {
            var input = StudioQuickExport.Load(Path.Combine(inputRoot, name));
            // Real-source bounded preview evidence; full-resolution RAW exports are measured separately.
            var scale = Math.Min(1, 1600d / Math.Max(input.PixelWidth, input.PixelHeight));
            var proxy = new TransformedBitmap(input, new ScaleTransform(scale, scale)); proxy.Freeze();
            var watch = System.Diagnostics.Stopwatch.StartNew(); var processed = ColorStudioBitmapRenderer.Render(proxy, Stack(), null); var renderMs = watch.Elapsed.TotalMilliseconds;
            var expected = Bgr(processed); var exact = Path.Combine(outputRoot, Path.GetFileNameWithoutExtension(name) + "-real-source-proxy.png"); StudioQuickExport.Encode(processed, "result.png", exact);
            CollectionAssert.AreEqual(expected, Bgr(StudioQuickExport.Load(exact)));
            var jpeg = Path.Combine(outputRoot, Path.GetFileNameWithoutExtension(name) + "-real-source-proxy.jpg"); StudioQuickExport.Encode(processed, "result.jpg", jpeg);
            var actual = Bgr(StudioQuickExport.Load(jpeg)); var deltas = expected.Zip(actual, (a, b) => Math.Abs(a - b)).ToArray();
            var sorted = deltas.Order().ToArray();
            // Lossy JPEG tail is recorded, not forced to be equal to processing pixels.
            Assert.IsLessThan(4, deltas.Average()); Assert.IsFalse(Bgr(proxy).SequenceEqual(expected));
            rows.Add(new { sourceFile = name, sourceSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(inputRoot, name)))), sourceDimensions = new { input.PixelWidth, input.PixelHeight }, processedDimensions = new { processed.PixelWidth, processed.PixelHeight }, scope = "real JPEG decoded/resampled <=1600, full tool stack, actual encoded PNG/JPEG", renderMs, losslessMaxDelta = 0, jpegMeanDelta = deltas.Average(), jpegP95Delta = sorted[(int)(sorted.Length * .95)], jpegMaxDelta = deltas.Max(), status = "PASS_PROGRAMMATIC_PROXY_ONLY" });
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "REAL_JPEG_TOOL_MATRIX.json"), JsonSerializer.Serialize(new { sourceHead = Environment.GetEnvironmentVariable("PIXEL_TART_TEST_SOURCE_HEAD") ?? "WORKTREE", cases = rows }, new JsonSerializerOptions { WriteIndented = true }));
        }
    });

    [TestMethod]
    public Task FilmSpatialVersionChangesOnlyOnExplicitEditingAndUndoRestoresLegacy() => RunSta(() =>
    {
        using var editor = new TetherReferenceModeViewModel();
        var legacy = new PixelTartFilmSettings(true, GrainAmount: 40, Seed: 33);
        var node = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.Film, "旧胶片", FilmSettings: legacy);
        editor.ApplyTargetSnapshot(null, legacy, new([node]), false, Guid.NewGuid());
        Assert.AreEqual(1, editor.FilmSettings.SpatialVersion);
        Assert.AreEqual(1, editor.AdjustmentStack.Nodes[0].FilmSettings!.SpatialVersion);
        editor.FilmGrainAmount = 41;
        Assert.AreEqual(2, editor.FilmSettings.SpatialVersion);
        Assert.AreEqual(2, editor.AdjustmentStack.Nodes[0].FilmSettings!.SpatialVersion);
        editor.UndoAdjustmentCommand.Execute(null);
        Assert.AreEqual(1, editor.FilmSettings.SpatialVersion); Assert.AreEqual(40, editor.FilmGrainAmount);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task FilmAfterNewToolsCreatesARealNodeAndEnableUndoRemainOneState() => RunSta(async () =>
    {
        using var editor = new TetherReferenceModeViewModel();
        var exposure = ColorStudioToolCatalog.Parameters.Single(p => p.NodeType == ColorStudioNodeType.BasicTone && p.Key == "exposure");
        editor.SetToolParameter(exposure, .3); var original = editor.AdjustmentStack.DeepClone();
        editor.FilmProfileId = "PT-W01"; editor.FilmProfileAmount = 60; editor.FilmEnabled = true;
        var film = editor.AdjustmentStack.Nodes.Single(n => n.Type == ColorStudioNodeType.Film);
        Assert.IsTrue(film.Enabled); Assert.IsTrue(film.FilmSettings!.Enabled); Assert.AreEqual("PT-W01", film.FilmSettings.ProfileId);
        var image = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Rgb24, null, new byte[] { 100, 130, 160, 170, 140, 110 }, 6); image.Freeze();
        var withoutFilm = Bgr(ColorStudioBitmapRenderer.Render(image, original, null)); var withFilm = Bgr(ColorStudioBitmapRenderer.Render(image, editor.AdjustmentStack, null));
        Assert.IsFalse(withoutFilm.SequenceEqual(withFilm));
        editor.FilmEnabled = false; Assert.IsFalse(editor.AdjustmentStack.Nodes.Single(n => n.Type == ColorStudioNodeType.Film).Enabled);
        CollectionAssert.AreEqual(withoutFilm, Bgr(ColorStudioBitmapRenderer.Render(image, editor.AdjustmentStack, null)));
        editor.UndoAdjustmentCommand.Execute(null); Assert.IsTrue(editor.FilmEnabled);
        CollectionAssert.AreEqual(withFilm, Bgr(ColorStudioBitmapRenderer.Render(image, editor.AdjustmentStack, null)));
        editor.SelectedAdjustmentNode = editor.AdjustmentStack.Nodes.Single(n => n.Type == ColorStudioNodeType.Film); editor.ToggleAdjustmentNodeCommand.Execute(null);
        Assert.IsFalse(editor.FilmEnabled); Assert.IsFalse(editor.SelectedAdjustmentNode!.Enabled);
        var legacyFilm = new PixelTartFilmSettings(true, "PT-W01", 60);
        editor.ApplyTargetSnapshot(null, legacyFilm, original, render: false);
        Assert.IsTrue(editor.FilmEnabled); Assert.HasCount(1, editor.AdjustmentStack.Nodes.Where(n => n.Type == ColorStudioNodeType.Film));
        CollectionAssert.AreEqual(withFilm, Bgr(ColorStudioBitmapRenderer.Render(image, editor.AdjustmentStack, null)), "Old stack with standalone film is migrated into one real film node.");
        await Task.CompletedTask;
    });

    private static byte[] Bytes(BitmapSource source) { var stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8; var output = new byte[stride * source.PixelHeight]; source.CopyPixels(output, stride, 0); return output; }
    private static byte[] Bgr(BitmapSource source) { var converted = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0); return Bytes(converted); }
    private static ushort[] Alpha(BitmapSource source) { var converted = new FormatConvertedBitmap(source, PixelFormats.Rgba64, null, 0); var rgba = new ushort[source.PixelWidth * source.PixelHeight * 4]; converted.CopyPixels(rgba, source.PixelWidth * 8, 0); return Enumerable.Range(0, rgba.Length / 4).Select(i => rgba[i * 4 + 3]).ToArray(); }
}
