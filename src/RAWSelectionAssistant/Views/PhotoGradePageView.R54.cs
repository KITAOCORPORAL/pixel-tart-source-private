using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
    private StudioNodeGraphView? r54Graph;
    private double r54GraphHeight=216;private bool r54DefaultSized;
    private bool r54SavingLayout;
    private void R54EditorChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName=="SelectedAdjustmentNode")r53ParameterTab=model?.Editor.SelectedAdjustmentNode?.Type switch{RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.Curve=>"曲线",RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.ColorBalance=>"色轮",RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.ColorRange=>"HSL",_=>"基础"};if(e.PropertyName is "AdjustmentStack" or "SelectedAdjustmentNode")ApplyR53Layout();r54Graph?.InvalidateVisual();}
    private void AttachR54Graph()
    {
        if(model is null)return;
        var owner=model;r54Graph=new(owner.Editor){Thumbnail=id=>owner.LayerThumbnailContext?.Outputs.GetValueOrDefault(id),MaskThumbnail=id=>owner.LayerThumbnailContext?.Masks.GetValueOrDefault(id)};
        R53NodeContent.Children.Clear();R53NodeContent.Children.Add(r54Graph);
        r54DefaultSized=false;var layout=owner.Layout.Normalize();r54GraphHeight=layout.R54GraphHeight;r53NodesCollapsed=layout.R54GraphCollapsed??(ActualWidth<=1366);
        r54Graph.Restore(new(layout.R54GraphPanX,layout.R54GraphPanY),layout.R54GraphZoom);
        r54Graph.LayoutChanged+=()=>{R54GraphZoomLabel.Text=$"{r54Graph.Zoom:P0}";SaveR54GraphLayout();};
        r54Graph.RequestMask+=()=>{R53Brush(this,new());OpenR53Panel();};
    }
    private void SaveR54GraphLayout()
    {
        if(model is null||r54Graph is null||r54SavingLayout)return;
        r54SavingLayout=true;try{model.Layout=model.Layout with{R54GraphHeight=r54GraphHeight,R54GraphCollapsed=r53NodesCollapsed,R54GraphZoom=r54Graph.Zoom,R54GraphPanX=r54Graph.Pan.X,R54GraphPanY=r54Graph.Pan.Y};}finally{r54SavingLayout=false;}
    }
    private void R54OpenNodeLibrary(object sender,RoutedEventArgs e)=>r54Graph?.OpenLibrary((FrameworkElement)sender);
    private void R54FitGraph(object sender,RoutedEventArgs e){r53NodesCollapsed=false;ApplyR53Layout();r54Graph?.Fit();SaveR54GraphLayout();}
    private void R54GraphHeightDrag(object sender,DragDeltaEventArgs e){r53NodesCollapsed=false;r54GraphHeight=Math.Clamp(r54GraphHeight-e.VerticalChange,100,Math.Max(120,ActualHeight-320));ApplyR53Layout();}
    private void R54GraphHeightDone(object sender,DragCompletedEventArgs e)=>SaveR54GraphLayout();
    private StackPanel? r54ProfessionalGroup;
    private void UpdateR54ProfessionalGroup(bool professional)
    {
        if(R53SimpleLayers.Content is not StackPanel group||model is null)return;
        bool complex=!professional&&model.Editor.HasProfessionalRouting;
        foreach(var child in group.Children.OfType<UIElement>())if(child!=R53SimpleLayerList&&child!=r54ProfessionalGroup)child.Visibility=complex?Visibility.Collapsed:Visibility.Visible;
        if(r54ProfessionalGroup is null)
        {
            r54ProfessionalGroup=new StackPanel();var title=new TextBlock{Text="专业组 · 锁定路由",Margin=new Thickness(6),Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156,196,186))};r54ProfessionalGroup.Children.Add(title);
            var enabled=new CheckBox{Content="可见"};enabled.SetBinding(CheckBox.IsCheckedProperty,new System.Windows.Data.Binding("Editor.ProfessionalGroupEnabled"){Mode=System.Windows.Data.BindingMode.TwoWay});r54ProfessionalGroup.Children.Add(enabled);
            var amount=new Slider{Minimum=0,Maximum=1,ToolTip="仅专业组合成强度"};amount.PreviewMouseLeftButtonDown+=(_,_)=>model?.Editor.BeginEditTransaction();amount.PreviewMouseLeftButtonUp+=(_,_)=>model?.Editor.CommitEditTransaction();amount.SetBinding(Slider.ValueProperty,new System.Windows.Data.Binding("Editor.ProfessionalGroupAmount"){Mode=System.Windows.Data.BindingMode.TwoWay});r54ProfessionalGroup.Children.Add(amount);
            var edit=new Button{Content="切到专业编辑路由"};edit.Click+=(_,_)=>{if(model is not null)model.R53Professional=true;OpenR53Panel();ApplyR53Layout();};r54ProfessionalGroup.Children.Add(edit);group.Children.Insert(0,r54ProfessionalGroup);
        }
        r54ProfessionalGroup.Visibility=complex?Visibility.Visible:Visibility.Collapsed;
    }

    private StackPanel? nodeToolNotice;
    private static RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType? ToolType(string name)=>name switch
    {"色轮"=>RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.ColorBalance,"曲线"=>RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.Curve,"范围" or "HSL"=>RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.ColorRange,"基础"=>RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.BasicTone,_=>null};
    internal void SelectClosureNodeTool(string name){r53ParameterTab=name;} // presentation only; never select/add/edit a node
    private bool ToolMatchesSelection()=>ToolType(r53ParameterTab) is{} type&&(model?.Editor.SelectedAdjustmentNode?.Type==type||r53ParameterTab=="基础"&&model?.Editor.SelectedAdjustmentNode?.Type is RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.WhiteBalance or RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.Levels or RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.SkinTone or RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.Details);
    private void UpdateNodeToolNotice(StackPanel group,bool professional)
    {
        if(nodeToolNotice is null){nodeToolNotice=new StackPanel{Margin=new Thickness(6)};group.Children.Insert(Math.Min(2,group.Children.Count),nodeToolNotice);}
        nodeToolNotice.Children.Clear();bool mismatch=professional&&r53ParameterTab!="全部"&&!ToolMatchesSelection();nodeToolNotice.Visibility=mismatch?Visibility.Visible:Visibility.Collapsed;
        if(!mismatch)return;nodeToolNotice.Children.Add(new TextBlock{Text="当前节点不支持此工具；未改变编辑目标。",TextWrapping=TextWrapping.Wrap,Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156,196,186))});
        var add=new Button{Content="新增“"+r53ParameterTab+"”节点",IsEnabled=model?.Editor.SelectedNodeEditable==true&&ToolType(r53ParameterTab) is not null};add.Click+=(_,_)=>AddRequestedToolNode();nodeToolNotice.Children.Add(add);
    }
    internal void AddRequestedToolNode(){if(model is null||ToolType(r53ParameterTab) is not{} type)return;model.Editor.AddGraphSerialNode(type.ToString());ApplyR53Layout();}
}
