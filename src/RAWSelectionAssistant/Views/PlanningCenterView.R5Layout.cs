using RAWSelectionAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PlanningCenterView
{
    private FrameworkElement? _dockedPane;
    private Panel? _dockOrigin;
    private IInputElement? _dockReturnFocus;
    private bool _layoutUpdating;
    private bool _layoutDragging;
    private Action? _inspectorCapture;
    private Guid? _inspectorShotId;
    private Guid? _inspectorProjectId;
    internal bool InspectorIsDrawer => PlanningInspectorColumn.Width.Value==0;
    private static readonly string[][] SectionGroups = [["方案","项目概览","文字","镜头清单"],["视觉","参考图","情绪板","灯位图"],["执行","服化道与道具","设备","人员","场地","档期","当天时间表","预算"],["资料","文件","文档内容"]];
    private string SectionCount(string page) => _vm is null?"":page switch
    {
        "镜头清单"=>_vm.Shots.Count.ToString(), "参考图"=>_vm.AllProjectReferences.Count.ToString(),"情绪板"=>_vm.AllProjectReferences.Count(r=>r.IsMoodboard).ToString(),
        "文件"=>_vm.Attachments.Count.ToString(),_=>ResourcePages.TryGetValue(page,out var type)?_vm.ResourceCatalog.Items.Count(r=>!r.Archived&&r.GetType()==type).ToString():""
    };
    private void RenderR5Navigation()
    {
        if(_vm is null)return;ContentNavigation.Children.Clear();
        foreach(var group in SectionGroups)
        {
            var label=Text(group[0],11);label.Foreground=Brush("TextSecondaryBrush");label.Margin=new Thickness(8,12,0,4);ContentNavigation.Children.Add(label);
            foreach(var page in group.Skip(1))
            {
                var button=AsyncAction(page,async()=>{if(await FlushDocumentAsync()&&await _vm.SelectContentSafelyAsync(page)){ClosePlanningDock();DocumentScroll.ScrollToTop();}});
                button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(10,4,10,4);button.MinHeight=30;
                var row=new DockPanel();var count=Text(SectionCount(page),11);count.Foreground=Brush("TextSecondaryBrush");DockPanel.SetDock(count,Dock.Right);row.Children.Add(count);
                row.Children.Add(Text(page=="镜头清单"?"镜头清单与分镜":page,13));button.Content=row;
                if(_vm.ContentPage==page){button.Background=Brush("SurfaceSecondaryBrush");button.BorderBrush=Brush("AccentBrush");button.BorderThickness=new Thickness(2,0,0,0);}
                System.Windows.Automation.AutomationProperties.SetAutomationId(button,"PlanningPage"+PlanningCenterViewModel.ContentPages.ToList().IndexOf(page));ContentNavigation.Children.Add(button);
            }
        }
    }
    private void UpdateR5Layout()
    {
        if(_layoutUpdating||_layoutDragging||ActualWidth<=0||PlanningInspectorPane is null)return;
        _layoutUpdating=true;
        try
        {
            var saved=_vm?.UiLayout??new();var preview=_vm?.IsPreviewMode==true;
            var list=!preview&&ActualWidth>=1500;var outline=!preview&&ActualWidth>=880;var inspector=!preview&&ActualWidth>1100&&saved.InspectorOpen;
            DocumentListColumn.Width=new(list?saved.ListWidth:0);SectionOutlineColumn.Width=new(outline?184:0);PlanningInspectorColumn.Width=new(inspector?saved.InspectorWidth:0);
            if(!ReferenceEquals(_dockedPane,DocumentListPane))DocumentListPane.Visibility=list?Visibility.Visible:Visibility.Collapsed;
            if(!ReferenceEquals(_dockedPane,ContentNavigationBorder))ContentNavigationBorder.Visibility=outline?Visibility.Visible:Visibility.Collapsed;
            if(!ReferenceEquals(_dockedPane,PlanningInspectorPane))PlanningInspectorPane.Visibility=inspector?Visibility.Visible:Visibility.Collapsed;
            PlanningListSplitter.Visibility=list?Visibility.Visible:Visibility.Collapsed;PlanningInspectorSplitter.Visibility=inspector?Visibility.Visible:Visibility.Collapsed;
            PlanningSectionToggle.Visibility=outline?Visibility.Collapsed:Visibility.Visible;
            PlanningModalCard.Width=Math.Min(500,Math.Max(260,ActualWidth-32));PlanningModalCard.MaxHeight=Math.Max(180,ActualHeight-32);
            PlanningDockPanel.Width=Math.Min(340,Math.Max(220,ActualWidth-24));
            var compact=ActualWidth<1100;EditorHeader.Margin=new Thickness(compact?12:20,compact?6:12,compact?12:20,compact?6:10);
            DocumentContent.Margin=new Thickness(compact?14:24,compact?8:16,compact?14:24,40);
            var actions=(WrapPanel)EditorHeader.Children[1];EditorHeader.RowDefinitions.Clear();EditorHeader.RowDefinitions.Add(new(){Height=GridLength.Auto});
            if(ActualWidth<1060)EditorHeader.RowDefinitions.Add(new(){Height=GridLength.Auto});
            Grid.SetRow(actions,ActualWidth<1060?1:0);Grid.SetColumn(actions,ActualWidth<1060?0:1);Grid.SetColumnSpan(actions,ActualWidth<1060?2:1);actions.Margin=new Thickness(0,ActualWidth<1060?8:0,0,0);
            PlanningSimpleModeButton.FontWeight=_vm?.IsAdvancedPlanning==true?FontWeights.Normal:FontWeights.Bold;
            PlanningAdvancedModeButton.FontWeight=_vm?.IsAdvancedPlanning==true?FontWeights.Bold:FontWeights.Normal;
        }finally{_layoutUpdating=false;}
    }
    internal void OpenPlanningDock(FrameworkElement pane,bool right)
    {
        if(ReferenceEquals(_dockedPane,pane))return;
        ClosePlanningDock();_dockReturnFocus=Keyboard.FocusedElement;
        _dockOrigin=pane.Parent as Panel;if(_dockOrigin is null)return;
        _dockOrigin.Children.Remove(pane);_dockedPane=pane;pane.Visibility=Visibility.Visible;
        Grid.SetRow(pane,0);Grid.SetColumn(pane,0);
        PlanningDockContent.Content=pane;PlanningDockPanel.HorizontalAlignment=right?HorizontalAlignment.Right:HorizontalAlignment.Left;PlanningDockOverlay.Visibility=Visibility.Visible;
        WorkspaceLayout.IsEnabled=false;PlanningDockClose.Focus();
    }
    internal void ClosePlanningDock()
    {
        if(_dockedPane is null)return;
        PlanningDockContent.Content=null;
        if(ReferenceEquals(_dockedPane,DocumentListPane)){Grid.SetRow(_dockedPane,0);Grid.SetColumn(_dockedPane,0);}
        else {Grid.SetRow(_dockedPane,1);Grid.SetColumn(_dockedPane,ReferenceEquals(_dockedPane,PlanningInspectorPane)?2:0);}
        _dockOrigin!.Children.Add(_dockedPane);_dockedPane=null;_dockOrigin=null;PlanningDockOverlay.Visibility=Visibility.Collapsed;WorkspaceLayout.IsEnabled=true;
        UpdateR5Layout();if(_dockReturnFocus is UIElement {IsVisible:true} control)control.Focus();else PlanningSectionToggle.Focus();_dockReturnFocus=null;
    }
    private async void PlanningRetry_Click(object sender,RoutedEventArgs e){await FlushDocumentAsync();}
    private void ClosePlanningDock_Click(object sender,RoutedEventArgs e)=>ClosePlanningDock();
    private void SectionDrawer_Click(object sender,RoutedEventArgs e)=>OpenPlanningDock(ContentNavigationBorder,false);
    private async void InspectorDrawer_Click(object sender,RoutedEventArgs e)
    {
        if(_vm is null)return;
        if(ActualWidth<=1100)OpenPlanningDock(PlanningInspectorPane,true);
        else {await _vm.SaveUiLayoutAsync(_vm.UiLayout with{InspectorOpen=!_vm.UiLayout.InspectorOpen});UpdateR5Layout();}
    }
    private void LayoutSplitter_DragStarted(object sender,DragStartedEventArgs e)=>_layoutDragging=true;
    private async void LayoutSplitter_DragCompleted(object sender,DragCompletedEventArgs e)
    {
        _layoutDragging=false;if(_vm is null)return;
        await _vm.SaveUiLayoutAsync(_vm.UiLayout with { ListWidth=DocumentListColumn.Width.Value>0?DocumentListColumn.Width.Value:_vm.UiLayout.ListWidth,InspectorWidth=PlanningInspectorColumn.Width.Value>0?PlanningInspectorColumn.Width.Value:_vm.UiLayout.InspectorWidth });UpdateR5Layout();
    }
    private async void LayoutSplitter_KeyDown(object sender,KeyEventArgs e)
    {
        if(_vm is null||e.Key is not(Key.Left or Key.Right))return;var delta=e.Key==Key.Left?-16:16;
        await _vm.SaveUiLayoutAsync(ReferenceEquals(sender,PlanningListSplitter)?_vm.UiLayout with{ListWidth=_vm.UiLayout.ListWidth+delta}:_vm.UiLayout with{InspectorWidth=_vm.UiLayout.InspectorWidth+delta});UpdateR5Layout();e.Handled=true;
    }
    private async void LayoutSplitter_Reset(object sender,MouseButtonEventArgs e)
    {if(_vm is null)return;await _vm.SaveUiLayoutAsync(_vm.UiLayout with{ListWidth=248,InspectorWidth=300});UpdateR5Layout();e.Handled=true;}
    private void AddInspectorGroup(string title,UIElement body,bool expanded=true)
    {
        var header=new DockPanel();var moveUp=new Button{Content="↑",ToolTip="上移分组",Padding=new Thickness(3),Margin=new Thickness(2)};var moveDown=new Button{Content="↓",ToolTip="下移分组",Padding=new Thickness(3),Margin=new Thickness(2)};DockPanel.SetDock(moveUp,Dock.Right);DockPanel.SetDock(moveDown,Dock.Right);header.Children.Add(moveDown);header.Children.Add(moveUp);header.Children.Add(Text(title,13,true));
        var expander=new Expander{Header=header,Tag=title,Content=body,IsExpanded=!(_vm?.UiLayout.CollapsedGroups??[]).Contains(title)&&expanded,Margin=new Thickness(0,0,0,14)};
        Point? dragStart=null;header.PreviewMouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is TextBlock)dragStart=e.GetPosition(header);};
        header.PreviewMouseMove+=(_,e)=>{if(dragStart is not {} start||e.LeftButton!=MouseButtonState.Pressed||(e.GetPosition(header)-start).Length<4)return;dragStart=null;DragDrop.DoDragDrop(header,new DataObject("Kitao.PlanningInspectorGroup",expander),DragDropEffects.Move);};
        header.PreviewMouseLeftButtonUp+=(_,_)=>dragStart=null;expander.AllowDrop=true;
        expander.DragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent("Kitao.PlanningInspectorGroup")?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};
        expander.Drop+=async(_,e)=>{if(e.Data.GetData("Kitao.PlanningInspectorGroup") is Expander source&&PlanningInspectorContent.Children.Contains(source)){var groups=PlanningInspectorContent.Children.OfType<Expander>().ToList();await MoveInspectorGroupAsync(source,groups.IndexOf(expander)-groups.IndexOf(source));}e.Handled=true;};
        async void Save(bool open){if(_vm is null||_rendering)return;var set=(_vm.UiLayout.CollapsedGroups??[]).ToHashSet();if(open)set.Remove(title);else set.Add(title);await _vm.SaveUiLayoutAsync(_vm.UiLayout with{CollapsedGroups=set.ToArray()});}
        expander.Expanded+=(_,_)=>Save(true);expander.Collapsed+=(_,_)=>Save(false);
        moveUp.Click+=async(_,e)=>{e.Handled=true;await MoveInspectorGroupAsync(expander,-1);};moveDown.Click+=async(_,e)=>{e.Handled=true;await MoveInspectorGroupAsync(expander,1);};
        expander.PreviewKeyDown+=async(_,e)=>{if(Keyboard.Modifiers==ModifierKeys.Alt&&e.Key is Key.Up or Key.Down){e.Handled=true;await MoveInspectorGroupAsync(expander,e.Key==Key.Up?-1:1);}};
        var order=_vm?.UiLayout.InspectorGroupOrder??[];var rank=Array.IndexOf(order,title);var index=PlanningInspectorContent.Children.Count;
        if(rank>=0){for(var i=0;i<PlanningInspectorContent.Children.Count;i++)if(PlanningInspectorContent.Children[i] is Expander other){var otherRank=Array.IndexOf(order,other.Tag?.ToString()??"");if(otherRank<0||otherRank>rank){index=i;break;}}}
        PlanningInspectorContent.Children.Insert(index,expander);
    }
    internal async Task<bool> MoveInspectorGroupAsync(Expander group,int delta)
    {
        if(_vm is null)return false;var groups=PlanningInspectorContent.Children.OfType<Expander>().ToList();var index=groups.IndexOf(group);var next=Math.Clamp(index+delta,0,groups.Count-1);if(index<0||index==next)return false;
        groups.RemoveAt(index);groups.Insert(next,group);var titles=groups.Select(g=>g.Tag?.ToString()??"").ToArray();
        if(!await _vm.SaveUiLayoutAsync(_vm.UiLayout with{InspectorGroupOrder=titles}))return false;
        foreach(var item in groups)PlanningInspectorContent.Children.Remove(item);for(var i=0;i<groups.Count;i++)PlanningInspectorContent.Children.Insert(i,groups[i]);group.Focus();return true;
    }
    private void RenderR5Inspector()
    {
        if(_vm is null)return;
        _inspectorCapture=null;_inspectorShotId=null;_inspectorProjectId=_vm.ProjectId;PlanningInspectorContent.Children.Clear();
        var overview=new StackPanel();overview.Children.Add(Text(_vm.ProgressText,15,true));overview.Children.Add(Text(_vm.EstimatedTimeText,12));overview.Children.Add(Text($"摄影资源 {_vm.ResourceCatalog.Items.Count(i=>!i.Archived)} · 参考 {_vm.AllProjectReferences.Count}",12));AddInspectorGroup("完成度",overview);
        var info=new StackPanel();info.Children.Add(Text(_vm.DocumentDate,12));
        foreach(var (label,property) in new[]{("标题",nameof(_vm.DocumentTitle)),("地点",nameof(_vm.DocumentLocation)),("人员",nameof(_vm.DocumentPeople))})
        {info.Children.Add(Text(label,11));var input=Editor(property);input.MinHeight=30;input.Margin=new Thickness(0,2,0,7);info.Children.Add(input);}AddInspectorGroup("拍摄信息",info);
        if(_vm.ContentPage=="镜头清单"&&_vm.SelectedShot is {} shot)RenderR5ShotInspector(shot);
        var links=new StackPanel();links.Children.Add(AsyncAction("查看关联档期",async()=>await _vm.ShowBookingCommand.ExecuteAsync(null)));links.Children.Add(AsyncAction("开始拍摄",async()=>{if(await FlushDocumentAsync())await _vm.EnterTetherCommand.ExecuteAsync(null);}));AddInspectorGroup("关联",links);
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap,FontSize=12};status.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("SaveStatus"));PlanningInspectorContent.Children.Add(status);
        PlanningInspectorContent.Children.Add(AsyncAction("保存 / 重试",async()=>{await FlushDocumentAsync();}));
    }
    private void RenderR5ShotInspector(ProjectShot shot)
    {
        if(_vm is null)return;var owner=_vm;var project=owner.ProjectId;_inspectorShotId=shot.ShotId;
        var row=new RAWSelectionAssistant.ViewModels.PlanningShotEditRow(owner.ShotDraft(shot.ShotId),owner.StageShot);row.BeginEdit();var fields=new Dictionary<string,TextBox>();var form=new StackPanel();
        foreach(var (key,label) in new[]{("Name","镜头名称"),("Scene","场景"),("ShotSize","景别"),("CameraAndLens","机位 / 镜头"),("Action","人物动作"),("Props","道具"),("Lighting","灯光"),("Remarks","备注"),("EstimatedMinutes","预计分钟")})
        {form.Children.Add(Text(label,11));var box=new TextBox{Text=row[key],Margin=new Thickness(0,2,0,8),TextWrapping=TextWrapping.Wrap};System.Windows.Automation.AutomationProperties.SetName(box,label);fields[key]=box;form.Children.Add(box);}
        var original=fields.ToDictionary(p=>p.Key,p=>p.Value.Text);
        _inspectorCapture=()=>{if(owner.ProjectId!=project)return;if(fields.All(p=>p.Value.Text==original[p.Key]))return;var current=new PlanningShotEditRow(owner.ShotDraft(shot.ShotId),owner.StageShot);current.BeginEdit();
            foreach(var pair in fields)if(pair.Value.Text!=original[pair.Key]){current[pair.Key]=pair.Value.Text;original[pair.Key]=pair.Value.Text;}
            current.EndEdit();};
        form.Children.Add(AsyncAction("应用镜头修改",async()=>{_inspectorCapture?.Invoke();if(await owner.FlushAsync())Render();}));
        form.Children.Add(Action("取消本次修改",()=>{foreach(var pair in fields)pair.Value.Text=original[pair.Key];}));AddInspectorGroup("当前镜头",form);
    }
}
