namespace RAWSelectionAssistant.Core.Models;

public sealed class QuickToolLayout
{
    public const string CurrentSchemaVersion = "1.0";
    public string SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<string> OrderedToolIds { get; set; } = QuickToolsService.DefaultPinnedTools.ToList();
}
