namespace RAWSelectionAssistant.Core.Models;

public enum ToolId
{
    LocalSplit,
    Workflow,
    PhotoOrganize,
    Collage,
    BatchCompress,
    Watermark,
    DeleteRejects,
    FtpTool,
    BatchRename,
    BatchConvert,
    Toolbox
}

public enum ToolMaturity
{
    Available,
    Preview
}

public enum ToolMenuGroup
{
    Workflow,
    Organize,
    Output,
    Transfer,
    More
}

public sealed record ToolDefinition(
    ToolId Id,
    string DisplayName,
    string Description,
    string IconResourceKey,
    string TargetPageKey,
    bool CanPin,
    bool IsAvailable,
    ToolMaturity Maturity,
    ToolMenuGroup MenuGroup,
    int SortOrder,
    string? MenuLabel = null)
{
    public string SettingsId => Id.ToString();
    public string EffectiveMenuLabel => string.IsNullOrWhiteSpace(MenuLabel) ? DisplayName : MenuLabel;
}
