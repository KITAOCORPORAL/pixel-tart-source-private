using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchFourPersistenceTests
{
    [TestMethod]
    public async Task CatalogSurvivesOtherSessionAndRejectsStaleWrite()
    {
        using var temp=new TempDirectory();var a=new StudioVersionRepository(temp.Path);var b=new StudioVersionRepository(temp.Path);
        var key=StudioVersionRepository.Identity(null,null,new string('a',64));var initial=await a.LoadAsync(key);
        var version=new StudioEditVersion(Guid.NewGuid(),"default",DateTimeOffset.UtcNow,null,null,new string('a',64),null,null,null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto);
        var saved=await a.SaveAsync(initial,[version],version.Id);Assert.AreEqual(version.Id,(await b.LoadAsync(key)).SelectedId);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>b.SaveAsync(initial,[version with{Name="stale"}],version.Id));
        Assert.AreEqual("default",(await b.LoadAsync(key)).Versions[0].Name);
        Assert.AreNotEqual(key,StudioVersionRepository.Identity(Guid.NewGuid(),Guid.NewGuid(),new string('a',64)));
    }
    [TestMethod]
    public async Task ReceiptFailureCanRecoverOnlyMatchingOutputWithoutReencoding()
    {
        using var temp=new TempDirectory();var output=temp.Combine("out.png");await File.WriteAllBytesAsync(output,[1,2,3]);
        var id=Guid.NewGuid();var receipt=new StudioExportReceipt(Guid.NewGuid(),null,null,new string('a',64),id,1,"legacy",output,"",DateTimeOffset.UtcNow,new(1,"PNG",8,"Original",1,1,null,null,"Default","sRGB","None","None"));
        Directory.CreateDirectory(output+".kitao-export.json");
        await Assert.ThrowsAsync<IOException>(()=>StudioExportReceiptStore.WriteAsync(output,receipt));
        Assert.IsTrue(File.Exists(output+".kitao-export.pending.json"));
        Assert.AreEqual("Conflict",(await ExportRecovery.RecoverAsync(output,Guid.NewGuid(),receipt.SourceHash)).State);
        Directory.Delete(output+".kitao-export.json");
        Assert.AreEqual("Completed",(await ExportRecovery.RecoverAsync(output,id,receipt.SourceHash)).State);
        Assert.AreEqual("Completed",(await ExportRecovery.ReadAsync(output+".kitao-export.json")).CompletionState);
        CollectionAssert.AreEqual(new byte[]{1,2,3},await File.ReadAllBytesAsync(output));
    }
    [TestMethod]
    public async Task PublishingResumesReceiptWithoutSecondRender()
    {
        using var temp=new TempDirectory();var source=temp.Combine("source.jpg");await File.WriteAllBytesAsync(source,[7,8,9]);var output=temp.Combine("out");Directory.CreateDirectory(output);
        var renderer=new FailingReceiptRenderer();var service=new RAWSelectionAssistant.Core.Services.Publishing.PublishingExportService(renderer,path=>{if(renderer.Renders==1)Directory.CreateDirectory(path+".kitao-export.json");});
        var request=new RAWSelectionAssistant.Core.Models.PublishingExportRequest([source],output,new());
        var first=await service.ExportAsync(Guid.NewGuid(),request);Assert.AreEqual(RAWSelectionAssistant.Core.Models.PublishingItemState.Partial,first.Items[0].State);
        var target=first.Items[0].DestinationPath!;Directory.Delete(target+".kitao-export.json");
        var retry=await service.ExportAsync(Guid.NewGuid(),request);Assert.AreEqual(RAWSelectionAssistant.Core.Models.PublishingItemState.Completed,retry.Items[0].State);Assert.AreEqual(1,renderer.Renders);
    }
    [TestMethod]
    public async Task UndoCannotOverwriteAnAbaAnnotation()
    {
        using var temp=new TempDirectory();var file=temp.Combine("a.bin");await File.WriteAllBytesAsync(file,[1]);
        await using var repo=new RAWSelectionAssistant.Core.Services.AssetLibrary.SqliteAssetLibraryRepository(temp.Combine("library.db"));
        var id=(await repo.ImportWithIdentitiesAsync([new(file)])).Items.Single().AssetId;
        var change=await repo.UpdateAssetMetadataAsync(id,rating:4);var store=new RAWSelectionAssistant.Core.Services.AssetLibrary.PhotographerAnnotationStore(new RAWSelectionAssistant.Core.Services.AssetLibrary.AssetLibraryDatabase(repo.DatabasePath));
        var current=await store.ReadAsync(id);var other=await store.CompareExchangeAsync(current,current with{Rating=2});await store.CompareExchangeAsync(other.Current,other.Current with{Rating=4});
        Assert.IsFalse(await repo.UndoAsync(change.UndoToken!));Assert.AreEqual(4,(await store.ReadAsync(id)).Rating);
    }
    private sealed class FailingReceiptRenderer:RAWSelectionAssistant.Core.Services.Publishing.IPublishingRenderer
    {
        public int Renders;private string? _destination;
        public Task RenderAsync(string source,string destination,RAWSelectionAssistant.Core.Models.PublishingOptions options,CancellationToken token=default)
        {Renders++;File.Copy(source,destination);var marker=destination.LastIndexOf('.',destination.Length-".publishing".Length-1);_destination=destination[..marker];return Task.CompletedTask;}
                public Task VerifyAsync(string path,CancellationToken token=default)=>Task.CompletedTask;
    }}
