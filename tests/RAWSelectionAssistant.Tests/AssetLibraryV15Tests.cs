using System.Text;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryV15Tests
{
    [TestMethod]
    public async Task FolderTreeSupportsBatchHierarchyCountsCopyMoveAndCycleGuard()
    {
        await using var setup = await TestSetup.CreateAsync();
        var result = await setup.Repository.BatchCreateFoldersAsync("人体/身体\n人体/宗教\n参考/白棚");
        Assert.HasCount(5, result.Created);
        var folders = await setup.Repository.ListFoldersAsync();
        var body = folders.Single(x => x.Name == "身体");
        var religion = folders.Single(x => x.Name == "宗教");
        var human = folders.Single(x => x.Name == "人体");
        var assetPath = setup.WriteFile("portrait.jpg", "bytes");
        await setup.Repository.ImportAsync([new(assetPath)]);
        var asset = (await setup.Repository.QueryAsync(new())).Items.Single();
        await setup.Repository.AddToFolderAsync([asset.AssetId], body.FolderId);
        await setup.Repository.AddToFolderAsync([asset.AssetId], religion.FolderId);

        var tree = await setup.Repository.GetFolderTreeAsync();
        var humanNode = tree.Single(x => x.Folder.FolderId == human.FolderId);
        Assert.AreEqual(1, humanNode.DescendantAssetCount);
        Assert.HasCount(2, humanNode.Children);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Repository.MoveFolderAsync(new(human.FolderId, body.FolderId, 0)));

        var copies = await setup.Repository.CopyFolderStructureAsync(human.FolderId, null, "人体模板");
        Assert.HasCount(3, copies);
        Assert.AreEqual("人体模板", copies[0].Name);
    }

    [TestMethod]
    public async Task FolderUndoPersistsAcrossRestartAndRemovesOnlyIntroducedAutoTags()
    {
        await using var setup = await TestSetup.CreateAsync();
        var source = setup.WriteFile("auto-tag.jpg", "bytes");
        await setup.Repository.ImportAsync([new(source)]);
        var asset = (await setup.Repository.QueryAsync(new())).Items.Single();
        var existingTag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "人体"));
        var introducedTag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "宗教"));
        await setup.Repository.AddTagsAsync([asset.AssetId], [existingTag.TagId]);
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "宗教参考", AutoTagIds: [existingTag.TagId, introducedTag.TagId]));
        var added = await setup.Repository.AddToFolderAsync([asset.AssetId], folder.FolderId);
        Assert.IsNotNull(added.UndoToken);
        await setup.RestartAsync();

        var journal = await setup.Repository.ListUndoJournalAsync();
        var token = journal.Single(x => x.Token.OperationId == added.UndoToken.OperationId).Token;
        Assert.IsTrue(await setup.Repository.UndoAsync(token));
        Assert.IsEmpty(await setup.Repository.ListFolderMembershipsAsync(assetId: asset.AssetId));
        var tagIds = (await setup.Repository.ListTagMembershipsAsync(assetId: asset.AssetId)).Select(x => x.TagId).ToArray();
        CollectionAssert.Contains(tagIds, existingTag.TagId);
        CollectionAssert.DoesNotContain(tagIds, introducedTag.TagId);
    }

    [TestMethod]
    public async Task TagMergeUndoSurvivesRepositoryRestart()
    {
        await using var setup = await TestSetup.CreateAsync();
        var sourcePath = setup.WriteFile("merge.jpg", "bytes");
        await setup.Repository.ImportAsync([new(sourcePath)]);
        var asset = (await setup.Repository.QueryAsync(new())).Items.Single();
        var source = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "flash"));
        var target = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "闪光灯"));
        await setup.Repository.AddTagsAsync([asset.AssetId], [source.TagId]);
        var merge = await setup.Repository.MergeTagsAsync(source.TagId, target.TagId);
        await setup.RestartAsync();

        Assert.IsTrue(await setup.Repository.UndoAsync(merge.UndoToken!));
        Assert.HasCount(1, await setup.Repository.ListTagMembershipsAsync(source.TagId));
        Assert.IsFalse((await setup.Repository.ListTagsAsync(includeArchived: true)).Single(x => x.TagId == source.TagId).IsArchived);
    }

    [TestMethod]
    public async Task DuplicateImportUsesContentHashUnlessIndependentWasExplicit()
    {
        await using var setup = await TestSetup.CreateAsync();
        var first = setup.WriteFile("a.jpg", "identical");
        var second = setup.WriteFile("nested/b.jpg", "identical");
        await setup.Repository.ImportAsync([new(first, ComputeContentHash: true)]);
        var skipped = await setup.Repository.ImportAsync([new(second, ComputeContentHash: true)]);
        Assert.AreEqual(1, skipped.SkippedCount);
        Assert.HasCount(1, (await setup.Repository.QueryAsync(new(PageSize: 10))).Items);

        var explicitDuplicate = await setup.Repository.ImportAsync([new(second, ComputeContentHash: true, DuplicateBehavior: AssetDuplicateBehavior.ImportIndependentRecord)]);
        Assert.AreEqual(1, explicitDuplicate.ImportedCount);
        Assert.HasCount(2, (await setup.Repository.QueryAsync(new(PageSize: 10))).Items);
        await using var connection = new SqliteConnection($"Data Source={setup.Repository.DatabasePath}"); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT NormalizedSourcePath,DuplicateDiscriminator FROM AssetItems WHERE SourcePath=$source;"; command.Parameters.AddWithValue("$source", Path.GetFullPath(second));
        await using var reader = await command.ExecuteReaderAsync(); var rows = new List<(string Path, string Discriminator)>(); while (await reader.ReadAsync()) rows.Add((reader.GetString(0), reader.GetString(1)));
        Assert.IsTrue(rows.All(row => !row.Path.Contains("|INDEPENDENT|", StringComparison.Ordinal)));
        Assert.IsTrue(rows.Any(row => row.Discriminator.Length > 0));
    }

    [TestMethod]
    public async Task ManagedCopyIsRemovedWhenDatabaseTransactionFails()
    {
        await using var setup = await TestSetup.CreateAsync();
        var source = setup.WriteFile("managed/source.jpg", "managed bytes"); var library = setup.Combine("managed-library");
        var progress = new ThrowingProgress();
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Repository.ImportAsync([new(source, AssetImportMode.ManagedCopy, library)], progress: progress));
        Assert.IsTrue(!Directory.Exists(library) || !Directory.EnumerateFiles(library).Any());
        Assert.IsEmpty((await setup.Repository.QueryAsync(new(PageSize: 10))).Items);
    }

    [TestMethod]
    public async Task MultiFolderAndBatchRatingEachCreateOneCompleteUndoOperation()
    {
        await using var setup = await TestSetup.CreateAsync();
        var paths = Enumerable.Range(0, 2).Select(index => setup.WriteFile($"batch-{index}.jpg", index.ToString())).ToArray();
        await setup.Repository.ImportAsync(paths.Select(path => new AssetImportRequest(path)));
        var assets = (await setup.Repository.QueryAsync(new(PageSize: 10))).Items; var first = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "A")); var second = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "B"));
        var folders = await setup.Repository.AddToFoldersAsync(assets.Select(asset => asset.AssetId), [first.FolderId, second.FolderId]);
        Assert.AreEqual(4, folders.ChangedCount); Assert.IsTrue(await setup.Repository.UndoAsync(folders.UndoToken!)); Assert.IsEmpty(await setup.Repository.ListFolderMembershipsAsync());
        var ratings = await setup.Repository.UpdateAssetsMetadataAsync(assets.Select(asset => asset.AssetId), rating: 5);
        Assert.AreEqual(2, ratings.ChangedCount); Assert.IsTrue(await setup.Repository.UndoAsync(ratings.UndoToken!));
        Assert.IsTrue((await setup.Repository.QueryAsync(new(PageSize: 10))).Items.All(asset => asset.Rating == 0));
    }

    [TestMethod]
    public async Task KeysetPagingTraversesAllRowsWithoutDuplicates()
    {
        await using var setup = await TestSetup.CreateAsync();
        await setup.Repository.ImportAsync(Enumerable.Range(0, 513).Select(index => new AssetImportRequest(setup.Combine($"deep-{index:0000}.asset"))));
        var ids = new HashSet<Guid>(); string? cursor = null;
        do
        {
            var page = await setup.Repository.QueryAsync(new(PageSize: 37, Cursor: cursor));
            Assert.IsTrue(page.Items.All(asset => ids.Add(asset.AssetId))); cursor = page.NextCursor;
        } while (cursor is not null);
        Assert.HasCount(513, ids);
    }

    [TestMethod]
    public async Task TagManagerBatchSearchMoveAndPartialSummaryAreDeterministic()
    {
        await using var setup = await TestSetup.CreateAsync();
        var paths = Enumerable.Range(0, 3).Select(x => setup.WriteFile($"{x}.jpg", x.ToString())).ToArray();
        await setup.Repository.ImportAsync(paths.Select(x => new AssetImportRequest(x)));
        var assets = (await setup.Repository.QueryAsync(new(PageSize: 10))).Items;
        var group = await setup.Repository.SaveTagGroupAsync(new(Guid.NewGuid(), "概念"));
        var tags = await setup.Repository.BatchCreateTagsAsync("身体, 宗教，凝视", group.TagGroupId);
        await setup.Repository.AddTagsAsync(assets.Select(x => x.AssetId), [tags[0].TagId]);
        await setup.Repository.AddTagsAsync(assets.Take(2).Select(x => x.AssetId), [tags[1].TagId]);

        var summary = await setup.Repository.GetTagUsageSummaryAsync(assets.Select(x => x.AssetId));
        Assert.IsTrue(summary.Single(x => x.Tag.TagId == tags[0].TagId).IsCommon);
        Assert.IsTrue(summary.Single(x => x.Tag.TagId == tags[1].TagId).IsPartial);
        Assert.AreEqual(tags[0].TagId, (await setup.Repository.SearchTagsAsync("身")).Single().TagId);
        var moved = await setup.Repository.MoveTagsToGroupAsync([tags[2].TagId], null);
        Assert.IsNotNull(moved.UndoToken);
        Assert.IsNull((await setup.Repository.ListTagsAsync(includeArchived: true)).Single(x => x.TagId == tags[2].TagId).TagGroupId);
    }

    [TestMethod]
    public async Task SmartFolderSupportsOneLevelOrGroupNotAndTextDateOperators()
    {
        await using var setup = await TestSetup.CreateAsync();
        var a = setup.WriteFile("Alpha.jpg", "a"); var b = setup.WriteFile("Beta.jpg", "b"); var c = setup.WriteFile("Gamma.png", "c");
        await setup.Repository.ImportAsync([new(a), new(b), new(c)]);
        var assets = (await setup.Repository.QueryAsync(new(PageSize: 10))).Items;
        foreach (var asset in assets.Where(x => x.DisplayName is "Alpha.jpg" or "Beta.jpg")) await setup.Repository.UpdateAssetMetadataAsync(asset.AssetId, rating: 4, comment: "reference");
        var groupId = Guid.NewGuid();
        var smart = await setup.Repository.SaveSmartFolderAsync(new(Guid.NewGuid(), "A/B 精选", SmartFolderLogic.And),
        [
            new(Guid.NewGuid(), Guid.Empty, SmartFolderField.FileName, SmartFolderOperator.StartsWith, "Alpha", GroupId: groupId, GroupLogic: SmartFolderLogic.Or),
            new(Guid.NewGuid(), Guid.Empty, SmartFolderField.FileName, SmartFolderOperator.StartsWith, "Beta", GroupId: groupId, GroupLogic: SmartFolderLogic.Or),
            new(Guid.NewGuid(), Guid.Empty, SmartFolderField.Rating, SmartFolderOperator.GreaterThanOrEqual, "4"),
            new(Guid.NewGuid(), Guid.Empty, SmartFolderField.Comment, SmartFolderOperator.EndsWith, "discard", Negated: true)
        ]);
        var page = await setup.Repository.QueryAsync(new(SmartFolderId: smart.SmartFolderId, PageSize: 10));
        Assert.HasCount(2, page.Items);
        Assert.IsTrue(page.Items.All(x => x.Rating == 4));
    }

    [TestMethod]
    public async Task SystemFolderCannotBeArchivedOrMadeCyclic()
    {
        await using var setup = await TestSetup.CreateAsync();
        var system = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "全部素材", IsSystem: true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Repository.ArchiveFolderAsync(system.FolderId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Repository.SaveFolderAsync(system with { ParentFolderId = system.FolderId }));
    }

    [TestMethod]
    public async Task MissingAssetCanRelinkByUniqueFileNameWithoutSourceMutation()
    {
        await using var setup = await TestSetup.CreateAsync();
        var missing = setup.Combine("old/moved.jpg");
        await setup.Repository.ImportAsync([new(missing)]);
        var newRoot = setup.Combine("new-root"); Directory.CreateDirectory(newRoot);
        var target = Path.Combine(newRoot, "moved.jpg"); await File.WriteAllTextAsync(target, "new bytes", Encoding.UTF8);
        var before = await File.ReadAllBytesAsync(target);

        var result = await setup.Repository.RelinkMissingAssetsAsync(new(newRoot));
        Assert.AreEqual(1, result.RelinkedCount);
        var asset = (await setup.Repository.QueryAsync(new(PageSize: 10))).Items.Single();
        Assert.AreEqual(Path.GetFullPath(target), asset.SourcePath);
        Assert.IsFalse(asset.IsMissing);
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(target));
    }

    [TestMethod]
    public async Task UndoJournalRetainsOnlyMostRecentOneHundredEntries()
    {
        await using var setup = await TestSetup.CreateAsync();
        var source = setup.WriteFile("undo.jpg", "bytes"); await setup.Repository.ImportAsync([new(source)]);
        var asset = (await setup.Repository.QueryAsync(new())).Items.Single();
        for (var index = 0; index < 105; index++) await setup.Repository.UpdateAssetMetadataAsync(asset.AssetId, comment: index.ToString());
        Assert.HasCount(100, await setup.Repository.ListUndoJournalAsync(100));
    }

    [TestMethod]
    public async Task IndexedPagingUsesSqlLimitAndRatingIndexPlan()
    {
        await using var setup = await TestSetup.CreateAsync();
        await setup.Repository.ImportAsync(Enumerable.Range(0, 2_000).Select(x => new AssetImportRequest(setup.Combine($"meta-{x:0000}.asset"))));
        var page = await setup.Repository.QueryAsync(new(PageSize: 64));
        Assert.HasCount(64, page.Items);
        Assert.AreEqual(2_000, page.TotalCount);
        await using var connection = new SqliteConnection($"Data Source={setup.Repository.DatabasePath}"); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "EXPLAIN QUERY PLAN SELECT AssetId FROM AssetItems WHERE Rating>=4 ORDER BY Rating;";
        var details = new List<string>(); await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) details.Add(reader.GetString(3));
        Assert.IsTrue(details.Any(x => x.Contains("IX_AssetItems_Rating", StringComparison.OrdinalIgnoreCase)), string.Join(" | ", details));
    }

    private sealed class TestSetup : IAsyncDisposable
    {
        private readonly string _root;
        private TestSetup(string root, SqliteAssetLibraryRepository repository) { _root = root; Repository = repository; }
        public SqliteAssetLibraryRepository Repository { get; private set; }
        public static async Task<TestSetup> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-AssetLibraryV15Tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "asset-library-v15.db")); await repository.InitializeAsync(); return new(root, repository);
        }
        public async Task RestartAsync() { var path = Repository.DatabasePath; await Repository.DisposeAsync(); Repository = new(path); await Repository.InitializeAsync(); }
        public string Combine(string name) => Path.Combine(_root, name.Replace('/', Path.DirectorySeparatorChar));
        public string WriteFile(string name, string content) { var path = Combine(name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content, Encoding.UTF8); return path; }
        public async ValueTask DisposeAsync() { await Repository.DisposeAsync(); try { Directory.Delete(_root, true); } catch { } }
    }

    private sealed class ThrowingProgress : IProgress<int>
    {
        public void Report(int value) => throw new InvalidOperationException("Injected failure after managed copy write.");
    }
}
