namespace RAWSelectionAssistant.Core.Models;

public sealed record OperationProgress(
    string Stage,
    string CurrentItem,
    long Processed,
    long Total = 0,
    double Percent = 0);
