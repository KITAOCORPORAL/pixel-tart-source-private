using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

/// <summary>
/// SQLite metadata repository for the virtual Asset Library.  It deliberately
/// uses a separate database file from the existing Pixel Tart workflow database;
/// this keeps the feature branch migration-safe while the schema proposal is
/// reviewed.
/// </summary>
public sealed class SqliteAssetLibraryRepository : IAssetLibraryRepository
{
    private readonly AssetLibraryDatabase _database;
    private readonly ConcurrentDictionary<Guid, Func<CancellationToken, Task>> _undo = new();
    private int _initialized;

    public SqliteAssetLibraryRepository(string databasePath) : this(new AssetLibraryDatabase(databasePath)) { }

    public SqliteAssetLibraryRepository(AssetLibraryDatabase database) => _database = database;

    public string DatabasePath => _database.DatabasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _initialized) != 0) return;
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await AssetLibrarySchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _initialized, 1);
    }

    public async Task<AssetLibraryMetadataIndexResult> ImportAsync(
        IEnumerable<AssetImportRequest> requests,
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null)
    {
        var started = DateTimeOffset.UtcNow;
        var imported = 0;
        var skipped = 0;
        var missing = 0;
        var warnings = new List<string>();
        var materialized = requests.Where(x => !string.IsNullOrWhiteSpace(x.SourcePath)).ToArray();
        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < materialized.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var request = materialized[index];
                var sourcePath = Path.GetFullPath(request.SourcePath);
                var normalized = NormalizePath(sourcePath);
                var info = new FileInfo(sourcePath);
                if (!info.Exists)
                {
                    missing++;
                    warnings.Add($"Missing source: {Path.GetFileName(sourcePath)}");
                    await UpsertAssetAsync(connection, transaction, Guid.NewGuid(), sourcePath, normalized, info, request, managedCopyPath: null, cancellationToken).ConfigureAwait(false);
                    imported++;
                    progress?.Report(index + 1);
                    continue;
                }

                var existingId = await FindAssetIdAsync(connection, transaction, normalized, cancellationToken).ConfigureAwait(false);
                var assetId = existingId ?? Guid.NewGuid();
                string? managedCopyPath = null;
                if (request.Mode == AssetImportMode.ManagedCopy)
                {
                    if (string.IsNullOrWhiteSpace(request.ManagedLibraryRoot))
                        throw new ArgumentException("Managed copy imports require a library root.", nameof(requests));
                    managedCopyPath = await EnsureManagedCopyAsync(sourcePath, request.ManagedLibraryRoot!, assetId, cancellationToken).ConfigureAwait(false);
                }

                await UpsertAssetAsync(connection, transaction, assetId, sourcePath, normalized, info, request, managedCopyPath, cancellationToken).ConfigureAwait(false);
                if (existingId is null) imported++; else skipped++;
                progress?.Report(index + 1);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(0, 0, 0, true, DateTimeOffset.UtcNow - started, warnings);
        }

        return new(imported, skipped, missing, false, DateTimeOffset.UtcNow - started, warnings);
    }

    public async Task<AssetItem?> GetAssetAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectAssetSql + " WHERE AssetId=$id LIMIT 1;";
        command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadAsset(reader) : null;
    }

    public async Task<AssetLibraryPage> QueryAsync(AssetLibraryQuery query, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        Regex? regex = null;
        if (!string.IsNullOrWhiteSpace(query.FileNameRegex))
        {
            try { regex = new Regex(query.FileNameRegex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
            catch (ArgumentException ex) { return new([], null, 0, ex.Message); }
        }
        if (query.SmartFolderId is not null)
        {
            try
            {
                foreach (var rule in await ListSmartFolderRulesAsync(query.SmartFolderId.Value, cancellationToken).ConfigureAwait(false))
                    if (rule.Operator == SmartFolderOperator.Regex) _ = new Regex(rule.Value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex) { return new([], null, 0, ex.Message); }
        }

        var offset = ParseCursor(query.Cursor);
        var pageSize = query.EffectivePageSize;
        var candidates = await LoadCandidatesAsync(query, regex is not null || query.SmartFolderId is not null, cancellationToken).ConfigureAwait(false);
        var filtered = new List<AssetItem>();
        var total = 0;
        foreach (var item in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (regex is not null && !regex.IsMatch(item.DisplayName)) continue;
            if (query.SmartFolderId is not null)
            {
                try
                {
                    if (!await MatchesSmartFolderAsync(item, query.SmartFolderId.Value, cancellationToken).ConfigureAwait(false)) continue;
                }
                catch (ArgumentException ex)
                {
                    return new([], null, 0, ex.Message);
                }
            }
            total++;
            if (total > offset && filtered.Count < pageSize) filtered.Add(item);
        }

        if (regex is null && query.SmartFolderId is null)
            total = candidates.Count;
        var next = offset + filtered.Count < total ? (offset + filtered.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        return new(filtered, next, total);
    }

    public async Task UpdateAssetAsync(Guid assetId, int? rating = null, string? comment = null, CancellationToken cancellationToken = default)
        => _ = await UpdateAssetMetadataAsync(assetId, rating, comment, cancellationToken).ConfigureAwait(false);

    public async Task<AssetLibraryBatchResult> UpdateAssetMetadataAsync(Guid assetId, int? rating = null, string? comment = null, CancellationToken cancellationToken = default)
    {
        var previous = await GetAssetAsync(assetId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"Asset {assetId} was not found.");
        var nextRating = rating ?? previous.Rating;
        if (nextRating is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(rating));
        var nextComment = comment ?? previous.Comment;
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE AssetItems SET Rating=$rating,Comment=$comment WHERE AssetId=$id;";
        command.Parameters.AddWithValue("$rating", nextRating); command.Parameters.AddWithValue("$comment", nextComment); command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var token = RegisterUndo("Update asset metadata", async tokenCancellation =>
        {
            await UpdateAssetMetadataInternalAsync(assetId, previous.Rating, previous.Comment, tokenCancellation).ConfigureAwait(false);
        });
        return new(1, token, []);
    }

    public async Task<IReadOnlyList<AssetFolder>> ListFoldersAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<AssetFolder>();
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FolderId,ParentFolderId,Name,Description,Icon,Color,SortOrder,CreatedAt,UpdatedAt,IsArchived,IsSystem,AutoTagIdsJson FROM AssetFolders WHERE ($include=1 OR IsArchived=0) ORDER BY SortOrder,Name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$include", includeArchived ? 1 : 0);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadFolder(reader));
        return result;
    }

    public async Task<AssetFolder> SaveFolderAsync(AssetFolder folder, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var created = folder.CreatedAt ?? now;
        var updated = now;
        var parent = folder.ParentFolderId?.ToString("D");
        var tagsJson = JsonSerializer.Serialize(folder.AutoTagIds ?? []);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AssetFolders(FolderId,ParentFolderId,Name,Description,Icon,Color,SortOrder,CreatedAt,UpdatedAt,IsArchived,IsSystem,AutoTagIdsJson)
            VALUES($id,$parent,$name,$description,$icon,$color,$sort,$created,$updated,$archived,$system,$tags)
            ON CONFLICT(FolderId) DO UPDATE SET ParentFolderId=excluded.ParentFolderId,Name=excluded.Name,Description=excluded.Description,Icon=excluded.Icon,Color=excluded.Color,SortOrder=excluded.SortOrder,UpdatedAt=excluded.UpdatedAt,IsArchived=excluded.IsArchived,IsSystem=excluded.IsSystem,AutoTagIdsJson=excluded.AutoTagIdsJson;
            """;
        command.Parameters.AddWithValue("$id", folder.FolderId.ToString("D")); command.Parameters.AddWithValue("$parent", (object?)parent ?? DBNull.Value); command.Parameters.AddWithValue("$name", folder.Name.Trim()); command.Parameters.AddWithValue("$description", folder.Description ?? string.Empty); command.Parameters.AddWithValue("$icon", (object?)folder.Icon ?? DBNull.Value); command.Parameters.AddWithValue("$color", (object?)folder.Color ?? DBNull.Value); command.Parameters.AddWithValue("$sort", folder.SortOrder); command.Parameters.AddWithValue("$created", created.ToString("O")); command.Parameters.AddWithValue("$updated", updated.ToString("O")); command.Parameters.AddWithValue("$archived", folder.IsArchived ? 1 : 0); command.Parameters.AddWithValue("$system", folder.IsSystem ? 1 : 0); command.Parameters.AddWithValue("$tags", tagsJson);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return folder with { Name = folder.Name.Trim(), CreatedAt = created, UpdatedAt = updated, AutoTagIds = folder.AutoTagIds ?? [] };
    }

    public async Task ArchiveFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "UPDATE AssetFolders SET IsArchived=1,UpdatedAt=$at WHERE FolderId=$id;"; command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); command.Parameters.AddWithValue("$id", folderId.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AssetFolderMembership>> ListFolderMembershipsAsync(Guid? folderId = null, Guid? assetId = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<AssetFolderMembership>();
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT AssetId,FolderId,AddedAt FROM AssetFolderMemberships WHERE ($folder IS NULL OR FolderId=$folder) AND ($asset IS NULL OR AssetId=$asset) ORDER BY AddedAt;";
        command.Parameters.AddWithValue("$folder", (object?)folderId?.ToString("D") ?? DBNull.Value); command.Parameters.AddWithValue("$asset", (object?)assetId?.ToString("D") ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), DateTimeOffset.Parse(reader.GetString(2))));
        return result;
    }

    public Task<AssetLibraryBatchResult> AddToFolderAsync(IEnumerable<Guid> assetIds, Guid folderId, CancellationToken cancellationToken = default)
        => ChangeFolderMembershipAsync(assetIds, folderId, add: true, cancellationToken);

    public Task<AssetLibraryBatchResult> RemoveFromFolderAsync(IEnumerable<Guid> assetIds, Guid folderId, CancellationToken cancellationToken = default)
        => ChangeFolderMembershipAsync(assetIds, folderId, add: false, cancellationToken);

    public async Task<IReadOnlyList<TagGroup>> ListTagGroupsAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<TagGroup>();
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT TagGroupId,Name,SortOrder,CreatedAt,IsArchived FROM TagGroups WHERE ($include=1 OR IsArchived=0) ORDER BY SortOrder,Name COLLATE NOCASE;"; command.Parameters.AddWithValue("$include", includeArchived ? 1 : 0);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetInt32(2), DateTimeOffset.Parse(reader.GetString(3)), reader.GetInt32(4) != 0));
        return result;
    }

    public async Task<TagGroup> SaveTagGroupAsync(TagGroup group, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var created = group.CreatedAt ?? DateTimeOffset.UtcNow;
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "INSERT INTO TagGroups(TagGroupId,Name,SortOrder,CreatedAt,IsArchived) VALUES($id,$name,$sort,$created,$archived) ON CONFLICT(TagGroupId) DO UPDATE SET Name=excluded.Name,SortOrder=excluded.SortOrder,IsArchived=excluded.IsArchived;"; command.Parameters.AddWithValue("$id", group.TagGroupId.ToString("D")); command.Parameters.AddWithValue("$name", group.Name.Trim()); command.Parameters.AddWithValue("$sort", group.SortOrder); command.Parameters.AddWithValue("$created", created.ToString("O")); command.Parameters.AddWithValue("$archived", group.IsArchived ? 1 : 0); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return group with { Name = group.Name.Trim(), CreatedAt = created };
    }

    public async Task<IReadOnlyList<AssetTag>> ListTagsAsync(Guid? tagGroupId = null, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<AssetTag>();
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT t.TagId,t.Name,t.TagGroupId,t.SortOrder,(SELECT COUNT(*) FROM AssetTagMemberships m WHERE m.TagId=t.TagId),t.CreatedAt,t.IsArchived FROM AssetTags t WHERE ($group IS NULL OR t.TagGroupId=$group) AND ($include=1 OR t.IsArchived=0) ORDER BY t.SortOrder,t.Name COLLATE NOCASE;"; command.Parameters.AddWithValue("$group", (object?)tagGroupId?.ToString("D") ?? DBNull.Value); command.Parameters.AddWithValue("$include", includeArchived ? 1 : 0);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)), reader.GetInt32(3), reader.GetInt32(4), DateTimeOffset.Parse(reader.GetString(5)), reader.GetInt32(6) != 0));
        return result;
    }

    public async Task<AssetTag> SaveTagAsync(AssetTag tag, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var created = tag.CreatedAt ?? DateTimeOffset.UtcNow;
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "INSERT INTO AssetTags(TagId,Name,TagGroupId,SortOrder,UsageCount,CreatedAt,IsArchived) VALUES($id,$name,$group,$sort,0,$created,$archived) ON CONFLICT(TagId) DO UPDATE SET Name=excluded.Name,TagGroupId=excluded.TagGroupId,SortOrder=excluded.SortOrder,IsArchived=excluded.IsArchived;"; command.Parameters.AddWithValue("$id", tag.TagId.ToString("D")); command.Parameters.AddWithValue("$name", tag.Name.Trim()); command.Parameters.AddWithValue("$group", (object?)tag.TagGroupId?.ToString("D") ?? DBNull.Value); command.Parameters.AddWithValue("$sort", tag.SortOrder); command.Parameters.AddWithValue("$created", created.ToString("O")); command.Parameters.AddWithValue("$archived", tag.IsArchived ? 1 : 0); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return tag with { Name = tag.Name.Trim(), CreatedAt = created };
    }

    public async Task ArchiveTagAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "UPDATE AssetTags SET IsArchived=1 WHERE TagId=$id;"; command.Parameters.AddWithValue("$id", tagId.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AssetTagMembership>> ListTagMembershipsAsync(Guid? tagId = null, Guid? assetId = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<AssetTagMembership>();
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "SELECT AssetId,TagId,AddedAt FROM AssetTagMemberships WHERE ($tag IS NULL OR TagId=$tag) AND ($asset IS NULL OR AssetId=$asset) ORDER BY AddedAt;"; command.Parameters.AddWithValue("$tag", (object?)tagId?.ToString("D") ?? DBNull.Value); command.Parameters.AddWithValue("$asset", (object?)assetId?.ToString("D") ?? DBNull.Value); await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), DateTimeOffset.Parse(reader.GetString(2))));
        return result;
    }

    public Task<AssetLibraryBatchResult> AddTagsAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default)
        => ChangeTagMembershipAsync(assetIds, tagIds, add: true, cancellationToken);

    public Task<AssetLibraryBatchResult> RemoveTagsAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default)
        => ChangeTagMembershipAsync(assetIds, tagIds, add: false, cancellationToken);

    public async Task<AssetLibraryBatchResult> MergeTagsAsync(Guid sourceTagId, Guid targetTagId, CancellationToken cancellationToken = default)
    {
        if (sourceTagId == targetTagId) throw new ArgumentException("Source and target tags must differ.");
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var source = await ReadTagByIdAsync(sourceTagId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Source tag not found.");
        var target = await ReadTagByIdAsync(targetTagId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Target tag not found.");
        var sourceMembers = (await ListTagMembershipsAsync(sourceTagId, cancellationToken: cancellationToken).ConfigureAwait(false)).ToArray();
        var targetMembers = (await ListTagMembershipsAsync(targetTagId, cancellationToken: cancellationToken).ConfigureAwait(false)).Select(x => x.AssetId).ToHashSet();
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var member in sourceMembers)
        {
            await using var add = connection.CreateCommand(); add.Transaction = transaction; add.CommandText = "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);"; add.Parameters.AddWithValue("$asset", member.AssetId.ToString("D")); add.Parameters.AddWithValue("$tag", targetTagId.ToString("D")); add.Parameters.AddWithValue("$at", member.AddedAt.ToString("O")); await add.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var remove = connection.CreateCommand()) { remove.Transaction = transaction; remove.CommandText = "DELETE FROM AssetTagMemberships WHERE TagId=$tag;"; remove.Parameters.AddWithValue("$tag", sourceTagId.ToString("D")); await remove.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using (var archive = connection.CreateCommand()) { archive.Transaction = transaction; archive.CommandText = "UPDATE AssetTags SET IsArchived=1 WHERE TagId=$tag;"; archive.Parameters.AddWithValue("$tag", sourceTagId.ToString("D")); await archive.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        var token = RegisterUndo("Merge tags", async undoCancellation =>
        {
            await RestoreMergedTagsAsync(source, target, sourceMembers, targetMembers, undoCancellation).ConfigureAwait(false);
        });
        return new(sourceMembers.Length, token, []);
    }

    public async Task<IReadOnlyList<SmartFolder>> ListSmartFoldersAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false); var result = new List<SmartFolder>(); await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "SELECT SmartFolderId,Name,Logic,Description,CreatedAt,UpdatedAt,IsArchived FROM SmartFolders WHERE ($include=1 OR IsArchived=0) ORDER BY Name COLLATE NOCASE;"; command.Parameters.AddWithValue("$include", includeArchived ? 1 : 0); await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), Enum.TryParse<SmartFolderLogic>(reader.GetString(2), true, out var logic) ? logic : SmartFolderLogic.And, reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4)), DateTimeOffset.Parse(reader.GetString(5)), reader.GetInt32(6) != 0)); return result;
    }

    public async Task<SmartFolder> SaveSmartFolderAsync(SmartFolder folder, IEnumerable<SmartFolderRule> rules, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false); var now = DateTimeOffset.UtcNow; var created = folder.CreatedAt ?? now;
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var command = connection.CreateCommand()) { command.Transaction = transaction; command.CommandText = "INSERT INTO SmartFolders(SmartFolderId,Name,Logic,Description,CreatedAt,UpdatedAt,IsArchived) VALUES($id,$name,$logic,$description,$created,$updated,$archived) ON CONFLICT(SmartFolderId) DO UPDATE SET Name=excluded.Name,Logic=excluded.Logic,Description=excluded.Description,UpdatedAt=excluded.UpdatedAt,IsArchived=excluded.IsArchived;"; command.Parameters.AddWithValue("$id", folder.SmartFolderId.ToString("D")); command.Parameters.AddWithValue("$name", folder.Name.Trim()); command.Parameters.AddWithValue("$logic", folder.Logic.ToString()); command.Parameters.AddWithValue("$description", folder.Description ?? string.Empty); command.Parameters.AddWithValue("$created", created.ToString("O")); command.Parameters.AddWithValue("$updated", now.ToString("O")); command.Parameters.AddWithValue("$archived", folder.IsArchived ? 1 : 0); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using (var clear = connection.CreateCommand()) { clear.Transaction = transaction; clear.CommandText = "DELETE FROM SmartFolderRules WHERE SmartFolderId=$id;"; clear.Parameters.AddWithValue("$id", folder.SmartFolderId.ToString("D")); await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        foreach (var rule in rules.OrderBy(x => x.SortOrder)) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "INSERT INTO SmartFolderRules(RuleId,SmartFolderId,Field,Operator,Value,Negated,SortOrder) VALUES($id,$folder,$field,$operator,$value,$negated,$sort);"; command.Parameters.AddWithValue("$id", rule.RuleId == Guid.Empty ? Guid.NewGuid().ToString("D") : rule.RuleId.ToString("D")); command.Parameters.AddWithValue("$folder", folder.SmartFolderId.ToString("D")); command.Parameters.AddWithValue("$field", rule.Field.ToString()); command.Parameters.AddWithValue("$operator", rule.Operator.ToString()); command.Parameters.AddWithValue("$value", rule.Value ?? string.Empty); command.Parameters.AddWithValue("$negated", rule.Negated ? 1 : 0); command.Parameters.AddWithValue("$sort", rule.SortOrder); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return folder with { Name = folder.Name.Trim(), CreatedAt = created, UpdatedAt = now };
    }

    public async Task<IReadOnlyList<SmartFolderRule>> ListSmartFolderRulesAsync(Guid smartFolderId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false); var result = new List<SmartFolderRule>(); await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "SELECT RuleId,SmartFolderId,Field,Operator,Value,Negated,SortOrder FROM SmartFolderRules WHERE SmartFolderId=$id ORDER BY SortOrder,RuleId;"; command.Parameters.AddWithValue("$id", smartFolderId.ToString("D")); await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), Enum.TryParse<SmartFolderField>(reader.GetString(2), true, out var field) ? field : SmartFolderField.FileName, Enum.TryParse<SmartFolderOperator>(reader.GetString(3), true, out var op) ? op : SmartFolderOperator.Contains, reader.GetString(4), reader.GetInt32(5) != 0, reader.GetInt32(6))); return result;
    }

    public async Task<bool> UndoAsync(AssetLibraryUndoToken token, CancellationToken cancellationToken = default)
    {
        if (!_undo.TryRemove(token.OperationId, out var operation)) return false;
        await operation(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<AssetLibraryBatchResult> ChangeFolderMembershipAsync(IEnumerable<Guid> assetIds, Guid folderId, bool add, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false); var ids = assetIds.Distinct().ToArray(); if (ids.Length == 0) return new(0, null, []);
        var previous = (await ListFolderMembershipsAsync(folderId: folderId, cancellationToken: cancellationToken).ConfigureAwait(false)).Where(x => ids.Contains(x.AssetId)).ToArray();
        var autoTags = await ReadFolderAutoTagsAsync(folderId, cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var changed = 0;
        foreach (var id in ids)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            if (add) { command.CommandText = "INSERT OR IGNORE INTO AssetFolderMemberships(AssetId,FolderId,AddedAt) VALUES($asset,$folder,$at);"; command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); }
            else command.CommandText = "DELETE FROM AssetFolderMemberships WHERE AssetId=$asset AND FolderId=$folder;";
            command.Parameters.AddWithValue("$asset", id.ToString("D")); command.Parameters.AddWithValue("$folder", folderId.ToString("D")); changed += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (add && autoTags.Count > 0 && ids.Length > 0) await ChangeTagMembershipInternalAsync(ids, autoTags, add: true, cancellationToken).ConfigureAwait(false);
        var token = RegisterUndo(add ? "Add assets to folder" : "Remove assets from folder", async undoCancellation =>
        {
            if (add) await ChangeFolderMembershipInternalAsync(ids, folderId, add: false, undoCancellation).ConfigureAwait(false);
            else await RestoreFolderMembershipsAsync(previous, undoCancellation).ConfigureAwait(false);
        });
        return new(changed, token, []);
    }

    private async Task<AssetLibraryBatchResult> ChangeTagMembershipAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> tagIds, bool add, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false); var assets = assetIds.Distinct().ToArray(); var tags = tagIds.Distinct().ToArray(); if (assets.Length == 0 || tags.Length == 0) return new(0, null, []);
        var previous = (await ListTagMembershipsAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Where(x => assets.Contains(x.AssetId) && tags.Contains(x.TagId)).ToArray();
        var changed = await ChangeTagMembershipInternalAsync(assets, tags, add, cancellationToken).ConfigureAwait(false);
        var token = RegisterUndo(add ? "Add tags" : "Remove tags", async undoCancellation =>
        {
            if (add) await ChangeTagMembershipInternalAsync(assets, tags, add: false, undoCancellation).ConfigureAwait(false);
            else await RestoreTagMembershipsAsync(previous, undoCancellation).ConfigureAwait(false);
        });
        return new(changed, token, []);
    }

    private async Task<int> ChangeTagMembershipInternalAsync(IReadOnlyList<Guid> assets, IReadOnlyList<Guid> tags, bool add, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); var changed = 0;
        foreach (var asset in assets) foreach (var tag in tags)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction; command.Parameters.AddWithValue("$asset", asset.ToString("D")); command.Parameters.AddWithValue("$tag", tag.ToString("D"));
            if (add) { command.CommandText = "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);"; command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); }
            else command.CommandText = "DELETE FROM AssetTagMemberships WHERE AssetId=$asset AND TagId=$tag;";
            changed += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return changed;
    }

    private async Task<IReadOnlyList<AssetItem>> LoadCandidatesAsync(AssetLibraryQuery query, bool needsPostFilter, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        var where = new List<string> { query.IncludeArchived ? "1=1" : "a.IsArchived=0" };
        if (!string.IsNullOrWhiteSpace(query.SearchText)) { where.Add("(a.DisplayName LIKE $search OR a.Comment LIKE $search OR EXISTS(SELECT 1 FROM AssetTagMemberships sm JOIN AssetTags st ON st.TagId=sm.TagId WHERE sm.AssetId=a.AssetId AND st.Name LIKE $search) OR EXISTS(SELECT 1 FROM AssetFolderMemberships sfm JOIN AssetFolders sf ON sf.FolderId=sfm.FolderId WHERE sfm.AssetId=a.AssetId AND sf.Name LIKE $search))"); command.Parameters.AddWithValue("$search", "%" + query.SearchText.Trim() + "%"); }
        if (query.FolderId is not null) { where.Add("EXISTS(SELECT 1 FROM AssetFolderMemberships fm WHERE fm.AssetId=a.AssetId AND fm.FolderId=$folder)"); command.Parameters.AddWithValue("$folder", query.FolderId.Value.ToString("D")); }
        if (query.TagId is not null) { where.Add("EXISTS(SELECT 1 FROM AssetTagMemberships tm WHERE tm.AssetId=a.AssetId AND tm.TagId=$tag)"); command.Parameters.AddWithValue("$tag", query.TagId.Value.ToString("D")); }
        if (query.MinimumRating is not null) { where.Add("a.Rating >= $minRating"); command.Parameters.AddWithValue("$minRating", query.MinimumRating.Value); }
        if (query.MaximumRating is not null) { where.Add("a.Rating <= $maxRating"); command.Parameters.AddWithValue("$maxRating", query.MaximumRating.Value); }
        if (!string.IsNullOrWhiteSpace(query.MediaType)) { where.Add("a.MediaType=$mediaType"); command.Parameters.AddWithValue("$mediaType", query.MediaType); }
        if (!string.IsNullOrWhiteSpace(query.Extension)) { where.Add("a.Extension=$extension"); command.Parameters.AddWithValue("$extension", query.Extension.StartsWith('.') ? query.Extension : "." + query.Extension); }
        if (query.UncategorizedOnly) where.Add("NOT EXISTS(SELECT 1 FROM AssetFolderMemberships uf WHERE uf.AssetId=a.AssetId)");
        if (query.UntaggedOnly) where.Add("NOT EXISTS(SELECT 1 FROM AssetTagMemberships ut WHERE ut.AssetId=a.AssetId)");
        if (query.MissingOnly) where.Add("a.IsMissing=1");
        command.CommandText = SelectAssetSql + " WHERE " + string.Join(" AND ", where) + " ORDER BY a.AddedAt DESC,a.AssetId;";
        var result = new List<AssetItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadAsset(reader)); return result;
    }

    private async Task<bool> MatchesSmartFolderAsync(AssetItem item, Guid smartFolderId, CancellationToken cancellationToken)
    {
        var folder = await ReadSmartFolderAsync(smartFolderId, cancellationToken).ConfigureAwait(false); if (folder is null) return false;
        var rules = await ListSmartFolderRulesAsync(smartFolderId, cancellationToken).ConfigureAwait(false); if (rules.Count == 0) return true;
        var values = new List<bool>(rules.Count); foreach (var rule in rules) { var match = await EvaluateRuleAsync(item, rule, cancellationToken).ConfigureAwait(false); values.Add(rule.Negated ? !match : match); }
        return folder.Logic == SmartFolderLogic.Or ? values.Any(x => x) : values.All(x => x);
    }

    private async Task<bool> EvaluateRuleAsync(AssetItem item, SmartFolderRule rule, CancellationToken cancellationToken)
    {
        if (rule.Field is SmartFolderField.Folder or SmartFolderField.Tag)
        {
            var names = await GetNamesAsync(rule.Field == SmartFolderField.Folder ? "AssetFolders" : "AssetTags", rule.Field == SmartFolderField.Folder ? "AssetFolderMemberships" : "AssetTagMemberships", rule.Field == SmartFolderField.Folder ? "FolderId" : "TagId", item.AssetId, cancellationToken).ConfigureAwait(false);
            if (rule.Operator == SmartFolderOperator.NotEquals) return names.All(name => !EvaluateText(name, SmartFolderOperator.Equals, rule.Value));
            return names.Any(name => EvaluateText(name, rule.Operator, rule.Value));
        }
        if (rule.Field == SmartFolderField.Rating) return EvaluateNumber(item.Rating, rule.Operator, rule.Value);
        if (rule.Field == SmartFolderField.FileSize) return EvaluateNumber(item.FileSize, rule.Operator, rule.Value);
        if (rule.Field == SmartFolderField.Width) return EvaluateNumber(item.Width ?? 0, rule.Operator, rule.Value);
        if (rule.Field == SmartFolderField.Height) return EvaluateNumber(item.Height ?? 0, rule.Operator, rule.Value);
        if (rule.Field == SmartFolderField.AspectRatio) return EvaluateNumber(item.Width is > 0 && item.Height is > 0 ? (double)item.Width.Value / item.Height.Value : 0, rule.Operator, rule.Value);
        if (rule.Field == SmartFolderField.IsMissing) return EvaluateBoolean(item.IsMissing, rule.Operator);
        if (rule.Field == SmartFolderField.IsUncategorized) return EvaluateBoolean(!await ExistsMembershipAsync("AssetFolderMemberships", item.AssetId, cancellationToken).ConfigureAwait(false), rule.Operator);
        if (rule.Field == SmartFolderField.IsUntagged) return EvaluateBoolean(!await ExistsMembershipAsync("AssetTagMemberships", item.AssetId, cancellationToken).ConfigureAwait(false), rule.Operator);
        var text = rule.Field switch
        {
            SmartFolderField.FileName => item.DisplayName,
            SmartFolderField.Extension => item.Extension,
            SmartFolderField.MediaType => item.MediaType,
            SmartFolderField.Comment => item.Comment,
            SmartFolderField.Orientation => item.Orientation ?? string.Empty,
            _ => string.Empty
        };
        return EvaluateText(text ?? string.Empty, rule.Operator, rule.Value);
    }

    private async Task<IReadOnlyList<string>> GetNamesAsync(string entityTable, string membershipTable, string idColumn, Guid assetId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = $"SELECT e.Name FROM {membershipTable} m JOIN {entityTable} e ON e.{idColumn}=m.{idColumn} WHERE m.AssetId=$asset ORDER BY e.Name;"; command.Parameters.AddWithValue("$asset", assetId.ToString("D")); var result = new List<string>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(reader.GetString(0)); return result;
    }

    private async Task<bool> ExistsMembershipAsync(string table, Guid assetId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = $"SELECT EXISTS(SELECT 1 FROM {table} WHERE AssetId=$asset);"; command.Parameters.AddWithValue("$asset", assetId.ToString("D")); return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 0;
    }

    private async Task<AssetTag?> ReadTagByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "SELECT TagId,Name,TagGroupId,SortOrder,UsageCount,CreatedAt,IsArchived FROM AssetTags WHERE TagId=$id;"; command.Parameters.AddWithValue("$id", id.ToString("D")); await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)), reader.GetInt32(3), reader.GetInt32(4), DateTimeOffset.Parse(reader.GetString(5)), reader.GetInt32(6) != 0) : null;
    }

    private async Task<SmartFolder?> ReadSmartFolderAsync(Guid id, CancellationToken cancellationToken)
    {
        var folders = await ListSmartFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false); return folders.FirstOrDefault(x => x.SmartFolderId == id);
    }

    private async Task<IReadOnlyList<Guid>> ReadFolderAutoTagsAsync(Guid folderId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "SELECT AutoTagIdsJson FROM AssetFolders WHERE FolderId=$id;"; command.Parameters.AddWithValue("$id", folderId.ToString("D")); var value = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)); if (string.IsNullOrWhiteSpace(value)) return []; try { return JsonSerializer.Deserialize<Guid[]>(value) ?? []; } catch (JsonException) { return []; }
    }

    private async Task RestoreMergedTagsAsync(AssetTag source, AssetTag target, IReadOnlyList<AssetTagMembership> sourceMembers, IReadOnlySet<Guid> targetMembers, CancellationToken cancellationToken)
    {
        await SaveTagAsync(source with { IsArchived = false }, cancellationToken).ConfigureAwait(false);
        var moved = sourceMembers.Where(x => !targetMembers.Contains(x.AssetId)).Select(x => x.AssetId).ToArray(); if (moved.Length > 0) await ChangeTagMembershipInternalAsync(moved, [target.TagId], add: false, cancellationToken).ConfigureAwait(false);
        if (sourceMembers.Count > 0) await ChangeTagMembershipInternalAsync(sourceMembers.Select(x => x.AssetId).ToArray(), [source.TagId], add: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task RestoreFolderMembershipsAsync(IReadOnlyList<AssetFolderMembership> memberships, CancellationToken cancellationToken)
    {
        if (memberships.Count == 0) return; await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); foreach (var item in memberships) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "INSERT OR IGNORE INTO AssetFolderMemberships(AssetId,FolderId,AddedAt) VALUES($asset,$folder,$at);"; command.Parameters.AddWithValue("$asset", item.AssetId.ToString("D")); command.Parameters.AddWithValue("$folder", item.FolderId.ToString("D")); command.Parameters.AddWithValue("$at", item.AddedAt.ToString("O")); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); } await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RestoreTagMembershipsAsync(IReadOnlyList<AssetTagMembership> memberships, CancellationToken cancellationToken)
    {
        if (memberships.Count == 0) return; await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); foreach (var item in memberships) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);"; command.Parameters.AddWithValue("$asset", item.AssetId.ToString("D")); command.Parameters.AddWithValue("$tag", item.TagId.ToString("D")); command.Parameters.AddWithValue("$at", item.AddedAt.ToString("O")); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); } await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ChangeFolderMembershipInternalAsync(IEnumerable<Guid> assetIds, Guid folderId, bool add, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); foreach (var asset in assetIds) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.Parameters.AddWithValue("$asset", asset.ToString("D")); command.Parameters.AddWithValue("$folder", folderId.ToString("D")); if (add) { command.CommandText = "INSERT OR IGNORE INTO AssetFolderMemberships(AssetId,FolderId,AddedAt) VALUES($asset,$folder,$at);"; command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); } else command.CommandText = "DELETE FROM AssetFolderMemberships WHERE AssetId=$asset AND FolderId=$folder;"; await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); } await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAssetMetadataInternalAsync(Guid assetId, int rating, string comment, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "UPDATE AssetItems SET Rating=$rating,Comment=$comment WHERE AssetId=$id;"; command.Parameters.AddWithValue("$rating", rating); command.Parameters.AddWithValue("$comment", comment); command.Parameters.AddWithValue("$id", assetId.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private AssetLibraryUndoToken RegisterUndo(string description, Func<CancellationToken, Task> operation)
    {
        var token = new AssetLibraryUndoToken(Guid.NewGuid(), description, DateTimeOffset.UtcNow); _undo[token.OperationId] = operation; return token;
    }

    private static async Task<Guid?> FindAssetIdAsync(SqliteConnection connection, SqliteTransaction transaction, string normalized, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT AssetId FROM AssetItems WHERE NormalizedSourcePath=$path LIMIT 1;"; command.Parameters.AddWithValue("$path", normalized); var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false); return value is string id && Guid.TryParse(id, out var parsed) ? parsed : null;
    }

    private static async Task UpsertAssetAsync(SqliteConnection connection, SqliteTransaction transaction, Guid assetId, string sourcePath, string normalized, FileInfo info, AssetImportRequest request, string? managedCopyPath, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow; var displayName = info.Name; var extension = NormalizeExtension(info.Extension); var mediaType = ClassifyMediaType(extension); var contentHash = request.ComputeContentHash && info.Exists ? await ComputeHashAsync(sourcePath, cancellationToken).ConfigureAwait(false) : null; var modified = info.Exists ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) : now;
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = """
            INSERT INTO AssetItems(AssetId,SourcePath,NormalizedSourcePath,DisplayName,Extension,MediaType,FileSize,ContentHash,Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode,ManagedCopyPath)
            VALUES($id,$source,$normalized,$name,$extension,$media,$size,$hash,NULL,NULL,NULL,NULL,$added,$modified,0,'',$missing,0,$mode,$managed)
            ON CONFLICT(NormalizedSourcePath) DO UPDATE SET SourcePath=excluded.SourcePath,DisplayName=excluded.DisplayName,Extension=excluded.Extension,MediaType=excluded.MediaType,FileSize=excluded.FileSize,ContentHash=COALESCE(excluded.ContentHash,AssetItems.ContentHash),ModifiedAt=excluded.ModifiedAt,IsMissing=excluded.IsMissing,ImportMode=excluded.ImportMode,ManagedCopyPath=COALESCE(excluded.ManagedCopyPath,AssetItems.ManagedCopyPath);
            """;
        command.Parameters.AddWithValue("$id", assetId.ToString("D")); command.Parameters.AddWithValue("$source", sourcePath); command.Parameters.AddWithValue("$normalized", normalized); command.Parameters.AddWithValue("$name", displayName); command.Parameters.AddWithValue("$extension", extension); command.Parameters.AddWithValue("$media", mediaType); command.Parameters.AddWithValue("$size", info.Exists ? info.Length : 0); command.Parameters.AddWithValue("$hash", (object?)contentHash ?? DBNull.Value); command.Parameters.AddWithValue("$added", now.ToString("O")); command.Parameters.AddWithValue("$modified", modified.ToString("O")); command.Parameters.AddWithValue("$missing", info.Exists ? 0 : 1); command.Parameters.AddWithValue("$mode", request.Mode.ToString()); command.Parameters.AddWithValue("$managed", (object?)managedCopyPath ?? DBNull.Value); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> EnsureManagedCopyAsync(string sourcePath, string root, Guid assetId, CancellationToken cancellationToken)
    {
        var destinationRoot = Path.GetFullPath(root); Directory.CreateDirectory(destinationRoot); var fileName = $"{assetId:N}_{Path.GetFileName(sourcePath)}"; var destination = Path.Combine(destinationRoot, fileName); if (!File.Exists(destination)) { await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true); await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true); await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false); } return destination;
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 131072, true); var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false); return Convert.ToHexString(hash);
    }

    private static AssetItem ReadAsset(SqliteDataReader reader)
    {
        return new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt64(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetInt32(7), reader.IsDBNull(8) ? null : reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : DateTimeOffset.Parse(reader.GetString(10)), DateTimeOffset.Parse(reader.GetString(11)), DateTimeOffset.Parse(reader.GetString(12)), reader.GetInt32(13), reader.GetString(14), reader.GetInt32(15) != 0, reader.GetInt32(16) != 0, Enum.TryParse<AssetImportMode>(reader.GetString(17), true, out var mode) ? mode : AssetImportMode.Reference, reader.IsDBNull(18) ? null : reader.GetString(18));
    }

    private static AssetFolder ReadFolder(SqliteDataReader reader)
    {
        Guid[] tags = []; try { tags = JsonSerializer.Deserialize<Guid[]>(reader.GetString(11)) ?? []; } catch (JsonException) { }
        return new(Guid.Parse(reader.GetString(0)), reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt32(6), DateTimeOffset.Parse(reader.GetString(7)), DateTimeOffset.Parse(reader.GetString(8)), reader.GetInt32(9) != 0, reader.GetInt32(10) != 0, tags);
    }

    private const string SelectAssetSql = "SELECT a.AssetId,a.SourcePath,a.DisplayName,a.Extension,a.MediaType,a.FileSize,a.ContentHash,a.Width,a.Height,a.Orientation,a.CaptureTime,a.AddedAt,a.ModifiedAt,a.Rating,a.Comment,a.IsMissing,a.IsArchived,a.ImportMode,a.ManagedCopyPath FROM AssetItems a";

    private static int ParseCursor(string? cursor) => int.TryParse(cursor, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var offset) && offset > 0 ? offset : 0;
    private static string NormalizePath(string path) => OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;
    private static string NormalizeExtension(string extension) => string.IsNullOrWhiteSpace(extension) ? string.Empty : (extension.StartsWith('.') ? extension : "." + extension).ToUpperInvariant();
    private static string ClassifyMediaType(string extension) => extension switch { ".JPG" or ".JPEG" or ".PNG" or ".WEBP" or ".TIFF" or ".TIF" => "Image", ".ARW" or ".CR2" or ".CR3" or ".NEF" or ".RAF" or ".DNG" or ".ORF" or ".RW2" => "Raw", ".PSD" or ".PSB" => "Document", ".MP4" or ".MOV" or ".AVI" => "Video", _ => "Other" };
    private static bool EvaluateText(string actual, SmartFolderOperator op, string expected) => op switch { SmartFolderOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase), SmartFolderOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase), SmartFolderOperator.NotEquals => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase), SmartFolderOperator.Regex => Regex.IsMatch(actual, expected, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), SmartFolderOperator.IsTrue => !string.IsNullOrWhiteSpace(actual), SmartFolderOperator.IsFalse => string.IsNullOrWhiteSpace(actual), _ => false };
    private static bool EvaluateNumber(double actual, SmartFolderOperator op, string expected) => double.TryParse(expected, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) && op switch { SmartFolderOperator.Equals => Math.Abs(actual - value) < 0.0001, SmartFolderOperator.NotEquals => Math.Abs(actual - value) >= 0.0001, SmartFolderOperator.GreaterThan => actual > value, SmartFolderOperator.GreaterThanOrEqual => actual >= value, SmartFolderOperator.LessThan => actual < value, SmartFolderOperator.LessThanOrEqual => actual <= value, _ => false };
    private static bool EvaluateBoolean(bool actual, SmartFolderOperator op) => op switch { SmartFolderOperator.IsTrue => actual, SmartFolderOperator.IsFalse => !actual, SmartFolderOperator.Equals => actual, SmartFolderOperator.NotEquals => !actual, _ => false };
}
