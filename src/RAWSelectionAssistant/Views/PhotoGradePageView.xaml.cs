using System.ComponentModel;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView:UserControl
{
 private ReferenceColorWorkspaceViewModel? model;
 private TetherReferenceModeViewModel? editor;
 public PhotoGradePageView()
 {
  InitializeComponent();Unloaded+=(_,_)=>{model?.ExitInstantPresentationForLifecycle();};
  DataContextChanged+=(_,_)=>{if(model is not null)model.PropertyChanged-=Changed;if(editor is not null)editor.PropertyChanged-=EditorChanged;model=DataContext as ReferenceColorWorkspaceViewModel;if(model is not null)model.PropertyChanged+=Changed;editor=model?.Editor;if(editor is not null)editor.PropertyChanged+=EditorChanged;Refresh();RefreshStep();};
 }
 private void EditorChanged(object? s,PropertyChangedEventArgs e){if(e.PropertyName is nameof(TetherReferenceModeViewModel.SelectedAdjustmentNode) or nameof(TetherReferenceModeViewModel.AdjustmentStack))RefreshStep();}
 private void RefreshStep(){CurrentParameters.NodeTypes=editor?.SelectedAdjustmentNode?.Type.ToString()??"BasicTone";}
 private void Changed(object? s,PropertyChangedEventArgs e){if(e.PropertyName==nameof(ReferenceColorWorkspaceViewModel.PhotoMaskColor))Refresh();}
 private void Refresh()
 {
  // User-selected non-red/green-only focus indication; not a fabricated computed mask.
  PhotoViewport.FocusVisualStyle=null;PhotoViewport.ToolTip=model?.PhotoMaskColor;
  OrderedSteps.BorderBrush=model?.PhotoMaskColor switch{"Yellow"=>Brushes.Yellow,"Magenta"=>Brushes.Magenta,"White"=>Brushes.White,_=>Brushes.Cyan};
  OrderedSteps.BorderThickness=new Thickness(2);
 }
}
