namespace RAWSelectionAssistant.Core.Models;

public enum JpegFileSourceType
{
    SourceDirectory,
    CustomerReturnedFile,
    ManuallySelectedFile
}

public enum CustomerJpegHandlingMode
{
    Strict,
    SmartBackup,
    AllowCustomerFile
}

public enum SourceDirectoryType
{
    Jpeg,
    Raw,
    Mixed,
    Other
}

public sealed class JpegQualityInfo
{
    public long FileSizeBytes { get; set; }
    public int? PixelWidth { get; set; }
    public int? PixelHeight { get; set; }
    public long? TotalPixels => PixelWidth is > 0 && PixelHeight is > 0 ? (long)PixelWidth.Value * PixelHeight.Value : null;
    public bool? HasExif { get; set; }
    public string CameraMake { get; set; } = string.Empty;
    public string CameraModel { get; set; } = string.Empty;
    public DateTime? DateTimeOriginal { get; set; }
    public bool? HasIccProfile { get; set; }
    public string SoftwareTag { get; set; } = string.Empty;
    public string Orientation { get; set; } = string.Empty;
    public List<string> QualityWarnings { get; set; } = [];
    public string MetadataReadError { get; set; } = string.Empty;

    public string PixelDimensions => PixelWidth is > 0 && PixelHeight is > 0 ? $"{PixelWidth} × {PixelHeight}" : "未知";
    public string ExifStatusText => HasExif switch { true => "包含 EXIF", false => "无 EXIF", _ => "未知" };
    public string IccStatusText => HasIccProfile switch { true => "包含 ICC", false => "无 ICC", _ => "未知" };
    public string CameraText => string.Join(' ', new[] { CameraMake, CameraModel }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim() is { Length: > 0 } text ? text : "未知";
    public string DateTimeOriginalText => DateTimeOriginal?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知";
    public string SoftwareText => string.IsNullOrWhiteSpace(SoftwareTag) ? "未知" : SoftwareTag;
    public string OrientationText => string.IsNullOrWhiteSpace(Orientation) ? "未知" : Orientation;
    public string QualityWarningsText => QualityWarnings.Count == 0 ? "未发现明显风险，仍无法确认是否为原图" : string.Join("；", QualityWarnings);

    public int ExifCompletenessScore =>
        (HasExif == true ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(CameraMake) ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(CameraModel) ? 1 : 0) +
        (DateTimeOriginal.HasValue ? 1 : 0) +
        (HasIccProfile == true ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(SoftwareTag) ? 1 : 0);
}

public sealed record SourceDirectorySetting(string Path, SourceDirectoryType DirectoryType, int Priority);

public static class JpegQualityEnumExtensions
{
    public static string ToChinese(this JpegFileSourceType sourceType) => sourceType switch
    {
        JpegFileSourceType.SourceDirectory => "来源目录文件",
        JpegFileSourceType.CustomerReturnedFile => "客户返回文件",
        JpegFileSourceType.ManuallySelectedFile => "用户手动指定文件",
        _ => "未知来源"
    };

    public static string ToChinese(this CustomerJpegHandlingMode mode) => mode switch
    {
        CustomerJpegHandlingMode.Strict => "严格模式（默认）",
        CustomerJpegHandlingMode.SmartBackup => "智能备用模式",
        CustomerJpegHandlingMode.AllowCustomerFile => "允许客户文件模式",
        _ => "严格模式（默认）"
    };

    public static string ToChinese(this SourceDirectoryType type) => type switch
    {
        SourceDirectoryType.Jpeg => "JPG 来源目录",
        SourceDirectoryType.Raw => "RAW 来源目录",
        SourceDirectoryType.Mixed => "JPG + RAW 混合目录",
        SourceDirectoryType.Other => "其他格式目录",
        _ => "JPG + RAW 混合目录"
    };
}
