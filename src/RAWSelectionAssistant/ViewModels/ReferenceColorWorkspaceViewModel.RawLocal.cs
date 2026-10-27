using System.ComponentModel;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private CancellationTokenSource? rawLocalCancellation;private long rawLocalRevision;
 public Task RawLocalPreviewWork{get;private set;}=Task.CompletedTask;
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
 private void CancelRawLocalPreview(){rawLocalCancellation?.Cancel();rawLocalRevision++;}
 private void QueueRawLocalPreview()
 {
  CancelRawLocalPreview();if(ActiveTarget is not{} target||target.RawDefinition is not{} definition||target.LinearRawMaster is not{} master)return;
  var version=target.ActiveEditVersionId;var revision=rawLocalRevision;var cancellation=new CancellationTokenSource();rawLocalCancellation=cancellation;RawLocalPreviewWork=Refresh();
  async Task Refresh(){try{await Task.Delay(90,cancellation.Token);using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);var pixels=await RenderProfessionalPreviewAsync(master,definition,1600,cancellation.Token,"RawLocalPreview");var bitmap=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(cancellation.Token)),pixels.Orientation);if(_disposed||revision!=rawLocalRevision||!ReferenceEquals(target,ActiveTarget)||target.ActiveEditVersionId!=version||System.Text.Json.JsonSerializer.Serialize(target.RawDefinition)!=System.Text.Json.JsonSerializer.Serialize(definition))return;Editor.PresentIndependentRaw(bitmap);var thumb=new TransformedBitmap(bitmap,new System.Windows.Media.ScaleTransform(Math.Min(1,320d/bitmap.PixelWidth),Math.Min(1,320d/bitmap.PixelWidth)));thumb.Freeze();target.Thumbnail=thumb;await RefreshPreviewAnalysisAsync(cancellation.Token,bitmap);}catch(OperationCanceledException){}catch(Exception e)when(e is IOException or InvalidOperationException or ArgumentException or NotSupportedException){if(revision==rawLocalRevision)StatusText="RAW局部预览失败，参数已保留："+e.Message;}finally{if(ReferenceEquals(rawLocalCancellation,cancellation))rawLocalCancellation=null;cancellation.Dispose();}}
 }
 private void ApplyGeometry(ProfessionalRawGeometry? geometry){var target=ActiveTarget!;target.RawDefinition=target.RawDefinition! with{Geometry=geometry};CaptureEditVersion(target);ScheduleR53Autosave();var size=ProfessionalRawPipeline.OutputSize(target.LinearRawMaster!,target.RawDefinition);Editor.SetIndependentRawSize(new(size.Width,size.Height));QueueRawLocalPreview();}
 public void SetRawOutputGeometry(ProfessionalRawGeometry? geometry)
 {
  if(ActiveTarget is not{} target||!RawLocalEditing||Editor.ReadOnlyTarget)return;geometry?.Validate();if(target.ActiveEditVersionId is not Guid version)return;RememberRawLocal(target,target.RawDefinition!);ApplyGeometry(geometry);
 }
}
