namespace RAWSelectionAssistant.Core.Models;

public static class PublishingDefaults
{
    public const string TaskType = "PublishingExport";
    public const string DefaultSuffix = "_social";
    public const int MaximumInputCount = 5000;
    public static IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };
}

public enum PublishingSizeMode { Original, LongestEdge, Exact }
public enum PublishingOutputFormat { Jpeg, Png }
public enum WatermarkLayerType { Image, Text }
public enum WatermarkPosition { TopLeft, TopCenter, TopRight, MiddleLeft, Center, MiddleRight, BottomLeft, BottomCenter, BottomRight }

public sealed record PublishingDimensions(
    bool Enabled = true,
    PublishingSizeMode Mode = PublishingSizeMode.LongestEdge,
    int LongestEdge = 2400,
    int Width = 1920,
    int Height = 1080,
    int JpegQuality = 88,
    bool PreserveMetadata = true)
{
    public PublishingDimensions Validate()
    {
        if (LongestEdge is < 320 or > 30000 || Width is < 1 or > 30000 || Height is < 1 or > 30000) throw new ArgumentOutOfRangeException(nameof(LongestEdge));
        if (JpegQuality is < 40 or > 100) throw new ArgumentOutOfRangeException(nameof(JpegQuality));
        return this;
    }
}

public sealed record WatermarkColorAdjustments(double Hue = 0, double Saturation = 0, double Lightness = 0, bool Invert = false)
{
    public WatermarkColorAdjustments Validate()
    {
        if (Hue is < -180 or > 180 || Saturation is < -1 or > 1 || Lightness is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(Hue));
        return this;
    }
}

public sealed record WatermarkLayer(
    Guid Id,
    WatermarkLayerType Type,
    bool Enabled = true,
    string? ImagePath = null,
    string Text = "",
    string FontFamily = "Microsoft YaHei UI",
    string FontWeight = "SemiBold",
    double FontSize = 32,
    string Color = "#FFFFFFFF",
    double LetterSpacing = 0,
    double Opacity = .72,
    double WidthPercent = 12,
    WatermarkPosition Position = WatermarkPosition.BottomRight,
    double MarginPercent = 3,
    double HorizontalOffset = 0,
    double VerticalOffset = 0,
    WatermarkColorAdjustments? ColorAdjustments = null)
{
    public WatermarkColorAdjustments EffectiveColorAdjustments => ColorAdjustments ?? new();
    public WatermarkLayer Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("水印图层身份不能为空。", nameof(Id));
        if (Opacity is < 0 or > 1 || WidthPercent is <= 0 or > 100 || MarginPercent is < 0 or > 50 || FontSize is <= 0 or > 1000 || LetterSpacing is < -20 or > 100) throw new ArgumentOutOfRangeException(nameof(Opacity));
        if (Type == WatermarkLayerType.Image && (string.IsNullOrWhiteSpace(ImagePath) || !Path.IsPathFullyQualified(ImagePath))) throw new ArgumentException("图片水印路径无效。", nameof(ImagePath));
        if (Type == WatermarkLayerType.Text && string.IsNullOrWhiteSpace(Text)) throw new ArgumentException("文字水印内容不能为空。", nameof(Text));
        EffectiveColorAdjustments.Validate();
        return this;
    }
}

public sealed record PublishingOptions(
    PublishingDimensions? Dimensions = null,
    bool WatermarksEnabled = true,
    IReadOnlyList<WatermarkLayer>? WatermarkLayers = null,
    PublishingOutputFormat OutputFormat = PublishingOutputFormat.Jpeg,
    string Suffix = PublishingDefaults.DefaultSuffix)
{
    public PublishingDimensions EffectiveDimensions => Dimensions ?? new();
    public IReadOnlyList<WatermarkLayer> EffectiveWatermarkLayers => WatermarkLayers ?? [];
    public PublishingOptions Validate()
    {
        EffectiveDimensions.Validate();
        if (WatermarksEnabled) foreach (var layer in EffectiveWatermarkLayers.Where(layer => layer.Enabled)) layer.Validate();
        if (Suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("文件名后缀包含无效字符。", nameof(Suffix));
        return this;
    }
}

public sealed record PublishingExportRequest(
    IReadOnlyList<string> SourceFiles,
    string DestinationDirectory,
    PublishingOptions Options,
    Guid? ProjectId = null)
{
    public PublishingExportRequest Validate()
    {
        if (SourceFiles.Count is 0 or > PublishingDefaults.MaximumInputCount) throw new ArgumentOutOfRangeException(nameof(SourceFiles));
        if (SourceFiles.Any(path => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))) throw new ArgumentException("输入路径必须为绝对路径。", nameof(SourceFiles));
        if (string.IsNullOrWhiteSpace(DestinationDirectory) || !Path.IsPathFullyQualified(DestinationDirectory)) throw new ArgumentException("输出目录必须为绝对路径。", nameof(DestinationDirectory));
        Options.Validate();
        return this;
    }
}

public enum PublishingItemState { Completed, Failed, Cancelled }
public sealed record PublishingItemResult(int Sequence, PublishingItemState State, string SourcePath, string? DestinationPath, long BytesWritten, string? ErrorMessage = null);
public sealed record PublishingExportResult(Guid TaskId, TaskLifecycleState State, TaskResultSummary Summary, IReadOnlyList<PublishingItemResult> Items);

public sealed record PublishingPreset(Guid Id, string Name, PublishingOptions Options, DateTimeOffset UpdatedAtUtc)
{
    public PublishingPreset Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("发布预设无效。");
        Options.Validate(); return this;
    }
}

public sealed record ProjectPublishingDefaults(Guid ProjectId, Guid DefaultPublishingPresetId);
