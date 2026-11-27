using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>A browser-safe projection of the saved WPF document. It never contains markup or source paths.</summary>
public sealed record PlanningShareRichSnapshot(Guid ProjectId, Guid DocumentId, string PlanSha256,
    IReadOnlyList<PlanningShareRichBlock> Blocks, IReadOnlyList<string> Losses)
{
    public const string FileName = "share-rich.json";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

public sealed record PlanningShareRichBlock(string Type, string Text = "", string? Align = null,
    IReadOnlyList<PlanningShareRichMark>? Marks = null, PlanningShareRichTable? NativeTable = null,
    PlanningShareRichImage? NativeImage = null, bool? PageBreakBefore = null);

public sealed record PlanningShareRichMark(int Start, int End, bool? Bold, bool? Italic, bool? Underline,
    string? Color, double? Size, string? Highlight, string? Font);
public sealed record PlanningShareRichTable(IReadOnlyList<PlanningShareRichRow> Rows, bool RepeatHeader);
public sealed record PlanningShareRichRow(IReadOnlyList<PlanningShareRichCell> Cells);
public sealed record PlanningShareRichCell(int ColSpan, int RowSpan, IReadOnlyList<PlanningShareRichBlock> Paragraphs);
public sealed record PlanningShareRichImage(Guid AssetId, string Caption, double Width, double Height);

public sealed record PlanningShareSectionNotice(string Section, string Item, string Message)
{
    public const string Extension = "PlanningShareSectionNoticesV1";
}
