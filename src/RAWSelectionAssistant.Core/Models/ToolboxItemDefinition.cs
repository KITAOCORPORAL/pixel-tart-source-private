namespace RAWSelectionAssistant.Core.Models;

public sealed record ToolboxItemDefinition(ToolDefinition Definition)
{
    public ToolId Id => Definition.Id;
    public string DisplayName => Definition.DisplayName;
    public string Description => Definition.Description;
    public string TargetPageKey => Definition.TargetPageKey;
}

public sealed record PhotoGroup(Guid Id, string Name, string CoverImagePath, int Count, IReadOnlyList<string> Items);
