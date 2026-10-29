using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
 private Window? r53MultiWindow;
 private void R53MultiCompare(object sender,RoutedEventArgs e)=>OpenR53SharedComparison(null);
 private async void OpenR53SharedComparison(StudioStillSnapshot? still)
 {
  if(model is null)return;var workspace=model;var select=new ListBox{ItemsSource=workspace.R53Variants,DisplayMemberPath="Name",SelectionMode=SelectionMode.Multiple,Height=100};foreach(var v in (still is null?workspace.R53Variants.Take(2):workspace.R53Variants.Where(v=>v.Id==workspace.ActiveTarget?.ActiveEditVersionId)))select.SelectedItems.Add(v);
  var root=new DockPanel{Margin=new Thickness(10)};var prepare=new Button{Content=still is null?"比较所选2–4变体":"剧照与所选1–3变体"};var close=new Button{Content="退出比较 · 返回主图"};var status=new TextBlock();var bar=new StackPanel{Orientation=Orientation.Horizontal};bar.Children.Add(prepare);bar.Children.Add(close);bar.Children.Add(status);DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);DockPanel.SetDock(select,Dock.Top);root.Children.Add(select);var comparison=new StudioSharedComparisonView();root.Children.Add(comparison);bool closed=false;var sourceTarget=workspace.ActiveTarget;
  PropertyChangedEventHandler changed=(_,args)=>{if(args.PropertyName==nameof(workspace.ActiveTarget)&&!ReferenceEquals(sourceTarget,workspace.ActiveTarget)){r53MultiWindow?.Close();return;}if(args.PropertyName is nameof(workspace.R53CompareFrames) or nameof(workspace.R53MultiCompareStatus)){status.Text=workspace.R53MultiCompareStatus;comparison.SetFrames(workspace.R53CompareFrames);}};workspace.PropertyChanged+=changed;
  async void Prepare(){try{await workspace.PrepareR53SharedComparisonAsync(select.SelectedItems.Cast<StudioEditVersion>().Select(v=>v.Id).ToArray(),still);if(!closed&&still is not null)comparison.SetWipe(true);}catch(Exception error)when(error is ArgumentException or InvalidOperationException or System.IO.IOException or NotSupportedException){status.Text="比较未完成："+error.Message;}}
  r53MultiWindow?.Close();var window=new Window{Title="光镜 · 剧照与变体共享比较",Owner=Window.GetWindow(this),Width=1100,Height=780,Content=root,Background=Background};r53MultiWindow=window;prepare.Click+=(_,_)=>Prepare();close.Click+=(_,_)=>window.Close();window.Closed+=(_,_)=>{closed=true;workspace.PropertyChanged-=changed;workspace.CloseR53VariantComparison();if(r53MultiWindow==window)r53MultiWindow=null;};window.Show();await System.Threading.Tasks.Task.Yield();if(!closed)Prepare();
 }
}
