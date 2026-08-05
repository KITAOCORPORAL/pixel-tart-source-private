namespace RAWSelectionAssistant.Core.Models;

public enum TetherAssetFilter
{
    All,
    JpegOnly,
    RawOnly,
    Paired,
    Unpaired,
    Favorites,
    Rated,
    Rejected,
    NeedsAttention
}

public enum TetherAssetSort
{
    NewestFirst,
    OldestFirst,
    FileName,
    Rating,
    Status
}

public enum TetherPreviewMode { Fit, Fill, ActualSize, Free }
public enum TetherCompareMode { None, SideBySide, Overlay }
public enum TetherGuideMode { None, Thirds, CenterCross, Square, Ratio4x5, Ratio3x4, Ratio2x3, Ratio16x9, Ratio9x16, SafeArea }
public enum TetherCanvasTone { Black, DarkGray, MidGray, Checkerboard }

public sealed record TetherExifInfo(
    string FileType,
    string CaptureTime,
    string CameraMake,
    string CameraModel,
    string Lens,
    string FocalLength,
    string Aperture,
    string Shutter,
    string Iso,
    string ExposureCompensation,
    string WhiteBalance,
    string ColorSpace,
    string PixelDimensions,
    string FileSize,
    string PairingStatus,
    string? SourcePath = null,
    bool MetadataAvailable = false)
{
    public static TetherExifInfo Unavailable(TetherAssetRecord asset) => new(
        string.IsNullOrWhiteSpace(asset.Extension) ? "未提供" : asset.Extension.TrimStart('.').ToUpperInvariant(),
        asset.ModifiedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "未提供",
        "未提供", "未提供", "未提供", "未提供", "未提供", "未提供", "未提供", "未提供", "未提供", "未提供", "未提供",
        asset.FileSize is long bytes ? FormatBytes(bytes) : "未提供",
        asset.PairedAssetId.HasValue ? "JPG/RAW 已配对" : "未配对",
        asset.SourcePath,
        false);

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        var display = (double)value;
        while (display >= 1024 && unit < units.Length - 1) { display /= 1024; unit++; }
        return $"{display:0.##} {units[unit]}";
    }
}

public sealed record TetherHistogramData(int[] Red, int[] Green, int[] Blue, int[] Luminance, bool BasedOnProxy)
{
    public static TetherHistogramData Empty { get; } = new(new int[256], new int[256], new int[256], new int[256], true);
}

public sealed record TetherAnnotationSaveResult(bool Success, TetherAnnotationRecord? Annotation, string? ErrorCode = null, string? Message = null);

public sealed record TetherDisplaySettings(
    Guid SessionId,
    bool AutoLatest = true,
    TetherGuideMode GuideMode = TetherGuideMode.None,
    TetherCanvasTone CanvasTone = TetherCanvasTone.DarkGray,
    int HighlightThreshold = 250,
    int ShadowThreshold = 5,
    string? ReferencePath = null,
    double ReferenceOpacity = .45,
    double ReferenceScale = 1,
    double ReferenceOffsetX = 0,
    double ReferenceOffsetY = 0,
    bool ReferenceFlipHorizontal = false,
    bool ReferenceLocked = false);
