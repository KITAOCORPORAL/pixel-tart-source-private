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

    [TestMethod]
    public async Task Collections_CreateEditMembershipAndPersistAcrossRestart()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("tray.sqlite");
        Guid collectionId;
        Guid[] entryIds;
        await using (var service = new SqliteInspirationTrayService(path))
        {
            var refs = Enumerable.Range(0, 3).Select(_ => new AssetLibraryStableReference(Guid.NewGuid(), Guid.NewGuid(), new string('a', 64))).ToArray();
            var added = await service.AddRangeAsync(refs);
            entryIds = added.AddedEntries.Select(x => x.TrayEntryId).ToArray();
            var created = await service.CreateCollectionAsync("Weekend selects", Guid.NewGuid());
            collectionId = created.CollectionId;
            Assert.AreEqual(0, created.EntryCount);
            Assert.AreEqual(2, await service.AddEntriesToCollectionAsync(collectionId, entryIds.Take(2)));
            Assert.AreEqual(1, await service.RemoveEntriesFromCollectionAsync(collectionId, [entryIds[0]]));
            Assert.AreEqual(1, await service.RenameCollectionAsync(collectionId, "Final selects"));
            Assert.AreEqual(1, (await service.ListCollectionsAsync()).Single().EntryCount);
            Assert.AreEqual(entryIds[1], (await service.ListCollectionEntriesAsync(collectionId)).Single().TrayEntryId);
        }
        await using var restarted = new SqliteInspirationTrayService(path);
        var listed = await restarted.ListCollectionsAsync();
        Assert.HasCount(1, listed);
        Assert.AreEqual("Final selects", listed[0].Name);
        Assert.AreEqual(1, listed[0].EntryCount);
        Assert.AreEqual(1, await restarted.ArchiveCollectionAsync(collectionId));
        Assert.IsEmpty(await restarted.ListCollectionsAsync());
    }

    [TestMethod]
    public async Task CollectionDragSemantics_MoveAndManualReorderPersistAcrossRestart()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("tray.sqlite");
        Guid sourceId;
        Guid targetId;
        Guid[] entryIds;
        await using (var service = new SqliteInspirationTrayService(path))
        {
            var refs = Enumerable.Range(0, 4)
                .Select(i => new AssetLibraryStableReference(Guid.NewGuid(), Guid.NewGuid(), i.ToString("x").PadLeft(64, '0')))
                .ToArray();
            entryIds = (await service.AddRangeAsync(refs)).AddedEntries.Select(entry => entry.TrayEntryId).ToArray();
            sourceId = (await service.CreateCollectionAsync("Source")).CollectionId;
            targetId = (await service.CreateCollectionAsync("Target")).CollectionId;
            await service.AddEntriesToCollectionAsync(sourceId, entryIds);

            await service.AddEntriesToCollectionAsync(targetId, entryIds.Take(2));
            await service.RemoveEntriesFromCollectionAsync(sourceId, entryIds.Take(2));
            await service.ReorderCollectionAsync(targetId, [entryIds[1], entryIds[0]]);
            await service.ReorderAsync([entryIds[3], entryIds[2], entryIds[1], entryIds[0]]);
        }

        await using var restarted = new SqliteInspirationTrayService(path);
        CollectionAssert.AreEqual(new[] { entryIds[1], entryIds[0] },
            (await restarted.ListCollectionEntriesAsync(targetId)).Select(entry => entry.TrayEntryId).ToArray());
        CollectionAssert.AreEqual(new[] { entryIds[2], entryIds[3] },
            (await restarted.ListCollectionEntriesAsync(sourceId)).Select(entry => entry.TrayEntryId).ToArray());
        CollectionAssert.AreEqual(new[] { entryIds[3], entryIds[2], entryIds[1], entryIds[0] },
            (await restarted.ListAsync()).Select(entry => entry.TrayEntryId).ToArray());
    }
}
