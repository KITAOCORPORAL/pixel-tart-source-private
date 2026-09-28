using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using PixelTart.MatchV4.Dx12;

var outputPath = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "PixelTart-MatchV4-GPU-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 64, SinkhornIterations: 2, TileSize: 512);
var transform = new MatchV4ResolvedTransform(
    new(.035, .012, -.009),
    [new(.041, .016, -.011), new(.031, .009, -.008), new(.022, .006, -.004)],
    settings, "phase3-runtime-transform", Strength: .72, KeepLuminance: false);
var sourceValues = new float[256 * 192 * 3];
for (var i = 0; i < sourceValues.Length; i++)
{
    var pixel = i / 3; var channel = i % 3;
    var x = pixel % 256; var y = pixel / 256;
    sourceValues[i] = Math.Clamp((float)((channel switch { 0 => x, 1 => y, _ => (x + y) % 256 }) / 255d), 0, 1);
}
var source = new HighBitDepthImageBuffer(256, 192, sourceValues, "32f", "sRGB", 6);
var cpu = await new MatchV4CpuPixelBackend().ExecuteAsync(source, transform);
var wholeBackend = new Dx12ColorMatchComputeBackend(1024);
var tileBackend = new Dx12ColorMatchComputeBackend(64);
var report = new Dictionary<string, object?>
{
    ["generatedUtc"] = DateTimeOffset.UtcNow,
    ["hardware"] = wholeBackend.Capability,
    ["source"] = new { source.Width, source.Height, source.Orientation, source.WorkingColorSpace },
    ["transformHash"] = transform.TransformHash,
    ["cpu"] = new { backend = cpu.Backend.ToString(), computeMs = cpu.ComputeTime.TotalMilliseconds },
};
if (!wholeBackend.IsAvailable)
{
    report["status"] = "GPU_UNAVAILABLE";
    report["reason"] = wholeBackend.Capability.FailureReason ?? "DX12 adapter unavailable";
    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var whole = await ExecuteTimedAsync(wholeBackend, source, transform);
var tiled = await ExecuteTimedAsync(tileBackend, source, transform);
var cpuGpu = Metrics(cpu.Pixels, whole.Pixels);
var wholeTiled = Metrics(whole.Pixels, tiled.Pixels);
var cancellation = new CancellationTokenSource();
cancellation.Cancel();
var cancellationObserved = false;
try { await ((IMatchV4PixelBackend)wholeBackend).ExecuteAsync(source, transform, cancellation.Token); }
catch (OperationCanceledException) { cancellationObserved = true; }
var generation = Guid.NewGuid();
var executor = new MatchV4PixelExecutor(new MatchV4CpuPixelBackend(), wholeBackend);
var routed = await executor.ExecuteAsync(source, transform, generation, preferGpu: true);
report["status"] = "GPU_RUNTIME_PASS";
report["whole"] = new { whole.Backend, tileCount = whole.TileCount, uploadMs = whole.UploadTime.TotalMilliseconds, computeMs = whole.ComputeTime.TotalMilliseconds, readbackMs = whole.ReadbackTime.TotalMilliseconds };
report["tiled"] = new { tiled.Backend, tileCount = tiled.TileCount, uploadMs = tiled.UploadTime.TotalMilliseconds, computeMs = tiled.ComputeTime.TotalMilliseconds, readbackMs = tiled.ReadbackTime.TotalMilliseconds };
report["cpuGpuFloat32"] = cpuGpu;
report["wholeVsTiled"] = wholeTiled;
report["metadataPreserved"] = new { whole.Pixels.Orientation, whole.Pixels.WorkingColorSpace, sourceMetadata = whole.Pixels.Metadata is null ? "null" : "present" };
report["cancellationObserved"] = cancellationObserved;
report["routedExecutor"] = new { routed.Backend, routed.UsedCpuFallback, routed.TransformHash, ProcessingGenerationMatches = routed.ProcessingGenerationId == generation };
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

static async Task<MatchV4PixelExecutionResult> ExecuteTimedAsync(IMatchV4PixelBackend backend, HighBitDepthImageBuffer source, MatchV4ResolvedTransform transform)
{
    var clock = Stopwatch.StartNew();
    var result = await backend.ExecuteAsync(source, transform);
    clock.Stop();
    return result with { ComputeTime = result.ComputeTime + (clock.Elapsed - result.UploadTime - result.ComputeTime - result.ReadbackTime) };
}

static object Metrics(HighBitDepthImageBuffer left, HighBitDepthImageBuffer right)
{
    var errors = new double[left.Rgb32.Length];
    var leftSpan = left.Rgb32.Span; var rightSpan = right.Rgb32.Span;
    for (var i = 0; i < errors.Length; i++) errors[i] = Math.Abs(leftSpan[i] - rightSpan[i]);
    Array.Sort(errors);
    var mean = errors.Average();
    return new { mean, median = Quantile(errors, .5), p95 = Quantile(errors, .95), p99 = Quantile(errors, .99), max = errors[^1], pixelCount = left.PixelCount, dimensions = new { left.Width, left.Height } };
}

static double Quantile(double[] values, double q) => values[Math.Clamp((int)Math.Ceiling((values.Length - 1) * q), 0, values.Length - 1)];
