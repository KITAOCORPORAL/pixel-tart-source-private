using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using PixelTart.MatchV4.Dx12;

var root = Get("--root") ?? Environment.GetEnvironmentVariable("PIXEL_TART_RAW_CORPUS_ROOT");
var output = Path.GetFullPath(Get("--output") ?? Path.Combine(Path.GetTempPath(), "PixelTart-ParityForensics-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) { Console.Error.WriteLine("Set --root or PIXEL_TART_RAW_CORPUS_ROOT."); return 2; }
Directory.CreateDirectory(output);
var names = new[] { "TNAN8886.CR3", "DSCF0347.RAF", "DSCF0370.RAF", "LMAN1714.RAF" };
var paths = names.Select(name => Find(Path.GetFullPath(root), name)).Where(p => p is not null).Cast<string>().ToArray();
var decoder = new LibRawDecoder();
var decoded = new List<(string Path, FrozenRawMaster Master)>();
foreach (var path in paths) { try { decoded.Add((path, await new RawMatchTiff16ProductPipeline(decoder).DecodeFrozenMasterAsync(path))); } catch (Exception ex) { Console.Error.WriteLine($"Decode failed {Path.GetFileName(path)}: {ex.Message}"); } }
var rows = new List<object>();
foreach (var (path, master) in decoded)
{
    var reference = decoded.Select(x => x.Master).FirstOrDefault(x => x.SourceSha256 != master.SourceSha256);
    if (reference is null) continue;
    var proxy = HasFlag("--full") ? master.Image : new RawMatchTiff16ProductPipeline(decoder).PreviewMaster(master.Image, 1600);
    foreach (var tile in new[] { 1024 })
    {
        var backend = new Dx12ColorMatchComputeBackend(tile);
        var executor = new MatchV4ProductExecutor(new ReferenceMatchV4Engine(gpu: new MatchV4BackendAdapter(backend)), new MatchV4PixelExecutor(new MatchV4CpuPixelBackend(), backend), backend.Capability);
        var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples: 256, SinkhornIterations: 24, TileSize: tile) { ComputeQuality = ReferenceMatchV4ComputeQuality.Auto };
        var session = executor.CreateSession(master, reference.Image, reference.SourceSha256, settings, MatchV4ExecutionMode.Auto);
        var cpu = await session.PreviewAsync(proxy, .72, false, MatchV4ExecutionMode.Cpu);
        var gpu = await session.PreviewAsync(proxy, .72, false, MatchV4ExecutionMode.Auto);
        rows.Add(new { referenceIdentity = reference.SourceSha256, analysis = executor.Resolve(master, reference.Image, reference.SourceSha256, settings), gpuBackend = gpu.Backend, gpu.UsedCpuFallback, gpu.GpuFailure, metrics = Analyze(Path.GetFileName(path), master, proxy, cpu.Pixels, gpu.Pixels, tile, session.TransformHash, backend.Capability) });
    }
}
var json = Path.Combine(output, "MATCH_V4_GPU_OUTLIER_FORENSICS.json");
await File.WriteAllTextAsync(json, JsonSerializer.Serialize(new { generatedUtc = DateTimeOffset.UtcNow, thresholds = new { oneE6 = 1e-6, oneE5 = 1e-5, oneE4 = 1e-4, oneE3 = 1e-3, oneE2 = 1e-2 }, rows }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(json);
return 0;

static object Analyze(string file, FrozenRawMaster master, HighBitDepthImageBuffer source, HighBitDepthImageBuffer cpu, HighBitDepthImageBuffer gpu, int tile, string transformHash, GpuCapabilityInfo capability)
{
    var total = source.PixelCount; var errors = new double[total * 3]; var outliers = new List<object>(); var mask = new bool[total];
    var c = cpu.Rgb32.Span; var g = gpu.Rgb32.Span; var s = source.Rgb32.Span;
    var counts = new int[5]; var max = 0d; var maxIndex = -1; var sum = 0d;
    for (var p = 0; p < total; p++)
    {
        var pixelMax = 0d;
        for (var ch = 0; ch < 3; ch++) { var e = Math.Abs(c[p * 3 + ch] - g[p * 3 + ch]); errors[p * 3 + ch] = e; pixelMax = Math.Max(pixelMax, e); }
        sum += errors[p * 3] + errors[p * 3 + 1] + errors[p * 3 + 2];
        if (pixelMax > 1e-6) counts[0]++; if (pixelMax > 1e-5) counts[1]++; if (pixelMax > 1e-4) counts[2]++; if (pixelMax > 1e-3) counts[3]++; if (pixelMax > 1e-2) counts[4]++;
        if (pixelMax > 1e-6) mask[p] = true;
        if (pixelMax > max) { max = pixelMax; maxIndex = p; }
        if (pixelMax > 1e-4)
        {
            var x = p % source.Width; var y = p / source.Width; var lab = OklabColorSpace.FromSrgb(s[p * 3], s[p * 3 + 1], s[p * 3 + 2]); var cl = OklabColorSpace.FromSrgb(c[p * 3], c[p * 3 + 1], c[p * 3 + 2]); var gl = OklabColorSpace.FromSrgb(g[p * 3], g[p * 3 + 1], g[p * 3 + 2]);
            outliers.Add(new { x, y, channelErrors = new[] { errors[p * 3], errors[p * 3 + 1], errors[p * 3 + 2] }, cpuRgb = new[] { c[p * 3], c[p * 3 + 1], c[p * 3 + 2] }, gpuRgb = new[] { g[p * 3], g[p * 3 + 1], g[p * 3 + 2] }, sourceRgb = new[] { s[p * 3], s[p * 3 + 1], s[p * 3 + 2] }, cpuOklab = cl, gpuOklab = gl, sourceOklab = lab, deltaE = Math.Sqrt(Math.Pow(cl.L - gl.L, 2) + Math.Pow(cl.A - gl.A, 2) + Math.Pow(cl.B - gl.B, 2)), tile = new { index = (y / tile) * ((source.Width + tile - 1) / tile) + x / tile, localX = x % tile, localY = y % tile, right = Math.Min(x % tile, tile - 1 - x % tile), bottom = Math.Min(y % tile, tile - 1 - y % tile) }, edgeDistance = new { left = x, top = y, right = source.Width - 1 - x, bottom = source.Height - 1 - y }, protection = Protection(lab) });
        }
    }
    var finite = errors.Where(double.IsFinite).ToArray(); Array.Sort(finite); var p95 = finite[(int)Math.Ceiling((finite.Length - 1) * .95)]; var p99 = finite[(int)Math.Ceiling((finite.Length - 1) * .99)];
    var bbox = outliers.Count == 0 ? null : new { left = outliers.Min(o => (int)o.GetType().GetProperty("x")!.GetValue(o)!), top = outliers.Min(o => (int)o.GetType().GetProperty("y")!.GetValue(o)!), right = outliers.Max(o => (int)o.GetType().GetProperty("x")!.GetValue(o)!), bottom = outliers.Max(o => (int)o.GetType().GetProperty("y")!.GetValue(o)!) };
    return new { file, width = source.Width, height = source.Height, megapixels = source.PixelCount / 1_000_000d, tile, tileCount = ((source.Width + tile - 1) / tile) * ((source.Height + tile - 1) / tile), transformHash, capability.AdapterName, capability.MemoryBudget, totalPixels = total, counts = new { over1e6 = counts[0], over1e5 = counts[1], over1e4 = counts[2], over1e3 = counts[3], over1e2 = counts[4] }, ratios = counts.Select(v => v / (double)total).ToArray(), meanChannelAbs = sum / errors.Length, p95, p99, max, maxCoordinate = maxIndex < 0 ? null : new { x = maxIndex % source.Width, y = maxIndex / source.Width }, bbox, clusterCount = Clusters(mask, source.Width, source.Height), outliers = outliers.Take(500).ToArray() };
}
static int Clusters(bool[] mask, int width, int height) { var seen = new bool[mask.Length]; var count = 0; var q = new Queue<int>(); for (var i = 0; i < mask.Length; i++) { if (!mask[i] || seen[i]) continue; count++; seen[i] = true; q.Enqueue(i); while (q.Count > 0) { var p = q.Dequeue(); var x = p % width; var y = p / width; foreach (var n in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) }) { if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= width || n.Item2 >= height) continue; var j = n.Item2 * width + n.Item1; if (mask[j] && !seen[j]) { seen[j] = true; q.Enqueue(j); } } } } return count; }
static object Protection(OklabColor c) { var neutral = Math.Clamp(1 - c.Chroma / .08, 0, 1); var hue = Math.Atan2(c.B, c.A) * 180 / Math.PI; if (hue < 0) hue += 360; var skin = c.L is > .28 and < .9 && hue is > 25 and < 80 && c.Chroma is > .025 and < .22; return new { c.L, c.A, c.B, c.Chroma, hue, neutral, skin, highlight = Math.Clamp((c.L - .82) / .18, 0, 1), shadow = Math.Clamp((.2 - c.L) / .2, 0, 1) }; }
static string? Find(string root, string name) => Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
static bool HasFlag(string key) => Environment.GetCommandLineArgs().Any(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
static string? Get(string key) { var a = Environment.GetCommandLineArgs(); var i = Array.FindIndex(a, x => x.Equals(key, StringComparison.OrdinalIgnoreCase)); return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[i + 1] : null; }
