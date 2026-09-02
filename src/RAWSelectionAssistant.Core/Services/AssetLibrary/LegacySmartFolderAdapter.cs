using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

internal static class LegacySmartFolderAdapter
{
    private static readonly byte[] ProjectionMarker = [0x50, 0x33, 0x4c, 0x47];

    public static async Task<IReadOnlyList<SmartFolderRule>> ProjectAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid smartFolderId,
        AssetQueryDocument document,
        string queryHash,
        CancellationToken cancellationToken)
    {
        EnsureDocumentShape(document);
        var result = new List<SmartFolderRule>();
        for (var index = 0; index < document.RootGroup.Children.Count; index++)
        {
            var child = document.RootGroup.Children[index];
            var path = $"$.rootGroup.children[{index}]";
            if (child.Kind == AssetQueryNodeKind.Rule)
            {
                result.Add(await ProjectRuleAsync(
                    connection, transaction, smartFolderId, child, queryHash, path, result.Count,
                    groupId: null, SmartFolderLogic.And, cancellationToken).ConfigureAwait(false));
                continue;
            }

            if (!child.Enabled || child.Negated || child.Children.Any(grandchild => grandchild.Kind != AssetQueryNodeKind.Rule))
                throw new InvalidDataException("P3 查询包含旧智能文件夹接口无法表示的嵌套或取反规则组。");
            var groupId = CreateProjectionId(queryHash, path + ":group");
            var groupLogic = child.Logic == AssetQueryLogic.Any ? SmartFolderLogic.Or : SmartFolderLogic.And;
            for (var nestedIndex = 0; nestedIndex < child.Children.Count; nestedIndex++)
            {
                result.Add(await ProjectRuleAsync(
                    connection, transaction, smartFolderId, child.Children[nestedIndex], queryHash,
                    $"{path}.children[{nestedIndex}]", result.Count, groupId, groupLogic, cancellationToken).ConfigureAwait(false));
            }
        }
        return result;
    }

    public static void RejectStaleProjection(
        IReadOnlyList<SmartFolderRule> incoming,
        IReadOnlyList<SmartFolderRule> current)
    {
        var currentRuleIds = current.Select(rule => rule.RuleId).ToHashSet();
        var currentGroupIds = current.Where(rule => rule.GroupId is not null).Select(rule => rule.GroupId!.Value).ToHashSet();
        if (incoming.Any(rule => IsProjectionId(rule.RuleId) && !currentRuleIds.Contains(rule.RuleId)) ||
            incoming.Any(rule => rule.GroupId is { } groupId && IsProjectionId(groupId) && !currentGroupIds.Contains(groupId)))
            throw new InvalidOperationException("旧智能文件夹编辑快照已过期，已拒绝覆盖当前 P3 查询文档。");
    }

    public static bool IsUnchangedProjection(
        IReadOnlyList<SmartFolderRule> incoming,
        IReadOnlyList<SmartFolderRule> current)
    {
        if (incoming.Count != current.Count) return false;
        var left = incoming.OrderBy(rule => rule.SortOrder).ThenBy(rule => rule.RuleId).ToArray();
        var right = current.OrderBy(rule => rule.SortOrder).ThenBy(rule => rule.RuleId).ToArray();
        return left.SequenceEqual(right);
    }

    private static void EnsureDocumentShape(AssetQueryDocument document)
    {
        if (document.Scope != AssetQueryScope.AllAssets ||
            document.Text.Length != 0 ||
            document.SearchClauses is { Count: > 0 } ||
            document.SortField != AssetLibrarySortField.AddedAt ||
            document.SortDirection != AssetLibrarySortDirection.Descending ||
            document.IncludeArchived ||
            document.RootGroup.Kind != AssetQueryNodeKind.Group ||
            !document.RootGroup.Enabled ||
            document.RootGroup.Negated)
            throw new InvalidDataException("P3 查询包含旧智能文件夹接口无法表示的范围、搜索、排序、归档或根组设置。");
    }

    private static async Task<SmartFolderRule> ProjectRuleAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid smartFolderId,
        AssetQueryNode rule,
        string queryHash,
        string path,
        int sortOrder,
        Guid? groupId,
        SmartFolderLogic groupLogic,
        CancellationToken cancellationToken)
    {
        if (!rule.Enabled || rule.Locked || rule.Field is null || rule.Operator is null ||
            rule.CaseSensitivity != AssetQueryCaseSensitivity.Insensitive ||
            !Enum.TryParse<SmartFolderField>(rule.Field.Value.ToString(), out var field))
            throw new InvalidDataException($"{path} 包含旧智能文件夹接口无法表示的规则设置或字段。");

        var op = rule.Operator.Value switch
        {
            AssetQueryOperator.Contains => SmartFolderOperator.Contains,
            AssetQueryOperator.Equals => SmartFolderOperator.Equals,
            AssetQueryOperator.NotEquals => SmartFolderOperator.NotEquals,
            AssetQueryOperator.StartsWith => SmartFolderOperator.StartsWith,
            AssetQueryOperator.EndsWith => SmartFolderOperator.EndsWith,
            AssetQueryOperator.GreaterThan => SmartFolderOperator.GreaterThan,
            AssetQueryOperator.GreaterThanOrEqual => SmartFolderOperator.GreaterThanOrEqual,
            AssetQueryOperator.LessThan => SmartFolderOperator.LessThan,
            AssetQueryOperator.LessThanOrEqual => SmartFolderOperator.LessThanOrEqual,
            AssetQueryOperator.Regex => SmartFolderOperator.Regex,
            AssetQueryOperator.IsTrue => SmartFolderOperator.IsTrue,
            AssetQueryOperator.IsFalse => SmartFolderOperator.IsFalse,
            AssetQueryOperator.Between => SmartFolderOperator.InRange,
            AssetQueryOperator.AnyOf when rule.Field is AssetQueryField.Folder or AssetQueryField.Tag && rule.Values.Count == 1 => SmartFolderOperator.Equals,
            AssetQueryOperator.NoneOf when rule.Field is AssetQueryField.Folder or AssetQueryField.Tag && rule.Values.Count == 1 => SmartFolderOperator.NotEquals,
            _ => throw new InvalidDataException($"{path} 使用旧智能文件夹接口无法表示的操作符 {rule.Operator}。")
        };

        string value;
        if (rule.Field is AssetQueryField.Folder or AssetQueryField.Tag)
        {
            value = await ResolveReferenceDisplayNameAsync(
                connection, transaction, rule.Field.Value, rule.Values.Single(), path, cancellationToken).ConfigureAwait(false);
        }
        else if (rule.Operator == AssetQueryOperator.Between)
        {
            value = string.Join("..", rule.Values);
        }
        else
        {
            value = rule.Values.Count == 0 ? string.Empty : rule.Values.Single();
        }

        return new(
            CreateProjectionId(queryHash, path + ":rule"),
            smartFolderId,
            field,
            op,
            value,
            rule.Negated,
            sortOrder,
            groupId,
            groupLogic);
    }

    private static async Task<string> ResolveReferenceDisplayNameAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        AssetQueryField field,
        string reference,
        string path,
        CancellationToken cancellationToken)
    {
        var isTag = field == AssetQueryField.Tag;
        var rows = new List<(Guid Id, string Name)>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = isTag
            ? """
                SELECT t.TagId,t.Name
                FROM AssetTags t LEFT JOIN TagGroups g ON g.TagGroupId=t.TagGroupId
                WHERE t.IsArchived=0 AND (t.TagGroupId IS NULL OR g.IsArchived=0)
                ORDER BY t.TagId;
                """
            : "SELECT FolderId,Name FROM AssetFolders WHERE IsArchived=0 ORDER BY FolderId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add((Guid.Parse(reader.GetString(0)), reader.GetString(1)));

        IReadOnlyList<(Guid Id, string Name)> matches;
        if (reference.StartsWith("id:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(reference[3..], out var id))
            matches = rows.Where(row => row.Id == id).ToArray();
        else if (reference.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
        {
            var key = NormalizeKey(reference[5..]);
            matches = rows.Where(row => NormalizeKey(row.Name) == key).ToArray();
        }
        else throw new InvalidDataException($"{path} 包含无效引用格式。");

        if (matches.Count != 1)
            throw new InvalidDataException($"{path} 的引用不存在、已归档或不唯一，无法投影到旧智能文件夹接口。");
        return matches[0].Name;
    }

    private static Guid CreateProjectionId(string queryHash, string path)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(queryHash + "|" + path));
        var bytes = digest[..16];
        ProjectionMarker.CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static bool IsProjectionId(Guid id)
    {
        var bytes = id.ToByteArray();
        return bytes.AsSpan(0, ProjectionMarker.Length).SequenceEqual(ProjectionMarker);
    }

    private static string NormalizeKey(string value) =>
        value.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
}
