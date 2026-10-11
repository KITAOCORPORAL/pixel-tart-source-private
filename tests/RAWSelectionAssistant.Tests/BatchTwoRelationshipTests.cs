using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Tethering;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class BatchTwoRelationshipTests
{
    [TestMethod]
    public async Task TwoProjectsShareOneSourceAndRemovingLinkDoesNotDeleteIt()
    {
        using var temp = new TempDirectory(); var source = temp.Combine("shared.jpg"); await File.WriteAllBytesAsync(source,[1,2,3]);
        await using var repo = new SqliteAssetLibraryRepository(temp.Combine("lib.db"));
        var id = (await repo.ImportWithIdentitiesAsync([new(source, ComputeContentHash:true)])).Items.Single().AssetId;
        var one=Guid.NewGuid(); var two=Guid.NewGuid();
        await repo.SaveProjectAssetLinkAsync(new(one,id,"Reference",DateTimeOffset.UtcNow));
        await repo.SaveProjectAssetLinkAsync(new(two,id,"Reference",DateTimeOffset.UtcNow));
        Assert.HasCount(2,await repo.ListProjectAssetLinksAsync(assetId:id));
        await repo.RemoveProjectAssetLinkAsync(one,id);
        Assert.HasCount(1,await repo.ListProjectAssetLinksAsync(assetId:id)); Assert.IsTrue(File.Exists(source)); Assert.IsNotNull(await repo.GetAssetAsync(id));
    }
    [TestMethod]
    public void AnnotationConflictNeverMergesCustomerIntoPhotographerNote()
    {
        var now=DateTimeOffset.UtcNow;
        var asset=new AssetItem(Guid.NewGuid(),"fixture.jpg","fixture",".jpg","image",3,null,null,null,null,null,now,now,4,"existing") { ColorLabel="红" };
        var capture=new TetherAnnotationRecord(Guid.NewGuid(),Guid.NewGuid(),5,"蓝","incoming",now,now,ClientNote:"customer");
        var proposed=TetherAnnotationMerge.Propose(asset,capture); Assert.HasCount(3,proposed.Conflicts);
        Assert.AreEqual("existing",proposed.PhotographerNote); Assert.AreEqual("customer",proposed.ClientNote);
        var merged=TetherAnnotationMerge.Propose(asset,capture,AnnotationConflictChoice.MergeNotes);
        Assert.IsFalse(merged.PhotographerNote.Contains("customer")); Assert.AreEqual(4,merged.Rating);
        var twice=TetherAnnotationMerge.Propose(asset with { Comment=merged.PhotographerNote },capture,AnnotationConflictChoice.MergeNotes);
        Assert.AreEqual(merged.PhotographerNote,twice.PhotographerNote);
        var keep=TetherAnnotationMerge.Propose(asset,capture,AnnotationConflictChoice.KeepLibrary); Assert.AreEqual("existing",keep.PhotographerNote);
    }
    [TestMethod]
    public async Task CaptureContextIsFrozenBeforeRestart()
    {
        using var temp=new TempDirectory(); var asset=Guid.NewGuid(); var project=Guid.NewGuid(); var shot=Guid.NewGuid();
        var context=new ShootExecutionContext(project,Guid.NewGuid(),shot,[],[],null,null,null,[],null,3);
        var store=new TetherCaptureContextStore(temp.Path); await store.FreezeAsync(asset,context);
        await store.FreezeAsync(asset,context with { CurrentShotId=Guid.NewGuid(),Revision=4 });
        var reopened=await new TetherCaptureContextStore(temp.Path).LoadAsync(asset);
        Assert.AreEqual(shot,reopened!.CurrentShotId); Assert.AreEqual(3L,reopened.Revision);
    }
}
