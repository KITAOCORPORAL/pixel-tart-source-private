using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

internal static class AssetQueryReferenceIntegrity
{
    public static async Task<AssetQueryDocument> ResolveActiveNameReferencesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AssetQueryDocument document,
        CancellationToken cancellationToken)
        => await ResolveNameReferencesAsync(
            connection, transaction, document, includeArchived: false, cancellationToken).ConfigureAwait(false);

    public static async Task<AssetQueryDocument> ResolveLegacyNameReferencesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AssetQueryDocument document,
        CancellationToken cancellationToken)
        => await ResolveNameReferencesAsync(
            connection, transaction, document, includeArchived: true, cancellationToken).ConfigureAwait(false);

    private static async Task<AssetQueryDocument> ResolveNameReferencesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AssetQueryDocument document,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        if (!ContainsNameReference(document.RootGroup)) return document;
        var folders = await ReadEntitiesAsync(connection, transaction, isTag: false, includeArchived, cancellationToken).ConfigureAwait(false);
        var tags = await ReadEntitiesAsync(connection, transaction, isTag: true, includeArchived, cancellationToken).ConfigureAwait(false);
        return document with
        {
            RootGroup = ResolveNode(document.RootGroup, "$.rootGroup", folders, tags)
        };
    }

    private static bool ContainsNameReference(AssetQueryNode node) =>
        node.Enabled && (node.Kind == AssetQueryNodeKind.Rule
            ? node.Field is AssetQueryField.Folder or AssetQueryField.Tag && node.Values.Any(value => value.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            : node.Children.Any(ContainsNameReference));

    private static AssetQueryNode ResolveNode(
        AssetQueryNode node,
        string path,
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> folders,
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> tags)
    {
        if (!node.Enabled) return node;
        if (node.Kind == AssetQueryNodeKind.Group)
        {
            return node with
            {
                Children = node.Children.Select((child, index) =>
                    ResolveNode(child, $"{path}.children[{index}]", folders, tags)).ToArray()
            };
        }
        if (node.Field is not (AssetQueryField.Folder or AssetQueryField.Tag)) return node;

        var indexByName = node.Field == AssetQueryField.Tag ? tags : folders;
        var values = new string[node.Values.Count];
        for (var index = 0; index < node.Values.Count; index++)
        {
            var value = node.Values[index];
            if (!value.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                values[index] = value;
                continue;
            }

            var displayName = NormalizeName(value[5..]);
            var key = NormalizeKey(displayName);
            if (!indexByName.TryGetValue(key, out var matches) || matches.Count == 0)
                throw new InvalidDataException($"{path}.values[{index}] 引用“{displayName}”不存在或已归档。");
            if (matches.Count != 1)
                throw new InvalidDataException($"{path}.values[{index}] 引用“{displayName}”不唯一，无法迁移为稳定标识。");
            values[index] = "id:" + matches[0].ToString("D");
        }
        return node with { Values = values };
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> ReadEntitiesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        bool isTag,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = (isTag, includeArchived) switch
        {
            (true, false) => """
                SELECT t.TagId,t.Name
                FROM AssetTags t LEFT JOIN TagGroups g ON g.TagGroupId=t.TagGroupId
                WHERE t.IsArchived=0 AND (t.TagGroupId IS NULL OR g.IsArchived=0)
                ORDER BY t.TagId;
                """,
            (true, true) => "SELECT TagId,Name FROM AssetTags ORDER BY TagId;",
            (false, false) => "SELECT FolderId,Name FROM AssetFolders WHERE IsArchived=0 ORDER BY FolderId;",
            _ => "SELECT FolderId,Name FROM AssetFolders ORDER BY FolderId;"
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = Guid.Parse(reader.GetString(0));
            var key = NormalizeKey(reader.GetString(1));
            if (!values.TryGetValue(key, out var matches)) values[key] = matches = [];
            matches.Add(id);
        }
        return values.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<Guid>)pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static string NormalizeName(string value)
    {
        var normalized = value.Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (normalized.Length == 0) throw new InvalidDataException("名称引用不能为空。");
        return normalized;
    }

    private static string NormalizeKey(string value) => NormalizeName(value).ToUpperInvariant();
}
