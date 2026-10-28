using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
    private void R53LayerAction(object sender,RoutedEventArgs e)
    {
        if(model is null||sender is not Button{DataContext:RAWSelectionAssistant.Core.Services.Projects.ColorAdjustmentStackNode node,Tag:string action})return;
        model.Editor.SelectedAdjustmentNode=node;
        if(action=="visibility")model.Editor.ToggleAdjustmentNodeCommand.Execute(null);
        else if(action=="lock")model.Editor.SelectedNodeLocked=!node.IsLocked;
        else{R53Brush(sender,e);OpenR53Panel();}
        e.Handled=true;
    }
    private bool r53CompactPanelOpen,r53WidePanelOpen=true,r53NodesCollapsed,r53FilmCollapsed;
    private string r53ParameterTab="基础";
    private WrapPanel? r53ParameterTabs;
    private void R53ToggleNodes(object s,RoutedEventArgs e){r53NodesCollapsed=!r53NodesCollapsed;ApplyR53Layout();}
    private void R53ToggleFilmstrip(object s,RoutedEventArgs e){r53FilmCollapsed=!r53FilmCollapsed;ApplyR53Layout();}
    private void R53OpenFilmPanel(object s,RoutedEventArgs e){EditModes.SelectedIndex=1;OpenR53Panel();}
    private void R53OpenLayerPanel(object s,RoutedEventArgs e){EditModes.SelectedIndex=0;OpenR53Panel();}
    private void OpenR53Panel(){r53CompactPanelOpen=r53WidePanelOpen=true;ApplyR53Layout();}
    private void R53FileActions(object s,RoutedEventArgs e)
    {
        var menu=new ContextMenu{PlacementTarget=(UIElement)s};
        menu.Items.Add(new MenuItem{Header="导入照片…",Command=model?.ChooseTargetCommand});
        menu.Items.Add(new MenuItem{Header="打开项目…",Command=model?.OpenSessionCommand});
        menu.Items.Add(new MenuItem{Header="保存项目 / 重试保存",Command=model?.SaveSessionCommand});
        var export=new MenuItem{Header="导出主变体…"};export.Click+=(_,_)=>OpenExport(s,e);menu.Items.Add(export);
        var close=new MenuItem{Header="退出暗室"};close.Click+=(_,_)=>{if(Window.GetWindow(this)?.DataContext is RAWSelectionAssistant.ViewModels.MainViewModel host)host.NavigateCommand.Execute("Workbench");else Window.GetWindow(this)?.Close();};menu.Items.Add(close);menu.IsOpen=true;
    }
    private void R53WorkspaceMenu(object s,RoutedEventArgs e)
    {
        var menu=new ContextMenu{PlacementTarget=(UIElement)s};
        foreach(var (name,command) in new[]{("复制调整",model?.CopyAdjustmentsCommand as System.Windows.Input.ICommand),("粘贴调整",model?.ApplyAdjustmentsCommand),("同步到所选照片",model?.PhotoGradeSyncStepCommand)})menu.Items.Add(new MenuItem{Header=name,Command=command});
        void Add(string title,Action action){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Add("前后对比方式",()=>CompareMenu(s,e));Add("直方图 / 示波器",()=>{R53ToggleScopes(s,e);OpenR53Panel();});Add("面板布局",()=>PanelMenu(s,e));var guides=new MenuItem{Header="参考线"};foreach(var (title,index) in new[]{("无",0),("三分线",1),("中心十字",2)}){var guide=new MenuItem{Header=title};guide.Click+=(_,_)=>GuideChoice.SelectedIndex=index;guides.Items.Add(guide);}menu.Items.Add(guides);
        Add("变体与历史",()=>{EditModes.SelectedIndex=4;OpenR53Panel();});Add("二到四变体同步对比",()=>R53MultiCompare(s,e));Add("风格与方案",()=>{EditModes.SelectedIndex=3;OpenR53Panel();});Add("所有现有调整工具",()=>{r53ParameterTab="全部";EditModes.SelectedIndex=0;OpenR53Panel();});Add("颜色管理…",()=>OpenColorManagement(s,e));Add("帮助与诊断",()=>{r53ParameterTab="全部";EditModes.SelectedIndex=0;OpenR53Panel();DiagnosticsSection.IsExpanded=true;DiagnosticsSection.BringIntoView();});menu.IsOpen=true;
    }
    private void ApplyR53ParameterHierarchy(bool professional,bool film)
    {
        if(AdjustmentScroll.Content is not StackPanel group||R53SimpleLayers is null)return;
        if(r53ParameterTabs is null){r53ParameterTabs=new WrapPanel{Margin=new Thickness(4)};foreach(var name in new[]{"曲线","色轮","HSL","基础"}){var button=new Button{Content=name,Width=64,ToolTip=name+"参数"};button.Click+=(_,_)=>{r53ParameterTab=name;ApplyR53Layout();};r53ParameterTabs.Children.Add(button);}group.Children.Insert(Math.Min(1,group.Children.Count),r53ParameterTabs);}
        if(group.Children.IndexOf(R53SimpleLayers)!=0){group.Children.Remove(R53SimpleLayers);group.Children.Insert(0,R53SimpleLayers);}
        if(group.Children.IndexOf(r53ParameterTabs)!=1){group.Children.Remove(r53ParameterTabs);group.Children.Insert(1,r53ParameterTabs);}
        R53SimpleLayerList.Visibility=professional?Visibility.Collapsed:Visibility.Visible;
        r53ParameterTabs.Visibility=professional?Visibility.Visible:Visibility.Collapsed;
        R53SimpleLayers.Header=professional?"所选节点":"图层";
        foreach(var button in r53ParameterTabs.Children.OfType<Button>())button.Background=new SolidColorBrush(Equals(button.Content,r53ParameterTab)?Color.FromRgb(48,68,62):Color.FromRgb(29,29,32));
        foreach(var section in group.Children.OfType<Expander>())
        {
            if(ReferenceEquals(section,R53SimpleLayers))continue;
            var tag=section.Tag as string??"";
            var basic=tag is "AdjustmentScroll.1" or "AdjustmentScroll.2" or "AdjustmentScroll.5" or "AdjustmentScroll.6";
            var show=r53ParameterTab=="全部"||(!professional?basic:r53ParameterTab switch{"曲线"=>tag=="AdjustmentScroll.4","色轮"=>tag=="AdjustmentScroll.7","HSL"=>tag=="AdjustmentScroll.ColorRange",_=>basic});
            section.Visibility=show?Visibility.Visible:Visibility.Collapsed;
            if(professional&&show)section.IsExpanded=true;
        }
        foreach(var button in group.Children.OfType<Button>())button.Visibility=r53ParameterTab=="全部"?Visibility.Visible:Visibility.Collapsed;
        if(PhotographerCurve is not null)PhotographerCurve.Compact=true;
    }
}
