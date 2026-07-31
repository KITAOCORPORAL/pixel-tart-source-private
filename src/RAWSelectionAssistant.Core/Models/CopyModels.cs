namespace RAWSelectionAssistant.Core.Models;

public sealed record CopyOutcome(
    Guid ItemId,
    string SourcePath,
    string DestinationPath,
    MatchStatus Status,
    string ErrorMessage,
    DateTime OperationTime);

public sealed class CopySummary
{
    public List<CopyOutcome> Outcomes { get; } = [];
    public int CopiedCount => Outcomes.Count(x => x.Status == MatchStatus.Copied);
    public int SkippedCount => Outcomes.Count(x => x.Status == MatchStatus.Skipped);
    public int FailedCount => Outcomes.Count(x => x.Status == MatchStatus.CopyFailed);
}
