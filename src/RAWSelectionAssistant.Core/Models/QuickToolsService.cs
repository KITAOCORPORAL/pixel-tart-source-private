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

    public static List<string> Move(IEnumerable<string>? values, string toolId, int offset)
    {
        var result = Normalize(values);
        var index = result.FindIndex(x => string.Equals(x, toolId, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return result;
        var destination = Math.Clamp(index + offset, 0, result.Count - 1);
        if (destination == index) return result;
        (result[index], result[destination]) = (result[destination], result[index]);
        return result;
    }

    public static List<string> Remove(IEnumerable<string>? values, string toolId) =>
        Normalize(values).Where(x => !string.Equals(x, toolId, StringComparison.OrdinalIgnoreCase)).ToList();

    public static List<string> Add(IEnumerable<string>? values, string toolId)
    {
        var result = Normalize(values);
        if (result.Count >= MaximumPinnedTools || result.Contains(toolId, StringComparer.OrdinalIgnoreCase) ||
            !ToolRegistry.TryGet(toolId, out var definition) || !definition.CanPin) return result;
        result.Add(definition.SettingsId);
        return result;
    }
}
