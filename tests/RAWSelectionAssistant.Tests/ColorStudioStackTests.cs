using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorStudioStackTests
{
    [TestMethod]
    public void SchemeV2_RoundTripsOrderedNodesAndHash()
    {
        var scheme = new ColorStudioSchemeV2(Guid.NewGuid(), "Studio", new ColorAdjustmentStack([
            new(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "参考仿色", true, new Dictionary<string, double> { ["match_strength"] = 80 }),
            new(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "肤色", true, new Dictionary<string, double> { ["keep_original_luminance"] = 1 }, [new(210, 150, 120)]),
            new(Guid.NewGuid(), ColorStudioNodeType.TransitionBlend, "过渡", true, new Dictionary<string, double> { ["amount"] = .2 }), new(Guid.NewGuid(), ColorStudioNodeType.Film, "胶片")]), DateTimeOffset.UtcNow);
        var restored = ColorStudioSchemeSerializer.Deserialize(ColorStudioSchemeSerializer.Serialize(scheme));
        Assert.HasCount(4, restored.Stack.Nodes); CollectionAssert.AreEqual(scheme.Stack.Nodes.Select(node => node.Type).ToArray(), restored.Stack.Nodes.Select(node => node.Type).ToArray()); Assert.AreEqual(ColorStudioSchemeSerializer.ComputeHash(scheme), ColorStudioSchemeSerializer.ComputeHash(restored));
    }

    [TestMethod]
    public void LegacyReferenceLook_MigratesToReferenceAndFilmNodes()
    {
        var source = new VisualPixelBuffer(2, 1, new byte[] { 30, 40, 50, 200, 180, 160 }); var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "source", source, 3));
        var reference = new ReferenceLook(Guid.NewGuid(), "旧方案", null, [new(Guid.NewGuid(), null, "ref", "fixture", "hash", 1, analysis)], new(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, new(true, "PT-W01", 50));
        var migrated = ColorStudioLegacyMigration.Migrate(reference);
        CollectionAssert.AreEqual(new[] { ColorStudioNodeType.ReferenceMatch, ColorStudioNodeType.Film }, migrated.Stack.Nodes.Select(node => node.Type).ToArray());
        Assert.AreEqual("PT-W01", migrated.Stack.Nodes[1].FilmSettings?.ProfileId);
    }

    [TestMethod]
    public void RenderPipeline_PreservesLinearOrderAndDoesNotMutateSource()
    {
        var bytes = new byte[] { 35, 50, 70, 190, 150, 120, 60, 80, 100, 220, 210, 200 }; var source = new VisualPixelBuffer(2, 2, bytes); var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "source", source, 3));
        var stack = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "范围", true, new Dictionary<string, double> { ["strength"] = 100, ["keep_original_luminance"] = 1 }, [new(190, 150, 120)]), new(Guid.NewGuid(), ColorStudioNodeType.TransitionBlend, "过渡", true, new Dictionary<string, double> { ["amount"] = .25 }), new(Guid.NewGuid(), ColorStudioNodeType.Film, "胶片", true, new Dictionary<string, double> { ["profile_amount"] = 70 })]);
        var result = new ColorStudioRenderPipeline().Render(source, analysis, null, stack); Assert.HasCount(3, result.NodeOutputs); CollectionAssert.AreEqual(bytes, source.Rgb24.ToArray()); CollectionAssert.AreNotEqual(bytes, result.Pixels.Rgb24.ToArray());
    }

    [TestMethod]
    public void ColorRangeSamplesAndSelectionPreview_AreSeparateFromExport()
    {
        var source = new VisualPixelBuffer(2, 1, new byte[] { 190, 150, 120, 25, 65, 170 });
        var node = new ColorAdjustmentStackNode(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "肤色", true,
            new Dictionary<string, double> { ["hue"] = 25, ["lightness"] = .1, ["keep_original_luminance"] = 1, ["range"] = .01, ["softness"] = .01 }, [new(190, 150, 120)]);
        var renderer = new ColorStudioRenderPipeline();
        var preview = renderer.ShowSelection(source, node);
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "source", source, 3));
        var output = renderer.Render(source, analysis, null, new ColorAdjustmentStack([node])).Pixels;
        CollectionAssert.AreNotEqual(preview.Rgb24.ToArray(), output.Rgb24.ToArray());
        CollectionAssert.AreEqual(source.Rgb24.ToArray()[3..], output.Rgb24.ToArray()[3..]);
        var original = OklabColorSpace.FromSrgb(new(190, 150, 120));
        var adjusted = OklabColorSpace.FromSrgb(new(output.Rgb24.Span[0], output.Rgb24.Span[1], output.Rgb24.Span[2]));
        Assert.IsLessThan(.012, Math.Abs(original.L - adjusted.L));
        Assert.HasCount(2, node.AddSample(new(20, 40, 60)).Samples);
        Assert.HasCount(0, node.RemoveSampleAt(0).Samples);
    }

    [TestMethod]
    public void SelectedNodeSync_PreservesTargetSpecificNodes()
    {
        var shared = Guid.NewGuid(); var privateId = Guid.NewGuid();
        var original = new ColorAdjustmentStack([new(shared, ColorStudioNodeType.Film, "original"), new(privateId, ColorStudioNodeType.ColorRange, "target-only")]);
        var incoming = new ColorAdjustmentStack([new(shared, ColorStudioNodeType.Film, "updated")]);
        var result = original.SyncSelectedFrom(incoming, new HashSet<Guid> { shared });
        CollectionAssert.AreEqual(new[] { "updated", "target-only" }, result.Nodes.Select(node => node.Name).ToArray());
    }

    [TestMethod]
    public void TransitionBlend_DoesNotBlurTextureBetweenAdjacentPixels()
    {
        var source = new VisualPixelBuffer(2, 1, new byte[] { 0, 0, 0, 255, 255, 255 });
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "edge", source, 3));
        var result = new ColorStudioRenderPipeline().Render(source, analysis, null,
            new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.TransitionBlend, "blend")])).Pixels;
        CollectionAssert.AreEqual(source.Rgb24.ToArray(), result.Rgb24.ToArray());
    }
}
