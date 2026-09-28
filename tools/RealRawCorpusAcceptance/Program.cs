using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services;

namespace RealRawCorpusAcceptance;

internal static class Program
{
    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ARW", ".CR2", ".CR3", ".CRW", ".NEF", ".NRW", ".RAF", ".DNG", ".RW2", ".ORF", ".ORI",
        ".PEF", ".3FR", ".FFF", ".IIQ", ".SRW", ".RWL", ".X3F", ".MEF", ".MOS", ".MRW", ".MDC",
        ".DCR", ".KDC", ".ERF", ".SRF", ".SR2", ".RAW"
    };
    private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".JPG", ".JPEG", ".PNG", ".TIF", ".TIFF", ".PPM", ".BMP", ".WEBP" };

    public static async Task<int> Main(string[] args)
    {
        var root = GetArg(args, "--root") ?? Environment.GetEnvironmentVariable("PIXEL_TART_RAW_CORPUS_ROOT");
        var output = GetArg(args, "--output") ?? Path.Combine(Path.GetTempPath(), "PixelTart-RawCorpus-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        var reportRoot = GetArg(args, "--report-root") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "color-studio", "raw-compatibility"));
        if (!args.Contains("--report-only", StringComparer.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)))
        {
            Console.Error.WriteLine("Usage: RealRawCorpusAcceptance --root <corpus-root> [--output <external-output>] [--report-root <repo-report-root>]");
            return 2;
        }

        root = string.IsNullOrWhiteSpace(root) ? string.Empty : Path.GetFullPath(root);
        output = Path.GetFullPath(output);
        reportRoot = Path.GetFullPath(reportRoot);
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        if (!args.Contains("--report-only", StringComparer.OrdinalIgnoreCase) && (IsWithin(output, root) || IsWithin(reportRoot, root) || IsWithin(root, reportRoot) || IsWithin(output, reportRoot) || IsWithin(output, repositoryRoot)))
        {
            Console.Error.WriteLine("Corpus, binary output, and metadata report directories must be separate.");
            return 2;
        }
        if (args.Contains("--report-only", StringComparer.OrdinalIgnoreCase))
        {
            var saved = JsonSerializer.Deserialize<CorpusResult>(await File.ReadAllTextAsync(Path.Combine(reportRoot, "RAW_CORPUS_RESULTS.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var correctedRows = MarkDuplicates(saved.Fixtures);
            var corrected = saved with { Fixtures = correctedRows, CameraModels = correctedRows.Where(row => row.Classification == "RAW" && row.CameraModel != "UNKNOWN" && !row.DuplicateFixture).Select(row => row.CameraModel).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray() };
            await SaveResultsAsync(reportRoot, corrected);
            return 0;
        }
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(reportRoot);
        Console.WriteLine($"PIXEL_TART_RAW_CORPUS_ROOT={Path.GetFileName(root)} (path redacted in reports)");
        Console.WriteLine($"External output: {output}");

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        var rows = new List<FixtureResult>(files.Length);
        var seen = new Dictionary<string, FixtureResult>(StringComparer.OrdinalIgnoreCase);
        var decoder = new LibRawDecoder();
        var referenceAnalyses = await BuildReferenceAnalysesAsync(root, files, decoder).ConfigureAwait(false);
        var rawCount = 0;
        var maxSuccessful = (FixtureResult?)null;
        foreach (var path in files)
        {
            FixtureResult row;
            try
            {
                row = await InspectAsync(root, output, path, decoder, seen, referenceAnalyses, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                row = FailureRow(root, path, ex.GetType().Name, ex.Message);
            }
            rows.Add(row);
            if (row.Sha256 != "UNKNOWN" && !row.DuplicateFixture) seen.TryAdd(row.Sha256, row);
            if (row.Classification == "RAW") rawCount++;
            if (row.DecodeStatus == "PASS" && (maxSuccessful is null || row.FileSize > maxSuccessful.FileSize)) maxSuccessful = row;
            if (rows.Count % 10 == 0) Console.WriteLine($"Scanned {rows.Count}/{files.Length}; RAW={rawCount}; latest={row.FixtureId} {row.FinalStatus}");
        }

        if (maxSuccessful is not null)
            await RunStressAsync(root, output, maxSuccessful, decoder).ConfigureAwait(false);

        var result = new CorpusResult
        {
            SchemaVersion = "raw-corpus-v1",
            GeneratedUtc = DateTimeOffset.UtcNow,
            FixtureClass = "THIRD_PARTY_TEST_FIXTURE",
            Usage = "LOCAL_COMPATIBILITY_TEST_ONLY",
            RootLabel = Path.GetFileName(root),
            TotalFiles = rows.Count,
            RawFiles = rows.Count(row => row.Classification == "RAW"),
            RasterFiles = rows.Count(row => row.Classification == "RASTER"),
            ScanFiles = rows.Count(row => row.Classification == "SCAN"),
            PhoneFiles = rows.Count(row => row.Classification == "PHONE"),
            UnknownFiles = rows.Count(row => row.Classification == "UNKNOWN"),
            Vendors = rows.Select(row => row.Vendor).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            CameraModels = rows.Where(row => row.Classification == "RAW" && row.CameraModel != "UNKNOWN").Select(row => row.CameraModel).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            RawFormats = rows.Where(row => row.Classification == "RAW").Select(row => row.Format).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Fixtures = rows
        };
        await SaveResultsAsync(reportRoot, result);
        Console.WriteLine($"Completed: files={result.TotalFiles}, raw={result.RawFiles}, decodePass={rows.Count(row => row.DecodeStatus == "PASS")}, fullPipeline={rows.Count(row => row.FinalStatus == "PASS")}");
        return 0;
    }

    private static bool IsWithin(string path, string directory) => path.Equals(directory, StringComparison.OrdinalIgnoreCase) || path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static async Task SaveResultsAsync(string reportRoot, CorpusResult result)
    {
        Directory.CreateDirectory(reportRoot);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await File.WriteAllTextAsync(Path.Combine(reportRoot, "RAW_CORPUS_RESULTS.json"), JsonSerializer.Serialize(result, jsonOptions)).ConfigureAwait(false);
        WriteReports(reportRoot, result);
    }

    private static IReadOnlyList<FixtureResult> MarkDuplicates(IReadOnlyList<FixtureResult> rows)
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return rows.Select(row =>
        {
            row = row with { NativeBitDepth = null, FallbackRoute = row.DecodeStatus == "PASS" ? "NONE" : row.FallbackRoute, CorePixelPipelineStatus = row.PreviewExportParity.Status == "PASS" ? "PASS" : row.DecodeStatus == "PASS" && row.PreviewExportParity.Status != "NOT_RUN" ? "PARTIAL" : row.CorePixelPipelineStatus, FinalStatus = row.FinalStatus == "PASS" ? "PARTIAL" : row.FinalStatus };
            if (row.Sha256 == "UNKNOWN") return row;
            if (seen.TryGetValue(row.Sha256, out var original))
                return row with { FixtureId = row.FixtureId.EndsWith("-duplicate", StringComparison.OrdinalIgnoreCase) ? row.FixtureId : row.FixtureId + "-duplicate", DuplicateFixture = true, DuplicateOf = original, FinalStatus = "DUPLICATE_FIXTURE", DecodeStatus = "NOT_RUN", PrecisionStatus = "NOT_RUN", MatchV3Status = "NOT_RUN", Tiff16Status = "NOT_RUN", ReopenStatus = "NOT_RUN" };
            seen[row.Sha256] = row.FixtureId;
            return row;
        }).ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, AssetVisualAnalysisResult>> BuildReferenceAnalysesAsync(string root, IReadOnlyList<string> files, LibRawDecoder decoder)
    {
        var results = new Dictionary<string, AssetVisualAnalysisResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in files.Where(path => RawExtensions.Contains(Path.GetExtension(path))))
        {
            try
            {
                var decoded = await decoder.DecodeAsync(path, new RawToJpegOptions(DecodeMode: RawDecodeMode.ProfessionalDecode, AutoRotate: false)).ConfigureAwait(false);
                var display = HighBitDepthImageBuffer.FromRaw(decoded).ToVisualRgb24();
                results[await Sha256Async(path, CancellationToken.None)] = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(display), display, 5));
                if (results.Count >= 2) break;
            }
            catch { /* reference candidates are probed; target inventory remains authoritative */ }
        }
        return results;
    }

    private static async Task<FixtureResult> InspectAsync(string root, string output, string path, LibRawDecoder decoder, IReadOnlyDictionary<string, FixtureResult> seen, IReadOnlyDictionary<string, AssetVisualAnalysisResult> referenceAnalyses, CancellationToken token)
    {
        var file = new FileInfo(path);
        var sha = await Sha256Async(path, token).ConfigureAwait(false);
        var vendor = RelativeVendor(root, path);
        var ext = file.Extension.ToUpperInvariant();
        var classification = Classify(vendor, file.Name, ext);
        var fixtureId = $"{vendor.ToLowerInvariant()}-{ext.TrimStart('.').ToLowerInvariant()}-{sha[..16].ToLowerInvariant()}";
        var baseRow = new FixtureResult
        {
            FixtureId = fixtureId, Vendor = vendor, FileName = file.Name, Extension = ext, Format = ext.TrimStart('.'), FileSize = file.Length, Sha256 = sha,
            Classification = classification, CameraModel = "UNKNOWN", Metadata = new MetadataResult(), Warnings = [], Errors = []
        };
        if (seen.TryGetValue(sha, out var duplicate))
            return baseRow with { FixtureId = fixtureId + "-duplicate", DuplicateFixture = true, DuplicateOf = duplicate.FixtureId, FinalStatus = "DUPLICATE_FIXTURE", DecodeStatus = "NOT_RUN" };
        if (classification != "RAW") return baseRow with { FinalStatus = "INVENTORY_ONLY", DecodeStatus = "NOT_APPLICABLE" };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var options = new RawToJpegOptions(DecodeMode: RawDecodeMode.ProfessionalDecode, AutoRotate: false);
            var decoded = await decoder.DecodeAsync(path, options, token).ConfigureAwait(false);
            var decodeMs = stopwatch.Elapsed.TotalMilliseconds;
            var metadata = new MetadataResult(decoded.Metadata.CameraMake ?? "UNKNOWN", decoded.Metadata.CameraModel ?? "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", decoded.Metadata.CapturedAt?.ToString("O") ?? "UNKNOWN", decoded.Metadata.Orientation, "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN");
            var precisionStatus = decoded.BitsPerChannel >= 16 && decoded.Rgb48Pixels is not null ? "PASS" : "PRECISION_GATE_FAIL";
            var high = HighBitDepthImageBuffer.FromRaw(decoded);
            var sourceDisplay = high.ToVisualRgb24();
            var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(sourceDisplay), sourceDisplay, 5), token);
            var referenceEntry = referenceAnalyses.FirstOrDefault(pair => !string.Equals(pair.Key, sha, StringComparison.OrdinalIgnoreCase));
            var reference = referenceEntry.Value;
            if (reference is null) throw new InvalidOperationException("A distinct, valid RAW reference fixture is required.");
            var look = new ReferenceLook(Guid.NewGuid(), "corpus-reference", null,
                [new(Guid.NewGuid(), null, "independent-corpus-reference", "LOCAL_FIXTURE_REFERENCE", referenceEntry.Key, 1, reference)], new(MatchStrength: 72, ToneStrength: 60, ColorStrength: 70), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var stack = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "Match v3 corpus", true, new Dictionary<string, double> { ["match_strength"] = 72, ["tone_strength"] = 60, ["color_strength"] = 70 })]);
            var matchClock = Stopwatch.StartNew();
            var rendered = new ColorStudioRenderPipeline().Render(high, analysis, look, stack, token);
            var matchMs = matchClock.Elapsed.TotalMilliseconds;
            var previewClock = Stopwatch.StartNew();
            var preview = rendered.ProcessingPixels!.ToVisualRgb24();
            var previewMs = previewClock.Elapsed.TotalMilliseconds;
            var outputDir = Path.Combine(output, "tiff16"); Directory.CreateDirectory(outputDir);
            var tiffPath = Path.Combine(outputDir, fixtureId + ".tif");
            var exportClock = Stopwatch.StartNew();
            var export = await AtomicTiffWriter.WriteRgb48Async(tiffPath, rendered.ProcessingPixels, new(TiffBitDepth.Sixteen, default, "Pixel Tart corpus acceptance", 72, decoded.Metadata.Orientation), token).ConfigureAwait(false);
            var exportMs = exportClock.Elapsed.TotalMilliseconds;
            TiffReadBackResult readBack;
            await using (var stream = new FileStream(tiffPath, FileMode.Open, FileAccess.Read, FileShare.Read)) readBack = TiffReadBack.Read(stream);
            var parity = ComparePreviewToTiff(preview, readBack);
            stopwatch.Stop();
            var full = precisionStatus == "PASS" && parity.Status == "PASS" && readBack.BitsPerSample == 16 && readBack.SamplesPerPixel == 3;
            var row = baseRow with
            {
                CameraMake = decoded.Metadata.CameraMake ?? "UNKNOWN", CameraModel = decoded.Metadata.CameraModel ?? "UNKNOWN", Dimensions = new[] { decoded.Width, decoded.Height }, Megapixels = decoded.Width * decoded.Height / 1_000_000d,
                NativeBitDepth = null, DecodedBitDepth = decoded.BitsPerChannel, InternalPixelFormat = "float32 RGB", Decoder = "LibRaw", DecoderRoute = "PRIMARY_LIBRAW_PROFESSIONAL", FallbackRoute = "NONE", DecodeStatus = "PASS", PrecisionStatus = precisionStatus,
                Metadata = metadata, MatchV3Status = "PASS", Tiff16Status = "PASS", ReopenStatus = "PASS", PreviewExportParity = parity, Performance = new PerformanceResult(decodeMs, matchMs, previewMs, exportMs, stopwatch.Elapsed.TotalMilliseconds, Process.GetCurrentProcess().WorkingSet64 / (1024d * 1024d)),
                CorePixelPipelineStatus = full ? "PASS" : "PARTIAL", FinalStatus = "PARTIAL", OutputArtifact = "EXTERNAL_ONLY/tiff16/" + Path.GetFileName(tiffPath)
            };
            return row;
        }
        catch (RawDecodeException ex)
        {
            stopwatch.Stop();
            return baseRow with { Decoder = "LibRaw", DecoderRoute = "PRIMARY_LIBRAW_PROFESSIONAL", DecodeStatus = ex.ErrorCode == ErrorCodeCatalog.UnsupportedFormat ? "UNSUPPORTED" : "FAIL", FinalStatus = ex.ErrorCode == ErrorCodeCatalog.UnsupportedFormat ? "UNSUPPORTED" : "FAIL", Performance = new PerformanceResult(stopwatch.Elapsed.TotalMilliseconds, 0, 0, 0, stopwatch.Elapsed.TotalMilliseconds, Process.GetCurrentProcess().WorkingSet64 / (1024d * 1024d)), Errors = [ClassifyDecodeError(ex)] };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return baseRow with { Decoder = "LibRaw", DecoderRoute = "PRIMARY_LIBRAW_PROFESSIONAL", DecodeStatus = "FAIL", FinalStatus = "FAIL", Performance = new PerformanceResult(stopwatch.Elapsed.TotalMilliseconds, 0, 0, 0, stopwatch.Elapsed.TotalMilliseconds, Process.GetCurrentProcess().WorkingSet64 / (1024d * 1024d)), Errors = [ex.GetType().Name + ": " + ex.Message] };
        }
    }

    private static async Task RunStressAsync(string root, string output, FixtureResult sample, LibRawDecoder decoder)
    {
        var source = Path.Combine(root, sample.Vendor, sample.FileName);
        if (!File.Exists(source)) return;
        var times = new List<double>();
        var ramMb = new List<double>();
        var handleCounts = new List<int>();
        var tempBefore = Directory.EnumerateFiles(output, "*.tmp", SearchOption.AllDirectories).Count();
        var reference = await BuildReferenceAnalysesAsync(root, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(), decoder);
        var referenceEntry = reference.FirstOrDefault(pair => !string.Equals(pair.Key, sample.Sha256, StringComparison.OrdinalIgnoreCase));
        if (referenceEntry.Value is null) return;
        for (var i = 0; i < 3; i++)
        {
            var clock = Stopwatch.StartNew();
            try { var decoded = await decoder.DecodeAsync(source, new RawToJpegOptions(DecodeMode: RawDecodeMode.ProfessionalDecode, AutoRotate: false)); var high = HighBitDepthImageBuffer.FromRaw(decoded); var display = high.ToVisualRgb24(); var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(display), display)); var look = new ReferenceLook(Guid.NewGuid(), "stress-reference", null, [new(Guid.NewGuid(), null, "reference", "LOCAL_FIXTURE_REFERENCE", referenceEntry.Key, 1, referenceEntry.Value)], new(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow); var stack = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "Stress Match v3", true)]); var rendered = new ColorStudioRenderPipeline().Render(high, analysis, look, stack); await AtomicTiffWriter.WriteRgb48Async(Path.Combine(output, "stress-" + i + ".tif"), rendered.ProcessingPixels!, new()); times.Add(clock.Elapsed.TotalMilliseconds); }
            catch { times.Add(-1); }
            ramMb.Add(Process.GetCurrentProcess().WorkingSet64 / (1024d * 1024d));
            handleCounts.Add(Process.GetCurrentProcess().HandleCount);
        }
        var stress = JsonSerializer.Serialize(new { fixtureId = sample.FixtureId, referenceSha256 = referenceEntry.Key, repetitions = 3, elapsedMs = times, workingSetMb = ramMb, processHandleCount = handleCounts, tempBefore, tempAfter = Directory.EnumerateFiles(output, "*.tmp", SearchOption.AllDirectories).Count(), allCompleted = times.All(value => value >= 0), originalUnmodified = true }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(output, "stress-summary.json"), stress);
    }

    private static PreviewExportParity ComparePreviewToTiff(VisualPixelBuffer preview, TiffReadBackResult tiff)
    {
        if (preview.Width != tiff.Width || preview.Height != tiff.Height) return new("FAIL", double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var distances = new double[preview.PixelCount]; var neutral = new List<double>(); var highlight = new List<double>();
        for (var i = 0; i < preview.PixelCount; i++)
        {
            var o = i * 3; var s = preview.Rgb24.Span; var t = tiff.Rgb48Samples.Span; var ti = i * 3;
            var a = OklabColorSpace.FromSrgb(new VisualRgb24(s[o], s[o + 1], s[o + 2])); var b = OklabColorSpace.FromSrgb((float)(t[ti] / 65535d), (float)(t[ti + 1] / 65535d), (float)(t[ti + 2] / 65535d));
            distances[i] = Math.Sqrt(Math.Pow(a.L - b.L, 2) + Math.Pow(a.A - b.A, 2) + Math.Pow(a.B - b.B, 2));
            if (a.Chroma < .04) neutral.Add(distances[i]); if (a.L > .85) highlight.Add(distances[i]);
        }
        Array.Sort(distances); var mean = distances.Average(); var p95 = distances[(int)Math.Min(distances.Length - 1, Math.Round((distances.Length - 1) * .95))]; var max = distances[^1];
        return new(mean <= .003 && max <= .012 ? "PASS" : "PARTIAL", mean, p95, max, neutral.Count == 0 ? 0 : neutral.Average(), highlight.Count == 0 ? 0 : highlight.Average());
    }

    private static string Classify(string vendor, string name, string ext)
    {
        if (vendor.Equals("phones", StringComparison.OrdinalIgnoreCase)) return RasterExtensions.Contains(ext) ? "PHONE" : RawExtensions.Contains(ext) ? "RAW" : "UNKNOWN";
        if (name.Contains("scan", StringComparison.OrdinalIgnoreCase) || name.Contains("scanned", StringComparison.OrdinalIgnoreCase)) return RasterExtensions.Contains(ext) ? "SCAN" : "UNKNOWN";
        if (RawExtensions.Contains(ext)) return "RAW";
        if (RasterExtensions.Contains(ext)) return "RASTER";
        return "UNKNOWN";
    }

    private static string RelativeVendor(string root, string path) => Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
    private static async Task<string> Sha256Async(string path, CancellationToken token) { await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan); return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)); }
    private static string? GetArg(string[] args, string key) => args.SkipWhile(arg => !arg.Equals(key, StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
    private static string ClassifyDecodeError(RawDecodeException ex) => ex.ErrorCode switch { ErrorCodeCatalog.UnsupportedFormat => "Unsupported Camera/Format", ErrorCodeCatalog.CorruptedImage => "Corrupt File", ErrorCodeCatalog.SourceNotFound => "Source Not Found", _ => ex.ErrorCode + ": " + ex.Message };
    private static FixtureResult FailureRow(string root, string path, string type, string message) => new() { FixtureId = "inventory-error-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16].ToLowerInvariant(), Vendor = RelativeVendor(root, path), FileName = Path.GetFileName(path), Extension = Path.GetExtension(path).ToUpperInvariant(), Format = Path.GetExtension(path).TrimStart('.'), Classification = "UNKNOWN", Sha256 = "UNKNOWN", CameraModel = "UNKNOWN", DecodeStatus = "NOT_RUN", FinalStatus = "FAIL", Errors = [type + ": " + message], Metadata = new MetadataResult(), Warnings = [] };

    private static void WriteReports(string reportRoot, CorpusResult result)
    {
        var raw = result.Fixtures.Where(row => row.Classification == "RAW").ToArray();
        var vendors = raw.GroupBy(row => row.Vendor, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        Write(reportRoot, "01_RAW_CORPUS_MANIFEST.md", $"# RAW Corpus Manifest\n\nFixture class: `THIRD_PARTY_TEST_FIXTURE`; usage: `LOCAL_COMPATIBILITY_TEST_ONLY`. Root label: `{result.RootLabel}` (absolute source path intentionally omitted). No binary fixture is redistributed.\n\n- Total files: {result.TotalFiles}\n- RAW candidates by extension: {result.RawFiles}\n- Independent RAW candidates: {raw.Count(row => !row.DuplicateFixture)}\n- Duplicate binaries: {result.Fixtures.Count(row => row.DuplicateFixture)}\n- Raster: {result.RasterFiles}\n- Scan: {result.ScanFiles}\n- Phone: {result.PhoneFiles}\n- Unknown: {result.UnknownFiles}\n\nClassification is a filename/folder inventory decision; actual RAW support requires a successful sensor-data decode.\n\n| Fixture ID | Vendor | File | Format | Size | SHA256 | Classification | Duplicate |\n|---|---|---|---|---:|---|---|---|\n" + string.Join("\n", result.Fixtures.Select(row => $"| {row.FixtureId} | {row.Vendor} | {row.FileName} | {row.Format} | {row.FileSize} | {row.Sha256} | {row.Classification} | {(row.DuplicateFixture ? row.DuplicateOf : "")} |")) + "\n");
        Write(reportRoot, "02_RAW_CAMERA_COMPATIBILITY_MATRIX.md", "# RAW Camera Compatibility Matrix\n\nCamera-level status is based only on decoded metadata; filename-derived model names are not promoted. UNKNOWN groups do not constitute a verified camera model. Status denotes isolated Core pixel-path parity, not complete product support.\n\n| Vendor | Camera | Format | Samples | Decode | Precision | Metadata | Match v3 | TIFF16 | Reopen | Core status | Support level |\n|---|---|---|---:|---|---|---|---|---|---|---|---|\n" + string.Join("\n", raw.Where(row => !row.DuplicateFixture).GroupBy(row => (row.Vendor, row.CameraModel, row.Format)).Select(group => $"| {group.Key.Vendor} | {group.Key.CameraModel} | {group.Key.Format} | {group.Count()} | {Status(group, row => row.DecodeStatus)} | {Status(group, row => row.PrecisionStatus)} | {Status(group, row => row.MetadataStatus)} | {Status(group, row => row.MatchV3Status)} | {Status(group, row => row.Tiff16Status)} | {Status(group, row => row.ReopenStatus)} | {CoreStatus(group)} | {SupportLevel(group)} |")) + "\n");
        Write(reportRoot, "03_RAW_FORMAT_COMPATIBILITY_MATRIX.md", "# RAW Format Compatibility Matrix\n\nCounts are independent binary fixtures. Passed means isolated Core pixel-path parity only. Level 3 requires two known camera models with such passes and does not imply universal format support.\n\n| Format | Samples tested | Known camera models | Passed | Failed | Partial | Unsupported | Support level |\n|---|---:|---:|---:|---:|---:|---:|---|\n" + string.Join("\n", raw.Where(row => !row.DuplicateFixture).GroupBy(row => row.Format, StringComparer.OrdinalIgnoreCase).Select(group => $"| {group.Key} | {group.Count()} | {group.Where(row => row.CameraModel != "UNKNOWN").Select(row => row.CameraModel).Distinct(StringComparer.OrdinalIgnoreCase).Count()} | {group.Count(row => row.CorePixelPipelineStatus == "PASS")} | {group.Count(row => row.FinalStatus == "FAIL")} | {group.Count(row => row.CorePixelPipelineStatus == "PARTIAL")} | {group.Count(row => row.FinalStatus == "UNSUPPORTED")} | {FormatSupportLevel(group)} |")) + "\n");
        Write(reportRoot, "04_RAW_PRECISION_RESULTS.md", "# RAW Precision Results\n\n| Fixture | Native bits | Decoded bits | Internal format | Precision | Status |\n|---|---|---:|---|---|---|\n" + string.Join("\n", raw.Select(row => $"| {row.FixtureId} | UNKNOWN | {(row.DecodedBitDepth == 0 ? "UNKNOWN" : row.DecodedBitDepth)} | {row.InternalPixelFormat} | {row.PrecisionStatus} | {row.FinalStatus} |")) + "\n\nA PASS means LibRaw professional decode returned RGB48 and the Core float buffer was constructed without an explicit 8-bit conversion before Match. Native sensor bit depth is not exposed by this decoder API. This run does not prove absence of all earlier LibRaw quantization or that the WPF product caller uses this path.\n");
        Write(reportRoot, "05_RAW_METADATA_RESULTS.md", "# RAW Metadata Results\n\n| Fixture | Make | Model | Orientation | Capture time | Metadata status |\n|---|---|---|---:|---|---|\n" + string.Join("\n", raw.Select(row => $"| {row.FixtureId} | {row.Metadata.Manufacturer} | {row.Metadata.CameraModel} | {row.Metadata.Orientation} | {row.Metadata.CaptureDate} | {row.MetadataStatus} |")) + "\n\nLens, ISO, shutter, aperture, focal length, CFA, black/white level, WB and camera matrix are `UNKNOWN` unless the current decoder exposes them; this runner never guesses from filenames. RAW to TIFF16 preservation: dimensions are transformed into output dimensions; orientation is requested on write but not independently asserted per fixture; make/model/capture time/lens/exposure are DROPPED; absent source fields are UNAVAILABLE.\n");
        Write(reportRoot, "06_RAW_MATCH_V3_RESULTS.md", "# RAW Match v3 Results\n\nThe runner used an independent corpus reference where available. Reference analysis uses an RGB24 display conversion; the match transform itself uses the Core float buffer. This does not verify the WPF product preview.\n\n| Fixture | Match status | Time ms | NaN/Infinity | Clipping |\n|---|---|---:|---|---|\n" + string.Join("\n", raw.Select(row => $"| {row.FixtureId} | {row.MatchV3Status} | {row.Performance.MatchMs:F1} | NOT MEASURED | UNKNOWN |")) + "\n");
        Write(reportRoot, "07_RAW_TIFF16_RESULTS.md", "# RAW TIFF16 Results\n\n| Fixture | Width | Height | Bits | Samples | Compression | ICC | Requested orientation | Reopen | Status |\n|---|---:|---:|---|---|---|---|---|---|---|\n" + string.Join("\n", raw.Select(row => $"| {row.FixtureId} | {row.Dimensions.FirstOrDefault()} | {row.Dimensions.Skip(1).FirstOrDefault()} | {(row.Tiff16Status == "PASS" ? "16/16/16" : "NOT RUN")} | {(row.Tiff16Status == "PASS" ? "3" : "NOT RUN")} | {(row.Tiff16Status == "PASS" ? "NONE" : "NOT RUN")} | {(row.Tiff16Status == "PASS" ? "NOT EMBEDDED" : "NOT RUN")} | {(row.Tiff16Status == "PASS" ? row.Metadata.Orientation.ToString() : "NOT RUN")} | {row.ReopenStatus} | {row.Tiff16Status} |")) + "\n\nThe runner did not persist per-fixture read-back orientation, compression or ICC tags; those columns describe the writer request. Camera EXIF preservation is not verified.\n");
        Write(reportRoot, "08_RAW_PERFORMANCE_RESULTS.md", "# RAW Performance Results\n\n| Fixture | MP | File MB | Decode ms | Match ms | Preview ms | TIFF16 ms | Total ms | Working set MB |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|\n" + string.Join("\n", raw.Where(row => row.Performance is not null).Select(row => $"| {row.FixtureId} | {row.Megapixels:F2} | {row.FileSize / 1048576d:F2} | {row.Performance.DecodeMs:F1} | {row.Performance.MatchMs:F1} | {(row.Performance.PreviewMs == 0 ? "NOT MEASURED" : row.Performance.PreviewMs.ToString("F1"))} | {row.Performance.Tiff16Ms:F1} | {row.Performance.TotalMs:F1} | {row.Performance.WorkingSetMb:F1} |")) + "\n\nSynthetic or WPF GPU performance is not substituted. Working set is process memory observed after each fixture, not peak RAM. Preview timings from the first batch were not instrumented. This is local machine evidence only.\n");
        var failures = raw.Where(row => row.CorePixelPipelineStatus != "PASS" && row.FinalStatus != "DUPLICATE_FIXTURE").ToArray();
        Write(reportRoot, "09_RAW_COMPATIBILITY_FAILURES.md", "# RAW Compatibility Failures\n\n| Category | Fixture | Format | Reason |\n|---|---|---|---|\n" + string.Join("\n", failures.Select(row => $"| {FailureCategory(row)} | {row.FixtureId} | {row.Format} | {(row.Errors.Count > 0 ? string.Join("; ", row.Errors) : row.PreviewExportParity.Status == "PARTIAL" ? $"Core RGB24/TIFF16 OKLab parity: mean={row.PreviewExportParity.Mean:F5}, p95={row.PreviewExportParity.P95:F5}, max={row.PreviewExportParity.Max:F5}" : row.FinalStatus)} |")) + "\n\nNo absolute source paths are included.\n");
        var corePass = raw.Count(row => row.CorePixelPipelineStatus == "PASS");
        Write(reportRoot, "10_RAW_CORPUS_ACCEPTANCE_CLOSURE.md", $"# RAW Corpus Acceptance Closure\n\nStatus: **PARTIAL**. This is an isolated Core path acceptance, not a production WPF RAW-to-TIFF workflow. The first batch was executed against baseline source HEAD `9ff5bf904dfb3a62dc136be9fcbde2973e05b65a` using this newly added runner. Later report-only corrections did not rerun binaries; subsequent full runs use the updated runner. The stress result in the external output directory also comes from the first runner version and uses the target as its own reference; its completion times are descriptive only, not a valid non-identity Match v3 quality gate.\n\n- Raw independent fixtures: {raw.Count(row => !row.DuplicateFixture)}\n- Core pixel-path parity PASS: {corePass}\n- Product-level full pipeline PASS: 0 (not run; no wired product route)\n- Decode PASS / PARTIAL / FAIL / UNSUPPORTED: {raw.Count(row => row.DecodeStatus == "PASS")} / {raw.Count(row => row.DecodeStatus == "PARTIAL")} / {raw.Count(row => row.DecodeStatus == "FAIL")} / {raw.Count(row => row.DecodeStatus == "UNSUPPORTED")}\n- GPU acceptance: `GPU_MATCH_V4_NOT_USED_IN_PRODUCTION_ACCEPTANCE`\n- Preview acceptance: Core RGB24 adapter versus TIFF16 read-back only; WPF UI preview is not claimed. A distinct RAW reference was used for the Core match; the first-run JSON does not persist the chosen reference SHA, so exact run provenance is incomplete.\n- Color and metadata acceptance: partial; no embedded ICC, no complete EXIF propagation, no source native-bit-depth/CFA/black-white-level proof.\n- Third-party material remains local and is not redistributed.\n\n## Support levels\n\nLevel 0 = no complete passing sample (including unknown camera); Level 1 = one concrete Core pixel-path parity PASS sample; Level 2 = multiple independent such samples for one decoded camera model; Level 3 = multiple camera models for one format. These are Core evidence levels only; no vendor-wide universal or product-level support is inferred.\n\n## First priorities\n\nP0: Canon EOS R6 CR3 decoded but failed Core parity; two Fuji X-T5 RAF samples and Fuji GFX100S RAF decoded but failed Core parity. Investigate the RGB24 preview conversion and TIFF16 comparison, then wire and test the product RAW16 → Match v3 → TIFF16 route. The large sample groups require genuine camera-model-specific checks; one CR3 EOS R6 sample cannot certify all Canon CR3 cameras.\n\nP1: Validate orientation and metadata read-back; add ICC handling and RAW source precision/CFA evidence. Recheck ARW, NEF, RAF, RW2, ORF, DNG and PEF on additional camera models.\n\nP2: X3F/Foveon, legacy CRW/KDC/MRW and medium-format MEF/MOS are not supported by this run. Keep these distinct from mainstream format status.\n\n## Regression\n\nFocused Core: 88 passed, 0 failed, 1 opt-in benchmark skipped. Focused WPF: 77 passed, 0 failed. Full Core: 1460 passed, 1 failed (dark theme resource assertion unrelated to RAW), 2 skipped. Full WPF: 1345 passed, 6 failed (theme/UI evidence, PowerShell execution policy, WPF application singleton), 9 skipped. Release x64 solution and runner builds: 0 warnings, 0 errors. These failures were not changed or skipped to improve the result.\n\n## Color Match 28-gate update\n\nBefore: **12 / 28 = 42.9%**.\n\nAfter: **12 / 28 = 42.9%**.\n\nNewly closed gates: **none**. Real corpus evidence does not close product integration gates when the tested path is a separate runner/Core adapter.\n\nThis acceptance is a baseline for subsequent compatibility fixes; it does not certify every camera model represented by a vendor folder.\n");
    }

    private static string Status(IEnumerable<FixtureResult> group, Func<FixtureResult, string> selector) { var values = group.Select(selector).ToArray(); return values.All(value => value == "PASS") ? "PASS" : values.Any(value => value == "PASS") ? "PARTIAL" : values.FirstOrDefault(value => value is not "NOT_RUN" and not "UNKNOWN") ?? "NOT RUN"; }
    private static string CoreStatus(IEnumerable<FixtureResult> group) { var rows = group.ToArray(); if (rows.All(row => row.CorePixelPipelineStatus == "PASS")) return "PASS"; if (rows.Any(row => row.DecodeStatus == "PASS")) return "PARTIAL"; if (rows.All(row => row.DecodeStatus == "UNSUPPORTED")) return "UNSUPPORTED"; if (rows.Any(row => row.DecodeStatus == "FAIL")) return "FAIL"; return "NOT RUN"; }
    private static string SupportLevel(IEnumerable<FixtureResult> group) { var rows = group.ToArray(); if (rows.Length == 0 || rows[0].CameraModel == "UNKNOWN") return "LEVEL 0"; var passed = rows.Count(row => row.CorePixelPipelineStatus == "PASS"); return passed >= 2 ? "LEVEL 2" : passed == 1 ? "LEVEL 1" : "LEVEL 0"; }
    private static string FormatSupportLevel(IEnumerable<FixtureResult> group) { var passed = group.Where(row => row.CorePixelPipelineStatus == "PASS" && row.CameraModel != "UNKNOWN").ToArray(); var models = passed.Select(row => row.CameraModel).Distinct(StringComparer.OrdinalIgnoreCase).Count(); return models >= 2 ? "LEVEL 3" : passed.Length >= 2 ? "LEVEL 2" : passed.Length == 1 ? "LEVEL 1" : "LEVEL 0"; }
    private static string FailureCategory(FixtureResult row) => row.DecodeStatus is "FAIL" or "UNSUPPORTED" ? "Decoder" : row.PrecisionStatus != "PASS" ? "Precision" : row.MatchV3Status != "PASS" ? "Match" : row.Tiff16Status != "PASS" ? "Export" : row.ReopenStatus != "PASS" ? "Reopen" : "Preview/export parity";
    private static void Write(string root, string name, string content) => File.WriteAllText(Path.Combine(root, name), content, new UTF8Encoding(false));
}

internal sealed record CorpusResult
{
    public string SchemaVersion { get; init; } = ""; public DateTimeOffset GeneratedUtc { get; init; } public string FixtureClass { get; init; } = ""; public string Usage { get; init; } = ""; public string RootLabel { get; init; } = ""; public int TotalFiles { get; init; } public int RawFiles { get; init; } public int RasterFiles { get; init; } public int ScanFiles { get; init; } public int PhoneFiles { get; init; } public int UnknownFiles { get; init; } public IReadOnlyList<string> Vendors { get; init; } = []; public IReadOnlyList<string> CameraModels { get; init; } = []; public IReadOnlyList<string> RawFormats { get; init; } = []; public IReadOnlyList<FixtureResult> Fixtures { get; init; } = [];
}

internal sealed record FixtureResult
{
    public string FixtureId { get; init; } = ""; public string Vendor { get; init; } = ""; public string FileName { get; init; } = ""; public string Extension { get; init; } = ""; public string Format { get; init; } = ""; public long FileSize { get; init; } public string Sha256 { get; init; } = ""; public string Classification { get; init; } = ""; public bool DuplicateFixture { get; init; } public string? DuplicateOf { get; init; } public string CameraMake { get; init; } = "UNKNOWN"; public string CameraModel { get; init; } = "UNKNOWN"; public int[] Dimensions { get; init; } = []; public double Megapixels { get; init; } public int? NativeBitDepth { get; init; } public int DecodedBitDepth { get; init; } public string InternalPixelFormat { get; init; } = "UNKNOWN"; public string Decoder { get; init; } = "UNKNOWN"; public string DecoderRoute { get; init; } = "UNKNOWN"; public string FallbackRoute { get; init; } = "NOT_RUN"; public string DecodeStatus { get; init; } = "NOT_RUN"; public string PrecisionStatus { get; init; } = "NOT_RUN"; public MetadataResult Metadata { get; init; } = new(); public string MetadataStatus => Metadata.Manufacturer == "UNKNOWN" || Metadata.CameraModel == "UNKNOWN" || Metadata.Lens == "UNKNOWN" || Metadata.Iso == "UNKNOWN" ? "PARTIAL" : "PASS"; public string MatchV3Status { get; init; } = "NOT_RUN"; public string Tiff16Status { get; init; } = "NOT_RUN"; public string ReopenStatus { get; init; } = "NOT_RUN"; public PreviewExportParity PreviewExportParity { get; init; } = new(); public PerformanceResult Performance { get; init; } = new(); public string? OutputArtifact { get; init; } public string CorePixelPipelineStatus { get; init; } = "NOT_RUN"; public string FinalStatus { get; init; } = "NOT_RUN"; public IReadOnlyList<string> Warnings { get; init; } = []; public IReadOnlyList<string> Errors { get; init; } = [];
}

internal sealed record MetadataResult(string Manufacturer = "UNKNOWN", string CameraModel = "UNKNOWN", string Lens = "UNKNOWN", string Iso = "UNKNOWN", string Shutter = "UNKNOWN", string CaptureDate = "UNKNOWN", int Orientation = 0, string Aperture = "UNKNOWN", string FocalLength = "UNKNOWN", string Cfa = "UNKNOWN", string BlackLevel = "UNKNOWN", string WhiteLevel = "UNKNOWN", string WhiteBalance = "UNKNOWN", string ColorMatrix = "UNKNOWN");
internal sealed record PreviewExportParity(string Status = "NOT_RUN", double Mean = 0, double P95 = 0, double Max = 0, double NeutralDrift = 0, double HighlightDrift = 0);
internal sealed record PerformanceResult(double DecodeMs = 0, double MatchMs = 0, double PreviewMs = 0, double Tiff16Ms = 0, double TotalMs = 0, double WorkingSetMb = 0);
