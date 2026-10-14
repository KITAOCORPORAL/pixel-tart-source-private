using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;
public sealed partial class PlanningRichEditor
{
 private Border? _findPanel;
 private TextBox? _query;
 private TextBox? _replacement;
 private CheckBox? _case;
 private CheckBox? _whole;
 private TextBlock? _findStatus;
 private int _matchIndex=-1;
 public bool OwnsBodyFocus=>_editor.IsKeyboardFocusWithin||_findPanel?.IsKeyboardFocusWithin==true;
 internal int ReplaceBody(string query,string replacement,bool matchCase,bool wholeWord,bool all)
 {
  if(replacement.Contains('\r')||replacement.Contains('\n'))throw new ArgumentException("替换文本不能包含换行；请在正文中编辑段落。");
  var matches=PlanningTextSearch.Find(_editor.Document,query,matchCase,wholeWord);if(matches.Count==0)return 0;
  var chosen=all?matches:matches.Skip(Math.Clamp(_matchIndex,0,matches.Count-1)).Take(1).ToArray();
  var changed=chosen.Where(m=>new System.Windows.Documents.TextRange(PlanningTextSearch.Range(m).Start,PlanningTextSearch.Range(m).End).Text!=replacement).ToArray();
  if(changed.Length==0)return 0;
  _history.Execute(()=>{foreach(var match in changed.Reverse())PlanningTextSearch.Replace(match,replacement);});_persist?.Invoke();return changed.Length;
 }
 internal void OpenFind(bool replace)
 {
  if(_findPanel is not null){_findPanel.Visibility=Visibility.Visible;_replacement!.Visibility=replace?Visibility.Visible:Visibility.Collapsed;_query!.Focus();return;}
  var content=new StackPanel{Margin=new Thickness(14)};var tabs=new TabControl{Background=System.Windows.Media.Brushes.Transparent,Foreground=System.Windows.Media.Brushes.Black};tabs.Items.Add(new TabItem{Header="查找"});tabs.Items.Add(new TabItem{Header="替换"});tabs.SelectedIndex=replace?1:0;content.Children.Add(tabs);
  content.Children.Add(new TextBlock{Text="范围：普通正文（含表格、嵌套列表）；不含摄影结构字段、页眉页脚。",TextWrapping=TextWrapping.Wrap});
  _query=new TextBox{MinWidth=180,Margin=new Thickness(0,8,0,4)};_replacement=new TextBox{MinWidth=180,Margin=new Thickness(0,4,0,4),Visibility=replace?Visibility.Visible:Visibility.Collapsed};
  AutomationProperties.SetName(_query,"查找正文");AutomationProperties.SetName(_replacement,"替换为");content.Children.Add(new TextBlock{Text="查找内容"});content.Children.Add(_query);content.Children.Add(new TextBlock{Text="替换为"});content.Children.Add(_replacement);
  _case=new CheckBox{Content="区分大小写"};_whole=new CheckBox{Content="全词匹配"};content.Children.Add(_case);content.Children.Add(_whole);
  content.Children.Add(new TextBlock{Text="摄影结构字段请在分镜表/检查器修改，经现有同步保存；本次不会批量改动镜头。",TextWrapping=TextWrapping.Wrap});
  _findStatus=new TextBlock{Margin=new Thickness(0,5,0,5)};content.Children.Add(_findStatus);var buttons=new WrapPanel();content.Children.Add(buttons);
  void Find(int direction)
  {var matches=PlanningTextSearch.Find(_editor.Document,_query.Text,_case.IsChecked==true,_whole.IsChecked==true);if(matches.Count==0){_matchIndex=-1;_findStatus.Text="找到 0 处";return;}
   _matchIndex=_matchIndex<0?(direction<0?matches.Count-1:0):(_matchIndex+direction+matches.Count)%matches.Count;var range=PlanningTextSearch.Range(matches[_matchIndex]);_editor.Selection.Select(range.Start,range.End);range.Start.Paragraph?.BringIntoView();_findStatus.Text=$"找到 {matches.Count} 处 · 第 {_matchIndex+1} / {matches.Count}";}
  void Button(string title,Action action){var b=new Button{Content=title,Margin=new Thickness(4)};b.Click+=(_,_)=>{try{action();}catch(Exception e){_findStatus.Text=e.Message;}};buttons.Children.Add(b);}
  Button("上一处",()=>Find(-1));Button("下一处",()=>Find(1));Button("替换",()=>{var n=ReplaceBody(_query.Text,_replacement.Text,_case.IsChecked==true,_whole.IsChecked==true,false);_findStatus.Text=$"替换 {n} 处；保存失败时修改保留为草稿。";});
  Button("全部替换",()=>{var n=ReplaceBody(_query.Text,_replacement.Text,_case.IsChecked==true,_whole.IsChecked==true,true);_findStatus.Text=$"替换 {n} 处；一次撤销恢复正文。";});
  Button("关闭",()=>{_findPanel!.Visibility=Visibility.Collapsed;_editor.Focus();});tabs.SelectionChanged+=(_,_)=>_replacement.Visibility=tabs.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;
  _query.TextChanged+=(_,_)=>{_matchIndex=-1;var count=PlanningTextSearch.Find(_editor.Document,_query.Text,_case.IsChecked==true,_whole.IsChecked==true).Count;_findStatus.Text=$"找到 {count} 处";};
  _findPanel=new Border{Child=content,BorderThickness=new Thickness(1),BorderBrush=System.Windows.Media.Brushes.Gray,Background=Background};SetDock(_findPanel,Dock.Top);Children.Insert(0,_findPanel);_query.Focus();
  _findPanel.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){_findPanel.Visibility=Visibility.Collapsed;_editor.Focus();e.Handled=true;}};
 }
}
