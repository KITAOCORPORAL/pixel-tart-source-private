using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

if (args.Length != 2) throw new ArgumentException("Usage: ColorBaseline <explicit-raw-path> <expected-sha256>; output is stdout only");
string path = Path.GetFullPath(args[0]);
string Hash() { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
string before = Hash();
if (!string.Equals(before, args[1], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Source hash mismatch; decode refused");
using var process = Process.GetCurrentProcess();
var stopwatch = Stopwatch.StartNew();
var stages = new List<string>();
var master = await new ProfessionalRawDecoder(s => stages.Add(s)).DecodeAsync(path, new ProfessionalRawDefinition());
stopwatch.Stop();
try {
    var pixels = master.Image.Rgb32.ToArray();
    long[] zeros = new long[3], maximum = new long[3];
    for (int i = 0; i < pixels.Length; i++) { if (pixels[i] == 0) zeros[i % 3]++; if (pixels[i] == 1) maximum[i % 3]++; }
    string after = Hash();
    if (after != before) throw new IOException("Source changed during decode");
    Console.WriteLine(JsonSerializer.Serialize(new { version = "ColorBaselineProbeV1", sourceSha256 = before, sourceUnchanged = true, master.Image.Width, master.Image.Height, contract = master.Contract, stages, outputZeroByChannel = zeros, outputMaxByChannel = maximum, channelSamples = pixels.Length / 3, decodeMilliseconds = stopwatch.Elapsed.TotalMilliseconds, processPeakWorkingSetBytes = process.PeakWorkingSet64, limitations = "Final RGB16 converted to float only; no sensor saturation attribution; timings include hash inside decoder and process peak is not incremental." }, new JsonSerializerOptions { WriteIndented = true }));
} finally { master.Image.Dispose(); }
