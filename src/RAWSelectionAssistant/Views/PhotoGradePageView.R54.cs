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
        var owner=model;r54Graph=new(owner.Editor){Thumbnail=id=>owner.LayerThumbnailContext?.Outputs.GetValueOrDefault(id),MaskThumbnail=id=>owner.LayerThumbnailContext?.Masks.GetValueOrDefault(id),DevelopedStage=()=>owner.IsLightPrintLocked&&!owner.IsLoading?RAWSelectionAssistant.Core.Services.Projects.DevelopedStageProjection.FromCommitted(owner.ActiveTarget?.ActiveEditVersionId,owner.ActiveTarget?.RawDefinition):null,DevelopedThumbnail=()=>!owner.IsLoading&&!owner.IsInstantGenerating?owner.Editor.MatchedImage:null};
        r54Graph.StageSelected+=stage=>ShowDevelopedStage(stage);r54Graph.RefreshDevelopedStage();
        R53NodeContent.Children.Clear();R53NodeContent.Children.Add(r54Graph);
        r54DefaultSized=false;var layout=owner.Layout.Normalize();r54GraphHeight=layout.R54GraphHeight;r53NodesCollapsed=layout.R54GraphCollapsed??(ActualWidth<=1366);
        r54Graph.Restore(new(layout.R54GraphPanX,layout.R54GraphPanY),layout.R54GraphZoom);
        r54Graph.LayoutChanged+=()=>{R54GraphZoomLabel.Text=$"{r54Graph.Zoom:P0}";SaveR54GraphLayout();};
        r54Graph.RequestMask+=()=>{R53Brush(this,new());OpenR53Panel();};
    }
    private Window? developedRecipeWindow;
    private void ShowDevelopedStage(RAWSelectionAssistant.Core.Services.Projects.DevelopedStageProjection stage)
    {
        developedRecipeWindow?.Close();
        EditModes.SelectedIndex=0;OpenR53Panel();
        var box=new TextBox{Text=stage.FrozenRecipeJson,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=240,ToolTip="冻结配方只读；执行由现有显影管线负责。"};
        var panel=new StackPanel{Margin=new Thickness(4)};panel.Children.Add(new TextBlock{Text="即时胶片 · 已显影 · 系统锁定",TextWrapping=TextWrapping.Wrap});panel.Children.Add(new TextBlock{Text=$"变体 {stage.VariantId}\n来源 {stage.SourceVariantId?.ToString()??"原图"}\n配方版本 {stage.RecipeVersion} / {stage.RecipeSha256}",TextWrapping=TextWrapping.Wrap});panel.Children.Add(box);
        var dialog=new Window{Title="光镜 · 冻结显影配方",Owner=Window.GetWindow(this),Width=560,Height=420,Content=panel,Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(29,29,32)),ShowInTaskbar=false};developedRecipeWindow=dialog;dialog.Closed+=(_,_)=>{if(ReferenceEquals(developedRecipeWindow,dialog))developedRecipeWindow=null;};dialog.Show();
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
    private Border? r54ProfessionalGroup;
    private void UpdateR54ProfessionalGroup(bool professional)
    {
        if(R53SimpleLayers.Content is not StackPanel group||model is null)return;
        bool complex=!professional&&model.Editor.HasProfessionalRouting;
        foreach(var child in group.Children.OfType<UIElement>())if(child!=R53SimpleLayerList&&child!=r54ProfessionalGroup)child.Visibility=complex?Visibility.Collapsed:Visibility.Visible;
        if(r54ProfessionalGroup is null)
        {
            var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(26)});row.ColumnDefinitions.Add(new(){Width=new GridLength(100)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(54)});
            var enabled=new CheckBox{ToolTip="专业组显隐",VerticalAlignment=VerticalAlignment.Center};enabled.SetBinding(CheckBox.IsCheckedProperty,new System.Windows.Data.Binding("Editor.ProfessionalGroupEnabled"){Mode=System.Windows.Data.BindingMode.TwoWay});row.Children.Add(enabled);
            var title=new TextBlock{Text="▣ 专业组 🔒",ToolTip="复杂路由整体保留，不压平；显隐和强度只作用整个组。",VerticalAlignment=VerticalAlignment.Center,TextTrimming=System.Windows.TextTrimming.CharacterEllipsis};Grid.SetColumn(title,1);row.Children.Add(title);
            var amount=new Slider{Minimum=0,Maximum=1,ToolTip="专业组强度",VerticalAlignment=VerticalAlignment.Center};amount.PreviewMouseLeftButtonDown+=(_,_)=>model?.Editor.BeginEditTransaction();amount.PreviewMouseLeftButtonUp+=(_,_)=>model?.Editor.CommitEditTransaction();amount.SetBinding(Slider.ValueProperty,new System.Windows.Data.Binding("Editor.ProfessionalGroupAmount"){Mode=System.Windows.Data.BindingMode.TwoWay});Grid.SetColumn(amount,2);row.Children.Add(amount);
            var edit=new Button{Content="专业",ToolTip="切到专业模式编辑路由"};edit.Click+=(_,_)=>{if(model is not null)model.R53Professional=true;OpenR53Panel();ApplyR53Layout();};Grid.SetColumn(edit,3);row.Children.Add(edit);
            r54ProfessionalGroup=new Border{Height=38,BorderBrush=System.Windows.Media.Brushes.DimGray,BorderThickness=new Thickness(0,0,0,1),Child=row};group.Children.Insert(0,r54ProfessionalGroup);
        }
        r54ProfessionalGroup.Visibility=complex?Visibility.Visible:Visibility.Collapsed;
    }

    private StackPanel? nodeToolNotice;
    private static RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType? ToolType(string name)=>name switch
    {"色轮"=>RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.ColorBalance,"曲线"=>RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.Curve,"HSL"=>RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.ColorRange,"基础"=>RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.BasicTone,_=>null};
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
    private async Task SampleR54ColorAsync(Point point)
    {
        if(model is not{} workspace)return;
        var editor=workspace.Editor;
        if(editor.SelectedAdjustmentNode is not{Type:RAWSelectionAssistant.Core.Services.Projects.ColorStudioNodeType.ColorRange,IsLocked:false}){editor.CancelSamplingCommand.Execute(null);return;}
        var mapped=ColorStudioSampleMapping.Map(point,PhotoViewport.RenderSize,editor.EffectiveViewMode,editor.SplitPosition,editor.SourceImage,editor.MatchedImage,PhotoViewport.State);
        if(mapped is not{} sample)return;
        var (image,x,y)=sample;
        if(System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))editor.SampleMode="减少取样";
        else if(System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))editor.SampleMode="增加取样";
        await workspace.CompleteRangeSampleAsync((x+.5)/image.PixelWidth,(y+.5)/image.PixelHeight);
    }
}
