using System.Collections.Concurrent;
using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.Projects;

public enum ProjectShotStatus { NotStarted, InProgress, Completed, Skipped }
public enum ShotReferenceKind { Lighting, Pose, Storyboard, Styling, General }
public enum PoseExecutionStatus { NotShot, Current, Completed }

/// <summary>A non-owning execution reference. Library items retain their stable library/asset identity;
/// external material retains only its managed reference. Source image bytes are never stored here.</summary>
public sealed record ProjectShotReference(
    Guid ReferenceId,
    ShotReferenceKind Kind,
    Guid? LibraryId = null,
    Guid? AssetId = null,
    string? ExternalReference = null,
    string? Title = null,
    string? Note = null,
    PoseExecutionStatus PoseStatus = PoseExecutionStatus.NotShot,
    bool IsPinned = false,
    IReadOnlyDictionary<string, string>? Details = null)
{
    public ProjectShotReference Normalize()
    {
        var isLibrary = LibraryId is not null && AssetId is not null;
        var isExternal = !string.IsNullOrWhiteSpace(ExternalReference);
        if (ReferenceId == Guid.Empty || isLibrary == isExternal || (LibraryId is null) != (AssetId is null))
            throw new ArgumentException("A shot reference requires one stable library source or one external managed reference.");
        return this with
        {
            ExternalReference = isExternal ? ExternalReference!.Trim() : null,
            Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
            Details = Details is null ? null : new Dictionary<string, string>(Details, StringComparer.Ordinal)
        };
    }
}

public sealed record ProjectShot(
    Guid ShotId,
    Guid ProjectId,
    int Order,
    string Name,
    ProjectShotStatus Status,
    string? Notes,
    Guid? ReferenceLookId,
    IReadOnlyList<ProjectShotReference> References,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version = 1,
    int EstimatedMinutes = 0,
    string? Scene = null,
    DateTimeOffset? CapturedAt = null,
    bool IsArchived = false)
{
    public ProjectShot Normalize()
    {
        if (ShotId == Guid.Empty || ProjectId == Guid.Empty || Order < 0 || EstimatedMinutes is < 0 or > 1440 || string.IsNullOrWhiteSpace(Name) || Version != 1)
            throw new ArgumentException("Shot identity, project, order and name must be valid.");
        var references = References.Select(reference => reference.Normalize()).ToArray();
        if (references.Select(reference => reference.ReferenceId).Distinct().Count() != references.Length)
            throw new ArgumentException("Shot reference identities must be unique.");
        return this with { Name = Name.Trim(), Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(), Scene = string.IsNullOrWhiteSpace(Scene) ? null : Scene.Trim(), References = references };
    }
}

public sealed record ProjectShotCatalog(Guid ProjectId, IReadOnlyList<ProjectShot> Shots, int Version = 1);

public sealed class ProjectShotStore(string directory)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private string FilePath(Guid projectId) => Path.Combine(Path.GetFullPath(directory), $"{projectId:N}.shots.json");

    public async Task<ProjectShotCatalog> LoadAsync(Guid projectId, CancellationToken token = default)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.", nameof(projectId));
        var path = FilePath(projectId); var gate = Gates.GetOrAdd(path, _ => new(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try { return await ReadAsync(projectId, path, token).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    public Task SaveAsync(ProjectShot shot, CancellationToken token = default)
    {
        shot = shot.Normalize();
        return UpdateAsync(shot.ProjectId, catalog => catalog with
        {
            Shots = catalog.Shots.Where(item => item.ShotId != shot.ShotId).Append(shot)
                .OrderBy(item => item.Order).ThenBy(item => item.CreatedAt).ToArray()
        }, token);
    }

    public Task RemoveAsync(Guid projectId, Guid shotId, CancellationToken token = default) =>
        UpdateAsync(projectId, catalog => catalog with { Shots = catalog.Shots.Where(item => item.ShotId != shotId).ToArray() }, token);

    public Task SaveCatalogAsync(ProjectShotCatalog catalog, CancellationToken token = default)
    {
        if (catalog.Version != 1 || catalog.ProjectId == Guid.Empty || catalog.Shots.Any(shot => shot.ProjectId != catalog.ProjectId))
            throw new ArgumentException("Shot catalog identity or version is invalid.", nameof(catalog));
        var normalized = catalog.Shots.Select(shot => shot.Normalize()).OrderBy(shot => shot.Order).ToArray();
        if (normalized.Select(shot => shot.ShotId).Distinct().Count() != normalized.Length)
            throw new ArgumentException("Shot identities must be unique.", nameof(catalog));
        return UpdateAsync(catalog.ProjectId, _ => catalog with { Shots = normalized }, token);
    }

    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedShotIds, CancellationToken token = default) =>
        UpdateAsync(projectId, catalog =>
        {
            var active = catalog.Shots.Where(shot => !shot.IsArchived).ToDictionary(shot => shot.ShotId);
            if (orderedShotIds.Count != active.Count || orderedShotIds.Distinct().Count() != active.Count || orderedShotIds.Any(id => !active.ContainsKey(id)))
                throw new ArgumentException("The reorder list must contain every active shot exactly once.", nameof(orderedShotIds));
            var order = orderedShotIds.Select((id, index) => active[id] with { Order = index, UpdatedAt = DateTimeOffset.UtcNow });
            return catalog with { Shots = order.Concat(catalog.Shots.Where(shot => shot.IsArchived)).ToArray() };
        }, token);

    private static async Task<ProjectShotCatalog> ReadAsync(Guid projectId, string path, CancellationToken token)
    {
        if (!File.Exists(path)) return new(projectId, []);
        await using var stream = File.OpenRead(path);
        var catalog = await JsonSerializer.DeserializeAsync<ProjectShotCatalog>(stream, cancellationToken: token).ConfigureAwait(false)
            ?? throw new InvalidDataException("Shot catalog is empty.");
        if (catalog.ProjectId != projectId || catalog.Version != 1 || catalog.Shots.Any(shot => shot.ProjectId != projectId))
            throw new InvalidDataException("Shot catalog identity or version is invalid.");
        return catalog with { Shots = catalog.Shots.Select(shot => shot.Normalize()).OrderBy(shot => shot.Order).ToArray() };
    }

    private async Task UpdateAsync(Guid projectId, Func<ProjectShotCatalog, ProjectShotCatalog> update, CancellationToken token)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.", nameof(projectId));
        var path = FilePath(projectId); var gate = Gates.GetOrAdd(path, _ => new(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false); var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var updated = update(await ReadAsync(projectId, path, token).ConfigureAwait(false));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(stream, updated, cancellationToken: token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false); stream.Flush(true); }
            token.ThrowIfCancellationRequested(); File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); gate.Release(); }
    }
}

/// <summary>Small, UI-independent state machine consumed by tethered execution.</summary>
public sealed class ProjectShotExecution
{
    private IReadOnlyList<ProjectShot> _shots = [];
    public IReadOnlyList<ProjectShot> Shots => _shots;
    public ProjectShot? Current { get; private set; }
    public Guid? EffectiveLookId(Guid? projectDefaultLookId) => Current?.ReferenceLookId ?? projectDefaultLookId;

    public void Load(IEnumerable<ProjectShot> shots, Guid? currentId = null)
    {
        _shots = shots.Select(shot => shot.Normalize()).OrderBy(shot => shot.Order).ToArray();
        Current = _shots.FirstOrDefault(shot => shot.ShotId == currentId) ?? _shots.FirstOrDefault();
    }
    public ProjectShot? Move(int delta)
    {
        if (_shots.Count == 0) return Current = null;
        var index = Current is null ? 0 : Array.FindIndex(_shots.ToArray(), shot => shot.ShotId == Current.ShotId);
        Current = _shots[Math.Clamp(index + delta, 0, _shots.Count - 1)]; return Current;
    }
    public ProjectShot Select(Guid shotId) => Current = _shots.First(shot => shot.ShotId == shotId);
    public ProjectShot SetStatus(ProjectShotStatus status) => ReplaceCurrent(Current! with { Status = status, UpdatedAt = DateTimeOffset.UtcNow });
    public ProjectShot SetPoseStatus(Guid referenceId, PoseExecutionStatus status, bool advance = false)
    {
        if (Current is null) throw new InvalidOperationException("No current shot.");
        var references = Current.References.Select(reference => reference.ReferenceId == referenceId
            ? reference with { PoseStatus = status }
            : status == PoseExecutionStatus.Current && reference.Kind == ShotReferenceKind.Pose && reference.PoseStatus == PoseExecutionStatus.Current
                ? reference with { PoseStatus = PoseExecutionStatus.NotShot } : reference).ToArray();
        if (advance && status == PoseExecutionStatus.Completed)
        {
            var next = references.FirstOrDefault(reference => reference.Kind == ShotReferenceKind.Pose && reference.PoseStatus == PoseExecutionStatus.NotShot);
            if (next is not null) references = references.Select(reference => reference.ReferenceId == next.ReferenceId ? reference with { PoseStatus = PoseExecutionStatus.Current } : reference).ToArray();
        }
        return ReplaceCurrent(Current with { References = references, UpdatedAt = DateTimeOffset.UtcNow });
    }
    public ProjectShot SetPinned(Guid referenceId, bool pinned) => ReplaceCurrent(Current! with
    {
        References = Current!.References.Select(reference => reference.ReferenceId == referenceId ? reference with { IsPinned = pinned } : reference).ToArray(),
        UpdatedAt = DateTimeOffset.UtcNow
    });
    private ProjectShot ReplaceCurrent(ProjectShot shot)
    {
        Current = shot.Normalize(); _shots = _shots.Select(item => item.ShotId == shot.ShotId ? Current : item).ToArray(); return Current;
    }
}
