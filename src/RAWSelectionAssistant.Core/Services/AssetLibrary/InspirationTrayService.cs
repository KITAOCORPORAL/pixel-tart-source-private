using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

/// <summary>Resolution state of a temporary inspiration reference.</summary>
public enum InspirationTrayResolutionState
{
    Resolved,
    LibraryOffline,
    AssetMissing,
    HashMismatch
}

/// <summary>A portable, non-owning reference held by the inspiration tray.</summary>
public sealed record InspirationTrayEntry(
    Guid TrayEntryId,
    AssetLibraryStableReference Reference,
    DateTimeOffset AddedAtUtc,
    int SortOrder,
    string? OptionalNote,
    string? SourceContext,
    InspirationTrayResolutionState ResolutionState,
    DateTimeOffset? LastResolvedAtUtc);

public sealed record InspirationTrayAddResult(int AddedCount, int ExistingCount, IReadOnlyList<InspirationTrayEntry> AddedEntries);
public sealed record InspirationTrayResolution(AssetLibraryStableReference Reference, InspirationTrayResolutionState State);
public sealed record InspirationCollectionSnapshot(IReadOnlyList<InspirationTrayEntry> Entries, DateTimeOffset CreatedAtUtc);
public sealed record InspirationCollectionSummary(Guid CollectionId, string Name, Guid? ProjectId, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, int EntryCount);

/// <summary>
/// Persists temporary inspiration references in one SQLite transaction per operation.
/// The tray never copies or edits source files and may contain entries from offline libraries.
/// </summary>
public interface IInspirationTrayService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<InspirationTrayAddResult> AddAsync(AssetLibraryStableReference reference, string? sourceContext = null, string? note = null, CancellationToken cancellationToken = default);
    Task<InspirationTrayAddResult> AddRangeAsync(IEnumerable<AssetLibraryStableReference> references, string? sourceContext = null, CancellationToken cancellationToken = default);
    Task<int> RemoveAsync(Guid trayEntryId, CancellationToken cancellationToken = default);
    Task<int> RemoveRangeAsync(IEnumerable<Guid> trayEntryIds, CancellationToken cancellationToken = default);
    Task<int> ClearAsync(CancellationToken cancellationToken = default);
    Task<int> ReorderAsync(IReadOnlyList<Guid> orderedEntryIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspirationTrayEntry>> ListAsync(CancellationToken cancellationToken = default);
    Task<int> ResolveAsync(IEnumerable<InspirationTrayResolution> resolutions, CancellationToken cancellationToken = default);
    Task<int> DeduplicateAsync(CancellationToken cancellationToken = default);
    Task<InspirationCollectionSnapshot> SaveAsInspirationCollectionAsync(CancellationToken cancellationToken = default);
    Task<InspirationCollectionSummary> CreateCollectionAsync(string name, Guid? projectId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspirationCollectionSummary>> ListCollectionsAsync(CancellationToken cancellationToken = default);
    Task<int> RenameCollectionAsync(Guid collectionId, string name, CancellationToken cancellationToken = default);
    Task<int> ArchiveCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);
    Task<int> AddEntriesToCollectionAsync(Guid collectionId, IEnumerable<Guid> trayEntryIds, CancellationToken cancellationToken = default);
    Task<int> RemoveEntriesFromCollectionAsync(Guid collectionId, IEnumerable<Guid> trayEntryIds, CancellationToken cancellationToken = default);
    Task<int> ReorderCollectionAsync(Guid collectionId, IReadOnlyList<Guid> trayEntryIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspirationTrayEntry>> ListCollectionEntriesAsync(Guid collectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetLibraryStableReference>> GetSelectedReferencesAsync(IEnumerable<Guid> trayEntryIds, CancellationToken cancellationToken = default);
}

public sealed class SqliteInspirationTrayService : IInspirationTrayService
{
    private readonly string _databasePath;
    private int _initialized;

    public SqliteInspirationTrayService(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath => _databasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _initialized) != 0) return;
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS InspirationTrayEntries(
                TrayEntryId TEXT NOT NULL PRIMARY KEY,
                LibraryId TEXT NOT NULL,
                AssetId TEXT NOT NULL,
                ContentHash TEXT NOT NULL,
                AddedAtUtc TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                OptionalNote TEXT NULL,
                SourceContext TEXT NULL,
                ResolutionState TEXT NOT NULL DEFAULT 'Resolved',
                LastResolvedAtUtc TEXT NULL,
                UNIQUE(LibraryId,AssetId));
            CREATE INDEX IF NOT EXISTS IX_InspirationTrayEntries_Order ON InspirationTrayEntries(SortOrder,AddedAtUtc,TrayEntryId);
            CREATE TABLE IF NOT EXISTS InspirationCollections(
                CollectionId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                ProjectId TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS InspirationCollectionEntries(
                CollectionId TEXT NOT NULL,
                TrayEntryId TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                PRIMARY KEY(CollectionId,TrayEntryId),
                FOREIGN KEY(CollectionId) REFERENCES InspirationCollections(CollectionId) ON DELETE CASCADE,
                FOREIGN KEY(TrayEntryId) REFERENCES InspirationTrayEntries(TrayEntryId) ON DELETE CASCADE);
            CREATE INDEX IF NOT EXISTS IX_InspirationCollectionEntries_Order ON InspirationCollectionEntries(CollectionId,SortOrder);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _initialized, 1);
    }

    public Task<InspirationTrayAddResult> AddAsync(AssetLibraryStableReference reference, string? sourceContext = null, string? note = null, CancellationToken cancellationToken = default) =>
        AddRangeCoreAsync([reference], sourceContext, note, cancellationToken);

    public Task<InspirationTrayAddResult> AddRangeAsync(IEnumerable<AssetLibraryStableReference> references, string? sourceContext = null, CancellationToken cancellationToken = default) =>
        AddRangeCoreAsync(references, sourceContext, null, cancellationToken);

    private async Task<InspirationTrayAddResult> AddRangeCoreAsync(IEnumerable<AssetLibraryStableReference> references, string? sourceContext, string? note, CancellationToken cancellationToken)
    {
        var refs = references?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(references));
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT LibraryId || ':' || AssetId FROM InspirationTrayEntries;";
            await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) existing.Add(reader.GetString(0));
        }
        var maxOrder = 0;
        await using (var max = connection.CreateCommand()) { max.Transaction = tx; max.CommandText = "SELECT COALESCE(MAX(SortOrder),-1) FROM InspirationTrayEntries;"; maxOrder = Convert.ToInt32(await max.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)); }
        var added = new List<InspirationTrayEntry>();
        foreach (var reference in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = $"{reference.LibraryId:D}:{reference.AssetId:D}";
            if (!existing.Add(key)) continue;
            var entry = new InspirationTrayEntry(Guid.NewGuid(), reference, DateTimeOffset.UtcNow, ++maxOrder, note, sourceContext, InspirationTrayResolutionState.Resolved, DateTimeOffset.UtcNow);
            await using var insert = connection.CreateCommand(); insert.Transaction = tx;
            insert.CommandText = "INSERT INTO InspirationTrayEntries(TrayEntryId,LibraryId,AssetId,ContentHash,AddedAtUtc,SortOrder,OptionalNote,SourceContext,ResolutionState,LastResolvedAtUtc) VALUES($id,$library,$asset,$hash,$added,$order,$note,$source,$state,$resolved);";
            insert.Parameters.AddWithValue("$id", entry.TrayEntryId.ToString("D")); insert.Parameters.AddWithValue("$library", reference.LibraryId.ToString("D")); insert.Parameters.AddWithValue("$asset", reference.AssetId.ToString("D")); insert.Parameters.AddWithValue("$hash", reference.ContentHash); insert.Parameters.AddWithValue("$added", entry.AddedAtUtc.ToString("O")); insert.Parameters.AddWithValue("$order", entry.SortOrder); insert.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value); insert.Parameters.AddWithValue("$source", (object?)sourceContext ?? DBNull.Value); insert.Parameters.AddWithValue("$state", entry.ResolutionState.ToString()); insert.Parameters.AddWithValue("$resolved", entry.LastResolvedAtUtc!.Value.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); added.Add(entry);
        }
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(added.Count, refs.Length - added.Count, added);
    }

    public Task<int> RemoveAsync(Guid trayEntryId, CancellationToken cancellationToken = default) => RemoveRangeAsync([trayEntryId], cancellationToken);
    public async Task<int> RemoveRangeAsync(IEnumerable<Guid> trayEntryIds, CancellationToken cancellationToken = default)
    {
        var ids = trayEntryIds?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(trayEntryIds));
        await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var count = 0; foreach (var id in ids) { await using var command = connection.CreateCommand(); command.Transaction = tx; command.CommandText = "DELETE FROM InspirationTrayEntries WHERE TrayEntryId=$id;"; command.Parameters.AddWithValue("$id", id.ToString("D")); count += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false); return count;
    }

    public async Task<int> ClearAsync(CancellationToken cancellationToken = default) { await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var x = c.CreateCommand(); x.CommandText = "DELETE FROM InspirationTrayEntries;"; return await x.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }

    public async Task<int> ReorderAsync(IReadOnlyList<Guid> orderedEntryIds, CancellationToken cancellationToken = default)
    {
        if (orderedEntryIds is null) throw new ArgumentNullException(nameof(orderedEntryIds)); await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var tx = (SqliteTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); var changed = 0; for (var i = 0; i < orderedEntryIds.Count; i++) { await using var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "UPDATE InspirationTrayEntries SET SortOrder=$order WHERE TrayEntryId=$id;"; cmd.Parameters.AddWithValue("$order", i); cmd.Parameters.AddWithValue("$id", orderedEntryIds[i].ToString("D")); changed += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false); return changed;
    }

    public async Task<IReadOnlyList<InspirationTrayEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT TrayEntryId,LibraryId,AssetId,ContentHash,AddedAtUtc,SortOrder,OptionalNote,SourceContext,ResolutionState,LastResolvedAtUtc FROM InspirationTrayEntries ORDER BY SortOrder,AddedAtUtc,TrayEntryId;"; await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); var result = new List<InspirationTrayEntry>(); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { if (!Enum.TryParse<InspirationTrayResolutionState>(reader.GetString(8), out var state)) state = InspirationTrayResolutionState.HashMismatch; result.Add(new(Guid.Parse(reader.GetString(0)), new(Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2)), reader.GetString(3)), DateTimeOffset.Parse(reader.GetString(4)), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), state, reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)))); }
        return result;
    }

    public async Task<int> ResolveAsync(IEnumerable<InspirationTrayResolution> resolutions, CancellationToken cancellationToken = default)
    {
        var values = resolutions?.ToArray() ?? throw new ArgumentNullException(nameof(resolutions)); await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var tx = (SqliteTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); var changed = 0; foreach (var item in values) { await using var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "UPDATE InspirationTrayEntries SET ResolutionState=$state,LastResolvedAtUtc=$at WHERE LibraryId=$library AND AssetId=$asset AND ContentHash=$hash;"; cmd.Parameters.AddWithValue("$state", item.State.ToString()); cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); cmd.Parameters.AddWithValue("$library", item.Reference.LibraryId.ToString("D")); cmd.Parameters.AddWithValue("$asset", item.Reference.AssetId.ToString("D")); cmd.Parameters.AddWithValue("$hash", item.Reference.ContentHash); changed += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false); return changed;
    }

    public async Task<int> DeduplicateAsync(CancellationToken cancellationToken = default) { await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = c.CreateCommand(); cmd.CommandText = "DELETE FROM InspirationTrayEntries WHERE TrayEntryId NOT IN (SELECT TrayEntryId FROM (SELECT TrayEntryId,ROW_NUMBER() OVER (PARTITION BY LibraryId,AssetId ORDER BY AddedAtUtc,SortOrder,TrayEntryId) AS rn FROM InspirationTrayEntries) WHERE rn=1);"; return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
    public async Task<InspirationCollectionSnapshot> SaveAsInspirationCollectionAsync(CancellationToken cancellationToken = default) => new(await ListAsync(cancellationToken).ConfigureAwait(false), DateTimeOffset.UtcNow);
    public async Task<InspirationCollectionSummary> CreateCollectionAsync(string name, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Collection name is required.", nameof(name));
        await InitializeAsync(cancellationToken).ConfigureAwait(false); var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO InspirationCollections(CollectionId,Name,ProjectId,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$name,$project,$created,$updated);";
        cmd.Parameters.AddWithValue("$id", id.ToString("D")); cmd.Parameters.AddWithValue("$name", name.Trim()); cmd.Parameters.AddWithValue("$project", (object?)projectId?.ToString("D") ?? DBNull.Value); cmd.Parameters.AddWithValue("$created", now.ToString("O")); cmd.Parameters.AddWithValue("$updated", now.ToString("O")); await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return new(id, name.Trim(), projectId, now, now, 0);
    }
    public async Task<IReadOnlyList<InspirationCollectionSummary>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT c.CollectionId,c.Name,c.ProjectId,c.CreatedAtUtc,c.UpdatedAtUtc,COUNT(e.TrayEntryId) FROM InspirationCollections c LEFT JOIN InspirationCollectionEntries e ON e.CollectionId=c.CollectionId WHERE c.IsArchived=0 GROUP BY c.CollectionId ORDER BY c.UpdatedAtUtc DESC;";
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); var result = new List<InspirationCollectionSummary>(); while (await r.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(r.GetString(0)), r.GetString(1), r.IsDBNull(2) ? null : Guid.Parse(r.GetString(2)), DateTimeOffset.Parse(r.GetString(3)), DateTimeOffset.Parse(r.GetString(4)), r.GetInt32(5))); return result;
    }
    public async Task<int> RenameCollectionAsync(Guid collectionId, string name, CancellationToken cancellationToken = default)
    { if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Collection name is required.", nameof(name)); await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = c.CreateCommand(); cmd.CommandText = "UPDATE InspirationCollections SET Name=$name,UpdatedAtUtc=$updated WHERE CollectionId=$id AND IsArchived=0;"; cmd.Parameters.AddWithValue("$name", name.Trim()); cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O")); cmd.Parameters.AddWithValue("$id", collectionId.ToString("D")); return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
    public async Task<int> SetCollectionProjectAsync(Guid collectionId, Guid? projectId, CancellationToken cancellationToken = default)
    { await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = c.CreateCommand(); cmd.CommandText = "UPDATE InspirationCollections SET ProjectId=$project,UpdatedAtUtc=$updated WHERE CollectionId=$id AND IsArchived=0;"; cmd.Parameters.AddWithValue("$project", (object?)projectId?.ToString("D") ?? DBNull.Value); cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O")); cmd.Parameters.AddWithValue("$id", collectionId.ToString("D")); return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
    public async Task<int> ArchiveCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    { await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = c.CreateCommand(); cmd.CommandText = "UPDATE InspirationCollections SET IsArchived=1,UpdatedAtUtc=$updated WHERE CollectionId=$id AND IsArchived=0;"; cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O")); cmd.Parameters.AddWithValue("$id", collectionId.ToString("D")); return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
    public Task<int> AddEntriesToCollectionAsync(Guid collectionId, IEnumerable<Guid> trayEntryIds, CancellationToken cancellationToken = default) => UpdateCollectionEntriesAsync(collectionId, trayEntryIds, false, cancellationToken);
    public Task<int> RemoveEntriesFromCollectionAsync(Guid collectionId, IEnumerable<Guid> trayEntryIds, CancellationToken cancellationToken = default) => UpdateCollectionEntriesAsync(collectionId, trayEntryIds, true, cancellationToken);
    private async Task<int> UpdateCollectionEntriesAsync(Guid collectionId, IEnumerable<Guid> trayEntryIds, bool remove, CancellationToken cancellationToken)
    { var ids = trayEntryIds?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(trayEntryIds)); await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); var changed = 0; foreach (var id in ids) { await using var cmd = c.CreateCommand(); cmd.CommandText = remove ? "DELETE FROM InspirationCollectionEntries WHERE CollectionId=$collection AND TrayEntryId=$entry;" : "INSERT OR IGNORE INTO InspirationCollectionEntries(CollectionId,TrayEntryId,SortOrder) VALUES($collection,$entry,COALESCE((SELECT MAX(SortOrder)+1 FROM InspirationCollectionEntries WHERE CollectionId=$collection),0));"; cmd.Parameters.AddWithValue("$collection", collectionId.ToString("D")); cmd.Parameters.AddWithValue("$entry", id.ToString("D")); changed += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); } return changed; }
    public async Task<int> ReorderCollectionAsync(Guid collectionId, IReadOnlyList<Guid> trayEntryIds, CancellationToken cancellationToken = default)
    { if (trayEntryIds is null) throw new ArgumentNullException(nameof(trayEntryIds)); await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var tx = (SqliteTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); var changed = 0; for (var i=0;i<trayEntryIds.Count;i++){ await using var cmd=c.CreateCommand(); cmd.Transaction=tx; cmd.CommandText="UPDATE InspirationCollectionEntries SET SortOrder=$order WHERE CollectionId=$collection AND TrayEntryId=$entry;"; cmd.Parameters.AddWithValue("$order",i); cmd.Parameters.AddWithValue("$collection",collectionId.ToString("D")); cmd.Parameters.AddWithValue("$entry",trayEntryIds[i].ToString("D")); changed += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);} await tx.CommitAsync(cancellationToken).ConfigureAwait(false); return changed; }
    public async Task<IReadOnlyList<AssetLibraryStableReference>> GetSelectedReferencesAsync(IEnumerable<Guid> trayEntryIds, CancellationToken cancellationToken = default) { var ids = trayEntryIds?.ToHashSet() ?? throw new ArgumentNullException(nameof(trayEntryIds)); return (await ListAsync(cancellationToken).ConfigureAwait(false)).Where(x => ids.Contains(x.TrayEntryId)).Select(x => x.Reference).ToArray(); }

    public async Task<IReadOnlyList<InspirationTrayEntry>> ListCollectionEntriesAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false); await using var c = await OpenAsync(cancellationToken).ConfigureAwait(false); await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT t.TrayEntryId,t.LibraryId,t.AssetId,t.ContentHash,t.AddedAtUtc,t.SortOrder,t.OptionalNote,t.SourceContext,t.ResolutionState,t.LastResolvedAtUtc FROM InspirationTrayEntries t JOIN InspirationCollectionEntries e ON e.TrayEntryId=t.TrayEntryId WHERE e.CollectionId=$collection ORDER BY e.SortOrder,e.TrayEntryId;"; cmd.Parameters.AddWithValue("$collection", collectionId.ToString("D"));
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); var result = new List<InspirationTrayEntry>(); while (await r.ReadAsync(cancellationToken).ConfigureAwait(false)) { if (!Enum.TryParse<InspirationTrayResolutionState>(r.GetString(8), out var state)) state = InspirationTrayResolutionState.HashMismatch; result.Add(new(Guid.Parse(r.GetString(0)), new(Guid.Parse(r.GetString(1)), Guid.Parse(r.GetString(2)), r.GetString(3)), DateTimeOffset.Parse(r.GetString(4)), r.GetInt32(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), state, r.IsDBNull(9) ? null : DateTimeOffset.Parse(r.GetString(9)))); } return result;
    }
    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken) { var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared, Pooling = true }.ToString()); await connection.OpenAsync(cancellationToken).ConfigureAwait(false); await using var pragma = connection.CreateCommand(); pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;"; await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); return connection; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
