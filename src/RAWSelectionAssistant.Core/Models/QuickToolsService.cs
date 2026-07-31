namespace RAWSelectionAssistant.Core.Models;

public static class QuickToolsService
{
    // “工具箱”始终独立可达，不占工作台普通快捷位。
    public const int MaximumPinnedTools = 3;

    public static IReadOnlyList<string> DefaultPinnedTools { get; } =
        [ToolId.Workflow.ToString(), ToolId.PhotoOrganize.ToString(), ToolId.BatchCompress.ToString()];

    public static List<string> Normalize(IEnumerable<string>? values)
    {
        var source = values ?? DefaultPinnedTools;
        var result = new List<string>();
        var seen = new HashSet<ToolId>();

        foreach (var value in source)
        {
            if (!ToolRegistry.TryGet(value, out var definition) || !definition.CanPin || !seen.Add(definition.Id))
            {
                continue;
            }

            result.Add(definition.SettingsId);
            if (result.Count == MaximumPinnedTools)
            {
                break;
            }
        }

        return result;
    }
}
