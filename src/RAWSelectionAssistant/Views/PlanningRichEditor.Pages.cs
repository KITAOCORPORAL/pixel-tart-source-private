using System.Windows;
using System.Windows.Controls;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;
public sealed partial class PlanningRichEditor
{
 private Border? _pageSettingsPanel;
 internal void ApplyHeaderFooter(PlanningHeaderFooter value)
 {value.Validate();_history.Execute(()=>_editor.Document.Tag=value);_persist?.Invoke();}
 internal void EditHeaderFooter()
 {
  if(_pageSettingsPanel is not null){_pageSettingsPanel.Visibility=Visibility.Visible;return;}
  var initial=_editor.Document.Tag as PlanningHeaderFooter??new();var panel=new StackPanel{Margin=new Thickness(14)};
  panel.Children.Add(new TextBlock{Text="页眉、页脚和页码 · 整份文档（单节）",FontWeight=FontWeights.Bold});
  TextBox Field(string label,string value){panel.Children.Add(new TextBlock{Text=label});var box=new TextBox{Text=value,Margin=new Thickness(0,4,0,7),MaxLength=300};panel.Children.Add(box);return box;}
  var header=Field("页眉",initial.Header);var footer=Field("页脚",initial.Footer);var different=new CheckBox{Content="首页不同",IsChecked=initial.DifferentFirstPage};panel.Children.Add(different);
  var firstHeader=Field("首页页眉",initial.FirstHeader);var firstFooter=Field("首页页脚",initial.FirstFooter);var number=new CheckBox{Content="第N页/共M页（页脚）",IsChecked=initial.PageNumbers};panel.Children.Add(number);
  var hide=new CheckBox{Content="首页不显示页码",IsChecked=initial.HideFirstPageNumber};panel.Children.Add(hide);var start=Field("起始页码",initial.StartNumber.ToString());
  var status=new TextBlock{TextWrapping=TextWrapping.Wrap};panel.Children.Add(status);var border=new Border{Child=panel,BorderBrush=System.Windows.Media.Brushes.Gray,BorderThickness=new Thickness(1)};_pageSettingsPanel=border;SetDock(border,Dock.Top);
  var apply=new Button{Content="应用",Margin=new Thickness(4)};apply.Click+=(_,_)=>{try{if(!int.TryParse(start.Text,out var value))throw new ArgumentException("起始页码必须为整数。");ApplyHeaderFooter(new(){Header=header.Text,Footer=footer.Text,FirstHeader=firstHeader.Text,FirstFooter=firstFooter.Text,DifferentFirstPage=different.IsChecked==true,PageNumbers=number.IsChecked==true,HideFirstPageNumber=hide.IsChecked==true,StartNumber=value});Children.Remove(border);_pageSettingsPanel=null;_editor.Focus();}catch(Exception e){status.Text=e.Message;}};panel.Children.Add(apply);
  var cancel=new Button{Content="取消",Margin=new Thickness(4)};cancel.Click+=(_,_)=>{Children.Remove(border);_pageSettingsPanel=null;_editor.Focus();};panel.Children.Add(cancel);Children.Insert(0,border);header.Focus();
 }
 private DockPanel EnhancedPreview()
 {
  FlushPending();var paginator=new PlanningPaginator(Clone());var panel=new DockPanel{Height=640};var controls=new WrapPanel();SetDock(controls,Dock.Top);panel.Children.Add(controls);
  var page=new System.Windows.Controls.Primitives.DocumentPageView{DocumentPaginator=paginator};var scroll=new ScrollViewer{Content=page,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(scroll);
  var label=new TextBlock{Margin=new Thickness(8)};var index=0;void Update(){page.PageNumber=index;label.Text=$"第 {index+1} 页 / 共 {paginator.PageCount} 页";}
  void Button(string text,Action action){var b=new Button{Content=text,Margin=new Thickness(4)};b.Click+=(_,_)=>{action();Update();};controls.Children.Add(b);}
  Button("上一页",()=>index=Math.Max(0,index-1));Button("下一页",()=>index=Math.Min(paginator.PageCount-1,index+1));controls.Children.Add(label);var target=new TextBox{Text="1",Width=50};controls.Children.Add(target);Button("跳转",()=>{if(int.TryParse(target.Text,out var n))index=Math.Clamp(n-1,0,paginator.PageCount-1);});var zoom=new ComboBox{ItemsSource=new[]{50,75,100,125,150,200},SelectedItem=(int)PageZoom,Width=75};controls.Children.Add(zoom);page.LayoutTransform=new System.Windows.Media.ScaleTransform(PageZoom/100,PageZoom/100);zoom.SelectionChanged+=(_,_)=>{if(zoom.SelectedItem is int z){page.LayoutTransform=new System.Windows.Media.ScaleTransform(z/100d,z/100d);SetPageZoom(z);PageZoomChanged?.Invoke(z);}};Update();return panel;
 }
}
