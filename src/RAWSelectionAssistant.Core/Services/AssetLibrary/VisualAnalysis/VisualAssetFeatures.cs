using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

/// <summary>
/// The single search/index variant.  Inspector requests may still render 3/5/7
/// palettes, but they never replace these canonical searchable features.
/// </summary>
public static class AssetVisualFeatureContract
{
    public const string AnalysisVersion = AssetVisualAnalysisResult.CurrentVersion;
    public const int PaletteSize = 5;
    public const PaletteSortMode PaletteSort = PaletteSortMode.Weight;
    public const double MinimumPaletteWeight = .15;
    public const double MinimumChromaticSaturation = .08;
    public const double MinimumChromaticLabChroma = 8;
    public const int CandidatePoolLimit = 5_000;
    public const int ResultLimit = 100;

    public static bool IsCanonical(AssetVisualAnalysisResult result) =>
        result.AnalysisVersion == AnalysisVersion &&
        result.PaletteSize == PaletteSize &&
        result.PaletteSort == PaletteSort;
}

public static class VisualClassificationThresholds
{
    public const double LowContrastMaximum = .25;
    public const double MediumContrastMaximum = .55;
    public const double NarrowLuminanceSpanMaximum = .35;
    public const double MediumLuminanceSpanMaximum = .70;
    public const double LowToneMedianMaximum = 32;
    public const double HighToneMedianMinimum = 120;
    public const double LowSaturationMaximum = .18;
    public const double MediumSaturationMaximum = .50;
    public const double NeutralWarmCoolMagnitudeMaximum = .15;
}

public enum AssetVisualFeatureState
{
    NotAnalyzed,
    Valid,
    Stale,
    Failed
}

public readonly record struct VisualHueRange(double StartDegrees, double EndDegrees)
{
    public double Start => Normalize(StartDegrees);
    public double End => Normalize(EndDegrees);
    public bool CrossesZero => Start > End;
    public bool Contains(double hue)
    {
        hue = Normalize(hue);
        return CrossesZero ? hue >= Start || hue <= End : hue >= Start && hue <= End;
    }

    private static double Normalize(double value) => (value % 360 + 360) % 360;
}

public sealed record AssetVisualFeatureSummary
{
    public required Guid AssetId { get; init; }
    public required AssetVisualFeatureState State { get; init; }
    public required string AnalysisVersion { get; init; }
    public required string ContentFingerprint { get; init; }
    public string? SourceContentHash { get; init; }
    public required VisualAnalysisSourceKind AnalysisSource { get; init; }
    public required string SourceProfile { get; init; }
    public required string AnalysisProfile { get; init; }
    public ColorHarmonyTendency? Harmony { get; init; }
    public ToneKeyTendency? ToneKey { get; init; }
    public ContrastTendency? Contrast { get; init; }
    public LuminanceSpanTendency? LuminanceSpan { get; init; }
    public SaturationTendency? Saturation { get; init; }
    public WarmCoolTendency? WarmCool { get; init; }
    public double? DominantHue { get; init; }
    public double? SecondaryHue { get; init; }
    public double? AverageHue { get; init; }
    public double? AverageLuma { get; init; }
    public double? MedianLuma { get; init; }
    public double? ContrastMetric { get; init; }
    public double? LumaSpreadMetric { get; init; }
    public double? AverageSaturation { get; init; }
    public double? MedianSaturation { get; init; }
    public double? AverageLightness { get; init; }
    public double? WarmCoolMetric { get; init; }
    public double? DeepShadowRatio { get; init; }
    public double? ShadowRatio { get; init; }
    public double? MidtoneRatio { get; init; }
    public double? HighlightRatio { get; init; }
    public double? SpecularRatio { get; init; }
    public double? BlackClipRatio { get; init; }
    public double? WhiteClipRatio { get; init; }
    public string? HistogramLumaSignature { get; init; }
    public string? PaletteSignature { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record AssetVisualFeatures(
    AssetVisualFeatureSummary Summary,
    AssetVisualAnalysisResult? Analysis);

public sealed record VisualAssetFilter(
    AssetVisualFeatureState? State = null,
    VisualHueRange? DominantHue = null,
    ColorHarmonyTendency? Harmony = null,
    ToneKeyTendency? ToneKey = null,
    ContrastTendency? Contrast = null,
    SaturationTendency? Saturation = null,
    WarmCoolTendency? WarmCool = null,
    double? MinimumAverageLuma = null,
    double? MaximumAverageLuma = null,
    double? MinimumContrast = null,
    double? MaximumContrast = null,
    double? MinimumAverageSaturation = null,
    double? MaximumAverageSaturation = null,
    double? MinimumMedianSaturation = null,
    double? MaximumMedianSaturation = null,
    double? MinimumLumaSpread = null,
    double? MaximumLumaSpread = null,
    double? MinimumShadowRatio = null,
    double? MinimumHighlightRatio = null,
    double? MaximumBlackClipRatio = null,
    double? MaximumWhiteClipRatio = null,
    double? MinimumWarmCoolMetric = null,
    double? MaximumWarmCoolMetric = null,
    VisualLab? PaletteColor = null,
    double MaximumDeltaE = 20,
    double MinimumPaletteWeight = AssetVisualFeatureContract.MinimumPaletteWeight);

public sealed record VisualAssetQuery(
    AssetLibraryQuery Scope,
    VisualAssetFilter Filter,
    int PageSize = 100,
    string? Cursor = null)
{
    public int EffectivePageSize => Math.Clamp(PageSize, 1, 200);
}

public sealed record VisualAssetMatch(
    AssetItem Asset,
    AssetVisualFeatureSummary Features,
    double? ColorDeltaE = null);

public sealed record VisualAssetPage(
    IReadOnlyList<VisualAssetMatch> Items,
    string? NextCursor,
    int TotalCount);

public sealed record VisualSimilarityScores(
    double Color,
    double Tone,
    double Contrast,
    double Saturation,
    double Overall,
    double PaletteComponent,
    double HistogramComponent)
{
    public string Explanation =>
        $"颜色 {Color:F0} · 影调 {Tone:F0} · 对比 {Contrast:F0} · 饱和度 {Saturation:F0}（满分 100）";
}

public sealed record VisualSimilarityMatch(
    AssetItem Asset,
    AssetVisualFeatureSummary Features,
    VisualSimilarityScores Scores);

public enum VisualSimilarityMode
{
    Full,
    Palette
}

public sealed record VisualSimilarityQuery(
    Guid ReferenceAssetId,
    AssetLibraryQuery Scope,
    int Limit = AssetVisualFeatureContract.ResultLimit,
    VisualSimilarityMode Mode = VisualSimilarityMode.Full)
{
    public int EffectiveLimit => Math.Clamp(Limit, 1, AssetVisualFeatureContract.ResultLimit);
}

public sealed record VisualSimilarityProfile(
    double ColorWeight = .40,
    double ToneWeight = .30,
    double ContrastWeight = .20,
    double SaturationWeight = .10)
{
    public static VisualSimilarityProfile Default { get; } = new();
    public VisualSimilarityProfile Normalize()
    {
        var values = new[] { Math.Max(0, ColorWeight), Math.Max(0, ToneWeight), Math.Max(0, ContrastWeight), Math.Max(0, SaturationWeight) };
        var sum = values.Sum(); if (sum <= 0) throw new ArgumentException("At least one similarity weight must be positive.");
        return new(values[0] / sum, values[1] / sum, values[2] / sum, values[3] / sum);
    }
}

public interface IVisualAssetQueryService
{
    Task<AssetVisualFeatures> GetFeaturesAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<VisualAssetPage> QueryAsync(VisualAssetQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisualAssetMatch>> SearchByColorAsync(VisualAssetQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisualSimilarityMatch>> FindSimilarAsync(VisualSimilarityQuery query, CancellationToken cancellationToken = default);
}
