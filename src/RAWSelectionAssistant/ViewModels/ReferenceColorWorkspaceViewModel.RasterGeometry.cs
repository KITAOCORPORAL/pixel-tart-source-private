using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 public async Task<bool> EnsureGeometryEditingAsync()
 {
  if(LocalOutputEditing)return !Editor.ReadOnlyTarget;
#if !WHOLE_APP_DIAGNOSTIC
  return false;
#else
  if(ActiveTarget is not{} target||IsLoading||IsExporting||Editor.ReadOnlyTarget||RawMatchTiff16ProductPipeline.IsRaw(target.Path)||target.RawDefinition is not null)return false;
  var stack=Editor.AdjustmentStack; if(stack.Nodes.Any(n=>!ProfessionalRawLocalRenderer.Supports(n.Type))){StatusText="当前版本包含未适配的参考/空间节点；保留版本，未烘焙或丢弃调整。";return false;}
  var before=target.ActiveEditVersionId;Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);
  var definition=InstantFilmJpegInput.Definition with{RenderedInputVersion=2,LocalEdits=stack.Nodes.Count==0?null:new(stack.DeepClone(),2),Geometry=new ProfessionalRawGeometry(Version:2)};
  try{var master=await RenderedSrgbInput.ReadAsync(target.Path,definition,CancellationToken.None);if(!ReferenceEquals(target,ActiveTarget)||target.ActiveEditVersionId!=before||Editor.ReadOnlyTarget){master.Image.Dispose();return false;}
   await CreateR53VariantAsync(false);target.RawDefinition=definition;target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;target.LinearRawMaster=master;target.RawInput=master.Contract;CaptureEditVersion(target);await ActivateTargetAsync(target);return LocalOutputEditing;
  }catch(Exception e)when(e is IOException or InvalidOperationException or ArgumentException or NotSupportedException){StatusText="未进入非破坏裁剪："+e.Message;return false;}
#endif
 }
 public void SetOutputStraighten(double degrees){if(!LocalOutputEditing||ActiveTarget?.RawDefinition is not{} d)return;SetRawOutputGeometry((d.Geometry??new()) with{Version=2,StraightenDegrees=Math.Clamp(degrees,-15,15)});}
}
