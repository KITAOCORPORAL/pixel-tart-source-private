using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class OnlineHttpResultGuardTests
{
 [TestMethod][DataRow("project")][DataRow("asset")][DataRow("version")][DataRow("revision")][DataRow("lock")]
 public Task InvalidRemoteResultDoesNotReplaceSavedConfirmation(string bad)=>RunSta(async()=>
 {
  var project=SelectionProjectFactory.CreateDraft("guard","synthetic",1);var asset=new SelectionAsset(Guid.NewGuid(),project.Id,"synthetic.jpg","",null,SelectionAssetStatus.Ready,0,false,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,CloudAssetId:"confirmed");var rule=SelectionRule.Default(project.Id,1);var good=new SelectionFinalItem(project.Id,asset.Id,asset.OriginalFileName,true,false,null,false);var previous=new SelectionFinalResult(project.Id,DateTimeOffset.UtcNow,[good]){SelectionVersion=1,Revision=8,IsLocked=true};
  var malformed=previous with{Revision=9};if(bad=="project")malformed=malformed with{SelectionProjectId=Guid.NewGuid()};if(bad=="asset")malformed=malformed with{Items=[good with{ImageId=Guid.NewGuid()}]};if(bad=="version")malformed=malformed with{SelectionVersion=2};if(bad=="revision")malformed=malformed with{Revision=7};if(bad=="lock")malformed=malformed with{IsLocked=false};
  var store=new InMemorySelectionWorkspaceStore(new([project],[asset],[rule],[previous]));var page=new OnlineSelectionProjectViewModel(new GuardOnlyProvider(malformed),store,new SelectionResultSyncService(new FileNameNormalizer()));await page.OpenProjectAsync(project,rule,[asset],previous);await page.RefreshCustomerResultsAsync();Assert.AreSame(previous,page.FinalResult);Assert.AreEqual(8L,(await store.LoadAsync()).FinalResults.Single().Revision);Assert.IsTrue(page.StatusText.Contains("无效"));
 });
 [TestMethod]public Task ValidConfirmedKnownIdsPersistAfterValidation()=>RunSta(async()=>
 {
  var p=SelectionProjectFactory.CreateDraft("guard","synthetic",1);var a=new SelectionAsset(Guid.NewGuid(),p.Id,"local-name.jpg","",null,SelectionAssetStatus.Ready,0,false,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,CloudAssetId:"confirmed");var r=SelectionRule.Default(p.Id,1);var remote=new SelectionFinalResult(p.Id,DateTimeOffset.UtcNow,[new(p.Id,a.Id,"remote-name-not-used-for-matching",true,false,null,false)]){SelectionVersion=1,Revision=9,IsLocked=true};var store=new InMemorySelectionWorkspaceStore(new([p],[a],[r],[]));var page=new OnlineSelectionProjectViewModel(new GuardOnlyProvider(remote),store,new SelectionResultSyncService(new FileNameNormalizer()));await page.OpenProjectAsync(p,r,[a]);await page.RefreshCustomerResultsAsync();var saved=(await store.LoadAsync()).FinalResults.Single();Assert.AreEqual(9L,saved.Revision);Assert.AreEqual("local-name.jpg",saved.Items.Single().OriginalFileName);Assert.AreEqual(1,page.SelectedCount);
 });
 // This fake is only for malformed-response guards; real HTTP acceptance uses the running service separately.
 private sealed class GuardOnlyProvider(SelectionFinalResult result):IHttpOnlineSelectionWorkflow
 {
  private readonly NoneOnlineSelectionProvider none=new();public OnlineSelectionProviderKind Kind=>OnlineSelectionProviderKind.LocalDev;public bool IsConfigured=>true;public string DisplayName=>"guard unit fake";public string? PublishedLink(Guid id)=>null;public int? ExpectedSelectionVersion(Guid id)=>1;
  public Task<OnlineSelectionProviderResult<SelectionFinalResult>> GetFinalSelectionAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(OnlineSelectionProviderResult<SelectionFinalResult>.Completed(result));
  public Task<OnlineSelectionProviderResult<SelectionProject>> CreateProjectAsync(SelectionProject p,CancellationToken ct=default)=>none.CreateProjectAsync(p,ct);public Task<OnlineSelectionProviderResult<SelectionProject>> UpdateProjectAsync(SelectionProject p,CancellationToken ct=default)=>none.UpdateProjectAsync(p,ct);public Task<OnlineSelectionProviderResult<SelectionProject>> GetProjectAsync(Guid id,CancellationToken ct=default)=>none.GetProjectAsync(id,ct);
  public Task<OnlineSelectionProviderResult<SelectionAsset>> UploadAssetAsync(Guid id,SelectionAsset a,Stream s,IProgress<SelectionUploadProgress>? progress=null,CancellationToken ct=default)=>none.UploadAssetAsync(id,a,s,progress,ct);public Task<OnlineSelectionProviderResult<SelectionPublish>> PublishProjectAsync(Guid id,SelectionPublish p,CancellationToken ct=default)=>none.PublishProjectAsync(id,p,ct);public Task<OnlineSelectionProviderResult<SelectionPublish>> PublishWithRuleAsync(Guid id,SelectionPublish p,SelectionRule r,CancellationToken ct=default)=>none.PublishProjectAsync(id,p,ct);public Task<OnlineSelectionProviderResult<SelectionProgress>> GetSelectionProgressAsync(Guid id,CancellationToken ct=default)=>none.GetSelectionProgressAsync(id,ct);public Task<OnlineSelectionProviderResult<bool>> UnpublishAsync(Guid id,CancellationToken ct=default)=>none.UnpublishAsync(id,ct);public Task<OnlineSelectionProviderResult<bool>> DeleteCloudAssetAsync(Guid id,Guid a,CancellationToken ct=default)=>none.DeleteCloudAssetAsync(id,a,ct);
 }
}
