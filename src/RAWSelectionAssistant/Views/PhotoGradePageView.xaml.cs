using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
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
 private Point? panStart;
 private void FitPhoto(object sender,RoutedEventArgs e)=>PhotoViewport.State.Fit();
 private void ActualPhoto(object sender,RoutedEventArgs e)=>PhotoViewport.State.SetZoom(1);
 private void ZoomOut(object sender,RoutedEventArgs e)=>PhotoViewport.State.SetZoom(PhotoViewport.State.Zoom/1.25);
 private void ZoomIn(object sender,RoutedEventArgs e)=>PhotoViewport.State.SetZoom(PhotoViewport.State.Zoom*1.25);
 private void PhotoWheel(object sender,MouseWheelEventArgs e){PhotoViewport.State.ZoomAbout(e.GetPosition(PhotoViewport),e.Delta>0?1.15:1/1.15);e.Handled=true;}
 private void PhotoDown(object sender,MouseButtonEventArgs e){if(e.ClickCount==2){PhotoViewport.State.Fit();return;}panStart=e.GetPosition(PhotoViewport);PhotoViewport.CaptureMouse();}
 private void PhotoMove(object sender,MouseEventArgs e){if(panStart is{} start&&e.LeftButton==MouseButtonState.Pressed){var next=e.GetPosition(PhotoViewport);PhotoViewport.State.PanBy(next-start);panStart=next;}}
 private void PhotoUp(object sender,MouseButtonEventArgs e){panStart=null;PhotoViewport.ReleaseMouseCapture();}
 private void EditingModeChanged(object sender,SelectionChangedEventArgs e)
 {
  if(!ReferenceEquals(e.Source,EditModes)||model is null)return;
  if(EditModes.SelectedItem==PolaroidTab&&model.ActiveTarget?.RawDefinition is null)model.InstantEngineVersion="V3";
  RefreshStep();
 }
 private void RefreshStep()
 {
  if(CurrentParameters is null)return;
  var type=editor?.SelectedAdjustmentNode?.Type;
  CurrentParameters.NodeTypes=type is RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.WhiteBalance?"WhiteBalance":"BasicTone";
 }
 private void Changed(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(ReferenceColorWorkspaceViewModel.PhotoMaskColor))Refresh();}
 private void Refresh()
 {
  PhotoViewport.FocusVisualStyle=null;PhotoViewport.ToolTip="滚轮缩放 · 拖动平移 · 双击适合窗口";
  OrderedSteps.BorderBrush=model?.PhotoMaskColor switch{"Yellow"=>Brushes.Yellow,"Magenta"=>Brushes.Magenta,"White"=>Brushes.White,_=>Brushes.Cyan};
  OrderedSteps.BorderThickness=new Thickness(2);
 }
}
