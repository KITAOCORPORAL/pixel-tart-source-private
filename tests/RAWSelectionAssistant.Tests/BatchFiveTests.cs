using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchFiveTests
{
    [TestMethod]
    public async Task CasJournalUndoRedoAndBackgroundCannotOverwriteUserValues()
    {
        using var temp=new TempDirectory();var file=temp.Combine("a.bin");await File.WriteAllBytesAsync(file,[1]);var db=temp.Combine("library.db");
        await using var repo=new SqliteAssetLibraryRepository(db);var id=(await repo.ImportWithIdentitiesAsync([new(file)])).Items.Single().AssetId;
        var store=new PhotographerAnnotationStore(new AssetLibraryDatabase(db));var before=await store.ReadAsync(id);var first=await store.CompareExchangeAsync(before,before with{Rating=4,Color="蓝",Note="user"});
        Assert.IsNotNull(first.UndoToken);Assert.IsTrue(await repo.UndoAsync(first.UndoToken));Assert.AreEqual(0,(await store.ReadAsync(id)).Rating);Assert.IsTrue(await repo.RedoAsync(first.UndoToken));
        await using var background=new SqliteAssetLibraryRepository(db);await background.UpdateAssetMetadataAsync(id,rating:1,comment:"background");
        await background.UpdateAssetMetadataAsync(id,rating:2,comment:"second background");Assert.AreEqual("user",(await store.ReadAsync(id)).Note);Assert.AreEqual(4,(await store.ReadAsync(id)).Rating);
        var current=await store.ReadAsync(id);await store.CompareExchangeAsync(current,current with{Rating=5});Assert.IsFalse(await repo.UndoAsync(first.UndoToken));
    }
    [TestMethod]
    public async Task BatchUndoOnlyContainsSuccesses()
    {
        using var temp=new TempDirectory();var db=temp.Combine("library.db");var path=temp.Combine("a");await File.WriteAllBytesAsync(path,[1]);await using var repo=new SqliteAssetLibraryRepository(db);var id=(await repo.ImportWithIdentitiesAsync([new(path)])).Items.Single().AssetId;
        var store=new PhotographerAnnotationStore(new AssetLibraryDatabase(db));var expected=await store.ReadAsync(id);var results=await store.ApplyBatchAsync([expected,expected with{AssetId=Guid.NewGuid()}],rating:3);
        Assert.AreEqual("Success",results[0].Status);Assert.AreEqual("Failed",results[1].Status);Assert.IsTrue(await repo.UndoAsync(results[0].UndoToken!));Assert.AreEqual(0,(await store.ReadAsync(id)).Rating);
    }
    [TestMethod]
    public async Task ManagedArtifactRecoveryRejectsTamperingAndIsRepeatable()
    {
        using var temp=new TempDirectory();var output=temp.Combine("out.png");var encoded=temp.Combine("input.tmp");await File.WriteAllBytesAsync(encoded,[1,2,3]);var id=Guid.NewGuid();var receipt=new StudioExportReceipt(id,null,null,new string('a',64),id,1,"PNG",output,"",DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<IOException>(()=>StudioExportReceiptStore.CommitOutputAsync(encoded,output,receipt,checkpoint:s=>{if(s=="Pending")throw new IOException("stop");}));
        Assert.IsFalse(File.Exists(output));Assert.AreEqual("Completed",(await ExportRecovery.RecoverAsync(output,id,receipt.SourceHash)).State);Assert.AreEqual("Completed",(await ExportRecovery.RecoverAsync(output,id,receipt.SourceHash)).State);CollectionAssert.AreEqual(new byte[]{1,2,3},await File.ReadAllBytesAsync(output));
    }
    [TestMethod]
    public async Task VersionConflictKeepsBothHistoriesAndCasRejectsStaleResolution()
    {
        using var temp=new TempDirectory();var repo=new StudioVersionRepository(temp.Path);var hash=new string('a',64);var key=StudioVersionRepository.Identity(null,null,hash);var empty=await repo.LoadAsync(key);var v=new StudioEditVersion(Guid.NewGuid(),"remote",DateTimeOffset.UtcNow,null,null,hash,null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto);var current=await repo.SaveAsync(empty,[v],v.Id);
        var local=v with{Name="local",Archived=true};var preserved=StudioVersionConflictResolution.PreserveLocalAsNew(new(current,[local],local.Id,"conflict"));Assert.HasCount(2,preserved);Assert.AreEqual(v.Id,preserved[1].OriginVersionId);Assert.IsTrue(preserved[1].Archived);
        await repo.SaveAsync(current,[v with{Name="new remote"}],v.Id);await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.SaveAsync(current,preserved,v.Id));
    }
    [TestMethod]
    public async Task ContinuousCasUndoRedoUsesUpdatedVersionFences()
    {
        using var temp=new TempDirectory();var path=temp.Combine("a");await File.WriteAllBytesAsync(path,[1]);await using var repo=new SqliteAssetLibraryRepository(temp.Combine("lib.db"));var id=(await repo.ImportWithIdentitiesAsync([new(path)])).Items.Single().AssetId;var store=new PhotographerAnnotationStore(new AssetLibraryDatabase(repo.DatabasePath));
        var zero=await store.ReadAsync(id);var one=await store.CompareExchangeAsync(zero,zero with{Rating=1});var two=await store.CompareExchangeAsync(one.Current,one.Current with{Rating=2});
        Assert.IsTrue(await repo.UndoAsync(two.UndoToken!));Assert.IsTrue(await repo.UndoAsync(one.UndoToken!));Assert.AreEqual(0,(await store.ReadAsync(id)).Rating);
        Assert.IsTrue(await repo.RedoAsync(one.UndoToken!));Assert.IsTrue(await repo.RedoAsync(two.UndoToken!));Assert.AreEqual(2,(await store.ReadAsync(id)).Rating);
    }    [TestMethod]
    public async Task RecoveryRejectsForeignArtifactAndCancelOnlyDeletesOwnedBytes()
    {
        using var temp=new TempDirectory();var output=temp.Combine("out.png");var encoded=temp.Combine("input.tmp");await File.WriteAllBytesAsync(encoded,[1,2,3]);var id=Guid.NewGuid();var receipt=new StudioExportReceipt(id,null,null,new string('a',64),id,1,"PNG",output,"",DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<IOException>(()=>StudioExportReceiptStore.CommitOutputAsync(encoded,output,receipt,checkpoint:p=>{if(p=="Pending")throw new IOException("stop");}));
        var pending=await ExportRecovery.ReadAsync(output+".kitao-export.pending.json");var foreign=temp.Combine("foreign.raw");await File.WriteAllBytesAsync(foreign,[1,2,3]);
        await File.WriteAllTextAsync(output+".kitao-export.pending.json",System.Text.Json.JsonSerializer.Serialize(pending with{ManagedArtifactPath=foreign}));
        Assert.AreEqual("Failed",(await ExportRecovery.RecoverAsync(output,id,receipt.SourceHash)).State);Assert.IsTrue(File.Exists(foreign));
        await File.WriteAllTextAsync(output+".kitao-export.pending.json",System.Text.Json.JsonSerializer.Serialize(pending));
        Assert.AreEqual("Cancelled",(await ExportRecovery.CancelAsync(output,id,true)).State);Assert.IsTrue(File.Exists(foreign));Assert.IsFalse(File.Exists(pending.ManagedArtifactPath));
        Assert.AreEqual("Cancelled",(await ExportRecovery.RecoverAsync(output,id,receipt.SourceHash)).State);
    }    [TestMethod]
    public async Task CrossLibraryCopyIsExplicitIdempotentAndKeepsOrigin()
    {
        using var temp=new TempDirectory();var repo=new StudioVersionRepository(temp.Path);var hash=new string('c',64);var source=StudioVersionRepository.Identity(null,null,hash);var version=new StudioEditVersion(Guid.NewGuid(),"look",DateTimeOffset.UtcNow,null,null,hash,null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto);await repo.SaveAsync(await repo.LoadAsync(source),[version],version.Id);
        var library=Guid.NewGuid();var asset=Guid.NewGuid();var target=await repo.LoadAsync(StudioVersionRepository.Identity(library,asset,hash));var copied=await repo.CopySelectedAsync(source,version.Id,target,library,asset,hash);Assert.HasCount(1,copied.Versions);Assert.AreEqual(version.Id,copied.Versions[0].OriginVersionId);Assert.AreEqual(library,copied.Versions[0].LibraryId);
        var repeated=await repo.CopySelectedAsync(source,version.Id,copied,library,asset,hash);Assert.HasCount(1,repeated.Versions);Assert.HasCount(1,(await repo.LoadAsync(source)).Versions);
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.CopySelectedAsync(source,version.Id,copied,library,asset,new string('d',64)));
    }    [TestMethod]
    public async Task LocalUndoAfterForeignEditDoesNotReviveOlderHistory()
    {
        using var temp=new TempDirectory();var path=temp.Combine("a");await File.WriteAllBytesAsync(path,[1]);await using var repo=new SqliteAssetLibraryRepository(temp.Combine("lib.db"));var id=(await repo.ImportWithIdentitiesAsync([new(path)])).Items.Single().AssetId;var store=new PhotographerAnnotationStore(new AssetLibraryDatabase(repo.DatabasePath));
        var a=await store.ReadAsync(id);var first=await store.CompareExchangeAsync(a,a with{Rating=1});
        var foreign=await store.CompareExchangeAsync(first.Current,first.Current with{Rating=5});
        var local=await store.CompareExchangeAsync(foreign.Current,foreign.Current with{Rating=2});Assert.IsTrue(await repo.UndoAsync(local.UndoToken!));
        Assert.IsFalse(await repo.UndoAsync(first.UndoToken!));Assert.AreEqual(5,(await store.ReadAsync(id)).Rating);
    }}
