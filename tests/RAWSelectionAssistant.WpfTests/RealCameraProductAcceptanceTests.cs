using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

/// <summary>Opt-in, real-camera product command gate. Never runs against private files in ordinary CI.</summary>
[TestClass]
public sealed class RealCameraProductAcceptanceTests
{
    private const double MeanLimit = .003;
    private const double TailLimit = .012;

    [TestMethod]
    public async Task CompanyRealRawProductAcceptanceGate()
    {
        var fixtureRoot = Environment.GetEnvironmentVariable("PIXEL_TART_COMPANY_RAW_ROOT");
        var evidenceRoot = Environment.GetEnvironmentVariable("PIXEL_TART_COMPANY_RAW_EVIDENCE_ROOT");
        if (string.IsNullOrWhiteSpace(fixtureRoot) || !Directory.Exists(fixtureRoot))
        {
            Assert.Inconclusive("NOT RUN — real camera fixture directory unavailable; set PIXEL_TART_COMPANY_RAW_ROOT.");
            return;
        }
        if (string.IsNullOrWhiteSpace(evidenceRoot))
        {
            Assert.Inconclusive("NOT RUN — explicitly set an external PIXEL_TART_COMPANY_RAW_EVIDENCE_ROOT.");
            return;
        }

        fixtureRoot = Path.GetFullPath(fixtureRoot);
        evidenceRoot = Path.GetFullPath(evidenceRoot);
        Assert.IsFalse(evidenceRoot.StartsWith(fixtureRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(IsInsideGitCheckout(fixtureRoot), "Private RAW binaries must live outside the checkout.");
        Assert.IsFalse(IsInsideGitCheckout(evidenceRoot), "Private TIFF/PNG results must live outside the checkout.");
        Directory.CreateDirectory(evidenceRoot);
        var fixtures = Directory.EnumerateFiles(fixtureRoot, "*", SearchOption.AllDirectories)
            .Where(RawMatchTiff16ProductPipeline.IsRaw).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        if (fixtures.Length < 2) { Assert.Inconclusive("NOT RUN — at least two independent RAW fixtures required."); return; }

        var rows = new List<GateRow>();
        var rowByPath = new Dictionary<string, GateRow>(StringComparer.OrdinalIgnoreCase);
        var decoder = new LibRawDecoder();
        var pipeline = new RawMatchTiff16ProductPipeline(decoder);
        var decodable = new List<string>();
        foreach (var path in fixtures)
        {
            var row = new GateRow(Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            rows.Add(row);
            rowByPath.Add(path, row);
            try
            {
                var watch = Stopwatch.StartNew();
                var image = await pipeline.DecodeMasterAsync(path);
                row.DecodeMs = watch.Elapsed.TotalMilliseconds;
                row.Width = image.Width; row.Height = image.Height;
                row.Camera = image.Metadata?.CameraModel;
                row.Precision = image.SourceBitDepth == "16" && image.Rgb32.Length == image.Width * image.Height * 3;
                if (!row.Precision) { row.Status = "FAILED_PRECISION"; continue; }
                decodable.Add(path);
                row.Status = "DECODE_PASS";
            }
            catch (Exception ex) { row.Status = "DECODE_BLOCKED"; row.Error = ex.GetType().Name + ": " + ex.Message; }
            finally { Save(); }
        }

        foreach (var path in decodable)
        {
            var row = rowByPath[path];
            var referencePath = decodable.FirstOrDefault(p => !string.Equals(rowByPath[p].Sha256, row.Sha256, StringComparison.Ordinal));
            if (referencePath is null) { row.Status = "REFERENCE_UNAVAILABLE"; Save(); continue; }
            var referencePixels = pipeline.DisplaySource(await pipeline.DecodeMasterAsync(referencePath), 1600);
            var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(referencePixels), referencePixels));
            var source = new ReferenceLookSource(Guid.NewGuid(), null, "Independent real camera reference", "LOCAL_ONLY",
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(referencePath))), 1, analysis);
            var look = new ReferenceLook(Guid.NewGuid(), "Real camera nonidentity Match v3", null, [source],
                new(MatchStrength: 72, ToneStrength: 60, ColorStrength: 70), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var stack = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "Match v3", true,
                new Dictionary<string, double> { ["match_strength"] = 72, ["tone_strength"] = 60, ["color_strength"] = 70 })]);
            var output = Path.Combine(evidenceRoot, "real-camera-" + rows.IndexOf(row).ToString("00"));
            Directory.CreateDirectory(output);
            try
            {
                var watch = Stopwatch.StartNew();
                using var workspace = new ReferenceColorWorkspaceViewModel(new LocalDialogs(output, path));
                await workspace.LoadTargetAsync(path);
                if (!string.Equals(workspace.ActiveTarget?.Path, path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Product target import failed: " + workspace.StatusText);
                workspace.Targets[0].AppliedLookSnapshot = look;
                workspace.Targets[0].ColorAdjustmentStackSnapshot = stack;
                await workspace.ActivateTargetCommand.ExecuteAsync(workspace.Targets[0]);
                if (workspace.Editor.MatchedImage is null || workspace.Editor.HasError)
                    throw new InvalidDataException("Product preview failed: " + workspace.Editor.StatusText);
                row.PreviewMs = watch.Elapsed.TotalMilliseconds;
                await workspace.ExportSelectedCommand.ExecuteAsync(null);
                if (workspace.Targets[0].ExportStatus != ReferenceExportStatus.Succeeded)
                    throw new InvalidDataException("Product export failed: " + workspace.ExportFailureSummary);
                row.ExportMs = watch.Elapsed.TotalMilliseconds - row.PreviewMs;
                using var stream = File.OpenRead(workspace.Targets[0].OutputPath!);
                var tiff = TiffReadBack.Read(stream);
                row.Tiff16 = tiff.Width == row.Width && tiff.Height == row.Height && tiff.BitsPerSample == 16 && tiff.SamplesPerPixel == 3;
                row.Orientation = tiff.Orientation;
                row.Parity = Compare(workspace.Editor.MatchedImage, tiff);
                row.Status = row.Tiff16 && row.Parity.Mean <= MeanLimit && row.Parity.P95 <= TailLimit && row.Parity.Max <= TailLimit
                    ? "REAL_CAMERA_PRODUCT_PASS" : "PARTIAL";
            }
            catch (Exception ex) { row.Status = "FAILED"; row.Error = ex.GetType().Name + ": " + ex.Message; }
            finally { Save(); }
        }

        Assert.IsTrue(rows.All(r => r.Status == "REAL_CAMERA_PRODUCT_PASS"),
            "Real camera product gate is not closed: " + string.Join(", ", rows.Select(r => r.Camera + "/" + r.File + "=" + r.Status)));

        void Save() => File.WriteAllText(Path.Combine(evidenceRoot, "real-camera-product-gate.json"),
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool IsInsideGitCheckout(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, ".git")) || Directory.Exists(Path.Combine(directory.FullName, ".git"))) return true;
        return false;
    }

    private static ParityResult Compare(BitmapSource preview, TiffReadBackResult tiff)
    {
        var bitmap = new FormatConvertedBitmap(preview, PixelFormats.Bgra32, null, 0);
        var bgra = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(bgra, bitmap.PixelWidth * 4, 0);
        var deltas = new double[bitmap.PixelWidth * bitmap.PixelHeight];
        var samples = tiff.Rgb48Samples.Span;
        for (var i = 0; i < deltas.Length; i++)
        {
            var x = i % bitmap.PixelWidth; var y = i / bitmap.PixelWidth;
            var sx = Math.Min(tiff.Width - 1, (int)((x + .5) * tiff.Width / bitmap.PixelWidth));
            var sy = Math.Min(tiff.Height - 1, (int)((y + .5) * tiff.Height / bitmap.PixelHeight));
            var source = (sy * tiff.Width + sx) * 3; var display = i * 4;
            var a = OklabColorSpace.FromSrgb(new VisualRgb24(bgra[display + 2], bgra[display + 1], bgra[display]));
            var b = OklabColorSpace.FromSrgb(samples[source] / 65535f, samples[source + 1] / 65535f, samples[source + 2] / 65535f);
            deltas[i] = Math.Sqrt(Math.Pow(a.L - b.L, 2) + Math.Pow(a.A - b.A, 2) + Math.Pow(a.B - b.B, 2));
        }
        Array.Sort(deltas);
        return new(deltas.Average(), deltas[(int)Math.Round((deltas.Length - 1) * .95)], deltas[^1]);
    }

    private sealed record ParityResult(double Mean, double P95, double Max);
    private sealed class GateRow(string file, string sha256)
    {
        public string File { get; } = file;
        public string Sha256 { get; } = sha256;
        public string? Camera { get; set; }
        public string Status { get; set; } = "NOT_RUN";
        public string? Error { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Precision { get; set; }
        public bool Tiff16 { get; set; }
        public int Orientation { get; set; }
        public double DecodeMs { get; set; }
        public double PreviewMs { get; set; }
        public double ExportMs { get; set; }
        public ParityResult? Parity { get; set; }
    }

    private sealed class LocalDialogs(string folder, string source) : IDialogService
    {
        public string? ChooseFolder(string title, string? initialDirectory = null) => folder;
        public IReadOnlyList<string> ChooseFiles(string title, string filter, bool multiselect = true) => [source];
        public string? ChooseSaveFile(string title, string filter, string defaultExtension, string? suggestedFileName = null) => null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds) => null;
        public bool Confirm(string message, string title) => true;
        public void ShowError(string message) { }
        public void ShowInfo(string message) { }
        public HelpAction ShowHelp() => HelpAction.None;
        public void ShowFeedback() { }
        public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates) => null;
        public bool ShowMediaDetails(MediaSelectionItem item, bool showAdvancedDetails) => false;
        public void RevealFile(string path) { }
    }
}
