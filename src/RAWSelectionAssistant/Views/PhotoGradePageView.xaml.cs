using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView:UserControl
{
 private ReferenceColorWorkspaceViewModel? model;
 public PhotoGradePageView()
 {
  InitializeComponent();
  PreviewKeyDown+=(_,e)=>
  {
   if(Keyboard.Modifiers!=ModifierKeys.Control)return;
   if(e.Key==Key.E){OpenExport(this,new RoutedEventArgs());e.Handled=true;}
   else if(e.Key==Key.D0){PhotoViewport.State.Fit();e.Handled=true;}
   else if(e.Key==Key.D1){PhotoViewport.State.SetZoom(1);e.Handled=true;}
  };
  DataContextChanged+=(_,_)=>Attach();
  Loaded+=(_,_)=>Attach();
  Unloaded+=(_,_)=>Detach(cancel:true);
  IsVisibleChanged+=(_,_)=>{if(IsVisible)Attach();else Detach(cancel:true);};
 }
 private void Attach()
 {
  Detach(cancel:false);model=DataContext as ReferenceColorWorkspaceViewModel;model?.EnableLightPrintWorkspace();
  // Weak subscription also covers controls constructed but never attached/unloaded.
  if(model is not null)PropertyChangedEventManager.AddHandler(model,Changed,string.Empty);

  Refresh();UpdateZones();
 }
 private void Detach(bool cancel)
 {
  if(model is not null){PropertyChangedEventManager.RemoveHandler(model,Changed,string.Empty);if(cancel)model.ExitInstantPresentationForLifecycle();}

  model=null;
 }
 private void UpdateZones()
 {
  if(ToneRatios is null)return;var z=model?.PreviewHistogram?.Zones;
  ToneRatios.Text=z is null?"暗部 —   阴影 —   中间调 —   亮部 —   高光 —":$"暗部{z.Ratios.Take(2).Sum():P0}  阴影{z.Ratios.Skip(2).Take(2).Sum():P0}  中间调{z.Ratios.Skip(4).Take(3).Sum():P0}  亮部{z.Ratios.Skip(7).Take(2).Sum():P0}  高光{z.Ratios.Skip(9).Sum():P0}";
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
  if(EditModes is null||!ReferenceEquals(e.Source,EditModes)||AdjustmentScroll is null)return;
  var i=EditModes.SelectedIndex;if(i<0)return;var panes=new[]{AdjustmentScroll,PaperScroll,LayersScroll,StylesScroll,VariantsScroll};
  for(int n=0;n<panes.Length;n++)panes[n].Visibility=i==n?Visibility.Visible:Visibility.Collapsed;
  if(FilmAction is not null)FilmAction.Visibility=i==1?Visibility.Visible:Visibility.Collapsed;if(PanelTitle is not null)PanelTitle.Text=new[]{"调整","光片","图层","风格","变体"}[Math.Clamp(i,0,4)];
  if(i==1&&model is{} workspace&&workspace.ActiveTarget?.RawDefinition is null)workspace.InstantEngineVersion="V3";
 }
 private void FileMenu(object sender,RoutedEventArgs e)=>Menu(sender,("导入照片",model?.ChooseTargetCommand),("打开项目",model?.OpenSessionCommand),("保存项目",model?.SaveSessionCommand));
 private void EditMenu(object sender,RoutedEventArgs e)=>Menu(sender,("撤销",model?.PhotoGradeUndoCommand),("重做",model?.PhotoGradeRedoCommand));
 private void AdjustMenu(object sender,RoutedEventArgs e)=>ShowAdjust(sender,e);
 private void ViewMenu(object sender,RoutedEventArgs e){var m=new ContextMenu();void Add(string text,Action action){var i=new MenuItem{Header=text};i.Click+=(_,_)=>action();m.Items.Add(i);}Add("适合窗口",()=>PhotoViewport.State.Fit());Add("100%",()=>PhotoViewport.State.SetZoom(1));Add("切换面板",()=>TogglePanel(sender,e));m.PlacementTarget=(UIElement)sender;m.IsOpen=true;}
 private void HelpMenu(object sender,RoutedEventArgs e){DiagnosticsSection.IsExpanded=true;EditModes.SelectedIndex=0;DiagnosticsSection.BringIntoView();}
 private void Menu(object sender,params (string,System.Windows.Input.ICommand?)[] items){var m=new ContextMenu();foreach(var (text,command)in items)m.Items.Add(new MenuItem{Header=text,Command=command});m.PlacementTarget=(UIElement)sender;m.IsOpen=true;}
 private void CompareMenu(object sender,RoutedEventArgs e){if(model is null)return;var m=new ContextMenu();foreach(var c in model.PhotographerComparisonModes){var i=new MenuItem{Header=c.Label};i.Click+=(_,_)=>model.PhotographerComparisonValue=c.Value;m.Items.Add(i);}m.PlacementTarget=(UIElement)sender;m.IsOpen=true;}
 private void ShowVariants(object sender,RoutedEventArgs e)=>EditModes.SelectedIndex=4;
 private void ShowAdjust(object sender,RoutedEventArgs e)=>EditModes.SelectedIndex=0;
 private void TogglePanel(object sender,RoutedEventArgs e){bool on=RightPanel.Visibility==Visibility.Visible;RightPanel.Visibility=on?Visibility.Collapsed:Visibility.Visible;PanelColumn.MinWidth=on?0:280;PanelColumn.Width=new GridLength(on?0:304);SplitterColumn.Width=new GridLength(on?0:4);}
 private void SelectTool(object sender,RoutedEventArgs e)=>PhotoViewport.Cursor=Cursors.Arrow;
 private void HandTool(object sender,RoutedEventArgs e)=>PhotoViewport.Cursor=Cursors.Hand;
 private void ZoomTool(object sender,RoutedEventArgs e)=>PhotoViewport.Cursor=Cursors.SizeAll;
 private void CameraI2(object sender,RoutedEventArgs e){if(model is not null)model.LightPrintCamera="i-2";}
 private void CameraSX70(object sender,RoutedEventArgs e){if(model is not null)model.LightPrintCamera="SX-70";}
 private void OpenExport(object sender,RoutedEventArgs e){if(model is null)return;var window=new DarkroomExportDialog(model){Owner=Window.GetWindow(this)};window.ShowDialog();}
 private void Changed(object? sender,PropertyChangedEventArgs e)
 {
  if(e.PropertyName==nameof(ReferenceColorWorkspaceViewModel.PhotoMaskColor))Refresh();
  if(e.PropertyName==nameof(ReferenceColorWorkspaceViewModel.PreviewHistogram))UpdateZones();
  // A newly imported ordinary photo restores the historical default engine. The
  // photographer's selected Polaroid tab must still be ready without version knowledge.
  if(e.PropertyName==nameof(ReferenceColorWorkspaceViewModel.InstantEngineVersion)&&model?.ActiveTarget is{} target&&target.RawDefinition is null&&EditModes.SelectedItem==PolaroidTab&&model.InstantEngineVersion!="V3")model.InstantEngineVersion="V3";
 }
 private void Refresh()
 {
  PhotoViewport.FocusVisualStyle=null;PhotoViewport.ToolTip="滚轮缩放 · 拖动平移 · 双击适合窗口";
  OrderedSteps.BorderBrush=model?.PhotoMaskColor switch{"Yellow"=>Brushes.Yellow,"Magenta"=>Brushes.Magenta,"White"=>Brushes.White,_=>Brushes.Cyan};
  OrderedSteps.BorderThickness=new Thickness(0);
 }
}
