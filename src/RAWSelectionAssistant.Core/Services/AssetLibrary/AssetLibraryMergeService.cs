using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed record AssetLibraryMergePreview(
    Guid OperationId, int SourceAssets, int NewAssets, int DuplicateAssets, int IdConflicts,
    int NewFolders, int NewTags, int NewSmartFolders, long EstimatedCopyBytes);

public sealed record AssetLibraryMergeResult(
    Guid OperationId, bool AlreadyApplied, int AddedAssets, int ReusedAssets,
    int AddedFolders, int AddedTags, int AddedSmartFolders, IReadOnlyDictionary<Guid, Guid> AssetIdMap);

public sealed class AssetLibraryMergeService
{
    public async Task<AssetLibraryMergePreview> PreviewLibraryAsync(
        AssetLibraryContainerDescriptor target,
        AssetLibraryContainerDescriptor source,
        Guid? operationId = null,
        CancellationToken cancellationToken = default)
    {
        if (target.LibraryId == source.LibraryId) throw new InvalidOperationException("不能合并具有相同 LibraryId 的两个可写素材库。");
        var sourceAssets = await ReadAssetsAsync(source.DatabasePath, cancellationToken).ConfigureAwait(false);
        var targetAssets = await ReadAssetsAsync(target.DatabasePath, cancellationToken).ConfigureAwait(false);
        var targetHashes = targetAssets.Where(item => !string.IsNullOrWhiteSpace(item.ContentHash)).Select(item => item.ContentHash!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetIds = targetAssets.Select(item => item.AssetId).ToHashSet();
        var duplicate = sourceAssets.Count(item => !string.IsNullOrWhiteSpace(item.ContentHash) && targetHashes.Contains(item.ContentHash!));
        var idConflicts = sourceAssets.Count(item => targetIds.Contains(item.AssetId) && (string.IsNullOrWhiteSpace(item.ContentHash) || !targetHashes.Contains(item.ContentHash!)));
        return new(operationId ?? source.LibraryId, sourceAssets.Count, sourceAssets.Count - duplicate, duplicate, idConflicts,
            await CountAsync(source.DatabasePath, "AssetFolders", cancellationToken).ConfigureAwait(false),
            await CountAsync(source.DatabasePath, "AssetTags", cancellationToken).ConfigureAwait(false),
            await CountAsync(source.DatabasePath, "SmartFolders", cancellationToken).ConfigureAwait(false),
            sourceAssets.Where(item => item.IsManaged && File.Exists(item.ManagedCopyPath)).Sum(item => new FileInfo(item.ManagedCopyPath!).Length));
    }

    public async Task<AssetLibraryMergeResult> MergeLibraryAsync(
        AssetLibraryContainerDescriptor target,
        AssetLibraryContainerDescriptor source,
        Guid? operationId = null,
        Action<string>? checkpoint = null,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewLibraryAsync(target, source, operationId, cancellationToken).ConfigureAwait(false);
        var operation = preview.OperationId;
        var stageRoot = Path.Combine(target.ManagedAssetsPath, $"merge-{operation:N}");
        var assetMap = new Dictionary<Guid, Guid>();
        var folderMap = new Dictionary<Guid, Guid>();
        var groupMap = new Dictionary<Guid, Guid>();
        var tagMap = new Dictionary<Guid, Guid>();
        var smartMap = new Dictionary<Guid, Guid>();
        var addedAssets = 0; var reusedAssets = 0;

        await using var targetConnection = await OpenAsync(target.DatabasePath, write: true, cancellationToken).ConfigureAwait(false);
        await EnsureMergeHistoryAsync(targetConnection, cancellationToken).ConfigureAwait(false);
        if (await IsAppliedAsync(targetConnection, operation, cancellationToken).ConfigureAwait(false))
            return new(operation, true, 0, 0, 0, 0, 0, assetMap);

        var sourceAssets = await ReadAssetsAsync(source.DatabasePath, cancellationToken).ConfigureAwait(false);
        var targetAssets = await ReadAssetsAsync(target.DatabasePath, cancellationToken).ConfigureAwait(false);
        var byHash = targetAssets.Where(item => !string.IsNullOrWhiteSpace(item.ContentHash)).GroupBy(item => item.ContentHash!, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().AssetId, StringComparer.OrdinalIgnoreCase);
        var targetIds = targetAssets.Select(item => item.AssetId).ToHashSet();

        try
        {
            foreach (var asset in sourceAssets)
            {
                if (!string.IsNullOrWhiteSpace(asset.ContentHash) && byHash.TryGetValue(asset.ContentHash, out var duplicateId))
                { assetMap[asset.AssetId] = duplicateId; reusedAssets++; continue; }
                var mapped = targetIds.Contains(asset.AssetId) ? Guid.NewGuid() : asset.AssetId;
                while (!targetIds.Add(mapped)) mapped = Guid.NewGuid();
                assetMap[asset.AssetId] = mapped;
                if (asset.IsManaged && File.Exists(asset.ManagedCopyPath))
                {
                    Directory.CreateDirectory(stageRoot);
                    var extension = Path.GetExtension(asset.ManagedCopyPath);
                    var destination = Path.Combine(stageRoot, mapped.ToString("N") + extension);
                    await CopyVerifiedAsync(asset.ManagedCopyPath!, destination, asset.ContentHash, cancellationToken).ConfigureAwait(false);
                    asset.ImportMode = "ManagedCopy"; asset.ManagedCopyPath = destination; asset.SourcePath = destination;
                }
                addedAssets++;
            }
            checkpoint?.Invoke("files-staged");

            await using var transaction = (SqliteTransaction)await targetConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using var sourceConnection = await OpenAsync(source.DatabasePath, write: false, cancellationToken).ConfigureAwait(false);
                foreach (var asset in sourceAssets.Where(item => assetMap[item.AssetId] != item.AssetId || !targetAssets.Any(targetItem => targetItem.AssetId == item.AssetId)))
                {
                    if (reusedAssets > 0 && byHash.TryGetValue(asset.ContentHash ?? string.Empty, out var duplicateId) && assetMap[asset.AssetId] == duplicateId) continue;
                    await InsertAssetAsync(targetConnection, transaction, asset, assetMap[asset.AssetId], cancellationToken).ConfigureAwait(false);
                }
                checkpoint?.Invoke("assets-inserted");

                await MergeFoldersAsync(sourceConnection, targetConnection, transaction, folderMap, cancellationToken).ConfigureAwait(false);
                await MergeTagsAsync(sourceConnection, targetConnection, transaction, groupMap, tagMap, cancellationToken).ConfigureAwait(false);
                await MergeSmartFoldersAsync(sourceConnection, targetConnection, transaction, folderMap, tagMap, smartMap, cancellationToken).ConfigureAwait(false);
                await MergeMembershipsAsync(sourceConnection, targetConnection, transaction, assetMap, folderMap, tagMap, cancellationToken).ConfigureAwait(false);
                await MergeFolderAutoTagsAsync(sourceConnection, targetConnection, transaction, folderMap, tagMap, cancellationToken).ConfigureAwait(false);
                await CopyMappedVisualTableAsync(sourceConnection, targetConnection, transaction, "AssetVisualAnalysis", assetMap, cancellationToken).ConfigureAwait(false);
                await CopyMappedVisualTableAsync(sourceConnection, targetConnection, transaction, "AssetVisualFeatures", assetMap, cancellationToken).ConfigureAwait(false);
                await CopyMappedVisualTableAsync(sourceConnection, targetConnection, transaction, "AssetVisualPaletteColors", assetMap, cancellationToken).ConfigureAwait(false);
                await ExecuteAsync(targetConnection, transaction, "UPDATE AssetTags SET UsageCount=(SELECT COUNT(*) FROM AssetTagMemberships m WHERE m.TagId=AssetTags.TagId);", cancellationToken).ConfigureAwait(false);
                checkpoint?.Invoke("relationships-inserted");

                var now = DateTimeOffset.UtcNow.ToString("O");
                await ExecuteAsync(targetConnection, transaction,
                    "INSERT INTO AssetLibraryMergeHistory(OperationId,SourceLibraryId,AppliedAt,Summary) VALUES($operation,$source,$at,$summary);",
                    cancellationToken, ("$operation", operation.ToString("D")), ("$source", source.LibraryId.ToString("D")), ("$at", now), ("$summary", $"added={addedAssets};reused={reusedAssets}")).ConfigureAwait(false);
                await ExecuteAsync(targetConnection, transaction,
                    "INSERT INTO AssetLibraryUndoJournal(OperationId,Description,OperationKind,PayloadJson,CreatedAt,JournalVersion) VALUES($operation,$description,'PortableMerge',$payload,$at,1);",
                    cancellationToken, ("$operation", Guid.NewGuid().ToString("D")), ("$description", $"合并素材库：{source.DisplayName}"), ("$payload", $"{{\"merge_operation_id\":\"{operation:D}\"}}"), ("$at", now)).ConfigureAwait(false);
                checkpoint?.Invoke("before-commit");
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); throw; }
            return new(operation, false, addedAssets, reusedAssets, folderMap.Count, tagMap.Count, smartMap.Count, assetMap);
        }
        catch
        {
            try { if (Directory.Exists(stageRoot)) Directory.Delete(stageRoot, true); } catch { }
            throw;
        }
    }

    public async Task<AssetLibraryMergePreview> PreviewPackageAsync(AssetLibraryContainerDescriptor target, string packagePath, CancellationToken cancellationToken = default)
    {
        var inspection = await new AssetLibraryPackageService().InspectAsync(packagePath, cancellationToken: cancellationToken).ConfigureAwait(false);
        var temporary = await ImportTemporaryAsync(packagePath, target, cancellationToken).ConfigureAwait(false);
        try { return await PreviewLibraryAsync(target, temporary, inspection.PackageId, cancellationToken).ConfigureAwait(false); }
        finally { TryDeleteDirectory(temporary.ContainerPath); }
    }

    public async Task<AssetLibraryMergeResult> MergePackageAsync(AssetLibraryContainerDescriptor target, string packagePath, Action<string>? checkpoint = null, CancellationToken cancellationToken = default)
    {
        var inspection = await new AssetLibraryPackageService().InspectAsync(packagePath, cancellationToken: cancellationToken).ConfigureAwait(false);
        var temporary = await ImportTemporaryAsync(packagePath, target, cancellationToken).ConfigureAwait(false);
        try { return await MergeLibraryAsync(target, temporary, inspection.PackageId, checkpoint, cancellationToken).ConfigureAwait(false); }
        finally { TryDeleteDirectory(temporary.ContainerPath); }
    }

    private static async Task<AssetLibraryContainerDescriptor> ImportTemporaryAsync(string packagePath, AssetLibraryContainerDescriptor target, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(target.ContainerPath)!;
        var path = Path.Combine(parent, $".ptpack-merge-{Guid.NewGuid():N}.ptlibrary");
        return await new AssetLibraryPackageService().ImportAsync(packagePath, path, "临时合并源", cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task MergeFoldersAsync(SqliteConnection source, SqliteConnection target, SqliteTransaction transaction, Dictionary<Guid, Guid> map, CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(source, "SELECT FolderId,ParentFolderId,Name,Description,Icon,Color,SortOrder,CreatedAt,UpdatedAt,IsArchived,IsSystem,AutoTagIdsJson FROM AssetFolders ORDER BY SortOrder,FolderId;", 12, cancellationToken).ConfigureAwait(false);
        foreach (var row in rows) { var id = Guid.Parse((string)row[0]!); map[id] = await IdExistsAsync(target, transaction, "AssetFolders", "FolderId", id, cancellationToken).ConfigureAwait(false) ? Guid.NewGuid() : id; }
        foreach (var row in rows)
        {
            var id = Guid.Parse((string)row[0]!); var parent = row[1] is string parentText && Guid.TryParse(parentText, out var parentId) ? map[parentId].ToString("D") : null;
            await ExecuteAsync(target, transaction, "INSERT INTO AssetFolders(FolderId,ParentFolderId,Name,Description,Icon,Color,SortOrder,CreatedAt,UpdatedAt,IsArchived,IsSystem,AutoTagIdsJson) VALUES($id,$parent,$name,$description,$icon,$color,$sort,$created,$updated,$archived,$system,$auto);", cancellationToken,
                ("$id", map[id].ToString("D")), ("$parent", parent), ("$name", row[2]), ("$description", row[3]), ("$icon", row[4]), ("$color", row[5]), ("$sort", row[6]), ("$created", row[7]), ("$updated", row[8]), ("$archived", row[9]), ("$system", row[10]), ("$auto", RewriteIds((string)row[11]!, map))).ConfigureAwait(false);
        }
    }

    private static async Task MergeTagsAsync(SqliteConnection source, SqliteConnection target, SqliteTransaction transaction, Dictionary<Guid, Guid> groups, Dictionary<Guid, Guid> tags, CancellationToken cancellationToken)
    {
        var groupRows = await ReadRowsAsync(source, "SELECT TagGroupId,Name,SortOrder,CreatedAt,IsArchived FROM TagGroups ORDER BY SortOrder,TagGroupId;", 5, cancellationToken).ConfigureAwait(false);
        foreach (var row in groupRows)
        {
            var id = Guid.Parse((string)row[0]!); var existing = await FindSimpleIdByNameAsync(target, transaction, "TagGroups", "TagGroupId", "Name", (string)row[1]!, cancellationToken).ConfigureAwait(false);
            groups[id] = existing ?? (await IdExistsAsync(target, transaction, "TagGroups", "TagGroupId", id, cancellationToken).ConfigureAwait(false) ? Guid.NewGuid() : id);
            if (existing is null) await ExecuteAsync(target, transaction, "INSERT INTO TagGroups(TagGroupId,Name,SortOrder,CreatedAt,IsArchived) VALUES($id,$name,$sort,$created,$archived);", cancellationToken, ("$id", groups[id].ToString("D")), ("$name", row[1]), ("$sort", row[2]), ("$created", row[3]), ("$archived", row[4])).ConfigureAwait(false);
        }
        var tagRows = await ReadRowsAsync(source, "SELECT TagId,Name,TagGroupId,SortOrder,UsageCount,CreatedAt,IsArchived FROM AssetTags ORDER BY SortOrder,TagId;", 7, cancellationToken).ConfigureAwait(false);
        foreach (var row in tagRows)
        {
            var id = Guid.Parse((string)row[0]!); Guid? group = row[2] is string groupText && Guid.TryParse(groupText, out var groupId) ? groups[groupId] : null;
            var existing = await FindIdByNameAsync(target, transaction, "AssetTags", "TagId", "Name", (string)row[1]!, group, cancellationToken).ConfigureAwait(false);
            tags[id] = existing ?? (await IdExistsAsync(target, transaction, "AssetTags", "TagId", id, cancellationToken).ConfigureAwait(false) ? Guid.NewGuid() : id);
            if (existing is null) await ExecuteAsync(target, transaction, "INSERT INTO AssetTags(TagId,Name,TagGroupId,SortOrder,UsageCount,CreatedAt,IsArchived) VALUES($id,$name,$group,$sort,0,$created,$archived);", cancellationToken, ("$id", tags[id].ToString("D")), ("$name", row[1]), ("$group", group?.ToString("D")), ("$sort", row[3]), ("$created", row[5]), ("$archived", row[6])).ConfigureAwait(false);
        }
    }

    private static async Task MergeSmartFoldersAsync(SqliteConnection source, SqliteConnection target, SqliteTransaction transaction, IReadOnlyDictionary<Guid, Guid> folders, IReadOnlyDictionary<Guid, Guid> tags, Dictionary<Guid, Guid> smart, CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(source, "SELECT SmartFolderId,Name,Logic,Description,CreatedAt,UpdatedAt,IsArchived FROM SmartFolders ORDER BY SmartFolderId;", 7, cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            var id = Guid.Parse((string)row[0]!); smart[id] = await IdExistsAsync(target, transaction, "SmartFolders", "SmartFolderId", id, cancellationToken).ConfigureAwait(false) ? Guid.NewGuid() : id;
            var name = await UniqueNameAsync(target, transaction, (string)row[1]!, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(target, transaction, "INSERT INTO SmartFolders(SmartFolderId,Name,Logic,Description,CreatedAt,UpdatedAt,IsArchived) VALUES($id,$name,$logic,$description,$created,$updated,$archived);", cancellationToken, ("$id", smart[id].ToString("D")), ("$name", name), ("$logic", row[2]), ("$description", row[3]), ("$created", row[4]), ("$updated", row[5]), ("$archived", row[6])).ConfigureAwait(false);
        }
        var rules = await ReadRowsAsync(source, "SELECT RuleId,SmartFolderId,Field,Operator,Value,Negated,SortOrder,GroupId,GroupLogic FROM SmartFolderRules ORDER BY SmartFolderId,SortOrder;", 9, cancellationToken).ConfigureAwait(false);
        foreach (var row in rules)
        {
            var ruleId = Guid.NewGuid(); var smartId = smart[Guid.Parse((string)row[1]!)]; var field = (string)row[2]!;
            var value = field.Equals("Folder", StringComparison.OrdinalIgnoreCase) ? RewriteIds((string)row[4]!, folders) : field.Equals("Tag", StringComparison.OrdinalIgnoreCase) ? RewriteIds((string)row[4]!, tags) : (string)row[4]!;
            await ExecuteAsync(target, transaction, "INSERT INTO SmartFolderRules(RuleId,SmartFolderId,Field,Operator,Value,Negated,SortOrder,GroupId,GroupLogic) VALUES($id,$smart,$field,$operator,$value,$negated,$sort,$group,$logic);", cancellationToken, ("$id", ruleId.ToString("D")), ("$smart", smartId.ToString("D")), ("$field", row[2]), ("$operator", row[3]), ("$value", value), ("$negated", row[5]), ("$sort", row[6]), ("$group", row[7]), ("$logic", row[8])).ConfigureAwait(false);
        }
        var documents = await ReadRowsAsync(source, "SELECT SmartFolderId,DocumentVersion,QueryJson,LegacyRulesBackupJson,UpdatedAt FROM SmartFolderQueryDocuments ORDER BY SmartFolderId;", 5, cancellationToken).ConfigureAwait(false);
        foreach (var row in documents)
        {
            var id = smart[Guid.Parse((string)row[0]!)]; var parsed = AssetQueryDocumentCodec.Parse((string)row[2]!); if (!parsed.IsValid || parsed.Document is null) throw new InvalidDataException($"智能文件夹文档无效：{parsed.ErrorMessage}");
            var rewritten = parsed.Document with { RootGroup = RewriteQueryNode(parsed.Document.RootGroup, folders, tags) };
            var validated = AssetQueryDocumentCodec.Normalize(rewritten); if (!validated.IsValid || validated.Document is null) throw new InvalidDataException($"智能文件夹引用重写后无效：{validated.ErrorMessage}");
            var canonical = AssetQueryDocumentCodec.SerializeCanonical(validated.Document); var hash = AssetQueryDocumentCodec.ComputeHash(validated.Document);
            await ExecuteAsync(target, transaction, "INSERT INTO SmartFolderQueryDocuments(SmartFolderId,DocumentVersion,QueryJson,QueryHash,LegacyRulesBackupJson,UpdatedAt) VALUES($id,$version,$json,$hash,$backup,$updated);", cancellationToken, ("$id", id.ToString("D")), ("$version", row[1]), ("$json", canonical), ("$hash", hash), ("$backup", row[3]), ("$updated", row[4])).ConfigureAwait(false);
        }
    }

    private static async Task MergeMembershipsAsync(SqliteConnection source, SqliteConnection target, SqliteTransaction transaction, IReadOnlyDictionary<Guid, Guid> assets, IReadOnlyDictionary<Guid, Guid> folders, IReadOnlyDictionary<Guid, Guid> tags, CancellationToken cancellationToken)
    {
        foreach (var row in await ReadRowsAsync(source, "SELECT AssetId,FolderId,AddedAt FROM AssetFolderMemberships;", 3, cancellationToken).ConfigureAwait(false))
            await ExecuteAsync(target, transaction, "INSERT OR IGNORE INTO AssetFolderMemberships(AssetId,FolderId,AddedAt) VALUES($asset,$folder,$at);", cancellationToken, ("$asset", assets[Guid.Parse((string)row[0]!)].ToString("D")), ("$folder", folders[Guid.Parse((string)row[1]!)].ToString("D")), ("$at", row[2])).ConfigureAwait(false);
        foreach (var row in await ReadRowsAsync(source, "SELECT AssetId,TagId,AddedAt FROM AssetTagMemberships;", 3, cancellationToken).ConfigureAwait(false))
            await ExecuteAsync(target, transaction, "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);", cancellationToken, ("$asset", assets[Guid.Parse((string)row[0]!)].ToString("D")), ("$tag", tags[Guid.Parse((string)row[1]!)].ToString("D")), ("$at", row[2])).ConfigureAwait(false);
    }

    private static async Task MergeFolderAutoTagsAsync(SqliteConnection source, SqliteConnection target, SqliteTransaction transaction, IReadOnlyDictionary<Guid, Guid> folders, IReadOnlyDictionary<Guid, Guid> tags, CancellationToken cancellationToken)
    {
        foreach (var row in await ReadRowsAsync(source, "SELECT FolderId,TagId FROM AssetFolderAutoTags;", 2, cancellationToken).ConfigureAwait(false))
            await ExecuteAsync(target, transaction, "INSERT OR IGNORE INTO AssetFolderAutoTags(FolderId,TagId) VALUES($folder,$tag);", cancellationToken,
                ("$folder", folders[Guid.Parse((string)row[0]!)].ToString("D")), ("$tag", tags[Guid.Parse((string)row[1]!)].ToString("D"))).ConfigureAwait(false);
    }

    private static async Task CopyMappedVisualTableAsync(SqliteConnection source, SqliteConnection target, SqliteTransaction transaction, string table, IReadOnlyDictionary<Guid, Guid> assets, CancellationToken cancellationToken)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "AssetVisualAnalysis", "AssetVisualFeatures", "AssetVisualPaletteColors" };
        if (!allowed.Contains(table)) throw new ArgumentException("不支持的视觉数据表。", nameof(table));
        var columns = new List<string>();
        await using (var schema = source.CreateCommand())
        {
            schema.CommandText = $"PRAGMA table_info({table});";
            await using var reader = await schema.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) columns.Add(reader.GetString(1));
        }
        if (columns.Count == 0 || columns[0] != "AssetId") throw new InvalidDataException($"视觉数据表结构无效：{table}");
        await using var read = source.CreateCommand(); read.CommandText = $"SELECT * FROM {table};";
        await using var rows = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await rows.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!Guid.TryParse(rows.GetString(0), out var sourceId) || !assets.TryGetValue(sourceId, out var targetId)) throw new InvalidDataException($"视觉数据包含悬空素材引用：{table}");
            await using var insert = target.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = $"INSERT OR IGNORE INTO {table}({string.Join(',', columns)}) VALUES({string.Join(',', columns.Select((_, index) => "$v" + index))});";
            for (var index = 0; index < columns.Count; index++) insert.Parameters.AddWithValue("$v" + index, index == 0 ? targetId.ToString("D") : rows.IsDBNull(index) ? DBNull.Value : rows.GetValue(index));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task InsertAssetAsync(SqliteConnection connection, SqliteTransaction transaction, MergeAsset asset, Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO AssetItems(AssetId,SourcePath,NormalizedSourcePath,DuplicateDiscriminator,DisplayName,Extension,MediaType,FileSize,ContentHash,Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode,ManagedCopyPath) VALUES($id,$source,$normalized,$duplicate,$name,$extension,$media,$size,$hash,$width,$height,$orientation,$capture,$added,$modified,$rating,$comment,$missing,$archived,$mode,$managed);", cancellationToken,
            ("$id", id.ToString("D")), ("$source", asset.SourcePath), ("$normalized", asset.SourcePath), ("$duplicate", id == asset.AssetId ? asset.DuplicateDiscriminator : id.ToString("N")), ("$name", asset.DisplayName), ("$extension", asset.Extension), ("$media", asset.MediaType), ("$size", asset.FileSize), ("$hash", asset.ContentHash), ("$width", asset.Width), ("$height", asset.Height), ("$orientation", asset.Orientation), ("$capture", asset.CaptureTime), ("$added", asset.AddedAt), ("$modified", asset.ModifiedAt), ("$rating", asset.Rating), ("$comment", asset.Comment), ("$missing", asset.IsMissing), ("$archived", asset.IsArchived), ("$mode", asset.ImportMode), ("$managed", asset.ManagedCopyPath)).ConfigureAwait(false);

    private static async Task<List<MergeAsset>> ReadAssetsAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(path, false, cancellationToken).ConfigureAwait(false);
        var rows = await ReadRowsAsync(connection, "SELECT AssetId,SourcePath,DuplicateDiscriminator,DisplayName,Extension,MediaType,FileSize,ContentHash,Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode,ManagedCopyPath FROM AssetItems ORDER BY AssetId;", 20, cancellationToken).ConfigureAwait(false);
        return rows.Select(row => new MergeAsset(Guid.Parse((string)row[0]!), (string)row[1]!, (string)row[2]!, (string)row[3]!, (string)row[4]!, (string)row[5]!, Convert.ToInt64(row[6]), row[7] as string, row[8], row[9], row[10], row[11], row[12]!, row[13]!, Convert.ToInt32(row[14]), (string)row[15]!, Convert.ToInt32(row[16]), Convert.ToInt32(row[17]), (string)row[18]!, row[19] as string)).ToList();
    }

    private static async Task<List<object?[]>> ReadRowsAsync(SqliteConnection connection, string sql, int fields, CancellationToken cancellationToken)
    { var rows = new List<object?[]>(); await using var command = connection.CreateCommand(); command.CommandText = sql; await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { var row = new object?[fields]; for (var i = 0; i < fields; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i); rows.Add(row); } return rows; }
    private static async Task<int> CountAsync(string path, string table, CancellationToken cancellationToken) { await using var connection = await OpenAsync(path, false, cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = $"SELECT COUNT(*) FROM {table};"; return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)); }
    private static async Task<SqliteConnection> OpenAsync(string path, bool write, CancellationToken cancellationToken) { var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = write ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly, Pooling = false }.ToString()); await connection.OpenAsync(cancellationToken).ConfigureAwait(false); await using var pragma = connection.CreateCommand(); pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;"; await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); return connection; }
    private static async Task EnsureMergeHistoryAsync(SqliteConnection connection, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = "CREATE TABLE IF NOT EXISTS AssetLibraryMergeHistory(OperationId TEXT NOT NULL PRIMARY KEY,SourceLibraryId TEXT NOT NULL,AppliedAt TEXT NOT NULL,Summary TEXT NOT NULL);"; await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
    private static async Task<bool> IsAppliedAsync(SqliteConnection connection, Guid operation, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = "SELECT EXISTS(SELECT 1 FROM AssetLibraryMergeHistory WHERE OperationId=$id);"; command.Parameters.AddWithValue("$id", operation.ToString("D")); return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 0; }
    private static async Task<bool> IdExistsAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string column, Guid id, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"SELECT EXISTS(SELECT 1 FROM {table} WHERE {column}=$id);"; command.Parameters.AddWithValue("$id", id.ToString("D")); return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 0; }
    private static async Task<Guid?> FindIdByNameAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string idColumn, string nameColumn, string name, Guid? group, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = group is null ? $"SELECT {idColumn} FROM {table} WHERE {nameColumn}=$name AND TagGroupId IS NULL LIMIT 1;" : $"SELECT {idColumn} FROM {table} WHERE {nameColumn}=$name AND TagGroupId=$group LIMIT 1;"; command.Parameters.AddWithValue("$name", name); if (group is not null) command.Parameters.AddWithValue("$group", group.Value.ToString("D")); var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false); return value is string text && Guid.TryParse(text, out var id) ? id : null; }
    private static async Task<Guid?> FindSimpleIdByNameAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string idColumn, string nameColumn, string name, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"SELECT {idColumn} FROM {table} WHERE {nameColumn}=$name LIMIT 1;"; command.Parameters.AddWithValue("$name", name); var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false); return value is string text && Guid.TryParse(text, out var id) ? id : null; }
    private static async Task<string> UniqueNameAsync(SqliteConnection connection, SqliteTransaction transaction, string proposed, CancellationToken cancellationToken) { for (var i = 1; i < 10_000; i++) { var value = i == 1 ? proposed : $"{proposed}（导入 {i}）"; await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT EXISTS(SELECT 1 FROM SmartFolders WHERE Name=$name);"; command.Parameters.AddWithValue("$name", value); if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 0) return value; } throw new InvalidOperationException("无法为智能文件夹生成唯一名称。"); }
    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
    private static string RewriteIds(string value, IReadOnlyDictionary<Guid, Guid> map) { foreach (var item in map) { value = value.Replace(item.Key.ToString("D"), item.Value.ToString("D"), StringComparison.OrdinalIgnoreCase); value = value.Replace(item.Key.ToString("N"), item.Value.ToString("N"), StringComparison.OrdinalIgnoreCase); } return value; }
    private static AssetQueryNode RewriteQueryNode(AssetQueryNode node, IReadOnlyDictionary<Guid, Guid> folders, IReadOnlyDictionary<Guid, Guid> tags)
    {
        if (node.Kind == AssetQueryNodeKind.Group) return node with { Children = node.Children.Select(child => RewriteQueryNode(child, folders, tags)).ToArray() };
        var map = node.Field switch { AssetQueryField.Folder => folders, AssetQueryField.Tag => tags, _ => null };
        return map is null ? node : node with { Values = node.Values.Select(value => RewriteIds(value, map)).ToArray() };
    }
    private static async Task CopyVerifiedAsync(string source, string destination, string? expectedHash, CancellationToken cancellationToken) { await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true); await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[1024 * 1024]; while (true) { var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); if (read == 0) break; hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false); } var actual = Convert.ToHexString(hash.GetHashAndReset()); if (!string.IsNullOrWhiteSpace(expectedHash) && !string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase)) throw new IOException($"托管素材复制校验失败：{source}"); }
    private static void TryDeleteDirectory(string path) { try { new AssetLibraryDatabase(Path.Combine(path, "database", "asset-library-v16.db")).ClearConnectionPool(); if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    private sealed class MergeAsset(Guid assetId, string sourcePath, string duplicateDiscriminator, string displayName, string extension, string mediaType, long fileSize, string? contentHash, object? width, object? height, object? orientation, object? captureTime, object addedAt, object modifiedAt, int rating, string comment, int isMissing, int isArchived, string importMode, string? managedCopyPath)
    {
        public Guid AssetId { get; } = assetId; public string SourcePath { get; set; } = sourcePath; public string DuplicateDiscriminator { get; } = duplicateDiscriminator; public string DisplayName { get; } = displayName; public string Extension { get; } = extension; public string MediaType { get; } = mediaType; public long FileSize { get; } = fileSize; public string? ContentHash { get; } = contentHash; public object? Width { get; } = width; public object? Height { get; } = height; public object? Orientation { get; } = orientation; public object? CaptureTime { get; } = captureTime; public object AddedAt { get; } = addedAt; public object ModifiedAt { get; } = modifiedAt; public int Rating { get; } = rating; public string Comment { get; } = comment; public int IsMissing { get; } = isMissing; public int IsArchived { get; } = isArchived; public string ImportMode { get; set; } = importMode; public string? ManagedCopyPath { get; set; } = managedCopyPath; public bool IsManaged => string.Equals(ImportMode, "ManagedCopy", StringComparison.OrdinalIgnoreCase);
    }
}
