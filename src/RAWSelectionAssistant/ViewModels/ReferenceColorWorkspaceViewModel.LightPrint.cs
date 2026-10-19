using System.ComponentModel;using System.Security.Cryptography;using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Services;using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private bool _lightPrintEnabled,_developingLightPrint,_lightPrintReshoot,_restoringLightPrintControls;
 private CancellationTokenSource? _lightPrintPreviewCancellation;
 private long _lightPrintPreviewRevision;
 private TetherReferenceModeViewModel.PreparedSourceState? _lightPrintBeforeDraft;
 private Guid? _lightPrintDraftTarget;
 private string _lightPrintStatus="选择相纸和相框，画面实时预览；显影后生成独立变体。";
 private string _selectedLightPrintFrameId="generic.white.square";
 public string LightPrintCamera {get=>InstantCamera;set{if(IsLightPrintLocked)return;if(value is "SX-70" or "i-2"){InstantCamera=value;OnPropertyChanged(nameof(LightPrintCamera));}}}
 public IReadOnlyList<string> LightPrintCameras {get;}=["SX-70","i-2"];
 public IReadOnlyList<LightPrintFrame> LightPrintFrameChoices=>LightPrintFrames.Available;
 public string SelectedLightPrintFrameId {get=>_selectedLightPrintFrameId;set{if(!LightPrintFrames.Available.Any(f=>f.Id==value&&f.Shippable))return;if(IsLightPrintLocked){LightPrintStatus="此光片已显影；点从原图重新拍一张。";return;}if(SetProperty(ref _selectedLightPrintFrameId,value))QueueLightPrintPreview();}}
 public bool LightPrintRecipeVisible=>!IsLightPrintLocked;
 public bool IsLightPrintLocked=>_lightPrintEnabled&&ActiveTarget?.RawDefinition?.LightPrint is not null&&!_lightPrintReshoot;
 public bool LightPrintControlsEnabled=>ActiveTarget is not null&&!IsLightPrintLocked&&!IsInstantGenerating&&!IsLoading&&!IsExporting;
 public string LightPrintStatus {get=>_lightPrintStatus;private set=>SetProperty(ref _lightPrintStatus,value);}
 public string LightPrintRecipeSummary=>ActiveTarget?.RawDefinition?.LightPrint is{} light?$"已显影 · {PhotographerPaperName} · {LightPrintFrames.Available.First(f=>f.Id==light.FrameId).Name} · 种子 {ActiveTarget.RawDefinition.InstantFilm!.V3!.Random.RootSeed}":"未显影预览 · 不写历史";
 public Task LightPrintPreviewWork {get;private set;}=Task.CompletedTask;
 public AsyncRelayCommand DevelopLightPrintCommand {get;private set;}=null!;
 public RelayCommand ReshootLightPrintCommand {get;private set;}=null!;
 public RelayCommand CancelLightPrintPreviewCommand {get;private set;}=null!;
 public void RequestLightPrintPreview()=>QueueLightPrintPreview();
 public AsyncRelayCommand NewNormalLightPrintVariantCommand {get;private set;}=null!;
 public AsyncRelayCommand ApplyWindowGradeCommand {get;private set;}=null!;
 private readonly Dictionary<(Guid,Guid),Stack<ProfessionalRawDefinition>> _windowUndo=[],_windowRedo=[];
 public void EnableLightPrintWorkspace(){if(_lightPrintEnabled){NotifyLightPrint();return;}_lightPrintEnabled=true;if(ActiveTarget?.RawDefinition?.LightPrint is{} l)_selectedLightPrintFrameId=l.FrameId;InstantEngineVersion="V3";NotifyLightPrint();}
 private void NotifyLightPrint(){foreach(var n in new[]{nameof(IsLightPrintLocked),nameof(LightPrintRecipeVisible),nameof(InstantEditingAllowed),nameof(LightPrintControlsEnabled),nameof(LightPrintRecipeSummary),nameof(RasterAvailabilityReason),nameof(DarkroomAdjustmentEnabled)})OnPropertyChanged(n);DevelopLightPrintCommand?.RaiseCanExecuteChanged();ApplyWindowGradeCommand?.RaiseCanExecuteChanged();}
 private void InitializeLightPrintCommands()
 {
  var cameraSettings=System.IO.Path.Combine(RAWSelectionAssistant.Core.Utilities.AppDataPaths.Root,"lightprint-camera.json");
  try{if(File.Exists(cameraSettings))InstantCameraAnimation=System.Text.Json.JsonDocument.Parse(File.ReadAllText(cameraSettings)).RootElement.GetProperty("Enabled").GetBoolean();}catch(Exception e)when(e is IOException or System.Text.Json.JsonException or KeyNotFoundException){}

  PropertyChanged+=(_,e)=>
  {
   if(_lightPrintEnabled&&e.PropertyName==nameof(InstantCameraAnimation))
   {
    try{var path=System.IO.Path.Combine(RAWSelectionAssistant.Core.Utilities.AppDataPaths.Root,"lightprint-camera.json");Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new{Enabled=InstantCameraAnimation}));}catch(IOException){}
   }
   if(e.PropertyName is nameof(ActiveTarget) or nameof(IsLoading) or nameof(IsInstantGenerating)){NotifyLightPrint();return;}
   if(_lightPrintEnabled&&!_restoringLightPrintControls&&e.PropertyName is{} name&&(name==nameof(SelectedInstantProfile)||name.StartsWith("InstantV3",StringComparison.Ordinal)&&name is not(nameof(InstantV3Stocks) or nameof(InstantV3Patterns) or nameof(InstantV3DefectKinds))||name is nameof(InstantAmount) or nameof(InstantUneven) or nameof(InstantSecondSourceName) or nameof(InstantSecondWeight) or nameof(InstantSecondCropX) or nameof(InstantSecondCropY) or nameof(InstantSecondZoom) or nameof(InstantFit) or nameof(InstantCropX) or nameof(InstantCropY)))QueueLightPrintPreview();
  };
  DevelopLightPrintCommand=new(async _=>
  {
   if(_disposed||IsLoading||IsExporting||IsInstantGenerating||IsLightPrintLocked||ActiveTarget is not{} target)return;
   CancelLightPrintTask(false);await LightPrintPreviewWork;if(_disposed||!ReferenceEquals(ActiveTarget,target))return;_developingLightPrint=true;var animation=InstantDevelopmentDemo;
   try{if(_lightPrintBeforeDraft is{} prior)Editor.RestorePreparedSource(prior,target.AssetId);InstantDevelopmentDemo=InstantCameraAnimation;InstantEngineVersion="V3";await GenerateInstantFilmAsync(false);if(target.RawDefinition?.LightPrint is not null){_lightPrintReshoot=false;_lightPrintBeforeDraft=null;LightPrintStatus="显影完成 · 已锁定；可在调整页编辑画面窗口。";}}
   finally{_developingLightPrint=false;InstantDevelopmentDemo=animation;NotifyLightPrint();}
  },_=>ActiveTarget is not null&&!IsLightPrintLocked&&!IsLoading&&!IsExporting&&!IsInstantGenerating);
  ReshootLightPrintCommand=new(_=>{if(_disposed||IsLoading||IsExporting||IsInstantGenerating||ActiveTarget is null)return;CancelLightPrintTask(false);_lightPrintReshoot=true;InstantInputMode="Original";InstantV3Seed=BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));LightPrintStatus="从原图重新拍摄 · 旧变体保留；显影后生成新变体。";NotifyLightPrint();QueueLightPrintPreview();});
  CancelLightPrintPreviewCommand=new(_=>CancelLightPrintTask(true));
  NewNormalLightPrintVariantCommand=new(async _=>
  {
   if(ActiveTarget is not{} target||IsLoading||IsExporting||IsInstantGenerating)return;
   CancelLightPrintTask(true);await LightPrintPreviewWork;CaptureEditVersion(target);var prior=target.ActiveEditVersionId;
   target.ActiveEditVersionId=Guid.NewGuid();target.RawDefinition=null;target.RawInput=null;target.LinearRawMaster=null;target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;CaptureEditVersion(target);
   var index=target.EditVersions.ToList().FindIndex(v=>v.Id==target.ActiveEditVersionId);target.EditVersions[index]=target.EditVersions[index] with{Name="普通编辑 · 原图",OriginVersionId=prior};await ActivateTargetAsync(target);NotifyLightPrint();
  });
  ApplyWindowGradeCommand=new(async _=>
  {
   if(_disposed||IsLoading||IsExporting||IsInstantGenerating||ActiveTarget is not{} target||target.RawDefinition is not{LightPrint:not null} prior||target.ActiveEditVersionId is not Guid version)return;
   var stack=Editor.AdjustmentStack.DeepClone();
   if(stack.Nodes.Any(n=>n.Type is not(ColorStudioNodeType.BasicTone or ColorStudioNodeType.WhiteBalance or ColorStudioNodeType.Curve))){LightPrintStatus="首版窗口调色仅接通影调、白平衡和曲线。";return;}
   CancelLightPrintTask(false);await LightPrintPreviewWork;await StopComparisonAsync();
   var revision=Volatile.Read(ref _activationRevision);var oldSource=Editor.CapturePreparedSource();var oldHistory=Editor.CaptureSessionHistory();var oldVersions=target.EditVersions.ToArray();var oldStatus=target.Status;var oldError=target.Error;
   var key=(target.Id,version);var next=prior with{LightPrint=prior.LightPrint! with{WindowAdjustments=stack}};
   _instantCancellation?.Dispose();_instantCancellation=new();var token=_instantCancellation.Token;IsInstantGenerating=true;InstantChanged();
   try
   {
    if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
    var master=ReusableMaster(target,next)??await DecodeProfessionalInputAsync(target.Path,next,token);
    var prepared=await PrepareProfessionalPreviewAsync(target,master,next,token);
    EnsurePreparedCurrent(target,prior,version,revision,token);target.RawDefinition=next;
    await PresentProfessionalPreviewAsync(target,prepared,revision,token);
    EnsurePreparedCurrent(target,next,version,revision,token);
    if(!_windowUndo.TryGetValue(key,out var undo))_windowUndo[key]=undo=new();undo.Push(prior);_windowRedo.Remove(key);CaptureEditVersion(target);LightPrintStatus="窗口调整已提交，相框不染色。";
   }
   catch(Exception e)when(e is OperationCanceledException or IOException or InvalidOperationException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.COMException)
   {
    if(target.ActiveEditVersionId==version&&target.RawDefinition==next)
    {target.RawDefinition=prior;target.EditVersions.Clear();foreach(var item in oldVersions)target.EditVersions.Add(item);}
    if(!_disposed&&ReferenceEquals(ActiveTarget,target)&&target.ActiveEditVersionId==version)
    {Editor.ApplyTargetSnapshot(null,null,prior.LightPrint!.WindowAdjustments,render:false,targetIdentity:version);Editor.RestorePreparedSource(oldSource,target.AssetId);Editor.RestoreSessionHistory(oldHistory);target.Status=oldStatus;target.Error=oldError;}
    LightPrintStatus="窗口调整未提交，旧结果保持："+e.Message;
   }
   finally{IsInstantGenerating=false;InstantChanged();NotifyLightPrint();}
  },_=>ActiveTarget?.RawDefinition?.LightPrint is not null&&!IsLoading&&!IsInstantGenerating&&!IsExporting);

 }
 private InstantFilmDefinition ApplyLightPrintFrame(InstantFilmDefinition film)
 {
  var frame=LightPrintFrames.Available.Single(f=>f.Id==SelectedLightPrintFrameId&&f.Shippable);var r=film.V3!;
  return film with{PaperFrame=true,V3=r with{Surface=r.Surface with{Frame=true,BaseColor=frame.Color},Geometry=new InstantPaperGeometry(Fit:InstantFit,CropX:InstantCropX,CropY:InstantCropY),Paper=(r.Paper??new()) with{Version=3,Window=frame.Window,Pattern=frame.Pattern}}};
 }
 private void CancelLightPrintTask(bool restore)
 {
  Interlocked.Increment(ref _lightPrintPreviewRevision);_lightPrintPreviewCancellation?.Cancel();
  if(restore&&!_disposed&&_lightPrintBeforeDraft is{} prior&&ActiveTarget?.Id==_lightPrintDraftTarget){Editor.RestorePreparedSource(prior,ActiveTarget!.AssetId);_lightPrintBeforeDraft=null;_lightPrintReshoot=false;RestoreInstantControls(ActiveTarget.RawDefinition?.InstantFilm);NotifyLightPrint();LightPrintStatus="预览已取消，保留上一有效结果。";}
 }
 private void QueueLightPrintPreview()
 {
  if(!_lightPrintEnabled||_developingLightPrint||_disposed||IsLightPrintLocked||IsLoading||IsExporting||IsInstantGenerating||ActiveTarget is not{} target)return;
  InstantEngineVersion="V3";_lightPrintPreviewCancellation?.Cancel();var cts=new CancellationTokenSource();_lightPrintPreviewCancellation=cts;var revision=Interlocked.Increment(ref _lightPrintPreviewRevision);var activation=Volatile.Read(ref _activationRevision);var version=target.ActiveEditVersionId;
  if(_lightPrintDraftTarget!=target.Id){_lightPrintBeforeDraft=null;_lightPrintDraftTarget=target.Id;}
  _lightPrintBeforeDraft??=Editor.CapturePreparedSource();LightPrintStatus="实时预览…（未显影，不改旧变体）";
  var priorWork=LightPrintPreviewWork;LightPrintPreviewWork=RenderAsync();
  async Task RenderAsync()
  {
   var token=cts.Token;try
   {
    await Task.Yield();await priorWork;token.ThrowIfCancellationRequested();Editor.CopyCurrentLookTo([target]);var draft=BuildInstantDefinition(target,target.RawDefinition,version,false);draft=draft with{InstantFilm=ApplyLightPrintFrame(draft.InstantFilm!)};await Task.Delay(90,token);if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
    var definition=draft;
    // Quick draft uses a labelled bounded input; developing/export always use the full frozen source.
    ProfessionalRawMaster master;
    if(RawMatchTiff16ProductPipeline.IsRaw(target.Path))master=ReusableMaster(target,definition)??await DecodeProfessionalInputAsync(target.Path,definition,token);
    else
    {
     BitmapSource source;
     if(definition.InstantInput is {Mode:"CurrentEdits"} input)
     {
      source=await Editor.ProcessForExportAsync(target.Path,input.Look,input.Film,token,input.Stack);
      if(Math.Max(source.PixelWidth,source.PixelHeight)>768){var scale=768d/Math.Max(source.PixelWidth,source.PixelHeight);source=new System.Windows.Media.Imaging.TransformedBitmap(source,new System.Windows.Media.ScaleTransform(scale,scale));source.Freeze();}
     }
     else source=await Task.Run(()=>StudioQuickExport.Load(target.Path,768),token);
     master=InstantFilmJpegInput.FromRendered(source,target.SourceHash??target.ObservedSourceHash!,definition);
    }
    using var draftSource=RawMatchTiff16ProductPipeline.IsRaw(target.Path)?null:master.Image;
    using var pin=StudioGlobalResources.RetainBuffer(master.Image,(long)master.Image.Rgb32.Length*4);
    var pixels=await RenderProfessionalPreviewAsync(master,definition,900,token);BitmapSource image;
    try{image=RawDisplayBitmapAdapter.ToBitmap(pixels.ToVisualRgb24(token));}finally{pixels.Dispose();}
    if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);definition.InstantFilm?.V3?.MultipleInput?.ValidateSource(token);token.ThrowIfCancellationRequested();
    if(_disposed||revision!=Volatile.Read(ref _lightPrintPreviewRevision)||activation!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(target,ActiveTarget)||version!=target.ActiveEditVersionId)return;
    Editor.IndependentRawMode=true;await Editor.SetSourceAsync(target.AssetId,null,token,render:false);Editor.PresentIndependentRaw(image);Editor.SetIndependentRawSize(new(image.PixelWidth,image.PixelHeight));LightPrintStatus="快速预览 · 正在核对原尺寸效果";
    CandidateProcessingEvidence.Enter("LightPrintQuickVisible",token);
    var full=ReusableMaster(target,definition)??await DecodeProfessionalInputAsync(target.Path,definition,token);
    using var fullOwner=ReferenceEquals(full.Image,target.LinearRawMaster?.Image)?null:full.Image;
    using var fullLease=StudioGlobalResources.RetainBuffer(full.Image,(long)full.Image.Rgb32.Length*4);
    var high=await RenderProfessionalPreviewAsync(full,definition,1600,token);BitmapSource complete;
    try{complete=RawDisplayBitmapAdapter.ToBitmap(high.ToVisualRgb24(token));}finally{high.Dispose();}
    CandidateProcessingEvidence.Enter("LightPrintHqPrepared",token);
    if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);definition.InstantFilm?.V3?.MultipleInput?.ValidateSource(token);token.ThrowIfCancellationRequested();
    if(_disposed||revision!=Volatile.Read(ref _lightPrintPreviewRevision)||activation!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(target,ActiveTarget)||version!=target.ActiveEditVersionId)return;
    Editor.PresentIndependentRaw(complete);var outputSize=ProfessionalRawPipeline.OutputSize(full,definition);Editor.SetIndependentRawSize(new(outputSize.Width,outputSize.Height));LightPrintStatus="预览就绪 · 点显影生成独立变体";
   }
   catch(OperationCanceledException){}
   catch(Exception e)when(e is IOException or InvalidOperationException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException){if(revision==Volatile.Read(ref _lightPrintPreviewRevision))LightPrintStatus="预览未完成，旧结果保留："+e.Message;}
   finally{if(ReferenceEquals(_lightPrintPreviewCancellation,cts))_lightPrintPreviewCancellation=null;cts.Dispose();}
  }
 }
 private async Task<bool> MoveWindowHistoryAsync(bool redo)
 {
  if(ActiveTarget is not{} target||target.RawDefinition?.LightPrint is null||target.ActiveEditVersionId is not Guid id)return false;
  var from=redo?_windowRedo:_windowUndo;var to=redo?_windowUndo:_windowRedo;var key=(target.Id,id);if(!from.TryGetValue(key,out var values)||values.Count==0)return false;
  var current=target.RawDefinition;var oldStatus=target.Status;var oldError=target.Error;var next=values.Peek();var source=Editor.CapturePreparedSource();var history=Editor.CaptureSessionHistory();target.RawDefinition=next;await ActivateTargetAsync(target);
  if(target.Status==ReferenceTargetStatus.Failed){target.RawDefinition=current;Editor.RestorePreparedSource(source,target.AssetId);Editor.RestoreSessionHistory(history);target.Status=oldStatus;target.Error=oldError;LightPrintStatus="窗口历史恢复失败，旧结果保持。";return true;}
  values.Pop();CaptureEditVersion(target);if(!to.TryGetValue(key,out var other))to[key]=other=new();other.Push(current);return true;
 }
}
