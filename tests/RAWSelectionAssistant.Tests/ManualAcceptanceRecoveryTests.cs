using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ManualAcceptanceRecoveryTests
{
    [TestMethod]
    public async Task TrashRestoreAfterRestartPreservesMixedMembershipsAndAllSupportedSorts()
    {
        await using var setup=await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var first=await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(),null,"甲"));
        var second=await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(),null,"乙"));
        await setup.Repository.AddToFolderAsync([setup.A,setup.B],first.FolderId);
        await setup.Repository.AddToFolderAsync([setup.B,setup.C],second.FolderId);
        var membership=await setup.Repository.ListFolderMembershipsAsync();
        var before=new Dictionary<AssetLibrarySortField,Guid[]>();
        foreach(var sort in Enum.GetValues<AssetLibrarySortField>())before[sort]=(await setup.Repository.QueryAsync(new(PageSize:100,SortField:sort))).Items.Select(x=>x.AssetId).ToArray();
        await setup.Repository.SetAssetsTrashedAsync([setup.A,setup.B],true);
        await setup.RestartAsync();
        var restored=await setup.Repository.SetAssetsTrashedAsync([setup.A,setup.B],false);
        Assert.AreEqual(2,restored.ChangedCount);Assert.IsEmpty(restored.Warnings);
        CollectionAssert.AreEquivalent(membership.ToArray(),(await setup.Repository.ListFolderMembershipsAsync()).ToArray());
        foreach(var sort in before.Keys)CollectionAssert.AreEqual(before[sort],(await setup.Repository.QueryAsync(new(PageSize:100,SortField:sort))).Items.Select(x=>x.AssetId).ToArray());
        Assert.HasCount(2,(await setup.Repository.QueryAsync(new(FolderId:first.FolderId))).Items);
    }

    [TestMethod]
    public async Task RemovedFolderPromotesChildrenAndTrashRestoreExplainsFallback()
    {
        await using var setup=await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var parent=await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(),null,"原归属"));
        var child=await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(),parent.FolderId,"子文件夹",SortOrder:7));
        await setup.Repository.AddToFolderAsync([setup.A],parent.FolderId);
        await setup.Repository.AddToFolderAsync([setup.B],child.FolderId);
        await setup.Repository.SetAssetsTrashedAsync([setup.A],true);
        Assert.AreEqual(1,await setup.Repository.DeleteFolderDefinitionAsync(parent.FolderId));
        await setup.RestartAsync();
        var kept=(await setup.Repository.ListFoldersAsync()).Single(x=>x.FolderId==child.FolderId);
        Assert.IsNull(kept.ParentFolderId);Assert.AreEqual(7,kept.SortOrder);
        Assert.HasCount(1,await setup.Repository.ListFolderMembershipsAsync(folderId:child.FolderId));
        var restored=await setup.Repository.SetAssetsTrashedAsync([setup.A],false);
        StringAssert.Contains(string.Join("",restored.Warnings),"原归属");
        StringAssert.Contains(string.Join("",restored.Warnings),"未分类");
        Assert.IsTrue((await setup.Repository.QueryAsync(new(UncategorizedOnly:true))).Items.Any(x=>x.AssetId==setup.A));
    }

    [TestMethod]
    public async Task PermanentRemovalRejectsLiveAssetsAndLeavesOriginalFileBytes()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-DeleteScope",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var path=Path.Combine(root,"original.png");
        var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1ioAAAAASUVORK5CYII=");await File.WriteAllBytesAsync(path,bytes);
        await using var repository=new SqliteAssetLibraryRepository(Path.Combine(root,"assets.db"));
        await repository.ImportAsync([new AssetImportRequest(path)]);
        var id=(await repository.QueryAsync(new())).Items.Single().AssetId;
        Assert.AreEqual(0,await repository.DeleteTrashedAssetRecordsAsync([id]));
        await repository.SetAssetsTrashedAsync([id],true);
        Assert.AreEqual(1,await repository.DeleteTrashedAssetRecordsAsync([id]));
        Assert.IsEmpty((await repository.QueryAsync(AssetLibrarySystemCollections.CreateQuery(AssetLibrarySystemCollection.RecycleBin))).Items);
        CollectionAssert.AreEqual(bytes,await File.ReadAllBytesAsync(path));
        Assert.IsEmpty(await repository.ListFolderMembershipsAsync(assetId:id));
        Assert.IsFalse((await repository.ListUndoJournalAsync()).Any(row=>row.OperationKind.Contains("trash",StringComparison.OrdinalIgnoreCase)));
    }
}
