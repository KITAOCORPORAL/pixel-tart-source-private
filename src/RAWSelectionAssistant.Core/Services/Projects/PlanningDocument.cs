namespace RAWSelectionAssistant.Core.Services.Projects;

// Proposal status is intentionally separate from booking and shot execution status.
public sealed record PlanningDocument
{
    public int FoundationVersion { get; init; }
    public IReadOnlyList<PlanningShotColumn> ShotColumns { get; init; } = [];
    public PlanningDocument EnsureIdentity(Guid projectId)
    {
        if (projectId == Guid.Empty || FoundationVersion is < 0 or > 1 || ShotColumns is null || References is null || Attachments is null)
            throw new System.IO.InvalidDataException("Invalid planning foundation schema.");
        foreach (var column in ShotColumns) column.Validate();
        if (ShotColumns.Select(c => c.Id).Distinct().Count() != ShotColumns.Count) throw new System.IO.InvalidDataException("Duplicate custom column identity.");
        // Legacy plans have exactly one document per project. A namespaced deterministic ID
        // is stable even across read-only opens before the first migration save.
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Kitao.PlanningDocument.v1:" + projectId.ToString("N")));
        var package = Package ?? new PlanningDocumentPackage { ProjectId = projectId, DocumentId = new Guid(bytes.AsSpan(0, 16)), Body = Body };
        package.Validate();
        if (package.ProjectId != projectId) throw new System.IO.InvalidDataException("Document project mismatch.");
        return this with { FoundationVersion = 1, Package = package };
    }
    public PlanningDocumentPackage? Package { get; init; }
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; init; }
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Body { get; init; } = "";
    public string CustomBody { get; init; } = "";
    public string Status { get; init; } = "草稿";
    public string Location { get; init; } = "";
    public string People { get; init; } = "";
    public DateTimeOffset? ShootDate { get; init; }
    public IReadOnlyList<PlanningDocumentReference> References { get; init; } = [];
    public IReadOnlyList<PlanningAttachment> Attachments { get; init; } = [];
}

public sealed record PlanningDocumentReference(ProjectShotReference Source, string Category = "其他",
    bool IsHero = false, bool IsMoodboard = false, string Group = "整体氛围")
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; init; }
}
public sealed record PlanningAttachment(Guid Id, string Path, string Name)
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; init; }
}
public sealed record PlanningDocumentDraft(Guid ProjectId, PlanningDocument Document, PlanningSummary Summary, IReadOnlyList<ProjectShot>? Shots = null);

public sealed record PlanningShotColumn(Guid Id, string Name, bool Archived = false)
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; init; }
    public PlanningShotColumn Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 80) throw new System.IO.InvalidDataException("Invalid shot column.");
        return this;
    }
}
