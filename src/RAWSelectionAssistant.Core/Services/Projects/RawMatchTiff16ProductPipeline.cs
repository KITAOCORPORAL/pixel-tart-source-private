using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Color Studio's RAW master and TIFF16 route. RGB24 is exclusively a display/analysis adapter.</summary>
public sealed class RawMatchTiff16ProductPipeline(IRawDecoder decoder)
{
    public static bool IsRaw(string path) => RawToJpegDefaults.CandidateRawExtensions.Contains(Path.GetExtension(path));

    public async Task<HighBitDepthImageBuffer> DecodeMasterAsync(string path, CancellationToken token = default)
    {
        if (!IsRaw(path)) throw new RawDecodeException(ErrorCodeCatalog.UnsupportedFormat, "Not a candidate RAW file.");
        var decoded = await decoder.DecodeAsync(path, new RawToJpegOptions(AutoRotate: false, DecodeMode: RawDecodeMode.ProfessionalDecode), token).ConfigureAwait(false);
        if (!decoded.IsProfessionalPrecision || decoded.Channels != 3 || decoded.Rgb48Pixels!.Length != checked(decoded.Width * decoded.Height * 3))
            throw new RawDecodeException(ErrorCodeCatalog.DecodeFailed, "The camera RAW did not provide genuine 16-bit RGB output.");
        if (!string.Equals(decoded.Metadata.ColorSpace, "sRGB", StringComparison.OrdinalIgnoreCase))
            throw new RawDecodeException(ErrorCodeCatalog.DecodeFailed, "The RAW output color space is not supported by Color Studio.");
        return HighBitDepthImageBuffer.FromRaw(decoded);
    }

    public ColorStudioRenderResult Render(HighBitDepthImageBuffer master, ReferenceLook? look, ColorAdjustmentStack? stack, int maximumEdge = 0, CancellationToken token = default, PixelTartFilmSettings? film = null)
    {
        ArgumentNullException.ThrowIfNull(master);
        if (master.SourceBitDepth != "16" || !string.Equals(master.WorkingColorSpace, "sRGB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RAW processing requires 16-bit decoded sRGB input.");
        var working = maximumEdge > 0 ? CreateProxy(master, maximumEdge, token) : master;
        var state = stack is { Nodes.Count: > 0 } ? stack.DeepClone() : look is not null
            ? ColorStudioLegacyMigration.Migrate(look with { Film = film ?? look.Film }).Stack
            : new ColorAdjustmentStack([new(Guid.NewGuid(), ColorStudioNodeType.Preset, "原图", true)]);
        // The float renderer does not yet reproduce every display-only Film/ColorRange effect.
        // Refuse those nodes rather than export a silently different professional result.
        if (state.Nodes.Any(node => node.Enabled && node.Type != ColorStudioNodeType.ReferenceMatch &&
            (node.Type != ColorStudioNodeType.Preset || node.NumericParameters.Count != 0)) ||
            (stack is null && film is { Enabled: true }) ||
            (stack is { Nodes.Count: > 0 } && film is { Enabled: true }))
            throw new NotSupportedException("RAW TIFF16 export currently supports Match v3 only; this adjustment needs a high-precision implementation.");
        var display = working.ToVisualRgb24();
        var analysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(display), display), token);
        return new ColorStudioRenderPipeline().Render(working, analysis, look, state, token);
    }

    public VisualPixelBuffer DisplaySource(HighBitDepthImageBuffer master, int maximumEdge, CancellationToken token = default) =>
        CreateProxy(master, maximumEdge, token).ToVisualRgb24();

    public HighBitDepthImageBuffer PreviewMaster(HighBitDepthImageBuffer master, int maximumEdge = 1600, CancellationToken token = default) =>
        CreateProxy(master, maximumEdge, token);

    public async Task<TiffExportResult> ExportAsync(string sourcePath, string destinationPath, ReferenceLook? look, ColorAdjustmentStack? stack, CancellationToken token = default, PixelTartFilmSettings? film = null)
    {
        var master = await DecodeMasterAsync(sourcePath, token).ConfigureAwait(false);
        var output = Render(master, look, stack, token: token, film: film).ProcessingPixels!;
        return await AtomicTiffWriter.WriteRgb48Async(destinationPath, output,
            new(TiffBitDepth.Sixteen, Software: "Pixel Tart", Orientation: output.Orientation), token, overwrite: false).ConfigureAwait(false);
    }

    private static HighBitDepthImageBuffer CreateProxy(HighBitDepthImageBuffer source, int maximumEdge, CancellationToken token)
    {
        var edge = Math.Max(source.Width, source.Height);
        if (edge <= maximumEdge) return source;
        var width = Math.Max(1, (int)Math.Round(source.Width * maximumEdge / (double)edge));
        var height = Math.Max(1, (int)Math.Round(source.Height * maximumEdge / (double)edge));
        var values = new float[checked(width * height * 3)];
        var original = source.Rgb32.Span;
        for (var y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            var sourceY = Math.Min(source.Height - 1, (int)((y + .5) * source.Height / height));
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(source.Width - 1, (int)((x + .5) * source.Width / width));
                original.Slice((sourceY * source.Width + sourceX) * 3, 3).CopyTo(values.AsSpan((y * width + x) * 3, 3));
            }
        }
        return new(width, height, values, source.SourceBitDepth, source.WorkingColorSpace, source.Orientation, source.Metadata);
    }
}
