using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
 private Window? r53MultiWindow;
 private void R53MultiCompare(object sender,RoutedEventArgs e)=>OpenR53SharedComparison(null);
 private StudioViewingTableView? viewingTable;private Grid? viewingHost;private Border? viewingTop;private Panel? stripParent;private int stripIndex;
 internal bool ViewingMode=>viewingTable is not null;
 internal StudioViewingTableView? ViewingControl=>viewingTable;
 private async void OpenViewingTable(object sender,RoutedEventArgs e)=>await ToggleViewingTableAsync();
 internal async Task ToggleViewingTableAsync()
 {
  if(viewingTable is not null){ExitViewingTable();return;}if(model is null)return;
  var table=new StudioViewingTableView(model);viewingTable=table;table.ReturnRequested+=ExitViewingTable;
  viewingHost=new Grid{Background=Background};viewingHost.RowDefinitions.Add(new RowDefinition());viewingHost.RowDefinitions.Add(new RowDefinition{Height=new GridLength(114)});viewingHost.Children.Add(table);Grid.SetColumn(viewingHost,1);Grid.SetColumnSpan(viewingHost,4);Panel.SetZIndex(viewingHost,50);
  stripParent=(Panel)FilmstripArea.Parent;stripIndex=stripParent.Children.IndexOf(FilmstripArea);stripParent.Children.Remove(FilmstripArea);Grid.SetRow(FilmstripArea,1);Grid.SetColumn(FilmstripArea,0);Grid.SetColumnSpan(FilmstripArea,1);viewingHost.Children.Add(FilmstripArea);R53MainLayout.Children.Add(viewingHost);
  var back=new Button{Content="← 返回暗室 · "+(model.SessionPath is null?"未命名项目":System.IO.Path.GetFileNameWithoutExtension(model.SessionPath)),ToolTip="返回暗室 Ctrl+L"};System.Windows.Automation.AutomationProperties.SetName(back,"返回暗室");back.Click+=(_,_)=>ExitViewingTable();viewingTop=new Border{Background=Background,Child=back};Grid.SetRow(viewingTop,1);Panel.SetZIndex(viewingTop,50);R3Root.Children.Add(viewingTop);ApplyR53Layout();table.Focus();await table.RefreshAsync();
 }
 private void ExitViewingTable()
 {
  var table=viewingTable;if(table is null)return;viewingTable=null;table.Close();if(viewingHost is not null){viewingHost.Children.Remove(FilmstripArea);R53MainLayout.Children.Remove(viewingHost);viewingHost=null;}if(stripParent is not null){Grid.SetRow(FilmstripArea,3);Grid.SetColumn(FilmstripArea,0);Grid.SetColumnSpan(FilmstripArea,2);stripParent.Children.Insert(Math.Min(stripIndex,stripParent.Children.Count),FilmstripArea);stripParent=null;}if(viewingTop is not null){R3Root.Children.Remove(viewingTop);viewingTop=null;}ApplyR53Layout();
 }
 private void ApplyViewingHostLayout(){if(viewingTable is null)return;ResourceColumn.Width=new GridLength(0);PanelColumn.MinWidth=0;PanelColumn.Width=new GridLength(0);SplitterColumn.Width=new GridLength(0);R53Resources.Visibility=Visibility.Collapsed;RightPanel.Visibility=Visibility.Collapsed;R53ParameterRail.Visibility=Visibility.Collapsed;PanelSplitter.Visibility=Visibility.Collapsed;R53SerialNodes.Visibility=Visibility.Collapsed;R53NodeRow.Height=new GridLength(0);R54ScopesArea.Visibility=Visibility.Collapsed;R53FilmRow.Height=new GridLength(0);PhotoStrip.Visibility=Visibility.Visible;R53FilmContentRow.Height=new GridLength(92);}
 private async void OpenR53SharedComparison(StudioStillSnapshot? still)
 {
  if(model is null)return;var workspace=model;var select=new ListBox{ItemsSource=workspace.R53Variants,DisplayMemberPath="Name",SelectionMode=SelectionMode.Multiple,Height=100};foreach(var v in (still is null?workspace.R53Variants.Take(2):workspace.R53Variants.Where(v=>v.Id==workspace.ActiveTarget?.ActiveEditVersionId)))select.SelectedItems.Add(v);
  var root=new DockPanel{Margin=new Thickness(10)};var prepare=new Button{Content=still is null?"比较所选2–4变体":"剧照与所选1–3变体"};var close=new Button{Content="退出比较 · 返回主图"};var status=new TextBlock();var bar=new StackPanel{Orientation=Orientation.Horizontal};bar.Children.Add(prepare);bar.Children.Add(close);bar.Children.Add(status);DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);DockPanel.SetDock(select,Dock.Top);root.Children.Add(select);var comparison=new StudioSharedComparisonView();root.Children.Add(comparison);bool closed=false;var sourceTarget=workspace.ActiveTarget;
  PropertyChangedEventHandler changed=(_,args)=>{if(args.PropertyName==nameof(workspace.ActiveTarget)&&!ReferenceEquals(sourceTarget,workspace.ActiveTarget)){r53MultiWindow?.Close();return;}if(args.PropertyName is nameof(workspace.R53CompareFrames) or nameof(workspace.R53MultiCompareStatus)){status.Text=workspace.R53MultiCompareStatus;comparison.SetFrames(workspace.R53CompareFrames);}};workspace.PropertyChanged+=changed;
  async void Prepare(){try{await workspace.PrepareR53SharedComparisonAsync(select.SelectedItems.Cast<StudioEditVersion>().Select(v=>v.Id).ToArray(),still);if(!closed&&still is not null)comparison.SetWipe(true);}catch(Exception error)when(error is ArgumentException or InvalidOperationException or System.IO.IOException or NotSupportedException){status.Text="比较未完成："+error.Message;}}
  r53MultiWindow?.Close();var window=new Window{Title="光镜 · 剧照与变体共享比较",Owner=Window.GetWindow(this),Width=1100,Height=780,Content=root,Background=Background};r53MultiWindow=window;prepare.Click+=(_,_)=>Prepare();close.Click+=(_,_)=>window.Close();window.Closed+=(_,_)=>{closed=true;workspace.PropertyChanged-=changed;workspace.CloseR53VariantComparison();if(r53MultiWindow==window)r53MultiWindow=null;};window.Show();await System.Threading.Tasks.Task.Yield();if(!closed)Prepare();
 }
}
