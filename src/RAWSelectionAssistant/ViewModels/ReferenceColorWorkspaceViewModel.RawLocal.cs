using System.ComponentModel;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private CancellationTokenSource? rawLocalCancellation;private long rawLocalRevision;
 public Task RawLocalPreviewWork{get;private set;}=Task.CompletedTask;
 internal Task RawBaseWarmWork{get;private set;}=Task.CompletedTask;
 public bool RasterGeometryEditing=>ActiveTarget?.RawDefinition is {ProcessingVersion:5,RenderedInputVersion:2,InstantFilm:null}&&!IsLoading&&!IsExporting;
 public bool LocalOutputEditing=>RawLocalEditing||RasterGeometryEditing;
 public bool RawLocalEditing=>ActiveTarget?.RawDefinition is {ProcessingVersion:4,IsRenderedInput:false,InstantFilm:null}&&!IsLoading&&!IsExporting;
 private bool restoringRawLocal;
 private readonly Dictionary<(Guid,Guid),Stack<ProfessionalRawDefinition>> rawLocalUndo=[],rawLocalRedo=[];
 private void RememberRawLocal(ReferenceTargetItem target,ProfessionalRawDefinition before){if(target.ActiveEditVersionId is not Guid id)return;var key=(target.Id,id);if(!rawLocalUndo.TryGetValue(key,out var history))rawLocalUndo[key]=history=new();history.Push(before with{LocalEdits=before.LocalEdits?.Freeze(),SceneExposure=before.SceneExposure?.Validate()});if(history.Count>100){var keep=history.Take(100).Reverse().ToArray();history.Clear();foreach(var item in keep)history.Push(item);}rawLocalRedo.Remove(key);}
 private bool MoveRawLocal(bool redo)
 {
  if(!LocalOutputEditing||ActiveTarget is not{} target||target.ActiveEditVersionId is not Guid id)return false;
  var key=(target.Id,id);var from=redo?rawLocalRedo:rawLocalUndo;var to=redo?rawLocalUndo:rawLocalRedo;if(!from.TryGetValue(key,out var history)||history.Count==0)return false;if(!to.TryGetValue(key,out var other))to[key]=other=new();other.Push(target.RawDefinition!);var next=history.Pop();CancelRawLocalPreview();restoringRawLocal=true;try{target.RawDefinition=next;var selection=Editor.SelectedAdjustmentNode?.Id;Editor.ApplyTargetSnapshot(null,null,next.LocalEdits?.Stack,false,id);if(selection is{} selected)Editor.SelectedAdjustmentNode=Editor.AdjustmentNodes.FirstOrDefault(n=>n.Id==selected);var size=ProfessionalRawPipeline.OutputSize(target.LinearRawMaster!,next);Editor.SetIndependentRawSize(new(size.Width,size.Height));CaptureEditVersion(target);ScheduleR53Autosave();}finally{restoringRawLocal=false;}if(target.LinearRawMaster?.Contract.DecodeKey!=next.DecodeKey){RawLocalPreviewWork=ActivateTargetAsync(target);}else QueueRawLocalPreview();return true;
 }
 private void ObserveRawLocalChange(string? property)
 {
  if(restoringRawLocal||property!=nameof(Editor.AdjustmentStack)||IsLoading||_applyingActiveSnapshot||ActiveTarget is not{} target||target.RawDefinition is not{} definition||!LocalOutputEditing||!Editor.RawLocalEditing)return;
  // Synchronous persistence first: a save/switch immediately after an edit cannot lose it.
  if(System.Text.Json.JsonSerializer.Serialize(definition.LocalEdits?.Stack)!=System.Text.Json.JsonSerializer.Serialize(Editor.AdjustmentStack))RememberRawLocal(target,definition);Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);QueueRawLocalPreview();
 }
 private LatestPreviewQueue<RawPreviewSnapshot,BitmapSource>? rawFastQueue;private readonly LeasedPreviewBufferCache rawFastBases=new(2);
 public BitmapSource? LayerThumbnailSource=>Editor.SourceImage??Editor.MatchedImage;
 public string RawPreviewQuality{get;private set;}="精细";
 private sealed record RawPreviewSnapshot(ReferenceTargetItem Target,Guid? Variant,ProfessionalRawMaster Master,ProfessionalRawDefinition Definition,long Revision);
 private CancellationTokenSource? rawWarmCancellation;private string? rawWarmKey;
 private void WarmRawFastBase(ProfessionalRawMaster master,ProfessionalRawDefinition definition){rawWarmCancellation?.Cancel();var cancel=new CancellationTokenSource();rawWarmCancellation=cancel;var target=ActiveTarget;var epoch=rawFastBases.Generation;rawWarmKey=FastBaseKey(master,definition);RawBaseWarmWork=Warm();async Task Warm(){try{var key=FastBaseKey(master,definition);using(var cached=rawFastBases.Acquire(key))if(cached is not null)return;using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);var basis=await Task.Run(()=>BuildFastBasis(master,definition,cancel.Token),cancel.Token);if(_disposed||cancel.IsCancellationRequested||!ReferenceEquals(target,ActiveTarget)){basis.Dispose();return;}using var stored=rawFastBases.StoreAndAcquire(key,basis,epoch);}catch(OperationCanceledException){}catch(Exception error)when(error is IOException or InvalidOperationException or ArgumentException or NotSupportedException){System.Diagnostics.Trace.WriteLine(error);}finally{if(ReferenceEquals(rawWarmCancellation,cancel))rawWarmCancellation=null;cancel.Dispose();}}}
 private static RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer BuildFastBasis(ProfessionalRawMaster master,ProfessionalRawDefinition definition,CancellationToken token)=>definition.SceneExposure is not null?SceneExposureInteractiveV1.Prepare(master,definition,800,token):ProfessionalRawLocalRenderer.FastBase(master,definition,800,token);
 private static string FastBaseKey(ProfessionalRawMaster master,ProfessionalRawDefinition definition)=>System.Text.Json.JsonSerializer.Serialize(new{master.Contract.SourceSha256,master.Contract.DecodeKey,master.Image.Width,master.Image.Height,master.Image.Orientation,Definition=definition with{LocalEdits=null,SceneExposure=null},Scene=definition.SceneExposure is not null,edge=800,quality="SceneSamples2xV1OrAreaV1"});
 private void CancelRawLocalPreview(){layerThumbnailQueue?.Invalidate();LayerThumbnailContext=null;OnPropertyChanged(nameof(LayerThumbnailContext));OnPropertyChanged(nameof(SceneExposureMaskPreview));rawWarmCancellation?.Cancel();rawLocalCancellation?.Cancel();rawLocalRevision++;rawFastQueue?.Invalidate();rawFastBases.Clear();}
 private void QueueRawLocalPreview()
 {
  rawLocalCancellation?.Cancel();var revision=++rawLocalRevision;if(ActiveTarget is not{} target||target.RawDefinition is not{} definition||target.LinearRawMaster is not{} master)return;
  NotifySceneExposure();NotifyColorState();QueueLayerThumbnails();var version=target.ActiveEditVersionId;definition=definition with{LocalEdits=definition.LocalEdits?.Freeze(),SceneExposure=definition.SceneExposure?.Validate()};var snapshot=new RawPreviewSnapshot(target,version,master,definition,revision);
  rawFastQueue??=new LatestPreviewQueue<RawPreviewSnapshot,BitmapSource>(async(frame,token)=>{
   using var pin=StudioGlobalResources.RetainBuffer(frame.Master.Image,(long)frame.Master.Image.Rgb32.Length*4);
   if(frame.Definition.ColorState?.Version==2){if(DocumentWorkingRendererV2.RequiresExactPreview(frame.Master,frame.Definition)){using var exact=await Task.Run(()=>DocumentWorkingRendererV2.Preview(frame.Master,frame.Definition,800,token),token);return RawDisplayBitmapAdapter.ToBitmap(exact.ToVisualRgb24(token));}var proxyKey=System.Text.Json.JsonSerializer.Serialize(new{frame.Master.Contract.SourceSha256,frame.Master.Contract.DecodeKey,frame.Master.Image.Width,frame.Master.Image.Height,frame.Master.Image.Orientation,kind="V2LinearAreaProxy800"});var proxyEpoch=rawFastBases.Generation;var proxyLease=rawFastBases.Acquire(proxyKey);if(proxyLease is null){var proxy=await Task.Run(()=>DocumentWorkingRendererV2.ProxyMaster(frame.Master,800,token),token);if(token.IsCancellationRequested){proxy.Dispose();token.ThrowIfCancellationRequested();}proxyLease=rawFastBases.StoreAndAcquire(proxyKey,proxy,proxyEpoch);}using var proxyHeld=proxyLease;using var wide=await Task.Run(()=>DocumentWorkingRendererV2.InteractivePreview(frame.Master,frame.Definition,proxyHeld.Buffer,800,token),token);return RawDisplayBitmapAdapter.ToBitmap(wide.ToVisualRgb24(token));}
   var key=FastBaseKey(frame.Master,frame.Definition);if(rawWarmKey==key&&!RawBaseWarmWork.IsCompleted)await RawBaseWarmWork.WaitAsync(token);
   var epoch=rawFastBases.Generation;var lease=rawFastBases.Acquire(key);if(lease is null){var basis=await Task.Run(()=>BuildFastBasis(frame.Master,frame.Definition,token),token);if(token.IsCancellationRequested){basis.Dispose();token.ThrowIfCancellationRequested();}lease=rawFastBases.StoreAndAcquire(key,basis,epoch);}
   using var held=lease;using var pixels=await Task.Run(()=>frame.Definition.SceneExposure is not null?SceneExposureInteractiveV1.Render(frame.Master,frame.Definition,held.Buffer,800,token):ProfessionalRawLocalRenderer.Fast(frame.Master,frame.Definition,held.Buffer,token),token);return RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token));
  },(_,frame,bitmap)=>{if(_disposed||!ReferenceEquals(ActiveTarget,frame.Target)||frame.Target.ActiveEditVersionId!=frame.Variant)return;Editor.PresentIndependentRaw(bitmap);RawPreviewQuality=frame.Definition.ColorState?.Version==2&&DocumentWorkingRendererV2.RequiresExactPreview(frame.Master,frame.Definition)?"精确预览 · 正在更新":"快速 · 正在精细更新";OnPropertyChanged(nameof(RawPreviewQuality));},error=>StatusText="快速预览未更新："+error.Message);
  rawFastQueue.Submit(snapshot);var cancellation=new CancellationTokenSource();rawLocalCancellation=cancellation;RawLocalPreviewWork=Refresh();
  async Task Refresh(){try{await Task.Delay(400,cancellation.Token);await rawFastQueue.Work;using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);using var pixels=await RenderProfessionalPreviewAsync(master,definition,1600,cancellation.Token,"RawLocalFinePreview");var bitmap=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(cancellation.Token)),pixels.Orientation);if(_disposed||revision!=rawLocalRevision||!ReferenceEquals(target,ActiveTarget)||target.ActiveEditVersionId!=version)return;Editor.PresentIndependentRaw(bitmap);RawPreviewQuality="精细";OnPropertyChanged(nameof(RawPreviewQuality));var thumb=new TransformedBitmap(bitmap,new System.Windows.Media.ScaleTransform(Math.Min(1,320d/bitmap.PixelWidth),Math.Min(1,320d/bitmap.PixelWidth)));thumb.Freeze();target.Thumbnail=thumb;await RefreshPreviewAnalysisAsync(cancellation.Token,bitmap);}catch(OperationCanceledException){}catch(Exception e)when(e is IOException or InvalidOperationException or ArgumentException or NotSupportedException){if(revision==rawLocalRevision)StatusText="RAW局部预览失败，参数已保留："+e.Message;}finally{if(ReferenceEquals(rawLocalCancellation,cancellation))rawLocalCancellation=null;cancellation.Dispose();}}
 }
 public bool CanEditSceneExposure=>
#if WHOLE_APP_DIAGNOSTIC
 RawLocalEditing
#else
 false
#endif
&&!Editor.ReadOnlyTarget&&ActiveTarget?.RawDefinition?.SceneExposure?.Locked!=true;
 public bool SceneExposureLocked{get=>ActiveTarget?.RawDefinition?.SceneExposure?.Locked==true;set{if(!RawLocalEditing||Editor.ReadOnlyTarget||ActiveTarget is not{} target||SceneExposureLocked==value)return;RememberRawLocal(target,target.RawDefinition!);target.RawDefinition=target.RawDefinition! with{SceneExposure=(target.RawDefinition.SceneExposure??new(0)) with{Locked=value}};CaptureEditVersion(target);ScheduleR53Autosave();NotifySceneExposure();}}
 public string SceneExposureMaskStatus=>ActiveTarget?.RawDefinition?.SceneExposure?.Mask is{} mask?$"蒙版快照 · {mask.Strokes.Count} 笔（不自动同步原层）":"全图作用 · 未绑定蒙版快照";
 private void NotifySceneExposure(){OnPropertyChanged(nameof(SceneLocalExposureEv));OnPropertyChanged(nameof(SceneExposureMaskStatus));OnPropertyChanged(nameof(SceneExposureLocked));OnPropertyChanged(nameof(CanEditSceneExposure));}
 public void ClearSceneExposureMask(){if(!CanEditSceneExposure||ActiveTarget is not{} target)return;RememberRawLocal(target,target.RawDefinition!);target.RawDefinition=target.RawDefinition! with{SceneExposure=new(SceneLocalExposureEv,null)};CaptureEditVersion(target);ScheduleR53Autosave();NotifySceneExposure();QueueRawLocalPreview();}
 public double SceneLocalExposureEv{get=>ActiveTarget?.RawDefinition?.SceneExposure?.ExposureEv??0;set{
#if WHOLE_APP_DIAGNOSTIC
 if(!CanEditSceneExposure||ActiveTarget is not{} target||!double.IsFinite(value)||SceneLocalExposureEv==value)return;RememberRawLocal(target,target.RawDefinition!);target.RawDefinition=target.RawDefinition! with{SceneExposure=new SceneExposureV1(Math.Clamp(value,-10,10),target.RawDefinition!.SceneExposure?.Mask).Validate()};CaptureEditVersion(target);ScheduleR53Autosave();NotifySceneExposure();QueueRawLocalPreview();
#endif
 }}
 public void BindSceneExposureMask(){
#if WHOLE_APP_DIAGNOSTIC
 if(!CanEditSceneExposure||ActiveTarget is not{} target||Editor.SelectedAdjustmentNode is not{} node||node.UserLocked||node.SystemLocked)return;RememberRawLocal(target,target.RawDefinition!);target.RawDefinition=target.RawDefinition! with{SceneExposure=new(SceneLocalExposureEv,node.LocalMask?.Freeze())};CaptureEditVersion(target);ScheduleR53Autosave();NotifySceneExposure();QueueRawLocalPreview();
#endif
 }
 private void ApplyGeometry(ProfessionalRawGeometry? geometry){var target=ActiveTarget!;target.RawDefinition=target.RawDefinition! with{Geometry=geometry};CaptureEditVersion(target);ScheduleR53Autosave();var size=ProfessionalRawPipeline.OutputSize(target.LinearRawMaster!,target.RawDefinition);Editor.SetIndependentRawSize(new(size.Width,size.Height));QueueRawLocalPreview();}
 public void SetRawOutputGeometry(ProfessionalRawGeometry? geometry)
 {
  if(ActiveTarget is not{} target||!LocalOutputEditing||Editor.ReadOnlyTarget)return;geometry?.Validate();if(target.ActiveEditVersionId is not Guid version)return;RememberRawLocal(target,target.RawDefinition!);ApplyGeometry(geometry);
 }
}
