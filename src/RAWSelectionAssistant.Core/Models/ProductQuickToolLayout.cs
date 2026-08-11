namespace RAWSelectionAssistant.Core.Models;

public sealed class ProductQuickToolLayout
{
    public const string CurrentSchemaVersion = "1.0";
    public string SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<string> OrderedToolIds { get; set; } = ProductToolboxPolicy.DefaultPinnedTools.ToList();
}
