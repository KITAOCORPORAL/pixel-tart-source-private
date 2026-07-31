namespace RAWSelectionAssistant.Core.Models;

public enum CollectionCategory
{
    JpegOnly,
    RawOnly,
    JpegAndRaw,
    Custom
}

public enum FileCategory
{
    Jpeg,
    Raw,
    Sidecar,
    ProcessedImage,
    Custom
}

public enum MediaOverallStatus
{
    Waiting,
    CompleteMatched,
    PartialMatched,
    Conflict,
    NotFound,
    PartiallyCopied,
    FullyCopied,
    CopyFailed,
    WaitingConfirmation
}

public static class MediaEnumExtensions
{
    public static string ToChinese(this CollectionCategory category) => category switch
    {
        CollectionCategory.JpegOnly => "仅 JPG",
        CollectionCategory.RawOnly => "仅 RAW",
        CollectionCategory.JpegAndRaw => "JPG + RAW",
        CollectionCategory.Custom => "自定义格式",
        _ => "未知"
    };

    public static string ToChinese(this FileCategory category) => category switch
    {
        FileCategory.Jpeg => "JPG",
        FileCategory.Raw => "RAW",
        FileCategory.Sidecar => "附属文件",
        FileCategory.ProcessedImage => "成品图像",
        FileCategory.Custom => "其他",
        _ => "其他"
    };

    public static string ToChinese(this MediaOverallStatus status) => status switch
    {
        MediaOverallStatus.Waiting => "等待匹配",
        MediaOverallStatus.CompleteMatched => "完整匹配",
        MediaOverallStatus.PartialMatched => "部分匹配",
        MediaOverallStatus.Conflict => "存在冲突",
        MediaOverallStatus.NotFound => "完全未找到",
        MediaOverallStatus.PartiallyCopied => "部分已复制",
        MediaOverallStatus.FullyCopied => "全部已复制",
        MediaOverallStatus.CopyFailed => "复制失败",
        MediaOverallStatus.WaitingConfirmation => "等待手动确认",
        _ => "未知"
    };
}
