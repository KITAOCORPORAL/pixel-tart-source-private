using System.Text;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryV1Tests
{
    [TestMethod]
    public async Task ReferenceAndManagedCopyImportDoNotMutateSourceBytes()
    {
        await using var setup = await TestSetup.CreateAsync();
        var source = setup.WriteFile("DSC0001.JPG", "source-bytes");
        var before = await File.ReadAllBytesAsync(source);

        var reference = await setup.Repository.ImportAsync([new(source, AssetImportMode.Reference, ComputeContentHash: true)]);
        var managed = await setup.Repository.ImportAsync([new(source, AssetImportMode.ManagedCopy, setup.Combine("library"))]);

        Assert.AreEqual(1, reference.ImportedCount);
        Assert.AreEqual(0, reference.MissingCount);
        Assert.AreEqual(1, managed.SkippedCount);
        var item = (await setup.Repository.QueryAsync(new(PageSize: 10))).Items.Single();
        Assert.AreEqual(AssetImportMode.ManagedCopy, item.ImportMode);
        Assert.IsTrue(File.Exists(item.ManagedCopyPath));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(source));
    }

    [TestMethod]
    public async Task AssetCanBelongToMultipleFoldersWithoutCopyingFile()
    {
        await using var setup = await TestSetup.CreateAsync();
        var source = setup.WriteFile("portrait.ARW", "raw");
        await setup.Repository.ImportAsync([new(source)]);
        var asset = (await setup.Repository.QueryAsync(new())).Items.Single();
        var first = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "人物"));
        var second = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "商业"));

        var result = await setup.Repository.AddToFolderAsync([asset.AssetId], first.FolderId);
        await setup.Repository.AddToFolderAsync([asset.AssetId], second.FolderId);
        Assert.AreEqual(1, result.ChangedCount);
        var memberships = await setup.Repository.ListFolderMembershipsAsync(assetId: asset.AssetId);
        Assert.HasCount(2, memberships);
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task TagMergeMigratesMembershipsAndUndoRestoresSourceTag()
    {
        await using var setup = await TestSetup.CreateAsync();
        var a = setup.WriteFile("a.jpg", "a");
        var b = setup.WriteFile("b.jpg", "b");
        await setup.Repository.ImportAsync([new(a), new(b)]);
        var assets = (await setup.Repository.QueryAsync(new(PageSize: 10))).Items;
        var source = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "flash"));
        var target = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "闪光灯"));
        await setup.Repository.AddTagsAsync(assets.Select(x => x.AssetId), [source.TagId]);
        await setup.Repository.AddTagsAsync([assets[0].AssetId], [target.TagId]);

        var merge = await setup.Repository.MergeTagsAsync(source.TagId, target.TagId);
        Assert.AreEqual(2, merge.ChangedCount);
        Assert.HasCount(2, await setup.Repository.ListTagMembershipsAsync(target.TagId));
        Assert.IsTrue((await setup.Repository.ListTagsAsync(includeArchived: true)).Single(x => x.TagId == source.TagId).IsArchived);

        Assert.IsTrue(await setup.Repository.UndoAsync(merge.UndoToken!));
        Assert.HasCount(2, await setup.Repository.ListTagMembershipsAsync(source.TagId));
        Assert.HasCount(1, await setup.Repository.ListTagMembershipsAsync(target.TagId));
        Assert.IsFalse((await setup.Repository.ListTagsAsync(includeArchived: true)).Single(x => x.TagId == source.TagId).IsArchived);
    }

    [TestMethod]
    public async Task InvalidRegexReturnsUserReadableErrorWithoutThrowing()
    {
        await using var setup = await TestSetup.CreateAsync();
        var source = setup.WriteFile("DSC0002.JPG", "bytes");
        await setup.Repository.ImportAsync([new(source)]);
        var page = await setup.Repository.QueryAsync(new(FileNameRegex: "[", PageSize: 20));
        Assert.IsFalse(string.IsNullOrWhiteSpace(page.RegexError));
        Assert.IsEmpty(page.Items);
    }

    [TestMethod]
    public async Task UncategorizedAndUntaggedQueriesAreIndependent()
    {
        await using var setup = await TestSetup.CreateAsync();
        var paths = Enumerable.Range(1, 3).Select(i => setup.WriteFile($"{i}.jpg", i.ToString())).ToArray();
        await setup.Repository.ImportAsync(paths.Select(x => new AssetImportRequest(x)));
        var assets = (await setup.Repository.QueryAsync(new(PageSize: 10))).Items;
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "已分类"));
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "有标签"));
        await setup.Repository.AddToFolderAsync([assets[0].AssetId], folder.FolderId);
        await setup.Repository.AddTagsAsync([assets[1].AssetId], [tag.TagId]);

        Assert.HasCount(2, (await setup.Repository.QueryAsync(new(UncategorizedOnly: true, PageSize: 10))).Items);
        Assert.HasCount(2, (await setup.Repository.QueryAsync(new(UntaggedOnly: true, PageSize: 10))).Items);
        Assert.HasCount(1, (await setup.Repository.QueryAsync(new(UncategorizedOnly: true, UntaggedOnly: true, PageSize: 10))).Items);
    }

    [TestMethod]
    public async Task PagingCursorIsDeterministicAndCancellationIsHonored()
    {
        await using var setup = await TestSetup.CreateAsync();
        var requests = Enumerable.Range(0, 37).Select(i => new AssetImportRequest(setup.WriteFile($"asset-{i:000}.jpg", "x")));
        await setup.Repository.ImportAsync(requests);
        var first = await setup.Repository.QueryAsync(new(PageSize: 7));
        Assert.HasCount(7, first.Items);
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.NextCursor));
        var second = await setup.Repository.QueryAsync(new(PageSize: 7, Cursor: first.NextCursor));
        Assert.HasCount(7, second.Items);
        Assert.IsFalse(first.Items.Select(x => x.AssetId).Intersect(second.Items.Select(x => x.AssetId)).Any());

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => setup.Repository.QueryAsync(new(PageSize: 10), cancellation.Token));
    }

    [TestMethod]
    public async Task ImportCancellationIsAtomicAndLeavesSourceUntouched()
    {
        await using var setup = await TestSetup.CreateAsync();
        var source = setup.WriteFile("cancel.jpg", "unchanged");
        var before = await File.ReadAllBytesAsync(source);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await setup.Repository.ImportAsync([new(source)], cancellation.Token);

        Assert.IsTrue(result.Cancelled);
        Assert.AreEqual(0, result.ImportedCount);
        Assert.IsEmpty((await setup.Repository.QueryAsync(new(PageSize: 10))).Items);
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(source));
    }

    [TestMethod]
    public async Task SmartFolderSupportsAndAndNegatedTagRule()
    {
        await using var setup = await TestSetup.CreateAsync();
        var first = setup.WriteFile("DSC0100.JPG", "first");
        var second = setup.WriteFile("DSC0200.JPG", "second");
        await setup.Repository.ImportAsync([new(first), new(second)]);
        var assets = (await setup.Repository.QueryAsync(new(PageSize: 10))).Items;
        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "人物"));
        await setup.Repository.AddTagsAsync([assets[0].AssetId], [tag.TagId]);
        var smart = await setup.Repository.SaveSmartFolderAsync(
            new(Guid.NewGuid(), "DSC 人物", SmartFolderLogic.And),
            [new(Guid.NewGuid(), Guid.Empty, SmartFolderField.FileName, SmartFolderOperator.Regex, "DSC0[0-9]+"), new(Guid.NewGuid(), Guid.Empty, SmartFolderField.Tag, SmartFolderOperator.Equals, "人物")]);
        var rules = (await setup.Repository.ListSmartFolderRulesAsync(smart.SmartFolderId)).Select(x => x with { SmartFolderId = smart.SmartFolderId }).ToArray();
        await setup.Repository.SaveSmartFolderAsync(smart, rules);
        var page = await setup.Repository.QueryAsync(new(SmartFolderId: smart.SmartFolderId, PageSize: 10));
        Assert.HasCount(1, page.Items);
        Assert.AreEqual(assets[0].AssetId, page.Items[0].AssetId);
    }

    [TestMethod]
    public async Task InvalidSmartFolderRegexReturnsErrorInsteadOfCrashing()
    {
        await using var setup = await TestSetup.CreateAsync();
        var smart = await setup.Repository.SaveSmartFolderAsync(new(Guid.NewGuid(), "坏规则"), [new(Guid.NewGuid(), Guid.Empty, SmartFolderField.FileName, SmartFolderOperator.Regex, "[")]);
        var page = await setup.Repository.QueryAsync(new(SmartFolderId: smart.SmartFolderId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(page.RegexError));
    }

    [TestMethod]
    public async Task OneHundredThousandMetadataRecordsRemainPageableWithoutMediaBytes()
    {
        await using var setup = await TestSetup.CreateAsync();
        var requests = Enumerable.Range(0, 100_000)
            .Select(index => new AssetImportRequest(setup.Combine($"synthetic-{index:000000}.metadata")))
            .ToArray();

        var result = await setup.Repository.ImportAsync(requests);

        Assert.AreEqual(100_000, result.ImportedCount);
        var first = await setup.Repository.QueryAsync(new(PageSize: 128));
        Assert.HasCount(128, first.Items);
        Assert.AreEqual(100_000, first.TotalCount);
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.NextCursor));
        Assert.IsFalse(first.Items.Any(item => File.Exists(item.SourcePath)));
        var second = await setup.Repository.QueryAsync(new(PageSize: 128, Cursor: first.NextCursor));
        Assert.HasCount(128, second.Items);
        Assert.IsFalse(first.Items.Select(item => item.AssetId).Intersect(second.Items.Select(item => item.AssetId)).Any());
    }

    private sealed class TestSetup : IAsyncDisposable
    {
        private readonly string _root;
        private TestSetup(string root, SqliteAssetLibraryRepository repository) { _root = root; Repository = repository; }
        public SqliteAssetLibraryRepository Repository { get; }
        public static async Task<TestSetup> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-AssetLibraryTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "asset-library.db")); await repository.InitializeAsync(); return new(root, repository);
        }
        public string Combine(string name) => Path.Combine(_root, name);
        public string WriteFile(string name, string content) { var path = Combine(name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content, Encoding.UTF8); return path; }
        public async ValueTask DisposeAsync() { await Repository.DisposeAsync(); try { Directory.Delete(_root, true); } catch { } }
    }
}
