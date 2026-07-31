namespace RAWSelectionAssistant.Core.Models;

public sealed record NormalizedFileName(
    string OriginalInput,
    string DisplayName,
    string ComparisonName,
    string NumericId);
