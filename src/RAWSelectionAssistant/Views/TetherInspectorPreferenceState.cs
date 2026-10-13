using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;

/// <summary>Shared logical state for both deferred inspector template instances.</summary>
public sealed class TetherInspectorPreferenceState : ObservableObject
{
    private TetherWorkspacePreferences _value=new();
    public double InspectorWidth=>_value.InspectorWidth;
    public void Restore(TetherWorkspacePreferences value){_value=value;OnPropertyChanged(nameof(ExposureExpanded));OnPropertyChanged(nameof(ReferenceExpanded));OnPropertyChanged(nameof(ShotExpanded));OnPropertyChanged(nameof(SessionExpanded));OnPropertyChanged(nameof(InformationExpanded));OnPropertyChanged(nameof(AnnotationExpanded));OnPropertyChanged(nameof(LutExpanded));OnPropertyChanged(nameof(ClientExpanded));OnPropertyChanged(nameof(FileExpanded));}
    public TetherWorkspacePreferences Snapshot(double width)=>_value with{InspectorWidth=width};
    public bool ExposureExpanded {get=>_value.ExposureExpanded;set{if(value==_value.ExposureExpanded)return;_value=_value with{ExposureExpanded=value};OnPropertyChanged();}}
    public bool ReferenceExpanded {get=>_value.ReferenceExpanded;set{if(value==_value.ReferenceExpanded)return;_value=_value with{ReferenceExpanded=value};OnPropertyChanged();}}
    public bool ShotExpanded {get=>_value.ShotExpanded;set{if(value==_value.ShotExpanded)return;_value=_value with{ShotExpanded=value};OnPropertyChanged();}}
    public bool SessionExpanded {get=>_value.SessionExpanded;set{if(value==_value.SessionExpanded)return;_value=_value with{SessionExpanded=value};OnPropertyChanged();}}
    public bool InformationExpanded {get=>_value.InformationExpanded;set{if(value==_value.InformationExpanded)return;_value=_value with{InformationExpanded=value};OnPropertyChanged();}}
    public bool AnnotationExpanded {get=>_value.AnnotationExpanded;set{if(value==_value.AnnotationExpanded)return;_value=_value with{AnnotationExpanded=value};OnPropertyChanged();}}
    public bool LutExpanded {get=>_value.LutExpanded;set{if(value==_value.LutExpanded)return;_value=_value with{LutExpanded=value};OnPropertyChanged();}}
    public bool ClientExpanded {get=>_value.ClientExpanded;set{if(value==_value.ClientExpanded)return;_value=_value with{ClientExpanded=value};OnPropertyChanged();}}
    public bool FileExpanded {get=>_value.FileExpanded;set{if(value==_value.FileExpanded)return;_value=_value with{FileExpanded=value};OnPropertyChanged();}}
}
