namespace RAWSelectionAssistant.Core.Models;

public static class ProductToolboxPolicy
{
    public const int MaximumPinnedTools = 4;

    private static readonly IReadOnlyList<ToolDefinition> ProductDefinitions =
    [
        new(ToolId.PhotoOrganize, "整理图片", "按日期、相机或自定义规则安全整理照片。", "ToolIconOrganize", "PhotoGrouping", true, true, FeatureAvailability.Production, ToolMenuGroup.Organize, 10),
        new(ToolId.RawToJpeg, "RAW 转 JPG", "只读解码 RAW 并输出 JPG，源文件始终保留。", "ToolIconRawToJpeg", "RawToJpeg", true, true, FeatureAvailability.Production, ToolMenuGroup.Output, 20),
        new(ToolId.BatchCompress, "批量压缩", "将照片压缩到独立输出目录，不覆盖源文件。", "ToolIconBatchCompress", "BatchCompress", true, true, FeatureAvailability.Production, ToolMenuGroup.Output, 30),
        new(ToolId.Collage, "拼图", "使用模板制作拼图并安全导出到新文件。", "ToolIconCollage", "Collage", true, true, FeatureAvailability.Production, ToolMenuGroup.Organize, 40),
        new(ToolId.Watermark, "批量水印", "预览交付照片的文字或图片水印。", "ToolIconWatermark", "Watermark", true, true, FeatureAvailability.Preview, ToolMenuGroup.Output, 50)
    ];

    private static readonly IReadOnlyDictionary<ToolId, ToolDefinition> ById =
        ProductDefinitions.ToDictionary(item => item.Id);

    public static IReadOnlyList<ToolDefinition> Catalog { get; } = ProductDefinitions;

    public static IReadOnlyList<ToolDefinition> ProductionCatalog { get; } =
        ProductDefinitions.Where(item => item.Availability == FeatureAvailability.Production).ToArray();

    public static IReadOnlyList<ToolDefinition> Pinnable { get; } =
        ProductDefinitions.Where(item => item.CanPin).ToArray();

    public static IReadOnlyList<string> DefaultPinnedTools { get; } =
        [ToolId.PhotoOrganize.ToString(), ToolId.RawToJpeg.ToString(), ToolId.BatchCompress.ToString(), ToolId.Collage.ToString()];

    public static ToolDefinition Get(ToolId id) => ById[id];

    public static bool TryGet(string? id, out ToolDefinition definition)
    {
        if (Enum.TryParse<ToolId>(id, true, out var parsed) && ById.TryGetValue(parsed, out var found))
        {
            definition = found;
            return true;
        }

        definition = default!;
        return false;
    }

    public static List<string> Normalize(IEnumerable<string>? values)
    {
        var source = values ?? DefaultPinnedTools;
        var result = new List<string>(MaximumPinnedTools);
        var seen = new HashSet<ToolId>();
        foreach (var value in source)
        {
            if (!TryGet(value, out var definition) || !definition.CanPin ||
                definition.Availability != FeatureAvailability.Production || !seen.Add(definition.Id))
            {
                continue;
            }

            result.Add(definition.SettingsId);
            if (result.Count == MaximumPinnedTools) break;
        }

        return result;
    }

    public static List<string> Add(IEnumerable<string>? values, string toolId)
    {
        var result = Normalize(values);
        if (result.Count >= MaximumPinnedTools || result.Contains(toolId, StringComparer.OrdinalIgnoreCase) ||
            !TryGet(toolId, out var definition) || !definition.CanPin ||
            definition.Availability != FeatureAvailability.Production)
        {
            return result;
        }

        result.Add(definition.SettingsId);
        return result;
    }

    public static List<string> Remove(IEnumerable<string>? values, string toolId) =>
        Normalize(values).Where(value => !string.Equals(value, toolId, StringComparison.OrdinalIgnoreCase)).ToList();

    public static List<string> Move(IEnumerable<string>? values, string toolId, int offset)
    {
        var result = Normalize(values);
        var index = result.FindIndex(value => string.Equals(value, toolId, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return result;
        var destination = Math.Clamp(index + offset, 0, result.Count - 1);
        if (destination != index) (result[index], result[destination]) = (result[destination], result[index]);
        return result;
    }
}
