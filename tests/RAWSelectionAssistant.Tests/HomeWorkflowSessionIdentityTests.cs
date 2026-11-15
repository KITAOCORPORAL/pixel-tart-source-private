using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class HomeWorkflowSessionIdentityTests
{
    [TestMethod]
    public async Task SessionSaveReopenPreservesExplicitWorkflowIdentity()
    {
        var path=Path.Combine(Path.GetTempPath(),"Guangjing-Workflow-"+Guid.NewGuid()+".json");
        try{var id=Guid.NewGuid();var document=new ColorStudioSessionDocument([],null){WorkflowProjectId=id};
            await ColorStudioSessionStore.SaveAsync(path,document);var loaded=await ColorStudioSessionStore.LoadAsync(path);Assert.AreEqual(id,loaded.WorkflowProjectId);}
        finally{if(File.Exists(path))File.Delete(path);}
    }
}
