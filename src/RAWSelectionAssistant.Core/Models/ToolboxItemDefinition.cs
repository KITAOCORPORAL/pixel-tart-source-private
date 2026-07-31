namespace RAWSelectionAssistant.Core.Models;

public sealed record ToolboxItemDefinition(string Id, string Title, string Description, string TargetPageKey);

public sealed record PhotoGroup(Guid Id, string Name, string CoverImagePath, int Count, IReadOnlyList<string> Items);
