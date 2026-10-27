using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
public partial class PhotoGradePageView
{
    private bool r53ResourcesRequested=true,r53CompareHeld;
    private string r53ResourceTab="预设";private string r53Search="";private bool r53FavoritesOnly,r53RecentOnly;
    private Window? r53Inspect;
    private Window? r53Owner;
    private Window? r53ResourceWindow;private StackPanel? r53NarrowResourceContent;
    private void ReleaseR53Compare(){if(r53CompareHeld)model?.Editor.HoldOriginal(false);r53CompareHeld=false;}
    private void R53WindowDeactivated(object? s,EventArgs e){ReleaseR53Compare();CancelMaskStroke();}
    private void InitializeR53()
    {
        SizeChanged+=(_,_)=>{if(ActualWidth<1800&&R53InspectDock.Visibility==Visibility.Visible)closeR53Inspection?.Invoke();ApplyR53Layout();};
        PreviewKeyDown+=async(_,e)=>
        {
            if(Keyboard.FocusedElement is TextBox or ComboBox || model is null)return;
            if(Keyboard.Modifiers==ModifierKeys.Control&&(e.Key is Key.D1 or Key.D2)){model.R53Professional=e.Key==Key.D2;ApplyR53Layout();EditModes.SelectedIndex=model.R53Professional?2:0;e.Handled=true;return;}
            if(Keyboard.Modifiers!=ModifierKeys.None)return;
            if(ExecuteR53PointerShortcut(e.Key)){e.Handled=true;}
            else if(e.Key==Key.F2){await model.CreateR53VariantAsync(true);e.Handled=true;}
            else if(e.Key==Key.F3){await model.CreateR53VariantAsync(false);e.Handled=true;}
            else if(e.Key==Key.Escape){CancelMaskStroke();r53MultiWindow?.Close();closeR53Inspection?.Invoke();e.Handled=true;}
        };
        Loaded+=(_,_)=>{var window=Window.GetWindow(this);if(ReferenceEquals(window,r53Owner))return;if(r53Owner is not null)r53Owner.Deactivated-=R53WindowDeactivated;r53Owner=window;if(r53Owner is not null)r53Owner.Deactivated+=R53WindowDeactivated;};
        Unloaded+=(_,_)=>{ReleaseR53Compare();closeR53Inspection?.Invoke();if(r53Owner is not null)r53Owner.Deactivated-=R53WindowDeactivated;r53Owner=null;};
    }
    internal bool ExecuteR53PointerShortcut(Key key)
    {
        if(model is null)return false;
        if(key==Key.B){R53Brush(this,new());return true;}
        if(key==Key.Oem5){model.Editor.ViewMode=model.Editor.ViewMode=="原片"?"仿色结果":"原片";return true;}
        return false;
    }
    internal void ApplyR53Layout()
    {
        if(R53Resources is null)return;
        // ActualWidth is the WPF client DIP width; no resolution or DPI heuristic.
        var width=ActualWidth>0?ActualWidth:Width;var professional=model?.R53Professional==true;
        var resources=r53ResourcesRequested&&width>=1200;
        ResourceColumn.Width=new GridLength(resources?264:0);R53Resources.Visibility=resources?Visibility.Visible:Visibility.Collapsed;
        if(width<1440){SetPanelOpen(false);}else if(RightPanel.Visibility!=Visibility.Visible)SetPanelOpen(true);
        R53SerialNodes.Visibility=professional?Visibility.Visible:Visibility.Collapsed;R53NodeRow.Height=new GridLength(professional?132:0);
    }
    private void R53ParameterCategory(object sender,RoutedEventArgs e)
    {
        if(model is null||sender is not Button{Tag:string type})return;
        var parsed=Enum.Parse<ColorStudioNodeType>(type);var node=model.Editor.AdjustmentNodes.FirstOrDefault(n=>n.Type==parsed);
        if(node is null){model.Editor.AddAdjustmentNodeCommand.Execute(type);node=model.Editor.AdjustmentNodes.LastOrDefault(n=>n.Type==parsed);}
        if(node is not null)model.Editor.SelectedAdjustmentNode=node;EditModes.SelectedIndex=0;
        if(type=="Curve")PhotographerCurve.BringIntoView();else if(type=="ColorBalance")AdjustmentScroll.ScrollToVerticalOffset(380);else AdjustmentScroll.ScrollToTop();
    }
    private void R53Simple(object s,RoutedEventArgs e){if(model is not null)model.R53Professional=false;ApplyR53Layout();EditModes.SelectedIndex=0;}
    private void R53Professional(object s,RoutedEventArgs e){if(model is not null)model.R53Professional=true;ApplyR53Layout();EditModes.SelectedIndex=2;}
    private void R53ToggleResources(object s,RoutedEventArgs e)
    {
        if(ActualWidth>=1200){r53ResourcesRequested=!r53ResourcesRequested;ApplyR53Layout();return;}
        if(r53ResourceWindow is not null){r53ResourceWindow.Activate();return;}
        var panel=new DockPanel{Margin=new Thickness(12)};var header=new StackPanel();var tabs=new WrapPanel();
        foreach(var name in new[]{"预设","滤镜","即时胶片"}){var tab=new Button{Content=name};tab.Click+=(_,_)=>{r53ResourceTab=name;RenderR53Resources();};tabs.Children.Add(tab);}header.Children.Add(tabs);
        var search=new TextBox{Text=r53Search,Margin=new Thickness(0,8,0,8)};System.Windows.Automation.AutomationProperties.SetName(search,"搜索资源");search.TextChanged+=(_,_)=>{r53Search=search.Text;RenderR53Resources();};header.Children.Add(search);
        var filters=new WrapPanel();foreach(var name in new[]{"全部","收藏","最近"}){var filter=new Button{Content=name};filter.Click+=(_,_)=>{r53FavoritesOnly=name=="收藏";r53RecentOnly=name=="最近";RenderR53Resources();};filters.Children.Add(filter);}header.Children.Add(filters);DockPanel.SetDock(header,Dock.Top);panel.Children.Add(header);
        r53NarrowResourceContent=new StackPanel();panel.Children.Add(new ScrollViewer{Content=r53NarrowResourceContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var window=new Window{Title="光镜 · 资源",Owner=Window.GetWindow(this),Width=340,Height=550,Content=panel,Background=Background};r53ResourceWindow=window;
        window.Closed+=(_,_)=>{r53ResourceWindow=null;r53NarrowResourceContent=null;RenderR53Resources();};RenderR53Resources();window.Show();search.Focus();
    }
    private void R53Brush(object s,RoutedEventArgs e){CancelMaskStroke();if(model is not null)model.Editor.ViewMode="仿色结果";activeTool="画笔";PhotoViewport.Cursor=Cursors.Cross;ToolLabel.Text="画笔 · 局部蒙版";ToolHint.Text="在当前图层绘制 · 锁定图层不可改";EditModes.SelectedIndex=2;DrawMaskOverlay();}
    private void R53CompareDown(object s,MouseButtonEventArgs e){if(model is null)return;r53CompareHeld=true;model.Editor.HoldOriginal(true);R53HoldCompare.CaptureMouse();e.Handled=true;}
    private void R53CompareUp(object s,MouseButtonEventArgs e){R53CompareLost(s,e);R53HoldCompare.ReleaseMouseCapture();e.Handled=true;}
    private void R53CompareLost(object s,MouseEventArgs e){ReleaseR53Compare();}
    private void R53ToggleVariants(object s,RoutedEventArgs e){if(model?.ActiveTarget is {} target){target.VariantsExpanded=!target.VariantsExpanded;model.NotifyR53Edit();}}
    private async void R53InlineVariantPicked(object s,SelectionChangedEventArgs e)
    {if(ReferenceEquals(e.Source,s)&&model is {} workspace&&e.AddedItems.OfType<StudioEditVersion>().FirstOrDefault() is {} version&&!workspace.IsLoading)await workspace.SwitchEditVersionAsync(version.Id);}
    private async void R53NewOriginal(object s,RoutedEventArgs e){if(model is not null)await model.CreateR53VariantAsync(true);}
    private async void R53Duplicate(object s,RoutedEventArgs e){if(model is not null)await model.CreateR53VariantAsync(false);}
    private void R53SetMain(object s,RoutedEventArgs e)=>model?.SetR53MainVariant();
    private async void R53UndoDelete(object s,RoutedEventArgs e){if(model is not null)await model.UndoR53VariantDeleteAsync();}
    private void R53PresetTab(object s,RoutedEventArgs e){r53ResourceTab="预设";RenderR53Resources();}
    private void R53FilterTab(object s,RoutedEventArgs e){r53ResourceTab="滤镜";RenderR53Resources();}
    private void R53InstantTab(object s,RoutedEventArgs e){r53ResourceTab="即时胶片";RenderR53Resources();EditModes.SelectedIndex=1;}
    private void RenderR53Resources()
    {
        var target=r53NarrowResourceContent??R53ResourceContent;if(target is null)return;target.Children.Clear();
        void Add(string name,Action action){if(!string.IsNullOrWhiteSpace(r53Search)&&!name.Contains(r53Search,StringComparison.CurrentCultureIgnoreCase))return;if(r53FavoritesOnly&&model?.R53FavoriteResources.Contains(name)!=true)return;if(r53RecentOnly&&model?.R53RecentResources.Contains(name)!=true)return;var row=new DockPanel();var star=new Button{Content=model?.R53FavoriteResources.Contains(name)==true?"★":"☆",ToolTip="收藏资源"};System.Windows.Automation.AutomationProperties.SetName(star,"收藏 "+name);DockPanel.SetDock(star,Dock.Right);star.Click+=(_,_)=>{model?.ToggleR53Favorite(name);RenderR53Resources();};row.Children.Add(star);var b=new Button{Content=name,Height=30,HorizontalContentAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,3,0,3)};b.Click+=(_,_)=>{action();model?.RecordR53Resource(name);RenderR53Resources();};row.Children.Add(b);target.Children.Add(row);}
        if(r53ResourceTab=="即时胶片"){foreach(var paper in model?.PhotographerPapers??[])Add(paper.Name,()=>{if(model is not null){model.SelectedPhotographerPaperId=paper.ProfileId;EditModes.SelectedIndex=1;}});return;}
        if(r53ResourceTab=="滤镜"){Add("胶片风格（现有处理链）",()=>EditModes.SelectedIndex=3);return;}
        Add("提亮 +0.3 EV",()=>{if(model is null)return;model.Editor.AddAdjustmentNodeCommand.Execute("BasicTone");var parameter=ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure");model.Editor.SetToolParameter(parameter,.3);});
        foreach(var (name,type) in new[]{("基础调色","BasicTone"),("白平衡","WhiteBalance"),("曲线","Curve"),("色轮","ColorBalance"),("色域取样（已有能力）","ColorRange")})Add(name,()=>model?.Editor.AddAdjustmentNodeCommand.Execute(type));
        foreach(var scheme in model?.Editor.ColorSchemes??[])Add(scheme.Name,()=>{if(model is not null){model.Editor.SelectedColorScheme=scheme;model.Editor.RequestApplySchemeCommand.Execute(null);}});
    }
    private void R53SearchChanged(object sender,TextChangedEventArgs e){r53Search=(sender as TextBox)?.Text??"";RenderR53Resources();}
    private void R53ResourceAll(object sender,RoutedEventArgs e){r53FavoritesOnly=r53RecentOnly=false;RenderR53Resources();}
    private void R53ResourceFavorites(object sender,RoutedEventArgs e){r53FavoritesOnly=true;r53RecentOnly=false;RenderR53Resources();}
    private void R53ResourceRecent(object sender,RoutedEventArgs e){r53RecentOnly=true;r53FavoritesOnly=false;RenderR53Resources();}
    private bool scopesOpen=true;private void R53ToggleScopes(object sender,RoutedEventArgs e){scopesOpen=!scopesOpen;HistogramPanel.Visibility=scopesOpen?Visibility.Visible:Visibility.Collapsed;HistogramRow.Height=new GridLength(scopesOpen?108:0);}
    private Action? closeR53Inspection;
    private void R53Inspect3D(object s,RoutedEventArgs e)
    {
        if(model is null)return;
        if(closeR53Inspection is not null){closeR53Inspection();return;}
        var ownerModel=model;
        var content=new StackPanel{Margin=new Thickness(8)};
        content.Children.Add(new TextBlock{Text="3D 色彩 · 当前真实图像数据",FontSize=14,Margin=new Thickness(0,8,0,8)});
        var cloud=new ColorSpace3DViewport{MinWidth=420,MinHeight=300,Height=340,DataContext=model};content.Children.Add(cloud);
        if(model.ColorSpaceModel is {} current)cloud.SetModel(current);
        System.ComponentModel.PropertyChangedEventHandler update=(_,args)=>{if(args.PropertyName==nameof(ownerModel.ColorSpaceModel)&&ownerModel.ColorSpaceModel is {} changed)cloud.SetModel(changed);};
        ownerModel.PropertyChanged+=update;
        content.Children.Add(new Button{Content="从当前图像生成",Command=model.BuildColorSpaceModelCommand});
        var undock=new Button{Content="解除停靠（浮动）"};content.Children.Add(undock);var back=new Button{Content="返回主图"};content.Children.Add(back);
        void Close()
        {
            ownerModel.PropertyChanged-=update;R53InspectDock.Child=null;R53InspectDock.Visibility=Visibility.Collapsed;R53InspectColumn.Width=new GridLength(0);
            var window=r53Inspect;r53Inspect=null;closeR53Inspection=null;window?.Close();
        }
        closeR53Inspection=Close;back.Click+=(_,_)=>Close();
        void Float(bool focused)
        {
            R53InspectDock.Child=null;R53InspectDock.Visibility=Visibility.Collapsed;R53InspectColumn.Width=new GridLength(0);
            r53Inspect=new Window{Title=focused?"光镜 · 专注色彩检查":"光镜 · 浮动色彩检查",Owner=Window.GetWindow(this),MinWidth=460,MinHeight=440,Width=620,Height=540,Content=content,Background=new SolidColorBrush(Color.FromRgb(29,29,32)),WindowStartupLocation=WindowStartupLocation.CenterOwner};
            r53Inspect.Closed+=(_,_)=>{r53Inspect=null;if(closeR53Inspection is not null)Close();};
            if(focused)r53Inspect.ShowDialog();else r53Inspect.Show();
        }
        undock.Click+=(_,_)=>{if(r53Inspect is null)Float(false);};
        // Client-DIP width permits 440px inspector + existing304 + resources264,
        // while leaving the main photo >=560. No overlay on wide default open.
        if(ActualWidth>=1800){R53InspectColumn.Width=new GridLength(440);R53InspectDock.Visibility=Visibility.Visible;R53InspectDock.Child=content;}
        else Float(true); // Explicit user entry into focused inspection; return is always present.
    }
}
