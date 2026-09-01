using System.Text.Json;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed partial class SqliteAssetLibraryRepository
{
    private const int UndoJournalLimit = 100;

    public async Task<AssetLibraryBatchResult> UpdateAssetsMetadataAsync(IEnumerable<Guid> assetIds, int? rating = null, string? comment = null, CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Distinct().ToArray();
        if (ids.Length == 0) return new(0, null, []);
        if (rating is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(rating));
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var previous = new List<AssetMetadataUndo>();
        var next = new List<AssetMetadataUndo>();
        foreach (var id in ids)
        {
            await using var read = connection.CreateCommand(); read.Transaction = transaction; read.CommandText = "SELECT Rating,Comment FROM AssetItems WHERE AssetId=$id;"; read.Parameters.AddWithValue("$id", id.ToString("D"));
            await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) continue;
            var oldRating = reader.GetInt32(0); var oldComment = reader.GetString(1);
            await reader.DisposeAsync().ConfigureAwait(false);
            var nextRating = rating ?? oldRating; var nextComment = comment ?? oldComment;
            if (nextRating == oldRating && string.Equals(nextComment, oldComment, StringComparison.Ordinal)) continue;
            previous.Add(new(id, oldRating, oldComment));
            next.Add(new(id, nextRating, nextComment));
            await ExecuteAsync(connection, transaction, "UPDATE AssetItems SET Rating=$rating,Comment=$comment WHERE AssetId=$id;", cancellationToken, ("$rating", nextRating), ("$comment", nextComment), ("$id", id.ToString("D"))).ConfigureAwait(false);
        }
        if (previous.Count == 0) { await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return new(0, null, []); }
        var token = CreateUndoToken("Update asset metadata batch");
        await WriteUndoJournalAsync(connection, transaction, token, "asset-metadata-v2", new AssetMetadataChange(previous.ToArray(), next.ToArray()), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(previous.Count, token, []);
    }

    public Task<AssetLibraryBatchResult> SetAssetsArchivedAsync(IEnumerable<Guid> assetIds, bool isArchived, CancellationToken cancellationToken = default) =>
        SetAssetFlagAsync(assetIds, "IsArchived", isArchived, "asset-archive-state-v2", isArchived ? "Archive assets" : "Restore assets", cancellationToken);

    public Task<AssetLibraryBatchResult> SetAssetsMissingAsync(IEnumerable<Guid> assetIds, bool isMissing, CancellationToken cancellationToken = default) =>
        SetAssetFlagAsync(assetIds, "IsMissing", isMissing, "asset-missing-state-v2", isMissing ? "Mark assets missing" : "Clear missing assets", cancellationToken);

    private async Task<AssetLibraryBatchResult> SetAssetFlagAsync(
        IEnumerable<Guid> assetIds,
        string column,
        bool value,
        string operationKind,
        string description,
        CancellationToken cancellationToken)
    {
        var ids = assetIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new(0, null, []);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var before = new List<AssetFlagState>();
        foreach (var id in ids)
        {
            await using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText = $"SELECT {column} FROM AssetItems WHERE AssetId=$id;";
            read.Parameters.AddWithValue("$id", id.ToString("D"));
            var scalar = await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (scalar is null or DBNull) continue;
            var previous = Convert.ToInt32(scalar, CultureInfo.InvariantCulture) != 0;
            if (previous == value) continue;
            before.Add(new(id, previous));
            await ExecuteAsync(connection, transaction, $"UPDATE AssetItems SET {column}=$value WHERE AssetId=$id;", cancellationToken,
                ("$value", value ? 1 : 0), ("$id", id.ToString("D"))).ConfigureAwait(false);
        }
        if (before.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(0, null, []);
        }

        var token = CreateUndoToken(description);
        var envelope = new AssetFlagChange(before.ToArray(), before.Select(item => item with { Value = value }).ToArray());
        await WriteUndoJournalAsync(connection, transaction, token, operationKind, envelope, cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(before.Count, token, []);
    }

    public async Task<AssetLibraryBatchResult> AddToFoldersAsync(IEnumerable<Guid> assetIds, IEnumerable<Guid> folderIds, CancellationToken cancellationToken = default)
    {
        var assets = assetIds.Distinct().ToArray(); var folders = folderIds.Distinct().ToArray();
        if (assets.Length == 0 || folders.Length == 0) return new(0, null, []);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var memberships = new List<AssetFolderMembership>(); var introducedAutoTags = new List<AssetTagMembership>(); var changedAt = DateTimeOffset.UtcNow;
        foreach (var folderId in folders)
        {
            var autoTags = new List<Guid>();
            await using (var readTags = connection.CreateCommand())
            {
                readTags.Transaction = transaction; readTags.CommandText = "SELECT TagId FROM AssetFolderAutoTags WHERE FolderId=$folder;"; readTags.Parameters.AddWithValue("$folder", folderId.ToString("D"));
                await using var reader = await readTags.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) autoTags.Add(Guid.Parse(reader.GetString(0)));
            }
            foreach (var assetId in assets)
            {
                await using var add = connection.CreateCommand(); add.Transaction = transaction; add.CommandText = "INSERT OR IGNORE INTO AssetFolderMemberships(AssetId,FolderId,AddedAt) VALUES($asset,$folder,$at);"; add.Parameters.AddWithValue("$asset", assetId.ToString("D")); add.Parameters.AddWithValue("$folder", folderId.ToString("D")); add.Parameters.AddWithValue("$at", changedAt.ToString("O"));
                if (await add.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 0) continue;
                memberships.Add(new(assetId, folderId, changedAt));
                foreach (var tagId in autoTags)
                {
                    await using var tag = connection.CreateCommand(); tag.Transaction = transaction; tag.CommandText = "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);"; tag.Parameters.AddWithValue("$asset", assetId.ToString("D")); tag.Parameters.AddWithValue("$tag", tagId.ToString("D")); tag.Parameters.AddWithValue("$at", changedAt.ToString("O"));
                    if (await tag.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 0) introducedAutoTags.Add(new(assetId, tagId, changedAt));
                }
            }
        }
        if (memberships.Count == 0) { await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return new(0, null, []); }
        var payload = new FolderMembershipChange(false, true, memberships.ToArray(), introducedAutoTags.ToArray());
        var token = CreateUndoToken("Add assets to folders");
        await WriteUndoJournalAsync(connection, transaction, token, "folder-membership-v2", payload, cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(memberships.Count, token, []);
    }

    private async Task<AssetLibraryPage> QueryIndexedPageAsync(AssetLibraryQuery query, int pageSize, AssetLibraryQueryCursor? cursor, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var count = connection.CreateCommand();
        var countWhere = BuildIndexedWhere(query, count);
        count.CommandText = "SELECT COUNT(*) FROM AssetItems a WHERE " + countWhere + ";";
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

        await using var page = connection.CreateCommand();
        var pageWhere = BuildIndexedWhere(query, page);
        if (cursor is not null) pageWhere = AddQueryCursorPredicate(pageWhere, query, cursor, page);
        page.CommandText = SelectAssetSql + " WHERE " + pageWhere + " ORDER BY " + BuildQueryOrderBy(query) + " LIMIT $limit;";
        page.Parameters.AddWithValue("$limit", pageSize + 1);
        var items = new List<AssetItem>(Math.Min(pageSize + 1, total));
        await using var reader = await page.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadAsset(reader));
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        var next = hasMore && items.Count > 0 ? await CreateQueryCursorAsync(query, items[^1], cancellationToken).ConfigureAwait(false) : null;
        return new(items, next, total);
    }

    private static string BuildIndexedWhere(AssetLibraryQuery query, SqliteCommand command)
    {
        var where = new List<string> { query.EffectiveArchiveScope switch
        {
            AssetLibraryArchiveScope.ArchivedOnly => "a.IsArchived=1",
            AssetLibraryArchiveScope.All => "1=1",
            _ => "a.IsArchived=0"
        } };
        if (query.SystemCollection == AssetLibrarySystemCollection.RecycleBin) where.Add("0=1");
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            where.Add("(a.DisplayName LIKE $search OR a.Extension LIKE $search OR a.Comment LIKE $search OR EXISTS(SELECT 1 FROM AssetTagMemberships sm JOIN AssetTags st ON st.TagId=sm.TagId WHERE sm.AssetId=a.AssetId AND st.Name LIKE $search) OR EXISTS(SELECT 1 FROM AssetFolderMemberships sfm JOIN AssetFolders sf ON sf.FolderId=sfm.FolderId WHERE sfm.AssetId=a.AssetId AND sf.Name LIKE $search))");
            command.Parameters.AddWithValue("$search", "%" + query.SearchText.Trim() + "%");
        }
        if (query.FolderId is not null) { where.Add("EXISTS(SELECT 1 FROM AssetFolderMemberships fm WHERE fm.AssetId=a.AssetId AND fm.FolderId=$folder)"); command.Parameters.AddWithValue("$folder", query.FolderId.Value.ToString("D")); }
        if (query.TagId is not null) { where.Add("EXISTS(SELECT 1 FROM AssetTagMemberships tm WHERE tm.AssetId=a.AssetId AND tm.TagId=$tag)"); command.Parameters.AddWithValue("$tag", query.TagId.Value.ToString("D")); }
        AddMembershipFilters(where, command, query.FolderIds, "AssetFolderMemberships", "FolderId", "folderList");
        AddMembershipFilters(where, command, query.TagIds, "AssetTagMemberships", "TagId", "tagList");
        var minimumRating = query.SystemCollection == AssetLibrarySystemCollection.HighRating ? Math.Max(4, query.MinimumRating ?? 0) : query.MinimumRating;
        if (minimumRating is not null) { where.Add("a.Rating >= $minRating"); command.Parameters.AddWithValue("$minRating", minimumRating.Value); }
        if (query.MaximumRating is not null) { where.Add("a.Rating <= $maxRating"); command.Parameters.AddWithValue("$maxRating", query.MaximumRating.Value); }
        if (!string.IsNullOrWhiteSpace(query.MediaType)) { where.Add("a.MediaType=$mediaType"); command.Parameters.AddWithValue("$mediaType", query.MediaType); }
        if (!string.IsNullOrWhiteSpace(query.Extension)) { where.Add("a.Extension=$extension"); command.Parameters.AddWithValue("$extension", NormalizeExtension(query.Extension)); }
        if (query.UncategorizedOnly || query.SystemCollection == AssetLibrarySystemCollection.Uncategorized) where.Add("NOT EXISTS(SELECT 1 FROM AssetFolderMemberships uf WHERE uf.AssetId=a.AssetId)");
        if (query.UntaggedOnly || query.SystemCollection == AssetLibrarySystemCollection.Untagged) where.Add("NOT EXISTS(SELECT 1 FROM AssetTagMemberships ut WHERE ut.AssetId=a.AssetId)");
        if (query.MissingOnly || query.SystemCollection == AssetLibrarySystemCollection.MissingFiles) where.Add("a.IsMissing=1");
        if (query.AddedFrom is not null) { where.Add("a.AddedAt >= $addedFrom"); command.Parameters.AddWithValue("$addedFrom", query.AddedFrom.Value.ToString("O")); }
        if (query.AddedTo is not null) { where.Add("a.AddedAt <= $addedTo"); command.Parameters.AddWithValue("$addedTo", query.AddedTo.Value.ToString("O")); }
        if (query.CaptureFrom is not null) { where.Add("a.CaptureTime >= $captureFrom"); command.Parameters.AddWithValue("$captureFrom", query.CaptureFrom.Value.ToString("O")); }
        if (query.CaptureTo is not null) { where.Add("a.CaptureTime <= $captureTo"); command.Parameters.AddWithValue("$captureTo", query.CaptureTo.Value.ToString("O")); }
        return string.Join(" AND ", where);
    }

    private static void AddMembershipFilters(List<string> where, SqliteCommand command, IReadOnlyList<Guid>? values, string table, string column, string parameterPrefix)
    {
        if (values is null) return;
        foreach (var value in values.Distinct())
        {
            var name = $"${parameterPrefix}{command.Parameters.Count}";
            where.Add($"EXISTS(SELECT 1 FROM {table} mf WHERE mf.AssetId=a.AssetId AND mf.{column}={name})");
            command.Parameters.AddWithValue(name, value.ToString("D"));
        }
    }

    private async Task<AssetLibraryPage> QuerySmartFolderPageAsync(AssetLibraryQuery query, int pageSize, AssetLibraryQueryCursor? cursor, CancellationToken cancellationToken)
    {
        var smartFolder = await ReadSmartFolderAsync(query.SmartFolderId!.Value, cancellationToken).ConfigureAwait(false);
        if (smartFolder is null) return new([], null, 0);
        var rules = await ListSmartFolderRulesAsync(smartFolder.SmartFolderId, cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var count = connection.CreateCommand();
        var countWhere = BuildIndexedWhere(query with { SmartFolderId = null, Cursor = null }, count);
        countWhere += " AND " + BuildSmartExpression(smartFolder, rules, count);
        count.CommandText = "SELECT COUNT(*) FROM AssetItems a WHERE " + countWhere + ";";
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

        await using var page = connection.CreateCommand();
        var pageWhere = BuildIndexedWhere(query with { SmartFolderId = null, Cursor = null }, page);
        pageWhere += " AND " + BuildSmartExpression(smartFolder, rules, page);
        if (cursor is not null) pageWhere = AddQueryCursorPredicate(pageWhere, query, cursor, page);
        page.CommandText = SelectAssetSql + " WHERE " + pageWhere + " ORDER BY " + BuildQueryOrderBy(query) + " LIMIT $limit;";
        page.Parameters.AddWithValue("$limit", pageSize + 1);
        var items = new List<AssetItem>(pageSize + 1);
        await using var reader = await page.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadAsset(reader));
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(items, hasMore && items.Count > 0 ? await CreateQueryCursorAsync(query, items[^1], cancellationToken).ConfigureAwait(false) : null, total);
    }

    private static string BuildSmartExpression(SmartFolder folder, IReadOnlyList<SmartFolderRule> rules, SqliteCommand command)
    {
        if (rules.Count == 0) return "1=1";
        var expressions = new List<string>();
        foreach (var rule in rules.Where(rule => rule.GroupId is null)) expressions.Add(BuildSmartRule(rule, command));
        foreach (var group in rules.Where(rule => rule.GroupId is not null).GroupBy(rule => rule.GroupId))
        {
            var join = group.First().GroupLogic == SmartFolderLogic.Or ? " OR " : " AND ";
            expressions.Add("(" + string.Join(join, group.Select(rule => BuildSmartRule(rule, command))) + ")");
        }
        var rootJoin = folder.Logic == SmartFolderLogic.Or ? " OR " : " AND ";
        return "(" + string.Join(rootJoin, expressions) + ")";
    }

    private static string BuildSmartRule(SmartFolderRule rule, SqliteCommand command)
    {
        if (IsVisualSmartField(rule.Field)) return BuildVisualSmartRule(rule, command);
        var parameter = $"$smart{command.Parameters.Count}";
        var expression = rule.Field switch
        {
            SmartFolderField.Folder => BuildMembershipNameRule("AssetFolderMemberships", "AssetFolders", "FolderId", rule, parameter),
            SmartFolderField.Tag => BuildMembershipNameRule("AssetTagMemberships", "AssetTags", "TagId", rule, parameter),
            SmartFolderField.Rating => BuildScalarRule("a.Rating", rule, parameter),
            SmartFolderField.FileSize => BuildScalarRule("a.FileSize", rule, parameter),
            SmartFolderField.Width => BuildScalarRule("COALESCE(a.Width,0)", rule, parameter),
            SmartFolderField.Height => BuildScalarRule("COALESCE(a.Height,0)", rule, parameter),
            SmartFolderField.AspectRatio => BuildScalarRule("CASE WHEN COALESCE(a.Height,0)=0 THEN 0 ELSE CAST(a.Width AS REAL)/a.Height END", rule, parameter),
            SmartFolderField.AddedAt => BuildScalarRule("a.AddedAt", rule, parameter),
            SmartFolderField.CaptureTime => BuildScalarRule("a.CaptureTime", rule, parameter),
            SmartFolderField.IsMissing => BuildBooleanRule("a.IsMissing=1", rule),
            SmartFolderField.IsUncategorized => BuildBooleanRule("NOT EXISTS(SELECT 1 FROM AssetFolderMemberships sf WHERE sf.AssetId=a.AssetId)", rule),
            SmartFolderField.IsUntagged => BuildBooleanRule("NOT EXISTS(SELECT 1 FROM AssetTagMemberships st WHERE st.AssetId=a.AssetId)", rule),
            SmartFolderField.FileName => BuildTextRule("a.DisplayName", rule, parameter),
            SmartFolderField.Extension => BuildTextRule("a.Extension", rule, parameter),
            SmartFolderField.MediaType => BuildTextRule("a.MediaType", rule, parameter),
            SmartFolderField.Comment => BuildTextRule("a.Comment", rule, parameter),
            SmartFolderField.Orientation => BuildTextRule("COALESCE(a.Orientation,'')", rule, parameter),
            _ => "0=1"
        };
        if (UsesSmartParameter(rule)) command.Parameters.AddWithValue(parameter, SmartParameterValue(rule));
        return rule.Negated ? $"NOT ({expression})" : $"({expression})";
    }

    private static bool IsVisualSmartField(SmartFolderField field) => field is
        SmartFolderField.VisualAnalysisStatus or SmartFolderField.VisualHarmony or SmartFolderField.VisualToneKey or
        SmartFolderField.VisualContrast or SmartFolderField.VisualSaturation or SmartFolderField.VisualWarmCool or
        SmartFolderField.VisualDominantHue or SmartFolderField.VisualDominantColor or SmartFolderField.VisualAverageLuma or
        SmartFolderField.VisualAverageSaturation or SmartFolderField.VisualLumaSpread or SmartFolderField.VisualShadowRatio or
        SmartFolderField.VisualHighlightRatio or SmartFolderField.VisualBlackClipRatio or SmartFolderField.VisualWhiteClipRatio;

    private static string BuildVisualSmartRule(SmartFolderRule rule, SqliteCommand command)
    {
        ValidateVisualSmartRule(rule);
        var version = $"$smartVersion{command.Parameters.Count}";
        command.Parameters.AddWithValue(version, AssetVisualFeatureContract.AnalysisVersion);
        string expression;
        if (rule.Field == SmartFolderField.VisualAnalysisStatus)
        {
            var state = $"CASE WHEN NOT EXISTS(SELECT 1 FROM AssetVisualFeatures avs WHERE avs.AssetId=a.AssetId) THEN 'NotAnalyzed' " +
                        $"WHEN NOT EXISTS(SELECT 1 FROM AssetVisualFeatures avs WHERE avs.AssetId=a.AssetId AND avs.AnalysisVersion={version}) THEN 'Stale' " +
                        $"WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures avs WHERE avs.AssetId=a.AssetId AND avs.AnalysisVersion={version} AND avs.SourceContentHash IS NOT NULL AND a.ContentHash IS NOT NULL AND avs.SourceContentHash=a.ContentHash AND avs.Outcome='Succeeded') THEN 'Valid' " +
                        $"WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures avs WHERE avs.AssetId=a.AssetId AND avs.AnalysisVersion={version} AND avs.SourceContentHash IS NOT NULL AND a.ContentHash IS NOT NULL AND avs.SourceContentHash=a.ContentHash AND avs.Outcome='Failed') THEN 'Failed' ELSE 'Stale' END";
            var value = $"$smartState{command.Parameters.Count}"; command.Parameters.AddWithValue(value, NormalizeVisualState(rule.Value));
            expression = rule.Operator == SmartFolderOperator.NotEquals ? $"{state}<>{value}" : $"{state}={value}";
        }
        else
        {
            var valid = $"vf.AssetId=a.AssetId AND vf.AnalysisVersion={version} AND vf.Outcome='Succeeded' AND vf.SourceContentHash IS NOT NULL AND a.ContentHash IS NOT NULL AND vf.SourceContentHash=a.ContentHash";
            if (rule.Field == SmartFolderField.VisualDominantHue)
            {
                var (start, end) = ParseRange(rule.Value);
                var startName = $"$smartHueStart{command.Parameters.Count}"; command.Parameters.AddWithValue(startName, start);
                var endName = $"$smartHueEnd{command.Parameters.Count}"; command.Parameters.AddWithValue(endName, end);
                var hue = start <= end ? $"pc.Hue BETWEEN {startName} AND {endName}" : $"(pc.Hue>={startName} OR pc.Hue<={endName})";
                expression = $"a.AssetId IN (SELECT pc.AssetId FROM AssetVisualPaletteColors pc JOIN AssetVisualFeatures vf ON vf.AssetId=pc.AssetId AND vf.AnalysisVersion=pc.AnalysisVersion JOIN AssetItems va ON va.AssetId=vf.AssetId WHERE vf.AnalysisVersion={version} AND vf.Outcome='Succeeded' AND vf.SourceContentHash IS NOT NULL AND va.ContentHash IS NOT NULL AND vf.SourceContentHash=va.ContentHash AND pc.Weight>={AssetVisualFeatureContract.MinimumPaletteWeight.ToString(CultureInfo.InvariantCulture)} AND pc.Saturation>=0.08 AND pc.Chroma>=8 AND {hue})";
            }
            else if (rule.Field == SmartFolderField.VisualDominantColor)
            {
                var (target, maximumDelta) = ParseColor(rule.Value);
                var l = $"$smartLabL{command.Parameters.Count}"; command.Parameters.AddWithValue(l, target.L);
                var aValue = $"$smartLabA{command.Parameters.Count}"; command.Parameters.AddWithValue(aValue, target.A);
                var bValue = $"$smartLabB{command.Parameters.Count}"; command.Parameters.AddWithValue(bValue, target.B);
                var delta = $"$smartDelta{command.Parameters.Count}"; command.Parameters.AddWithValue(delta, maximumDelta * maximumDelta);
                expression = $"EXISTS(SELECT 1 FROM AssetVisualFeatures vf JOIN AssetVisualPaletteColors pc ON pc.AssetId=vf.AssetId AND pc.AnalysisVersion=vf.AnalysisVersion WHERE {valid} AND pc.Weight>={AssetVisualFeatureContract.MinimumPaletteWeight.ToString(CultureInfo.InvariantCulture)} AND ((pc.LabL-{l})*(pc.LabL-{l})+(pc.LabA-{aValue})*(pc.LabA-{aValue})+(pc.LabB-{bValue})*(pc.LabB-{bValue}))<={delta})";
            }
            else
            {
                var column = rule.Field switch
                {
                    SmartFolderField.VisualHarmony => "vf.Harmony",
                    SmartFolderField.VisualToneKey => "vf.ToneKey",
                    SmartFolderField.VisualContrast => "vf.Contrast",
                    SmartFolderField.VisualSaturation => "vf.Saturation",
                    SmartFolderField.VisualWarmCool => "vf.WarmCool",
                    SmartFolderField.VisualAverageLuma => "vf.AverageLuma",
                    SmartFolderField.VisualAverageSaturation => "vf.AverageSaturation",
                    SmartFolderField.VisualLumaSpread => "vf.LumaSpreadMetric",
                    SmartFolderField.VisualShadowRatio => "vf.ShadowRatio",
                    SmartFolderField.VisualHighlightRatio => "vf.HighlightRatio",
                    SmartFolderField.VisualBlackClipRatio => "vf.BlackClipRatio",
                    SmartFolderField.VisualWhiteClipRatio => "vf.WhiteClipRatio",
                    _ => "NULL"
                };
                var value = $"$smartVisual{command.Parameters.Count}"; command.Parameters.AddWithValue(value, rule.Value);
                var condition = rule.Field is SmartFolderField.VisualHarmony or SmartFolderField.VisualToneKey or SmartFolderField.VisualContrast or SmartFolderField.VisualSaturation or SmartFolderField.VisualWarmCool
                    ? BuildTextRule(column, rule, value)
                    : BuildScalarRule(column, rule, value);
                expression = $"EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE {valid} AND {condition})";
            }
        }
        return rule.Negated ? $"NOT ({expression})" : $"({expression})";
    }

    private static void ValidateVisualSmartRule(SmartFolderRule rule)
    {
        static bool IsNumeric(SmartFolderField field) => field is SmartFolderField.VisualAverageLuma or SmartFolderField.VisualAverageSaturation or SmartFolderField.VisualLumaSpread or SmartFolderField.VisualShadowRatio or SmartFolderField.VisualHighlightRatio or SmartFolderField.VisualBlackClipRatio or SmartFolderField.VisualWhiteClipRatio;
        static bool IsNumericOperator(SmartFolderOperator value) => value is SmartFolderOperator.Equals or SmartFolderOperator.NotEquals or SmartFolderOperator.GreaterThan or SmartFolderOperator.GreaterThanOrEqual or SmartFolderOperator.LessThan or SmartFolderOperator.LessThanOrEqual;
        if (rule.Field == SmartFolderField.VisualAnalysisStatus && rule.Operator is not (SmartFolderOperator.Equals or SmartFolderOperator.NotEquals))
            throw new ArgumentException("VisualAnalysisStatus supports Equals or NotEquals only.");
        if (rule.Field is SmartFolderField.VisualDominantHue or SmartFolderField.VisualDominantColor && rule.Operator is not (SmartFolderOperator.Equals or SmartFolderOperator.InRange))
            throw new ArgumentException($"{rule.Field} supports Equals or InRange only.");
        if (IsNumeric(rule.Field) && !IsNumericOperator(rule.Operator))
            throw new ArgumentException($"{rule.Field} requires a numeric comparison operator.");
        if (rule.Field is SmartFolderField.VisualHarmony or SmartFolderField.VisualToneKey or SmartFolderField.VisualContrast or SmartFolderField.VisualSaturation or SmartFolderField.VisualWarmCool && rule.Operator is not (SmartFolderOperator.Equals or SmartFolderOperator.NotEquals))
            throw new ArgumentException($"{rule.Field} supports Equals or NotEquals only.");
    }

    private static (double Start, double End) ParseRange(string value)
    {
        var parts = value.Split(["..", ",", "，"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var start) || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var end))
            throw new ArgumentException($"Invalid range '{value}'. Use start..end.");
        static double Normalize(double hue) => (hue % 360 + 360) % 360;
        return (Normalize(start), Normalize(end));
    }

    private static string NormalizeVisualState(string value)
    {
        var compact = new string(value.Where(char.IsLetterOrDigit).ToArray());
        if (compact.Equals("Analyzed", StringComparison.OrdinalIgnoreCase) || compact.Equals("Valid", StringComparison.OrdinalIgnoreCase)) return AssetVisualFeatureState.Valid.ToString();
        if (compact.Equals("NotAnalyzed", StringComparison.OrdinalIgnoreCase)) return AssetVisualFeatureState.NotAnalyzed.ToString();
        if (compact.Equals("Failed", StringComparison.OrdinalIgnoreCase)) return AssetVisualFeatureState.Failed.ToString();
        return AssetVisualFeatureState.Stale.ToString();
    }

    private static (VisualLab Lab, double MaximumDelta) ParseColor(string value)
    {
        var parts = value.Split('@', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var hex = parts[0].TrimStart('#');
        if (hex.Length != 6 || !byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) || !byte.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) || !byte.TryParse(hex[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            throw new ArgumentException($"Invalid visual color '{value}'. Use #RRGGBB or #RRGGBB@DeltaE.");
        var maximum = parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? Math.Max(0, parsed) : 20;
        return (VisualAnalysisEngine.ToLab(new(red, green, blue)), maximum);
    }

    private static bool UsesSmartParameter(SmartFolderRule rule) => rule.Operator is not (SmartFolderOperator.IsTrue or SmartFolderOperator.IsFalse);
    private static object SmartParameterValue(SmartFolderRule rule) => rule.Operator switch
    {
        SmartFolderOperator.Contains => $"%{rule.Value}%",
        SmartFolderOperator.StartsWith => $"{rule.Value}%",
        SmartFolderOperator.EndsWith => $"%{rule.Value}",
        _ => rule.Value
    };
    private static string BuildTextRule(string column, SmartFolderRule rule, string parameter) => rule.Operator switch
    {
        SmartFolderOperator.Contains or SmartFolderOperator.StartsWith or SmartFolderOperator.EndsWith => $"{column} LIKE {parameter} COLLATE NOCASE",
        SmartFolderOperator.Equals => $"{column}={parameter} COLLATE NOCASE",
        SmartFolderOperator.NotEquals => $"{column}<>{parameter} COLLATE NOCASE",
        SmartFolderOperator.IsTrue => $"length(trim({column}))>0",
        SmartFolderOperator.IsFalse => $"length(trim({column}))=0",
        _ => "0=1"
    };
    private static string BuildScalarRule(string column, SmartFolderRule rule, string parameter) => rule.Operator switch
    {
        SmartFolderOperator.Equals => $"{column}={parameter}", SmartFolderOperator.NotEquals => $"{column}<>{parameter}",
        SmartFolderOperator.GreaterThan => $"{column}>{parameter}", SmartFolderOperator.GreaterThanOrEqual => $"{column}>={parameter}",
        SmartFolderOperator.LessThan => $"{column}<{parameter}", SmartFolderOperator.LessThanOrEqual => $"{column}<={parameter}", _ => "0=1"
    };
    private static string BuildBooleanRule(string truthExpression, SmartFolderRule rule) => rule.Operator switch
    {
        SmartFolderOperator.IsTrue or SmartFolderOperator.Equals => truthExpression,
        SmartFolderOperator.IsFalse or SmartFolderOperator.NotEquals => $"NOT ({truthExpression})",
        _ => "0=1"
    };
    private static string BuildMembershipNameRule(string memberships, string entities, string idColumn, SmartFolderRule rule, string parameter)
    {
        var nameExpression = BuildTextRule("e.Name", rule with { Operator = rule.Operator == SmartFolderOperator.NotEquals ? SmartFolderOperator.Equals : rule.Operator }, parameter);
        var exists = $"EXISTS(SELECT 1 FROM {memberships} m JOIN {entities} e ON e.{idColumn}=m.{idColumn} WHERE m.AssetId=a.AssetId AND {nameExpression})";
        return rule.Operator == SmartFolderOperator.NotEquals ? $"NOT ({exists})" : exists;
    }

    private async Task<string> CreateQueryCursorAsync(AssetLibraryQuery query, AssetItem item, CancellationToken cancellationToken)
    {
        var sortValue = await GetQuerySortValueAsync(item, query.EffectiveSortField, cancellationToken).ConfigureAwait(false);
        var envelope = new AssetLibraryQueryCursor(
            Version: 1,
            PlanFingerprint: ComputeQueryFingerprint(query),
            NullBucket: sortValue is null ? 1 : 0,
            SortValue: sortValue,
            AssetId: item.AssetId);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope)));
    }

    private async Task<string?> GetQuerySortValueAsync(AssetItem item, AssetLibrarySortField field, CancellationToken cancellationToken)
    {
        if (field is not (AssetLibrarySortField.Color or AssetLibrarySortField.VisualAnalysis)) return GetQuerySortValue(item, field);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {GetQuerySortExpression(field)} FROM AssetItems a WHERE a.AssetId=$id;";
        command.Parameters.AddWithValue("$id", item.AssetId.ToString("D"));
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null or DBNull) return null;
        return field == AssetLibrarySortField.Color
            ? Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture)
            : Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryReadQueryCursor(AssetLibraryQuery query, out AssetLibraryQueryCursor? cursor, out string? error)
    {
        cursor = null;
        error = null;
        if (string.IsNullOrWhiteSpace(query.Cursor)) return true;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(query.Cursor));
            cursor = JsonSerializer.Deserialize<AssetLibraryQueryCursor>(json);
            if (cursor is null || cursor.Version != 1 || cursor.AssetId == Guid.Empty || cursor.NullBucket is < 0 or > 1 ||
                !string.Equals(cursor.PlanFingerprint, ComputeQueryFingerprint(query), StringComparison.Ordinal) ||
                !IsValidQueryCursorValue(query.EffectiveSortField, cursor.NullBucket, cursor.SortValue))
            {
                cursor = null;
                error = "分页游标与当前查询或排序不匹配。";
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            cursor = null;
            error = "分页游标格式无效。";
            return false;
        }
    }

    private static string AddQueryCursorPredicate(string where, AssetLibraryQuery query, AssetLibraryQueryCursor cursor, SqliteCommand command)
    {
        var expression = GetQuerySortExpression(query.EffectiveSortField);
        var nullExpression = $"CASE WHEN {expression} IS NULL THEN 1 ELSE 0 END";
        command.Parameters.AddWithValue("$cursorAsset", cursor.AssetId.ToString("D"));
        command.Parameters.AddWithValue("$cursorNull", cursor.NullBucket);
        if (cursor.NullBucket == 1)
            return where + $" AND ({nullExpression}=1 AND a.AssetId>$cursorAsset)";

        command.Parameters.AddWithValue("$cursorSort", ParseQueryCursorValue(query.EffectiveSortField, cursor.SortValue!));
        var comparison = query.EffectiveSortDirection == AssetLibrarySortDirection.Ascending ? ">" : "<";
        return where + $" AND ({nullExpression}>$cursorNull OR ({nullExpression}=$cursorNull AND (({expression}){comparison}$cursorSort OR (({expression})=$cursorSort AND a.AssetId>$cursorAsset))))";
    }

    private static string BuildQueryOrderBy(AssetLibraryQuery query)
    {
        var expression = GetQuerySortExpression(query.EffectiveSortField);
        var direction = query.EffectiveSortDirection == AssetLibrarySortDirection.Ascending ? "ASC" : "DESC";
        return $"CASE WHEN {expression} IS NULL THEN 1 ELSE 0 END ASC,{expression} {direction},a.AssetId ASC";
    }

    private static bool IsAfterQueryCursor(AssetItem item, AssetLibraryQuery query, AssetLibraryQueryCursor cursor)
    {
        var itemValue = GetQuerySortValue(item, query.EffectiveSortField);
        var itemNullBucket = itemValue is null ? 1 : 0;
        if (itemNullBucket != cursor.NullBucket) return itemNullBucket > cursor.NullBucket;
        if (itemNullBucket == 1)
            return string.Compare(item.AssetId.ToString("D"), cursor.AssetId.ToString("D"), StringComparison.Ordinal) > 0;

        var comparison = CompareQuerySortValues(query.EffectiveSortField, itemValue!, cursor.SortValue!);
        if (comparison != 0)
            return query.EffectiveSortDirection == AssetLibrarySortDirection.Ascending ? comparison > 0 : comparison < 0;
        return string.Compare(item.AssetId.ToString("D"), cursor.AssetId.ToString("D"), StringComparison.Ordinal) > 0;
    }

    private static string GetQuerySortExpression(AssetLibrarySortField field)
    {
        var version = AssetVisualFeatureContract.AnalysisVersion.Replace("'", "''", StringComparison.Ordinal);
        return field switch
        {
            AssetLibrarySortField.CaptureTime => "a.CaptureTime",
            AssetLibrarySortField.FileName => "a.DisplayName COLLATE NOCASE",
            AssetLibrarySortField.FileSize => "a.FileSize",
            AssetLibrarySortField.Rating => "a.Rating",
            AssetLibrarySortField.Color => $"(SELECT vf.DominantHue FROM AssetVisualFeatures vf WHERE vf.AssetId=a.AssetId AND vf.AnalysisVersion='{version}' AND vf.Outcome='Succeeded' AND vf.SourceContentHash IS NOT NULL AND a.ContentHash IS NOT NULL AND vf.SourceContentHash=a.ContentHash LIMIT 1)",
            AssetLibrarySortField.VisualAnalysis => $"CASE WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE vf.AssetId=a.AssetId AND vf.AnalysisVersion='{version}' AND vf.Outcome='Succeeded' AND vf.SourceContentHash IS NOT NULL AND a.ContentHash IS NOT NULL AND vf.SourceContentHash=a.ContentHash) THEN 3 WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE vf.AssetId=a.AssetId AND vf.AnalysisVersion='{version}' AND vf.Outcome='Failed' AND vf.SourceContentHash IS NOT NULL AND a.ContentHash IS NOT NULL AND vf.SourceContentHash=a.ContentHash) THEN 2 WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE vf.AssetId=a.AssetId) THEN 1 ELSE 0 END",
            _ => "a.AddedAt"
        };
    }

    private static string? GetQuerySortValue(AssetItem item, AssetLibrarySortField field) => field switch
    {
        AssetLibrarySortField.CaptureTime => item.CaptureTime?.ToString("O", CultureInfo.InvariantCulture),
        AssetLibrarySortField.FileName => item.DisplayName,
        AssetLibrarySortField.FileSize => item.FileSize.ToString(CultureInfo.InvariantCulture),
        AssetLibrarySortField.Rating => item.Rating.ToString(CultureInfo.InvariantCulture),
        AssetLibrarySortField.Color or AssetLibrarySortField.VisualAnalysis => null,
        _ => item.AddedAt.ToString("O", CultureInfo.InvariantCulture)
    };

    private static object ParseQueryCursorValue(AssetLibrarySortField field, string value) => field switch
    {
        AssetLibrarySortField.FileSize => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        AssetLibrarySortField.Rating => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        AssetLibrarySortField.Color => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
        AssetLibrarySortField.VisualAnalysis => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        _ => value
    };

    private static bool IsValidQueryCursorValue(AssetLibrarySortField field, int nullBucket, string? value)
    {
        if (nullBucket == 1) return (field is AssetLibrarySortField.CaptureTime or AssetLibrarySortField.Color) && value is null;
        if (value is null) return false;
        return field switch
        {
            AssetLibrarySortField.AddedAt or AssetLibrarySortField.CaptureTime => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
            AssetLibrarySortField.FileSize => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && size >= 0,
            AssetLibrarySortField.Rating => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating) && rating is >= 0 and <= 5,
            AssetLibrarySortField.Color => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hue) && double.IsFinite(hue) && hue is >= 0 and <= 360,
            AssetLibrarySortField.VisualAnalysis => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var state) && state is >= 0 and <= 3,
            AssetLibrarySortField.FileName => true,
            _ => false
        };
    }

    private static int CompareQuerySortValues(AssetLibrarySortField field, string left, string right) => field switch
    {
        AssetLibrarySortField.FileName => CompareSqliteNoCase(left, right),
        AssetLibrarySortField.FileSize => long.Parse(left, CultureInfo.InvariantCulture).CompareTo(long.Parse(right, CultureInfo.InvariantCulture)),
        AssetLibrarySortField.Rating => int.Parse(left, CultureInfo.InvariantCulture).CompareTo(int.Parse(right, CultureInfo.InvariantCulture)),
        _ => string.Compare(left, right, StringComparison.Ordinal)
    };

    private static int CompareSqliteNoCase(string left, string right)
    {
        var count = Math.Min(left.Length, right.Length);
        for (var index = 0; index < count; index++)
        {
            var leftCharacter = left[index] is >= 'A' and <= 'Z' ? (char)(left[index] + 32) : left[index];
            var rightCharacter = right[index] is >= 'A' and <= 'Z' ? (char)(right[index] + 32) : right[index];
            if (leftCharacter != rightCharacter) return leftCharacter.CompareTo(rightCharacter);
        }
        return left.Length.CompareTo(right.Length);
    }

    private static string ComputeQueryFingerprint(AssetLibraryQuery query)
    {
        static string[] NormalizeIds(IReadOnlyList<Guid>? values) => (values ?? []).Distinct().OrderBy(value => value).Select(value => value.ToString("D")).ToArray();
        var contract = new
        {
            SearchText = (query.SearchText ?? string.Empty).Trim(),
            query.FolderId,
            query.TagId,
            query.MinimumRating,
            query.MaximumRating,
            query.MediaType,
            query.Extension,
            query.UncategorizedOnly,
            query.UntaggedOnly,
            query.MissingOnly,
            query.FileNameRegex,
            query.SmartFolderId,
            FolderIds = NormalizeIds(query.FolderIds),
            TagIds = NormalizeIds(query.TagIds),
            AddedFrom = query.AddedFrom?.ToString("O", CultureInfo.InvariantCulture),
            AddedTo = query.AddedTo?.ToString("O", CultureInfo.InvariantCulture),
            CaptureFrom = query.CaptureFrom?.ToString("O", CultureInfo.InvariantCulture),
            CaptureTo = query.CaptureTo?.ToString("O", CultureInfo.InvariantCulture),
            ArchiveScope = query.EffectiveArchiveScope,
            SortField = query.EffectiveSortField,
            SortDirection = query.EffectiveSortDirection,
            query.SystemCollection
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(contract))));
    }

    public async Task<IReadOnlyList<AssetFolderTreeItem>> GetFolderTreeAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var folders = await ListFoldersAsync(includeArchived, cancellationToken).ConfigureAwait(false);
        var memberships = await ListFolderMembershipsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var directAssets = memberships.GroupBy(x => x.FolderId).ToDictionary(x => x.Key, x => x.Select(y => y.AssetId).ToHashSet());
        var children = folders.GroupBy(x => x.ParentFolderId ?? Guid.Empty).ToDictionary(x => x.Key, x => x.OrderBy(y => y.SortOrder).ThenBy(y => y.Name, StringComparer.OrdinalIgnoreCase).ToArray());

        (AssetFolderTreeItem Node, HashSet<Guid> Assets) Build(AssetFolder folder, string parentPath, int depth)
        {
            var path = string.IsNullOrWhiteSpace(parentPath) ? folder.Name : $"{parentPath} / {folder.Name}";
            var assetIds = directAssets.TryGetValue(folder.FolderId, out var own) ? new HashSet<Guid>(own) : [];
            var childNodes = new List<AssetFolderTreeItem>();
            if (children.TryGetValue(folder.FolderId, out var childFolders))
            {
                foreach (var child in childFolders)
                {
                    var built = Build(child, path, depth + 1);
                    childNodes.Add(built.Node);
                    assetIds.UnionWith(built.Assets);
                }
            }
            var directCount = own?.Count ?? 0;
            return (new(folder, path, depth, directCount, assetIds.Count, childNodes), assetIds);
        }

        return children.TryGetValue(Guid.Empty, out var roots)
            ? roots.Select(x => Build(x, string.Empty, 0).Node).ToArray()
            : [];
    }

    public async Task<AssetLibraryBatchResult> SetFolderArchivedAsync(Guid folderId, bool isArchived, CancellationToken cancellationToken = default)
    {
        var before = await ReadFolderByIdAsync(folderId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Folder not found.");
        if (before.IsSystem && isArchived) throw new InvalidOperationException("系统集合不能归档。");
        if (before.IsArchived == isArchived) return new(0, null, []);
        var after = before with { IsArchived = isArchived, UpdatedAt = DateTimeOffset.UtcNow };
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SaveFolderInTransactionAsync(connection, transaction, after, cancellationToken).ConfigureAwait(false);
        var token = CreateUndoToken(isArchived ? "Archive folder" : "Restore folder");
        await WriteUndoJournalAsync(connection, transaction, token, "folder-archive-state-v2", new FolderArchiveChange(before, after), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(1, token, []);
    }

    public Task<AssetLibraryBatchResult> RestoreFolderAsync(Guid folderId, CancellationToken cancellationToken = default) =>
        SetFolderArchivedAsync(folderId, isArchived: false, cancellationToken);

    public async Task<AssetLibraryBatchResult> RenameFolderAsync(Guid folderId, string name, CancellationToken cancellationToken = default)
    {
        var trimmed = RequireName(name, nameof(name));
        var previous = await ReadFolderByIdAsync(folderId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Folder not found.");
        if (string.Equals(previous.Name, trimmed, StringComparison.Ordinal)) return new(0, null, []);
        var next = previous with { Name = trimmed };
        var token = await MutateAndJournalAsync("Rename folder", "folder-restore", previous, (connection, transaction, ct) => ExecuteAsync(connection, transaction, "UPDATE AssetFolders SET Name=$name,UpdatedAt=$updated WHERE FolderId=$id;", ct, ("$name", next.Name), ("$updated", DateTimeOffset.UtcNow.ToString("O")), ("$id", folderId.ToString("D"))), ct => SaveFolderAsync(previous, ct), cancellationToken).ConfigureAwait(false);
        return new(1, token, []);
    }

    public async Task<AssetLibraryBatchResult> MoveFolderAsync(AssetFolderMoveRequest request, CancellationToken cancellationToken = default)
    {
        var folder = await ReadFolderByIdAsync(request.FolderId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Folder not found.");
        if (request.NewParentFolderId == request.FolderId) throw new InvalidOperationException("不能将文件夹移动到自身。");
        if (request.NewParentFolderId is not null)
        {
            var folders = await ListFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false);
            var parentById = folders.ToDictionary(x => x.FolderId, x => x.ParentFolderId);
            for (var cursor = request.NewParentFolderId; cursor is not null; cursor = parentById.GetValueOrDefault(cursor.Value))
                if (cursor == request.FolderId) throw new InvalidOperationException("不能形成循环父子关系。");
        }
        if (folder.ParentFolderId == request.NewParentFolderId && folder.SortOrder == request.SortOrder) return new(0, null, []);
        var next = folder with { ParentFolderId = request.NewParentFolderId, SortOrder = request.SortOrder };
        var token = await MutateAndJournalAsync("Move folder", "folder-restore", folder, (connection, transaction, ct) => ExecuteAsync(connection, transaction, "UPDATE AssetFolders SET ParentFolderId=$parent,SortOrder=$sort,UpdatedAt=$updated WHERE FolderId=$id;", ct, ("$parent", (object?)next.ParentFolderId?.ToString("D") ?? DBNull.Value), ("$sort", next.SortOrder), ("$updated", DateTimeOffset.UtcNow.ToString("O")), ("$id", next.FolderId.ToString("D"))), ct => SaveFolderAsync(folder, ct), cancellationToken).ConfigureAwait(false);
        return new(1, token, []);
    }

    public async Task<AssetLibraryBatchResult> ReorderFoldersAsync(Guid? parentFolderId, IEnumerable<Guid> orderedFolderIds, CancellationToken cancellationToken = default)
    {
        var ids = orderedFolderIds.Distinct().ToArray();
        var folders = (await ListFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false)).Where(x => x.ParentFolderId == parentFolderId).ToArray();
        var valid = folders.Select(x => x.FolderId).ToHashSet();
        if (ids.Any(id => !valid.Contains(id))) throw new InvalidOperationException("排序列表包含不属于目标层级的文件夹。");
        var previous = folders.Where(x => ids.Contains(x.FolderId)).ToArray();
        var token = await MutateAndJournalAsync("Reorder folders", "folders-restore", previous, async (connection, transaction, ct) =>
        {
            for (var index = 0; index < ids.Length; index++) await ExecuteAsync(connection, transaction, "UPDATE AssetFolders SET SortOrder=$sort,UpdatedAt=$updated WHERE FolderId=$id;", ct, ("$sort", index), ("$updated", DateTimeOffset.UtcNow.ToString("O")), ("$id", ids[index].ToString("D"))).ConfigureAwait(false);
        }, async ct => { foreach (var item in previous) await SaveFolderAsync(item, ct).ConfigureAwait(false); }, cancellationToken).ConfigureAwait(false);
        return new(ids.Length, token, []);
    }

    public async Task<AssetFolderBatchCreateResult> BatchCreateFoldersAsync(string paths, Guid? parentFolderId = null, CancellationToken cancellationToken = default)
    {
        var created = new List<AssetFolder>();
        var existing = new List<string>();
        var invalid = new List<string>();
        var lines = (paths ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segments = line.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0 || segments.Any(x => x is "." or ".." || x.Any(char.IsControl))) { invalid.Add(line); continue; }
            var cursor = parentFolderId;
            var wasCreated = false;
            foreach (var segment in segments)
            {
                var siblings = (await ListFoldersAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Where(x => x.ParentFolderId == cursor).ToArray();
                var folder = siblings.FirstOrDefault(x => string.Equals(x.Name, segment, StringComparison.OrdinalIgnoreCase));
                if (folder is null)
                {
                    folder = await SaveFolderAsync(new(Guid.NewGuid(), cursor, segment, SortOrder: siblings.Length), cancellationToken).ConfigureAwait(false);
                    created.Add(folder); wasCreated = true;
                }
                cursor = folder.FolderId;
            }
            if (!wasCreated) existing.Add(line);
        }
        return new(created, existing, invalid);
    }

    public async Task<IReadOnlyList<AssetFolder>> CopyFolderStructureAsync(Guid sourceFolderId, Guid? targetParentFolderId, string? rootName = null, CancellationToken cancellationToken = default)
    {
        var folders = await ListFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false);
        var source = folders.FirstOrDefault(x => x.FolderId == sourceFolderId) ?? throw new KeyNotFoundException("Folder not found.");
        var children = folders.GroupBy(x => x.ParentFolderId ?? Guid.Empty).ToDictionary(x => x.Key, x => x.OrderBy(y => y.SortOrder).ToArray());
        var result = new List<AssetFolder>();
        async Task CopyAsync(AssetFolder item, Guid? parent, bool root)
        {
            var copy = item with
            {
                FolderId = Guid.NewGuid(),
                ParentFolderId = parent,
                Name = root && !string.IsNullOrWhiteSpace(rootName) ? rootName.Trim() : root ? item.Name + " 副本" : item.Name,
                CreatedAt = null,
                UpdatedAt = null,
                IsSystem = false
            };
            copy = await SaveFolderAsync(copy, cancellationToken).ConfigureAwait(false);
            result.Add(copy);
            if (children.TryGetValue(item.FolderId, out var nested))
                foreach (var child in nested) await CopyAsync(child, copy.FolderId, false).ConfigureAwait(false);
        }
        await CopyAsync(source, targetParentFolderId, true).ConfigureAwait(false);
        return result;
    }

    public async Task<IReadOnlyList<AssetTag>> BatchCreateTagsAsync(string values, Guid? tagGroupId = null, CancellationToken cancellationToken = default)
    {
        var names = (values ?? string.Empty).Split([',', '，', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var existing = await ListTagsAsync(tagGroupId, cancellationToken: cancellationToken).ConfigureAwait(false);
        var result = new List<AssetTag>();
        foreach (var name in names)
        {
            var found = existing.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (found is not null) { result.Add(found); continue; }
            var created = await SaveTagAsync(new(Guid.NewGuid(), name, tagGroupId, result.Count), cancellationToken).ConfigureAwait(false);
            result.Add(created);
        }
        return result;
    }

    public async Task<AssetLibraryBatchResult> RenameTagAsync(Guid tagId, string name, CancellationToken cancellationToken = default)
    {
        var previous = await ReadTagByIdAsync(tagId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Tag not found.");
        var trimmed = RequireName(name, nameof(name));
        if (string.Equals(previous.Name, trimmed, StringComparison.Ordinal)) return new(0, null, []);
        var next = previous with { Name = trimmed };
        var token = await MutateAndJournalAsync("Rename tag", "tag-restore", previous, (connection, transaction, ct) => ExecuteAsync(connection, transaction, "UPDATE AssetTags SET Name=$name WHERE TagId=$id;", ct, ("$name", next.Name), ("$id", tagId.ToString("D"))), ct => SaveTagAsync(previous, ct), cancellationToken).ConfigureAwait(false);
        return new(1, token, []);
    }

    public async Task<AssetLibraryBatchResult> MoveTagsToGroupAsync(IEnumerable<Guid> tagIds, Guid? tagGroupId, CancellationToken cancellationToken = default)
    {
        var ids = tagIds.Distinct().ToArray();
        var previous = new List<AssetTag>();
        foreach (var id in ids)
        {
            var tag = await ReadTagByIdAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Tag not found.");
            previous.Add(tag);
        }
        var token = await MutateAndJournalAsync("Move tags to group", "tags-restore", previous.ToArray(), async (connection, transaction, ct) =>
        {
            foreach (var tag in previous) await ExecuteAsync(connection, transaction, "UPDATE AssetTags SET TagGroupId=$group WHERE TagId=$id;", ct, ("$group", (object?)tagGroupId?.ToString("D") ?? DBNull.Value), ("$id", tag.TagId.ToString("D"))).ConfigureAwait(false);
        }, async ct => { foreach (var tag in previous) await SaveTagAsync(tag, ct).ConfigureAwait(false); }, cancellationToken).ConfigureAwait(false);
        return new(ids.Length, token, []);
    }

    public async Task<IReadOnlyList<AssetTag>> SearchTagsAsync(string searchText, int limit = 30, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.TagId,t.Name,t.TagGroupId,t.SortOrder,COUNT(m.AssetId),t.CreatedAt,t.IsArchived,MAX(m.AddedAt)
            FROM AssetTags t LEFT JOIN AssetTagMemberships m ON m.TagId=t.TagId
            WHERE t.IsArchived=0 AND ($search='' OR t.Name LIKE $prefix OR t.Name LIKE $contains)
            GROUP BY t.TagId
            ORDER BY CASE WHEN t.Name LIKE $prefix THEN 0 ELSE 1 END, MAX(m.AddedAt) DESC, COUNT(m.AssetId) DESC, t.Name COLLATE NOCASE
            LIMIT $limit;
            """;
        var search = searchText?.Trim() ?? string.Empty;
        command.Parameters.AddWithValue("$search", search); command.Parameters.AddWithValue("$prefix", search + "%"); command.Parameters.AddWithValue("$contains", "%" + search + "%"); command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));
        var result = new List<AssetTag>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)), reader.GetInt32(3), reader.GetInt32(4), DateTimeOffset.Parse(reader.GetString(5)), reader.GetInt32(6) != 0));
        return result;
    }

    public async Task<IReadOnlyList<AssetTagUsageSummary>> GetTagUsageSummaryAsync(IEnumerable<Guid> assetIds, CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var tags = await ListTagsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var memberships = await ListTagMembershipsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var counts = memberships.Where(x => ids.Contains(x.AssetId)).GroupBy(x => x.TagId).ToDictionary(x => x.Key, x => x.Select(y => y.AssetId).Distinct().Count());
        return tags.Where(x => counts.ContainsKey(x.TagId)).Select(x => new AssetTagUsageSummary(x, ids.Length, counts[x.TagId])).OrderByDescending(x => x.IsCommon).ThenByDescending(x => x.MembershipCount).ThenBy(x => x.Tag.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<AssetRelinkResult> RelinkMissingAssetsAsync(AssetRelinkRequest request, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(request.NewRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var candidates = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToArray();
        var byName = candidates.GroupBy(x => Path.GetFileName(x) ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? byHash = null;
        if (request.MatchMode == AssetRelinkMatchMode.ContentHash)
        {
            byHash = new(StringComparer.OrdinalIgnoreCase);
            foreach (var path in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byHash.TryAdd(await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false), path);
            }
        }
        var missing = new List<AssetItem>();
        string? cursor = null;
        do
        {
            var page = await QueryAsync(new(MissingOnly: true, PageSize: 500, Cursor: cursor), cancellationToken).ConfigureAwait(false);
            missing.AddRange(page.Items); cursor = page.NextCursor;
        } while (cursor is not null);
        var changed = 0; var warnings = new List<string>();
        foreach (var asset in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? target = null;
            if (request.MatchMode == AssetRelinkMatchMode.RelativePath && !string.IsNullOrWhiteSpace(request.PreviousRoot) && Path.GetFullPath(asset.SourcePath).StartsWith(Path.GetFullPath(request.PreviousRoot), StringComparison.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(Path.GetFullPath(request.PreviousRoot), asset.SourcePath);
                var candidate = Path.Combine(root, relative); if (File.Exists(candidate)) target = candidate;
            }
            else if (request.MatchMode == AssetRelinkMatchMode.ContentHash && !string.IsNullOrWhiteSpace(asset.ContentHash)) byHash!.TryGetValue(asset.ContentHash, out target);
            else if (byName.TryGetValue(asset.DisplayName, out var matches) && matches.Length == 1) target = matches[0];
            if (target is null) { warnings.Add($"未找到：{asset.DisplayName}"); continue; }
            try { await UpdateRelinkedAssetAsync(asset.AssetId, target, cancellationToken).ConfigureAwait(false); changed++; }
            catch (SqliteException) { warnings.Add($"目标已存在于素材库：{Path.GetFileName(target)}"); }
        }
        return new(changed, missing.Count - changed, warnings);
    }

    public async Task<IReadOnlyList<AssetUndoJournalEntry>> ListUndoJournalAsync(int limit = UndoJournalLimit, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OperationId,Description,OperationKind,CreatedAt,UndoneAt FROM AssetLibraryUndoJournal ORDER BY CreatedAt DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, UndoJournalLimit));
        var result = new List<AssetUndoJournalEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var token = new AssetLibraryUndoToken(Guid.Parse(reader.GetString(0)), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(3)));
            result.Add(new(token, reader.GetString(2), !reader.IsDBNull(4), reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4))));
        }
        return result;
    }

    private async Task<AssetLibraryUndoToken> RegisterPersistentUndoAsync<T>(string description, string kind, T payload, Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        var token = CreateUndoToken(description);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await WriteUndoJournalAsync(connection, transaction, token, kind, payload, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        TrackUndo(token, operation);
        return token;
    }

    private static AssetLibraryUndoToken CreateUndoToken(string description) => new(Guid.NewGuid(), description, DateTimeOffset.UtcNow);

    private void TrackUndo(AssetLibraryUndoToken token, Func<CancellationToken, Task> operation) => _undo[token.OperationId] = operation;

    private static async Task WriteUndoJournalAsync<T>(SqliteConnection connection, SqliteTransaction transaction, AssetLibraryUndoToken token, string kind, T payload, CancellationToken cancellationToken, int journalVersion = 1)
    {
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO AssetLibraryUndoJournal(OperationId,Description,OperationKind,PayloadJson,CreatedAt,UndoneAt,JournalVersion) VALUES($id,$description,$kind,$payload,$created,NULL,$version);";
            insert.Parameters.AddWithValue("$id", token.OperationId.ToString("D")); insert.Parameters.AddWithValue("$description", token.Description); insert.Parameters.AddWithValue("$kind", kind); insert.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(payload)); insert.Parameters.AddWithValue("$created", token.CreatedAt.ToString("O")); insert.Parameters.AddWithValue("$version", journalVersion);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using var trim = connection.CreateCommand();
        trim.Transaction = transaction;
        trim.CommandText = "DELETE FROM AssetLibraryUndoJournal WHERE OperationId NOT IN (SELECT OperationId FROM AssetLibraryUndoJournal ORDER BY CreatedAt DESC LIMIT $limit);";
        trim.Parameters.AddWithValue("$limit", UndoJournalLimit);
        await trim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<AssetLibraryUndoToken> MutateAndJournalAsync<T>(string description, string kind, T payload, Func<SqliteConnection, SqliteTransaction, CancellationToken, Task> mutation, Func<CancellationToken, Task> inProcessUndo, CancellationToken cancellationToken)
    {
        var token = CreateUndoToken(description);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await mutation(connection, transaction, cancellationToken).ConfigureAwait(false);
        await WriteUndoJournalAsync(connection, transaction, token, kind, payload, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        TrackUndo(token, inProcessUndo);
        return token;
    }

    private async Task<bool> ApplyPersistedUndoAtomicallyAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string kind;
        string json;
        int journalVersion;
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT OperationKind,PayloadJson,JournalVersion FROM AssetLibraryUndoJournal WHERE OperationId=$id AND UndoneAt IS NULL;";
            read.Parameters.AddWithValue("$id", operationId.ToString("D"));
            await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); return false; }
            kind = reader.GetString(0); json = reader.GetString(1); journalVersion = reader.GetInt32(2);
        }

        var handled = await DispatchPersistentUndoInTransactionAsync(connection, transaction, new(kind, json, journalVersion), cancellationToken).ConfigureAwait(false);
        if (!handled) { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); return false; }
        await using var mark = connection.CreateCommand(); mark.Transaction = transaction; mark.CommandText = "UPDATE AssetLibraryUndoJournal SET UndoneAt=$at WHERE OperationId=$id AND UndoneAt IS NULL;"; mark.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); mark.Parameters.AddWithValue("$id", operationId.ToString("D"));
        if (await mark.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1) { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); return false; }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ApplyPersistedRedoAtomicallyAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        PersistedUndo operation;
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT OperationKind,PayloadJson,JournalVersion FROM AssetLibraryUndoJournal WHERE OperationId=$id AND UndoneAt IS NOT NULL;";
            read.Parameters.AddWithValue("$id", operationId.ToString("D"));
            await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); return false; }
            operation = new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2));
        }
        if (operation.JournalVersion < 2 || !await DispatchPersistentRedoInTransactionAsync(connection, transaction, operation, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }
        await using var mark = connection.CreateCommand();
        mark.Transaction = transaction;
        mark.CommandText = "UPDATE AssetLibraryUndoJournal SET UndoneAt=NULL WHERE OperationId=$id AND UndoneAt IS NOT NULL;";
        mark.Parameters.AddWithValue("$id", operationId.ToString("D"));
        if (await mark.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1) { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); return false; }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task<bool> DispatchPersistentUndoInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, PersistedUndo undo, CancellationToken cancellationToken)
    {
        switch (undo.Kind)
        {
            case "asset-metadata-v2":
                if (undo.JournalVersion < 2) return false;
                await ApplyAssetMetadataStatesInTransactionAsync(connection, transaction, Deserialize<AssetMetadataChange>(undo.PayloadJson).Before, cancellationToken).ConfigureAwait(false);
                return true;
            case "asset-archive-state-v2":
                if (undo.JournalVersion < 2) return false;
                await ApplyAssetFlagStatesInTransactionAsync(connection, transaction, "IsArchived", Deserialize<AssetFlagChange>(undo.PayloadJson).Before, cancellationToken).ConfigureAwait(false);
                return true;
            case "asset-missing-state-v2":
                if (undo.JournalVersion < 2) return false;
                await ApplyAssetFlagStatesInTransactionAsync(connection, transaction, "IsMissing", Deserialize<AssetFlagChange>(undo.PayloadJson).Before, cancellationToken).ConfigureAwait(false);
                return true;
            case "folder-archive-state-v2":
                if (undo.JournalVersion < 2) return false;
                await SaveFolderInTransactionAsync(connection, transaction, Deserialize<FolderArchiveChange>(undo.PayloadJson).Before, cancellationToken).ConfigureAwait(false);
                return true;
            case "folder-membership-v2":
            {
                if (undo.JournalVersion < 2) return false;
                var payload = Deserialize<FolderMembershipChange>(undo.PayloadJson);
                await ApplyFolderMembershipStateInTransactionAsync(connection, transaction, payload, payload.BeforePresent, cancellationToken).ConfigureAwait(false);
                return true;
            }
            case "tag-membership-v2":
            {
                if (undo.JournalVersion < 2) return false;
                var payload = Deserialize<TagMembershipChange>(undo.PayloadJson);
                await ApplyTagMembershipStateInTransactionAsync(connection, transaction, payload.Memberships, payload.BeforePresent, cancellationToken).ConfigureAwait(false);
                return true;
            }
            case "asset-metadata":
            {
                var payload = Deserialize<AssetMetadataUndo>(undo.PayloadJson);
                await ExecuteAsync(connection, transaction, "UPDATE AssetItems SET Rating=$rating,Comment=$comment WHERE AssetId=$id;", cancellationToken, ("$rating", payload.Rating), ("$comment", payload.Comment), ("$id", payload.AssetId.ToString("D"))).ConfigureAwait(false);
                return true;
            }
            case "asset-metadata-batch":
            {
                var payload = Deserialize<AssetMetadataBatchUndo>(undo.PayloadJson);
                foreach (var item in payload.Items) await ExecuteAsync(connection, transaction, "UPDATE AssetItems SET Rating=$rating,Comment=$comment WHERE AssetId=$id;", cancellationToken, ("$rating", item.Rating), ("$comment", item.Comment), ("$id", item.AssetId.ToString("D"))).ConfigureAwait(false);
                return true;
            }
            case "folder-membership":
            {
                var payload = Deserialize<FolderMembershipUndo>(undo.PayloadJson);
                foreach (var row in payload.Memberships)
                {
                    if (payload.Restore) await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO AssetFolderMemberships(AssetId,FolderId,AddedAt) VALUES($asset,$folder,$at);", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$folder", row.FolderId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
                    else await ExecuteAsync(connection, transaction, "DELETE FROM AssetFolderMemberships WHERE AssetId=$asset AND FolderId=$folder;", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$folder", row.FolderId.ToString("D"))).ConfigureAwait(false);
                }
                if (!payload.Restore)
                    foreach (var row in payload.AddedAutoTags) await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE AssetId=$asset AND TagId=$tag;", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$tag", row.TagId.ToString("D"))).ConfigureAwait(false);
                return true;
            }
            case "tag-membership":
            {
                var payload = Deserialize<TagMembershipUndo>(undo.PayloadJson);
                foreach (var row in payload.Memberships)
                {
                    if (payload.Restore) await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$tag", row.TagId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
                    else await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE AssetId=$asset AND TagId=$tag;", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$tag", row.TagId.ToString("D"))).ConfigureAwait(false);
                }
                return true;
            }
            case "tag-merge":
            {
                var payload = Deserialize<TagMergeUndo>(undo.PayloadJson);
                await SaveTagInTransactionAsync(connection, transaction, payload.Source with { IsArchived = false }, cancellationToken).ConfigureAwait(false);
                var existingTarget = payload.TargetMemberIds.ToHashSet();
                foreach (var row in payload.SourceMembers)
                {
                    if (!existingTarget.Contains(row.AssetId)) await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE AssetId=$asset AND TagId=$tag;", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$tag", payload.Target.TagId.ToString("D"))).ConfigureAwait(false);
                    await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$tag", payload.Source.TagId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
                }
                return true;
            }
            case "folder-restore": await SaveFolderInTransactionAsync(connection, transaction, Deserialize<AssetFolder>(undo.PayloadJson), cancellationToken).ConfigureAwait(false); return true;
            case "folders-restore": foreach (var folder in Deserialize<AssetFolder[]>(undo.PayloadJson)) await SaveFolderInTransactionAsync(connection, transaction, folder, cancellationToken).ConfigureAwait(false); return true;
            case "tag-restore": await SaveTagInTransactionAsync(connection, transaction, Deserialize<AssetTag>(undo.PayloadJson), cancellationToken).ConfigureAwait(false); return true;
            case "tags-restore": foreach (var tag in Deserialize<AssetTag[]>(undo.PayloadJson)) await SaveTagInTransactionAsync(connection, transaction, tag, cancellationToken).ConfigureAwait(false); return true;
            default: return false;
        }
    }

    private static async Task<bool> DispatchPersistentRedoInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, PersistedUndo operation, CancellationToken cancellationToken)
    {
        switch (operation.Kind)
        {
            case "asset-metadata-v2":
                await ApplyAssetMetadataStatesInTransactionAsync(connection, transaction, Deserialize<AssetMetadataChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false);
                return true;
            case "asset-archive-state-v2":
                await ApplyAssetFlagStatesInTransactionAsync(connection, transaction, "IsArchived", Deserialize<AssetFlagChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false);
                return true;
            case "asset-missing-state-v2":
                await ApplyAssetFlagStatesInTransactionAsync(connection, transaction, "IsMissing", Deserialize<AssetFlagChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false);
                return true;
            case "folder-archive-state-v2":
                await SaveFolderInTransactionAsync(connection, transaction, Deserialize<FolderArchiveChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false);
                return true;
            case "folder-membership-v2":
            {
                var payload = Deserialize<FolderMembershipChange>(operation.PayloadJson);
                await ApplyFolderMembershipStateInTransactionAsync(connection, transaction, payload, payload.AfterPresent, cancellationToken).ConfigureAwait(false);
                return true;
            }
            case "tag-membership-v2":
            {
                var payload = Deserialize<TagMembershipChange>(operation.PayloadJson);
                await ApplyTagMembershipStateInTransactionAsync(connection, transaction, payload.Memberships, payload.AfterPresent, cancellationToken).ConfigureAwait(false);
                return true;
            }
            default:
                return false;
        }
    }

    private static async Task ApplyAssetMetadataStatesInTransactionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IEnumerable<AssetMetadataUndo> states,
        CancellationToken cancellationToken)
    {
        foreach (var state in states)
            await ExecuteAsync(connection, transaction, "UPDATE AssetItems SET Rating=$rating,Comment=$comment WHERE AssetId=$id;", cancellationToken,
                ("$rating", state.Rating), ("$comment", state.Comment), ("$id", state.AssetId.ToString("D"))).ConfigureAwait(false);
    }

    private static async Task ApplyFolderMembershipStateInTransactionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FolderMembershipChange change,
        bool present,
        CancellationToken cancellationToken)
    {
        foreach (var row in change.Memberships)
        {
            if (present)
                await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO AssetFolderMemberships(AssetId,FolderId,AddedAt) VALUES($asset,$folder,$at);", cancellationToken,
                    ("$asset", row.AssetId.ToString("D")), ("$folder", row.FolderId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
            else
                await ExecuteAsync(connection, transaction, "DELETE FROM AssetFolderMemberships WHERE AssetId=$asset AND FolderId=$folder;", cancellationToken,
                    ("$asset", row.AssetId.ToString("D")), ("$folder", row.FolderId.ToString("D"))).ConfigureAwait(false);
        }

        await ApplyTagMembershipStateInTransactionAsync(connection, transaction, change.IntroducedAutoTags, present, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyTagMembershipStateInTransactionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IEnumerable<AssetTagMembership> memberships,
        bool present,
        CancellationToken cancellationToken)
    {
        foreach (var row in memberships)
        {
            if (present)
                await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);", cancellationToken,
                    ("$asset", row.AssetId.ToString("D")), ("$tag", row.TagId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
            else
                await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE AssetId=$asset AND TagId=$tag;", cancellationToken,
                    ("$asset", row.AssetId.ToString("D")), ("$tag", row.TagId.ToString("D"))).ConfigureAwait(false);
        }
    }

    private static async Task ApplyAssetFlagStatesInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, string column, IEnumerable<AssetFlagState> states, CancellationToken cancellationToken)
    {
        foreach (var state in states)
            await ExecuteAsync(connection, transaction, $"UPDATE AssetItems SET {column}=$value WHERE AssetId=$id;", cancellationToken,
                ("$value", state.Value ? 1 : 0), ("$id", state.AssetId.ToString("D"))).ConfigureAwait(false);
    }

    private static async Task SaveFolderInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, AssetFolder folder, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, "UPDATE AssetFolders SET ParentFolderId=$parent,Name=$name,Description=$description,Icon=$icon,Color=$color,SortOrder=$sort,UpdatedAt=$updated,IsArchived=$archived,IsSystem=$system WHERE FolderId=$id;", cancellationToken,
            ("$parent", (object?)folder.ParentFolderId?.ToString("D") ?? DBNull.Value), ("$name", folder.Name), ("$description", folder.Description), ("$icon", (object?)folder.Icon ?? DBNull.Value), ("$color", (object?)folder.Color ?? DBNull.Value), ("$sort", folder.SortOrder), ("$updated", folder.EffectiveUpdatedAt.ToString("O")), ("$archived", folder.IsArchived ? 1 : 0), ("$system", folder.IsSystem ? 1 : 0), ("$id", folder.FolderId.ToString("D"))).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM AssetFolderAutoTags WHERE FolderId=$id;", cancellationToken, ("$id", folder.FolderId.ToString("D"))).ConfigureAwait(false);
        foreach (var tagId in folder.AutoTagIds ?? []) await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO AssetFolderAutoTags(FolderId,TagId) VALUES($folder,$tag);", cancellationToken, ("$folder", folder.FolderId.ToString("D")), ("$tag", tagId.ToString("D"))).ConfigureAwait(false);
    }

    private static Task SaveTagInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, AssetTag tag, CancellationToken cancellationToken)
        => ExecuteAsync(connection, transaction, "UPDATE AssetTags SET Name=$name,TagGroupId=$group,SortOrder=$sort,IsArchived=$archived WHERE TagId=$id;", cancellationToken, ("$name", tag.Name), ("$group", (object?)tag.TagGroupId?.ToString("D") ?? DBNull.Value), ("$sort", tag.SortOrder), ("$archived", tag.IsArchived ? 1 : 0), ("$id", tag.TagId.ToString("D")));

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<PersistedUndo?> ReadPersistedUndoAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OperationKind,PayloadJson,UndoneAt,JournalVersion FROM AssetLibraryUndoJournal WHERE OperationId=$id;";
        command.Parameters.AddWithValue("$id", operationId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || !reader.IsDBNull(2)) return null;
        return new(reader.GetString(0), reader.GetString(1), reader.GetInt32(3));
    }

    private async Task MarkUndoCompletedAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "UPDATE AssetLibraryUndoJournal SET UndoneAt=$at WHERE OperationId=$id AND UndoneAt IS NULL;"; command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O")); command.Parameters.AddWithValue("$id", operationId.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> DispatchPersistentUndoAsync(PersistedUndo undo, CancellationToken cancellationToken)
    {
        switch (undo.Kind)
        {
            case "asset-metadata":
                var metadata = Deserialize<AssetMetadataUndo>(undo.PayloadJson); await UpdateAssetMetadataInternalAsync(metadata.AssetId, metadata.Rating, metadata.Comment, cancellationToken).ConfigureAwait(false); return true;
            case "folder-membership":
                var folders = Deserialize<FolderMembershipUndo>(undo.PayloadJson); await ApplyFolderMembershipUndoAsync(folders, cancellationToken).ConfigureAwait(false); return true;
            case "tag-membership":
                var tags = Deserialize<TagMembershipUndo>(undo.PayloadJson); await ApplyTagMembershipUndoAsync(tags, cancellationToken).ConfigureAwait(false); return true;
            case "tag-merge":
                var merge = Deserialize<TagMergeUndo>(undo.PayloadJson); await RestoreMergedTagsAsync(merge.Source, merge.Target, merge.SourceMembers, merge.TargetMemberIds.ToHashSet(), cancellationToken).ConfigureAwait(false); return true;
            case "folder-restore": await SaveFolderAsync(Deserialize<AssetFolder>(undo.PayloadJson), cancellationToken).ConfigureAwait(false); return true;
            case "folders-restore": foreach (var folder in Deserialize<AssetFolder[]>(undo.PayloadJson)) await SaveFolderAsync(folder, cancellationToken).ConfigureAwait(false); return true;
            case "tag-restore": await SaveTagAsync(Deserialize<AssetTag>(undo.PayloadJson), cancellationToken).ConfigureAwait(false); return true;
            case "tags-restore": foreach (var tag in Deserialize<AssetTag[]>(undo.PayloadJson)) await SaveTagAsync(tag, cancellationToken).ConfigureAwait(false); return true;
            default: return false;
        }
    }

    private async Task ApplyFolderMembershipUndoAsync(FolderMembershipUndo payload, CancellationToken cancellationToken)
    {
        if (payload.Restore) await RestoreFolderMembershipsAsync(payload.Memberships, cancellationToken).ConfigureAwait(false);
        else
        {
            foreach (var group in payload.Memberships.GroupBy(x => x.FolderId)) await ChangeFolderMembershipInternalAsync(group.Select(x => x.AssetId), group.Key, false, cancellationToken).ConfigureAwait(false);
            foreach (var group in payload.AddedAutoTags.GroupBy(x => x.TagId)) await ChangeTagMembershipInternalAsync(group.Select(x => x.AssetId).ToArray(), [group.Key], false, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ApplyTagMembershipUndoAsync(TagMembershipUndo payload, CancellationToken cancellationToken)
    {
        if (payload.Restore) await RestoreTagMembershipsAsync(payload.Memberships, cancellationToken).ConfigureAwait(false);
        else foreach (var group in payload.Memberships.GroupBy(x => x.TagId)) await ChangeTagMembershipInternalAsync(group.Select(x => x.AssetId).ToArray(), [group.Key], false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AssetFolder?> ReadFolderByIdAsync(Guid folderId, CancellationToken cancellationToken)
        => (await ListFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false)).FirstOrDefault(x => x.FolderId == folderId);

    private async Task UpdateRelinkedAssetAsync(Guid assetId, string targetPath, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(targetPath); var info = new FileInfo(full);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE AssetItems SET SourcePath=$source,NormalizedSourcePath=$normalized,DisplayName=$name,Extension=$extension,MediaType=$media,FileSize=$size,ModifiedAt=$modified,IsMissing=0 WHERE AssetId=$id;";
        var extension = NormalizeExtension(info.Extension);
        command.Parameters.AddWithValue("$source", full); command.Parameters.AddWithValue("$normalized", NormalizePath(full)); command.Parameters.AddWithValue("$name", info.Name); command.Parameters.AddWithValue("$extension", extension); command.Parameters.AddWithValue("$media", ClassifyMediaType(extension)); command.Parameters.AddWithValue("$size", info.Length); command.Parameters.AddWithValue("$modified", new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero).ToString("O")); command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string RequireName(string value, string parameterName)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) throw new ArgumentException("Name is required.", parameterName);
        return result;
    }

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json) ?? throw new InvalidDataException("Undo journal payload is invalid.");

    private sealed record AssetLibraryQueryCursor(int Version, string PlanFingerprint, int NullBucket, string? SortValue, Guid AssetId);
    private sealed record PersistedUndo(string Kind, string PayloadJson, int JournalVersion = 1);
    private sealed record AssetMetadataUndo(Guid AssetId, int Rating, string Comment);
    private sealed record AssetMetadataBatchUndo(AssetMetadataUndo[] Items);
    private sealed record AssetMetadataChange(AssetMetadataUndo[] Before, AssetMetadataUndo[] After);
    private sealed record AssetFlagState(Guid AssetId, bool Value);
    private sealed record AssetFlagChange(AssetFlagState[] Before, AssetFlagState[] After);
    private sealed record FolderArchiveChange(AssetFolder Before, AssetFolder After);
    private sealed record FolderMembershipUndo(bool Restore, AssetFolderMembership[] Memberships, AssetTagMembership[] AddedAutoTags);
    private sealed record TagMembershipUndo(bool Restore, AssetTagMembership[] Memberships);
    private sealed record FolderMembershipChange(bool BeforePresent, bool AfterPresent, AssetFolderMembership[] Memberships, AssetTagMembership[] IntroducedAutoTags);
    private sealed record TagMembershipChange(bool BeforePresent, bool AfterPresent, AssetTagMembership[] Memberships);
    private sealed record TagMergeUndo(AssetTag Source, AssetTag Target, AssetTagMembership[] SourceMembers, Guid[] TargetMemberIds);
}
