using System.IO;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 public IReadOnlyList<string> RgbOutputSpaces{get;}=["sRGB","DisplayP3","AdobeRGB","ProPhotoRGB"];
 private string managedOutputSpace="sRGB";public string ManagedOutputSpace{get=>managedOutputSpace;set{if(RgbOutputSpaces.Contains(value))SetProperty(ref managedOutputSpace,value);}}
 private void NotifyColorState(){if(ActiveTarget?.RawDefinition?.ColorState is{} state){var id=state.Output.Label.Split(' ')[0];if(RgbOutputSpaces.Contains(id))managedOutputSpace=id;}OnPropertyChanged(nameof(ManagedOutputSpace));OnPropertyChanged(nameof(ColorManagementStatus));}
 public string AssignedInputSpace{get;set;}="sRGB";
 public bool ExplicitSdrDisplayColor{get=>Layout.ExplicitSdrDisplayColor;set{Layout=Layout with{ExplicitSdrDisplayColor=value};OnPropertyChanged();ScheduleR53Autosave();}}
 public RgbIccIdentityV1? ManualMonitorProfile=>Layout.ManualMonitorProfile;
 public void SetManualMonitorProfile(string? file){if(file is null){Layout=Layout with{ManualMonitorProfile=null};}else{var profile=RgbIccIdentityV1.FromBytes(File.ReadAllBytes(file),"Manual monitor ICC");using var cms=new LcmsRgbEngine();cms.Validate(profile);Layout=Layout with{ManualMonitorProfile=profile};}OnPropertyChanged(nameof(ManualMonitorProfile));ScheduleR53Autosave();}
 public string ColorManagementStatus=>ActiveTarget?.RawDefinition?.ColorState is{} s?$"颜色变体 V{s.Version} · {s.InputPolicy} · 输入 {s.Input.Sha256[..8]} → 编辑 {(s.Version==2?"线性ProPhoto D50":"encoded sRGB·V1")} → 输出 {s.Output.Label}":"兼容颜色路径 · 未迁移；创建颜色变体后才执行显式 ICC 转换";
 public async Task CreateManagedColorVariantAsync(bool assign=false,bool linearWide=false)
 {
#if WHOLE_APP_DIAGNOSTIC
  var target=FailedTarget??ActiveTarget;if(target is null||_disposed||IsLoading||IsExporting||IsInstantGenerating||ReferenceEquals(target,ActiveTarget)&&Editor.ReadOnlyTarget)return;
  if(RawMatchTiff16ProductPipeline.IsRaw(target.Path)){StatusText="RAW 保留 LibRaw 线性 ProPhoto D65，不能指定为 RGB 照片 ICC；此入口仅 PNG/JPEG/TIFF。";return;}
  if(target.RawDefinition is{InstantFilm:not null}){StatusText="先切回未显影照片版本；不暗迁即时胶片配方。";return;}
  var stack=ReferenceEquals(target,ActiveTarget)?Editor.AdjustmentStack:new ColorAdjustmentStack([]);if(stack.Nodes.Any(n=>!ProfessionalRawLocalRenderer.Supports(n.Type))){StatusText="颜色变体尚不接收参考匹配/空间节点，原配方保持。";return;}
  try{
   using var cms=new LcmsRgbEngine();var input=assign?(cms.StandardProfile(AssignedInputSpace),"AssignExplicitProfile"):DocumentColorWorkflowV1.InspectInput(target.Path,cms);
   if(!assign&&input.Item2=="UntaggedExplicitSrgb"&&!_dialogs.Confirm("输入没有嵌入ICC。明确按sRGB解释并记录到新变体？这不是自动识别。","未标记RGB输入"))return;
   await DocumentColorWorkflowV1.RequireOpaqueInputAsync(target.Path);
   var state=new DocumentColorStateV1(input.Item1,cms.StandardProfile(linearWide?"LinearProPhotoD50V2":"sRGB"),cms.StandardProfile(ManagedOutputSpace),input.Item2,Version:linearWide?2:1).Validate();
   var definition=InstantFilmJpegInput.Definition with{RenderedInputVersion=2,LocalEdits=stack.Nodes.Count==0?null:new(stack.DeepClone(),2),Geometry=target.RawDefinition?.Geometry,ColorState=state};definition.Validate();
   if(!await ValidateTargetIdentityAsync(target,CancellationToken.None))throw new IOException(target.Error);target.SourceHash??=target.ObservedSourceHash;var id=target.ActiveEditVersionId;var previousActive=ActiveTarget;var master=await DocumentColorWorkflowV1.ReadAsync(target.Path,definition,CancellationToken.None);
   if(_disposed||!ReferenceEquals(ActiveTarget,previousActive)||target.ActiveEditVersionId!=id||ReferenceEquals(target,ActiveTarget)&&Editor.ReadOnlyTarget){master.Image.Dispose();return;}
   if(ReferenceEquals(ActiveTarget,target)){Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);await CreateR53VariantAsync(false);}else{if(ActiveTarget is{} previous){Editor.CopyCurrentLookTo([previous]);CaptureEditVersion(previous);}ActiveTarget=target;target.ActiveEditVersionId=Guid.NewGuid();}target.RawDefinition=definition;target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;target.LinearRawMaster=master;target.RawInput=master.Contract;CaptureEditVersion(target);await ActivateTargetAsync(target);OnPropertyChanged(nameof(ColorManagementStatus));ScheduleR53Autosave();StatusText="已创建显式颜色管理变体；输入解释与输出转换已绑定ICC内容，旧变体未修改。";
  }catch(Exception e)when(e is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or ArgumentException){StatusText="未创建颜色管理变体："+e.Message;}
#endif
 }
 public async Task CreateRawWideOutputResearchAsync()
 {
#if WHOLE_APP_DIAGNOSTIC
  if(Environment.GetEnvironmentVariable("KITAO_RAW_WIDE_OUTPUT_RESEARCH")!="1"){StatusText="RAW宽色域研究变体默认关闭。";return;}
  if(ActiveTarget is not{} target||!IsLinearProfessionalRaw||target.RawDefinition is not{} old||old.RawWideOutput is not null||IsLoading||IsExporting||Editor.ReadOnlyTarget)return;
  try{if(old.LocalEdits?.Stack.Nodes.Count>0){StatusText="请从未加显示后局部层的RAW基础变体建立宽色域研究版本。";return;}using var cms=new LcmsRgbEngine();var state=new DocumentColorStateV1(cms.StandardProfile("LibRawProPhotoD65LinearV1"),cms.StandardProfile("LinearProPhotoD50V2"),cms.StandardProfile(ManagedOutputSpace),"Embedded",Version:2).Validate();var definition=old with{RawWideOutput=new(),ColorState=state,LocalEdits=null};definition.Validate();var master=target.LinearRawMaster??await _professionalDecoder.DecodeAsync(target.Path,old);var converted=await Task.Run(()=>RawWideOutputV1.ConvertMaster(master,definition));Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);await CreateR53VariantAsync(false);target.RawDefinition=definition;target.LinearRawMaster=converted;target.RawInput=converted.Contract;CaptureEditVersion(target);await ActivateTargetAsync(target);ScheduleR53Autosave();StatusText="已创建RAW宽色域研究变体；前端已截断信息不会恢复。";}catch(Exception error)when(error is IOException or InvalidOperationException or ArgumentException or NotSupportedException){StatusText="未创建RAW宽域变体："+error.Message;}
#endif
 }
 public void SetManagedOutputProfile(){if(ActiveTarget?.RawDefinition is not{ColorState:not null} definition||!LocalOutputEditing||Editor.ReadOnlyTarget)return;using var cms=new LcmsRgbEngine();RememberRawLocal(ActiveTarget,definition);ActiveTarget.RawDefinition=definition with{ColorState=definition.ColorState with{Output=cms.StandardProfile(ManagedOutputSpace)}};CaptureEditVersion(ActiveTarget);ScheduleR53Autosave();OnPropertyChanged(nameof(ColorManagementStatus));}
}
