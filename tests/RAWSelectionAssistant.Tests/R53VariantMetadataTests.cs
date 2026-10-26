using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class R53VariantMetadataTests
{
    static StudioEditVersion Version(Guid id,string hash)=>new(id,"合成变体",DateTimeOffset.UtcNow,null,null,hash,null,new([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"基础",NumericParameters:new Dictionary<string,double>{{"exposure",.4}})]),null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto);
    [TestMethod]public async Task CatalogPersistsOriginalMainRatingAndNodeLockWithoutChangingPixelParameters()
    {
        using var t=new TempDirectory();var repo=new StudioVersionRepository(t.Combine("catalog"));var hash=new string('a',64);var identity=StudioVersionRepository.Identity(null,null,hash);var catalog=await repo.LoadAsync(identity);var original=Version(Guid.NewGuid(),hash) with{IsOriginal=true,Stack=null};var edit=Version(Guid.NewGuid(),hash) with{IsMain=true,MainExplicit=true,Rating=4,ColorLabel="绿"};edit=edit with{Stack=edit.Stack! with{Nodes=edit.Stack.Nodes.Select(n=>n with{SystemLocked=true,UserLocked=true}).ToArray()}};
        await repo.SaveAsync(catalog,[original,edit],edit.Id);var reopened=await new StudioVersionRepository(t.Combine("catalog")).LoadAsync(identity);Assert.IsTrue(reopened.Versions[0].IsOriginal);Assert.IsTrue(reopened.Versions[1].IsMain);Assert.AreEqual(4,reopened.Versions[1].Rating);Assert.AreEqual("绿",reopened.Versions[1].ColorLabel);Assert.IsTrue(reopened.Versions[1].Stack!.Nodes.Single().SystemLocked);Assert.AreEqual(.4,reopened.Versions[1].Stack!.Nodes.Single().NumericParameters["exposure"]);
    }
    [TestMethod]public async Task InvalidOriginalDeleteAndTwoMainsRejectWithoutChangingCatalog()
    {
        using var t=new TempDirectory();var repo=new StudioVersionRepository(t.Combine("catalog"));var hash=new string('b',64);var start=await repo.LoadAsync(StudioVersionRepository.Identity(null,null,hash));var v=Version(Guid.NewGuid(),hash) with{IsOriginal=true};await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>repo.SaveAsync(start,[v with{Archived=true}],null));await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>repo.SaveAsync(start,[v with{IsMain=true},Version(Guid.NewGuid(),hash) with{IsMain=true}],v.Id));Assert.AreEqual(0,(await repo.LoadAsync(start.Identity)).Revision);
    }
    [TestMethod]public void ConflictRecoveryClonesNeverCreateSecondOriginalOrMain()
    {
        var hash=new string('c',64);var v=Version(Guid.NewGuid(),hash) with{IsOriginal=true,IsMain=true,MainExplicit=true};var local=v with{Name="本地更名"};var result=StudioVersionConflictResolution.PreserveLocalAsNew(new(new("synthetic",1,[v],v.Id),[local],local.Id,"合成冲突"));Assert.HasCount(2,result);Assert.HasCount(1,result.Where(x=>x.IsOriginal));Assert.HasCount(1,result.Where(x=>x.IsMain));Assert.AreEqual(v.Id,result[1].OriginVersionId);
    }
}
