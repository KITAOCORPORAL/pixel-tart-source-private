using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class InspirationTrayServiceTests
{
    [TestMethod]
    public async Task AddRange_DeduplicatesByLibraryAndAsset_WithoutCopyingFiles()
    {
        using var temp = new TempDirectory();
        await using var service = new SqliteInspirationTrayService(temp.Combine("tray.sqlite"));
        var library = Guid.NewGuid();
        var asset = Guid.NewGuid();
        var first = new AssetLibraryStableReference(library, asset, new string('A', 64));
        var duplicateWithNewHash = new AssetLibraryStableReference(library, asset, new string('B', 64));

        var result = await service.AddRangeAsync([first, duplicateWithNewHash]);

        Assert.AreEqual(1, result.AddedCount);
        Assert.AreEqual(1, result.ExistingCount);
        Assert.HasCount(1, await service.ListAsync());
        Assert.IsFalse(File.Exists(temp.Combine("sources", "anything")));
    }

    [TestMethod]
    public async Task BatchOperations_PersistOrderAndResolutionStateAcrossRestart()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("tray.sqlite");
        var library = Guid.NewGuid();
        var refs = Enumerable.Range(0, 3).Select(i => new AssetLibraryStableReference(library, Guid.NewGuid(), i.ToString("x").PadLeft(64, '0'))).ToArray();
        Guid[] ids;
        await using (var service = new SqliteInspirationTrayService(path))
        {
            await service.AddRangeAsync(refs, "search:portrait");
            ids = (await service.ListAsync()).Select(x => x.TrayEntryId).ToArray();
            await service.ReorderAsync([ids[2], ids[0], ids[1]]);
            await service.ResolveAsync([new InspirationTrayResolution(refs[2], InspirationTrayResolutionState.AssetMissing)]);
            Assert.HasCount(3, (await service.SaveAsInspirationCollectionAsync()).Entries);
        }
        await using var restarted = new SqliteInspirationTrayService(path);
        var entries = await restarted.ListAsync();
        Assert.AreEqual(ids[2], entries[0].TrayEntryId);
        Assert.AreEqual(InspirationTrayResolutionState.AssetMissing, entries[0].ResolutionState);
        Assert.AreEqual("search:portrait", entries[0].SourceContext);
    }

    [TestMethod]
    public async Task RemoveAndClear_OnlyRemoveReferences()
    {
        using var temp = new TempDirectory();
        await using var service = new SqliteInspirationTrayService(temp.Combine("tray.sqlite"));
        var references = Enumerable.Range(0, 10).Select(i => new AssetLibraryStableReference(Guid.NewGuid(), Guid.NewGuid(), i.ToString("x").PadLeft(64, '0'))).ToArray();
        var added = await service.AddRangeAsync(references);
        Assert.AreEqual(3, await service.RemoveRangeAsync(added.AddedEntries.Take(3).Select(x => x.TrayEntryId)));
        Assert.HasCount(7, await service.ListAsync());
        Assert.AreEqual(7, await service.ClearAsync());
        Assert.IsEmpty(await service.ListAsync());
    }
}
