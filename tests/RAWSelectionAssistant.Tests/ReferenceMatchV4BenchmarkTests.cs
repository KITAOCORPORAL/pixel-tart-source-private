using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ReferenceMatchV4BenchmarkTests
{
    [TestMethod]
    public void QualityCorpusWritesQuantitativeV3V4Report()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "artifacts", "reference-match-v4"); Directory.CreateDirectory(root);
        var rows = new List<object>();
        for (var scene = 1; scene <= 20; scene++)
        {
            var target = Fixture(96, 64, scene); var reference = Fixture(96, 64, scene + 31);
            var targetAnalysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), $"v4-target-{scene}", target, 5));
            var refAnalysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), $"v4-ref-{scene}", reference, 5));
            var look = new ReferenceLook(Guid.NewGuid(), $"fixture-{scene}", null,
                [new(Guid.NewGuid(), null, "synthetic", "fixture", $"fixture-{scene}", 1, refAnalysis)], new(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var v3 = new ReferenceLookMatcher().Match(target, targetAnalysis, look).Preview;
            var v4 = new ReferenceMatchV4Engine().Match(target, reference, settings: new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 64, SinkhornIterations: 8), preferGpu: false).Pixels;
            var v3Error = MeanOklabDistance(v3, reference); var v4Error = MeanOklabDistance(v4, reference);
            rows.Add(new { scene, v3OklabError = v3Error, v4OklabError = v4Error, neutralDrift = NeutralDrift(v4), highlightContamination = HighlightContamination(v4), shadowOversaturation = ShadowSaturation(v4), gamutClipping = GamutClipping(v4) });
            if (scene == 1) { WritePpm(Path.Combine(root, "scene-01-target.ppm"), target); WritePpm(Path.Combine(root, "scene-01-reference.ppm"), reference); WritePpm(Path.Combine(root, "scene-01-v3.ppm"), v3); WritePpm(Path.Combine(root, "scene-01-v4-cpu.ppm"), v4); }
        }
        File.WriteAllText(Path.Combine(root, "REFERENCE_MATCH_V3_V4_QUALITY_REPORT.json"), JsonSerializer.Serialize(new { generatedUtc = DateTimeOffset.UtcNow, corpus = 20, rows }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.HasCount(20, rows);
    }

    [TestMethod]
    public void ProtectionBehaviorWritesMeasuredReport()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "artifacts", "reference-match-v4"); Directory.CreateDirectory(root);
        var target = Fixture(96, 64, 71); var reference = Fixture(96, 64, 91);
        var rows = new List<object>();
        foreach (var protection in new[] { 0d, .25, .5, .75, 1d })
        {
            var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 64, SinkhornIterations: 8)
            { NeutralProtection = protection, SkinProtection = protection, HighlightProtection = protection, ShadowProtection = protection };
            var output = new ReferenceMatchV4Engine().Match(target, reference, settings: settings, preferGpu: false).Pixels;
            rows.Add(new { protection, skinCandidateDelta = MeanOklabDistance(output, target), neutralDrift = NeutralDrift(output), highlightContamination = HighlightContamination(output), shadowOversaturation = ShadowSaturation(output) });
        }
        File.WriteAllText(Path.Combine(root, "PROTECTION_BEHAVIOR_REPORT.json"), JsonSerializer.Serialize(new { generatedUtc = DateTimeOffset.UtcNow, rows }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.HasCount(5, rows);
    }

    [TestMethod]
    public void ProductionResolutionBenchmarkWritesThreeRepeatsWhenEnabled()
    {
        if (Environment.GetEnvironmentVariable("PIXEL_TART_RUN_V4_BENCHMARK") != "1") { Assert.Inconclusive("Set PIXEL_TART_RUN_V4_BENCHMARK=1 for the bounded production-size run."); return; }
        var root = Path.Combine(Environment.CurrentDirectory, "artifacts", "reference-match-v4"); Directory.CreateDirectory(root);
        var results = new List<object>();
        foreach (var (name, width, height) in new[] { ("12MP", 4000, 3000), ("24MP", 6000, 4000), ("45MP", 8256, 5504), ("60MP", 10000, 6000) })
        {
            for (var repeat = 1; repeat <= 3; repeat++)
            {
                var target = Fixture(width, height, repeat + width); var reference = Fixture(width, height, repeat + width + 100);
                var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 256, SinkhornIterations: 16, ResidualIterations: 0, TileSize: 1024, TileOverlap: 16);
                foreach (var preferGpu in new[] { false, true })
                {
                    var before = GC.GetTotalMemory(true); var timer = Stopwatch.StartNew();
                    var output = new ReferenceMatchV4Engine().Match(target, reference, name, $"{name}-reference", settings, preferGpu);
                    timer.Stop(); var after = GC.GetTotalMemory(false);
                    results.Add(new { resolution = name, width, height, repeat, elapsedMs = timer.Elapsed.TotalMilliseconds, allocatedWorkingSetBytes = Math.Max(0, after - before), representativeSamples = output.RepresentativeSourceCount, tileCount = ColorMatchTilePlanner.Plan(width, height, settings).Count, backend = output.Backend.ToString(), usedCpuFallback = output.UsedCpuFallback });
                }
            }
        }
        File.WriteAllText(Path.Combine(root, "REFERENCE_MATCH_V4_GPU_PERFORMANCE.json"), JsonSerializer.Serialize(new { generatedUtc = DateTimeOffset.UtcNow, gpu = GpuCapabilityDetector.Detect(), results }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.HasCount(24, results);
    }

    private static VisualPixelBuffer Fixture(int width, int height, int seed)
    {
        var bytes = new byte[checked(width * height * 3)];
        for (var i = 0; i < bytes.Length; i += 3) { var p = i / 3; bytes[i] = (byte)((p * 13 + seed * 17) % 256); bytes[i + 1] = (byte)((p * 7 + seed * 29) % 256); bytes[i + 2] = (byte)((p * 3 + seed * 43) % 256); }
        return new(width, height, bytes);
    }
    private static double MeanOklabDistance(VisualPixelBuffer a, VisualPixelBuffer b)
    { var sum = 0d; for (var i = 0; i < a.PixelCount; i++) { var o = i * 3; var x = OklabColorSpace.FromSrgb(new(a.Rgb24.Span[o], a.Rgb24.Span[o + 1], a.Rgb24.Span[o + 2])); var y = OklabColorSpace.FromSrgb(new(b.Rgb24.Span[o], b.Rgb24.Span[o + 1], b.Rgb24.Span[o + 2])); sum += Math.Sqrt(Math.Pow(x.L - y.L, 2) + Math.Pow(x.A - y.A, 2) + Math.Pow(x.B - y.B, 2)); } return sum / a.PixelCount; }
    private static double NeutralDrift(VisualPixelBuffer p) => Enumerable.Range(0, p.PixelCount).Select(i => { var o = i * 3; var c = OklabColorSpace.FromSrgb(new(p.Rgb24.Span[o], p.Rgb24.Span[o + 1], p.Rgb24.Span[o + 2])); return c.Chroma < .04 ? c.Chroma : 0; }).Average();
    private static double HighlightContamination(VisualPixelBuffer p) => Enumerable.Range(0, p.PixelCount).Select(i => { var o = i * 3; var c = OklabColorSpace.FromSrgb(new(p.Rgb24.Span[o], p.Rgb24.Span[o + 1], p.Rgb24.Span[o + 2])); return c.L > .85 ? c.Chroma : 0; }).Average();
    private static double ShadowSaturation(VisualPixelBuffer p) => Enumerable.Range(0, p.PixelCount).Select(i => { var o = i * 3; var c = OklabColorSpace.FromSrgb(new(p.Rgb24.Span[o], p.Rgb24.Span[o + 1], p.Rgb24.Span[o + 2])); return c.L < .2 ? c.Chroma : 0; }).Average();
    private static double GamutClipping(VisualPixelBuffer p) => p.Rgb24.Span.ToArray().Count(value => value is 0 or 255) / (double)p.Rgb24.Length;
    private static void WritePpm(string path, VisualPixelBuffer p) => File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes($"P6\n{p.Width} {p.Height}\n255\n").Concat(p.Rgb24.ToArray()).ToArray());
}
