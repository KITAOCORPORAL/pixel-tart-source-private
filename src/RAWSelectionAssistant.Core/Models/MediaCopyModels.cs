namespace RAWSelectionAssistant.Core.Models;

public sealed record MediaCopyOutcome(
    Guid ItemId,
    string FormatKey,
    string SourcePath,
    string DestinationPath,
    MatchStatus Status,
    string ErrorMessage,
    DateTime OperationTime);

public sealed class MediaCopySummary
{
    public List<MediaCopyOutcome> Outcomes { get; } = [];
    public int CopiedCount => Outcomes.Count(x => x.Status == MatchStatus.Copied);
    public int SkippedCount => Outcomes.Count(x => x.Status == MatchStatus.Skipped);
    public int FailedCount => Outcomes.Count(x => x.Status == MatchStatus.CopyFailed);
}
