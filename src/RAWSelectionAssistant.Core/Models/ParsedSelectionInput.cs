namespace RAWSelectionAssistant.Core.Models;

public sealed record ParsedSelectionInput(string OriginalInput, string CustomerInputFilePath = "");
