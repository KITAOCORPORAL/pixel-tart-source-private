using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorStudioSpatialScaleTests
{
    [TestMethod]
    public async Task AuthorizedFullRawSpatialEffectsRecordSameCoordinateScaleError()
    {
        var inputRoot = Environment.GetEnvironmentVariable("PIXEL_TART_NEW_STUDIO_CORPUS");
        var outputRoot = Environment.GetEnvironmentVariable("PIXEL_TART_SPATIAL_OUTPUT");
        if (string.IsNullOrEmpty(inputRoot) || string.IsNullOrEmpty(outputRoot)) { Assert.Inconclusive("Real spatial evidence is explicitly opt-in."); return; }
        Directory.CreateDirectory(outputRoot);
        var pipeline = new RawMatchTiff16ProductPipeline(new LibRawDecoder());
        var master = await pipeline.DecodeFrozenMasterAsync(Path.Combine(inputRoot, "DSC04831.ARW"));
        var proxies = new[] { 800, 1600 }.Select(edge => pipeline.PreviewMaster(master.Image, edge)).ToArray();
        var cases = new List<object>();
        foreach (var name in new[] { "clarity-structure", "sharpen", "denoise", "film-grain", "film-paper", "film-bloom-halo" })
        {
            var timer = Stopwatch.StartNew(); var full = Apply(master.Image, name); var fullMs = timer.Elapsed.TotalMilliseconds;
            foreach (var proxy in proxies)
            {
                timer.Restart(); var preview = Apply(proxy, name); var ms = timer.Elapsed.TotalMilliseconds;
                var delta = Measure(preview, full); cases.Add(new { name, fullDimensions = new[] { master.Image.Width, master.Image.Height }, previewDimensions = new[] { proxy.Width, proxy.Height }, fullMs, previewMs = ms, delta, contract = "same source center coordinates; full spatial neighborhood differs from bounded nearest proxy; measure output L error, not claim exact pixels" });
                await File.WriteAllTextAsync(Path.Combine(outputRoot, "SPATIAL_SCALE_MATRIX.json"), JsonSerializer.Serialize(new { source = "DSC04831.ARW", master.SourceSha256, cases }, new JsonSerializerOptions { WriteIndented = true }));
                // Perceptual L is normalized 0..1. Interactive 800 is approximate;
                // settled 1600 has a stricter budget. Tail errors remain bounded and
                // recorded, not hidden behind a mean-only assertion.
                var film = name.StartsWith("film"); var settled = proxy.Width >= 1600;
                var budget = film ? (Mean: .003, P95: .008, Max: .04)
                    : settled ? (Mean: .003, P95: .009, Max: .045) : (Mean: .005, P95: .015, Max: .07);
                Assert.IsLessThanOrEqualTo(budget.Mean, delta.MeanL, name + " mean L");
                Assert.IsLessThanOrEqualTo(budget.P95, delta.P95L, name + " P95 L");
                Assert.IsLessThanOrEqualTo(budget.Max, delta.MaxL, name + " maximum L");
            }
            GC.Collect(); GC.WaitForPendingFinalizers();
        }
        await master.ValidateSourceAsync();
    }

    [TestMethod]
    public void FilmV2GrainAndAllTexturesKeepPhaseAtAlignedImageCoordinates()
    {
        var full = new HighBitDepthImageBuffer(1200, 675, Enumerable.Repeat(.35f, 1200 * 675 * 3).ToArray());
        var proxy = new HighBitDepthImageBuffer(400, 225, Enumerable.Repeat(.35f, 400 * 225 * 3).ToArray());
        foreach (var texture in new[] { "None", "FineFiber", "Paper", "SoftMist", "Scanline" })
        {
            var settings = new PixelTartFilmSettings(true, GrainAmount: 70, SurfaceAmount: 80, TextureId: texture, TextureAmount: 80, Seed: 42, SpatialVersion: 2);
            var a = PixelTartFilmPipeline.Apply(full, settings); var b = PixelTartFilmPipeline.Apply(proxy, settings);
            var delta = Measure(b, a);
            Assert.AreEqual(0, delta.MaxL, 1e-7, "Aligned centers must share texture phase: " + texture);
            Assert.IsFalse(b.Rgb32.ToArray().SequenceEqual(proxy.Rgb32.ToArray()));
            CollectionAssert.AreEqual(b.Rgb32.ToArray(), PixelTartFilmPipeline.Apply(proxy, settings).Rgb32.ToArray());
        }
    }

    [TestMethod]
    public void FilmLegacyJsonDefaultsToVersionOneAndVersionTwoProtectsDeepShadows()
    {
        var old = JsonSerializer.Deserialize<PixelTartFilmSettings>("{\"Enabled\":true,\"GrainAmount\":70,\"Seed\":42}")!;
        Assert.AreEqual(1, old.SpatialVersion);
        var image = new HighBitDepthImageBuffer(64, 64, Enumerable.Repeat(.01f, 64 * 64 * 3).ToArray());
        CollectionAssert.AreEqual(PixelTartFilmPipeline.Apply(image, old).Rgb32.ToArray(), PixelTartFilmPipeline.Apply(image, old with { SpatialVersion = 1 }).Rgb32.ToArray());
        var texture = new PixelTartFilmSettings(true, SurfaceAmount: 100, TextureId: "Paper", TextureAmount: 100, SpatialVersion: 2);
        var result = PixelTartFilmPipeline.Apply(image, texture);
        Assert.IsLessThan(.04f, result.Rgb32.ToArray().Max(), "Deep-black paper must not become broad bright blotches.");
        Assert.AreEqual(texture, JsonSerializer.Deserialize<PixelTartFilmSettings>(JsonSerializer.Serialize(texture)));
    }
    private static HighBitDepthImageBuffer Apply(HighBitDepthImageBuffer image, string name)
    {
        if (name.StartsWith("film")) return PixelTartFilmPipeline.Apply(image, name switch
        {
            "film-grain" => new(true, GrainAmount: 70, GrainSize: 35, Seed: 42, SpatialVersion: 2),
            "film-paper" => new(true, SurfaceAmount: 80, TextureId: "Paper", TextureAmount: 80, Seed: 42, SpatialVersion: 2),
            _ => new(true, HalationAmount: 70, BloomAmount: 70, SpatialVersion: 2)
        });
        var parameters = name switch
        {
            "clarity-structure" => new Dictionary<string,double>{{"clarity",30},{"structure",30}},
            "sharpen" => new Dictionary<string,double>{{"sharpen",50},{"radius",2},{"threshold",.01}},
            _ => new Dictionary<string,double>{{"luma_noise",35},{"chroma_noise",35}}
        };
        return ColorStudioToolProcessor.Apply(image, new(Guid.NewGuid(), ColorStudioNodeType.Details, name, NumericParameters: parameters));
    }
    private sealed record Delta(double MeanL, double P95L, double MaxL, double MeanRgb, int Samples);
    private static Delta Measure(HighBitDepthImageBuffer proxy, HighBitDepthImageBuffer full)
    {
        var differences = new List<double>(); double rgb = 0;
        for (var p = 0; p < proxy.PixelCount; p += Math.Max(1, proxy.PixelCount / 65536))
        {
            var x = p % proxy.Width; var y = p / proxy.Width;
            var sx = Math.Min(full.Width-1,(int)((x+.5)*full.Width/proxy.Width)); var sy = Math.Min(full.Height-1,(int)((y+.5)*full.Height/proxy.Height)); var q=(sy*full.Width+sx)*3;
            var a=OklabColorSpace.FromSrgb(proxy.Rgb32.Span[p*3],proxy.Rgb32.Span[p*3+1],proxy.Rgb32.Span[p*3+2]);
            var b=OklabColorSpace.FromSrgb(full.Rgb32.Span[q],full.Rgb32.Span[q+1],full.Rgb32.Span[q+2]); differences.Add(Math.Abs(a.L-b.L));
            for(var c=0;c<3;c++) rgb += Math.Abs(proxy.Rgb32.Span[p*3+c]-full.Rgb32.Span[q+c]);
        }
        var ordered=differences.Order().ToArray();return new(differences.Average(),ordered[(int)(ordered.Length*.95)],differences.Max(),rgb/(differences.Count*3),differences.Count);
    }
}
