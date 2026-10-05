using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorStudioToolProcessorTests
{
    public TestContext? TestContext { get; set; }
    private static ColorAdjustmentStackNode Node(ColorStudioNodeType type, params (string Key, double Value)[] values) => new(Guid.NewGuid(), type, type.ToString(), true, values.ToDictionary(v => v.Key, v => v.Value));
    private static HighBitDepthImageBuffer Image(params float[] values) => new(values.Length / 3, 1, values);
    private static float[] Apply(HighBitDepthImageBuffer image, ColorAdjustmentStackNode node) => ColorStudioToolProcessor.Apply(image, node).Rgb32.ToArray();

    [TestMethod]
    public void EveryNewToolDefaultIsExactFloatIdentityAndDisabledNodesPreserveIt()
    {
        var source = Image(0, 0, 0, .0051f, .0071f, .0049f, .234567f, .654321f, .555555f, 1, 1, 1);
        foreach (var type in Enum.GetValues<ColorStudioNodeType>().Where(ColorStudioToolCatalog.IsTool))
        {
            CollectionAssert.AreEqual(source.Rgb32.ToArray(), Apply(source, Node(type)), type.ToString());
            var parameters = ColorStudioToolCatalog.GetParameters(type).ToDictionary(p => p.Key, p => p.DefaultValue);
            CollectionAssert.AreEqual(source.Rgb32.ToArray(), Apply(source, Node(type) with { NumericParameters = parameters }));
        }
    }

    [TestMethod]
    public void WhiteBalanceUsesRelativeDecodedColorAndNeutralTintHasOppositeDirections()
    {
        var gray = Image(.4f, .4f, .4f);
        var warm = Apply(gray, Node(ColorStudioNodeType.WhiteBalance, ("temperature", 40)));
        var cool = Apply(gray, Node(ColorStudioNodeType.WhiteBalance, ("temperature", -40)));
        Assert.IsTrue(warm[0] > warm[1] && warm[1] > warm[2]);
        Assert.IsTrue(cool[2] > cool[1] && cool[1] > cool[0]);
        var magenta = Apply(gray, Node(ColorStudioNodeType.WhiteBalance, ("tint", 40)));
        Assert.IsTrue(magenta[0] > magenta[1] && magenta[2] > magenta[1]);
    }

    [TestMethod]
    public void BasicToneRampsRemainOrderedAndControlsHaveDistinctScopes()
    {
        var source = new HighBitDepthImageBuffer(256, 1, Enumerable.Range(0, 256).SelectMany(i => new[] { i / 255f, i / 255f, i / 255f }).ToArray());
        foreach (var key in new[] { "brightness", "contrast", "highlights", "shadows", "whites", "blacks" })
            foreach (var amount in new[] { -100d, 100d })
            {
                var result = Apply(source, Node(ColorStudioNodeType.BasicTone, (key, amount)));
                Assert.AreEqual(0f, result[0]); Assert.AreEqual(1f, result[^1]);
                for (var i = 3; i < result.Length; i += 3) Assert.IsGreaterThanOrEqualTo(result[i - 3] - .00001f, result[i], $"{key} {amount} at {i / 3}");
                Assert.IsTrue(result.All(v => float.IsFinite(v) && v is >= 0 and <= 1));
            }
        var bright = Apply(source, Node(ColorStudioNodeType.BasicTone, ("brightness", 50)));
        var exposed = Apply(source, Node(ColorStudioNodeType.BasicTone, ("exposure", .5)));
        Assert.IsFalse(bright.SequenceEqual(exposed), "Brightness is no longer an alias of exposure.");
        var shadows = Apply(source, Node(ColorStudioNodeType.BasicTone, ("shadows", 50)));
        var high = Apply(source, Node(ColorStudioNodeType.BasicTone, ("highlights", 50)));
        Assert.IsGreaterThan(high[64 * 3], shadows[64 * 3]); Assert.IsGreaterThan(shadows[230 * 3], high[230 * 3]);
    }

    [TestMethod]
    public void LevelsPreserveUneditedChannelsAndRejectCrossedEndpoints()
    {
        var source = Image(.2f, .3f, .4f);
        var result = Apply(source, Node(ColorStudioNodeType.Levels, ("r_gamma", 2)));
        Assert.IsGreaterThan(.2, result[0]); Assert.AreEqual(.3f, result[1]); Assert.AreEqual(.4f, result[2]);
        Assert.ThrowsExactly<ArgumentException>(() => Apply(source, Node(ColorStudioNodeType.Levels, ("rgb_black", .8), ("rgb_white", .2))));
        Assert.ThrowsExactly<ArgumentException>(() => Apply(source, Node(ColorStudioNodeType.Levels, ("rgb_out_black", .8), ("rgb_out_white", .2))));
    }

    [TestMethod]
    public void CurvesPassThroughPointsStayWithinSegmentsAndSupportAddedDeletedPoints()
    {
        var node = Node(ColorStudioNodeType.Curve, ("r_count", 3), ("r_x0", 0), ("r_y0", 0), ("r_x1", .4), ("r_y1", .8), ("r_x2", 1), ("r_y2", 1));
        var points = ColorStudioToolProcessor.ReadCurve(node, "r");
        foreach (var point in points) Assert.AreEqual(point.Y, ColorStudioToolProcessor.EvaluateCurve(points, point.X), 1e-12);
        double previous = 0;
        for (var i = 0; i <= 1000; i++) { var output = ColorStudioToolProcessor.EvaluateCurve(points, i / 1000d); Assert.IsGreaterThanOrEqualTo(previous, output); previous = output; }
        var result = Apply(Image(.4f, .3f, .2f), node); Assert.AreEqual(.8f, result[0], 1e-6); Assert.AreEqual(.3f, result[1]); Assert.AreEqual(.2f, result[2]);
        var descending = new[] { new ColorStudioToolProcessor.CurvePoint(0, 0), new(.5, 1), new(1, 0) };
        for (var i = 0; i <= 1000; i++) Assert.IsTrue(ColorStudioToolProcessor.EvaluateCurve(descending, i / 1000d) is >= 0 and <= 1);
        Assert.ThrowsExactly<ArgumentException>(() => Apply(Image(.1f, .2f, .3f), node with { NumericParameters = new Dictionary<string, double> { ["rgb_x1"] = 0 } }));
    }

    [TestMethod]
    public void SkinUniformityProtectsGrayAndBlueAndMovesSampleTowardDeclaredTarget()
    {
        var skin = OklabColorSpace.ToSrgbGamutMappedFloat(new(.65, Math.Cos(.6) * .12, Math.Sin(.6) * .12));
        var source = Image((float)skin.R, (float)skin.G, (float)skin.B, .4f, .4f, .4f, .1f, .2f, .9f);
        var result = Apply(source, Node(ColorStudioNodeType.SkinTone, ("hue_uniformity", 70), ("target_hue", 55)));
        Assert.IsFalse(source.Rgb32.Span[..3].SequenceEqual(result.AsSpan(0, 3)));
        CollectionAssert.AreEqual(source.Rgb32.ToArray()[3..], result[3..]);
        var before = OklabColorSpace.FromSrgb((float)skin.R, (float)skin.G, (float)skin.B); var after = OklabColorSpace.FromSrgb(result[0], result[1], result[2]);
        Assert.IsGreaterThan(Math.Atan2(before.B, before.A), Math.Atan2(after.B, after.A)); Assert.AreEqual(before.L, after.L, 1e-6);
    }

    [TestMethod]
    public void ColorBalanceUsesIndependentTonalWeightsAndProtectsEndpoints()
    {
        var source = Image(0, 0, 0, .15f, .15f, .15f, .8f, .8f, .8f, 1, 1, 1);
        var result = Apply(source, Node(ColorStudioNodeType.ColorBalance, ("shadows_hue", 250), ("shadows_amount", 70)));
        Assert.IsGreaterThan(Math.Abs(result[6] - result[8]), Math.Abs(result[3] - result[5]));
        Assert.AreEqual(0f, result[0]); Assert.AreEqual(1f, result[^1], 1e-6);
    }

    [TestMethod]
    public void DetailsReduceNoiseWithoutErasingStepAndSharpenDifferentFromDenoise()
    {
        const int w = 32; const int h = 16;
        var rgb = new float[w * h * 3];
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) { var value = (x < 16 ? .25f : .75f) + ((x + y) % 2 == 0 ? -.015f : .015f); for (var c = 0; c < 3; c++) rgb[(y * w + x) * 3 + c] = value; }
        var image = new HighBitDepthImageBuffer(w, h, rgb);
        var clean = Apply(image, Node(ColorStudioNodeType.Details, ("luma_noise", 100)));
        static double Variance(IEnumerable<float> values) { var v = values.ToArray(); var mean = v.Average(); return v.Average(x => (x - mean) * (x - mean)); }
        var interior = Enumerable.Range(0, w * h).Where(p => p % w is > 2 and < 13).ToArray();
        Assert.IsLessThan(Variance(interior.Select(p => rgb[p * 3])) * .5, Variance(interior.Select(p => clean[p * 3])));
        Assert.IsGreaterThan(.4, clean[(8 * w + 17) * 3] - clean[(8 * w + 14) * 3]);
        var sharp = Apply(image, Node(ColorStudioNodeType.Details, ("sharpen", 80), ("threshold", 0)));
        Assert.IsFalse(sharp.SequenceEqual(clean)); Assert.IsFalse(sharp.SequenceEqual(rgb));
    }

    [TestMethod]
    public void FloatAndDisplayUseTheSameWholeStackWithoutNodeQuantization()
    {
        var bytes = Enumerable.Range(0, 64 * 3).Select(i => (byte)(20 + i * 37 % 220)).ToArray(); var pixels = new VisualPixelBuffer(8, 8, bytes);
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "tools", pixels));
        var stack = new ColorAdjustmentStack([Node(ColorStudioNodeType.WhiteBalance, ("temperature", 17)), Node(ColorStudioNodeType.BasicTone, ("exposure", .7)), Node(ColorStudioNodeType.Curve, ("rgb_y2", .58))], ProcessingVersion: 2);
        var pipeline = new ColorStudioRenderPipeline(); var display = pipeline.Render(pixels, analysis, null, stack); var master = pipeline.Render(HighBitDepthImageBuffer.FromVisualRgb24(pixels), analysis, null, stack);
        CollectionAssert.AreEqual(master.Pixels.Rgb24.ToArray(), display.Pixels.Rgb24.ToArray());
        CollectionAssert.AreEqual(master.ProcessingPixels!.Rgb32.ToArray(), display.ProcessingPixels!.Rgb32.ToArray());
        var restored = ColorStudioSchemeSerializer.Deserialize(ColorStudioSchemeSerializer.Serialize(new(Guid.NewGuid(), "完整处理", stack, DateTimeOffset.UnixEpoch)));
        CollectionAssert.AreEqual(master.ProcessingPixels.Rgb32.ToArray(), pipeline.Render(HighBitDepthImageBuffer.FromVisualRgb24(pixels), analysis, null, restored.Stack).ProcessingPixels!.Rgb32.ToArray());
        var reversed = stack with { Nodes = stack.Nodes.Reverse().ToArray() };
        Assert.IsFalse(master.Pixels.Rgb24.ToArray().SequenceEqual(pipeline.Render(pixels, analysis, null, reversed).Pixels.Rgb24.ToArray()));
    }

    [TestMethod]
    public void InvalidParametersAreRejectedAndCancellationInterruptsPixelWork()
    {
        var source = new HighBitDepthImageBuffer(64, 64, Enumerable.Repeat(.4f, 64 * 64 * 3).ToArray());
        Assert.ThrowsExactly<ArgumentException>(() => Apply(source, Node(ColorStudioNodeType.WhiteBalance, ("temperature", double.NaN))));
        Assert.ThrowsExactly<ArgumentException>(() => Apply(source, Node(ColorStudioNodeType.Curve, ("rgb_count", double.PositiveInfinity))));
        Assert.ThrowsExactly<OperationCanceledException>(() => ColorStudioToolProcessor.Apply(source, Node(ColorStudioNodeType.Details, ("luma_noise", 50)), new CancellationToken(true)));
        var node = Node(ColorStudioNodeType.WhiteBalance, ("temperature", 1000));
        CollectionAssert.AreEqual(Apply(source, node), Apply(source, Node(ColorStudioNodeType.WhiteBalance, ("temperature", 100))));
    }

    [TestMethod]
    public void EveryEffectControlChangesItsIntendedFixtureAndResetRestoresIdentity()
    {
        var samples = Enumerable.Range(0, 32 * 32).SelectMany(i => new[] { .10f + (i * 7 % 83) / 100f, .08f + (i * 11 % 81) / 100f, .09f + (i * 3 % 80) / 100f }).ToArray();
        var source = new HighBitDepthImageBuffer(32, 32, samples);
        var cases = new Dictionary<ColorStudioNodeType, string[]>
        {
            [ColorStudioNodeType.WhiteBalance] = ["temperature", "tint"],
            [ColorStudioNodeType.BasicTone] = ["exposure", "brightness", "contrast", "highlights", "shadows", "whites", "blacks", "saturation", "vibrance"],
            [ColorStudioNodeType.ColorBalance] = ["master_amount", "shadows_amount", "midtones_amount", "highlights_amount"],
            [ColorStudioNodeType.Details] = ["clarity", "structure", "sharpen", "luma_noise", "chroma_noise"]
        };
        foreach (var (type, keys) in cases) foreach (var key in keys)
        {
            var effect = Apply(source, Node(type, (key, key == "exposure" ? .5 : 40)));
            Assert.IsFalse(samples.SequenceEqual(effect), $"{type}.{key} must alter actual pixels");
            CollectionAssert.AreEqual(samples, Apply(source, Node(type, (key, 0))), $"{type}.{key} reset");
        }
        var skin = OklabColorSpace.ToSrgbGamutMappedFloat(new(.55, .12, .08));
        var skinSource = Image((float)skin.R, (float)skin.G, (float)skin.B);
        foreach (var key in new[] { "hue_uniformity", "chroma_uniformity", "lightness_uniformity" }) Assert.IsFalse(skinSource.Rgb32.ToArray().SequenceEqual(Apply(skinSource, Node(ColorStudioNodeType.SkinTone, (key, 50)))));
    }

    [TestMethod]
    public void OldEnumValuesAndLegacyDevelopMathematicsAreUnchanged()
    {
        const string json = "{\"id\":\"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee\",\"name\":\"旧方案\",\"stack\":{\"nodes\":[{\"id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"type\":5,\"name\":\"原显影\",\"enabled\":true,\"numericParameters\":{\"brightness\":50}}],\"version\":2,\"workingSpace\":\"OKLabD65\"},\"updatedAtUtc\":\"2026-01-01T00:00:00Z\",\"version\":2}";
        var restored = ColorStudioSchemeSerializer.Deserialize(json); var source = Image(.2f, .3f, .4f);
        Assert.AreEqual(1, restored.Stack.ProcessingVersion);
        var legacy = ColorStudioDevelop.Apply(source, restored.Stack.Nodes[0]).Rgb32.ToArray();
        var equivalent = ColorStudioDevelop.Apply(source, Node(ColorStudioNodeType.Develop, ("exposure", .5))).Rgb32.ToArray();
        CollectionAssert.AreEqual(equivalent, legacy, "Stored legacy brightness remains its old EV meaning.");
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "old", source.ToVisualRgb24()));
        CollectionAssert.AreEqual(legacy, new ColorStudioRenderPipeline().Render(source, analysis, null, restored.Stack).ProcessingPixels!.Rgb32.ToArray());
    }

    [TestMethod]
    public void LegacyByteStackKeepsNodeQuantizationUntilPrecisionIsExplicitlyUpgraded()
    {
        var pixels = new VisualPixelBuffer(1, 1, new byte[] { 71, 113, 147 });
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "legacy", pixels));
        var first = Node(ColorStudioNodeType.Develop, ("brightness", 13)); var second = Node(ColorStudioNodeType.Develop, ("brightness", -13));
        var renderer = new ColorStudioRenderPipeline(); var legacy = new ColorAdjustmentStack([first, second]);
        var once = ColorStudioDevelop.Apply(HighBitDepthImageBuffer.FromVisualRgb24(pixels), first).ToVisualRgb24();
        var twice = ColorStudioDevelop.Apply(HighBitDepthImageBuffer.FromVisualRgb24(once), second).ToVisualRgb24();
        CollectionAssert.AreEqual(twice.Rgb24.ToArray(), renderer.Render(pixels, analysis, null, legacy).Pixels.Rgb24.ToArray());
        var upgraded = legacy with { ProcessingVersion = 2 }; var result = renderer.Render(pixels, analysis, null, upgraded);
        Assert.IsNotNull(result.ProcessingPixels);
        var restored = ColorStudioSchemeSerializer.Deserialize(ColorStudioSchemeSerializer.Serialize(new(Guid.NewGuid(), "升级精度", upgraded, DateTimeOffset.UnixEpoch)));
        Assert.AreEqual(2, restored.Stack.ProcessingVersion);
    }

    [TestMethod]
    public void NewColorRangeSeparatesRelativeSaturationFromAbsoluteChromaAndKeepsFeatherContinuous()
    {
        var colors = Enumerable.Range(0, 101).Select(i => OklabColorSpace.ToSrgbGamutMappedFloat(new(.6, .02 + i * .001, .03))).ToArray();
        var image = new HighBitDepthImageBuffer(101, 1, colors.SelectMany(c => new[] { (float)c.R, (float)c.G, (float)c.B }).ToArray());
        var pixels = image.ToVisualRgb24(); var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "range", pixels));
        var sample = pixels.Rgb24.ToArray(); var node = Node(ColorStudioNodeType.ColorRange, ("range_version", 2), ("range", .03), ("softness", .06)) with { Samples = [new(sample[0], sample[1], sample[2])] };
        var renderer = new ColorStudioRenderPipeline();
        float[] Render(string key, double value) => renderer.Render(image, analysis, null, new([node with { NumericParameters = new Dictionary<string, double>(node.NumericParameters) { [key] = value } }], ProcessingVersion: 2)).ProcessingPixels!.Rgb32.ToArray();
        var relative = Render("saturation", 40); var absolute = Render("chroma", 40);
        Assert.IsFalse(relative.SequenceEqual(absolute));
        for (var i = 3; i < absolute.Length; i += 3) Assert.IsLessThan(.08, Math.Abs(absolute[i] - absolute[i - 3]), "Soft membership may not make a hard color edge.");
        var bounded = node with { NumericParameters = new Dictionary<string, double>(node.NumericParameters) { ["lightness_min"] = .9, ["lightness_feather"] = .02, ["hue"] = 50 } };
        CollectionAssert.AreEqual(image.Rgb32.ToArray(), renderer.Render(image, analysis, null, new([bounded], ProcessingVersion: 2)).ProcessingPixels!.Rgb32.ToArray());
    }

    [TestMethod]
    public void QuantitativeToolFixtureRecordsColorAndToneChangesWithoutPretendingToBeRealPhotos()
    {
        var colors = new[] { ("中性灰", .5f, .5f, .5f), ("肤色样本", .76f, .54f, .42f), ("蓝天样本", .25f, .50f, .80f), ("植被样本", .18f, .40f, .16f), ("近白", .97f, .96f, .94f), ("近黑", .01f, .015f, .02f) };
        var image = new HighBitDepthImageBuffer(colors.Length, 1, colors.SelectMany(c => new[] { c.Item2, c.Item3, c.Item4 }).ToArray());
        var rows = new List<object>();
        foreach (var (type, key, value) in new[] { (ColorStudioNodeType.WhiteBalance, "temperature", 35d), (ColorStudioNodeType.BasicTone, "exposure", .5), (ColorStudioNodeType.BasicTone, "highlights", -50d), (ColorStudioNodeType.BasicTone, "shadows", 50d), (ColorStudioNodeType.SkinTone, "hue_uniformity", 50d), (ColorStudioNodeType.Levels, "rgb_gamma", 1.2), (ColorStudioNodeType.Curve, "rgb_y2", .6) })
        {
            var result = Apply(image, Node(type, (key, value))); var deltas = new List<double>(); var tones = new List<double>(); var chroma = new List<double>();
            for (var i = 0; i < colors.Length; i++)
            {
                var before = OklabColorSpace.FromSrgb(colors[i].Item2, colors[i].Item3, colors[i].Item4); var after = OklabColorSpace.FromSrgb(result[i * 3], result[i * 3 + 1], result[i * 3 + 2]);
                var dl = after.L - before.L; var dc = after.Chroma - before.Chroma; var distance = Math.Sqrt(dl * dl + Math.Pow(after.A - before.A, 2) + Math.Pow(after.B - before.B, 2));
                deltas.Add(distance); tones.Add(Math.Abs(dl)); chroma.Add(Math.Abs(dc));
            }
            Assert.IsGreaterThan(0, deltas.Max(), $"{type}.{key} must have measured effect");
            rows.Add(new { tool = type.ToString(), parameter = key, value, meanOklabDelta = deltas.Average(), maxOklabDelta = deltas.Max(), meanToneDelta = tones.Average(), meanChromaDelta = chroma.Average(), finite = result.All(float.IsFinite), sampleCount = colors.Length });
        }
        var directory = Path.Combine(TestContext!.ResultsDirectory!, "new-tool-quantitative-fixture"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "METRICS.json"), System.Text.Json.JsonSerializer.Serialize(new { sourceHead = Environment.GetEnvironmentVariable("PIXEL_TART_TEST_SOURCE_HEAD") ?? "WORKTREE", scope = "synthetic declared color patches only; not real photo/C1 comparison", cases = rows }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    [TestMethod]
    public void NonReferenceEditsDoNotRepeatFullSourceAnalysisButReferenceMatchGetsActualPixels()
    {
        var red = new VisualPixelBuffer(1, 1, new byte[] { 255, 0, 0 }); var blue = new VisualPixelBuffer(1, 1, new byte[] { 0, 0, 255 });
        var tools = new ColorAdjustmentStack([Node(ColorStudioNodeType.WhiteBalance, ("tint", 10))], ProcessingVersion: 2);
        Assert.AreSame(ColorStudioProcessingAnalysis.Create(red, tools, null), ColorStudioProcessingAnalysis.Create(blue, tools, null));
        var reference = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "reference", red));
        var look = new ReferenceLook(Guid.NewGuid(), "参考", null, [new(Guid.NewGuid(), null, "参考", "fixture", "hash", 1, reference)], new(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var match = new ColorAdjustmentStack([Node(ColorStudioNodeType.ReferenceMatch)]);
        var analyzed = ColorStudioProcessingAnalysis.Create(blue, match, look); Assert.AreEqual(1u, analyzed.HistogramB[255]); Assert.AreEqual(1u, analyzed.HistogramR[0]);
        Assert.IsFalse(ColorStudioProcessingAnalysis.RequiresSourceAnalysis(match with { Nodes = [Node(ColorStudioNodeType.ReferenceMatch, ("match_strength", 0))] }, look));
    }

    [TestMethod]
    public void ProductRenderingCanAvoidRetainingLargePerNodeDiagnosticsWithoutChangingPixels()
    {
        var pixels = new VisualPixelBuffer(1, 1, new byte[] { 71, 113, 147 }); var stack = new ColorAdjustmentStack([Node(ColorStudioNodeType.WhiteBalance, ("temperature", 15)), Node(ColorStudioNodeType.BasicTone, ("exposure", .3))], ProcessingVersion: 2);
        var analysis = ColorStudioProcessingAnalysis.Create(pixels, stack, null); var renderer = new ColorStudioRenderPipeline();
        var detailed = renderer.Render(pixels, analysis, null, stack); var product = renderer.Render(pixels, analysis, null, stack, captureNodeDiagnostics: false);
        CollectionAssert.AreEqual(detailed.ProcessingPixels!.Rgb32.ToArray(), product.ProcessingPixels!.Rgb32.ToArray());
        Assert.HasCount(2, detailed.NodeOutputs); Assert.HasCount(0, product.NodeOutputs); Assert.HasCount(0, product.NodeInputs!);
    }

    [TestMethod]
    public void LevelExtremeGammaKeepsAnalyticDarkPrecisionAndCurveTableMatchesAnalyticCurve()
    {
        var tiny = Image(.00001f, .3f, .4f); var lifted = Apply(tiny, Node(ColorStudioNodeType.Levels, ("r_gamma", 5)));
        Assert.AreEqual(Math.Pow(.00001f, .2), lifted[0], 1e-7, "A coarse table near black would erase precision.");
        var node = Node(ColorStudioNodeType.Curve, ("rgb_y1", .12), ("rgb_y2", .6), ("rgb_y3", .9));
        var points = ColorStudioToolProcessor.ReadCurve(node, "rgb");
        var source = new HighBitDepthImageBuffer(1024, 1, Enumerable.Range(0, 1024).SelectMany(i => new[] { (i + .314f) / 1024, (i + .314f) / 1024, (i + .314f) / 1024 }).ToArray());
        var result = Apply(source, node);
        for (var i = 0; i < 1024; i++) Assert.AreEqual(ColorStudioToolProcessor.EvaluateCurve(points, source.Rgb32.Span[i * 3]), result[i * 3], 2e-6);
    }

    [TestMethod]
    public void SelectionMaskUsesFloatMembershipExcludesTransparentPixelsAndNeverMutatesInput()
    {
        var source = new VisualPixelBuffer(3, 1, new byte[] { 255, 0, 0, 255, 0, 0, 0, 0, 255 }, new byte[] { 255, 0, 255 });
        var node = Node(ColorStudioNodeType.ColorRange, ("range", .02), ("softness", .01)) with { Samples = [new(255, 0, 0)] };
        CollectionAssert.AreEqual(new byte[] { 255, 0, 0 }, ColorStudioRenderPipeline.SelectionWeights(source, node));
        var input = HighBitDepthImageBuffer.FromVisualRgb24(source); var before = input.Rgb32.ToArray();
        CollectionAssert.AreEqual(new byte[] { 255, 255, 0 }, ColorStudioRenderPipeline.SelectionWeights(input, node));
        CollectionAssert.AreEqual(before, input.Rgb32.ToArray());
        var negative = node with { NegativeSamples = [new(255, 0, 0)] };
        Assert.IsTrue(ColorStudioRenderPipeline.SelectionWeights(input, negative).All(value => value == 0));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => ColorStudioRenderPipeline.SelectionWeights(input, node, cancelled.Token));
    }

    [TestMethod]
    public void UnknownFutureNodeIsRejectedInsteadOfSilentlyDiscardingItsEffect()
    {
        var node = Node((ColorStudioNodeType)998, ("future_adjustment", 42));
        var stack = new ColorAdjustmentStack([node]);
        Assert.ThrowsExactly<ArgumentException>(() => stack.Normalize());
        Assert.AreEqual(42d, node.NumericParameters["future_adjustment"]);
        var serialized = System.Text.Json.JsonSerializer.Serialize(stack);
        var decoded = System.Text.Json.JsonSerializer.Deserialize<ColorAdjustmentStack>(serialized)!;
        Assert.ThrowsExactly<ArgumentException>(() => decoded.Normalize());
        Assert.AreEqual(42d, decoded.Nodes[0].NumericParameters["future_adjustment"]);
    }
}
