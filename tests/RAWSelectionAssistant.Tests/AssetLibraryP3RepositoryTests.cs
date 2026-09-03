using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3RepositoryTests
{
    [TestMethod]
    public async Task P3TestCleanupLeavesAnotherDatabaseConnectionUsable()
    {
        var first = await AssetLibraryP3TestSetup.CreateAsync();
        await using var second = await AssetLibraryP3TestSetup.CreateAsync();
        await using var activeSecondConnection = AssetLibraryP3TestSetup.CreatePooledConnection(second.DatabasePath);
        await activeSecondConnection.OpenAsync();

        await first.DisposeAsync();

        await using var command = activeSecondConnection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM AssetLibrarySchemaInfo;";
        Assert.IsGreaterThan(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [TestMethod]
    public async Task FolderAndTagAnyAllNoneNestedNotAndStablePagingShareOneQueryPlan()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var firstFolder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "第一组"));
        var secondFolder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "第二组"));
        var red = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "红色"));
        var blue = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "蓝色"));
        await setup.Repository.AddToFolderAsync([setup.A, setup.C], firstFolder.FolderId);
        await setup.Repository.AddToFolderAsync([setup.B, setup.C], secondFolder.FolderId);
        await setup.Repository.AddTagsAsync([setup.A, setup.C], [red.TagId]);
        await setup.Repository.AddTagsAsync([setup.B, setup.C], [blue.TagId]);

        await AssertIdsAsync(setup, [setup.A, setup.B, setup.C],
            Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, Id(red.TagId), Id(blue.TagId)));
        await AssertIdsAsync(setup, [setup.C],
            Rule(AssetQueryField.Tag, AssetQueryOperator.AllOf, Id(red.TagId), Id(blue.TagId)));
        await AssertIdsAsync(setup, [setup.B],
            Rule(AssetQueryField.Tag, AssetQueryOperator.NoneOf, Id(red.TagId)));
        await AssertIdsAsync(setup, [setup.A, setup.B, setup.C],
            Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, Id(firstFolder.FolderId), Id(secondFolder.FolderId)));
        await AssertIdsAsync(setup, [setup.C],
            Rule(AssetQueryField.Folder, AssetQueryOperator.AllOf, Id(firstFolder.FolderId), Id(secondFolder.FolderId)));
        await AssertIdsAsync(setup, [setup.B],
            Rule(AssetQueryField.Folder, AssetQueryOperator.NoneOf, Id(firstFolder.FolderId)));

        var nested = new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Group(AssetQueryLogic.Any,
                [
                    Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "5"),
                    Rule(AssetQueryField.Comment, AssetQueryOperator.Contains, "中文")
                ], negated: true),
                Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsFalse),
                Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThan, "5") with { Enabled = false }
            ])
        };
        await AssertIdsAsync(setup, [setup.A], nested.RootGroup.Children.ToArray());

        var query = new AssetLibraryQuery(PageSize: 1, SortField: AssetLibrarySortField.FileName, SortDirection: AssetLibrarySortDirection.Ascending)
        {
            Document = new AssetQueryDocument
            {
                SortField = AssetLibrarySortField.FileName,
                SortDirection = AssetLibrarySortDirection.Ascending,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.Any,
                [
                    Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "0"),
                    Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsTrue)
                ])
            }
        };
        var paged = await setup.ReadAllAsync(query);
        CollectionAssert.AreEqual(new[] { setup.A, setup.B, setup.C }, paged.Select(item => item.AssetId).ToArray());
    }

    [TestMethod]
    public async Task NullUnicodeCaseAndLikeMetacharactersHaveLiteralFailClosedSemantics()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();

        var literalSearch = await setup.Repository.QueryAsync(new(SearchText: "%_", PageSize: 20));
        Assert.IsTrue(string.IsNullOrWhiteSpace(literalSearch.RegexError), literalSearch.RegexError);
        CollectionAssert.AreEqual(new[] { setup.A }, literalSearch.Items.Select(item => item.AssetId).ToArray());

        await AssertIdsAsync(setup, [setup.A],
            Rule(AssetQueryField.FileName, AssetQueryOperator.Contains, "%_"));
        await AssertIdsAsync(setup, [setup.A],
            Rule(AssetQueryField.CaptureTime, AssetQueryOperator.Unknown));
        await AssertIdsAsync(setup, [setup.A],
            Rule(AssetQueryField.Comment, AssetQueryOperator.IsEmpty));
        await AssertIdsAsync(setup, [setup.B, setup.C],
            Rule(AssetQueryField.Comment, AssetQueryOperator.IsNotEmpty));
        await AssertIdsAsync(setup, [setup.B, setup.C],
            AssetQueryNode.Rule(
                AssetQueryField.Comment,
                AssetQueryOperator.Contains,
                ["mixed"],
                caseSensitivity: AssetQueryCaseSensitivity.Insensitive));
        await AssertIdsAsync(setup, [setup.B],
            AssetQueryNode.Rule(
                AssetQueryField.Comment,
                AssetQueryOperator.Contains,
                ["MiXeD"],
                caseSensitivity: AssetQueryCaseSensitivity.Sensitive));
        await AssertIdsAsync(setup, [setup.B],
            Rule(AssetQueryField.Comment, AssetQueryOperator.Contains, "中文\ud83d\udcf7"));

        var invalid = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 20)
        {
            Document = new AssetQueryDocument { Version = AssetQueryDocument.CurrentVersion + 1 }
        });
        Assert.IsEmpty(invalid.Items);
        Assert.IsFalse(string.IsNullOrWhiteSpace(invalid.RegexError));
    }

    [TestMethod]
    public async Task DuplicateRulesAreDeterministicAndDoNotChangeTheTruthTable()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var rule = Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "3");
        var single = Document(rule);
        var duplicate = Document(rule, rule);

        var singleResult = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 20) { Document = single });
        var duplicateResult = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 20) { Document = duplicate });

        Assert.IsTrue(string.IsNullOrWhiteSpace(singleResult.RegexError), singleResult.RegexError);
        Assert.IsTrue(string.IsNullOrWhiteSpace(duplicateResult.RegexError), duplicateResult.RegexError);
        CollectionAssert.AreEqual(
            singleResult.Items.Select(item => item.AssetId).ToArray(),
            duplicateResult.Items.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(
            AssetQueryDocumentCodec.SerializeCanonical(duplicate),
            AssetQueryDocumentCodec.SerializeCanonical(AssetQueryDocumentCodec.Parse(AssetQueryDocumentCodec.SerializeCanonical(duplicate)).Document!));
    }

    [TestMethod]
    public async Task InvalidOrArchivedReferencesAreReportedAndNeverExpandToAllAssets()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var live = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "可用标签"));
        var archived = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "已归档标签"));
        await setup.Repository.SetTagArchivedAsync(archived.TagId, true);
        var missing = Guid.NewGuid();
        var document = Document(
            Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, Id(live.TagId), Id(archived.TagId), Id(missing)));

        var issues = await setup.Repository.ValidateQueryReferencesAsync(document);

        Assert.IsGreaterThanOrEqualTo(2, issues.Count, "Both archived and missing references must be surfaced.");
        Assert.IsTrue(issues.Any(issue => issue.Message.Contains(archived.TagId.ToString("D"), StringComparison.OrdinalIgnoreCase) || issue.Path.Contains("children", StringComparison.Ordinal)));
        Assert.IsTrue(issues.Any(issue => issue.Message.Contains(missing.ToString("D"), StringComparison.OrdinalIgnoreCase) || issue.Path.Contains("children", StringComparison.Ordinal)));
        var page = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 20) { Document = document });
        Assert.IsEmpty(page.Items, "Invalid references must not silently turn a saved filter into all assets.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(page.RegexError));
    }

    [TestMethod]
    public async Task CanonicalSmartFolderCanBeSavedQueriedCopiedArchivedRestoredAndReopened()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var document = new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            Text = string.Empty,
            SortField = AssetLibrarySortField.Rating,
            SortDirection = AssetLibrarySortDirection.Descending,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "3"),
                AssetQueryNode.Group(AssetQueryLogic.Any,
                [
                    Rule(AssetQueryField.Comment, AssetQueryOperator.Contains, "中文"),
                    Rule(AssetQueryField.Extension, AssetQueryOperator.Equals, ".jpg")
                ])
            ])
        };
        var folder = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "P3 条件", Description: "三层规则"),
            document);

        await setup.RestartAsync();
        var stored = await setup.Repository.GetSmartFolderQueryDocumentAsync(folder.SmartFolderId);
        Assert.IsNotNull(stored);
        Assert.AreEqual(AssetQueryDocumentCodec.ComputeHash(document), stored.QueryHash);
        Assert.AreEqual(
            AssetQueryDocumentCodec.SerializeCanonical(document),
            AssetQueryDocumentCodec.SerializeCanonical(stored.Document));
        var result = await setup.Repository.QueryAsync(new(SmartFolderId: folder.SmartFolderId, PageSize: 20));
        Assert.IsTrue(string.IsNullOrWhiteSpace(result.RegexError), result.RegexError);
        CollectionAssert.AreEquivalent(new[] { setup.B, setup.C }, result.Items.Select(item => item.AssetId).ToArray());

        var copy = await setup.Repository.CopySmartFolderAsync(folder.SmartFolderId);
        Assert.AreNotEqual(folder.SmartFolderId, copy.SmartFolderId);
        Assert.AreNotEqual(folder.Name, copy.Name);
        var copyDocument = await setup.Repository.GetSmartFolderQueryDocumentAsync(copy.SmartFolderId);
        Assert.IsNotNull(copyDocument);
        Assert.AreEqual(stored.QueryHash, copyDocument.QueryHash);

        var archived = await setup.Repository.SetSmartFolderArchivedAsync(folder.SmartFolderId, true);
        Assert.AreEqual(1, archived.ChangedCount);
        Assert.IsNotNull(archived.UndoToken);
        Assert.IsFalse((await setup.Repository.ListSmartFoldersAsync()).Any(item => item.SmartFolderId == folder.SmartFolderId));
        Assert.IsTrue((await setup.Repository.ListSmartFoldersAsync(includeArchived: true)).Single(item => item.SmartFolderId == folder.SmartFolderId).IsArchived);
        var restored = await setup.Repository.SetSmartFolderArchivedAsync(folder.SmartFolderId, false);
        Assert.AreEqual(1, restored.ChangedCount);
        await setup.RestartAsync();
        Assert.IsFalse((await setup.Repository.ListSmartFoldersAsync()).Single(item => item.SmartFolderId == folder.SmartFolderId).IsArchived);
    }

    [TestMethod]
    public async Task SmartFolderRejectsInvalidRegexBeforePersistingAnyFolderState()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folderId = Guid.NewGuid();
        var invalid = Document(Rule(AssetQueryField.FileName, AssetQueryOperator.Regex, "[unterminated"));

        await Assert.ThrowsAsync<ArgumentException>(() => setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(folderId, "非法正则"),
            invalid));

        Assert.IsFalse((await setup.Repository.ListSmartFoldersAsync(includeArchived: true))
            .Any(folder => folder.SmartFolderId == folderId));
    }

    [TestMethod]
    public async Task CurrentScopeDocumentIsAndedWithSavedSmartFolderRulesInsteadOfReplacingTheRange()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var smartFolder = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "评分范围"),
            new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "3")])
            });
        var currentFilter = new AssetQueryDocument
        {
            Scope = AssetQueryScope.Current,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsFalse)])
        };

        var page = await setup.Repository.QueryAsync(new AssetLibraryQuery(
            SmartFolderId: smartFolder.SmartFolderId,
            PageSize: 20)
        {
            Document = currentFilter
        });

        Assert.IsTrue(string.IsNullOrWhiteSpace(page.RegexError), page.RegexError);
        CollectionAssert.AreEqual(new[] { setup.B }, page.Items.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(1, page.TotalCount);
    }

    [TestMethod]
    public async Task SavedAndTransientSmartFolderTextClausesAreAndedForDocumentAndLegacySearchInputs()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var smartFolder = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "文本范围"),
            new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                Text = "mixed",
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All)
            });
        var currentFilter = new AssetQueryDocument
        {
            Scope = AssetQueryScope.Current,
            Text = "100",
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All)
        };

        var composed = await setup.Repository.QueryAsync(new AssetLibraryQuery(
            SmartFolderId: smartFolder.SmartFolderId,
            PageSize: 20)
        {
            Document = currentFilter
        });
        Assert.IsTrue(string.IsNullOrWhiteSpace(composed.RegexError), composed.RegexError);
        CollectionAssert.AreEqual(new[] { setup.C }, composed.Items.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(1, composed.TotalCount);

        var legacySearch = await setup.Repository.QueryAsync(new(
            SearchText: "100",
            SmartFolderId: smartFolder.SmartFolderId,
            PageSize: 20));
        Assert.IsTrue(string.IsNullOrWhiteSpace(legacySearch.RegexError), legacySearch.RegexError);
        CollectionAssert.AreEqual(new[] { setup.C }, legacySearch.Items.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(1, legacySearch.TotalCount);

        var resaved = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "再次保存的组合文字"),
            new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                Text = "100",
                SearchClauses = ["mixed", "100"],
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All)
            });
        await setup.RestartAsync();
        var reopened = await setup.Repository.GetSmartFolderQueryDocumentAsync(resaved.SmartFolderId);
        CollectionAssert.AreEqual(new[] { "mixed", "100" }, reopened!.Document.SearchClauses!.ToArray());
        var persisted = await setup.Repository.QueryAsync(new(SmartFolderId: resaved.SmartFolderId, PageSize: 20));
        CollectionAssert.AreEqual(new[] { setup.C }, persisted.Items.Select(item => item.AssetId).ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            setup.Repository.ListSmartFolderRulesAsync(resaved.SmartFolderId));
    }

    [TestMethod]
    public async Task SmartFolderArchiveCandidateGateExpandsWhenEitherSavedOrTransientDocumentRequestsIt()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();

        var savedOnly = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "保存条件允许归档"),
            new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                IncludeArchived = true,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [Rule(AssetQueryField.FileName, AssetQueryOperator.Equals, "archived.jpg")])
            });
        var savedOnlyPage = await setup.Repository.QueryAsync(new AssetLibraryQuery(SmartFolderId: savedOnly.SmartFolderId, PageSize: 20)
        {
            Document = new AssetQueryDocument { Scope = AssetQueryScope.Current }
        });
        CollectionAssert.AreEqual(new[] { setup.Archived }, savedOnlyPage.Items.Select(item => item.AssetId).ToArray());

        var transientOnly = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "当前条件允许归档"),
            new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                IncludeArchived = false,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [Rule(AssetQueryField.Extension, AssetQueryOperator.Equals, ".jpg")])
            });
        var transientOnlyPage = await setup.Repository.QueryAsync(new AssetLibraryQuery(SmartFolderId: transientOnly.SmartFolderId, PageSize: 20)
        {
            Document = new AssetQueryDocument
            {
                Scope = AssetQueryScope.Current,
                IncludeArchived = true,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [Rule(AssetQueryField.FileName, AssetQueryOperator.Equals, "archived.jpg")])
            }
        });
        CollectionAssert.AreEqual(new[] { setup.Archived }, transientOnlyPage.Items.Select(item => item.AssetId).ToArray());

        var conflict = await setup.Repository.QueryAsync(new AssetLibraryQuery(SmartFolderId: transientOnly.SmartFolderId, PageSize: 20)
        {
            Document = new AssetQueryDocument
            {
                Scope = AssetQueryScope.Current,
                IncludeArchived = true,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [
                    AssetQueryNode.Rule(AssetQueryField.IsArchived, AssetQueryOperator.IsTrue),
                    AssetQueryNode.Rule(AssetQueryField.IsArchived, AssetQueryOperator.IsFalse)
                ])
            }
        });
        Assert.IsEmpty(conflict.Items, "Expanding the candidate pool must not weaken conflicting saved/transient rules.");
    }

    [TestMethod]
    public async Task P3DocumentAndLegacyFileNameRegexAreAndedAndRegexRemainsParameterized()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var query = new AssetLibraryQuery(FileNameRegex: "^portrait", PageSize: 20)
        {
            Document = Document(Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, "3"))
        };

        var page = await setup.Repository.QueryAsync(query);
        Assert.IsTrue(string.IsNullOrWhiteSpace(page.RegexError), page.RegexError);
        CollectionAssert.AreEqual(new[] { setup.B }, page.Items.Select(item => item.AssetId).ToArray());

        var plan = await setup.Repository.ExplainQueryPlanAsync(query);
        StringAssert.Contains(plan.SqlTemplate, "regexp(");
        Assert.DoesNotContain("^portrait", plan.SqlTemplate, StringComparison.Ordinal);
        Assert.IsTrue(plan.Parameters.Any(parameter => parameter.Name.StartsWith("$p3", StringComparison.Ordinal)));

        var invalid = await setup.Repository.QueryAsync(query with { FileNameRegex = "[unterminated" });
        Assert.IsEmpty(invalid.Items);
        Assert.IsFalse(string.IsNullOrWhiteSpace(invalid.RegexError));
    }

    [TestMethod]
    public async Task ExtensionSuggestionsExcludeArchivedAssetsByDefault()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        await setup.InsertAssetsAsync(
        [
            new(Guid.NewGuid(), "archived-only.secret", ".secret", "Other", 10, null, null, null, null,
                DateTimeOffset.UtcNow, 0, string.Empty, false, true)
        ]);

        var archivedOnly = await setup.Repository.GetQuerySuggestionsAsync(".secret", 20);
        Assert.IsFalse(archivedOnly.Any(item => item.Kind == "extension" && item.Value.Equals(".secret", StringComparison.OrdinalIgnoreCase)));
        var active = await setup.Repository.GetQuerySuggestionsAsync(".RAW", 20);
        Assert.IsTrue(active.Any(item => item.Kind == "extension" && item.Value.Equals(".RAW", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task TagManagementMergeDedupeReferenceRewriteAndUndoRedoSurviveRestart()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var firstGroup = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "人物", 0));
        var secondGroup = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "地点", 1));
        var source = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), " Cafe\u0301 ", firstGroup.TagGroupId, 0));
        var target = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "目标", secondGroup.TagGroupId, 0));
        var spare = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "备用", secondGroup.TagGroupId, 1));
        Assert.AreEqual("Caf\u00e9", source.Name);
        var canonicalDuplicates = await setup.Repository.BatchCreateTagsAsync("Caf\u00e9， Cafe\u0301", firstGroup.TagGroupId);
        Assert.HasCount(1, canonicalDuplicates);
        Assert.AreEqual(source.TagId, canonicalDuplicates[0].TagId);

        var tagArchive = await setup.Repository.SetTagArchivedAsync(spare.TagId, true);
        Assert.AreEqual(1, tagArchive.ChangedCount);
        Assert.IsFalse((await setup.Repository.ListTagsAsync(secondGroup.TagGroupId)).Any(tag => tag.TagId == spare.TagId));
        Assert.AreEqual(1, (await setup.Repository.SetTagArchivedAsync(spare.TagId, false)).ChangedCount);
        var groupArchive = await setup.Repository.SetTagGroupArchivedAsync(firstGroup.TagGroupId, true);
        Assert.AreEqual(1, groupArchive.ChangedCount, "Group archive changes only the group; child archive bits are preserved.");
        Assert.IsFalse((await setup.Repository.ListTagGroupsAsync()).Any(group => group.TagGroupId == firstGroup.TagGroupId));
        Assert.IsFalse((await setup.Repository.ListTagsAsync(includeArchived: true)).Single(tag => tag.TagId == source.TagId).IsArchived);
        Assert.AreEqual(1, (await setup.Repository.SetTagGroupArchivedAsync(firstGroup.TagGroupId, false)).ChangedCount);
        Assert.IsFalse((await setup.Repository.ListTagsAsync(includeArchived: true)).Single(tag => tag.TagId == source.TagId).IsArchived);

        var renamed = await setup.Repository.RenameTagAsync(source.TagId, "  人像  ");
        Assert.AreEqual(1, renamed.ChangedCount);
        var moved = await setup.Repository.MoveTagsToGroupAsync([source.TagId], secondGroup.TagGroupId);
        Assert.AreEqual(1, moved.ChangedCount);
        await setup.Repository.ReorderTagGroupsAsync([secondGroup.TagGroupId, firstGroup.TagGroupId]);
        await setup.Repository.ReorderTagsAsync(secondGroup.TagGroupId, [spare.TagId, source.TagId, target.TagId]);
        var orderedGroups = await setup.Repository.ListTagGroupsAsync();
        CollectionAssert.AreEqual(new[] { secondGroup.TagGroupId, firstGroup.TagGroupId }, orderedGroups.Select(group => group.TagGroupId).ToArray());
        var orderedTags = await setup.Repository.ListTagsAsync(secondGroup.TagGroupId);
        CollectionAssert.AreEqual(new[] { spare.TagId, source.TagId, target.TagId }, orderedTags.Select(tag => tag.TagId).ToArray());

        await setup.Repository.AddTagsAsync([setup.A, setup.C], [source.TagId]);
        await setup.Repository.AddTagsAsync([setup.B, setup.C], [target.TagId]);
        var saved = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "引用源标签"),
            Document(Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, Id(source.TagId))));

        var merge = await setup.Repository.MergeTagsAsync(source.TagId, target.TagId);
        Assert.IsNotNull(merge.UndoToken);
        CollectionAssert.AreEquivalent(
            new[] { setup.A, setup.B, setup.C },
            (await setup.Repository.ListTagMembershipsAsync(tagId: target.TagId)).Select(item => item.AssetId).ToArray());
        Assert.IsTrue((await setup.Repository.ListTagsAsync(includeArchived: true)).Single(tag => tag.TagId == source.TagId).IsArchived);
        var rewritten = await setup.Repository.GetSmartFolderQueryDocumentAsync(saved.SmartFolderId);
        Assert.IsNotNull(rewritten);
        Assert.IsTrue(Flatten(rewritten.Document.RootGroup).Any(node => node.Values.Contains(Id(target.TagId), StringComparer.Ordinal)));
        Assert.IsFalse(Flatten(rewritten.Document.RootGroup).Any(node => node.Values.Contains(Id(source.TagId), StringComparer.Ordinal)));

        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(merge.UndoToken));
        Assert.IsFalse((await setup.Repository.ListTagsAsync(includeArchived: true)).Single(tag => tag.TagId == source.TagId).IsArchived);
        CollectionAssert.AreEquivalent(
            new[] { setup.A, setup.C },
            (await setup.Repository.ListTagMembershipsAsync(tagId: source.TagId)).Select(item => item.AssetId).ToArray());
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.RedoAsync(merge.UndoToken));
        CollectionAssert.AreEquivalent(
            new[] { setup.A, setup.B, setup.C },
            (await setup.Repository.ListTagMembershipsAsync(tagId: target.TagId)).Select(item => item.AssetId).ToArray());
    }

    [TestMethod]
    public async Task BatchMetadataIsAtomicDeduplicatedAndUndoRedoPersistsAcrossRepositoryRestart()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "批量标签"));
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "批量文件夹"));
        await setup.Repository.AddTagsAsync([setup.A], [tag.TagId]);
        var ids = new[] { setup.A, setup.B, setup.C };
        var request = new AssetBatchMetadataRequest(
            ids,
            AddTagIds: [tag.TagId, tag.TagId],
            AddFolderIds: [folder.FolderId, folder.FolderId],
            Rating: 4,
            Comment: "批量更新",
            IsMissing: false);

        var preview = await setup.Repository.PreviewBatchMetadataAsync(request);
        Assert.AreEqual(3, preview.AssetCount);
        Assert.IsTrue(preview.HasMixedRatings);
        Assert.IsTrue(preview.HasMixedComments);
        Assert.IsGreaterThanOrEqualTo(1, preview.ExistingTagRelationships);
        Assert.AreEqual(3, preview.ChangedCount);
        Assert.IsFalse(string.IsNullOrWhiteSpace(preview.CanonicalRequestFingerprint));
        Assert.IsFalse(string.IsNullOrWhiteSpace(preview.BeforeStateFingerprint));
        Assert.IsFalse(string.IsNullOrWhiteSpace(preview.PreviewFingerprint));

        var applied = await setup.Repository.ApplyBatchMetadataAsync(request, preview);
        Assert.AreEqual(3, applied.ChangedCount);
        Assert.IsNotNull(applied.UndoToken);
        Assert.HasCount(3, await setup.Repository.ListTagMembershipsAsync(tagId: tag.TagId));
        Assert.HasCount(3, await setup.Repository.ListFolderMembershipsAsync(folderId: folder.FolderId));
        foreach (var id in ids)
        {
            var asset = await setup.Repository.GetAssetAsync(id);
            Assert.IsNotNull(asset);
            Assert.AreEqual(4, asset.Rating);
            Assert.AreEqual("批量更新", asset.Comment);
            Assert.IsFalse(asset.IsMissing);
        }

        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(applied.UndoToken));
        Assert.HasCount(1, await setup.Repository.ListTagMembershipsAsync(tagId: tag.TagId));
        Assert.IsEmpty(await setup.Repository.ListFolderMembershipsAsync(folderId: folder.FolderId));
        Assert.AreEqual(0, (await setup.Repository.GetAssetAsync(setup.A))!.Rating);
        Assert.AreEqual(3, (await setup.Repository.GetAssetAsync(setup.B))!.Rating);
        Assert.AreEqual(5, (await setup.Repository.GetAssetAsync(setup.C))!.Rating);

        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.RedoAsync(applied.UndoToken));
        Assert.HasCount(3, await setup.Repository.ListTagMembershipsAsync(tagId: tag.TagId));
        Assert.HasCount(3, await setup.Repository.ListFolderMembershipsAsync(folderId: folder.FolderId));

        var before = await setup.SnapshotAsync(ids);
        var invalidRequest = request with { AddTagIds = [tag.TagId, Guid.NewGuid()], Rating = 2 };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => setup.Repository.PreviewBatchMetadataAsync(invalidRequest));
        Exception? validationFailure = null;
        try
        {
            await setup.Repository.ApplyBatchMetadataAsync(invalidRequest, preview);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException)
        {
            validationFailure = exception;
        }
        Assert.IsNotNull(validationFailure, "An unknown tag must reject the entire batch before any writes are committed.");
        CollectionAssert.AreEqual(before, await setup.SnapshotAsync(ids), "A failed validation must roll back the complete batch.");

        var missingAssetRequest = request with { AssetIds = [setup.A, Guid.NewGuid()], Rating = 1 };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => setup.Repository.PreviewBatchMetadataAsync(missingAssetRequest));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => setup.Repository.ApplyBatchMetadataAsync(missingAssetRequest, preview));
        CollectionAssert.AreEqual(before, await setup.SnapshotAsync(ids), "A missing asset must reject the complete batch before writes.");

        var missingTag = Guid.NewGuid();
        var invalidRemoveTag = request with { AddTagIds = [], RemoveTagIds = [missingTag] };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => setup.Repository.PreviewBatchMetadataAsync(invalidRemoveTag));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => setup.Repository.ApplyBatchMetadataAsync(invalidRemoveTag, preview));

        await setup.Repository.UpdateAssetMetadataAsync(setup.A, rating: 1);
        var partialRequest = new AssetBatchMetadataRequest([setup.A, setup.B], Rating: 4);
        var partialPreview = await setup.Repository.PreviewBatchMetadataAsync(partialRequest);
        Assert.AreEqual(1, (await setup.Repository.GetAssetAsync(setup.A))!.Rating, "Preview must roll back its exact apply simulation.");
        var partial = await setup.Repository.ApplyBatchMetadataAsync(partialRequest, partialPreview);
        Assert.AreEqual(1, partial.ChangedCount, "ChangedCount must count assets whose effective metadata or relationships changed.");

        var overLimit = Enumerable.Range(0, 10_001).Select(_ => Guid.NewGuid()).ToArray();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => setup.Repository.ApplyBatchMetadataAsync(request with { AssetIds = overLimit }, preview));

        var emptyRequest = request with { AssetIds = [] };
        var emptyPreview = await setup.Repository.PreviewBatchMetadataAsync(emptyRequest);
        var empty = await setup.Repository.ApplyBatchMetadataAsync(emptyRequest, emptyPreview);
        Assert.AreEqual(0, empty.ChangedCount);
        Assert.IsNull(empty.UndoToken);
    }

    [TestMethod]
    public async Task BatchMetadataPreviewContractRejectsChangedRequestAndExternalStateThenFreshPreviewApplies()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "preview-contract"));
        var request = new AssetBatchMetadataRequest([setup.A, setup.B], AddTagIds: [tag.TagId], Rating: 4);
        var preview = await setup.Repository.PreviewBatchMetadataAsync(request);
        Assert.AreEqual(2, preview.ChangedCount);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.ApplyBatchMetadataAsync(request with { Rating = 5 }, preview));
        Assert.IsEmpty(await setup.Repository.ListTagMembershipsAsync(tagId: tag.TagId));

        await setup.Repository.UpdateAssetMetadataAsync(setup.A, rating: 1);
        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Repository.ApplyBatchMetadataAsync(request, preview));
        StringAssert.Contains(stale.Message, "重新预览");
        Assert.AreEqual(1, (await setup.Repository.GetAssetAsync(setup.A))!.Rating);
        Assert.AreEqual(3, (await setup.Repository.GetAssetAsync(setup.B))!.Rating);
        Assert.IsEmpty(await setup.Repository.ListTagMembershipsAsync(tagId: tag.TagId));

        var fresh = await setup.Repository.PreviewBatchMetadataAsync(request);
        var applied = await setup.Repository.ApplyBatchMetadataAsync(request, fresh);
        Assert.AreEqual(fresh.ChangedCount, applied.ChangedCount);
        Assert.HasCount(2, await setup.Repository.ListTagMembershipsAsync(tagId: tag.TagId));
        Assert.AreEqual(4, (await setup.Repository.GetAssetAsync(setup.A))!.Rating);
        Assert.AreEqual(4, (await setup.Repository.GetAssetAsync(setup.B))!.Rating);

        var conflictRequest = new AssetBatchMetadataRequest(
            [setup.C],
            AddTagIds: [tag.TagId],
            RemoveTagIds: [tag.TagId],
            Rating: 5,
            ClearRating: true);
        var conflictPreview = await setup.Repository.PreviewBatchMetadataAsync(conflictRequest);
        Assert.AreEqual(2, conflictPreview.ConflictOverrideCount);
        Assert.HasCount(2, conflictPreview.ConflictOverrides);
        Assert.IsTrue(conflictPreview.ConflictOverrides.Any(value => value.Contains("添加覆盖移除", StringComparison.Ordinal)));
        Assert.IsTrue(conflictPreview.ConflictOverrides.Any(value => value.Contains("清除覆盖设置", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task LegacyV1JournalRemainsUndoOnlyWhileP3V2JournalHasAfterImage()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var legacy = await setup.InsertLegacyV1MetadataJournalAsync(setup.A, 1, "旧值", 5, "新值");

        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(legacy));
        Assert.AreEqual(1, (await setup.Repository.GetAssetAsync(setup.A))!.Rating);
        await setup.RestartAsync();
        Assert.IsFalse(await setup.Repository.RedoAsync(legacy));
        Assert.AreEqual(1, (await setup.Repository.GetAssetAsync(setup.A))!.Rating);

        var v2 = await setup.Repository.UpdateAssetsMetadataAsync([setup.A], rating: 4, comment: "P3 after image");
        Assert.IsNotNull(v2.UndoToken);
        Assert.IsTrue(await setup.Repository.UndoAsync(v2.UndoToken));
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.RedoAsync(v2.UndoToken));
        var redone = await setup.Repository.GetAssetAsync(setup.A);
        Assert.AreEqual(4, redone!.Rating);
        Assert.AreEqual("P3 after image", redone.Comment);
    }

    private static string Id(Guid id) => "id:" + id.ToString("D");

    private static AssetQueryNode Rule(AssetQueryField field, AssetQueryOperator operation, params string[] values) =>
        AssetQueryNode.Rule(field, operation, values);

    private static AssetQueryDocument Document(params AssetQueryNode[] nodes) => new()
    {
        RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, nodes)
    };

    private static IEnumerable<AssetQueryNode> Flatten(AssetQueryNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var nested in Flatten(child))
            yield return nested;
    }

    private static async Task AssertIdsAsync(
        AssetLibraryP3TestSetup setup,
        IReadOnlyCollection<Guid> expected,
        params AssetQueryNode[] rules)
    {
        var page = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 100)
        {
            Document = Document(rules)
        });
        Assert.IsTrue(string.IsNullOrWhiteSpace(page.RegexError), page.RegexError);
        CollectionAssert.AreEquivalent(expected.ToArray(), page.Items.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(expected.Count, page.TotalCount);
    }
}

internal sealed class AssetLibraryP3TestSetup : IAsyncDisposable
{
    private readonly string _root;

    private AssetLibraryP3TestSetup(string root, SqliteAssetLibraryRepository repository)
    {
        _root = root;
        Repository = repository;
    }

    public SqliteAssetLibraryRepository Repository { get; private set; }
    public string DatabasePath => Repository.DatabasePath;
    public Guid A { get; private set; }
    public Guid B { get; private set; }
    public Guid C { get; private set; }
    public Guid Archived { get; private set; }

    public static async Task<AssetLibraryP3TestSetup> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3CoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "asset-library.db"));
        await repository.InitializeAsync();
        return new(root, repository);
    }

    public static async Task<AssetLibraryP3TestSetup> CreateCanonicalAsync()
    {
        var setup = await CreateAsync();
        var now = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);
        setup.A = Guid.Parse("10000000-0000-0000-0000-000000000001");
        setup.B = Guid.Parse("10000000-0000-0000-0000-000000000002");
        setup.C = Guid.Parse("10000000-0000-0000-0000-000000000003");
        setup.Archived = Guid.Parse("10000000-0000-0000-0000-000000000004");
        await setup.InsertAssetsAsync(
        [
            new(setup.A, "100%_literal.jpg", ".jpg", "Image", 1_000, 1_000, 500, "Landscape", null, now.AddDays(-3), 0, "", false, false),
            new(setup.B, "portrait-\u4e2d\u6587.RAW", ".RAW", "Raw", 2_000, 500, 1_000, "Portrait", now.AddDays(-10), now.AddDays(-2), 3, "MiXeD \u4e2d\u6587\ud83d\udcf7", false, false),
            new(setup.C, "square-cafe.jpg", ".jpg", "Image", 3_000, 800, 800, "Square", now.AddDays(-1), now.AddDays(-1), 5, "mixed alpha_100%", true, false),
            new(setup.Archived, "archived.jpg", ".jpg", "Image", 4_000, null, null, null, null, now, 4, "archived", false, true)
        ]);
        return setup;
    }

    public async Task RestartAsync()
    {
        var path = Repository.DatabasePath;
        await Repository.DisposeAsync();
        Repository = new SqliteAssetLibraryRepository(path);
        await Repository.InitializeAsync();
    }

    public async Task InsertAssetsAsync(IEnumerable<AssetLibraryP3Seed> assets)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        foreach (var asset in assets)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO AssetItems(
                    AssetId,SourcePath,NormalizedSourcePath,DuplicateDiscriminator,DisplayName,Extension,MediaType,
                    FileSize,Width,Height,Orientation,CaptureTime,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode)
                VALUES(
                    $id,$path,$path,'',$name,$extension,$media,$size,$width,$height,$orientation,$capture,$added,$modified,
                    $rating,$comment,$missing,$archived,'Reference');
                """;
            var path = $"synthetic://p3/{asset.Id:D}";
            command.Parameters.AddWithValue("$id", asset.Id.ToString("D"));
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$name", asset.Name);
            command.Parameters.AddWithValue("$extension", asset.Extension);
            command.Parameters.AddWithValue("$media", asset.MediaType);
            command.Parameters.AddWithValue("$size", asset.FileSize);
            command.Parameters.AddWithValue("$width", (object?)asset.Width ?? DBNull.Value);
            command.Parameters.AddWithValue("$height", (object?)asset.Height ?? DBNull.Value);
            command.Parameters.AddWithValue("$orientation", (object?)asset.Orientation ?? DBNull.Value);
            command.Parameters.AddWithValue("$capture", asset.CaptureTime is null ? DBNull.Value : asset.CaptureTime.Value.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$added", asset.AddedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$modified", asset.AddedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$rating", asset.Rating);
            command.Parameters.AddWithValue("$comment", asset.Comment);
            command.Parameters.AddWithValue("$missing", asset.IsMissing ? 1 : 0);
            command.Parameters.AddWithValue("$archived", asset.IsArchived ? 1 : 0);
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<AssetItem>> ReadAllAsync(AssetLibraryQuery query)
    {
        var result = new List<AssetItem>();
        var seen = new HashSet<Guid>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < 100; pageNumber++)
        {
            var page = await Repository.QueryAsync(query with { Cursor = cursor });
            Assert.IsTrue(string.IsNullOrWhiteSpace(page.RegexError), page.RegexError);
            foreach (var item in page.Items)
            {
                Assert.IsTrue(seen.Add(item.AssetId), $"Duplicate P3 result across pages: {item.AssetId:D}");
                result.Add(item);
            }
            cursor = page.NextCursor;
            if (cursor is null) return result;
        }
        Assert.Fail("P3 pagination did not terminate after 100 pages.");
        return result;
    }

    public async Task<string[]> SnapshotAsync(IEnumerable<Guid> assetIds)
    {
        var result = new List<string>();
        foreach (var id in assetIds.OrderBy(id => id))
        {
            var asset = await Repository.GetAssetAsync(id);
            var tags = await Repository.ListTagMembershipsAsync(assetId: id);
            var folders = await Repository.ListFolderMembershipsAsync(assetId: id);
            result.Add(JsonSerializer.Serialize(new
            {
                asset!.AssetId,
                asset.Rating,
                asset.Comment,
                asset.IsMissing,
                asset.IsArchived,
                Tags = tags.Select(item => item.TagId).OrderBy(value => value).ToArray(),
                Folders = folders.Select(item => item.FolderId).OrderBy(value => value).ToArray()
            }));
        }
        return result.ToArray();
    }

    public async Task<AssetLibraryUndoToken> InsertLegacyV1MetadataJournalAsync(
        Guid assetId,
        int previousRating,
        string previousComment,
        int currentRating,
        string currentComment)
    {
        var token = new AssetLibraryUndoToken(Guid.NewGuid(), "Legacy v1 metadata", DateTimeOffset.UtcNow);
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE AssetItems SET Rating=$rating,Comment=$comment WHERE AssetId=$id;";
            update.Parameters.AddWithValue("$rating", currentRating);
            update.Parameters.AddWithValue("$comment", currentComment);
            update.Parameters.AddWithValue("$id", assetId.ToString("D"));
            await update.ExecuteNonQueryAsync();
        }
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO AssetLibraryUndoJournal(OperationId,Description,OperationKind,PayloadJson,CreatedAt,UndoneAt,JournalVersion) VALUES($id,$description,'asset-metadata',$payload,$created,NULL,1);";
            insert.Parameters.AddWithValue("$id", token.OperationId.ToString("D"));
            insert.Parameters.AddWithValue("$description", token.Description);
            insert.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(new { AssetId = assetId, Rating = previousRating, Comment = previousComment }));
            insert.Parameters.AddWithValue("$created", token.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            await insert.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        return token;
    }

    public async ValueTask DisposeAsync()
    {
        var databasePath = Repository.DatabasePath;
        await Repository.DisposeAsync();
        using (var poolKey = CreatePooledConnection(databasePath)) SqliteConnection.ClearPool(poolKey);
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    internal static SqliteConnection CreatePooledConnection(string databasePath) => new(
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 5
        }.ToString());
}

internal sealed record AssetLibraryP3Seed(
    Guid Id,
    string Name,
    string Extension,
    string MediaType,
    long FileSize,
    int? Width,
    int? Height,
    string? Orientation,
    DateTimeOffset? CaptureTime,
    DateTimeOffset AddedAt,
    int Rating,
    string Comment,
    bool IsMissing,
    bool IsArchived);
