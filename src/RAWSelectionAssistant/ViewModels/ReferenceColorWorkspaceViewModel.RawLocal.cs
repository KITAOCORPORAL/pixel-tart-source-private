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
 public bool RawLocalEditing=>ActiveTarget?.RawDefinition is {ProcessingVersion:4,IsRenderedInput:false,InstantFilm:null}&&!IsLoading&&!IsExporting;
 private bool restoringRawLocal;
 private readonly Dictionary<(Guid,Guid),Stack<ProfessionalRawDefinition>> rawLocalUndo=[],rawLocalRedo=[];
 private void RememberRawLocal(ReferenceTargetItem target,ProfessionalRawDefinition before){if(target.ActiveEditVersionId is not Guid id)return;var key=(target.Id,id);if(!rawLocalUndo.TryGetValue(key,out var history))rawLocalUndo[key]=history=new();history.Push(before with{LocalEdits=before.LocalEdits?.Freeze()});if(history.Count>100){var keep=history.Take(100).Reverse().ToArray();history.Clear();foreach(var item in keep)history.Push(item);}rawLocalRedo.Remove(key);}
 private bool MoveRawLocal(bool redo)
 {
  if(!RawLocalEditing||ActiveTarget is not{} target||target.ActiveEditVersionId is not Guid id)return false;
  var key=(target.Id,id);var from=redo?rawLocalRedo:rawLocalUndo;var to=redo?rawLocalUndo:rawLocalRedo;if(!from.TryGetValue(key,out var history)||history.Count==0)return false;if(!to.TryGetValue(key,out var other))to[key]=other=new();other.Push(target.RawDefinition!);var next=history.Pop();CancelRawLocalPreview();restoringRawLocal=true;try{target.RawDefinition=next;var selection=Editor.SelectedAdjustmentNode?.Id;Editor.ApplyTargetSnapshot(null,null,next.LocalEdits?.Stack,false,id);if(selection is{} selected)Editor.SelectedAdjustmentNode=Editor.AdjustmentNodes.FirstOrDefault(n=>n.Id==selected);var size=ProfessionalRawPipeline.OutputSize(target.LinearRawMaster!,next);Editor.SetIndependentRawSize(new(size.Width,size.Height));CaptureEditVersion(target);ScheduleR53Autosave();}finally{restoringRawLocal=false;}QueueRawLocalPreview();return true;
 }
 private void ObserveRawLocalChange(string? property)
 {
  if(restoringRawLocal||property!=nameof(Editor.AdjustmentStack)||IsLoading||_applyingActiveSnapshot||ActiveTarget is not{} target||target.RawDefinition is not {ProcessingVersion:4,IsRenderedInput:false,InstantFilm:null} definition||!Editor.RawLocalEditing)return;
  // Synchronous persistence first: a save/switch immediately after an edit cannot lose it.
  if(System.Text.Json.JsonSerializer.Serialize(definition.LocalEdits?.Stack)!=System.Text.Json.JsonSerializer.Serialize(Editor.AdjustmentStack))RememberRawLocal(target,definition);Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);QueueRawLocalPreview();
 }
 private LatestPreviewQueue<RawPreviewSnapshot,BitmapSource>? rawFastQueue;private readonly object rawFastCacheGate=new();private readonly Dictionary<string,RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer> rawFastBases=[];
 public BitmapSource? LayerThumbnailSource=>Editor.SourceImage??Editor.MatchedImage;
 public string RawPreviewQuality{get;private set;}="精细";
 private sealed record RawPreviewSnapshot(ReferenceTargetItem Target,Guid? Variant,ProfessionalRawMaster Master,ProfessionalRawDefinition Definition,long Revision);
 private CancellationTokenSource? rawWarmCancellation;private string? rawWarmKey;
 private void WarmRawFastBase(ProfessionalRawMaster master,ProfessionalRawDefinition definition){rawWarmCancellation?.Cancel();var cancel=new CancellationTokenSource();rawWarmCancellation=cancel;var target=ActiveTarget;rawWarmKey=FastBaseKey(master,definition);RawBaseWarmWork=Warm();async Task Warm(){try{var key=FastBaseKey(master,definition);lock(rawFastCacheGate)if(rawFastBases.ContainsKey(key))return;using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);var basis=await Task.Run(()=>ProfessionalRawLocalRenderer.FastBase(master,definition,800,cancel.Token),cancel.Token);if(_disposed||cancel.IsCancellationRequested||!ReferenceEquals(target,ActiveTarget)){basis.Dispose();return;}lock(rawFastCacheGate){if(rawFastBases.Count>=2)rawFastBases.Clear();rawFastBases[key]=basis;}}catch(OperationCanceledException){}catch(Exception error)when(error is IOException or InvalidOperationException or ArgumentException or NotSupportedException){System.Diagnostics.Trace.WriteLine(error);}finally{if(ReferenceEquals(rawWarmCancellation,cancel))rawWarmCancellation=null;cancel.Dispose();}}}
 private static string FastBaseKey(ProfessionalRawMaster master,ProfessionalRawDefinition definition)=>System.Text.Json.JsonSerializer.Serialize(new{master.Contract.SourceSha256,master.Contract.DecodeKey,master.Image.Width,master.Image.Height,master.Image.Orientation,Definition=definition with{LocalEdits=null},edge=800,quality="AreaBaseInteractiveV1"});
 private void CancelRawLocalPreview(){rawWarmCancellation?.Cancel();rawLocalCancellation?.Cancel();rawLocalRevision++;rawFastQueue?.Invalidate();lock(rawFastCacheGate)rawFastBases.Clear();}
 private void QueueRawLocalPreview()
 {
  rawLocalCancellation?.Cancel();var revision=++rawLocalRevision;if(ActiveTarget is not{} target||target.RawDefinition is not{} definition||target.LinearRawMaster is not{} master)return;
  var version=target.ActiveEditVersionId;definition=definition with{LocalEdits=definition.LocalEdits?.Freeze(),SceneExposure=definition.SceneExposure?.Validate()};var snapshot=new RawPreviewSnapshot(target,version,master,definition,revision);
  rawFastQueue??=new LatestPreviewQueue<RawPreviewSnapshot,BitmapSource>(async(frame,token)=>{
   using var pin=StudioGlobalResources.RetainBuffer(frame.Master.Image,(long)frame.Master.Image.Rgb32.Length*4);
   var key=FastBaseKey(frame.Master,frame.Definition);if(rawWarmKey==key&&!RawBaseWarmWork.IsCompleted)await RawBaseWarmWork.WaitAsync(token);
   RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer? basis;lock(rawFastCacheGate)rawFastBases.TryGetValue(key,out basis);if(basis is null){basis=await Task.Run(()=>ProfessionalRawLocalRenderer.FastBase(frame.Master,frame.Definition,800,token),token);token.ThrowIfCancellationRequested();lock(rawFastCacheGate){if(rawFastBases.Count>=2)rawFastBases.Clear();rawFastBases[key]=basis;}}
   var frozenBasis=basis;using var pixels=await Task.Run(()=>ProfessionalRawLocalRenderer.Fast(frame.Master,frame.Definition,frozenBasis,token),token);return RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token));
  },(_,frame,bitmap)=>{if(_disposed||!ReferenceEquals(ActiveTarget,frame.Target)||frame.Target.ActiveEditVersionId!=frame.Variant)return;Editor.PresentIndependentRaw(bitmap);RawPreviewQuality="快速 · 正在精细更新";OnPropertyChanged(nameof(RawPreviewQuality));},error=>StatusText="快速预览未更新："+error.Message);
  rawFastQueue.Submit(snapshot);var cancellation=new CancellationTokenSource();rawLocalCancellation=cancellation;RawLocalPreviewWork=Refresh();
  async Task Refresh(){try{await Task.Delay(400,cancellation.Token);await rawFastQueue.Work;using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);var pixels=await RenderProfessionalPreviewAsync(master,definition,1600,cancellation.Token,"RawLocalFinePreview");var bitmap=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(cancellation.Token)),pixels.Orientation);if(_disposed||revision!=rawLocalRevision||!ReferenceEquals(target,ActiveTarget)||target.ActiveEditVersionId!=version)return;Editor.PresentIndependentRaw(bitmap);RawPreviewQuality="精细";OnPropertyChanged(nameof(RawPreviewQuality));var thumb=new TransformedBitmap(bitmap,new System.Windows.Media.ScaleTransform(Math.Min(1,320d/bitmap.PixelWidth),Math.Min(1,320d/bitmap.PixelWidth)));thumb.Freeze();target.Thumbnail=thumb;await RefreshPreviewAnalysisAsync(cancellation.Token,bitmap);}catch(OperationCanceledException){}catch(Exception e)when(e is IOException or InvalidOperationException or ArgumentException or NotSupportedException){if(revision==rawLocalRevision)StatusText="RAW局部预览失败，参数已保留："+e.Message;}finally{if(ReferenceEquals(rawLocalCancellation,cancellation))rawLocalCancellation=null;cancellation.Dispose();}}
 }
 public double SceneLocalExposureEv{get=>ActiveTarget?.RawDefinition?.SceneExposure?.ExposureEv??0;set{
#if WHOLE_APP_DIAGNOSTIC
  if(!RawLocalEditing||ActiveTarget is not{} target||Editor.ReadOnlyTarget)return;RememberRawLocal(target,target.RawDefinition!);var mask=target.RawDefinition!.SceneExposure?.Mask??Editor.SelectedAdjustmentNode?.LocalMask?.Freeze();target.RawDefinition=target.RawDefinition with{SceneExposure=new SceneExposureV1(Math.Clamp(value,-10,10),mask).Validate()};CaptureEditVersion(target);ScheduleR53Autosave();OnPropertyChanged();QueueRawLocalPreview();
#endif
 }}
 public void BindSceneExposureMask(){
#if WHOLE_APP_DIAGNOSTIC
 if(!RawLocalEditing||ActiveTarget is not{} target||Editor.ReadOnlyTarget)return;RememberRawLocal(target,target.RawDefinition!);target.RawDefinition=target.RawDefinition! with{SceneExposure=new(SceneLocalExposureEv,Editor.SelectedAdjustmentNode?.LocalMask?.Freeze())};CaptureEditVersion(target);ScheduleR53Autosave();QueueRawLocalPreview();
#endif
 }
 private void ApplyGeometry(ProfessionalRawGeometry? geometry){var target=ActiveTarget!;target.RawDefinition=target.RawDefinition! with{Geometry=geometry};CaptureEditVersion(target);ScheduleR53Autosave();var size=ProfessionalRawPipeline.OutputSize(target.LinearRawMaster!,target.RawDefinition);Editor.SetIndependentRawSize(new(size.Width,size.Height));QueueRawLocalPreview();}
 public void SetRawOutputGeometry(ProfessionalRawGeometry? geometry)
 {
  if(ActiveTarget is not{} target||!RawLocalEditing||Editor.ReadOnlyTarget)return;geometry?.Validate();if(target.ActiveEditVersionId is not Guid version)return;RememberRawLocal(target,target.RawDefinition!);ApplyGeometry(geometry);
 }
}
