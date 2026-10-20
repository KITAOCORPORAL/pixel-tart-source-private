using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
#if PHOTOGRAPHER_TRIAL
 private bool _usePhotoGradePage=true;
#else
 private bool _usePhotoGradePage;
#endif
 public bool UsePhotoGradePage {get=>_usePhotoGradePage;set{if(SetProperty(ref _usePhotoGradePage,value))OnPropertyChanged(nameof(UseLegacyGradePage));}}
 public bool UseLegacyGradePage=>!UsePhotoGradePage;
 public RelayCommand TogglePhotoGradePageCommand=>new(_=>UsePhotoGradePage=!UsePhotoGradePage);
 public AsyncRelayCommand PhotoGradeUndoCommand {get;private set;}=null!;
 public AsyncRelayCommand PhotoGradeRedoCommand {get;private set;}=null!;
 private bool HasInstantHistory(Dictionary<Guid,Stack<Guid>> history)=>ActiveTarget is{} target&&history.TryGetValue(target.Id,out var entries)&&entries.Count>0;
 public RelayCommand ResetPhotoV3DraftCommand {get;private set;}=null!;
 private void InitializePhotoGradeHistoryCommands()
 {
  InitializeSrgbExposureCommands();InitializeComparison();InitializePhotographerCommands();InitializeLightPrintCommands();
  ResetPhotoV3DraftCommand=new(_=>{if(IsLightPrintLocked)return;var profile=SelectedInstantProfile;RestoreInstantControls(null);SelectedInstantProfile=profile;InstantEngineVersion="V3";},_=>!IsLightPrintLocked&&!IsInstantGenerating&&!IsLoading&&!IsExporting&&ActiveTarget is not null);
  PhotoGradeUndoCommand=new(async _=>
  {
   if(await MoveWindowHistoryAsync(false))return;
   if(HasInstantHistory(_instantV3Undo))await UndoInstantV3Command.ExecuteAsync(null);
   else if(!IsProfessionalRaw)Editor.UndoAdjustmentCommand.Execute(null);
  },_=>!IsInstantGenerating&&!IsLoading&&!IsExporting);
  PhotoGradeRedoCommand=new(async _=>
  {
   // Undo can return to a raster version, so route by history, not only current engine.
   if(await MoveWindowHistoryAsync(true))return;
   if(HasInstantHistory(_instantV3Redo))await RedoInstantV3Command.ExecuteAsync(null);
   else if(!IsProfessionalRaw)Editor.RedoAdjustmentCommand.Execute(null);
  },_=>!IsInstantGenerating&&!IsLoading&&!IsExporting);
 }
 private string _photoMaskColor="Cyan";
 public IReadOnlyList<string> PhotoMaskColors {get;}=["Cyan","Yellow","Magenta","White"];
 public string PhotoMaskColor {get=>_photoMaskColor;set{if(!PhotoMaskColors.Contains(value))throw new ArgumentException("Unsupported mask colour");SetProperty(ref _photoMaskColor,value);}}
 public RelayCommand PhotoGradeSyncStepCommand=>new(_=>
 {
  if(!PhotoGradeCanSyncStep||Editor.SelectedAdjustmentNode is not{} selected)return;
  SyncSelectedColorNodes(Editor.AdjustmentStack,new HashSet<Guid>{selected.Id},SelectedTargets.Where(x=>!ReferenceEquals(x,ActiveTarget)).ToArray());
 },_=>PhotoGradeCanSyncStep);
 public bool PhotoGradeOriginalInputAllowed=>ActiveTarget is{} target&&(RAWSelectionAssistant.Core.Services.Projects.RawMatchTiff16ProductPipeline.IsRaw(target.Path)||System.IO.Path.GetExtension(target.Path).ToLowerInvariant() is ".jpg" or ".jpeg"||IsInstantV3Selected&&System.IO.Path.GetExtension(target.Path).ToLowerInvariant() is ".tif" or ".tiff" or ".png");
 public bool PhotoGradeCanSyncStep=>PhotoGradeRasterToolsEnabled&&Editor.SelectedAdjustmentNode is not null&&SelectedTargets.Any(t=>!ReferenceEquals(t,ActiveTarget)&&t.RawDefinition is null);
 public bool PhotoGradeRasterToolsEnabled=>ActiveTarget is not null&&!IsProfessionalRaw&&!IsLoading&&!IsExporting&&!IsInstantGenerating;
 public string PhotoGradeContract=>"PhotoGradePageV1 · existing display-sRGB tools · V3 follows the selected photo recipe; wide-gamut math is not silently connected";
}
