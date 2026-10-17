using RAWSelectionAssistant.Utilities;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
 private bool _usePhotoGradePage;
 public bool UsePhotoGradePage {get=>_usePhotoGradePage;set{if(SetProperty(ref _usePhotoGradePage,value))OnPropertyChanged(nameof(UseLegacyGradePage));}}
 public bool UseLegacyGradePage=>!UsePhotoGradePage;
 public RelayCommand TogglePhotoGradePageCommand=>new(_=>UsePhotoGradePage=!UsePhotoGradePage);
 private string _photoMaskColor="Cyan";
 public IReadOnlyList<string> PhotoMaskColors {get;}=["Cyan","Yellow","Magenta","White"];
 public string PhotoMaskColor {get=>_photoMaskColor;set{if(!PhotoMaskColors.Contains(value))throw new ArgumentException("Unsupported mask colour");SetProperty(ref _photoMaskColor,value);}}
 public RelayCommand PhotoGradeSyncStepCommand=>new(_=>
 {
  if(Editor.SelectedAdjustmentNode is not{} selected||IsProfessionalRaw)return;
  SyncSelectedColorNodes(Editor.AdjustmentStack,new HashSet<Guid>{selected.Id},SelectedTargets.Where(x=>!ReferenceEquals(x,ActiveTarget)).ToArray());
 });
 public string PhotoGradeContract=>"PhotoGradePageV1 · existing display-sRGB tools · V3 follows the selected photo recipe; wide-gamut math is not silently connected";
}
