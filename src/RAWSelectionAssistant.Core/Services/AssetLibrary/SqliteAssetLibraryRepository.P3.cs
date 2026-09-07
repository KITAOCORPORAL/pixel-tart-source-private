using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

public sealed partial class SqliteAssetLibraryRepository
{
    public async Task<AssetQueryExecutionPlan> ExplainQueryPlanAsync(
        AssetLibraryQuery query,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (query.SmartFolderId is not null)
        {
            var saved = await GetSmartFolderQueryDocumentAsync(query.SmartFolderId.Value, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("智能文件夹缺少 P3 查询文档。");
            var savedNormalization = AssetQueryDocumentCodec.Normalize(saved.Document);
            if (!savedNormalization.IsValid || savedNormalization.Document is null)
                throw new InvalidDataException(savedNormalization.ErrorMessage);
            AssetQueryDocument? transient = null;
            if (query.Document is not null)
            {
                var transientNormalization = AssetQueryDocumentCodec.Normalize(query.Document);
                if (!transientNormalization.IsValid || transientNormalization.Document is null)
                    throw new InvalidDataException(transientNormalization.ErrorMessage);
                transient = transientNormalization.Document;
            }
            query = ComposeCurrentSmartFolderQuery(query, savedNormalization.Document, transient);
        }
        if (query.Document is null) throw new InvalidOperationException("P3 EXPLAIN requires a canonical query document.");
        var normalized = AssetQueryDocumentCodec.Normalize(query.Document);
        if (!normalized.IsValid || normalized.Document is null)
            throw new InvalidDataException(normalized.ErrorMessage);
        query = MergeLegacyFileNameRegexIntoP3Document(query, normalized.Document);
        normalized = AssetQueryDocumentCodec.Normalize(query.Document!);
        if (!normalized.IsValid || normalized.Document is null)
            throw new InvalidDataException(normalized.ErrorMessage);
        query = ApplyP3DocumentBaseQuery(query, normalized.Document);

        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var referenceErrors = await ValidateQueryReferencesAsync(connection, normalized.Document, cancellationToken).ConfigureAwait(false);
        if (referenceErrors.Count != 0)
            throw new InvalidDataException(string.Join("；", referenceErrors.Select(error => $"{error.Path}: {error.Message}")));
        var runtimeError = ValidateRuntimeRules(normalized.Document.RootGroup);
        if (runtimeError is not null) throw new InvalidDataException(runtimeError);

        await using var page = connection.CreateCommand();
        var where = BuildIndexedWhere(query, page) + " AND " + CompileP3Node(normalized.Document.RootGroup, page);
        page.CommandText = SelectAssetSql + " WHERE " + where + " ORDER BY " + BuildQueryOrderBy(query) + " LIMIT $limit;";
        page.Parameters.AddWithValue("$limit", query.EffectivePageSize + 1);

        var parameters = page.Parameters.Cast<SqliteParameter>().Select(parameter =>
        {
            var canonical = parameter.Value is null or DBNull
                ? "<null>"
                : Convert.ToString(parameter.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            return new AssetQueryPlanParameter(
                parameter.ParameterName,
                parameter.Value?.GetType().FullName ?? "null",
                canonical.Length,
                hash);
        }).ToArray();

        await using var explain = connection.CreateCommand();
        explain.CommandText = "EXPLAIN QUERY PLAN " + page.CommandText;
        foreach (SqliteParameter parameter in page.Parameters)
            explain.Parameters.AddWithValue(parameter.ParameterName, parameter.Value ?? DBNull.Value);
        var rows = new List<string>();
        await using var reader = await explain.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) rows.Add(reader.GetString(3));
        if (rows.Count == 0) throw new InvalidDataException("SQLite EXPLAIN QUERY PLAN 没有返回任何执行步骤。");
        return new(page.CommandText, parameters, rows);
    }

    private static AssetLibraryQuery ComposeCurrentSmartFolderQuery(
        AssetLibraryQuery query,
        AssetQueryDocument savedDocument,
        AssetQueryDocument? transientDocument)
    {
        // A smart folder is a complete saved result set. Establish that global
        // base first, then apply the transient Current document as an AND layer.
        var savedBase = ApplyP3DocumentBaseQuery(
            query,
            savedDocument with { Scope = AssetQueryScope.AllAssets });
        var transient = transientDocument ?? new AssetQueryDocument
        {
            Scope = AssetQueryScope.Current,
            Text = query.SearchText,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All),
            SortField = savedDocument.SortField,
            SortDirection = savedDocument.SortDirection,
            IncludeArchived = savedDocument.IncludeArchived
        };
        var clauses = GetP3DocumentSearchClauses(savedDocument)
            .Concat(GetP3DocumentSearchClauses(transient))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var combined = transient with
        {
            Scope = AssetQueryScope.Current,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                savedDocument.RootGroup,
                transient.RootGroup
            ]),
            // Candidate expansion is a gate, not another filter: either layer
            // may deliberately query archived assets and its explicit rules
            // still participate in the AND-composed truth table.
            IncludeArchived = savedDocument.IncludeArchived || transient.IncludeArchived,
            SearchClauses = clauses.Length == 0 ? null : clauses
        };

        return ApplyP3DocumentBaseQuery(savedBase, combined) with
        {
            SmartFolderId = query.SmartFolderId,
            SearchText = transient.Text,
            SearchClauses = clauses
        };
    }

    private static AssetLibraryQuery ApplyP3DocumentBaseQuery(AssetLibraryQuery query, AssetQueryDocument document)
    {
        var documentSearchClauses = GetP3DocumentSearchClauses(document);
        var effective = query with
        {
            SearchText = document.Text,
            SearchClauses = documentSearchClauses.Length == 0 ? null : documentSearchClauses,
            SortField = document.SortField,
            SortDirection = document.SortDirection,
            IncludeArchived = document.IncludeArchived,
            ArchiveScope = document.IncludeArchived ? AssetLibraryArchiveScope.All : AssetLibraryArchiveScope.ActiveOnly,
            Document = document
        };
        if (document.Scope != AssetQueryScope.AllAssets) return effective;
        return effective with
        {
            FolderId = null,
            TagId = null,
            SmartFolderId = null,
            FolderIds = null,
            TagIds = null,
            UncategorizedOnly = false,
            UntaggedOnly = false,
            MissingOnly = false,
            MinimumRating = null,
            MaximumRating = null,
            MediaType = null,
            Extension = null,
            AddedFrom = null,
            AddedTo = null,
            CaptureFrom = null,
            CaptureTo = null,
            SystemCollection = null
        };
    }

    private static string[] GetP3DocumentSearchClauses(AssetQueryDocument document)
    {
        var source = document.SearchClauses is { Count: > 0 }
            ? document.SearchClauses
            : [document.Text];
        return source
            .Select(value => (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormC))
            .Where(value => value.Length != 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static AssetLibraryQuery MergeLegacyFileNameRegexIntoP3Document(
        AssetLibraryQuery query,
        AssetQueryDocument document)
    {
        if (string.IsNullOrWhiteSpace(query.FileNameRegex))
            return query with { Document = document };

        var regexRule = AssetQueryNode.Rule(
            AssetQueryField.FileName,
            AssetQueryOperator.Regex,
            [query.FileNameRegex.Trim()],
            caseSensitivity: AssetQueryCaseSensitivity.Insensitive);
        var combined = document with
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                document.RootGroup,
                regexRule
            ])
        };
        return query with { Document = combined, FileNameRegex = null };
    }

    private async Task<AssetLibraryPage> QueryP3DocumentPageAsync(
        AssetLibraryQuery query,
        int pageSize,
        AssetLibraryQueryCursor? cursor,
        CancellationToken cancellationToken)
    {
        var normalization = AssetQueryDocumentCodec.Normalize(query.Document!);
        if (!normalization.IsValid || normalization.Document is null)
            return new([], null, 0, normalization.ErrorMessage);
        var document = normalization.Document;
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var referenceErrors = await ValidateQueryReferencesAsync(connection, document, cancellationToken).ConfigureAwait(false);
        if (referenceErrors.Count != 0)
            return new([], null, 0, string.Join("；", referenceErrors.Select(error => $"{error.Path}: {error.Message}")));
        var runtimeError = ValidateRuntimeRules(document.RootGroup);
        if (runtimeError is not null) return new([], null, 0, runtimeError);

        await using var count = connection.CreateCommand();
        var countWhere = BuildIndexedWhere(query, count);
        countWhere += " AND " + CompileP3Node(document.RootGroup, count);
        count.CommandText = "SELECT COUNT(*) FROM AssetItems a WHERE " + countWhere + ";";
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);

        await using var page = connection.CreateCommand();
        var pageWhere = BuildIndexedWhere(query, page);
        pageWhere += " AND " + CompileP3Node(document.RootGroup, page);
        if (cursor is not null) pageWhere = AddQueryCursorPredicate(pageWhere, query, cursor, page);
        page.CommandText = SelectAssetSql + " WHERE " + pageWhere + " ORDER BY " + BuildQueryOrderBy(query) + " LIMIT $limit;";
        page.Parameters.AddWithValue("$limit", pageSize + 1);
        var items = new List<AssetItem>(Math.Min(total, pageSize + 1));
        await using var reader = await page.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadAsset(reader));
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        var next = hasMore && items.Count != 0
            ? await CreateQueryCursorAsync(query, items[^1], cancellationToken).ConfigureAwait(false)
            : null;
        return new(items, next, total);
    }

    public async Task<IReadOnlyList<AssetQueryValidationIssue>> ValidateQueryReferencesAsync(
        AssetQueryDocument document,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var normalized = AssetQueryDocumentCodec.Normalize(document);
        if (!normalized.IsValid || normalized.Document is null) return normalized.Errors;
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return await ValidateQueryReferencesAsync(connection, normalized.Document, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssetQueryDocument> ResolveQueryReferencesAsync(
        AssetQueryDocument document,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var normalized = AssetQueryDocumentCodec.Normalize(document);
        if (!normalized.IsValid || normalized.Document is null)
            throw new ArgumentException(normalized.ErrorMessage, nameof(document));

        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var resolved = includeArchived
            ? await AssetQueryReferenceIntegrity.ResolveLegacyNameReferencesAsync(
                connection, transaction, normalized.Document, cancellationToken).ConfigureAwait(false)
            : await AssetQueryReferenceIntegrity.ResolveActiveNameReferencesAsync(
                connection, transaction, normalized.Document, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var resolvedNormalization = AssetQueryDocumentCodec.Normalize(resolved);
        if (!resolvedNormalization.IsValid || resolvedNormalization.Document is null)
            throw new InvalidDataException(resolvedNormalization.ErrorMessage);
        return resolvedNormalization.Document;
    }

    private static async Task<IReadOnlyList<AssetQueryValidationIssue>> ValidateQueryReferencesAsync(
        SqliteConnection connection,
        AssetQueryDocument document,
        CancellationToken cancellationToken)
    {
        var errors = new List<AssetQueryValidationIssue>();
        foreach (var entry in EnumerateRules(document.RootGroup, "$.rootGroup"))
        {
            if (!entry.Rule.Enabled || entry.Rule.Field is not (AssetQueryField.Folder or AssetQueryField.Tag)) continue;
            var table = entry.Rule.Field == AssetQueryField.Folder ? "AssetFolders" : "AssetTags";
            var idColumn = entry.Rule.Field == AssetQueryField.Folder ? "FolderId" : "TagId";
            for (var index = 0; index < entry.Rule.Values.Count; index++)
            {
                var value = entry.Rule.Values[index];
                await using var command = connection.CreateCommand();
                if (value.StartsWith("id:", StringComparison.Ordinal))
                {
            command.CommandText = entry.Rule.Field == AssetQueryField.Tag
                ? $"SELECT COUNT(*) FROM AssetTags t LEFT JOIN TagGroups g ON g.TagGroupId=t.TagGroupId WHERE t.{idColumn}=$value AND t.IsArchived=0 AND (t.TagGroupId IS NULL OR g.IsArchived=0);"
                : $"SELECT COUNT(*) FROM {table} WHERE {idColumn}=$value AND IsArchived=0;";
                    command.Parameters.AddWithValue("$value", value[3..]);
                }
                else if (value.StartsWith("name:", StringComparison.Ordinal))
                {
                    command.CommandText = entry.Rule.Field == AssetQueryField.Tag
                        ? "SELECT COUNT(*) FROM AssetTags t LEFT JOIN TagGroups g ON g.TagGroupId=t.TagGroupId WHERE t.Name=$value COLLATE NOCASE AND t.IsArchived=0 AND (t.TagGroupId IS NULL OR g.IsArchived=0);"
                        : $"SELECT COUNT(*) FROM {table} WHERE Name=$value COLLATE NOCASE AND IsArchived=0;";
                    command.Parameters.AddWithValue("$value", value[5..]);
                }
                else
                {
                    errors.Add(new($"{entry.Path}.values[{index}]", "引用格式无效。"));
                    continue;
                }
                var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
                if (count != 1)
                    errors.Add(new($"{entry.Path}.values[{index}]", count == 0 ? "引用不存在或已归档。" : "名称引用不唯一，必须重新选择稳定标识。"));
            }
        }
        return errors;
    }

    public async Task<IReadOnlyList<AssetQuerySuggestion>> GetQuerySuggestionsAsync(
        string text,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var normalized = (text ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormC);
        var maximum = Math.Clamp(limit, 1, 100);
        var result = new List<AssetQuerySuggestion>();
        var fieldSuggestions = new[]
        {
            new AssetQuerySuggestion("field", "文件名", "field:fileName", "按文件名筛选"),
            new AssetQuerySuggestion("field", "标签", "field:tag", "按标签筛选"),
            new AssetQuerySuggestion("field", "文件夹", "field:folder", "按文件夹筛选"),
            new AssetQuerySuggestion("field", "评分", "field:rating", "按评分筛选"),
            new AssetQuerySuggestion("field", "文件格式", "field:extension", "按扩展名筛选")
        };
        result.AddRange(fieldSuggestions.Where(item => normalized.Length == 0 || item.Label.Contains(normalized, StringComparison.OrdinalIgnoreCase)));

        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var pattern = "%" + EscapeLikePattern(normalized) + "%";
        foreach (var descriptor in new[]
        {
            (Kind: "folder", Sql: "SELECT Name,FolderId FROM AssetFolders WHERE IsArchived=0 AND Name LIKE $text ESCAPE '\\' ORDER BY Name COLLATE NOCASE LIMIT $limit;", Prefix: "id:"),
            (Kind: "tag", Sql: "SELECT t.Name,t.TagId FROM AssetTags t LEFT JOIN TagGroups g ON g.TagGroupId=t.TagGroupId WHERE t.IsArchived=0 AND (t.TagGroupId IS NULL OR g.IsArchived=0) AND t.Name LIKE $text ESCAPE '\\' ORDER BY t.Name COLLATE NOCASE LIMIT $limit;", Prefix: "id:"),
            (Kind: "extension", Sql: "SELECT DISTINCT Extension,Extension FROM AssetItems WHERE IsArchived=0 AND Extension LIKE $text ESCAPE '\\' ORDER BY Extension COLLATE NOCASE LIMIT $limit;", Prefix: string.Empty),
            (Kind: "file", Sql: "SELECT DisplayName,DisplayName FROM AssetItems WHERE IsArchived=0 AND DisplayName LIKE $text ESCAPE '\\' ORDER BY DisplayName COLLATE NOCASE LIMIT $limit;", Prefix: string.Empty)
        })
        {
            if (result.Count >= maximum) break;
            await using var command = connection.CreateCommand();
            command.CommandText = descriptor.Sql;
            command.Parameters.AddWithValue("$text", pattern);
            command.Parameters.AddWithValue("$limit", maximum - result.Count);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (result.Count < maximum && await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                result.Add(new(descriptor.Kind, reader.GetString(0), descriptor.Prefix + reader.GetString(1), descriptor.Kind));
        }
        return result.Take(maximum).ToArray();
    }

    private static IEnumerable<(AssetQueryNode Rule, string Path)> EnumerateRules(AssetQueryNode node, string path)
    {
        // A disabled group disables its complete subtree. Descendants must not
        // participate in reference or runtime validation until the ancestor is
        // explicitly re-enabled, at which point normal fail-closed validation
        // applies again.
        if (!node.Enabled) yield break;
        if (node.Kind == AssetQueryNodeKind.Rule)
        {
            yield return (node, path);
            yield break;
        }
        for (var index = 0; index < node.Children.Count; index++)
            foreach (var item in EnumerateRules(node.Children[index], $"{path}.children[{index}]")) yield return item;
    }

    private static string? ValidateRuntimeRules(AssetQueryNode root)
    {
        foreach (var (rule, path) in EnumerateRules(root, "$.rootGroup"))
        {
            if (rule.Operator != AssetQueryOperator.Regex) continue;
            try { _ = new Regex(rule.Values.Single(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
            catch (ArgumentException exception) { return $"{path}: 正则表达式无效：{exception.Message}"; }
        }
        return null;
    }

    private static string CompileP3Node(AssetQueryNode node, SqliteCommand command)
    {
        if (!node.Enabled) return "1=1";
        string expression;
        if (node.Kind == AssetQueryNodeKind.Group)
        {
            var children = node.Children.Where(child => child.Enabled).ToArray();
            if (children.Length == 0) expression = node.Logic == AssetQueryLogic.All ? "1=1" : "0=1";
            else expression = "(" + string.Join(node.Logic == AssetQueryLogic.Any ? " OR " : " AND ", children.Select(child => CompileP3Node(child, command))) + ")";
        }
        else expression = CompileP3Rule(node, command);
        return node.Negated ? $"NOT ({expression})" : expression;
    }

    private static string CompileP3Rule(AssetQueryNode rule, SqliteCommand command)
    {
        var field = rule.Field!.Value;
        if (field is AssetQueryField.Folder or AssetQueryField.Tag) return CompileMembershipRule(rule, command);
        if (field is AssetQueryField.IsUncategorized or AssetQueryField.IsUntagged or AssetQueryField.IsMissing or AssetQueryField.IsArchived)
            return CompileBooleanRule(rule);
        if (IsP3VisualField(field)) return CompileVisualRule(rule, command);
        var column = field switch
        {
            AssetQueryField.FileName => "a.DisplayName",
            AssetQueryField.Extension => "a.Extension",
            AssetQueryField.MediaType => "a.MediaType",
            AssetQueryField.Comment => "a.Comment",
            AssetQueryField.AddedAt => "a.AddedAt",
            AssetQueryField.CaptureTime => "a.CaptureTime",
            AssetQueryField.FileSize => "a.FileSize",
            AssetQueryField.Width => "a.Width",
            AssetQueryField.Height => "a.Height",
            AssetQueryField.LongEdge => "CASE WHEN a.Width IS NULL OR a.Height IS NULL THEN NULL WHEN a.Width>a.Height THEN a.Width ELSE a.Height END",
            AssetQueryField.ShortEdge => "CASE WHEN a.Width IS NULL OR a.Height IS NULL THEN NULL WHEN a.Width<a.Height THEN a.Width ELSE a.Height END",
            AssetQueryField.PixelCount => "CASE WHEN a.Width IS NULL OR a.Height IS NULL THEN NULL ELSE CAST(a.Width AS INTEGER)*a.Height END",
            AssetQueryField.AspectRatio => "CASE WHEN a.Width IS NULL OR a.Height IS NULL OR a.Height=0 THEN NULL ELSE CAST(a.Width AS REAL)/a.Height END",
            AssetQueryField.Orientation => "a.Orientation",
            AssetQueryField.Rating => "a.Rating",
            _ => throw new InvalidOperationException($"Unsupported P3 query field {field}.")
        };
        if (field is AssetQueryField.AddedAt or AssetQueryField.CaptureTime)
            return CompileDateRule(column, rule, command);
        return CompileColumnRule(column, rule, command, IsP3TextField(field), IsP3NumericField(field));
    }

    private static string CompileMembershipRule(AssetQueryNode rule, SqliteCommand command)
    {
        var isFolder = rule.Field == AssetQueryField.Folder;
        var memberships = isFolder ? "AssetFolderMemberships" : "AssetTagMemberships";
        var entities = isFolder ? "AssetFolders" : "AssetTags";
        var idColumn = isFolder ? "FolderId" : "TagId";
        var predicates = new List<string>();
        foreach (var value in rule.Values)
        {
            var parameter = "$p3ref" + command.Parameters.Count;
            var byId = value.StartsWith("id:", StringComparison.Ordinal);
            command.Parameters.AddWithValue(parameter, byId ? value[3..] : value[5..]);
            var activeEntity = isFolder
                ? "e.IsArchived=0"
                : "e.IsArchived=0 AND (e.TagGroupId IS NULL OR EXISTS(SELECT 1 FROM TagGroups tg WHERE tg.TagGroupId=e.TagGroupId AND tg.IsArchived=0))";
            predicates.Add(byId
                ? $"EXISTS(SELECT 1 FROM {memberships} m JOIN {entities} e ON e.{idColumn}=m.{idColumn} WHERE m.AssetId=a.AssetId AND {activeEntity} AND e.{idColumn}={parameter})"
                : $"EXISTS(SELECT 1 FROM {memberships} m JOIN {entities} e ON e.{idColumn}=m.{idColumn} WHERE m.AssetId=a.AssetId AND {activeEntity} AND e.Name={parameter} COLLATE NOCASE)");
        }
        return rule.Operator switch
        {
            AssetQueryOperator.AnyOf => "(" + string.Join(" OR ", predicates) + ")",
            AssetQueryOperator.AllOf => "(" + string.Join(" AND ", predicates) + ")",
            AssetQueryOperator.NoneOf => "NOT (" + string.Join(" OR ", predicates) + ")",
            _ => "0=1"
        };
    }

    private static string CompileBooleanRule(AssetQueryNode rule)
    {
        var truth = rule.Field switch
        {
            AssetQueryField.IsUncategorized => "NOT EXISTS(SELECT 1 FROM AssetFolderMemberships m JOIN AssetFolders f ON f.FolderId=m.FolderId WHERE m.AssetId=a.AssetId AND f.IsArchived=0)",
            AssetQueryField.IsUntagged => "NOT EXISTS(SELECT 1 FROM AssetTagMemberships m JOIN AssetTags t ON t.TagId=m.TagId LEFT JOIN TagGroups g ON g.TagGroupId=t.TagGroupId WHERE m.AssetId=a.AssetId AND t.IsArchived=0 AND (t.TagGroupId IS NULL OR g.IsArchived=0))",
            AssetQueryField.IsMissing => "a.IsMissing=1",
            AssetQueryField.IsArchived => "a.IsArchived=1",
            _ => "0=1"
        };
        return rule.Operator == AssetQueryOperator.IsFalse ? $"NOT ({truth})" : truth;
    }

    private static string CompileColumnRule(string column, AssetQueryNode rule, SqliteCommand command, bool text, bool numeric = false)
    {
        if (rule.Operator is AssetQueryOperator.Unknown or AssetQueryOperator.IsEmpty)
            return text ? $"({column} IS NULL OR length(trim({column}))=0)" : $"{column} IS NULL";
        if (rule.Operator is AssetQueryOperator.Known or AssetQueryOperator.IsNotEmpty)
            return text ? $"({column} IS NOT NULL AND length(trim({column}))>0)" : $"{column} IS NOT NULL";
        if (rule.Operator == AssetQueryOperator.Between)
        {
            var lower = AddP3Parameter(command, ParseP3Parameter(rule.Values[0], numeric));
            var upper = AddP3Parameter(command, ParseP3Parameter(rule.Values[1], numeric));
            return $"({column}>={lower} AND {column}<={upper})";
        }
        var collate = text && rule.CaseSensitivity == AssetQueryCaseSensitivity.Insensitive ? " COLLATE NOCASE" : string.Empty;
        if (rule.Operator is AssetQueryOperator.AnyOf or AssetQueryOperator.NoneOf)
        {
            var parameters = rule.Values.Select(value => AddP3Parameter(command, ParseP3Parameter(value, numeric))).ToArray();
            var comparedColumn = string.IsNullOrEmpty(collate) ? column : $"({column}){collate}";
            return rule.Operator == AssetQueryOperator.AnyOf
                ? $"{comparedColumn} IN ({string.Join(',', parameters)})"
                : $"{comparedColumn} NOT IN ({string.Join(',', parameters)})";
        }
        var rawValue = rule.Values[0];
        var parameterValue = text && rule.CaseSensitivity == AssetQueryCaseSensitivity.Sensitive
            ? rawValue
            : rule.Operator switch
            {
                AssetQueryOperator.Contains or AssetQueryOperator.NotContains => "%" + EscapeLikePattern(rawValue) + "%",
                AssetQueryOperator.StartsWith => EscapeLikePattern(rawValue) + "%",
                AssetQueryOperator.EndsWith => "%" + EscapeLikePattern(rawValue),
                _ => rawValue
            };
        var parameter = AddP3Parameter(command, ParseP3Parameter(parameterValue, numeric && rule.Operator is not (AssetQueryOperator.Contains or AssetQueryOperator.NotContains or AssetQueryOperator.StartsWith or AssetQueryOperator.EndsWith or AssetQueryOperator.Regex)));
        if (text && rule.CaseSensitivity == AssetQueryCaseSensitivity.Sensitive)
        {
            return rule.Operator switch
            {
                AssetQueryOperator.Contains => $"instr({column},{parameter})>0",
                AssetQueryOperator.NotContains => $"instr({column},{parameter})=0",
                AssetQueryOperator.StartsWith => $"substr({column},1,length({parameter}))={parameter} COLLATE BINARY",
                AssetQueryOperator.EndsWith => $"substr({column},-length({parameter}))={parameter} COLLATE BINARY",
                AssetQueryOperator.Regex => $"regexp_cs({parameter},{column})",
                _ => rule.Operator switch
                {
                    AssetQueryOperator.Equals => $"{column}={parameter} COLLATE BINARY",
                    AssetQueryOperator.NotEquals => $"{column}<>{parameter} COLLATE BINARY",
                    _ => "0=1"
                }
            };
        }
        return rule.Operator switch
        {
            AssetQueryOperator.Contains => $"{column} LIKE {parameter} ESCAPE '\\'{collate}",
            AssetQueryOperator.NotContains => $"NOT ({column} LIKE {parameter} ESCAPE '\\'{collate})",
            AssetQueryOperator.StartsWith => $"{column} LIKE {parameter} ESCAPE '\\'{collate}",
            AssetQueryOperator.EndsWith => $"{column} LIKE {parameter} ESCAPE '\\'{collate}",
            AssetQueryOperator.Regex => $"regexp({parameter},{column})",
            AssetQueryOperator.Equals => $"{column}={parameter}{collate}",
            AssetQueryOperator.NotEquals => $"{column}<>{parameter}{collate}",
            AssetQueryOperator.GreaterThan => $"{column}>{parameter}",
            AssetQueryOperator.GreaterThanOrEqual => $"{column}>={parameter}",
            AssetQueryOperator.LessThan => $"{column}<{parameter}",
            AssetQueryOperator.LessThanOrEqual => $"{column}<={parameter}",
            _ => "0=1"
        };
    }

    private static string CompileDateRule(string column, AssetQueryNode rule, SqliteCommand command)
    {
        if (rule.Operator == AssetQueryOperator.Unknown) return $"{column} IS NULL";
        if (rule.Operator == AssetQueryOperator.Known) return $"{column} IS NOT NULL";

        var instant = $"julianday({column})";
        if (rule.Operator == AssetQueryOperator.Between)
        {
            var lower = AddP3Parameter(command, rule.Values[0]);
            var upper = AddP3Parameter(command, rule.Values[1]);
            return $"({instant}>=julianday({lower}) AND {instant}<=julianday({upper}))";
        }

        var value = AddP3Parameter(command, rule.Values[0]);
        var comparison = $"julianday({value})";
        return rule.Operator switch
        {
            AssetQueryOperator.Equals => $"{instant}={comparison}",
            AssetQueryOperator.NotEquals => $"{instant}<>{comparison}",
            AssetQueryOperator.GreaterThan => $"{instant}>{comparison}",
            AssetQueryOperator.GreaterThanOrEqual => $"{instant}>={comparison}",
            AssetQueryOperator.LessThan => $"{instant}<{comparison}",
            AssetQueryOperator.LessThanOrEqual => $"{instant}<={comparison}",
            _ => "0=1"
        };
    }

    private static object ParseP3Parameter(string value, bool numeric) => numeric
        ? double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)
        : value;

    private static string AddP3Parameter(SqliteCommand command, object value)
    {
        var name = "$p3" + command.Parameters.Count;
        command.Parameters.AddWithValue(name, value);
        return name;
    }

    private static bool IsP3TextField(AssetQueryField field) => field is
        AssetQueryField.FileName or AssetQueryField.Extension or AssetQueryField.MediaType or AssetQueryField.Comment or AssetQueryField.Orientation;

    private static bool IsP3NumericField(AssetQueryField field) => field is
        AssetQueryField.Rating or AssetQueryField.FileSize or AssetQueryField.Width or AssetQueryField.Height or
        AssetQueryField.LongEdge or AssetQueryField.ShortEdge or AssetQueryField.PixelCount or AssetQueryField.AspectRatio or
        AssetQueryField.VisualDominantHue or AssetQueryField.VisualAverageLuma or AssetQueryField.VisualAverageSaturation or
        AssetQueryField.VisualLumaSpread or AssetQueryField.VisualShadowRatio or AssetQueryField.VisualHighlightRatio or
        AssetQueryField.VisualBlackClipRatio or AssetQueryField.VisualWhiteClipRatio;

    private static bool IsP3VisualField(AssetQueryField field) => field is
        AssetQueryField.VisualAnalysisStatus or AssetQueryField.VisualHarmony or AssetQueryField.VisualToneKey or
        AssetQueryField.VisualContrast or AssetQueryField.VisualSaturation or AssetQueryField.VisualWarmCool or
        AssetQueryField.VisualDominantHue or AssetQueryField.VisualDominantColor or AssetQueryField.VisualAverageLuma or
        AssetQueryField.VisualAverageSaturation or AssetQueryField.VisualLumaSpread or AssetQueryField.VisualShadowRatio or
        AssetQueryField.VisualHighlightRatio or AssetQueryField.VisualBlackClipRatio or AssetQueryField.VisualWhiteClipRatio;

    private static string CompileVisualRule(AssetQueryNode rule, SqliteCommand command)
    {
        var version = AddP3Parameter(command, AssetVisualFeatureContract.AnalysisVersion);
        if (rule.Field == AssetQueryField.VisualAnalysisStatus)
        {
            var state = $"CASE WHEN NOT EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE vf.AssetId=a.AssetId) THEN 'NotAnalyzed' " +
                        $"WHEN NOT EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE vf.AssetId=a.AssetId AND vf.AnalysisVersion={version}) THEN 'Stale' " +
                        $"WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE {BuildP3CurrentVisualFeaturePredicate("vf", version, "Succeeded")}) THEN 'Valid' " +
                        $"WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE {BuildP3CurrentVisualFeaturePredicate("vf", version, "Failed")}) THEN 'Failed' ELSE 'Stale' END";
            return CompileColumnRule(state, rule, command, text: true);
        }
        if (rule.Field == AssetQueryField.VisualDominantHue && rule.Operator == AssetQueryOperator.Between)
        {
            var lowerValue = double.Parse(rule.Values[0], NumberStyles.Float, CultureInfo.InvariantCulture);
            var upperValue = double.Parse(rule.Values[1], NumberStyles.Float, CultureInfo.InvariantCulture);
            var lower = AddP3Parameter(command, lowerValue);
            var upper = AddP3Parameter(command, upperValue);
            var range = lowerValue <= upperValue ? $"vf.DominantHue BETWEEN {lower} AND {upper}" : $"(vf.DominantHue>={lower} OR vf.DominantHue<={upper})";
            var match = $"EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE {BuildP3CurrentVisualFeaturePredicate("vf", version)} AND {range})";
            return GuardP3VisualRuleWithCurrentFeature(match, version);
        }
        if (rule.Field == AssetQueryField.VisualDominantColor)
        {
            var color = AddP3Parameter(command, rule.Values[0].Trim().ToUpperInvariant());
            var exists = $"EXISTS(SELECT 1 FROM AssetVisualFeatures vf JOIN AssetVisualPaletteColors pc ON pc.AssetId=vf.AssetId AND pc.AnalysisVersion=vf.AnalysisVersion WHERE {BuildP3CurrentVisualFeaturePredicate("vf", version)} AND upper(pc.Hex)=upper({color}))";
            var match = rule.Operator == AssetQueryOperator.NotEquals ? $"NOT ({exists})" : exists;
            return GuardP3VisualRuleWithCurrentFeature(match, version);
        }
        var column = rule.Field switch
        {
            AssetQueryField.VisualHarmony => "vf.Harmony",
            AssetQueryField.VisualToneKey => "vf.ToneKey",
            AssetQueryField.VisualContrast => "vf.Contrast",
            AssetQueryField.VisualSaturation => "vf.Saturation",
            AssetQueryField.VisualWarmCool => "vf.WarmCool",
            AssetQueryField.VisualDominantHue => "vf.DominantHue",
            AssetQueryField.VisualAverageLuma => "vf.AverageLuma",
            AssetQueryField.VisualAverageSaturation => "vf.AverageSaturation",
            AssetQueryField.VisualLumaSpread => "vf.LumaSpreadMetric",
            AssetQueryField.VisualShadowRatio => "vf.ShadowRatio",
            AssetQueryField.VisualHighlightRatio => "vf.HighlightRatio",
            AssetQueryField.VisualBlackClipRatio => "vf.BlackClipRatio",
            AssetQueryField.VisualWhiteClipRatio => "vf.WhiteClipRatio",
            _ => "NULL"
        };
        var visualText = rule.Field is AssetQueryField.VisualHarmony or AssetQueryField.VisualToneKey or AssetQueryField.VisualContrast or AssetQueryField.VisualSaturation or AssetQueryField.VisualWarmCool;
        var condition = CompileColumnRule(column, rule, command, visualText, numeric: !visualText);
        var expression = $"EXISTS(SELECT 1 FROM AssetVisualFeatures vf WHERE {BuildP3CurrentVisualFeaturePredicate("vf", version)} AND {condition})";
        return GuardP3VisualRuleWithCurrentFeature(expression, version);
    }

    private static string BuildP3CurrentVisualFeaturePredicate(
        string alias,
        string version,
        string outcome = "Succeeded") =>
        $"{alias}.AssetId=a.AssetId AND {alias}.AnalysisVersion={version} AND {alias}.Outcome='{outcome}' " +
        $"AND {alias}.SourceContentHash IS NOT NULL AND a.ContentHash IS NOT NULL AND {alias}.SourceContentHash=a.ContentHash";

    private static string GuardP3VisualRuleWithCurrentFeature(string expression, string version)
    {
        var valid = $"EXISTS(SELECT 1 FROM AssetVisualFeatures vf_guard WHERE {BuildP3CurrentVisualFeaturePredicate("vf_guard", version)})";
        // NULL deliberately preserves the validity requirement through NOT at either
        // the rule or group level; SQL three-valued logic will not turn an absent or
        // stale feature into a match for a negative visual condition.
        return $"CASE WHEN {valid} THEN ({expression}) ELSE NULL END";
    }

    public async Task<SmartFolderQueryDocument?> GetSmartFolderQueryDocumentAsync(
        Guid smartFolderId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DocumentVersion,QueryJson,QueryHash,LegacyRulesBackupJson,UpdatedAt FROM SmartFolderQueryDocuments WHERE SmartFolderId=$id;";
        command.Parameters.AddWithValue("$id", smartFolderId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var version = reader.GetInt32(0);
        if (version != AssetQueryDocument.CurrentVersion)
            throw new InvalidDataException($"不支持智能文件夹查询版本 {version}。");
        var parsed = AssetQueryDocumentCodec.Parse(reader.GetString(1));
        if (!parsed.IsValid || parsed.Document is null)
            throw new InvalidDataException(parsed.ErrorMessage);
        var expectedHash = AssetQueryDocumentCodec.ComputeHash(parsed.Document);
        var storedHash = reader.GetString(2);
        if (!string.Equals(expectedHash, storedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("智能文件夹查询文档哈希不匹配。");
        return new(
            smartFolderId,
            parsed.Document,
            expectedHash,
            reader.IsDBNull(3) ? null : reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture));
    }

    public async Task<SmartFolder> SaveSmartFolderQueryDocumentAsync(
        SmartFolder folder,
        AssetQueryDocument document,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var name = NormalizeEntityName(folder.Name, nameof(folder));
        var normalized = AssetQueryDocumentCodec.Normalize(document);
        if (!normalized.IsValid || normalized.Document is null)
            throw new ArgumentException(normalized.ErrorMessage, nameof(document));
        var runtimeError = ValidateRuntimeRules(normalized.Document.RootGroup);
        if (runtimeError is not null)
            throw new ArgumentException(runtimeError, nameof(document));
        var referenceErrors = await ValidateQueryReferencesAsync(normalized.Document, cancellationToken).ConfigureAwait(false);
        if (referenceErrors.Count != 0)
            throw new InvalidOperationException(string.Join("；", referenceErrors.Select(error => $"{error.Path}: {error.Message}")));
        var canonical = AssetQueryDocumentCodec.SerializeCanonical(normalized.Document);
        var hash = AssetQueryDocumentCodec.ComputeHash(normalized.Document);
        var now = DateTimeOffset.UtcNow;
        var created = folder.CreatedAt ?? now;

        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await EnsureUniqueEntityNameAsync(connection, transaction, "SmartFolders", "SmartFolderId", folder.SmartFolderId, null, name, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO SmartFolders(SmartFolderId,Name,Logic,Description,CreatedAt,UpdatedAt,IsArchived)
            VALUES($id,$name,$logic,$description,$created,$updated,$archived)
            ON CONFLICT(SmartFolderId) DO UPDATE SET Name=excluded.Name,Logic=excluded.Logic,Description=excluded.Description,UpdatedAt=excluded.UpdatedAt,IsArchived=excluded.IsArchived;
            """, cancellationToken,
            ("$id", folder.SmartFolderId.ToString("D")), ("$name", name), ("$logic", normalized.Document.RootGroup.Logic == AssetQueryLogic.Any ? SmartFolderLogic.Or.ToString() : SmartFolderLogic.And.ToString()),
            ("$description", folder.Description ?? string.Empty), ("$created", created.ToString("O")), ("$updated", now.ToString("O")), ("$archived", folder.IsArchived ? 1 : 0)).ConfigureAwait(false);

        string? backup = null;
        await using (var readBackup = connection.CreateCommand())
        {
            readBackup.Transaction = transaction;
            readBackup.CommandText = "SELECT LegacyRulesBackupJson FROM SmartFolderQueryDocuments WHERE SmartFolderId=$id;";
            readBackup.Parameters.AddWithValue("$id", folder.SmartFolderId.ToString("D"));
            var scalar = await readBackup.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            backup = scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
        }
        await ExecuteAsync(connection, transaction, """
            INSERT INTO SmartFolderQueryDocuments(SmartFolderId,DocumentVersion,QueryJson,QueryHash,LegacyRulesBackupJson,UpdatedAt)
            VALUES($id,$version,$json,$hash,$backup,$updated)
            ON CONFLICT(SmartFolderId) DO UPDATE SET DocumentVersion=excluded.DocumentVersion,QueryJson=excluded.QueryJson,QueryHash=excluded.QueryHash,LegacyRulesBackupJson=COALESCE(SmartFolderQueryDocuments.LegacyRulesBackupJson,excluded.LegacyRulesBackupJson),UpdatedAt=excluded.UpdatedAt;
            """, cancellationToken,
            ("$id", folder.SmartFolderId.ToString("D")), ("$version", AssetQueryDocument.CurrentVersion), ("$json", canonical),
            ("$hash", hash), ("$backup", (object?)backup ?? DBNull.Value), ("$updated", now.ToString("O"))).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return folder with
        {
            Name = name,
            Logic = normalized.Document.RootGroup.Logic == AssetQueryLogic.Any ? SmartFolderLogic.Or : SmartFolderLogic.And,
            CreatedAt = created,
            UpdatedAt = now
        };
    }

    public async Task<SmartFolder> CopySmartFolderAsync(
        Guid smartFolderId,
        string? copyName = null,
        CancellationToken cancellationToken = default)
    {
        var source = (await ListSmartFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(folder => folder.SmartFolderId == smartFolderId)
            ?? throw new KeyNotFoundException("智能文件夹不存在。");
        var document = await GetSmartFolderQueryDocumentAsync(smartFolderId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("智能文件夹缺少查询文档。");
        var existing = (await ListSmartFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false))
            .Select(folder => NormalizeEntityKey(folder.Name)).ToHashSet(StringComparer.Ordinal);
        var desired = string.IsNullOrWhiteSpace(copyName) ? source.Name + "（副本）" : copyName!;
        var candidate = NormalizeEntityName(desired, nameof(copyName));
        if (existing.Contains(NormalizeEntityKey(candidate)))
        {
            var suffix = 2;
            do { candidate = NormalizeEntityName(source.Name + $"（副本 {suffix++}）", nameof(copyName)); }
            while (existing.Contains(NormalizeEntityKey(candidate)));
        }
        var copy = source with { SmartFolderId = Guid.NewGuid(), Name = candidate, IsArchived = false, CreatedAt = null, UpdatedAt = null };
        return await SaveSmartFolderQueryDocumentAsync(copy, document.Document, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssetLibraryBatchResult> SetSmartFolderArchivedAsync(
        Guid smartFolderId,
        bool isArchived,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var before = (await ListSmartFoldersAsync(includeArchived: true, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(folder => folder.SmartFolderId == smartFolderId)
            ?? throw new KeyNotFoundException("智能文件夹不存在。");
        if (before.IsArchived == isArchived) return new(0, null, []);
        var after = before with { IsArchived = isArchived, UpdatedAt = DateTimeOffset.UtcNow };
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ApplySmartFolderStateAsync(connection, transaction, after, cancellationToken).ConfigureAwait(false);
        var token = CreateUndoToken(isArchived ? "Archive smart folder" : "Restore smart folder");
        await WriteUndoJournalAsync(connection, transaction, token, "smart-folder-state-v2", new P3SmartFolderStateChange(before, after), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(1, token, []);
    }

    private static async Task ApplySmartFolderStateAsync(SqliteConnection connection, SqliteTransaction transaction, SmartFolder folder, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction,
            "UPDATE SmartFolders SET Name=$name,Logic=$logic,Description=$description,UpdatedAt=$updated,IsArchived=$archived WHERE SmartFolderId=$id;",
            cancellationToken, ("$name", folder.Name), ("$logic", folder.Logic.ToString()), ("$description", folder.Description),
            ("$updated", folder.EffectiveUpdatedAt.ToString("O")), ("$archived", folder.IsArchived ? 1 : 0), ("$id", folder.SmartFolderId.ToString("D"))).ConfigureAwait(false);
    }

    private static async Task UpsertLegacySmartFolderDocumentAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SmartFolder folder,
        IReadOnlyList<SmartFolderRule> rules,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var rootChildren = new List<AssetQueryNode>();
        foreach (var rule in rules.Where(rule => rule.GroupId is null)) rootChildren.Add(ConvertLegacyRule(rule));
        foreach (var group in rules.Where(rule => rule.GroupId is not null).GroupBy(rule => rule.GroupId))
            rootChildren.Add(AssetQueryNode.Group(
                group.First().GroupLogic == SmartFolderLogic.Or ? AssetQueryLogic.Any : AssetQueryLogic.All,
                group.Select(ConvertLegacyRule)));
        var document = new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            RootGroup = AssetQueryNode.Group(folder.Logic == SmartFolderLogic.Or ? AssetQueryLogic.Any : AssetQueryLogic.All, rootChildren)
        };
        document = await AssetQueryReferenceIntegrity.ResolveActiveNameReferencesAsync(
            connection, transaction, document, cancellationToken).ConfigureAwait(false);
        var canonical = AssetQueryDocumentCodec.SerializeCanonical(document);
        var hash = AssetQueryDocumentCodec.ComputeHash(document);
        var backup = JsonSerializer.Serialize(rules);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO SmartFolderQueryDocuments(SmartFolderId,DocumentVersion,QueryJson,QueryHash,LegacyRulesBackupJson,UpdatedAt)
            VALUES($id,$version,$json,$hash,$backup,$updated)
            ON CONFLICT(SmartFolderId) DO UPDATE SET DocumentVersion=excluded.DocumentVersion,QueryJson=excluded.QueryJson,QueryHash=excluded.QueryHash,LegacyRulesBackupJson=excluded.LegacyRulesBackupJson,UpdatedAt=excluded.UpdatedAt;
            """, cancellationToken, ("$id", folder.SmartFolderId.ToString("D")), ("$version", AssetQueryDocument.CurrentVersion),
            ("$json", canonical), ("$hash", hash), ("$backup", backup), ("$updated", updatedAt.ToString("O"))).ConfigureAwait(false);
    }

    private static AssetQueryNode ConvertLegacyRule(SmartFolderRule rule)
    {
        if (!Enum.TryParse<AssetQueryField>(rule.Field.ToString(), true, out var field))
            throw new InvalidDataException($"无法迁移智能文件夹字段 {rule.Field}。");
        var operation = rule.Operator switch
        {
            SmartFolderOperator.Contains => AssetQueryOperator.Contains,
            SmartFolderOperator.Equals => field is AssetQueryField.Folder or AssetQueryField.Tag ? AssetQueryOperator.AnyOf : AssetQueryOperator.Equals,
            SmartFolderOperator.NotEquals => field is AssetQueryField.Folder or AssetQueryField.Tag ? AssetQueryOperator.NoneOf : AssetQueryOperator.NotEquals,
            SmartFolderOperator.StartsWith => AssetQueryOperator.StartsWith,
            SmartFolderOperator.EndsWith => AssetQueryOperator.EndsWith,
            SmartFolderOperator.GreaterThan => AssetQueryOperator.GreaterThan,
            SmartFolderOperator.GreaterThanOrEqual => AssetQueryOperator.GreaterThanOrEqual,
            SmartFolderOperator.LessThan => AssetQueryOperator.LessThan,
            SmartFolderOperator.LessThanOrEqual => AssetQueryOperator.LessThanOrEqual,
            SmartFolderOperator.Regex => AssetQueryOperator.Regex,
            SmartFolderOperator.IsTrue => AssetQueryOperator.IsTrue,
            SmartFolderOperator.IsFalse => AssetQueryOperator.IsFalse,
            SmartFolderOperator.InRange => AssetQueryOperator.Between,
            _ => throw new InvalidDataException($"无法迁移智能文件夹操作符 {rule.Operator}。")
        };
        IReadOnlyList<string> values = operation switch
        {
            AssetQueryOperator.IsTrue or AssetQueryOperator.IsFalse => [],
            AssetQueryOperator.Between => rule.Value.Split(["..", ",", "，"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ when field is AssetQueryField.Folder or AssetQueryField.Tag => ["name:" + rule.Value],
            _ => [NormalizeP3LegacyQueryValue(field, rule.Value)]
        };
        return AssetQueryNode.Rule(field, operation, values, negated: rule.Negated);
    }

    private static string NormalizeP3LegacyQueryValue(AssetQueryField field, string value) => field == AssetQueryField.VisualAnalysisStatus
        ? value.Trim().ToLowerInvariant() switch
        {
            "analyzed" or "completed" or "succeeded" or "valid" => "Valid",
            "pending" or "notanalyzed" => "NotAnalyzed",
            "failed" => "Failed",
            "stale" or "unavailable" => "Stale",
            _ => throw new InvalidDataException($"未知旧视觉分析状态“{value}”。")
        }
        : value;

    private static string NormalizeEntityName(string? value, string parameterName)
    {
        var normalized = (value ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (normalized.Length == 0) throw new ArgumentException("名称不能为空。", parameterName);
        if (normalized.Length > 128) throw new ArgumentException("名称不能超过 128 个字符。", parameterName);
        return normalized;
    }

    private static string NormalizeEntityKey(string value) => NormalizeEntityName(value, nameof(value)).ToUpperInvariant();

    private static SmartFolderLogic ParseP3SmartFolderLogic(string value) =>
        Enum.TryParse<SmartFolderLogic>(value, true, out var logic)
            ? logic
            : throw new InvalidDataException($"未知智能文件夹逻辑“{value}”，已拒绝执行。");

    private static async Task EnsureUniqueEntityNameAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string idColumn,
        Guid currentId,
        Guid? groupId,
        string name,
        CancellationToken cancellationToken)
    {
        var values = new List<(Guid Id, string Name)>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (table == "AssetTags")
        {
            command.CommandText = $"SELECT {idColumn},Name FROM {table} WHERE (($group IS NULL AND TagGroupId IS NULL) OR TagGroupId=$group);";
            command.Parameters.AddWithValue("$group", (object?)groupId?.ToString("D") ?? DBNull.Value);
        }
        else command.CommandText = $"SELECT {idColumn},Name FROM {table};";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            if (Guid.TryParse(reader.GetString(0), out var id)) values.Add((id, reader.GetString(1)));
        var key = NormalizeEntityKey(name);
        if (values.Any(value => value.Id != currentId && string.Equals(NormalizeEntityKey(value.Name), key, StringComparison.Ordinal)))
            throw new InvalidOperationException("同一范围内已存在同名项目（忽略大小写和 Unicode 表示差异）。");
    }

    public async Task<AssetBatchMetadataPreview> PreviewBatchMetadataAsync(
        AssetBatchMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var ids = NormalizeBatchAssetIds(request.AssetIds);
        var requestFingerprint = ComputeP3BatchRequestFingerprint(request, ids);
        var conflicts = DescribeP3BatchConflictOverrides(request);
        var conflictCount = CountP3BatchConflictOverrides(request);
        if (ids.Length == 0)
        {
            var emptyStateFingerprint = ComputeP3BatchStateFingerprint(new([], [], []));
            return new(
                0, 0, 0, false, false, 0, conflictCount, conflicts,
                requestFingerprint, emptyStateFingerprint,
                ComputeP3BatchPreviewFingerprint(requestFingerprint, emptyStateFingerprint),
                []);
        }
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var simulation = await SimulateP3BatchMetadataAsync(connection, transaction, request, ids, cancellationToken).ConfigureAwait(false);
        var beforeStateFingerprint = ComputeP3BatchStateFingerprint(simulation.Before);
        var changedCount = CountChangedP3Assets(simulation.Before, simulation.After);
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return new(
            simulation.Before.Assets.Length,
            simulation.Before.Tags.Length,
            simulation.Before.Folders.Length,
            simulation.Before.Assets.Select(asset => asset.Rating).Distinct().Skip(1).Any(),
            simulation.Before.Assets.Select(asset => asset.Comment).Distinct(StringComparer.Ordinal).Skip(1).Any(),
            changedCount,
            conflictCount,
            conflicts,
            requestFingerprint,
            beforeStateFingerprint,
            ComputeP3BatchPreviewFingerprint(requestFingerprint, beforeStateFingerprint),
            []);
    }

    public async Task<AssetLibraryBatchResult> ApplyBatchMetadataAsync(
        AssetBatchMetadataRequest request,
        AssetBatchMetadataPreview previewContract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewContract);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var ids = NormalizeBatchAssetIds(request.AssetIds);
        var requestFingerprint = ComputeP3BatchRequestFingerprint(request, ids);
        if (ids.Length == 0)
        {
            var emptyStateFingerprint = ComputeP3BatchStateFingerprint(new([], [], []));
            EnsureP3BatchPreviewContract(
                previewContract, requestFingerprint, emptyStateFingerprint, actualChangedCount: 0);
            return new(0, null, []);
        }

        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        // Acquire the write reservation before reading the preview state. This
        // makes the fingerprint comparison and the resulting mutation one
        // indivisible database decision.
        await using var transaction = connection.BeginTransaction(deferred: false);
        var simulation = await SimulateP3BatchMetadataAsync(connection, transaction, request, ids, cancellationToken).ConfigureAwait(false);
        var changedCount = CountChangedP3Assets(simulation.Before, simulation.After);
        var beforeStateFingerprint = ComputeP3BatchStateFingerprint(simulation.Before);
        try
        {
            EnsureP3BatchPreviewContract(
                previewContract, requestFingerprint, beforeStateFingerprint, changedCount);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        if (changedCount == 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(0, null, []);
        }
        var token = CreateUndoToken($"Update {changedCount} asset metadata rows");
        await WriteUndoJournalAsync(connection, transaction, token, "asset-batch-metadata-v2", new P3BatchMetadataChange(simulation.Before, simulation.After), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(changedCount, token, []);
    }

    private static void EnsureP3BatchPreviewContract(
        AssetBatchMetadataPreview preview,
        string actualRequestFingerprint,
        string actualBeforeStateFingerprint,
        int actualChangedCount)
    {
        var actualPreviewFingerprint = ComputeP3BatchPreviewFingerprint(
            actualRequestFingerprint, actualBeforeStateFingerprint);
        if (!string.Equals(preview.CanonicalRequestFingerprint, actualRequestFingerprint, StringComparison.Ordinal) ||
            !string.Equals(preview.BeforeStateFingerprint, actualBeforeStateFingerprint, StringComparison.Ordinal) ||
            !string.Equals(preview.PreviewFingerprint, actualPreviewFingerprint, StringComparison.Ordinal) ||
            preview.ChangedCount != actualChangedCount)
        {
            throw new InvalidOperationException("批量预览已过期：请求或素材元数据已变化，请重新预览后再应用。");
        }
    }

    private static string ComputeP3BatchRequestFingerprint(
        AssetBatchMetadataRequest request,
        IReadOnlyList<Guid> normalizedAssetIds)
    {
        var addTags = NormalizeIds(request.AddTagIds).OrderBy(id => id).ToArray();
        var removeTags = NormalizeIds(request.RemoveTagIds).Except(addTags).OrderBy(id => id).ToArray();
        var addFolders = NormalizeIds(request.AddFolderIds).OrderBy(id => id).ToArray();
        var removeFolders = NormalizeIds(request.RemoveFolderIds).Except(addFolders).OrderBy(id => id).ToArray();
        var canonical = new P3CanonicalBatchRequest(
            normalizedAssetIds.OrderBy(id => id).ToArray(),
            addTags, removeTags, addFolders, removeFolders,
            request.ClearRating ? null : request.Rating,
            request.ClearRating,
            request.ClearComment ? null : request.Comment,
            request.ClearComment,
            request.IsArchived,
            request.IsMissing);
        return ComputeP3ContractHash(JsonSerializer.Serialize(canonical));
    }

    private static IReadOnlyList<string> DescribeP3BatchConflictOverrides(AssetBatchMetadataRequest request)
    {
        var summaries = new List<string>();
        var tagOverlap = NormalizeIds(request.AddTagIds).Intersect(NormalizeIds(request.RemoveTagIds)).Count();
        if (tagOverlap != 0) summaries.Add($"{tagOverlap:N0} 个标签同时添加和移除，按添加覆盖移除。");
        var folderOverlap = NormalizeIds(request.AddFolderIds).Intersect(NormalizeIds(request.RemoveFolderIds)).Count();
        if (folderOverlap != 0) summaries.Add($"{folderOverlap:N0} 个文件夹同时添加和移除，按添加覆盖移除。");
        if (request.ClearRating && request.Rating is not null) summaries.Add("评分同时设置和清除，按清除覆盖设置。");
        if (request.ClearComment && request.Comment is not null) summaries.Add("备注同时设置和清除，按清除覆盖设置。");
        return summaries;
    }

    private static int CountP3BatchConflictOverrides(AssetBatchMetadataRequest request) =>
        NormalizeIds(request.AddTagIds).Intersect(NormalizeIds(request.RemoveTagIds)).Count() +
        NormalizeIds(request.AddFolderIds).Intersect(NormalizeIds(request.RemoveFolderIds)).Count() +
        (request.ClearRating && request.Rating is not null ? 1 : 0) +
        (request.ClearComment && request.Comment is not null ? 1 : 0);

    private static string ComputeP3BatchStateFingerprint(P3BatchSnapshot snapshot) =>
        ComputeP3ContractHash(JsonSerializer.Serialize(snapshot));

    private static string ComputeP3BatchPreviewFingerprint(string requestFingerprint, string stateFingerprint) =>
        ComputeP3ContractHash($"p3-batch-preview-v1|{requestFingerprint}|{stateFingerprint}");

    private static string ComputeP3ContractHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static async Task<P3BatchSimulation> SimulateP3BatchMetadataAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AssetBatchMetadataRequest request,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (request.Rating is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(request));
        var addTags = NormalizeIds(request.AddTagIds);
        var removeTags = NormalizeIds(request.RemoveTagIds).Except(addTags).ToArray();
        var addFolders = NormalizeIds(request.AddFolderIds);
        var removeFolders = NormalizeIds(request.RemoveFolderIds).Except(addFolders).ToArray();

        // Preview and apply deliberately share this exact validation and
        // mutation path. Preview runs it in a transaction that is rolled back.
        await ValidateActiveIdsAsync(connection, transaction, "AssetTags", "TagId", addTags.Concat(removeTags), cancellationToken).ConfigureAwait(false);
        await ValidateActiveIdsAsync(connection, transaction, "AssetFolders", "FolderId", addFolders.Concat(removeFolders), cancellationToken).ConfigureAwait(false);
        await CreateP3SelectionTableAsync(connection, transaction, ids, cancellationToken).ConfigureAwait(false);
        var before = await ReadP3BatchSnapshotAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (before.Assets.Length != ids.Count)
            throw new KeyNotFoundException($"批量请求包含 {ids.Count - before.Assets.Length} 个不存在的素材标识，未执行任何更改。");

        foreach (var asset in before.Assets)
        {
            var rating = request.ClearRating ? 0 : request.Rating ?? asset.Rating;
            var comment = request.ClearComment ? string.Empty : request.Comment ?? asset.Comment;
            await ExecuteAsync(connection, transaction, "UPDATE AssetItems SET Rating=$rating,Comment=$comment,IsArchived=$archived,IsMissing=$missing WHERE AssetId=$id;", cancellationToken,
                ("$rating", rating), ("$comment", comment), ("$archived", (request.IsArchived ?? asset.IsArchived) ? 1 : 0),
                ("$missing", (request.IsMissing ?? asset.IsMissing) ? 1 : 0), ("$id", asset.AssetId.ToString("D"))).ConfigureAwait(false);
        }
        await ApplyP3MembershipDeltaAsync(connection, transaction, "AssetTagMemberships", "TagId", addTags, removeTags, cancellationToken).ConfigureAwait(false);
        await ApplyP3MembershipDeltaAsync(connection, transaction, "AssetFolderMemberships", "FolderId", addFolders, removeFolders, cancellationToken).ConfigureAwait(false);
        await ApplyP3FolderAutoTagsAsync(connection, transaction, addFolders, cancellationToken).ConfigureAwait(false);
        var after = await ReadP3BatchSnapshotAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        return new(before, after);
    }

    public async Task<AssetLibraryBatchResult> SetTagArchivedAsync(Guid tagId, bool isArchived, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var before = await ReadP3TagAsync(tagId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("标签不存在。");
        if (before.IsArchived == isArchived) return new(0, null, []);
        var after = before with { IsArchived = isArchived };
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (isArchived)
            await ResolveP3NameReferencesInAllDocumentsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTagSaveAllowedAsync(connection, transaction, after, cancellationToken).ConfigureAwait(false);
        await SaveTagInTransactionAsync(connection, transaction, after, cancellationToken).ConfigureAwait(false);
        var token = CreateUndoToken(isArchived ? "Archive tag" : "Restore tag");
        await WriteUndoJournalAsync(connection, transaction, token, "tag-state-v2", new P3TagStateChange(before, after), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(1, token, []);
    }

    private async Task<AssetLibraryBatchResult> RenameTagP3Async(Guid tagId, string name, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var before = await ReadP3TagAsync(tagId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("标签不存在。");
        var normalized = NormalizeEntityName(name, nameof(name));
        if (string.Equals(before.Name, normalized, StringComparison.Ordinal)) return new(0, null, []);
        var after = before with { Name = normalized };
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ResolveP3NameReferencesInAllDocumentsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await EnsureUniqueEntityNameAsync(connection, transaction, "AssetTags", "TagId", tagId, before.TagGroupId, normalized, cancellationToken).ConfigureAwait(false);
        await SaveTagInTransactionAsync(connection, transaction, after, cancellationToken).ConfigureAwait(false);
        var token = CreateUndoToken("Rename tag");
        await WriteUndoJournalAsync(connection, transaction, token, "tag-batch-state-v2", new P3TagBatchStateChange([before], [after]), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(1, token, []);
    }

    private static async Task ResolveP3NameReferencesInAllDocumentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = new List<(Guid Id, string Json, string Hash)>();
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT SmartFolderId,QueryJson,QueryHash FROM SmartFolderQueryDocuments ORDER BY SmartFolderId;";
            await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                rows.Add((Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)));
        }

        foreach (var row in rows)
        {
            var parsed = AssetQueryDocumentCodec.Parse(row.Json);
            if (!parsed.IsValid || parsed.Document is null) throw new InvalidDataException(parsed.ErrorMessage);
            var resolved = await AssetQueryReferenceIntegrity.ResolveActiveNameReferencesAsync(
                connection, transaction, parsed.Document, cancellationToken).ConfigureAwait(false);
            var hash = AssetQueryDocumentCodec.ComputeHash(resolved);
            if (string.Equals(hash, row.Hash, StringComparison.OrdinalIgnoreCase)) continue;
            await ExecuteAsync(connection, transaction,
                "UPDATE SmartFolderQueryDocuments SET QueryJson=$json,QueryHash=$hash,UpdatedAt=$updated WHERE SmartFolderId=$id;",
                cancellationToken,
                ("$json", AssetQueryDocumentCodec.SerializeCanonical(resolved)),
                ("$hash", hash),
                ("$updated", DateTimeOffset.UtcNow.ToString("O")),
                ("$id", row.Id.ToString("D"))).ConfigureAwait(false);
        }
    }

    private async Task<AssetLibraryBatchResult> MoveTagsToGroupP3Async(IEnumerable<Guid> tagIds, Guid? tagGroupId, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var ids = NormalizeIds(tagIds);
        if (ids.Length == 0) return new(0, null, []);
        if (tagGroupId is not null)
        {
            var groups = await ListTagGroupsAsync(includeArchived: true, cancellationToken).ConfigureAwait(false);
            if (groups.All(group => group.TagGroupId != tagGroupId || group.IsArchived)) throw new InvalidOperationException("目标标签组不存在或已归档。");
        }
        var before = new List<AssetTag>();
        foreach (var id in ids) before.Add(await ReadP3TagAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("标签不存在。"));
        var after = before.Select(tag => tag with { TagGroupId = tagGroupId }).ToArray();
        var keys = after.Select(tag => NormalizeEntityKey(tag.Name)).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length) throw new InvalidOperationException("移动后会产生同名标签冲突。");
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var tag in after)
            await EnsureUniqueEntityNameAsync(connection, transaction, "AssetTags", "TagId", tag.TagId, tagGroupId, tag.Name, cancellationToken).ConfigureAwait(false);
        foreach (var tag in after) await SaveTagInTransactionAsync(connection, transaction, tag, cancellationToken).ConfigureAwait(false);
        var token = CreateUndoToken("Move tags to group");
        await WriteUndoJournalAsync(connection, transaction, token, "tag-batch-state-v2", new P3TagBatchStateChange(before.ToArray(), after), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(after.Length, token, []);
    }

    private async Task<AssetLibraryBatchResult> MergeTagsP3Async(Guid sourceTagId, Guid targetTagId, CancellationToken cancellationToken)
    {
        if (sourceTagId == targetTagId) throw new ArgumentException("源标签和目标标签必须不同。");
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var source = await ReadEffectiveActiveP3TagForMergeAsync(
            connection, transaction, sourceTagId, "源标签", cancellationToken).ConfigureAwait(false);
        var target = await ReadEffectiveActiveP3TagForMergeAsync(
            connection, transaction, targetTagId, "目标标签", cancellationToken).ConfigureAwait(false);
        var beforeMemberships = await ReadP3TagMergeMembershipsAsync(connection, transaction, sourceTagId, targetTagId, cancellationToken).ConfigureAwait(false);
        var beforeDocuments = await ReadP3QueryDocumentStatesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var afterDocuments = new List<P3QueryDocumentState>();
        foreach (var state in beforeDocuments)
        {
            var parsed = AssetQueryDocumentCodec.Parse(state.QueryJson);
            if (!parsed.IsValid || parsed.Document is null) throw new InvalidDataException(parsed.ErrorMessage);
            var replaced = ReplaceP3TagReference(parsed.Document, source, target);
            var canonical = AssetQueryDocumentCodec.SerializeCanonical(replaced);
            afterDocuments.Add(state with { QueryJson = canonical, QueryHash = AssetQueryDocumentCodec.ComputeHash(replaced), UpdatedAt = DateTimeOffset.UtcNow });
        }
        await ExecuteAsync(connection, transaction, """
            INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt)
            SELECT AssetId,$target,AddedAt FROM AssetTagMemberships WHERE TagId=$source;
            """, cancellationToken, ("$target", targetTagId.ToString("D")), ("$source", sourceTagId.ToString("D"))).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE TagId=$source;", cancellationToken, ("$source", sourceTagId.ToString("D"))).ConfigureAwait(false);
        var sourceAfter = source with { IsArchived = true };
        await SaveTagInTransactionAsync(connection, transaction, sourceAfter, cancellationToken).ConfigureAwait(false);
        await ApplyP3QueryDocumentStatesAsync(connection, transaction, afterDocuments, cancellationToken).ConfigureAwait(false);
        var afterMemberships = await ReadP3TagMergeMembershipsAsync(connection, transaction, sourceTagId, targetTagId, cancellationToken).ConfigureAwait(false);
        var before = new P3TagMergeState(source, target, beforeMemberships, beforeDocuments);
        var after = new P3TagMergeState(sourceAfter, target, afterMemberships, afterDocuments.ToArray());
        var token = CreateUndoToken("Merge tags");
        await WriteUndoJournalAsync(connection, transaction, token, "tag-merge-v2", new P3TagMergeChange(before, after), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(beforeMemberships.Count(row => row.TagId == sourceTagId), token, []);
    }

    public async Task<AssetLibraryBatchResult> MergeTagsAsync(IEnumerable<Guid> sourceTagIds, Guid targetTagId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var sourceIds = NormalizeIds(sourceTagIds).Where(id => id != targetTagId).ToArray();
        if (sourceIds.Length == 0) return new(0, null, []);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var target = await ReadEffectiveActiveP3TagForMergeAsync(
            connection, transaction, targetTagId, "目标标签", cancellationToken).ConfigureAwait(false);
        var sources = new List<AssetTag>();
        foreach (var id in sourceIds)
            sources.Add(await ReadEffectiveActiveP3TagForMergeAsync(
                connection, transaction, id, "源标签", cancellationToken).ConfigureAwait(false));
        var allIds = sourceIds.Append(targetTagId).ToArray();
        var beforeMemberships = await ReadP3TagMembershipsAsync(connection, transaction, allIds, cancellationToken).ConfigureAwait(false);
        var beforeDocuments = await ReadP3QueryDocumentStatesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var afterDocuments = new List<P3QueryDocumentState>();
        foreach (var state in beforeDocuments)
        {
            var parsed = AssetQueryDocumentCodec.Parse(state.QueryJson);
            if (!parsed.IsValid || parsed.Document is null) throw new InvalidDataException(parsed.ErrorMessage);
            var replaced = sources.Aggregate(parsed.Document, (current, source) => ReplaceP3TagReference(current, source, target));
            var canonical = AssetQueryDocumentCodec.SerializeCanonical(replaced);
            afterDocuments.Add(state with { QueryJson = canonical, QueryHash = AssetQueryDocumentCodec.ComputeHash(replaced), UpdatedAt = DateTimeOffset.UtcNow });
        }
        foreach (var source in sources)
        {
            await ExecuteAsync(connection, transaction, "INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt) SELECT AssetId,$target,AddedAt FROM AssetTagMemberships WHERE TagId=$source;", cancellationToken,
                ("$target", targetTagId.ToString("D")), ("$source", source.TagId.ToString("D"))).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE TagId=$source;", cancellationToken, ("$source", source.TagId.ToString("D"))).ConfigureAwait(false);
            await SaveTagInTransactionAsync(connection, transaction, source with { IsArchived = true }, cancellationToken).ConfigureAwait(false);
        }
        await ApplyP3QueryDocumentStatesAsync(connection, transaction, afterDocuments, cancellationToken).ConfigureAwait(false);
        var afterMemberships = await ReadP3TagMembershipsAsync(connection, transaction, allIds, cancellationToken).ConfigureAwait(false);
        var before = new P3MultiTagMergeState(sources.ToArray(), target, beforeMemberships, beforeDocuments);
        var after = new P3MultiTagMergeState(sources.Select(source => source with { IsArchived = true }).ToArray(), target, afterMemberships, afterDocuments.ToArray());
        var token = CreateUndoToken($"Merge {sources.Count} tags");
        await WriteUndoJournalAsync(connection, transaction, token, "tag-multi-merge-v2", new P3MultiTagMergeChange(before, after), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(beforeMemberships.Count(row => sourceIds.Contains(row.TagId)), token, []);
    }

    private static async Task<AssetTag> ReadEffectiveActiveP3TagForMergeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid tagId,
        string role,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT t.TagId,t.Name,t.TagGroupId,t.SortOrder,
                   (SELECT COUNT(*) FROM AssetTagMemberships m WHERE m.TagId=t.TagId),
                   t.CreatedAt,t.IsArchived,
                   CASE
                       WHEN t.TagGroupId IS NULL THEN 0
                       WHEN g.TagGroupId IS NULL THEN 1
                       ELSE g.IsArchived
                   END AS ParentGroupArchived
            FROM AssetTags t
            LEFT JOIN TagGroups g ON g.TagGroupId=t.TagGroupId
            WHERE t.TagId=$id;
            """;
        command.Parameters.AddWithValue("$id", tagId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new KeyNotFoundException($"{role} {tagId:D} 不存在。");
        if (reader.GetInt32(6) != 0 || reader.GetInt32(7) != 0)
            throw new InvalidOperationException($"{role}已归档或位于已归档标签组，不能参与合并。");
        return new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            reader.GetInt32(3),
            reader.GetInt32(4),
            DateTimeOffset.Parse(reader.GetString(5)),
            IsArchived: false);
    }

    private static AssetQueryDocument ReplaceP3TagReference(AssetQueryDocument document, AssetTag source, AssetTag target) =>
        document with { RootGroup = ReplaceP3TagReference(document.RootGroup, source, target) };

    private static AssetQueryNode ReplaceP3TagReference(AssetQueryNode node, AssetTag source, AssetTag target)
    {
        if (node.Kind == AssetQueryNodeKind.Group)
            return node with { Children = node.Children.Select(child => ReplaceP3TagReference(child, source, target)).ToArray() };
        if (node.Field != AssetQueryField.Tag) return node;
        var sourceId = "id:" + source.TagId.ToString("D");
        var sourceName = "name:" + source.Name.Normalize(System.Text.NormalizationForm.FormC);
        var targetId = "id:" + target.TagId.ToString("D");
        return node with
        {
            Values = node.Values.Select(value => string.Equals(value, sourceId, StringComparison.Ordinal) || string.Equals(value, sourceName, StringComparison.OrdinalIgnoreCase) ? targetId : value)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
    }

    private static async Task<AssetTagMembership[]> ReadP3TagMergeMembershipsAsync(SqliteConnection connection, SqliteTransaction transaction, Guid source, Guid target, CancellationToken cancellationToken)
    {
        var result = new List<AssetTagMembership>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT AssetId,TagId,AddedAt FROM AssetTagMemberships WHERE TagId=$source OR TagId=$target ORDER BY AssetId,TagId;";
        command.Parameters.AddWithValue("$source", source.ToString("D"));
        command.Parameters.AddWithValue("$target", target.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        return result.ToArray();
    }

    private static async Task<AssetTagMembership[]> ReadP3TagMembershipsAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Guid> tagIds, CancellationToken cancellationToken)
    {
        var result = new List<AssetTagMembership>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var parameters = new List<string>();
        for (var index = 0; index < tagIds.Count; index++)
        {
            var name = "$tag" + index;
            parameters.Add(name);
            command.Parameters.AddWithValue(name, tagIds[index].ToString("D"));
        }
        command.CommandText = $"SELECT AssetId,TagId,AddedAt FROM AssetTagMemberships WHERE TagId IN ({string.Join(',', parameters)}) ORDER BY AssetId,TagId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        return result.ToArray();
    }

    private static async Task<P3QueryDocumentState[]> ReadP3QueryDocumentStatesAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var result = new List<P3QueryDocumentState>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT SmartFolderId,DocumentVersion,QueryJson,QueryHash,LegacyRulesBackupJson,UpdatedAt FROM SmartFolderQueryDocuments ORDER BY SmartFolderId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture)));
        return result.ToArray();
    }

    private static async Task ApplyP3QueryDocumentStatesAsync(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<P3QueryDocumentState> states, CancellationToken cancellationToken)
    {
        foreach (var state in states)
            await ExecuteAsync(connection, transaction, "UPDATE SmartFolderQueryDocuments SET DocumentVersion=$version,QueryJson=$json,QueryHash=$hash,LegacyRulesBackupJson=$backup,UpdatedAt=$updated WHERE SmartFolderId=$id;", cancellationToken,
                ("$version", state.DocumentVersion), ("$json", state.QueryJson), ("$hash", state.QueryHash), ("$backup", (object?)state.LegacyRulesBackupJson ?? DBNull.Value), ("$updated", state.UpdatedAt.ToString("O")), ("$id", state.SmartFolderId.ToString("D"))).ConfigureAwait(false);
    }

    private static async Task ApplyP3TagMergeStateAsync(SqliteConnection connection, SqliteTransaction transaction, P3TagMergeState state, CancellationToken cancellationToken)
    {
        await SaveTagInTransactionAsync(connection, transaction, state.Source, cancellationToken).ConfigureAwait(false);
        await SaveTagInTransactionAsync(connection, transaction, state.Target, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE TagId=$source OR TagId=$target;", cancellationToken,
            ("$source", state.Source.TagId.ToString("D")), ("$target", state.Target.TagId.ToString("D"))).ConfigureAwait(false);
        foreach (var row in state.Memberships)
            await ExecuteAsync(connection, transaction, "INSERT INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);", cancellationToken,
                ("$asset", row.AssetId.ToString("D")), ("$tag", row.TagId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
        await ApplyP3QueryDocumentStatesAsync(connection, transaction, state.QueryDocuments, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyP3MultiTagMergeStateAsync(SqliteConnection connection, SqliteTransaction transaction, P3MultiTagMergeState state, CancellationToken cancellationToken)
    {
        foreach (var source in state.Sources) await SaveTagInTransactionAsync(connection, transaction, source, cancellationToken).ConfigureAwait(false);
        await SaveTagInTransactionAsync(connection, transaction, state.Target, cancellationToken).ConfigureAwait(false);
        foreach (var id in state.Sources.Select(source => source.TagId).Append(state.Target.TagId))
            await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE TagId=$id;", cancellationToken, ("$id", id.ToString("D"))).ConfigureAwait(false);
        foreach (var row in state.Memberships)
            await ExecuteAsync(connection, transaction, "INSERT INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);", cancellationToken,
                ("$asset", row.AssetId.ToString("D")), ("$tag", row.TagId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
        await ApplyP3QueryDocumentStatesAsync(connection, transaction, state.QueryDocuments, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssetLibraryBatchResult> SetTagGroupArchivedAsync(Guid tagGroupId, bool isArchived, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var before = await ReadP3TagGroupStateAsync(connection, transaction, tagGroupId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("标签组不存在。");
        if (before.Group.IsArchived == isArchived)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new(0, null, []);
        }
        var after = new P3TagGroupState(before.Group with { IsArchived = isArchived });
        if (isArchived)
            await ResolveP3NameReferencesInAllDocumentsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await ApplyP3TagGroupStateAsync(connection, transaction, after, cancellationToken).ConfigureAwait(false);
        var token = CreateUndoToken(isArchived ? "Archive tag group" : "Restore tag group");
        await WriteUndoJournalAsync(connection, transaction, token, "tag-group-state-v2", new P3TagGroupStateChange(before, after), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(1, token, []);
    }

    private static async Task EnsureTagSaveAllowedAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AssetTag tag,
        CancellationToken cancellationToken)
    {
        if (tag.TagGroupId is null) return;
        await using var group = connection.CreateCommand();
        group.Transaction = transaction;
        group.CommandText = "SELECT IsArchived FROM TagGroups WHERE TagGroupId=$id;";
        group.Parameters.AddWithValue("$id", tag.TagGroupId.Value.ToString("D"));
        var groupState = await group.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (groupState is null or DBNull) throw new KeyNotFoundException("标签组不存在。");
        if (Convert.ToInt32(groupState, CultureInfo.InvariantCulture) == 0 || tag.IsArchived) return;

        await using var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = "SELECT TagGroupId,IsArchived FROM AssetTags WHERE TagId=$id;";
        existing.Parameters.AddWithValue("$id", tag.TagId.ToString("D"));
        await using var reader = await existing.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var isExistingActiveInSameGroup = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            && !reader.IsDBNull(0)
            && Guid.Parse(reader.GetString(0)) == tag.TagGroupId.Value
            && reader.GetInt32(1) == 0;
        if (!isExistingActiveInSameGroup)
            throw new InvalidOperationException("不能在已归档标签组内创建或恢复可用标签。");
    }

    public Task<AssetLibraryBatchResult> ReorderTagGroupsAsync(IEnumerable<Guid> orderedTagGroupIds, CancellationToken cancellationToken = default) =>
        ReorderP3EntitiesAsync("TagGroups", "TagGroupId", null, orderedTagGroupIds, "tag-group-order-v2", "Reorder tag groups", cancellationToken);

    public Task<AssetLibraryBatchResult> ReorderTagsAsync(Guid? tagGroupId, IEnumerable<Guid> orderedTagIds, CancellationToken cancellationToken = default) =>
        ReorderP3EntitiesAsync("AssetTags", "TagId", tagGroupId, orderedTagIds, "tag-order-v2", "Reorder tags", cancellationToken);

    private async Task<AssetLibraryBatchResult> ReorderP3EntitiesAsync(
        string table,
        string idColumn,
        Guid? groupId,
        IEnumerable<Guid> orderedIds,
        string kind,
        string description,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var ids = NormalizeIds(orderedIds);
        if (ids.Length == 0) return new(0, null, []);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var before = await ReadP3OrderAsync(connection, transaction, table, idColumn, groupId, cancellationToken).ConfigureAwait(false);
        if (before.Select(item => item.Id).OrderBy(id => id).SequenceEqual(ids.OrderBy(id => id)) is false)
            throw new InvalidOperationException("排序列表必须完整且不能包含其他范围的项目。");
        var after = ids.Select((id, index) => new P3OrderState(id, index)).ToArray();
        await ApplyP3OrderAsync(connection, transaction, table, idColumn, after, cancellationToken).ConfigureAwait(false);
        var token = CreateUndoToken(description);
        await WriteUndoJournalAsync(connection, transaction, token, kind, new P3OrderChange(table, idColumn, before, after), cancellationToken, journalVersion: 2).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(ids.Length, token, []);
    }

    private static Guid[] NormalizeBatchAssetIds(IEnumerable<Guid>? values)
    {
        var ids = NormalizeIds(values);
        if (ids.Length > 10_000)
            throw new ArgumentOutOfRangeException(nameof(values), "单次批量元数据操作最多允许 10,000 个素材标识。");
        return ids;
    }
    private static Guid[] NormalizeIds(IEnumerable<Guid>? values) => (values ?? []).Where(id => id != Guid.Empty).Distinct().ToArray();

    private static async Task CreateP3SelectionTableAsync(SqliteConnection connection, SqliteTransaction? transaction, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        await using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = "CREATE TEMP TABLE IF NOT EXISTS P3SelectedAssets(AssetId TEXT NOT NULL PRIMARY KEY); DELETE FROM P3SelectedAssets;";
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        foreach (var id in ids)
            await ExecuteP3Async(connection, transaction, "INSERT INTO P3SelectedAssets(AssetId) VALUES($id);", cancellationToken, ("$id", id.ToString("D"))).ConfigureAwait(false);
    }

    private static async Task ValidateActiveIdsAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string idColumn, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        foreach (var id in ids.Distinct())
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = table == "AssetTags"
                ? "SELECT COUNT(*) FROM AssetTags t LEFT JOIN TagGroups g ON g.TagGroupId=t.TagGroupId WHERE t.TagId=$id AND t.IsArchived=0 AND (t.TagGroupId IS NULL OR g.IsArchived=0);"
                : $"SELECT COUNT(*) FROM {table} WHERE {idColumn}=$id AND IsArchived=0;";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) != 1)
                throw new KeyNotFoundException($"目标 {id} 不存在或已归档。");
        }
    }

    private static async Task<int> CountP3SelectionRelationshipsAsync(SqliteConnection connection, SqliteTransaction? transaction, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM {table} m JOIN P3SelectedAssets s ON s.AssetId=m.AssetId;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task<List<P3AssetState>> ReadP3AssetStatesAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        var result = new List<P3AssetState>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT a.AssetId,a.Rating,a.Comment,a.IsArchived,a.IsMissing FROM AssetItems a JOIN P3SelectedAssets s ON s.AssetId=a.AssetId ORDER BY a.AssetId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3) != 0, reader.GetInt32(4) != 0));
        return result;
    }

    private static async Task<P3BatchSnapshot> ReadP3BatchSnapshotAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var assets = await ReadP3AssetStatesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var tags = new List<AssetTagMembership>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT m.AssetId,m.TagId,m.AddedAt FROM AssetTagMemberships m JOIN P3SelectedAssets s ON s.AssetId=m.AssetId ORDER BY m.AssetId,m.TagId;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) tags.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        }
        var folders = new List<AssetFolderMembership>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT m.AssetId,m.FolderId,m.AddedAt FROM AssetFolderMemberships m JOIN P3SelectedAssets s ON s.AssetId=m.AssetId ORDER BY m.AssetId,m.FolderId;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) folders.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        }
        return new(assets.ToArray(), tags.ToArray(), folders.ToArray());
    }

    private static async Task ApplyP3MembershipDeltaAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string idColumn, IReadOnlyList<Guid> addIds, IReadOnlyList<Guid> removeIds, CancellationToken cancellationToken)
    {
        foreach (var id in removeIds)
            await ExecuteAsync(connection, transaction, $"DELETE FROM {table} WHERE {idColumn}=$target AND AssetId IN (SELECT AssetId FROM P3SelectedAssets);", cancellationToken, ("$target", id.ToString("D"))).ConfigureAwait(false);
        foreach (var id in addIds)
            await ExecuteAsync(connection, transaction, $"INSERT OR IGNORE INTO {table}(AssetId,{idColumn},AddedAt) SELECT AssetId,$target,$at FROM P3SelectedAssets;", cancellationToken, ("$target", id.ToString("D")), ("$at", DateTimeOffset.UtcNow.ToString("O"))).ConfigureAwait(false);
    }

    private static async Task ApplyP3FolderAutoTagsAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Guid> folderIds, CancellationToken cancellationToken)
    {
        foreach (var folderId in folderIds)
            await ExecuteAsync(connection, transaction, """
                INSERT OR IGNORE INTO AssetTagMemberships(AssetId,TagId,AddedAt)
                SELECT s.AssetId,ft.TagId,$at FROM P3SelectedAssets s JOIN AssetFolderAutoTags ft ON ft.FolderId=$folder;
                """, cancellationToken, ("$folder", folderId.ToString("D")), ("$at", DateTimeOffset.UtcNow.ToString("O"))).ConfigureAwait(false);
    }

    private static bool P3SnapshotsEqual(P3BatchSnapshot left, P3BatchSnapshot right) =>
        JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

    private static int CountChangedP3Assets(P3BatchSnapshot before, P3BatchSnapshot after)
    {
        static Dictionary<Guid, string> MembershipKeys(P3BatchSnapshot snapshot)
        {
            var tags = snapshot.Tags
                .GroupBy(value => value.AssetId)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join(',', group.Select(value => value.TagId).OrderBy(value => value)));
            var folders = snapshot.Folders
                .GroupBy(value => value.AssetId)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join(',', group.Select(value => value.FolderId).OrderBy(value => value)));
            return snapshot.Assets.ToDictionary(
                asset => asset.AssetId,
                asset => tags.GetValueOrDefault(asset.AssetId, string.Empty) + "|" +
                    folders.GetValueOrDefault(asset.AssetId, string.Empty));
        }

        var afterAssets = after.Assets.ToDictionary(asset => asset.AssetId);
        var beforeMemberships = MembershipKeys(before);
        var afterMemberships = MembershipKeys(after);
        return before.Assets.Count(asset =>
            !afterAssets.TryGetValue(asset.AssetId, out var afterAsset) ||
            asset != afterAsset ||
            !string.Equals(beforeMemberships[asset.AssetId], afterMemberships.GetValueOrDefault(asset.AssetId), StringComparison.Ordinal));
    }

    private async Task<AssetTag?> ReadP3TagAsync(Guid tagId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TagId,Name,TagGroupId,SortOrder,(SELECT COUNT(*) FROM AssetTagMemberships m WHERE m.TagId=t.TagId),CreatedAt,IsArchived FROM AssetTags t WHERE TagId=$id;";
        command.Parameters.AddWithValue("$id", tagId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)), reader.GetInt32(3), reader.GetInt32(4), DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture), reader.GetInt32(6) != 0)
            : null;
    }

    private static async Task<P3TagGroupState?> ReadP3TagGroupStateAsync(SqliteConnection connection, SqliteTransaction transaction, Guid groupId, CancellationToken cancellationToken)
    {
        TagGroup? group;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT TagGroupId,Name,SortOrder,CreatedAt,IsArchived FROM TagGroups WHERE TagGroupId=$id;";
            command.Parameters.AddWithValue("$id", groupId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            group = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetInt32(2), DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture), reader.GetInt32(4) != 0)
                : null;
        }
        if (group is null) return null;
        return new(group);
    }

    private static async Task ApplyP3TagGroupStateAsync(SqliteConnection connection, SqliteTransaction transaction, P3TagGroupState state, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, "UPDATE TagGroups SET Name=$name,SortOrder=$sort,IsArchived=$archived WHERE TagGroupId=$id;", cancellationToken,
            ("$name", state.Group.Name), ("$sort", state.Group.SortOrder), ("$archived", state.Group.IsArchived ? 1 : 0), ("$id", state.Group.TagGroupId.ToString("D"))).ConfigureAwait(false);
    }

    private static async Task<P3OrderState[]> ReadP3OrderAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string idColumn, Guid? groupId, CancellationToken cancellationToken)
    {
        var result = new List<P3OrderState>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (table == "AssetTags")
        {
            command.CommandText = $"SELECT {idColumn},SortOrder FROM {table} WHERE (($group IS NULL AND TagGroupId IS NULL) OR TagGroupId=$group) ORDER BY SortOrder,{idColumn};";
            command.Parameters.AddWithValue("$group", (object?)groupId?.ToString("D") ?? DBNull.Value);
        }
        else command.CommandText = $"SELECT {idColumn},SortOrder FROM {table} ORDER BY SortOrder,{idColumn};";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetInt32(1)));
        return result.ToArray();
    }

    private static async Task ApplyP3OrderAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string idColumn, IEnumerable<P3OrderState> states, CancellationToken cancellationToken)
    {
        foreach (var state in states)
            await ExecuteAsync(connection, transaction, $"UPDATE {table} SET SortOrder=$sort WHERE {idColumn}=$id;", cancellationToken, ("$sort", state.SortOrder), ("$id", state.Id.ToString("D"))).ConfigureAwait(false);
    }

    private static async Task ExecuteP3Async(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TryApplyP3UndoInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, PersistedUndo operation, CancellationToken cancellationToken)
    {
        if (operation.JournalVersion < 2) return false;
        switch (operation.Kind)
        {
            case "asset-batch-metadata-v2": await ApplyP3BatchSnapshotAsync(connection, transaction, Deserialize<P3BatchMetadataChange>(operation.PayloadJson).Before, cancellationToken).ConfigureAwait(false); return true;
            case "tag-state-v2": await RestoreTagJournalStateInTransactionAsync(connection, transaction, Deserialize<P3TagStateChange>(operation.PayloadJson).Before, cancellationToken).ConfigureAwait(false); return true;
            case "tag-batch-state-v2": foreach (var tag in Deserialize<P3TagBatchStateChange>(operation.PayloadJson).Before) await RestoreTagJournalStateInTransactionAsync(connection, transaction, tag, cancellationToken).ConfigureAwait(false); return true;
            case "tag-merge-v2": await ApplyP3TagMergeStateAsync(connection, transaction, Deserialize<P3TagMergeChange>(operation.PayloadJson).Before, cancellationToken).ConfigureAwait(false); return true;
            case "tag-multi-merge-v2": await ApplyP3MultiTagMergeStateAsync(connection, transaction, Deserialize<P3MultiTagMergeChange>(operation.PayloadJson).Before, cancellationToken).ConfigureAwait(false); return true;
            case "tag-group-state-v2": await ApplyP3TagGroupStateAsync(connection, transaction, Deserialize<P3TagGroupStateChange>(operation.PayloadJson).Before, cancellationToken).ConfigureAwait(false); return true;
            case "smart-folder-state-v2": await ApplySmartFolderStateAsync(connection, transaction, Deserialize<P3SmartFolderStateChange>(operation.PayloadJson).Before, cancellationToken).ConfigureAwait(false); return true;
            case "tag-group-order-v2" or "tag-order-v2":
            {
                var change = Deserialize<P3OrderChange>(operation.PayloadJson);
                await ApplyP3OrderAsync(connection, transaction, change.Table, change.IdColumn, change.Before, cancellationToken).ConfigureAwait(false);
                return true;
            }
            default: return false;
        }
    }

    private static async Task<bool> TryApplyP3RedoInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, PersistedUndo operation, CancellationToken cancellationToken)
    {
        if (operation.JournalVersion < 2) return false;
        switch (operation.Kind)
        {
            case "asset-batch-metadata-v2": await ApplyP3BatchSnapshotAsync(connection, transaction, Deserialize<P3BatchMetadataChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false); return true;
            case "tag-state-v2": await RestoreTagJournalStateInTransactionAsync(connection, transaction, Deserialize<P3TagStateChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false); return true;
            case "tag-batch-state-v2": foreach (var tag in Deserialize<P3TagBatchStateChange>(operation.PayloadJson).After) await RestoreTagJournalStateInTransactionAsync(connection, transaction, tag, cancellationToken).ConfigureAwait(false); return true;
            case "tag-merge-v2": await ApplyP3TagMergeStateAsync(connection, transaction, Deserialize<P3TagMergeChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false); return true;
            case "tag-multi-merge-v2": await ApplyP3MultiTagMergeStateAsync(connection, transaction, Deserialize<P3MultiTagMergeChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false); return true;
            case "tag-group-state-v2": await ApplyP3TagGroupStateAsync(connection, transaction, Deserialize<P3TagGroupStateChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false); return true;
            case "smart-folder-state-v2": await ApplySmartFolderStateAsync(connection, transaction, Deserialize<P3SmartFolderStateChange>(operation.PayloadJson).After, cancellationToken).ConfigureAwait(false); return true;
            case "tag-group-order-v2" or "tag-order-v2":
            {
                var change = Deserialize<P3OrderChange>(operation.PayloadJson);
                await ApplyP3OrderAsync(connection, transaction, change.Table, change.IdColumn, change.After, cancellationToken).ConfigureAwait(false);
                return true;
            }
            default: return false;
        }
    }

    private static async Task ApplyP3BatchSnapshotAsync(SqliteConnection connection, SqliteTransaction transaction, P3BatchSnapshot snapshot, CancellationToken cancellationToken)
    {
        foreach (var asset in snapshot.Assets)
            await ExecuteAsync(connection, transaction, "UPDATE AssetItems SET Rating=$rating,Comment=$comment,IsArchived=$archived,IsMissing=$missing WHERE AssetId=$id;", cancellationToken,
                ("$rating", asset.Rating), ("$comment", asset.Comment), ("$archived", asset.IsArchived ? 1 : 0), ("$missing", asset.IsMissing ? 1 : 0), ("$id", asset.AssetId.ToString("D"))).ConfigureAwait(false);
        foreach (var assetId in snapshot.Assets.Select(asset => asset.AssetId))
        {
            await ExecuteAsync(connection, transaction, "DELETE FROM AssetTagMemberships WHERE AssetId=$id;", cancellationToken, ("$id", assetId.ToString("D"))).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, "DELETE FROM AssetFolderMemberships WHERE AssetId=$id;", cancellationToken, ("$id", assetId.ToString("D"))).ConfigureAwait(false);
        }
        foreach (var row in snapshot.Tags)
            await ExecuteAsync(connection, transaction, "INSERT INTO AssetTagMemberships(AssetId,TagId,AddedAt) VALUES($asset,$tag,$at);", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$tag", row.TagId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
        foreach (var row in snapshot.Folders)
            await ExecuteAsync(connection, transaction, "INSERT INTO AssetFolderMemberships(AssetId,FolderId,AddedAt) VALUES($asset,$folder,$at);", cancellationToken, ("$asset", row.AssetId.ToString("D")), ("$folder", row.FolderId.ToString("D")), ("$at", row.AddedAt.ToString("O"))).ConfigureAwait(false);
    }

    private sealed record P3AssetState(Guid AssetId, int Rating, string Comment, bool IsArchived, bool IsMissing);
    private sealed record P3BatchSnapshot(P3AssetState[] Assets, AssetTagMembership[] Tags, AssetFolderMembership[] Folders);
    private sealed record P3BatchSimulation(P3BatchSnapshot Before, P3BatchSnapshot After);
    private sealed record P3BatchMetadataChange(P3BatchSnapshot Before, P3BatchSnapshot After);
    private sealed record P3CanonicalBatchRequest(
        Guid[] AssetIds,
        Guid[] AddTagIds,
        Guid[] RemoveTagIds,
        Guid[] AddFolderIds,
        Guid[] RemoveFolderIds,
        int? Rating,
        bool ClearRating,
        string? Comment,
        bool ClearComment,
        bool? IsArchived,
        bool? IsMissing);
    private sealed record P3TagStateChange(AssetTag Before, AssetTag After);
    private sealed record P3TagBatchStateChange(AssetTag[] Before, AssetTag[] After);
    private sealed record P3QueryDocumentState(Guid SmartFolderId, int DocumentVersion, string QueryJson, string QueryHash, string? LegacyRulesBackupJson, DateTimeOffset UpdatedAt);
    private sealed record P3TagMergeState(AssetTag Source, AssetTag Target, AssetTagMembership[] Memberships, P3QueryDocumentState[] QueryDocuments);
    private sealed record P3TagMergeChange(P3TagMergeState Before, P3TagMergeState After);
    private sealed record P3MultiTagMergeState(AssetTag[] Sources, AssetTag Target, AssetTagMembership[] Memberships, P3QueryDocumentState[] QueryDocuments);
    private sealed record P3MultiTagMergeChange(P3MultiTagMergeState Before, P3MultiTagMergeState After);
    private sealed record P3TagGroupState(TagGroup Group);
    private sealed record P3TagGroupStateChange(P3TagGroupState Before, P3TagGroupState After);
    private sealed record P3OrderState(Guid Id, int SortOrder);
    private sealed record P3OrderChange(string Table, string IdColumn, P3OrderState[] Before, P3OrderState[] After);

    private sealed record P3SmartFolderStateChange(SmartFolder Before, SmartFolder After);
}
