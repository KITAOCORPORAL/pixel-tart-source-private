using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

public readonly record struct VisualRgb24(byte R, byte G, byte B);

public sealed class VisualPixelBuffer
{
    public VisualPixelBuffer(int width, int height, ReadOnlyMemory<byte> rgb24)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (rgb24.Length != checked(width * height * 3)) throw new ArgumentException("RGB24 buffer length does not match dimensions.", nameof(rgb24));
        Width = width; Height = height; Rgb24 = rgb24;
    }

    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<byte> Rgb24 { get; }
    public int PixelCount => Width * Height;
}

public static class VisualAnalysisFingerprint
{
    public static string Compute(VisualPixelBuffer pixels) => Convert.ToHexString(SHA256.HashData(pixels.Rgb24.Span));
}

public readonly record struct VisualLab(double L, double A, double B);

public sealed record DominantColor(
    VisualRgb24 Rgb,
    VisualLab Lab,
    double Hue,
    double Saturation,
    double Lightness,
    double Weight,
    string Hex);

public enum PaletteSortMode { Weight, Lightness, Hue }

public enum ColorHarmonyTendency { LowSaturationNeutral, Monochrome, Analogous, Complementary, SplitComplementary, Triadic, Mixed }

public enum ToneKeyTendency { Low, Mid, High }

public enum ContrastTendency { Low, Medium, High }

public enum LuminanceSpanTendency { Narrow, Medium, Wide }

public enum SaturationTendency { Low, Medium, High }

public enum WarmCoolTendency { Cool, Neutral, Warm }

public enum VisualAnalysisSourceKind { RasterOriginal, RenderedProxy, EmbeddedPreview }

public sealed record ToneZoneRatios(double DeepShadow, double Shadow, double Midtone, double Highlight, double Specular)
{
    public double Sum => DeepShadow + Shadow + Midtone + Highlight + Specular;
}

public sealed record ColorDerivatives(
    VisualRgb24 Complementary,
    IReadOnlyList<VisualRgb24> Analogous,
    IReadOnlyList<VisualRgb24> Triadic,
    IReadOnlyList<VisualRgb24> SplitComplementary,
    IReadOnlyList<VisualRgb24> Monochrome);

public sealed record AssetVisualAnalysisResult(
    Guid AssetId,
    string ContentHash,
    string AnalysisVersion,
    int PaletteSize,
    PaletteSortMode PaletteSort,
    VisualAnalysisSourceKind AnalysisSource,
    string SourceProfile,
    string AnalysisProfile,
    IReadOnlyList<DominantColor> Palette,
    ColorHarmonyTendency Harmony,
    uint[] HistogramR,
    uint[] HistogramG,
    uint[] HistogramB,
    uint[] HistogramLuma,
    ToneZoneRatios ToneZones,
    double AverageLuma,
    double MedianLuma,
    double BlackClipRatio,
    double WhiteClipRatio,
    double ContrastMetric,
    ContrastTendency Contrast,
    double LuminanceSpanMetric,
    LuminanceSpanTendency LuminanceSpan,
    ToneKeyTendency ToneKey,
    double AverageSaturation,
    double DominantHue,
    SaturationTendency Saturation,
    double WarmCoolMetric,
    WarmCoolTendency WarmCool,
    DateTimeOffset CreatedAt,
    bool CacheHit = false)
{
    public const string CurrentVersion = "visual-analysis-v1";
}

public sealed record AssetVisualAnalysisRequest(
    Guid AssetId,
    string ContentHash,
    VisualPixelBuffer Pixels,
    int PaletteSize = 5,
    PaletteSortMode PaletteSort = PaletteSortMode.Weight,
    VisualAnalysisSourceKind AnalysisSource = VisualAnalysisSourceKind.RasterOriginal,
    string SourceProfile = "UnknownAssumedSrgb",
    string AnalysisProfile = "sRGB IEC61966-2.1",
    bool PixelsConvertedToAnalysisProfile = true);

public sealed record VisualAnalysisPerformanceSample(int Count, double MeanMilliseconds, double P95Milliseconds, int CacheHits, int CacheMisses);

public sealed record VisualSmartFilterQuery(
    double? DominantHue = null,
    double? HueTolerance = null,
    double? MaximumAverageSaturation = null,
    ToneKeyTendency? ToneKey = null,
    ContrastTendency? Contrast = null,
    double? MinimumShadowRatio = null,
    double? MinimumHighlightRatio = null,
    WarmCoolTendency? WarmCool = null);

public sealed record VisualColorSearchQuery(VisualLab Target, double MaximumDeltaE, int PageSize = 100, string? Cursor = null)
{
    public int EffectivePageSize => Math.Clamp(PageSize, 1, 200);
}

public interface IAssetVisualAnalysisQuery
{
    Task<IReadOnlyList<Guid>> QueryVisualAsync(VisualSmartFilterQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> SearchByColorAsync(VisualColorSearchQuery query, CancellationToken cancellationToken = default);
}
