using System.Security.Cryptography;using System.Text.Json;using System.Windows.Media;using System.Windows.Media.Imaging;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed record ViewingFrame(ViewingTableItem Item,BitmapSource Image,string ResolutionLabel);
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private readonly Dictionary<string,BitmapSource> viewingCache=[];private readonly Dictionary<string,IDisposable> viewingCacheReservations=[];private readonly SemaphoreSlim viewingGate=new(1,1);private CancellationTokenSource? viewingCancellation;private long viewingRevision;
 public ViewingTableState ViewingTable=>Layout.ViewingTable??ViewingTableState.Empty;
 public IReadOnlyList<ViewingFrame> ViewingFrames{get;private set;}=[];
 public string ViewingStatus{get;private set;}="中性观片 · 不改变导出像素";
 public IReadOnlyList<string> ViewingFailures{get;private set;}=[];
 public long ViewingCacheBytes=>viewingCache.Values.Sum(x=>(long)x.PixelWidth*x.PixelHeight*4);
 public void PinSelectedViewingVariants(bool replaceExisting=false)
 {
  if(IsLoading||IsExporting||IsInstantGenerating)return;
  if(ActiveTarget is{} active){Editor.CopyCurrentLookTo([active]);CaptureEditVersion(active);}
  var selectedIds=SelectedTargets.Select(t=>t.Id).ToHashSet();var items=ViewingTable.Items.Where(x=>!replaceExisting||!selectedIds.Contains(x.TargetId)).ToList();
  foreach(var target in SelectedTargets.Take(100)){CaptureEditVersion(target);EnsureR53Variants(target);var version=target.EditVersions.FirstOrDefault(v=>v.Id==target.ActiveEditVersionId&&!v.Archived);if(version is null||target.SourceHash is null||items.Any(x=>x.TargetId==target.Id&&x.VariantId==version.Id))continue;if(items.Count==100)break;var frozen=JsonSerializer.Deserialize<StudioEditVersion>(JsonSerializer.Serialize(version))!;items.Add(new(target.Id,version.Id,target.SourceHash,FilmBackendBindingV1.HashRecipe(frozen),frozen,20+items.Count%5*230,20+items.Count/5*170));}
  InvalidateViewingRequests();Layout=Layout with{ViewingTable=new ViewingTableState(1,items,ViewingTable.PresentationLight).Validate()};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));
 }
 public void SetViewingPlacement(Guid target,Guid variant,double x,double y,double scale,double rotation)
 {var items=ViewingTable.Items.Select(i=>i.TargetId==target&&i.VariantId==variant?(i with{X=x,Y=y,Scale=scale,Rotation=rotation}).Validate():i).ToArray();Layout=Layout with{ViewingTable=ViewingTable with{Items=items}};NotifyR53Edit();}
 public void ClearViewingPlacements(){ReleaseViewing();Layout=Layout with{ViewingTable=ViewingTableState.Empty};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));}
 public void RemoveViewingItem(Guid target,Guid variant){ReleaseViewing();Layout=Layout with{ViewingTable=ViewingTable with{Items=ViewingTable.Items.Where(i=>i.TargetId!=target||i.VariantId!=variant).ToArray()}};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));}
 public void SetViewingLight(bool enabled){Layout=Layout with{ViewingTable=ViewingTable with{PresentationLight=enabled}};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));}
 internal void InvalidateViewingRequests(){viewingRevision++;viewingCancellation?.Cancel();}
 public void CancelViewing(){InvalidateViewingRequests();ViewingFrames=[];ViewingFailures=[];OnPropertyChanged(nameof(ViewingFrames));OnPropertyChanged(nameof(ViewingFailures));}
 public void ReleaseViewing(){CancelViewing();viewingCache.Clear();foreach(var reservation in viewingCacheReservations.Values)reservation.Dispose();viewingCacheReservations.Clear();viewingIdentities.Clear();}
 internal Func<ViewingTableItem,int,CancellationToken,Task<BitmapSource>>? ViewingRenderOverride;
 private readonly Dictionary<string,(ViewingSourceLease.Stamp Stamp,string Hash)> viewingIdentities=new(StringComparer.OrdinalIgnoreCase);
 internal long ViewingHashReads{get;private set;} internal long ViewingDecodeCount{get;private set;}
 internal int ViewingActiveRenders{get;private set;} internal int ViewingPeakActiveRenders{get;private set;}
 public Task PrepareViewingTableAsync(CancellationToken token=default)=>PrepareViewingTableAsync(token,null,false);
 internal async Task PrepareViewingTableAsync(CancellationToken token,IReadOnlyList<Guid>? priorityVariants,bool visibleOnly=false)
 {
  CancelViewing();var revision=viewingRevision;using var c=CancellationTokenSource.CreateLinkedTokenSource(token);viewingCancellation=c;
  var state=ViewingTable.Validate();var priorities=(priorityVariants??[]).ToHashSet();var items=state.Items.Where(i=>!visibleOnly||priorities.Contains(i.VariantId)).OrderByDescending(i=>priorities.Contains(i.VariantId)).ToArray();
  var frames=new List<ViewingFrame>();var failures=new List<string>();var edge=ViewingProxyEdge(state.Items.Count);
  bool Current()=>!_disposed&&revision==viewingRevision&&!c.IsCancellationRequested;
  void Publish(){if(!Current())return;ViewingFrames=frames.ToArray();ViewingFailures=failures.ToArray();ViewingStatus=$"灯箱 {frames.Count}/{items.Length} · 失败{failures.Count} · 固定{state.Items.Count} · 中性sRGB代理";OnPropertyChanged(nameof(ViewingFrames));if(!Current())return;OnPropertyChanged(nameof(ViewingFailures));OnPropertyChanged(nameof(ViewingStatus));}
  try{foreach(var item in items){c.Token.ThrowIfCancellationRequested();try{var image=await (ViewingRenderOverride??RenderViewingItemAsync)(item,edge,c.Token);if(!Current())return;frames.Add(new(item,image,$"代理 {image.PixelWidth}×{image.PixelHeight} · 最长边最多1600px，不是原图细节"));}catch(Exception e)when(e is IOException or InvalidOperationException or NotSupportedException or ArgumentException){if(!Current())return;failures.Add(ViewingFailurePrefix(item)+" / "+(Targets.FirstOrDefault(t=>t.Id==item.TargetId)?.FileName??"缺少目标")+"："+ViewingFailureReason(e));}Publish();await Task.Yield();}}
  catch(OperationCanceledException){if(Current()){ViewingStatus="灯箱载入已取消";OnPropertyChanged(nameof(ViewingStatus));}}
  finally{if(ReferenceEquals(viewingCancellation,c))viewingCancellation=null;}
 }
 internal async Task RetryViewingItemAsync(ViewingTableItem item,CancellationToken token=default)
 {
  InvalidateViewingRequests();var revision=viewingRevision;using var c=CancellationTokenSource.CreateLinkedTokenSource(token);viewingCancellation=c;try{var image=await (ViewingRenderOverride??RenderViewingItemAsync)(item,ViewingProxyEdge(ViewingTable.Items.Count),c.Token);if(_disposed||revision!=viewingRevision||c.IsCancellationRequested)return;ViewingFrames=ViewingFrames.Where(f=>f.Item.VariantId!=item.VariantId).Append(new ViewingFrame(item,image,$"代理 {image.PixelWidth}×{image.PixelHeight}")).ToArray();ViewingFailures=ViewingFailures.Where(f=>!f.StartsWith(ViewingFailurePrefix(item),StringComparison.Ordinal)).ToArray();ViewingStatus=$"灯箱 {ViewingFrames.Count}/{ViewingTable.Items.Count} · 失败{ViewingFailures.Count}";OnPropertyChanged(nameof(ViewingFrames));OnPropertyChanged(nameof(ViewingFailures));OnPropertyChanged(nameof(ViewingStatus));}
  catch(Exception e)when(e is IOException or InvalidOperationException or NotSupportedException or ArgumentException or OperationCanceledException){if(_disposed||revision!=viewingRevision||c.IsCancellationRequested)return;ViewingFailures=ViewingFailures.Where(f=>!f.StartsWith(ViewingFailurePrefix(item),StringComparison.Ordinal)).Append(ViewingFailurePrefix(item)+" / "+ViewingFailureReason(e)).ToArray();OnPropertyChanged(nameof(ViewingFailures));}
  finally{if(ReferenceEquals(viewingCancellation,c))viewingCancellation=null;}
 }
 internal static int ViewingProxyEdge(int count)=>Math.Min(640,Math.Max(128,(int)Math.Sqrt(32d*1024*1024/Math.Max(1,count)/4)));
 internal static string ViewingFailurePrefix(ViewingTableItem item)=>item.TargetId+" / variant="+item.VariantId;
 internal static string ViewingFailureReason(Exception e)=>e switch{RAWSelectionAssistant.Core.Services.RawToJpeg.RawDecodeException raw=>$"解码失败 [{raw.ErrorCode}; stage={raw.Stage}; native={raw.NativeName??"unavailable"}]：{raw.Message}",StudioBudgetException budget=>$"预算拒绝 [required={budget.RequiredBytes}; available={budget.AvailableBytes}]：{e.Message}",_=>e.Message};
 internal async Task<BitmapSource> RenderViewingItemAsync(ViewingTableItem item,int edge,CancellationToken token)
 {
  token.ThrowIfCancellationRequested();item.Validate();if(edge is <1 or >1600)throw new ArgumentOutOfRangeException(nameof(edge));var target=Targets.SingleOrDefault(t=>t.Id==item.TargetId)??throw new IOException("灯箱照片已不在当前项目");
  // Preserve repository resolution for library targets; file-only targets use reliable handle identity below.
  if(target.LibraryId is not null&&!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
  if(!string.Equals(target.SourceHash,item.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("固定源身份不符");
  var epoch=viewingRevision;await viewingGate.WaitAsync(token);try{
   token.ThrowIfCancellationRequested();if(_disposed||epoch!=viewingRevision)throw new OperationCanceledException(token);
   using var lease=new ViewingSourceLease(target.Path);var stamp=lease.Identity;string hash;
   if(stamp is{} current&&viewingIdentities.TryGetValue(target.Path,out var previous)&&previous.Stamp==current)hash=previous.Hash;
   else{ViewingHashReads++;hash=Convert.ToHexString(await SHA256.HashDataAsync(lease.Stream,token));token.ThrowIfCancellationRequested();if(_disposed||epoch!=viewingRevision)throw new OperationCanceledException(token);if(stamp is{} fresh){if(viewingIdentities.Count>=100)viewingIdentities.Remove(viewingIdentities.Keys.First());viewingIdentities[target.Path]=(fresh,hash);}}
   if(!string.Equals(hash,item.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("源文件内容已变化，固定变体未自动替换");
   var key=Path.GetFullPath(target.Path)+"/"+item.SourceHash+item.RecipeHash+"/"+edge;
   if(viewingCache.TryGetValue(key,out var cached))return cached;
   ViewingActiveRenders++;ViewingPeakActiveRenders=Math.Max(ViewingPeakActiveRenders,ViewingActiveRenders);BitmapSource image;
   try{ViewingDecodeCount++;image=await RenderFrozenVersionAsync(target,item.FrozenVariant,edge,token);}finally{ViewingActiveRenders--;}
   token.ThrowIfCancellationRequested();if(_disposed||epoch!=viewingRevision)throw new OperationCanceledException(token);
   // The held handle prevents writes; independently reopen the path to detect replacement via a renamed parent.
   using(var verify=new ViewingSourceLease(target.Path)){if(stamp is{} before&&verify.Identity is{} after){if(before!=after)throw new IOException("源路径身份在载入期间变化");}else{ViewingHashReads++;var finalHash=Convert.ToHexString(await SHA256.HashDataAsync(verify.Stream,token));if(!string.Equals(finalHash,item.SourceHash,StringComparison.OrdinalIgnoreCase))throw new IOException("源文件在载入期间改变");}}
   token.ThrowIfCancellationRequested();if(_disposed||epoch!=viewingRevision)throw new OperationCanceledException(token);
   long bytes=(long)image.PixelWidth*image.PixelHeight*4;while(viewingCache.Count>0&&(viewingCache.Count>=100||ViewingCacheBytes+bytes>64L*1024*1024)){var retired=viewingCache.Keys.First();viewingCache.Remove(retired);if(viewingCacheReservations.Remove(retired,out var leaseReservation))leaseReservation.Dispose();}if(bytes<=64L*1024*1024){var reservation=StudioGlobalResources.ReserveWork(bytes);viewingCache[key]=image;viewingCacheReservations[key]=reservation;}return image;
  }finally{viewingGate.Release();}
 }
 private async Task<BitmapSource> RenderFrozenVersionAsync(ReferenceTargetItem target,StudioEditVersion version,int edge,CancellationToken token)
 {
  if(version.RawDefinition is{} definition){var borrowed=ReusableMaster(target,definition);var master=borrowed??await DecodeProfessionalInputAsync(target.Path,definition,token);using var owner=borrowed is null?master.Image:null;using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);using var pixels=await RenderProfessionalPreviewAsync(master,definition,edge,token,"ViewingTable");return StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token)),pixels.Orientation);}
  if(RawMatchTiff16ProductPipeline.IsRaw(target.Path)){
   RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer proxy;try{proxy=await _rawPipeline.DecodeViewingProxyAsync(target.Path,edge,token);}catch(IndexOutOfRangeException e){throw new IOException("RAW元数据解析失败；未猜测方向或回退像素："+target.FileName,e);}using var owner=proxy;using var result=_rawPipeline.Render(proxy,version.Look,version.Stack,token:token,film:version.Film).ProcessingPixels!;return StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(result.ToVisualRgb24(token)),result.Orientation);
  }
  return await Task.Run(()=>{var image=StudioQuickExport.LoadCompareProxy(target.Path,edge,token);var stack=ColorStudioEffectiveState.Resolve(version.Look,version.Stack,version.Film)??(version.Look is{} look?ColorStudioLegacyMigration.Migrate(look with{Film=version.Film??look.Film}).Stack:null);return stack is null?image:ColorStudioBitmapRenderer.Render(image,stack,version.Look,token);},token);
 }
 public async Task ReturnViewingToDarkroomAsync(ViewingTableItem item,CancellationToken token=default)
 {var target=Targets.SingleOrDefault(x=>x.Id==item.TargetId)??throw new IOException("源照片不在项目");if(ReferenceEquals(ActiveTarget,target)){Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);}if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);var version=target.EditVersions.SingleOrDefault(v=>v.Id==item.VariantId&&!v.Archived)??throw new IOException("固定变体已删除");if(FilmBackendBindingV1.HashRecipe(version)!=item.RecipeHash)throw new IOException("该变体后来已编辑；灯箱仍保留固定快照，请显式重新固定。");token.ThrowIfCancellationRequested();await ActivateTargetAsync(target);token.ThrowIfCancellationRequested();await SwitchEditVersionAsync(item.VariantId);}
}
