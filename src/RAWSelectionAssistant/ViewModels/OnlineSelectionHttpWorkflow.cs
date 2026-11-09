using RAWSelectionAssistant.Utilities;
using System.Windows;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Core.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class OnlineSelectionProjectViewModel
{
 public ICommand UploadProxiesCommand{get;private set;}=null!;public ICommand RefreshCustomerResultsCommand{get;private set;}=null!;public ICommand CopyPublishedLinkCommand{get;private set;}=null!;public ICommand UnpublishCommand{get;private set;}=null!;
 public Func<SelectionProject,SelectionFinalResult,IReadOnlyList<SelectionAsset>,string,Task<Guid>>? WorkflowHandoff{get;set;}
 public int CloudConfirmedCount=>Assets.Count(a=>a.Status==SelectionAssetStatus.Ready&&!string.IsNullOrWhiteSpace(a.CloudAssetId));
 public string UploadSummary=>$"本地代理 {Assets.Count(a=>!string.IsNullOrWhiteSpace(a.ProxyJpegPath))} 张 · 服务确认 {CloudConfirmedCount} 张";
 public string? PublishedLink=>(_provider as IHttpOnlineSelectionWorkflow)?.PublishedLink(Project?.Id??Guid.Empty);
 private void InitializeHttpCommands(){UploadProxiesCommand=new AsyncRelayCommand(_=>UploadProxiesAsync(),_=>!IsBusy&&IsProjectOpen&&FinalResult?.IsLocked!=true&&_provider is IHttpOnlineSelectionWorkflow);RefreshCustomerResultsCommand=new AsyncRelayCommand(_=>RefreshCustomerResultsAsync(),_=>!IsBusy&&IsProjectOpen&&_provider is IHttpOnlineSelectionWorkflow);CopyPublishedLinkCommand=new RelayCommand(_=>{if(PublishedLink is not string link){StatusText="没有有效的服务签发链接。";return;}try{Clipboard.SetText(link);StatusText="服务签发的受保护链接已复制。";}catch(System.Runtime.InteropServices.ExternalException){StatusText="复制未完成，请重试。";}},_=>!IsBusy&&PublishedLink is not null);UnpublishCommand=new AsyncRelayCommand(_=>UnpublishHttpAsync(),_=>!IsBusy&&IsProjectOpen&&_provider is IHttpOnlineSelectionWorkflow&&PublishedLink is not null);}
 private void RefreshHttpCommandStates(){OnPropertyChanged(nameof(UploadSummary));OnPropertyChanged(nameof(CloudConfirmedCount));OnPropertyChanged(nameof(PublishedLink));(UploadProxiesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();(RefreshCustomerResultsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();(CopyPublishedLinkCommand as RelayCommand)?.RaiseCanExecuteChanged();(UnpublishCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();}
 public async Task<bool> EnsureRemoteProjectAsync(CancellationToken ct=default){if(Project is null||_provider is not IHttpOnlineSelectionWorkflow)return false;await SaveAsync(ct);var local=Project;var result=await _provider.CreateProjectAsync(local,ct);if(Project?.Id!=local.Id)return false;if(!result.Success||result.Value is null){StatusText=result.Message;await SaveAsync(ct);return false;}if(result.Value.Id!=local.Id){StatusText="远端项目身份不匹配，未替换本地项目。";return false;}Project=result.Value with{LocalSourceDirectory=local.LocalSourceDirectory,WorkflowProjectId=local.WorkflowProjectId};await SaveAsync(ct);return true;}
 public async Task UploadProxiesAsync(CancellationToken ct=default)
 {
  if(Project is null||Rule is null||_provider is not IHttpOnlineSelectionWorkflow)return;IsBusy=true;
  try{if(!await EnsureRemoteProjectAsync(ct))return;var project=Project;var rule=Rule;var final=FinalResult;var originalAssets=Assets.Select(x=>x.ToModel()).ToArray();var queue=new SelectionUploadQueue(_provider);foreach(var asset in originalAssets.Where(a=>!string.IsNullOrWhiteSpace(a.ProxyJpegPath)&&string.IsNullOrWhiteSpace(a.CloudAssetId)))queue.Enqueue(asset);await queue.RunAsync(ct);var results=queue.Items.ToDictionary(x=>x.AssetId,x=>x.Asset);var snapshot=originalAssets.Select(x=>results.GetValueOrDefault(x.Id,x)).ToArray();await MergeAndSaveAsync(project,rule,snapshot,final,ct);if(Project?.Id!=project.Id)return;foreach(var item in Assets)if(results.TryGetValue(item.Id,out var updated))item.Apply(updated);StatusText=queue.Items.Any(x=>x.State==SelectionAssetStatus.Failed)?"上传未全部确认，可用同一素材身份重试上传。":"服务已确认上传，可按当前规则发布。";RaiseSummaries();}
  finally{IsBusy=false;}
 }
 public async Task RefreshCustomerResultsAsync(CancellationToken ct=default)
 {
  if(Project is null||_provider is not IHttpOnlineSelectionWorkflow http)return;IsBusy=true;
  try{var id=Project.Id;var result=await _provider.GetFinalSelectionAsync(id,ct);if(Project?.Id!=id)return;if(!result.Success||result.Value is null){StatusText=result.Message;return;}var value=result.Value;var ids=Assets.Select(a=>a.Id).ToHashSet();if(value.SelectionProjectId!=id||!value.IsLocked||value.SelectionVersion!=http.ExpectedSelectionVersion(id)||value.Revision<(FinalResult?.Revision??0)||value.Items.Count!=ids.Count||value.Items.Select(x=>x.ImageId).Distinct().Count()!=value.Items.Count||value.Items.Any(x=>x.SelectionProjectId!=id||!ids.Contains(x.ImageId))){StatusText="客户结果项目、素材或版本无效，未保存。";return;}value=value with{Items=value.Items.Select(item=>item with{OriginalFileName=Assets.Single(a=>a.Id==item.ImageId).OriginalFileName}).ToArray()};ApplyFinalResult(value);Project=Project with{Status=SelectionProjectStatus.ClientConfirmed};await SaveAsync(ct);StatusText=$"客户已确认 {value.Items.Count(x=>x.Selected)} 张，结果已校验并保存，可同步归片。";}
  finally{IsBusy=false;}
 }
 private async Task UnpublishHttpAsync(){if(Project is null)return;IsBusy=true;try{var id=Project.Id;var r=await _provider.UnpublishAsync(id);if(Project?.Id!=id)return;if(r.Success){Project=Project with{Status=SelectionProjectStatus.Closed};await SaveAsync();}StatusText=r.Success?"发布已撤销，客户链接立即失效。":r.Message;}finally{IsBusy=false;}}
}
