using System.Text.Json;
using System.Text.Json.Serialization;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record PlanningAssetReference(Guid LibraryId, Guid AssetId, string? ContentHash = null);
public sealed record PlanningCanvasRevision(Guid RevisionId, Guid CanvasId, DateTimeOffset CapturedAt,
    CanvasDocument EditableSnapshot, string? PreviewPngBase64 = null);
public sealed record PlanningDocumentPackage
{
    public int Version { get; init; } = 1;
    public Guid DocumentId { get; init; } = Guid.NewGuid();
    public Guid ProjectId { get; init; }
    public string Body { get; init; } = "";
    public PlanningRichLayout? RichLayout { get; init; }
    public string? RichTextPackageBase64 { get; init; }
    public IReadOnlyList<PlanningAssetReference> Assets { get; init; } = [];
    public IReadOnlyList<Guid> ShotIds { get; init; } = [];
    public IReadOnlyList<PlanningCanvasRevision> CanvasRevisions { get; init; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
    public PlanningDocumentPackage Validate()
    {
        if (Version != 1 || DocumentId == Guid.Empty || ProjectId == Guid.Empty) throw new InvalidDataException("不支持的文档包版本或身份。");
        if(RichLayout is {} layout){layout.Validate();}
        if(RichTextPackageBase64?.Length>64*1024*1024)throw new InvalidDataException("文档正文资源过大。");
        if (Assets is null || ShotIds is null || CanvasRevisions is null) throw new InvalidDataException("文档包集合不能为 null。");
        if (Assets.Any(a => a.LibraryId == Guid.Empty || a.AssetId == Guid.Empty) || ShotIds.Any(id => id == Guid.Empty)) throw new InvalidDataException("文档引用身份无效。");
        if (CanvasRevisions.Any(x => x.RevisionId == Guid.Empty || x.CanvasId == Guid.Empty || x.EditableSnapshot is null || x.EditableSnapshot.CanvasId != x.CanvasId)) throw new InvalidDataException("画布版本身份无效。");
        if (CanvasRevisions.Any(x => x.PreviewPngBase64?.Length > 32 * 1024 * 1024)) throw new InvalidDataException("画布快照超过允许大小。");
        return this;
    }
    public PlanningDocumentPackage CaptureCanvas(CanvasDocument canvas, string? preview = null)
    {
        if (canvas.ProjectId is Guid project && project != ProjectId) throw new InvalidDataException("画布不属于此项目。");
        // Serialize to detach every object/list: later canvas mutation cannot rewrite history.
        var snapshot = JsonSerializer.Deserialize<CanvasDocument>(JsonSerializer.Serialize(canvas))!;
        return this with { CanvasRevisions = CanvasRevisions.Append(new(Guid.NewGuid(), canvas.CanvasId, DateTimeOffset.UtcNow, snapshot, preview)).ToArray() };
    }
}

public sealed record PlanningReferenceOutcome(Guid ReferenceId, Guid LibraryId, Guid AssetId, DateTimeOffset LinkedAt)
{
    public PlanningReferenceOutcome Validate()
    {
        if (ReferenceId == Guid.Empty || LibraryId == Guid.Empty || AssetId == Guid.Empty) throw new InvalidDataException("参考与成片关系需要明确身份。");
        return this;
    }
}
