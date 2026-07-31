namespace RAWSelectionAssistant.Core.Models;

public static class QuickToolsService
{
    public const int MaximumPinnedTools = 4;

    public static IReadOnlyList<string> DefaultPinnedTools { get; } = ["Workflow", "PhotoOrganize", "BatchCompress", "Toolbox"];

    public static List<string> Normalize(IEnumerable<string>? values)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LocalSplit", "Workflow", "PhotoOrganize", "BatchCompress", "Watermark",
            "DeleteRejects", "FtpTool", "BatchRename", "BatchConvert", "Collage", "Toolbox"
        };
        var result = (values ?? DefaultPinnedTools)
            .Where(allowed.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumPinnedTools)
            .ToList();
        return result;
    }
}
