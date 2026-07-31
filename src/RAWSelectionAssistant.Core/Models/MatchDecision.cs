namespace RAWSelectionAssistant.Core.Models;

public sealed record MatchDecision(
    Guid ItemId,
    string NormalizedName,
    string NumericId,
    MatchStatus Status,
    RawFileEntry? SelectedRaw,
    IReadOnlyList<RawFileEntry> Candidates,
    bool IsDuplicate,
    string Note);
