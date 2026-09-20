namespace RAWSelectionAssistant.Core.Services.Projects;

// Proposal status is intentionally separate from booking and shot execution status.
public sealed record PlanningDocument
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Body { get; init; } = "";
    public string Status { get; init; } = "草稿";
    public string Location { get; init; } = "";
    public string People { get; init; } = "";
    public DateTimeOffset? ShootDate { get; init; }
    public IReadOnlyList<PlanningDocumentReference> References { get; init; } = [];
    public IReadOnlyList<PlanningAttachment> Attachments { get; init; } = [];
}

public sealed record PlanningDocumentReference(ProjectShotReference Source, string Category = "其他",
    bool IsHero = false, bool IsMoodboard = false, string Group = "整体氛围");
public sealed record PlanningAttachment(Guid Id, string Path, string Name);
public sealed record PlanningDocumentDraft(Guid ProjectId, PlanningDocument Document, PlanningSummary Summary, IReadOnlyList<ProjectShot>? Shots = null);
