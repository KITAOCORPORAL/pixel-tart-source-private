using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using PixelTart.MatchV4.Dx12;

var root = Get("--root") ?? Environment.GetEnvironmentVariable("PIXEL_TART_RAW_CORPUS_ROOT");
var output = Path.GetFullPath(Get("--output") ?? Path.Combine(Path.GetTempPath(), "PixelTart-HomeV4-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
{
    Console.Error.WriteLine("Set --root or PIXEL_TART_RAW_CORPUS_ROOT to the external RAW corpus.");
    return 2;
}
root = Path.GetFullPath(root); Directory.CreateDirectory(output);
var candidates = new[]
{
    Find(root, "TNAN8886.CR3"), Find(root, "DSCF0347.RAF"), Find(root, "DSCF0370.RAF"), Find(root, "LMAN1714.RAF")
}.Where(path => path is not null).Cast<string>().ToArray();
var decoder = new LibRawDecoder();
var backend = new Dx12ColorMatchComputeBackend();
var executor = new MatchV4ProductExecutor(new ReferenceMatchV4Engine(gpu: new MatchV4BackendAdapter(backend)), new MatchV4PixelExecutor(new MatchV4CpuPixelBackend(), backend), backend.Capability);
var rows = new List<object>();
FrozenRawMaster? referenceMaster = null;
var decoded = new List<(string Path, FrozenRawMaster Master)>();
foreach (var path in candidates)
{
    try { decoded.Add((path, await new RawMatchTiff16ProductPipeline(decoder).DecodeFrozenMasterAsync(path))); }
    catch (Exception ex)
    {
        rows.Add(new { fixtureId = Id(path, await ShaAsync(path)), file = Path.GetFileName(path), status = "DECODE_BLOCKED", error = ex.GetType().Name + ": " + ex.Message });
    }
}
referenceMaster = decoded.Skip(1).Select(x => x.Master).FirstOrDefault() ?? decoded.Select(x => x.Master).FirstOrDefault();
foreach (var path in candidates)
{
    var predecoded = decoded.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)).Master;
    var referenceForPath = decoded.Where(x => predecoded is null || !string.Equals(x.Master.SourceSha256, predecoded.SourceSha256, StringComparison.OrdinalIgnoreCase)).Select(x => x.Master).FirstOrDefault();
    var row = await RunAsync(path, decoder, executor, referenceForPath, predecoded, output);
    rows.Add(row.Result);
}
var report = new
{
    generatedUtc = DateTimeOffset.UtcNow,
    sourceRootLabel = Path.GetFileName(root),
    hardware = backend.Capability,
    route = "RAW -> ProfessionalDecode RGB48 -> FrozenRawMaster -> MatchV4ProductSession -> DX12 tiled pixel execution -> AtomicTiffWriter",
    gpuProductAcceptance = "HOME_RTX5060TI_EXTERNAL_ONLY",
    fixtures = rows
};
var reportPath = Path.Combine(output, "HOME_MATCH_V4_REAL_RAW_RESULTS.json");
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return 0;

static async Task<(object Result, FrozenRawMaster? Master)> RunAsync(string path, LibRawDecoder decoder, MatchV4ProductExecutor executor, FrozenRawMaster? referenceMaster, FrozenRawMaster? predecoded, string output)
{
    var file = new FileInfo(path); var sha = await ShaAsync(path); var decodeClock = Stopwatch.StartNew();
    FrozenRawMaster master;
    try { master = predecoded ?? await new RawMatchTiff16ProductPipeline(decoder).DecodeFrozenMasterAsync(path); }
    catch (Exception ex) { return (new { fixtureId = Id(path, sha), file = file.Name, sha256 = sha, status = "DECODE_BLOCKED", error = ex.GetType().Name + ": " + ex.Message }, null); }
    decodeClock.Stop();
    if (referenceMaster is null || string.Equals(referenceMaster.SourceSha256, sha, StringComparison.OrdinalIgnoreCase))
        return (new { fixtureId = Id(path, sha), file = file.Name, sha256 = sha, status = "REFERENCE_BLOCKED", error = "No distinct decoded reference fixture was available." }, master);
    var reference = referenceMaster.Image;
    var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 256, SinkhornIterations: 24, TileSize: 1024) { ComputeQuality = ReferenceMatchV4ComputeQuality.Auto };
    var resolveClock = Stopwatch.StartNew();
    var session = executor.CreateSession(master, reference, referenceMaster.SourceSha256, settings, MatchV4ExecutionMode.Auto);
    resolveClock.Stop();
    var proxy = new RawMatchTiff16ProductPipeline(decoder).PreviewMaster(master.Image, 1600);
    var cpuClock = Stopwatch.StartNew(); var cpu = await session.PreviewAsync(proxy, .72, false, MatchV4ExecutionMode.Cpu); cpuClock.Stop();
    var gpuClock = Stopwatch.StartNew(); var gpu = await session.PreviewAsync(proxy, .72, false, MatchV4ExecutionMode.Auto); gpuClock.Stop();
    var parity = Metrics(cpu.Pixels, gpu.Pixels);
    var destination = Path.Combine(output, Id(path, sha) + "-v4.tif");
    var exportClock = Stopwatch.StartNew(); var exported = await session.ExportTiff16Async(destination, .72, false, MatchV4ExecutionMode.Auto); exportClock.Stop();
    return (new
    {
        fixtureId = Id(path, sha), file = file.Name, sha256 = sha, cameraModel = master.Image.Metadata?.CameraModel,
        dimensions = new { master.Width, master.Height }, megapixels = master.Width * master.Height / 1_000_000d,
        decodeMs = decodeClock.Elapsed.TotalMilliseconds, resolveMs = resolveClock.Elapsed.TotalMilliseconds,
        preview = new { cpuMs = cpuClock.Elapsed.TotalMilliseconds, gpuMs = gpuClock.Elapsed.TotalMilliseconds, cpuBackend = cpu.Backend, gpuBackend = gpu.Backend, parity, gpu.UsedCpuFallback, gpuFailure = gpu.GpuFailure },
        export = new { path = "EXTERNAL_ONLY/" + Path.GetFileName(destination), ms = exportClock.Elapsed.TotalMilliseconds, backend = exported.Item1.Backend, usedCpuFallback = exported.Item1.UsedCpuFallback, transformHash = exported.Item1.TransformHash, decodeGeneration = session.DecodeGenerationId, processingGeneration = session.ProcessingGenerationId, tiff16 = exported.Export.BitDepth == TiffBitDepth.Sixteen },
        workingSetMb = Process.GetCurrentProcess().WorkingSet64 / (1024d * 1024d), status = gpu.UsedCpuFallback || !parity.Pass ? "PARTIAL" : "PASS"
    }, master);
}

static MetricsResult Metrics(HighBitDepthImageBuffer left, HighBitDepthImageBuffer right)
{
    if (left.Width != right.Width || left.Height != right.Height) return new(false, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
    var errors = new double[left.Rgb32.Length]; for (var i = 0; i < errors.Length; i++) errors[i] = Math.Abs(left.Rgb32.Span[i] - right.Rgb32.Span[i]); Array.Sort(errors);
    var mean = errors.Average(); var p95 = errors[(int)Math.Ceiling((errors.Length - 1) * .95)]; var p99 = errors[(int)Math.Ceiling((errors.Length - 1) * .99)]; var max = errors[^1];
    return new(mean <= 1e-5 && p95 <= 3e-5 && p99 <= 5e-5 && max <= 1e-4, mean, p95, p99, max);
}

static string? Find(string root, string name) => Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
static async Task<string> ShaAsync(string path) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream)); }
static string Id(string path, string sha) => $"{Path.GetFileNameWithoutExtension(path).ToLowerInvariant()}-{sha[..16].ToLowerInvariant()}";
static string? Get(string key) { var args = Environment.GetCommandLineArgs(); var index = Array.FindIndex(args, value => value.Equals(key, StringComparison.OrdinalIgnoreCase)); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }

readonly record struct MetricsResult(bool Pass, double Mean, double P95, double P99, double Max);
