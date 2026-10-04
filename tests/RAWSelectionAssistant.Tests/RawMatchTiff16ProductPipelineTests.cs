using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class RawMatchTiff16ProductPipelineTests
{
    [TestMethod]
    public async Task ProfessionalDecodeMatchPreviewAndAtomicTiffShareFrozenState()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ProductRaw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "synthetic.cr3");
            await File.WriteAllTextAsync(input, "repository-owned fake-decoder fixture");
            var decoder = new FakeDecoder(); var pipeline = new RawMatchTiff16ProductPipeline(decoder);
            var frozenMaster = await pipeline.DecodeFrozenMasterAsync(input);
            var master = frozenMaster.Image;
            Assert.AreEqual("16", master.SourceBitDepth);
            Assert.AreEqual("sRGB", master.WorkingColorSpace);
            Assert.AreEqual((ushort)6, master.Orientation);
            Assert.AreEqual("Test camera", master.Metadata?.CameraModel);
            Assert.AreEqual(32769 / 65535f, master.Rgb32.Span[0]);

            var referencePixels = new VisualPixelBuffer(8, 8, Enumerable.Repeat(new byte[] { 100, 180, 80 }, 64).SelectMany(x => x).ToArray());
            var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "synthetic-reference", referencePixels));
            var look = new ReferenceLook(Guid.NewGuid(), "Synthetic", null,
                [new(Guid.NewGuid(), analysis.AssetId, "Reference", "synthetic", "synthetic", 1, analysis)],
                new(MatchStrength: 72, ToneStrength: 60, ColorStrength: 70), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var stack = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.ReferenceMatch, "Match v3", true)]);
            var rendered = pipeline.Render(master, look, stack);
            Assert.IsNotNull(rendered.ProcessingPixels);
            Assert.AreEqual("16", rendered.ProcessingPixels.SourceBitDepth);
            var preview = rendered.Pixels;
            var destination = Path.Combine(root, "result.tif");
            await pipeline.ExportAsync(frozenMaster, destination, look, stack);
            Assert.AreEqual(1, decoder.DecodeCount); // the session-owned RAW master is reused for export
            Assert.IsTrue(decoder.AllProfessional);
            TiffReadBackResult readBack;
            using (var stream = File.OpenRead(destination)) readBack = TiffReadBack.Read(stream);
            Assert.AreEqual(8, readBack.Width); Assert.AreEqual(8, readBack.Height);
            Assert.AreEqual(16, readBack.BitsPerSample); Assert.AreEqual(3, readBack.SamplesPerPixel);
            Assert.AreEqual(6, readBack.Orientation);
            Assert.IsEmpty(readBack.IccProfile.ToArray()); // no fabricated color profile
            var errors = new double[8 * 8];
            for (var i = 0; i < errors.Length; i++)
            {
                var offset = i * 3;
                var a = OklabColorSpace.FromSrgb(new VisualRgb24(preview.Rgb24.Span[offset], preview.Rgb24.Span[offset + 1], preview.Rgb24.Span[offset + 2]));
                var b = OklabColorSpace.FromSrgb(readBack.Rgb48Samples.Span[offset] / 65535f, readBack.Rgb48Samples.Span[offset + 1] / 65535f, readBack.Rgb48Samples.Span[offset + 2] / 65535f);
                errors[i] = Math.Sqrt(Math.Pow(a.L - b.L, 2) + Math.Pow(a.A - b.A, 2) + Math.Pow(a.B - b.B, 2));
            }
            Array.Sort(errors);
            Assert.IsLessThanOrEqualTo(.003, errors.Average());
            Assert.IsLessThanOrEqualTo(.012, errors[^1]);
            Assert.IsLessThanOrEqualTo(.012, errors[(int)Math.Round((errors.Length - 1) * .95)]);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task FrozenMasterRejectsSourceMutationBeforeExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ProductRaw-Mutation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "synthetic.cr3");
            var destination = Path.Combine(root, "result.tif");
            await File.WriteAllTextAsync(input, "original source");
            var decoder = new FakeDecoder();
            var pipeline = new RawMatchTiff16ProductPipeline(decoder);
            var master = await pipeline.DecodeFrozenMasterAsync(input);
            await File.WriteAllTextAsync(input, "changed source");

            await Assert.ThrowsExactlyAsync<IOException>(async () => await pipeline.ExportAsync(master, destination, null, null));
            Assert.AreEqual(1, decoder.DecodeCount);
            Assert.IsFalse(File.Exists(destination));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task EightBitFallbackCannotPretendToBeProfessionalTiff()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-ProductRaw-Failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var destination = Path.Combine(root, "result.tif");
            var pipeline = new RawMatchTiff16ProductPipeline(new FakeDecoder(EightBit: true));
            await Assert.ThrowsExactlyAsync<RawDecodeException>(async () => await pipeline.ExportAsync(Path.Combine(root, "unsupported.raf"), destination, null, null));
            Assert.IsFalse(File.Exists(destination));
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ExistingDestinationIsNotReplacedByProductExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-Raw-NoOverwrite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var destination = Path.Combine(root, "protected.tif");
            var source = Path.Combine(root, "source.cr3");
            await File.WriteAllTextAsync(source, "source fixture");
            await File.WriteAllTextAsync(destination, "previous successful user export");
            var pipeline = new RawMatchTiff16ProductPipeline(new FakeDecoder());
            await Assert.ThrowsExactlyAsync<IOException>(async () => await pipeline.ExportAsync(source, destination, null, null));
            Assert.AreEqual("previous successful user export", await File.ReadAllTextAsync(destination));
            Assert.IsEmpty(Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task HighPrecisionColorRangeWithoutSamplesIsExactIdentity()
    {
        var pipeline = new RawMatchTiff16ProductPipeline(new FakeDecoder());
        var master = await pipeline.DecodeMasterAsync("synthetic.nef");
        var stack = new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.ColorRange, "Color range")]);
        CollectionAssert.AreEqual(master.Rgb32.ToArray(), pipeline.Render(master, null, stack).ProcessingPixels!.Rgb32.ToArray());
    }

    private sealed class FakeDecoder(bool EightBit = false) : IRawDecoder
    {
        public int DecodeCount { get; private set; }
        public bool AllProfessional { get; private set; } = true;
        public RawDecoderCapability GetCapability() => throw new NotSupportedException();
        public Task<RawDecodedImage> DecodeAsync(string path, RawToJpegOptions options, CancellationToken token = default)
        {
            DecodeCount++;
            AllProfessional &= options.DecodeMode == RawDecodeMode.ProfessionalDecode && !options.AutoRotate;
            var meta = new RawImageMetadata("Test", "Test camera", null, 6, "sRGB");
            if (EightBit) return Task.FromResult(new RawDecodedImage(8, 8, 24, new byte[8 * 8 * 3], meta));
            var pixels = Enumerable.Range(0, 8 * 8).SelectMany(i => new ushort[] { (ushort)(32769 + i), 29123, 45127 }).ToArray();
            return Task.FromResult(RawDecodedImage.FromRgb48(8, 8, 48, pixels, meta));
        }
    }
}
