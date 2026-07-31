namespace RAWSelectionAssistant.Core.Models;

public sealed class RawIndexSnapshot
{
    public List<RawFileEntry> Files { get; init; } = [];
    public Dictionary<string, List<RawFileEntry>> ByFullName { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<RawFileEntry>> ByNumericId { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int SkippedDirectoryCount { get; set; }
}
