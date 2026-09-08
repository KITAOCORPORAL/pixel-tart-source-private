using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryMergeTests
{
    [TestMethod]
    public async Task PreviewCommitAndRepeat_MergeByContentHashWithStableMappings()
    {
        using var temp = new TempDirectory();
        var containers = new AssetLibraryContainerService();
        var target = await containers.CreateAsync(temp.Combine("target.ptlibrary"), "target");
        var source = await containers.CreateAsync(temp.Combine("source.ptlibrary"), "source");
        var targetSame = temp.Combine("target-same.jpg");
        var sourceSame = temp.Combine("source-same.jpg");
        var sourceNew = temp.Combine("source-new.jpg");
        await File.WriteAllBytesAsync(targetSame, [1, 2, 3]);
        await File.WriteAllBytesAsync(sourceSame, [1, 2, 3]);
        await File.WriteAllBytesAsync(sourceNew, [4, 5, 6]);
        await using (var repository = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(target.DatabasePath)))
        {
            await repository.InitializeAsync();
            await repository.ImportAsync([new AssetImportRequest(targetSame, ComputeContentHash: true)]);
        }
        await using (var repository = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(source.DatabasePath)))
        {
            await repository.InitializeAsync();
            await repository.ImportAsync([
                new AssetImportRequest(sourceSame, ComputeContentHash: true),
                new AssetImportRequest(sourceNew, AssetImportMode.ManagedCopy, source.ManagedAssetsPath, true)]);
            var assets = (await repository.QueryAsync(new AssetLibraryQuery(PageSize: 10))).Items;
            var folder = await repository.SaveFolderAsync(new(Guid.NewGuid(), null, "参考"));
            var tag = await repository.SaveTagAsync(new(Guid.NewGuid(), "蓝色"));
            await repository.AddToFolderAsync(assets.Select(item => item.AssetId), folder.FolderId);
            await repository.AddTagsAsync(assets.Select(item => item.AssetId), [tag.TagId]);
            var smart = new SmartFolder(Guid.NewGuid(), "蓝色参考", SmartFolderLogic.And);
            await repository.SaveSmartFolderQueryDocumentAsync(smart, new AssetQueryDocument
            {
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [
                    AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, [folder.FolderId.ToString("D")]),
                    AssetQueryNode.Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, [tag.TagId.ToString("D")])
                ])
            });
        }
        var sourceBefore = await File.ReadAllBytesAsync(source.DatabasePath);
        var merger = new AssetLibraryMergeService();

        var preview = await merger.PreviewLibraryAsync(target, source);
        var result = await merger.MergeLibraryAsync(target, source);
        var repeated = await merger.MergeLibraryAsync(target, source);

        Assert.AreEqual(2, preview.SourceAssets);
        Assert.AreEqual(1, preview.NewAssets);
        Assert.AreEqual(1, preview.DuplicateAssets);
        Assert.AreEqual(1, result.AddedAssets);
        Assert.AreEqual(1, result.ReusedAssets);
        Assert.IsTrue(repeated.AlreadyApplied);
        CollectionAssert.AreEqual(sourceBefore, await File.ReadAllBytesAsync(source.DatabasePath), "源库必须保持字节不变。");
        await using var verify = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(target.DatabasePath));
        await verify.InitializeAsync();
        Assert.AreEqual(2, (await verify.QueryAsync(new AssetLibraryQuery(PageSize: 10))).TotalCount);
        Assert.HasCount(1, await verify.ListFoldersAsync());
        Assert.HasCount(1, await verify.ListTagsAsync());
        Assert.HasCount(2, await verify.ListFolderMembershipsAsync());
        Assert.HasCount(2, await verify.ListTagMembershipsAsync());
        var importedSmartFolders = await verify.ListSmartFoldersAsync();
        Assert.HasCount(1, importedSmartFolders);
        var importedSmart = importedSmartFolders[0];
        var importedDocument = await verify.GetSmartFolderQueryDocumentAsync(importedSmart.SmartFolderId);
        Assert.IsNotNull(importedDocument);
        Assert.HasCount(2, importedDocument.Document.RootGroup.Children);
    }

    [TestMethod]
    public async Task Merge_InjectedFailure_RollsBackDatabaseAndRemovesStagedFiles()
    {
        using var temp = new TempDirectory();
        var containers = new AssetLibraryContainerService();
        var target = await containers.CreateAsync(temp.Combine("target.ptlibrary"), "target");
        var source = await containers.CreateAsync(temp.Combine("source.ptlibrary"), "source");
        var image = temp.Combine("new.jpg"); await File.WriteAllBytesAsync(image, [9, 9, 9]);
        await using (var repository = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(source.DatabasePath)))
        {
            await repository.InitializeAsync();
            await repository.ImportAsync([new AssetImportRequest(image, AssetImportMode.ManagedCopy, source.ManagedAssetsPath, true)]);
        }
        var sourceBefore = await File.ReadAllBytesAsync(source.DatabasePath);

        await Assert.ThrowsExactlyAsync<InjectedMergeException>(() => new AssetLibraryMergeService().MergeLibraryAsync(
            target, source, checkpoint: point => { if (point == "relationships-inserted") throw new InjectedMergeException(); }));

        await using var verify = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(target.DatabasePath));
        await verify.InitializeAsync();
        Assert.AreEqual(0, (await verify.QueryAsync(new AssetLibraryQuery(PageSize: 10))).TotalCount);
        Assert.IsFalse(Directory.EnumerateDirectories(target.ManagedAssetsPath, "merge-*").Any());
        CollectionAssert.AreEqual(sourceBefore, await File.ReadAllBytesAsync(source.DatabasePath));
    }

    private sealed class InjectedMergeException : Exception;
}
