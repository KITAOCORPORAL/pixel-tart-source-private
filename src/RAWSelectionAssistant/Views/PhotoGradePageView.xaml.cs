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
  PhotoViewport.PreferDisplayedDimensions=true;
  PhotoViewport.State.Changed+=(_,_)=>DrawGuides();
  AddHandler(Expander.ExpandedEvent,new RoutedEventHandler(SectionExpanded));
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
 private void PhotoDown(object sender,MouseButtonEventArgs e){if(activeTool=="缩放"){PhotoViewport.State.ZoomAbout(e.GetPosition(PhotoViewport),Keyboard.Modifiers==ModifierKeys.Alt?.8:1.25);return;}if(e.ClickCount==2){PhotoViewport.State.Fit();return;}panStart=e.GetPosition(PhotoViewport);PhotoViewport.CaptureMouse();}
 private void PhotoMove(object sender,MouseEventArgs e){if(panStart is{} start&&e.LeftButton==MouseButtonState.Pressed){var next=e.GetPosition(PhotoViewport);PhotoViewport.State.PanBy(next-start);panStart=next;}}
 private void PhotoUp(object sender,MouseButtonEventArgs e){panStart=null;PhotoViewport.ReleaseMouseCapture();}
 private void EditingModeChanged(object sender,SelectionChangedEventArgs e)
 {
  if(EditModes is null||!ReferenceEquals(e.Source,EditModes)||AdjustmentScroll is null)return;
  var i=EditModes.SelectedIndex;if(i<0)return;
  if(HistogramPanel is not null){HistogramPanel.Visibility=i==4?Visibility.Collapsed:Visibility.Visible;HistogramRow.Height=new GridLength(i==4?0:108);DrawGuides();}var panes=new[]{AdjustmentScroll,PaperScroll,LayersScroll,StylesScroll,VariantsScroll};
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
 private void SelectTool(object sender,RoutedEventArgs e){activeTool="选择";ToolLabel.Text=activeTool;PhotoViewport.Cursor=Cursors.Arrow;}
 private void HandTool(object sender,RoutedEventArgs e){activeTool="抓手";ToolLabel.Text=activeTool;PhotoViewport.Cursor=Cursors.Hand;}
 private void ZoomTool(object sender,RoutedEventArgs e){activeTool="缩放";ToolLabel.Text=activeTool;PhotoViewport.Cursor=Cursors.Cross;}
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
 private bool switchingVariant;
 private async void VariantPicked(object sender,SelectionChangedEventArgs e)
 {
  var workspace=model;
  if(switchingVariant||!ReferenceEquals(e.Source,sender)||workspace is null||e.AddedItems.Count==0||
     workspace.IsLoading||workspace.IsExporting||workspace.IsInstantGenerating||
     HistoryVersions.SelectedItem is not StudioEditVersion version||version.Archived||
     workspace.ActiveTarget is not {} target||target.ActiveEditVersionId==version.Id||!target.EditVersions.Contains(version))return;
  switchingVariant=true;
  try{await workspace.SwitchEditVersionAsync(version.Id);}
  finally{switchingVariant=false;}
 }
 private void ApplyScheme(object sender,RoutedEventArgs e){if(model is null)return;model.Editor.RequestApplySchemeCommand.Execute(null);if(model.Editor.SchemeSwitchOpen){var choice=MessageBox.Show(Window.GetWindow(this),"当前调整未保存。保存当前方案后切换？选择否将直接切换；取消保持当前照片。","切换色彩方案",MessageBoxButton.YesNoCancel);if(choice==MessageBoxResult.Yes)model.Editor.SaveAndApplySchemeCommand.Execute(null);else if(choice==MessageBoxResult.No)model.Editor.DiscardAndApplySchemeCommand.Execute(null);else model.Editor.CancelSchemeSwitchCommand.Execute(null);}}
 private string activeTool="选择";private bool solo;
 private void GuidesChanged(object sender,SelectionChangedEventArgs e)=>DrawGuides();
 private void GuideSizeChanged(object sender,SizeChangedEventArgs e)=>DrawGuides();
 private void DrawGuides()
 {
  if(GuideOverlay is null||PhotoViewport is null)return;GuideOverlay.Children.Clear();if(GuideChoice?.SelectedIndex is not >0)return;
  var rect=PhotoViewport.State.ImageRect(new Rect(PhotoViewport.RenderSize));if(rect.IsEmpty||rect.Width<=0||rect.Height<=0)return;var origin=PhotoViewport.TranslatePoint(new Point(),GuideOverlay);rect.Offset(origin.X,origin.Y);
  var fractions=GuideChoice.SelectedIndex==1?new[]{1d/3,2d/3}:new[]{.5};foreach(var f in fractions){Line(rect.Left+rect.Width*f,rect.Top,rect.Left+rect.Width*f,rect.Bottom);Line(rect.Left,rect.Top+rect.Height*f,rect.Right,rect.Top+rect.Height*f);}
  void Line(double x1,double y1,double x2,double y2){if(!double.IsFinite(x1+y1+x2+y2))return;GuideOverlay.Children.Add(new System.Windows.Shapes.Line{X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=new SolidColorBrush(Color.FromArgb(120,255,255,255)),StrokeThickness=1});}
 }
 private void SectionExpanded(object sender,RoutedEventArgs e){if(!solo||e.OriginalSource is not Expander chosen)return;if(VisualTreeHelper.GetParent(chosen) is not StackPanel group)return;foreach(var section in group.Children.OfType<Expander>())if(section!=chosen)section.IsExpanded=false;}
 private void PanelMenu(object sender,RoutedEventArgs e)
 {
  var menu=new ContextMenu();var single=new MenuItem{Header="Solo 模式 · 每次只展开一个区块",IsCheckable=true,IsChecked=solo};single.Click+=(_,_)=>solo=single.IsChecked;menu.Items.Add(single);
  void Add(string label,Action action){var item=new MenuItem{Header=label};item.Click+=(_,_)=>action();menu.Items.Add(item);}
  var pane=new[]{AdjustmentScroll,PaperScroll,LayersScroll,StylesScroll,VariantsScroll}[Math.Clamp(EditModes.SelectedIndex,0,4)];
  Add("全部展开",()=>{foreach(var section in (pane.Content as StackPanel)?.Children.OfType<Expander>()??[])section.IsExpanded=true;});
  Add("全部折叠",()=>{foreach(var section in (pane.Content as StackPanel)?.Children.OfType<Expander>()??[])section.IsExpanded=false;});
  menu.Items.Add(new Separator());Add("面板宽度 · 304 px",()=>PanelColumn.Width=new GridLength(304));Add("隐藏工具面板",()=>TogglePanel(sender,e));menu.PlacementTarget=(UIElement)sender;menu.IsOpen=true;
 }
 private void RatePhoto(object sender,RoutedEventArgs e){if(sender is Button{DataContext:ReferenceTargetItem target,Tag:string number}&&int.TryParse(number,out var rating))target.Rating=rating;}

 private void Refresh()
 {
  PhotoViewport.FocusVisualStyle=null;PhotoViewport.ToolTip="滚轮缩放 · 拖动平移 · 双击适合窗口";
  OrderedSteps.BorderBrush=model?.PhotoMaskColor switch{"Yellow"=>Brushes.Yellow,"Magenta"=>Brushes.Magenta,"White"=>Brushes.White,_=>Brushes.Cyan};
  OrderedSteps.BorderThickness=new Thickness(0);
 }
}
