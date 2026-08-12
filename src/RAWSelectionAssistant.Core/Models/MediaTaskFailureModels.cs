using System.Text.Json;
using System.Text.RegularExpressions;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Models;

public static class MediaTaskStages
{
    public const string InputValidation = "输入检查";
    public const string FileOpen = "文件读取";
    public const string RawDecode = "RAW 解码";
    public const string ImageDecode = "图片解码";
    public const string Resize = "尺寸处理";
    public const string JpegEncode = "JPEG 编码";
    public const string TemporaryWrite = "临时文件写入";
    public const string OutputCommit = "输出文件写入";
    public const string OutputVerification = "最终文件验证";
    public const string SourceVerification = "源文件安全验证";
    public const string TaskPersistence = "任务状态保存";
}

public sealed record MediaTaskFailureDetail(
    string FileName,
    string Stage,
    string ErrorCode,
    string UserMessage,
    string TechnicalMessage,
    bool Retryable,
    bool OutputOwned)
{
    public string FirstLine => $"{FileName}：{UserMessage}";
}

public static partial class MediaTaskFailurePayload
{
    private const string Prefix = "PixelTartFailure/v1:";

    public static string Serialize(MediaTaskFailureDetail detail) =>
        Prefix + JsonSerializer.Serialize(detail with
        {
            FileName = Path.GetFileName(detail.FileName),
            TechnicalMessage = SanitizeTechnical(detail.TechnicalMessage)
        });

    public static bool TryParse(string? value, out MediaTaskFailureDetail? detail)
    {
        detail = null;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        try
        {
            detail = JsonSerializer.Deserialize<MediaTaskFailureDetail>(value[Prefix.Length..]);
            return detail is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string UserSummary(string? value, string fallback)
        => TryParse(value, out var detail) ? detail!.FirstLine : fallback;

    public static string SanitizeTechnical(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var sanitized = WindowsPathRegex().Replace(value, "<PATH_REDACTED>");
        return sanitized.Length <= 2000 ? sanitized : sanitized[..2000];
    }

    [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^\r\n;]+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathRegex();
}

public static class MediaTaskFailureMessages
{
    public static string UserMessage(string stage, string errorCode) => errorCode switch
    {
        ErrorCodeCatalog.SourceNotFound => "找不到源文件，或文件当前不可访问。",
        ErrorCodeCatalog.FileLocked => "文件正被其他程序占用。",
        ErrorCodeCatalog.PermissionDenied => "没有读取源文件或写入输出目录的权限。",
        ErrorCodeCatalog.SourceChanged => "处理期间源文件发生变化，已停止以保护文件。",
        ErrorCodeCatalog.UnsupportedFormat => "该文件类型目前不兼容。",
        ErrorCodeCatalog.DecodeFailed when stage == MediaTaskStages.RawDecode => "无法完成 RAW 解码。",
        ErrorCodeCatalog.DecodeFailed => "无法完成图片解码。",
        ErrorCodeCatalog.ColorProfileUnsupported => "无法转换为安全的 sRGB 输出。",
        ErrorCodeCatalog.CorruptedImage when stage == MediaTaskStages.OutputVerification => "JPG 已生成，但最终文件验证失败。",
        ErrorCodeCatalog.CorruptedImage when stage == MediaTaskStages.JpegEncode => "无法完成 JPEG 编码。",
        ErrorCodeCatalog.CorruptedImage => "图像数据不完整或无法解码。",
        ErrorCodeCatalog.HashMismatch => "JPG 已生成，但文件校验失败。",
        ErrorCodeCatalog.DiskSpaceInsufficient => "输出磁盘空间不足。",
        ErrorCodeCatalog.SourceAndDestinationSame => "输出文件与源文件不能是同一个文件。",
        ErrorCodeCatalog.DestinationNotWritable => "无法安全写入输出目录。",
        ErrorCodeCatalog.DatabaseUnavailable => "输出已生成，但任务记录未能完整保存。",
        ErrorCodeCatalog.CancelledByUser => "任务已取消，源文件保持不变。",
        _ when stage == MediaTaskStages.JpegEncode => "无法完成 JPEG 编码。",
        _ when stage == MediaTaskStages.OutputVerification => "输出生成完成，但最终验证失败。",
        _ => "处理未完成，请查看原因后重试。"
    };

    public static bool Retryable(string errorCode) => errorCode is not
        (ErrorCodeCatalog.UnsupportedFormat or ErrorCodeCatalog.SourceAndDestinationSame);
}
