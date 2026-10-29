using System.Security.Cryptography;using System.Text.Json;using System.Windows.Media;using System.Windows.Media.Imaging;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed record ViewingFrame(ViewingTableItem Item,BitmapSource Image,string ResolutionLabel);
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private readonly Dictionary<string,BitmapSource> viewingCache=[];private readonly SemaphoreSlim viewingGate=new(1,1);private CancellationTokenSource? viewingCancellation;private long viewingRevision;
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
  Layout=Layout with{ViewingTable=new ViewingTableState(1,items,ViewingTable.PresentationLight).Validate()};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));
 }
 public void SetViewingPlacement(Guid target,Guid variant,double x,double y,double scale,double rotation)
 {var items=ViewingTable.Items.Select(i=>i.TargetId==target&&i.VariantId==variant?(i with{X=x,Y=y,Scale=scale,Rotation=rotation}).Validate():i).ToArray();Layout=Layout with{ViewingTable=ViewingTable with{Items=items}};NotifyR53Edit();}
 public void ClearViewingPlacements(){ReleaseViewing();Layout=Layout with{ViewingTable=ViewingTableState.Empty};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));}
 public void RemoveViewingItem(Guid target,Guid variant){ReleaseViewing();Layout=Layout with{ViewingTable=ViewingTable with{Items=ViewingTable.Items.Where(i=>i.TargetId!=target||i.VariantId!=variant).ToArray()}};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));}
 public void SetViewingLight(bool enabled){Layout=Layout with{ViewingTable=ViewingTable with{PresentationLight=enabled}};NotifyR53Edit();OnPropertyChanged(nameof(ViewingTable));}
 public void CancelViewing(){viewingRevision++;viewingCancellation?.Cancel();ViewingFrames=[];ViewingFailures=[];OnPropertyChanged(nameof(ViewingFrames));OnPropertyChanged(nameof(ViewingFailures));}
 public void ReleaseViewing(){CancelViewing();viewingCache.Clear();}
 public async Task PrepareViewingTableAsync(CancellationToken token=default)
 {
  CancelViewing();var revision=viewingRevision;using var c=CancellationTokenSource.CreateLinkedTokenSource(token);viewingCancellation=c;var frames=new List<ViewingFrame>();var failures=new List<string>();var state=ViewingTable.Validate();var edge=Math.Min(640,Math.Max(128,(int)Math.Sqrt(32d*1024*1024/Math.Max(1,state.Items.Count)/4)));
  try{foreach(var item in state.Items){c.Token.ThrowIfCancellationRequested();try{var image=await RenderViewingItemAsync(item,edge,c.Token);if(_disposed||revision!=viewingRevision)return;frames.Add(new(item,image,$"代理 {image.PixelWidth}×{image.PixelHeight} · 局部放大最长边最多1600px，不宣称原图细节"));}catch(Exception e)when(e is IOException or InvalidOperationException or NotSupportedException or ArgumentException){failures.Add(item.TargetId+" / "+(Targets.FirstOrDefault(t=>t.Id==item.TargetId)?.FileName??"缺少目标")+"："+e.Message);}}
   if(_disposed||revision!=viewingRevision)return;ViewingFrames=frames;ViewingFailures=failures.ToArray();OnPropertyChanged(nameof(ViewingFailures));ViewingStatus=$"灯箱 {frames.Count}/{state.Items.Count} · 失败{failures.Count} · 中性sRGB观片代理；摆放不改变导出";OnPropertyChanged(nameof(ViewingFrames));OnPropertyChanged(nameof(ViewingStatus));
  }catch(OperationCanceledException){if(revision==viewingRevision){ViewingStatus="灯箱载入已取消";OnPropertyChanged(nameof(ViewingStatus));}}
  finally{if(ReferenceEquals(viewingCancellation,c))viewingCancellation=null;}
 }
 internal async Task<BitmapSource> RenderViewingItemAsync(ViewingTableItem item,int edge,CancellationToken token)
 {
  item.Validate();if(edge is <1 or >1600)throw new ArgumentOutOfRangeException(nameof(edge));var target=Targets.SingleOrDefault(t=>t.Id==item.TargetId)??throw new IOException("灯箱照片已不在当前项目");if(!string.Equals(target.SourceHash,item.SourceHash,StringComparison.OrdinalIgnoreCase)||!await ValidateTargetIdentityAsync(target,token))throw new IOException("源照片缺失或已改变，固定变体没有自动重生成");if(FilmBackendBindingV1.HashRecipe(item.FrozenVariant)!=item.RecipeHash)throw new InvalidDataException("灯箱固定配方hash不一致");
  var key=item.SourceHash+item.RecipeHash+"/"+edge;await viewingGate.WaitAsync(token);try{if(viewingCache.TryGetValue(key,out var cached))return cached;var image=await RenderFrozenVersionAsync(target,item.FrozenVariant,edge,token);token.ThrowIfCancellationRequested();if(!await ValidateTargetIdentityAsync(target,token))throw new IOException("载入期间源已改变，未缓存迟到图像");token.ThrowIfCancellationRequested();if(_disposed)throw new OperationCanceledException(token);long bytes=(long)image.PixelWidth*image.PixelHeight*4;while(viewingCache.Count>0&&(viewingCache.Count>=100||ViewingCacheBytes+bytes>64L*1024*1024))viewingCache.Remove(viewingCache.Keys.First());if(bytes<=64L*1024*1024)viewingCache[key]=image;return image;}finally{viewingGate.Release();}
 }
 private async Task<BitmapSource> RenderFrozenVersionAsync(ReferenceTargetItem target,StudioEditVersion version,int edge,CancellationToken token)
 {
  if(version.RawDefinition is{} definition){var borrowed=ReusableMaster(target,definition);var master=borrowed??await DecodeProfessionalInputAsync(target.Path,definition,token);using var owner=borrowed is null?master.Image:null;using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);using var pixels=await RenderProfessionalPreviewAsync(master,definition,edge,token,"ViewingTable");return StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token)),pixels.Orientation);}
  if(RawMatchTiff16ProductPipeline.IsRaw(target.Path)){
   FrozenRawMaster master;try{master=await _rawPipeline.DecodeFrozenMasterAsync(target.Path,token);}catch(IndexOutOfRangeException e){throw new IOException("RAW元数据解析失败；未猜测方向或回退像素："+target.FileName,e);}using var owner=master.Image;using var proxy=_rawPipeline.PreviewMaster(master.Image,edge,token);using var result=_rawPipeline.Render(proxy,version.Look,version.Stack,token:token,film:version.Film).ProcessingPixels!;return StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(result.ToVisualRgb24(token)),result.Orientation);
  }
  return await Task.Run(()=>{var image=StudioQuickExport.LoadCompareProxy(target.Path,edge,token);var stack=ColorStudioEffectiveState.Resolve(version.Look,version.Stack,version.Film)??(version.Look is{} look?ColorStudioLegacyMigration.Migrate(look with{Film=version.Film??look.Film}).Stack:null);return stack is null?image:ColorStudioBitmapRenderer.Render(image,stack,version.Look,token);},token);
 }
 public async Task ReturnViewingToDarkroomAsync(ViewingTableItem item,CancellationToken token=default)
 {var target=Targets.SingleOrDefault(x=>x.Id==item.TargetId)??throw new IOException("源照片不在项目");if(ReferenceEquals(ActiveTarget,target)){Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);}if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);var version=target.EditVersions.SingleOrDefault(v=>v.Id==item.VariantId&&!v.Archived)??throw new IOException("固定变体已删除");if(FilmBackendBindingV1.HashRecipe(version)!=item.RecipeHash)throw new IOException("该变体后来已编辑；灯箱仍保留固定快照，请显式重新固定。");await ActivateTargetAsync(target);await SwitchEditVersionAsync(item.VariantId);}
}
