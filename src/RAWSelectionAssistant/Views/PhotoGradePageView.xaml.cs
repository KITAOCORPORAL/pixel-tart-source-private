using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView:UserControl
{
 private ReferenceColorWorkspaceViewModel? model;
 private TetherReferenceModeViewModel? editor;
 public PhotoGradePageView()
 {
  InitializeComponent();
  DataContextChanged+=(_,_)=>Attach();
  Loaded+=(_,_)=>Attach();
  Unloaded+=(_,_)=>Detach(cancel:true);
  IsVisibleChanged+=(_,_)=>{if(IsVisible)Attach();else Detach(cancel:true);};
 }
 private void Attach()
 {
  Detach(cancel:false);model=DataContext as ReferenceColorWorkspaceViewModel;editor=model?.Editor;
  // Weak subscription also covers controls constructed but never attached/unloaded.
  if(model is not null)PropertyChangedEventManager.AddHandler(model,Changed,string.Empty);
  if(editor is not null)PropertyChangedEventManager.AddHandler(editor,EditorChanged,string.Empty);
  Refresh();RefreshStep();
 }
 private void Detach(bool cancel)
 {
  if(model is not null){PropertyChangedEventManager.RemoveHandler(model,Changed,string.Empty);if(cancel)model.ExitInstantPresentationForLifecycle();}
  if(editor is not null)PropertyChangedEventManager.RemoveHandler(editor,EditorChanged,string.Empty);
  model=null;editor=null;
 }
 private void EditorChanged(object? sender,PropertyChangedEventArgs e)
 {
  if(e.PropertyName is nameof(TetherReferenceModeViewModel.SelectedAdjustmentNode) or nameof(TetherReferenceModeViewModel.AdjustmentStack))RefreshStep();
 }
 private void RefreshStep()=>CurrentParameters.NodeTypes=editor?.SelectedAdjustmentNode?.Type.ToString()??"BasicTone";
 private void Changed(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(ReferenceColorWorkspaceViewModel.PhotoMaskColor))Refresh();}
 private void Refresh()
 {
  PhotoViewport.FocusVisualStyle=null;PhotoViewport.ToolTip=model?.PhotoMaskColor;
  OrderedSteps.BorderBrush=model?.PhotoMaskColor switch{"Yellow"=>Brushes.Yellow,"Magenta"=>Brushes.Magenta,"White"=>Brushes.White,_=>Brushes.Cyan};
  OrderedSteps.BorderThickness=new Thickness(2);
 }
}
