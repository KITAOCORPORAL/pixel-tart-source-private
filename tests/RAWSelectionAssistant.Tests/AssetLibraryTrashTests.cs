using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryTrashTests
{
    [TestMethod]
    public async Task TrashRestoreUndoRedoPersistsAndNeverMutatesReferenceSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-Trash", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.jpg");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
        var before = SHA256.HashData(await File.ReadAllBytesAsync(source));
        var database = Path.Combine(root, "library.db");
        AssetLibraryUndoToken token;
        Guid assetId;
        try
        {
            await using (var repository = new SqliteAssetLibraryRepository(database))
            {
                await repository.InitializeAsync();
                await repository.ImportAsync([new AssetImportRequest(source)]);
                var asset = (await repository.QueryAsync(new(PageSize: 10))).Items.Single(); assetId = asset.AssetId;
                await repository.SetAssetsArchivedAsync([assetId], true);
                var trashed = await repository.SetAssetsTrashedAsync([assetId], true); token = trashed.UndoToken!;
                Assert.HasCount(1, await repository.ListTrashEntriesAsync());
                Assert.HasCount(1, (await repository.QueryAsync(new(SystemCollection: AssetLibrarySystemCollection.RecycleBin, PageSize: 10))).Items);
            }
            await using (var reopened = new SqliteAssetLibraryRepository(database))
            {
                await reopened.InitializeAsync();
                Assert.HasCount(1, await reopened.ListTrashEntriesAsync());
                Assert.IsTrue(await reopened.UndoAsync(token));
                Assert.IsEmpty(await reopened.ListTrashEntriesAsync());
                Assert.IsTrue((await reopened.GetAssetAsync(assetId))!.IsArchived);
                Assert.IsTrue(await reopened.RedoAsync(token));
                Assert.HasCount(1, await reopened.ListTrashEntriesAsync());
                await reopened.SetAssetsTrashedAsync([assetId], false);
                Assert.IsTrue((await reopened.GetAssetAsync(assetId))!.IsArchived);
            }
            CollectionAssert.AreEqual(before, SHA256.HashData(await File.ReadAllBytesAsync(source)));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
