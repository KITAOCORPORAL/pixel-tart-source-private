namespace RAWSelectionAssistant.Core.Models;

public static class ToolRegistry
{
    private static readonly IReadOnlyList<ToolDefinition> Definitions =
    [
        new(ToolId.LocalSplit, "本地分片", "匹配本地 JPG、RAW 及相关文件。", "ToolIconLocalSplit", "LocalSplit", true, true, FeatureAvailability.Production, ToolMenuGroup.Workflow, 10),
        new(ToolId.Workflow, "归片工作区", "核对 JPG、RAW、冲突与复制结果。", "ToolIconWorkflow", "Workflow", true, true, FeatureAvailability.Production, ToolMenuGroup.Workflow, 20),
        new(ToolId.PhotoOrganize, "整理图片", "按日期、相机、格式或自定义规则安全分组并复制照片。", "ToolIconOrganize", "PhotoGrouping", true, true, FeatureAvailability.Production, ToolMenuGroup.Organize, 30),
        new(ToolId.Collage, "拼图", "使用数据驱动模板制作拼图、纵向长图和横向长图。", "ToolIconCollage", "Collage", true, true, FeatureAvailability.Production, ToolMenuGroup.Organize, 40),
        new(ToolId.BatchCompress, "批量压缩", "预设尺寸、质量与元数据保留方式。", "ToolIconBatchCompress", "BatchCompress", true, true, FeatureAvailability.Production, ToolMenuGroup.Output, 50),
        new(ToolId.Watermark, "批量水印", "为交付照片配置文字或图片水印。", "ToolIconWatermark", "Watermark", true, true, FeatureAvailability.Preview, ToolMenuGroup.Output, 60),
        new(ToolId.DeleteRejects, "删废片", "浏览、标记并安全确认待删除照片。", "ToolIconDeleteRejects", "DeleteRejects", true, false, FeatureAvailability.Hidden, ToolMenuGroup.Organize, 70),
        new(ToolId.FtpTool, "FTP 工具", "配置本地文件和远程目录传输任务。", "ToolIconFtp", "FtpTool", true, false, FeatureAvailability.Hidden, ToolMenuGroup.Transfer, 80),
        new(ToolId.BatchRename, "批量重命名", "按模板预览并统一重命名照片。", "ToolIconBatchRename", "BatchRename", true, false, FeatureAvailability.Hidden, ToolMenuGroup.Output, 90),
        new(ToolId.BatchConvert, "批量转档", "统一输出 JPEG、PNG 或 TIFF 格式。", "ToolIconBatchConvert", "BatchConvert", true, false, FeatureAvailability.Hidden, ToolMenuGroup.Output, 100),
        new(ToolId.Toolbox, "工具箱", "打开完整工具集合。", "ToolIconToolbox", "Toolbox", false, true, FeatureAvailability.Production, ToolMenuGroup.More, 110, "打开工具箱")
    ];

    private static readonly IReadOnlyDictionary<ToolId, ToolDefinition> ById =
        Definitions.ToDictionary(definition => definition.Id);

    static ToolRegistry()
    {
        if (Definitions.Select(definition => definition.Id).Distinct().Count() != Definitions.Count)
        {
            throw new InvalidOperationException("工具注册表包含重复 ToolId。");
        }

        if (Definitions.Select(definition => definition.TargetPageKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Definitions.Count)
        {
            throw new InvalidOperationException("工具注册表包含重复 TargetPageKey。");
        }
    }

    public static IReadOnlyList<ToolDefinition> All { get; } = Definitions.OrderBy(definition => definition.SortOrder).ToArray();

    public static IReadOnlyList<ToolDefinition> Catalog { get; } = All.Where(definition => definition.Id != ToolId.Toolbox).ToArray();

    public static IReadOnlyList<ToolDefinition> ReleaseCatalog { get; } = Catalog
        .Where(definition => definition.Availability != FeatureAvailability.Hidden)
        .ToArray();

    public static IReadOnlyList<ToolDefinition> Pinnable { get; } = All.Where(definition => definition.CanPin).ToArray();

    public static IReadOnlyList<ToolDefinition> ReleasePinnable { get; } = ReleaseCatalog
        .Where(definition => definition.CanPin && definition.Availability == FeatureAvailability.Production)
        .ToArray();

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
}
