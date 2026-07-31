namespace RAWSelectionAssistant.Core.Models;

public enum MatchStatus
{
    Waiting,
    Matched,
    NotFound,
    Conflict,
    ManuallyConfirmed,
    Copied,
    Skipped,
    CopyFailed,
    WaitingManualConfirmation
}

public static class MatchStatusExtensions
{
    public static string ToChinese(this MatchStatus status) => status switch
    {
        MatchStatus.Waiting => "等待解析",
        MatchStatus.Matched => "已匹配",
        MatchStatus.NotFound => "未找到",
        MatchStatus.Conflict => "存在冲突",
        MatchStatus.ManuallyConfirmed => "已手动确认",
        MatchStatus.Copied => "已复制",
        MatchStatus.Skipped => "已跳过",
        MatchStatus.CopyFailed => "复制失败",
        MatchStatus.WaitingManualConfirmation => "客户 JPG 等待确认",
        _ => "未知"
    };
}
