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
   var prior=target.RawDefinition;var active=target.ActiveEditVersionId;var revision=Volatile.Read(ref _activationRevision);
   var oldImage=Editor.MatchedImage;var oldSize=Editor.SourcePixelSize;var oldStatus=target.Status;var oldError=target.Error;
   var oldInput=target.RawInput;var oldMaster=target.LinearRawMaster;var oldLook=target.AppliedLookSnapshot;var oldStack=target.ColorAdjustmentStackSnapshot;var oldFilm=target.FilmSettingsSnapshot;Guid? committed=null;
   _instantCancellation?.Dispose();_instantCancellation=new();var token=_instantCancellation.Token;IsInstantGenerating=true;InstantChanged();
   try
   {
    var basis=InstantFilmJpegInput.Definition;var next=basis with{RenderedInputVersion=2,SrgbExposure=new RenderedSrgbExposure(ExposureEv:SrgbExposureEv).Validate()};next.Validate();
    var master=target.LinearRawMaster?.Contract.DecodeKey==next.DecodeKey?target.LinearRawMaster:await RenderedSrgbInput.ReadAsync(target.Path,next,token);
    using var preview=await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(master,next,1600,token),token);
    if(_disposed||revision!=Volatile.Read(ref _activationRevision)||!ReferenceEquals(ActiveTarget,target)||target.RawDefinition!=prior||target.ActiveEditVersionId!=active)throw new OperationCanceledException(token);
    token.ThrowIfCancellationRequested();Editor.CopyCurrentLookTo([target]);CaptureEditVersion(target);var previous=target.ActiveEditVersionId;
    target.ActiveEditVersionId=Guid.NewGuid();committed=target.ActiveEditVersionId;target.RawDefinition=next;target.LinearRawMaster=master;target.RawInput=master.Contract;target.AppliedLookSnapshot=null;target.ColorAdjustmentStackSnapshot=null;target.FilmSettingsSnapshot=null;CaptureEditVersion(target);
    IsInstantGenerating=false;await ActivateTargetAsync(target);
    if(target.Status==ReferenceTargetStatus.Failed)throw new IOException(target.Error);
    if(previous is Guid id){if(!_instantV3Undo.TryGetValue(target.Id,out var undo))_instantV3Undo[target.Id]=undo=new();undo.Push(id);}if(_instantV3Redo.TryGetValue(target.Id,out var redo))redo.Clear();
   }
   catch(Exception e)when(e is OperationCanceledException or IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException)
   {
    if(committed is Guid id&&target.ActiveEditVersionId==id)
    {target.ActiveEditVersionId=active;target.RawDefinition=prior;target.RawInput=oldInput;target.LinearRawMaster=oldMaster;target.AppliedLookSnapshot=oldLook;target.ColorAdjustmentStackSnapshot=oldStack;target.FilmSettingsSnapshot=oldFilm;var version=target.EditVersions.FirstOrDefault(x=>x.Id==id);if(version is not null)target.EditVersions.Remove(version);}
    if(ReferenceEquals(ActiveTarget,target)){RestoreInstantControls(prior?.InstantFilm);if(oldImage is not null){Editor.PresentIndependentRaw(oldImage);Editor.SetIndependentRawSize(oldSize);}target.Status=oldStatus;target.Error=oldError;}StatusText="sRGB exposure: "+e.Message;
   }
   finally{IsInstantGenerating=false;InstantChanged();ApplySrgbExposureCommand.RaiseCanExecuteChanged();}
  },_=>SrgbExposureAvailable&&!IsLoading&&!IsExporting&&!IsInstantGenerating);
 }
}
