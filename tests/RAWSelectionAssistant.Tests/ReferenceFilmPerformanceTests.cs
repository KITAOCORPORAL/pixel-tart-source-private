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
        if (string.IsNullOrWhiteSpace(output))
        {
            Assert.Inconclusive("Set PIXEL_TART_PERFORMANCE_EVIDENCE to collect local CPU evidence.");
            return;
        }
        Directory.CreateDirectory(output!);
        var rows = new List<object>();
        Measure(1600, 1067, "1600px interactive proxy");
        Measure(2048, 1365, "2048px proxy");
        Measure(1920, 1080, "1080p settled");
        Measure(6000, 4000, "24MP final");
        MeasureCancellation(6000, 4000);
        File.WriteAllText(Path.Combine(output!, "film-performance.json"), JsonSerializer.Serialize(new { source_commit = Environment.GetEnvironmentVariable("PIXEL_TART_PRODUCT_SOURCE_SHA") ?? "UNFROZEN_WORKTREE", rows }, new JsonSerializerOptions { WriteIndented = true }));
        void Measure(int width, int height, string name)
        {
            var rgb = new byte[width * height * 3];
            for (var i = 0; i < width * height; i++) { rgb[i * 3] = (byte)(i % 256); rgb[i * 3 + 1] = (byte)((i / width) % 256); rgb[i * 3 + 2] = (byte)((i / 17) % 256); }
            var input = new VisualPixelBuffer(width, height, rgb);
            _ = PixelTartFilmPipeline.Apply(input, new(true, "PT-W01", 70, 42, 38, 35, 28, 20, 40, "Paper", 55, 17));
            var before = GC.GetTotalMemory(true); var allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            _ = PixelTartFilmPipeline.Apply(input, new(true, "PT-W01", 70, 42, 38, 35, 28, 20, 40, "Paper", 55, 17));
            watch.Stop(); var after = GC.GetTotalMemory(false);
            rows.Add(new { name, width, height, film_ms = watch.Elapsed.TotalMilliseconds, managed_memory_delta_mb = Math.Max(0, after - before) / 1048576d, allocated_mb = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 1048576d, warm_buffer_pool = true, backend = "CPU" });
        }
        void MeasureCancellation(int width, int height)
        {
            var input = new VisualPixelBuffer(width, height, Enumerable.Repeat((byte)170, width * height * 3).ToArray());
            using var cancellation = new CancellationTokenSource(); var started = new ManualResetEventSlim(); var watch = new Stopwatch();
            var task = Task.Run(() => { started.Set(); return PixelTartFilmPipeline.Apply(input, new(true, "PT-W01", 70, 42, 38, 35, 28, 20, 40, "Paper", 55, 17), cancellation.Token); });
            started.Wait(); Thread.Sleep(50); watch.Start(); cancellation.Cancel();
            var canceled = false; try { task.GetAwaiter().GetResult(); } catch (OperationCanceledException) { canceled = true; }
            watch.Stop(); rows.Add(new { name = "24MP cancel", width, height, cancel_latency_ms = watch.Elapsed.TotalMilliseconds, canceled, backend = "CPU" });
        }
    }
}
