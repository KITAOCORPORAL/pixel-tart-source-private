using System.Collections.Concurrent;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record PlanningSummary(
    string ShootGoal = "",
    IReadOnlyList<string>? Keywords = null,
    string ClientRequirements = "",
    string MustCapture = "",
    string Notes = "",
    string OutputPurpose = "")
{
    public PlanningSummary Normalize() => this with
    {
        ShootGoal = ShootGoal.Trim(),
        Keywords = (Keywords ?? []).Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray(),
        ClientRequirements = ClientRequirements.Trim(), MustCapture = MustCapture.Trim(), Notes = Notes.Trim(), OutputPurpose = OutputPurpose.Trim()
    };
}

public enum PlanningVisualLinkKind { InspirationBoard, FreeCanvas }
public enum PlanningCanvasRole { Overview, Scene, Styling, Lighting, Pose, Storyboard, Scratch }

public sealed record PlanningVisualLink(Guid LinkId, PlanningVisualLinkKind Kind, Guid TargetId, string Title,
    PlanningCanvasRole Role = PlanningCanvasRole.Overview, DateTimeOffset? LinkedAt = null)
{
    public PlanningVisualLink Normalize()
    {
        if (LinkId == Guid.Empty || TargetId == Guid.Empty || string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("A visual link requires stable identities and a title.");
        return this with { Title = Title.Trim(), LinkedAt = LinkedAt ?? DateTimeOffset.UtcNow };
    }
}

/// <summary>Non-owning relationship. It never owns, moves, renames or deletes an asset source.</summary>
public sealed record ShotCaptureRelation(Guid? LibraryId, Guid AssetId, Guid ShotId, Guid ProjectId, Guid? BookingId,
    DateTimeOffset CapturedAt, long ExecutionRevision, string SourceKind = "Library")
{
    public string StableKey => $"{SourceKind}:{LibraryId?.ToString("N")??"none"}:{AssetId:N}:{ShotId:N}";
    public ShotCaptureRelation Validate()
    {
        if (LibraryId == Guid.Empty || AssetId == Guid.Empty || ShotId == Guid.Empty || ProjectId == Guid.Empty || ExecutionRevision < 0 || string.IsNullOrWhiteSpace(SourceKind))
            throw new ArgumentException("A capture relation requires stable asset, project and shot identities.");
        return this;
    }
}

public sealed record PlanningProjectState(Guid ProjectId, Guid? BookingId = null, PlanningSummary? Summary = null,
    Guid? CurrentShotId = null, IReadOnlyList<PlanningVisualLink>? VisualLinks = null,
    IReadOnlyList<ShotCaptureRelation>? CapturedAssets = null, long Revision = 0,
    DateTimeOffset? UpdatedAt = null, int Version = 1)
{
    public PlanningProjectState Normalize()
    {
        if (ProjectId == Guid.Empty || Revision < 0 || Version != 1) throw new ArgumentException("Planning state identity, revision or version is invalid.");
        var links = (VisualLinks ?? []).Select(link => link.Normalize()).GroupBy(link => link.LinkId).Select(group => group.First()).ToArray();
        var captures = (CapturedAssets ?? []).Select(item => item.Validate()).ToArray();
        if (captures.Any(item => item.ProjectId != ProjectId)) throw new ArgumentException("Captured assets must belong to the planning project.");
        return this with { Summary = (Summary ?? new()).Normalize(), VisualLinks = links, CapturedAssets = captures.GroupBy(item => item.StableKey, StringComparer.Ordinal).Select(group => group.First()).ToArray(), UpdatedAt = UpdatedAt ?? DateTimeOffset.UtcNow };
    }
}

public sealed class PlanningProjectStore(string directory)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private string FilePath(Guid projectId) => Path.Combine(Path.GetFullPath(directory), $"{projectId:N}.planning.json");

    public async Task<PlanningProjectState> LoadAsync(Guid projectId, CancellationToken token = default)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.", nameof(projectId));
        var path = FilePath(projectId); var gate = Gates.GetOrAdd(path, _ => new(1, 1)); await gate.WaitAsync(token).ConfigureAwait(false);
        try { return await ReadAsync(projectId, path, token).ConfigureAwait(false); } finally { gate.Release(); }
    }

    public Task SaveAsync(PlanningProjectState value, CancellationToken token = default) => UpdateAsync(value.ProjectId, _ => value, token);
    public Task UpdateSummaryAsync(Guid projectId, PlanningSummary summary, CancellationToken token = default) => UpdateAsync(projectId, state => state with { Summary = summary }, token);
    public Task SetCurrentShotAsync(Guid projectId, Guid? shotId, CancellationToken token = default) => UpdateAsync(projectId, state => state with { CurrentShotId = shotId }, token);
    public Task AddVisualLinkAsync(Guid projectId, PlanningVisualLink link, CancellationToken token = default) => UpdateAsync(projectId, state => state with { VisualLinks = state.VisualLinks!.Where(item => item.LinkId != link.LinkId).Append(link).ToArray() }, token);
    public Task RemoveVisualLinkAsync(Guid projectId, Guid linkId, CancellationToken token = default) => UpdateAsync(projectId, state => state with { VisualLinks = state.VisualLinks!.Where(item => item.LinkId != linkId).ToArray() }, token);
    public Task AddCaptureRelationAsync(ShotCaptureRelation relation, CancellationToken token = default) => UpdateAsync(relation.ProjectId, state => state with { CapturedAssets = state.CapturedAssets!.Append(relation).ToArray() }, token);

    private static async Task<PlanningProjectState> ReadAsync(Guid projectId, string path, CancellationToken token)
    {
        if (!File.Exists(path)) return new PlanningProjectState(projectId).Normalize();
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<PlanningProjectState>(stream, cancellationToken: token).ConfigureAwait(false) ?? throw new InvalidDataException("Planning state is empty.");
        if (value.ProjectId != projectId) throw new InvalidDataException("Planning project identity does not match the file.");
        return value.Normalize();
    }

    private async Task UpdateAsync(Guid projectId, Func<PlanningProjectState, PlanningProjectState> update, CancellationToken token)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.", nameof(projectId));
        var path = FilePath(projectId); var gate = Gates.GetOrAdd(path, _ => new(1, 1)); await gate.WaitAsync(token).ConfigureAwait(false);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var current = await ReadAsync(projectId, path, token).ConfigureAwait(false);
            var next = update(current) with { Revision = current.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow };
            next = next.Normalize(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(stream, next, cancellationToken: token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false); stream.Flush(true); }
            token.ThrowIfCancellationRequested(); File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
    }
}

public sealed record ShootExecutionContext(Guid ProjectId, Guid? BookingId, Guid? CurrentShotId,
    IReadOnlyList<Guid> ShotSequence, IReadOnlyList<ProjectShotReference> ShotReferences,
    Guid? EffectiveReferenceLookId, ProjectPaletteReference? ProjectPalette, ProjectToneTarget? ToneTarget,
    IReadOnlyList<Guid> PinnedReferenceIds, string? ShootNotes, long Revision)
{
    public ShotCaptureRelation BindCapture(Guid libraryId, Guid assetId, DateTimeOffset capturedAt)
    {
        if (CurrentShotId is not Guid shotId) throw new InvalidOperationException("No current shot was frozen for this capture.");
        return new ShotCaptureRelation(libraryId, assetId, shotId, ProjectId, BookingId, capturedAt, Revision).Validate();
    }
    public ShotCaptureRelation BindTetherCapture(Guid tetherAssetId, DateTimeOffset capturedAt)
    {
        if (CurrentShotId is not Guid shotId) throw new InvalidOperationException("No current shot was frozen for this capture.");
        return new ShotCaptureRelation(null, tetherAssetId, shotId, ProjectId, BookingId, capturedAt, Revision, "Tether").Validate();
    }
}

public sealed class PlanningExecutionContextService(ProjectShotStore shots, PlanningProjectStore planning,
    ProjectVisualReferenceStore visuals, ReferenceLookStore looks)
{
    public async Task<ShootExecutionContext> BuildAsync(Guid projectId, Guid? sessionLookId = null, CancellationToken token = default)
    {
        var state = await planning.LoadAsync(projectId, token).ConfigureAwait(false);
        var catalog = await shots.LoadAsync(projectId, token).ConfigureAwait(false);
        var active = catalog.Shots.Where(shot => !shot.IsArchived).OrderBy(shot => shot.Order).ToArray();
        var current = active.FirstOrDefault(shot => shot.ShotId == state.CurrentShotId) ?? active.FirstOrDefault();
        var visual = await visuals.LoadAsync(projectId, token).ConfigureAwait(false);
        var lookCatalog = await looks.LoadAsync(token).ConfigureAwait(false);
        lookCatalog.ProjectDefaults.TryGetValue(projectId, out var projectDefaultLookId);
        var effective = ReferenceLookResolver.Resolve(current?.ReferenceLookId, projectDefaultLookId == Guid.Empty ? null : projectDefaultLookId, sessionLookId);
        return new(projectId, state.BookingId, current?.ShotId, active.Select(shot => shot.ShotId).ToArray(), current?.References ?? [], effective,
            visual.DefaultPalette, visual.DefaultToneTarget, current?.References.Where(reference => reference.IsPinned).Select(reference => reference.ReferenceId).ToArray() ?? [], current?.Notes, state.Revision);
    }
}
