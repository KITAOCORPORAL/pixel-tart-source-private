using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.Duplicates;

public enum DuplicateReferenceKind { InspirationBoard, Canvas, Project, Planning }

public sealed record DuplicateReplacementAsset(Guid LibraryId, AssetItem Asset)
{
    public AssetLibraryStableReference StableReference => new(
        LibraryId,
        Asset.AssetId,
        Asset.ContentHash ?? throw new InvalidOperationException("替换目标必须具有可靠的内容指纹。"));
}

public interface IDuplicateReferenceParticipant
{
    DuplicateReferenceKind Kind { get; }
    Task<int> CountAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<object> CaptureAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default);
    Task ReplaceAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default);
    Task RestoreAsync(object snapshot, CancellationToken cancellationToken = default);
}

public sealed record DuplicateReferenceReplacementResult(DuplicateReferenceUsage PreviousUsage, int UpdatedReferenceCount);

/// <summary>
/// Coordinates independent durable stores as one compensating transaction. Every participant is
/// captured before the first write. Any failure restores every participant, including the one
/// whose write failed, so callers never observe a deliberately half-replaced reference graph.
/// </summary>
public sealed class DuplicateReferenceProtectionService(IEnumerable<IDuplicateReferenceParticipant> participants)
{
    private readonly IDuplicateReferenceParticipant[] _participants = participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants));

    public async Task<DuplicateReferenceUsage> GetUsageAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<DuplicateReferenceKind, int>();
        foreach (var participant in _participants)
            counts[participant.Kind] = counts.GetValueOrDefault(participant.Kind) + await participant.CountAsync(assetId, cancellationToken).ConfigureAwait(false);
        return new(
            counts.GetValueOrDefault(DuplicateReferenceKind.InspirationBoard),
            counts.GetValueOrDefault(DuplicateReferenceKind.Canvas),
            counts.GetValueOrDefault(DuplicateReferenceKind.Project),
            counts.GetValueOrDefault(DuplicateReferenceKind.Planning));
    }

    public async Task<DuplicateReferenceReplacementResult> ReplaceAsync(
        Guid sourceAssetId,
        DuplicateReplacementAsset target,
        CancellationToken cancellationToken = default)
    {
        return await ReplaceGroupAsync([sourceAssetId], target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Capture the whole requested group before writing any store, then restore it in reverse order on failure.</summary>
    public async Task<DuplicateReferenceReplacementResult> ReplaceGroupAsync(
        IEnumerable<Guid> sourceAssetIds,
        DuplicateReplacementAsset target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceAssetIds);
        var sources = sourceAssetIds.Distinct().ToArray();
        if (sources.Any(id => id == Guid.Empty) || target.Asset.AssetId == Guid.Empty) throw new ArgumentException("素材身份不能为空。");
        sources = sources.Where(id => id != target.Asset.AssetId).ToArray();
        if (sources.Length == 0) return new(new(), 0);
        _ = target.StableReference;
        var usages = new List<DuplicateReferenceUsage>(sources.Length);
        var snapshots = new List<(IDuplicateReferenceParticipant Participant, object Snapshot)>(_participants.Length * sources.Length);
        foreach (var source in sources)
        {
            usages.Add(await GetUsageAsync(source, cancellationToken).ConfigureAwait(false));
            foreach (var participant in _participants)
                snapshots.Add((participant, await participant.CaptureAsync(source, target, cancellationToken).ConfigureAwait(false)));
        }

        try
        {
            foreach (var source in sources)
                foreach (var participant in _participants)
                    await participant.ReplaceAsync(source, target, cancellationToken).ConfigureAwait(false);
            var usage = new DuplicateReferenceUsage(usages.Sum(item => item.InspirationBoardCount), usages.Sum(item => item.CanvasCount), usages.Sum(item => item.ProjectCount), usages.Sum(item => item.PlanningCount));
            return new(usage, usage.Total);
        }
        catch (Exception replacementFailure)
        {
            var rollbackFailures = new List<Exception>();
            foreach (var item in snapshots.AsEnumerable().Reverse())
            {
                try { await item.Participant.RestoreAsync(item.Snapshot, CancellationToken.None).ConfigureAwait(false); }
                catch (Exception rollbackFailure) { rollbackFailures.Add(rollbackFailure); }
            }
            if (rollbackFailures.Count > 0)
                throw new AggregateException("替换引用失败，且回滚未能完整完成。", new[] { replacementFailure }.Concat(rollbackFailures));
            throw new InvalidOperationException("替换引用失败，所有引用已恢复。", replacementFailure);
        }
    }
}

public sealed class CanvasReferenceParticipant(CanvasDocumentStore store) : IDuplicateReferenceParticipant
{
    private sealed record Snapshot(IReadOnlyList<CanvasDocument> Documents);
    public DuplicateReferenceKind Kind => DuplicateReferenceKind.Canvas;
    public async Task<int> CountAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        (await store.ListAsync(token: cancellationToken).ConfigureAwait(false)).Sum(document => document.Objects.Count(item => item.AssetId == assetId));

    public async Task<object> CaptureAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default) =>
        new Snapshot((await store.ListAsync(token: cancellationToken).ConfigureAwait(false)).Where(document => document.Objects.Any(item => item.AssetId == sourceAssetId || item.AssetId == target.Asset.AssetId)).ToArray());

    public async Task ReplaceAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default)
    {
        foreach (var document in (await store.ListAsync(token: cancellationToken).ConfigureAwait(false)).Where(document => document.Objects.Any(item => item.AssetId == sourceAssetId)))
        {
            var objects = document.Objects.Select(item => item.AssetId == sourceAssetId ? item with
            {
                LibraryId = target.LibraryId,
                AssetId = target.Asset.AssetId,
                ContentHash = target.Asset.ContentHash!,
                SourcePath = target.Asset.SourcePath,
                Name = target.Asset.DisplayName,
                SourceWidth = Math.Max(1, target.Asset.Width ?? (int)Math.Round(item.SourceWidth)),
                SourceHeight = Math.Max(1, target.Asset.Height ?? (int)Math.Round(item.SourceHeight))
            } : item).ToArray();
            await store.SaveAsync(document with { Objects = objects, UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RestoreAsync(object snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is not Snapshot state) throw new ArgumentException("画布引用快照无效。", nameof(snapshot));
        foreach (var document in state.Documents) await store.SaveAsync(document, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ProjectReferenceParticipant(IAssetLibraryRepository repository) : IDuplicateReferenceParticipant
{
    private sealed record Snapshot(IReadOnlyList<ProjectAssetLink> Source, IReadOnlyList<ProjectAssetLink> Target);
    public DuplicateReferenceKind Kind => DuplicateReferenceKind.Project;
    public async Task<int> CountAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        (await repository.ListProjectAssetLinksAsync(assetId, cancellationToken: cancellationToken).ConfigureAwait(false)).Count;

    public async Task<object> CaptureAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default) => new Snapshot(
        await repository.ListProjectAssetLinksAsync(sourceAssetId, cancellationToken: cancellationToken).ConfigureAwait(false),
        await repository.ListProjectAssetLinksAsync(target.Asset.AssetId, cancellationToken: cancellationToken).ConfigureAwait(false));

    public async Task ReplaceAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default)
    {
        foreach (var link in await repository.ListProjectAssetLinksAsync(sourceAssetId, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            await repository.SaveProjectAssetLinkAsync(link with { AssetId = target.Asset.AssetId }, cancellationToken).ConfigureAwait(false);
            await repository.RemoveProjectAssetLinkAsync(link.ProjectId, sourceAssetId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RestoreAsync(object snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is not Snapshot state) throw new ArgumentException("项目引用快照无效。", nameof(snapshot));
        var all = state.Source.Concat(state.Target).ToArray();
        foreach (var projectId in all.Select(item => item.ProjectId).Distinct())
        foreach (var assetId in all.Select(item => item.AssetId).Distinct())
            await repository.RemoveProjectAssetLinkAsync(projectId, assetId, cancellationToken).ConfigureAwait(false);
        foreach (var link in all) await repository.SaveProjectAssetLinkAsync(link, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class InspirationReferenceParticipant(string databasePath) : IDuplicateReferenceParticipant
{
    private readonly string _databasePath = Path.GetFullPath(databasePath);
    private sealed record Entry(string TrayEntryId, string LibraryId, string AssetId, string ContentHash, string AddedAtUtc, int SortOrder, string? Note, string? Context, string State, string? ResolvedAt);
    private sealed record Membership(string CollectionId, string TrayEntryId, int SortOrder);
    private sealed record Snapshot(Guid SourceAssetId, Guid TargetAssetId, IReadOnlyList<Entry> Entries, IReadOnlyList<Membership> Memberships);
    public DuplicateReferenceKind Kind => DuplicateReferenceKind.InspirationBoard;

    public async Task<int> CountAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_databasePath)) return 0;
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM InspirationCollectionEntries c JOIN InspirationTrayEntries t ON t.TrayEntryId=c.TrayEntryId WHERE t.AssetId=$asset;";
        command.Parameters.AddWithValue("$asset", assetId.ToString("D"));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<object> CaptureAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_databasePath)) return new Snapshot(sourceAssetId, target.Asset.AssetId, [], []);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadSnapshotAsync(connection, sourceAssetId, target.Asset.AssetId, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceAsync(Guid sourceAssetId, DuplicateReplacementAsset target, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_databasePath)) return;
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var sourceId = await FindEntryIdAsync(connection, transaction, sourceAssetId, cancellationToken).ConfigureAwait(false);
        if (sourceId is null) { await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return; }
        var targetId = await FindEntryIdAsync(connection, transaction, target.Asset.AssetId, cancellationToken).ConfigureAwait(false);
        if (targetId is null)
        {
            await ExecuteAsync(connection, transaction, "UPDATE InspirationTrayEntries SET LibraryId=$library,AssetId=$target,ContentHash=$hash,ResolutionState='Resolved',LastResolvedAtUtc=$at WHERE TrayEntryId=$sourceEntry;", cancellationToken,
                ("$library", target.LibraryId.ToString("D")), ("$target", target.Asset.AssetId.ToString("D")), ("$hash", target.Asset.ContentHash!), ("$at", DateTimeOffset.UtcNow.ToString("O")), ("$sourceEntry", sourceId));
        }
        else
        {
            await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO InspirationCollectionEntries(CollectionId,TrayEntryId,SortOrder) SELECT CollectionId,$targetEntry,SortOrder FROM InspirationCollectionEntries WHERE TrayEntryId=$sourceEntry; DELETE FROM InspirationTrayEntries WHERE TrayEntryId=$sourceEntry;", cancellationToken,
                ("$targetEntry", targetId), ("$sourceEntry", sourceId));
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreAsync(object snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is not Snapshot state) throw new ArgumentException("灵感板引用快照无效。", nameof(snapshot));
        if (!File.Exists(_databasePath) && state.Entries.Count == 0) return;
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var id in new[] { state.SourceAssetId, state.TargetAssetId })
        {
            await ExecuteAsync(connection, transaction, "DELETE FROM InspirationTrayEntries WHERE AssetId=$asset;", cancellationToken, ("$asset", id.ToString("D")));
        }
        foreach (var entry in state.Entries)
            await ExecuteAsync(connection, transaction, "INSERT INTO InspirationTrayEntries(TrayEntryId,LibraryId,AssetId,ContentHash,AddedAtUtc,SortOrder,OptionalNote,SourceContext,ResolutionState,LastResolvedAtUtc) VALUES($entry,$library,$asset,$hash,$added,$sort,$note,$context,$state,$resolved);", cancellationToken,
                ("$entry", entry.TrayEntryId), ("$library", entry.LibraryId), ("$asset", entry.AssetId), ("$hash", entry.ContentHash), ("$added", entry.AddedAtUtc), ("$sort", entry.SortOrder), ("$note", entry.Note), ("$context", entry.Context), ("$state", entry.State), ("$resolved", entry.ResolvedAt));
        foreach (var membership in state.Memberships)
            await ExecuteAsync(connection, transaction, "INSERT INTO InspirationCollectionEntries(CollectionId,TrayEntryId,SortOrder) VALUES($collection,$entry,$sort);", cancellationToken,
                ("$collection", membership.CollectionId), ("$entry", membership.TrayEntryId), ("$sort", membership.SortOrder));
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared, Pooling = true }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var pragma = connection.CreateCommand(); pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;"; await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task<Snapshot> ReadSnapshotAsync(SqliteConnection connection, Guid source, Guid target, CancellationToken cancellationToken)
    {
        var entries = new List<Entry>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT TrayEntryId,LibraryId,AssetId,ContentHash,AddedAtUtc,SortOrder,OptionalNote,SourceContext,ResolutionState,LastResolvedAtUtc FROM InspirationTrayEntries WHERE AssetId=$source OR AssetId=$target;";
            command.Parameters.AddWithValue("$source", source.ToString("D")); command.Parameters.AddWithValue("$target", target.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) entries.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9)));
        }
        var memberships = new List<Membership>();
        foreach (var entry in entries)
        {
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT CollectionId,TrayEntryId,SortOrder FROM InspirationCollectionEntries WHERE TrayEntryId=$entry;"; command.Parameters.AddWithValue("$entry", entry.TrayEntryId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) memberships.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        }
        return new(source, target, entries, memberships);
    }

    private static async Task<string?> FindEntryIdAsync(SqliteConnection connection, SqliteTransaction transaction, Guid assetId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT TrayEntryId FROM InspirationTrayEntries WHERE AssetId=$asset LIMIT 1;"; command.Parameters.AddWithValue("$asset", assetId.ToString("D")); return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DuplicateTrashSafetyService(IAssetLibraryRepository repository, DuplicateReferenceProtectionService protection)
{
    public async Task<AssetLibraryBatchResult> MoveToTrashAsync(Guid assetId, bool continueWhenReferenced, CancellationToken cancellationToken = default)
    {
        var usage = await protection.GetUsageAsync(assetId, cancellationToken).ConfigureAwait(false);
        if (usage.IsInUse && !continueWhenReferenced) throw new InvalidOperationException("这张素材正在被使用，请先保留或替换引用。");
        // The repository trash is recoverable and changes metadata only. Source bytes are never deleted.
        return await repository.SetAssetsTrashedAsync([assetId], true, cancellationToken).ConfigureAwait(false);
    }
}
