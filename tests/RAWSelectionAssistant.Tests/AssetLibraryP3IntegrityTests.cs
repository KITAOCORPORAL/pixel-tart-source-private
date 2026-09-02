using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3IntegrityTests
{
    [TestMethod]
    public async Task QueryReferenceResolutionMapsActiveNamesToStableIdsAcrossNestedGroups()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "人像"));
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "精选"));
        var document = new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Group(AssetQueryLogic.Any,
                [
                    AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, ["name:人像"]),
                    AssetQueryNode.Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, ["name:精选"])
                ]),
                AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, ["3"])
            ])
        };

        var resolved = await setup.Repository.ResolveQueryReferencesAsync(document);
        var rules = Flatten(resolved.RootGroup).Where(node => node.Kind == AssetQueryNodeKind.Rule).ToArray();

        CollectionAssert.AreEqual(new[] { $"id:{folder.FolderId:D}" }, rules[0].Values.ToArray());
        CollectionAssert.AreEqual(new[] { $"id:{tag.TagId:D}" }, rules[1].Values.ToArray());
        CollectionAssert.AreEqual(new[] { "3" }, rules[2].Values.ToArray());
        Assert.IsTrue(AssetQueryDocumentCodec.Normalize(resolved).IsValid);
    }

    [TestMethod]
    public async Task QueryReferenceResolutionRejectsMissingNameWithoutDefaultingToAllAssets()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var document = Document(AssetQueryNode.Rule(
            AssetQueryField.Folder, AssetQueryOperator.AnyOf, ["name:不存在的文件夹"]));

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.Repository.ResolveQueryReferencesAsync(document));

        StringAssert.Contains(failure.Message, "不存在的文件夹");
        var page = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 20) { Document = document });
        Assert.IsEmpty(page.Items);
        Assert.IsFalse(string.IsNullOrWhiteSpace(page.RegexError));
    }

    [TestMethod]
    public async Task QueryReferenceResolutionKeepsStableIdsAndSupportsLegacyNameCompatibility()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "兼容标签"));
        var document = Document(AssetQueryNode.Rule(
            AssetQueryField.Tag, AssetQueryOperator.AnyOf, [$"id:{tag.TagId:D}", "name:兼容标签"]));

        var resolved = await setup.Repository.ResolveQueryReferencesAsync(document);
        CollectionAssert.AreEqual(
            new[] { $"id:{tag.TagId:D}" },
            resolved.RootGroup.Children.Single().Values.ToArray());
    }

    [TestMethod]
    public async Task TagGroupArchivePreservesIndividualTagStateAndActiveVisibilityContract()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var group = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "人物", 0));
        var active = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "可用", group.TagGroupId));
        var individuallyArchived = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "单独归档", group.TagGroupId, IsArchived: true));
        await setup.Repository.AddTagsAsync([setup.A], [active.TagId]);

        var archived = await setup.Repository.SetTagGroupArchivedAsync(group.TagGroupId, true);
        Assert.AreEqual(1, archived.ChangedCount, "Only the group archive bit is changed.");
        Assert.IsNotNull(archived.UndoToken);
        Assert.IsEmpty(await setup.Repository.ListTagsAsync(group.TagGroupId));
        Assert.IsEmpty(await setup.Repository.SearchTagsAsync("可用"));
        Assert.IsEmpty((await setup.Repository.QueryAsync(new(TagId: active.TagId, PageSize: 20))).Items);

        var hidden = await setup.Repository.ListTagsAsync(group.TagGroupId, includeArchived: true);
        Assert.IsFalse(hidden.Single(tag => tag.TagId == active.TagId).IsArchived, "Group archive must not rewrite the child's own bit.");
        Assert.IsTrue(hidden.Single(tag => tag.TagId == individuallyArchived.TagId).IsArchived);

        var reference = new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, [$"id:{active.TagId:D}"])
            ])
        };
        Assert.IsNotEmpty(await setup.Repository.ValidateQueryReferencesAsync(reference));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "禁止创建", group.TagGroupId)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.SetTagArchivedAsync(individuallyArchived.TagId, false));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            setup.Repository.AddTagsAsync([setup.B], [active.TagId]));

        var restored = await setup.Repository.SetTagGroupArchivedAsync(group.TagGroupId, false);
        Assert.AreEqual(1, restored.ChangedCount);
        var visible = await setup.Repository.ListTagsAsync(group.TagGroupId);
        CollectionAssert.AreEqual(new[] { active.TagId }, visible.Select(tag => tag.TagId).ToArray());
        Assert.IsTrue((await setup.Repository.ListTagsAsync(group.TagGroupId, includeArchived: true))
            .Single(tag => tag.TagId == individuallyArchived.TagId).IsArchived);
        Assert.IsEmpty(await setup.Repository.ValidateQueryReferencesAsync(reference));
        CollectionAssert.AreEqual(
            new[] { setup.A },
            (await setup.Repository.QueryAsync(new(TagId: active.TagId, PageSize: 20))).Items.Select(item => item.AssetId).ToArray());
    }

    [TestMethod]
    public async Task UndoEnforcesLifoAcrossGroupAndChildArchiveOperations()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var group = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "撤销组"));
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "独立状态", group.TagGroupId));
        var groupArchive = await setup.Repository.SetTagGroupArchivedAsync(group.TagGroupId, true);
        Assert.IsNotNull(groupArchive.UndoToken);
        var childArchive = await setup.Repository.SetTagArchivedAsync(tag.TagId, true);
        Assert.IsNotNull(childArchive.UndoToken);

        await setup.RestartAsync();
        Assert.IsFalse(await setup.Repository.UndoAsync(groupArchive.UndoToken),
            "An older operation must not jump over a newer active journal entry.");
        Assert.IsTrue(await setup.Repository.UndoAsync(childArchive.UndoToken));
        Assert.IsTrue(await setup.Repository.UndoAsync(groupArchive.UndoToken));

        Assert.IsFalse((await setup.Repository.ListTagGroupsAsync(includeArchived: true))
            .Single(item => item.TagGroupId == group.TagGroupId).IsArchived);
        Assert.IsFalse((await setup.Repository.ListTagsAsync(group.TagGroupId, includeArchived: true))
            .Single(item => item.TagId == tag.TagId).IsArchived);
        Assert.IsTrue(await setup.Repository.RedoAsync(groupArchive.UndoToken));
        Assert.IsTrue(await setup.Repository.RedoAsync(childArchive.UndoToken));
    }

    [TestMethod]
    public async Task MergeRejectsTagsWhoseParentGroupIsArchivedForSingleAndMultiSourcePaths()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var sourceGroup = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "源组"));
        var targetGroup = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "目标组"));
        var first = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "源一", sourceGroup.TagGroupId));
        var second = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "源二", sourceGroup.TagGroupId));
        var target = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "目标", targetGroup.TagGroupId));
        await setup.Repository.AddTagsAsync([setup.A], [first.TagId]);
        await setup.Repository.AddTagsAsync([setup.B], [second.TagId]);

        await setup.Repository.SetTagGroupArchivedAsync(sourceGroup.TagGroupId, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.MergeTagsAsync(first.TagId, target.TagId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.MergeTagsAsync([first.TagId, second.TagId], target.TagId));

        await setup.Repository.SetTagGroupArchivedAsync(sourceGroup.TagGroupId, false);
        await setup.Repository.SetTagGroupArchivedAsync(targetGroup.TagGroupId, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.MergeTagsAsync(first.TagId, target.TagId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.MergeTagsAsync([first.TagId, second.TagId], target.TagId));

        CollectionAssert.AreEqual(new[] { setup.A },
            (await setup.Repository.ListTagMembershipsAsync(tagId: first.TagId)).Select(row => row.AssetId).ToArray());
        CollectionAssert.AreEqual(new[] { setup.B },
            (await setup.Repository.ListTagMembershipsAsync(tagId: second.TagId)).Select(row => row.AssetId).ToArray());
        Assert.IsFalse((await setup.Repository.ListTagsAsync(includeArchived: true))
            .Single(tag => tag.TagId == first.TagId).IsArchived);
    }

    [TestMethod]
    public async Task V6NameReferencesMigrateToStableIdsUsingUnicodeNormalizationAndActiveRows()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "Caf\u00e9"));
        var group = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "组"));
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "\u00c5ngstr\u00f6m", group.TagGroupId));
        var smartFolderId = Guid.NewGuid();
        await setup.Repository.SaveSmartFolderAsync(
            new(smartFolderId, "旧名称引用"),
            [
                new(Guid.NewGuid(), smartFolderId, SmartFolderField.Folder, SmartFolderOperator.Equals, "Cafe\u0301"),
                new(Guid.NewGuid(), smartFolderId, SmartFolderField.Tag, SmartFolderOperator.Equals, "\u00e5NGSTR\u00d6M")
            ]);
        await DowngradeToV6Async(setup.DatabasePath);

        await setup.RestartAsync();

        var migrated = await setup.Repository.GetSmartFolderQueryDocumentAsync(smartFolderId);
        Assert.IsNotNull(migrated);
        var references = EnumerateRules(migrated.Document.RootGroup)
            .Where(rule => rule.Field is AssetQueryField.Folder or AssetQueryField.Tag)
            .ToDictionary(rule => rule.Field!.Value, rule => rule.Values.Single());
        Assert.AreEqual($"id:{folder.FolderId:D}", references[AssetQueryField.Folder]);
        Assert.AreEqual($"id:{tag.TagId:D}", references[AssetQueryField.Tag]);
    }

    [TestMethod]
    public async Task V6MissingOrAmbiguousNameReferenceRollsBackMigration()
    {
        await AssertRejectedLegacyNameAsync("不存在", createAmbiguousTags: false);
        await AssertRejectedLegacyNameAsync("重复", createAmbiguousTags: true);
    }

    [TestMethod]
    public async Task RenameTagRewritesResidualNameReferenceToStableIdInSameTransaction()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "旧名称"));
        await setup.Repository.AddTagsAsync([setup.A], [tag.TagId]);
        var folder = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "名称引用"),
            new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [
                    AssetQueryNode.Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, ["name:旧名称"])
                ])
            });

        await setup.Repository.RenameTagAsync(tag.TagId, "新名称");

        var stored = await setup.Repository.GetSmartFolderQueryDocumentAsync(folder.SmartFolderId);
        Assert.IsNotNull(stored);
        Assert.AreEqual($"id:{tag.TagId:D}", EnumerateRules(stored.Document.RootGroup).Single().Values.Single());
        var result = await setup.Repository.QueryAsync(new(SmartFolderId: folder.SmartFolderId, PageSize: 20));
        Assert.IsTrue(string.IsNullOrWhiteSpace(result.RegexError), result.RegexError);
        CollectionAssert.AreEqual(new[] { setup.A }, result.Items.Select(item => item.AssetId).ToArray());
    }

    [TestMethod]
    public async Task RenameFolderRewritesResidualNameReferenceToStableIdInSameTransaction()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "旧文件夹"));
        await setup.Repository.AddToFolderAsync([setup.A], folder.FolderId);
        var smartFolder = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "文件夹名称引用"),
            new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [
                    AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, ["name:旧文件夹"])
                ])
            });

        await setup.Repository.RenameFolderAsync(folder.FolderId, "新文件夹");

        var stored = await setup.Repository.GetSmartFolderQueryDocumentAsync(smartFolder.SmartFolderId);
        Assert.IsNotNull(stored);
        Assert.AreEqual($"id:{folder.FolderId:D}", EnumerateRules(stored.Document.RootGroup).Single().Values.Single());
        var result = await setup.Repository.QueryAsync(new(SmartFolderId: smartFolder.SmartFolderId, PageSize: 20));
        Assert.IsTrue(string.IsNullOrWhiteSpace(result.RegexError), result.RegexError);
        CollectionAssert.AreEqual(new[] { setup.A }, result.Items.Select(item => item.AssetId).ToArray());
    }

    [TestMethod]
    public async Task P3DocumentIsLegacyAdaptersSingleSourceAndRejectsStaleProjectionSave()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folderId = Guid.NewGuid();
        var first = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(folderId, "投影"),
            DocumentWithFileName("before"));
        var staleRules = await setup.Repository.ListSmartFolderRulesAsync(folderId);
        Assert.AreEqual("before", staleRules.Single().Value);

        var second = await setup.Repository.SaveSmartFolderQueryDocumentAsync(first, DocumentWithFileName("after"));
        var currentRules = await setup.Repository.ListSmartFolderRulesAsync(folderId);
        Assert.AreEqual("after", currentRules.Single().Value);

        await setup.Repository.SaveSmartFolderAsync(second, currentRules);
        var afterRoundTrip = await setup.Repository.GetSmartFolderQueryDocumentAsync(folderId);
        Assert.IsNotNull(afterRoundTrip);
        Assert.AreEqual("after", EnumerateRules(afterRoundTrip.Document.RootGroup).Single().Values.Single());
        Assert.AreEqual(
            AssetQueryDocumentCodec.ComputeHash(DocumentWithFileName("after")),
            afterRoundTrip.QueryHash,
            "An unchanged legacy round trip must not rewrite the canonical P3 document.");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.SaveSmartFolderAsync(second, staleRules));
        var afterRejectedStaleSave = await setup.Repository.GetSmartFolderQueryDocumentAsync(folderId);
        Assert.IsNotNull(afterRejectedStaleSave);
        Assert.AreEqual(afterRoundTrip.QueryHash, afterRejectedStaleSave.QueryHash);
    }

    [TestMethod]
    public async Task LegacyAdapterFailsClosedForUnrepresentableCanonicalDocument()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folder = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "无法降级"),
            new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                Text = "P3 only",
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All)
            });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.Repository.ListSmartFolderRulesAsync(folder.SmartFolderId));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.Repository.SaveSmartFolderAsync(folder, []));
    }

    private static AssetQueryDocument Document(params AssetQueryNode[] nodes) => new()
    {
        Scope = AssetQueryScope.AllAssets,
        RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, nodes)
    };

    private static IEnumerable<AssetQueryNode> Flatten(AssetQueryNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var nested in Flatten(child))
                yield return nested;
    }

    private static AssetQueryDocument DocumentWithFileName(string value) => new()
    {
        Scope = AssetQueryScope.AllAssets,
        RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
        [
            AssetQueryNode.Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, [value])
        ])
    };

    private static IEnumerable<AssetQueryNode> EnumerateRules(AssetQueryNode node)
    {
        if (node.Kind == AssetQueryNodeKind.Rule)
        {
            yield return node;
            yield break;
        }
        foreach (var child in node.Children)
            foreach (var rule in EnumerateRules(child)) yield return rule;
    }

    private static async Task AssertRejectedLegacyNameAsync(string referenceName, bool createAmbiguousTags)
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var seed = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "迁移种子"));
        if (createAmbiguousTags)
        {
            var first = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "第一组"));
            var second = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "第二组"));
            await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), referenceName, first.TagGroupId));
            await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), referenceName, second.TagGroupId));
        }
        var smartFolderId = Guid.NewGuid();
        await setup.Repository.SaveSmartFolderAsync(
            new(smartFolderId, "坏名称引用"),
            [new(Guid.NewGuid(), smartFolderId, SmartFolderField.Tag, SmartFolderOperator.Equals, seed.Name)]);
        await DowngradeToV6Async(setup.DatabasePath);
        await ExecuteAsync(
            setup.DatabasePath,
            "UPDATE SmartFolderRules SET Value=$value WHERE SmartFolderId=$id;",
            ("$value", referenceName),
            ("$id", smartFolderId.ToString("D")));

        await Assert.ThrowsAsync<InvalidDataException>(() => setup.RestartAsync());
        Assert.AreEqual(6L, await ScalarAsync(setup.DatabasePath, "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;"));
        Assert.AreEqual(0L, await ScalarAsync(
            setup.DatabasePath,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SmartFolderQueryDocuments';"));
    }

    private static async Task DowngradeToV6Async(string databasePath)
    {
        await ExecuteAsync(databasePath, """
            PRAGMA foreign_keys=OFF;
            DROP TABLE IF EXISTS SmartFolderQueryDocuments;
            DELETE FROM AssetLibrarySchemaInfo WHERE Version > 6;
            INSERT OR IGNORE INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES(6,'2026-09-02T00:00:00.0000000+00:00');
            PRAGMA foreign_keys=ON;
            """);
    }

    private static async Task ExecuteAsync(
        string databasePath,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(string databasePath, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
