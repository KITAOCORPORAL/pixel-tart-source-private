using RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private double _srgbExposureEv;
 public double SrgbExposureEv {get=>_srgbExposureEv;set=>SetProperty(ref _srgbExposureEv,value);}
 public bool SrgbExposureAvailable=>ActiveTarget is{} target&&!RawMatchTiff16ProductPipeline.IsRaw(target.Path)&&target.RawDefinition?.InstantFilm is null;
 public AsyncRelayCommand ApplySrgbExposureCommand {get;private set;}=null!;
 private void InitializeSrgbExposureCommands()
 {
  ApplySrgbExposureCommand=new(async _=>
  {
   if(!SrgbExposureAvailable||ActiveTarget is not{} target||IsLoading||IsExporting||IsInstantGenerating)return;
   Editor.CopyCurrentLookTo([target]);
   var prior=target.RawDefinition;var active=target.ActiveEditVersionId;var revision=Volatile.Read(ref _activationRevision);
   var oldImage=Editor.MatchedImage;var oldSource=Editor.SourceImage;var oldMode=Editor.IndependentRawMode;var oldHistory=Editor.CaptureSessionHistory();var oldSize=Editor.SourcePixelSize;var oldStatus=target.Status;var oldError=target.Error;
   var oldRawMaster=target.RawMaster;var oldWidth=target.PixelWidth;var oldHeight=target.PixelHeight;var oldThumb=target.Thumbnail;var oldSourceThumb=target.SourceThumbnail;var oldEnabled=Editor.Enabled;var oldView=Editor.ViewMode;var oldTargetImage=TargetImage;var oldFailed=FailedTarget;
        var oldInput=target.RawInput;var oldMaster=target.LinearRawMaster;using var oldMasterLease=oldMaster is null?null:StudioGlobalResources.RetainBuffer(oldMaster.Image,(long)oldMaster.Image.Rgb32.Length*4);var oldLook=target.AppliedLookSnapshot;var oldStack=target.ColorAdjustmentStackSnapshot;var oldFilm=target.FilmSettingsSnapshot;Guid? committed=null;
   _instantCancellation?.Dispose();_instantCancellation=new();var token=_instantCancellation.Token;IsInstantGenerating=true;InstantChanged();
   Editor.StopProcessing();InvalidateProfessionalPreview();LastProcessingFailure=null;
   try
   {
    if(!await ValidateTargetIdentityAsync(target,token))throw new IOException(target.Error);
    var basis=InstantFilmJpegInput.Definition;var next=basis with{RenderedInputVersion=2,SrgbExposure=new RenderedSrgbExposure(ExposureEv:SrgbExposureEv).Validate()};next.Validate();
    var master=target.LinearRawMaster?.Contract.DecodeKey==next.DecodeKey&&string.Equals(target.LinearRawMaster.Contract.SourceSha256,target.SourceHash??target.ObservedSourceHash,StringComparison.OrdinalIgnoreCase)?target.LinearRawMaster:await RenderedSrgbInput.ReadAsync(target.Path,next,token);
    var prepared=await PrepareProfessionalPreviewAsync(target,master,next,token);
    EnsurePreparedCurrent(target,prior,active,revision,token);
    token.ThrowIfCancellationRequested();Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);var previous=target.ActiveEditVersionId;
    target.ActiveEditVersionId=Guid.NewGuid();committed=target.ActiveEditVersionId;target.RawDefinition=next;target.LinearRawMaster=master;target.RawInput=master.Contract;target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;CaptureEditVersion(target);
    EnsurePreparedCurrent(target,next,committed,revision,token);await PresentProfessionalPreviewAsync(target,prepared,revision,token);
    if(target.Status==ReferenceTargetStatus.Failed)throw new IOException(target.Error);
    if(previous is Guid id){if(!_instantV3Undo.TryGetValue(target.Id,out var undo))_instantV3Undo[target.Id]=undo=new();undo.Push(id);}if(_instantV3Redo.TryGetValue(target.Id,out var redo))redo.Clear();
   }
   catch(Exception e)when(e is OperationCanceledException or IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException or RAWSelectionAssistant.Core.Services.RawToJpeg.RawDecodeException)
   {
    if(committed is Guid id&&target.ActiveEditVersionId==id)
    {target.ActiveEditVersionId=active;target.RawDefinition=prior;target.RawInput=oldInput;target.RawMaster=_disposed?null:oldRawMaster;target.LinearRawMaster=_disposed||!ReferenceEquals(ActiveTarget,target)?null:oldMaster;target.PixelWidth=oldWidth;target.PixelHeight=oldHeight;target.Thumbnail=oldThumb;target.SourceThumbnail=oldSourceThumb;target.AppliedLookSnapshot=oldLook;target.ColorAdjustmentStackSnapshot=oldStack;target.FilmSettingsSnapshot=oldFilm;var version=target.EditVersions.FirstOrDefault(x=>x.Id==id);if(version is not null)target.EditVersions.Remove(version);}
    if(!_disposed&&!IsLoading&&ReferenceEquals(ActiveTarget,target))
    {
     RestoreInstantControls(prior?.InstantFilm);Editor.ApplyTargetSnapshot(oldLook,oldFilm,oldStack,render:false,targetIdentity:active??target.Id);await Editor.SetSourceAsync(target.AssetId,oldSource,render:false);
     if(oldImage is not null)Editor.PresentIndependentRaw(oldImage);Editor.SetIndependentRawSize(oldSize);Editor.IndependentRawMode=oldMode;Editor.Enabled=oldEnabled;Editor.ViewMode=oldView;TargetImage=oldTargetImage;FailedTarget=oldFailed;Editor.RestoreSessionHistory(oldHistory);target.Status=oldStatus;target.Error=oldError;
    }
    LastProcessingFailure=StudioProcessingFailure.From(e,"RenderedSrgbExposure");OnPropertyChanged(nameof(LastProcessingFailure));StatusText="sRGB 曝光未提交，保留此前版本："+e.Message;
   }
   finally{IsInstantGenerating=false;InstantChanged();ApplySrgbExposureCommand.RaiseCanExecuteChanged();}
  },_=>SrgbExposureAvailable&&!IsLoading&&!IsExporting&&!IsInstantGenerating);
 }
}
