using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class RawMatchTiff16ProductWpfTests
{
    [TestMethod]
    public async Task WorkspaceImportsRawPreviewsAndExportsGenuineTiff16()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RawWpf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "source.cr3"); await File.WriteAllTextAsync(path, "synthetic fake decoder only");
            var decoder = new FakeDecoder(); var dialog = new FakeDialogs(root, path);
            using var workspace = new ReferenceColorWorkspaceViewModel(dialog, rawDecoder: decoder);
            await workspace.ChooseTargetCommand.ExecuteAsync(null);
            Assert.AreEqual(path, workspace.ActiveTarget?.Path);
            Assert.IsNotNull(workspace.Editor.SourceImage);
            Assert.AreEqual(8, workspace.Editor.SourceImage.PixelWidth);
            var reference = new VisualPixelBuffer(8, 8, Enumerable.Repeat(new byte[] { 50, 180, 100 }, 64).SelectMany(x => x).ToArray());
            var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "synthetic", reference));
            workspace.Targets[0].AppliedLookSnapshot = new ReferenceLook(Guid.NewGuid(), "Synthetic", null,
                [new(Guid.NewGuid(), analysis.AssetId, "Reference", "synthetic", "synthetic", 1, analysis)],
                new(MatchStrength: 65), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            workspace.Targets[0].ColorAdjustmentStackSnapshot = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "Match v3")]);
            await workspace.ActivateTargetCommand.ExecuteAsync(workspace.Targets[0]);
            Assert.IsNotNull(workspace.Editor.MatchedImage);
            await workspace.ExportSelectedCommand.ExecuteAsync(null);
            Assert.AreEqual(ReferenceExportStatus.Succeeded, workspace.Targets[0].ExportStatus);
            var output = workspace.Targets[0].OutputPath!;
            StringAssert.EndsWith(output, ".tif");
            using var stream = File.OpenRead(output); var tiff = TiffReadBack.Read(stream);
            Assert.AreEqual(16, tiff.BitsPerSample); Assert.AreEqual(3, tiff.SamplesPerPixel);
            Assert.AreEqual(8, tiff.Width); Assert.AreEqual(8, tiff.Height);
            Assert.AreEqual(3, tiff.Orientation);
            var preview = new FormatConvertedBitmap(workspace.Editor.MatchedImage!, PixelFormats.Bgra32, null, 0);
            var bgra = new byte[preview.PixelWidth * preview.PixelHeight * 4];
            preview.CopyPixels(bgra, preview.PixelWidth * 4, 0);
            var distances = new double[tiff.Width * tiff.Height];
            for (var i = 0; i < distances.Length; i++)
            {
                var offset = i * 3; var display = i * 4;
                var a = OklabColorSpace.FromSrgb(new VisualRgb24(bgra[display + 2], bgra[display + 1], bgra[display]));
                var b = OklabColorSpace.FromSrgb(tiff.Rgb48Samples.Span[offset] / 65535f,
                    tiff.Rgb48Samples.Span[offset + 1] / 65535f, tiff.Rgb48Samples.Span[offset + 2] / 65535f);
                distances[i] = Math.Sqrt(Math.Pow(a.L - b.L, 2) + Math.Pow(a.A - b.A, 2) + Math.Pow(a.B - b.B, 2));
            }
            Array.Sort(distances);
            Assert.IsLessThanOrEqualTo(.003, distances.Average());
            Assert.IsLessThanOrEqualTo(.012, distances[(int)Math.Round((distances.Length - 1) * .95)]);
            Assert.IsLessThanOrEqualTo(.012, distances[^1]);
            Assert.IsTrue(decoder.AllProfessional);
            Assert.IsGreaterThanOrEqualTo(3, decoder.Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task UnsupportedRawIsExplainedWithoutCorruptExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RawWpf-Fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "unsupported.raf"); await File.WriteAllTextAsync(path, "unsupported fake sample");
            using var workspace = new ReferenceColorWorkspaceViewModel(new FakeDialogs(root, path), rawDecoder: new FakeDecoder(Fail: true));
            await workspace.ChooseTargetCommand.ExecuteAsync(null);
            Assert.IsNull(workspace.ActiveTarget);
            Assert.AreEqual(ReferenceTargetStatus.Failed, workspace.Targets[0].Status);
            StringAssert.Contains(workspace.Targets[0].Error!, "RAW");
            Assert.IsEmpty(Directory.GetFiles(root, "*.tif"));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class FakeDecoder(bool Fail = false) : IRawDecoder
    {
        public int Count { get; private set; }
        public bool AllProfessional { get; private set; } = true;
        public RawDecoderCapability GetCapability() => throw new NotSupportedException();
        public Task<RawDecodedImage> DecodeAsync(string path, RawToJpegOptions options, CancellationToken token = default)
        {
            Count++; AllProfessional &= options.DecodeMode == RawDecodeMode.ProfessionalDecode && !options.AutoRotate;
            if (Fail) throw new RawDecodeException(RAWSelectionAssistant.Core.Services.ErrorCodeCatalog.UnsupportedFormat, "Unsupported camera");
            var samples = Enumerable.Range(0, 8 * 8).SelectMany(i => new ushort[] { (ushort)(19001 + i), 28001, 39001 }).ToArray();
            return Task.FromResult(RawDecodedImage.FromRgb48(8, 8, 48, samples, new("Test", "Fake", null, 3, "sRGB")));
        }
    }

    private sealed class FakeDialogs(string folder, string source) : IDialogService
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
