using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

// This probe deliberately stops at the product's LibRaw boundary. It does not load WPF,
// Color Studio, Match, preview, or TIFF code, so a Fuji result is useful for separating the
// native/wrapper decode from the rest of the product. RAW files and probe output stay outside Git.
var root = Environment.GetEnvironmentVariable("PIXEL_TART_COMPANY_RAW_ROOT");
if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
{
    Console.Error.WriteLine("Set PIXEL_TART_COMPANY_RAW_ROOT to the external real-camera RAW directory.");
    return 2;
}

var output = Environment.GetEnvironmentVariable("PIXEL_TART_LIBRAW_PROBE_OUTPUT")
    ?? Path.Combine(Path.GetTempPath(), "PixelTart-LibRawProbe", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), "matrix.json");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

var files = Directory.EnumerateFiles(Path.GetFullPath(root), "*", SearchOption.AllDirectories)
    .Where(path => RawToJpegDefaults.CandidateRawExtensions.Contains(Path.GetExtension(path)))
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();
var capability = new LibRawDecoder().GetCapability();
var current = new List<object>();
foreach (var path in files)
    current.Add(await ProbeFixtureAsync(path));

var candidateExe = Environment.GetEnvironmentVariable("PIXEL_TART_LIBRAW_CANDIDATE_EXE");
var candidate = File.Exists(candidateExe)
    ? await RunCandidateAsync(candidateExe!, root, output)
    : new
    {
        status = "NOT_AVAILABLE",
        reason = "No side-by-side candidate probe executable was supplied. NuGet has no newer Sdcb.LibRaw Windows runtime in this checkout, and no native compiler/SDK is installed; production was not replaced.",
        executable = candidateExe
    };

var result = new
{
    generatedAt = DateTimeOffset.UtcNow,
    sourceRoot = Path.GetFullPath(root),
    current = new { package = "Sdcb.LibRaw 0.21.1.7", runtime = capability.Version, capability, fixtures = current },
    candidate
};
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(output);
return 0;

async Task<object> ProbeFixtureAsync(string path)
{
    var sourceSha = Convert.ToHexString(await SHA256.HashDataAsync(File.OpenRead(path)));
    var decoder = new LibRawDecoder();
    var same = new List<object>();
    for (var i = 0; i < 20; i++) same.Add(await DecodeObservationAsync(decoder, path, i));
    var fresh = new List<object>();
    for (var i = 0; i < 20; i++) fresh.Add(await DecodeObservationAsync(new LibRawDecoder(), path, i));
    return new { file = path, sha256 = sourceSha, sameDecoder = Summarize(same), newDecoder = Summarize(fresh) };
}

async Task<object> DecodeObservationAsync(LibRawDecoder decoder, string path, int iteration)
{
    try
    {
        var image = await decoder.DecodeAsync(path, new RawToJpegOptions(AutoRotate: false, DecodeMode: RawDecodeMode.ProfessionalDecode));
        var samples = image.Rgb48Pixels ?? throw new InvalidDataException("Professional decode did not return RGB48.");
        var bytes = MemoryMarshal.AsBytes(samples.AsSpan());
        return new { iteration, status = "PASS", width = image.Width, height = image.Height, orientation = image.Metadata.Orientation, sha256 = Convert.ToHexString(SHA256.HashData(bytes)), error = (string?)null };
    }
    catch (Exception ex)
    {
        return new { iteration, status = "FAIL", width = 0, height = 0, orientation = (ushort)0, sha256 = (string?)null, error = ex.Message };
    }
}

static object Summarize(IReadOnlyList<object> observations)
{
    var hashes = observations.Select(item => (string?)item.GetType().GetProperty("sha256")?.GetValue(item)).Where(hash => hash is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var failures = observations.Count(item => string.Equals((string?)item.GetType().GetProperty("status")?.GetValue(item), "FAIL", StringComparison.Ordinal));
    return new { runs = observations, uniqueHashes = hashes.Length, failures };
}

static async Task<object> RunCandidateAsync(string executable, string root, string output)
{
    var psi = new ProcessStartInfo(executable, $"--root \"{root}\" --output \"{output}.candidate.json\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start candidate probe process.");
    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return new { status = process.ExitCode == 0 ? "COMPLETED" : "FAILED", executable, exitCode = process.ExitCode, stdout, stderr };
}
