using System.IO;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchFourVersionTests
{
    [TestMethod]
    public async Task CrossSessionRepositoryReopensVersionAndRejectsConcurrentCatalog()
    {
        var root=Path.Combine(Path.GetTempPath(),"Kitao-Catalog-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var repo=new StudioVersionRepository(root);var key=StudioVersionRepository.Identity(null,null,new string('b',64));var start=await repo.LoadAsync(key);
            var v=new StudioEditVersion(Guid.NewGuid(),"edit",DateTimeOffset.UtcNow,null,null,new string('b',64),null,new([new(Guid.NewGuid(),ColorStudioNodeType.Develop,"Exposure",NumericParameters:new Dictionary<string,double>{{"exposure",.7}})]),null,ColorStudioMatchEngine.Stable,MatchV4ExecutionMode.Auto);
            await repo.SaveAsync(start,[v],v.Id);var reopened=await new StudioVersionRepository(root).LoadAsync(key);Assert.AreEqual(.7,reopened.Versions[0].Stack!.Nodes[0].NumericParameters["exposure"]);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.SaveAsync(start,[v with{Name="stale"}],v.Id));
        }finally{Directory.Delete(root,true);}
    }
}
