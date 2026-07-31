namespace RAWSelectionAssistant.Core.Models;

public sealed class RawFileEntry
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public string SourceRoot { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string NumericId { get; set; } = string.Empty;
}
