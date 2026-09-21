using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ReferenceFilmPerformanceTests
{
    [TestMethod]
    [TestCategory("PerformanceEvidence")]
    public void FilmCpuPerformance_WritesMeasuredEvidenceWhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("PIXEL_TART_PERFORMANCE_EVIDENCE");
        if (string.IsNullOrWhiteSpace(output)) Assert.Inconclusive("Set PIXEL_TART_PERFORMANCE_EVIDENCE to collect local CPU evidence.");
        Directory.CreateDirectory(output!);
        var rows = new List<object>();
        Measure(1920, 1080, "1080p");
        Measure(6000, 4000, "24MP");
        File.WriteAllText(Path.Combine(output!, "film-performance.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        void Measure(int width, int height, string name)
        {
            var rgb = new byte[width * height * 3];
            for (var i = 0; i < width * height; i++) { rgb[i * 3] = (byte)(i % 256); rgb[i * 3 + 1] = (byte)((i / width) % 256); rgb[i * 3 + 2] = (byte)((i / 17) % 256); }
            var input = new VisualPixelBuffer(width, height, rgb);
            var before = GC.GetTotalMemory(true); var watch = Stopwatch.StartNew();
            _ = PixelTartFilmPipeline.Apply(input, new(true, "PT-W01", 70, 42, 38, 35, 28, 20, 40, "Paper", 55, 17));
            watch.Stop(); var after = GC.GetTotalMemory(false);
            rows.Add(new { name, width, height, film_ms = watch.Elapsed.TotalMilliseconds, managed_memory_delta_mb = Math.Max(0, after - before) / 1048576d, backend = "CPU" });
        }
    }
}
