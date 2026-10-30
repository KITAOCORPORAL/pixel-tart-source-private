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
            if(Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or PasswordBox or ComboBox || model is null)return;
            if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.L){await ToggleViewingTableAsync();e.Handled=true;return;}
            if(ViewingMode){if(viewingTable!.HandleShortcut(e.Key,Keyboard.Modifiers,e.IsRepeat))e.Handled=true;return;}
            if(r54Graph?.IsKeyboardFocusWithin==true&&(e.Key is Key.F2 or Key.Delete||Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.D)){r54Graph.ExecuteGraphKey(e.Key,Keyboard.Modifiers);e.Handled=true;return;}
            if(Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Alt)&&e.Key==Key.G){model.CaptureR54Still();r53ResourceTab="剧照";RenderR53Resources();e.Handled=true;return;}
            if(Keyboard.Modifiers==ModifierKeys.Control&&(e.Key is Key.D1 or Key.D2)){model.R53Professional=e.Key==Key.D2;ApplyR53Layout();EditModes.SelectedIndex=0;e.Handled=true;return;}
            if(Keyboard.Modifiers==ModifierKeys.Shift&&e.Key==Key.H){if(model.Editor.SelectedAdjustmentNode?.Type==ColorStudioNodeType.ColorRange)model.Editor.ShowSelection=!model.Editor.ShowSelection;e.Handled=true;return;}
            if(Keyboard.Modifiers==ModifierKeys.Shift&&e.Key==Key.N){R53ToggleNodes(this,new());e.Handled=true;return;}
            if(Keyboard.Modifiers==ModifierKeys.Shift&&e.Key==Key.W){activeTool="白平衡吸管";ToolLabel.Text=activeTool;PhotoViewport.Cursor=Cursors.Cross;e.Handled=true;return;}
            if(Keyboard.Modifiers!=ModifierKeys.None)return;
            if(activeTool=="画笔"&&(e.Key is Key.X or Key.OemOpenBrackets or Key.OemCloseBrackets)){if(e.Key==Key.X)maskErasing=!maskErasing;else maskRadius=Math.Clamp(maskRadius*(e.Key==Key.OemOpenBrackets?.8:1.25),.002,.5);maskPanelRaw=null;ConfigureMaskPanel();e.Handled=true;}
            else if(e.Key==Key.W){if(r54SelectedStill is not null)R54CompareStill(r54SelectedStill);else model.Editor.ViewMode="左右对比";e.Handled=true;}
            else if(e.Key==Key.B&&activeTool!="画笔"){r53CompareHeld=true;model.Editor.HoldOriginal(true);e.Handled=true;}
            else if(ExecuteR53PointerShortcut(e.Key)){e.Handled=true;}
            else if(e.Key==Key.F2&&FilmstripArea.IsKeyboardFocusWithin){await model.CreateR53VariantAsync(true);e.Handled=true;}
            else if(e.Key==Key.F3&&FilmstripArea.IsKeyboardFocusWithin){await model.CreateR53VariantAsync(false);e.Handled=true;}
            else if(e.Key==Key.Escape){CancelMaskStroke();if(model.Editor.IsSampling)model.Editor.CancelSamplingCommand.Execute(null);r53MultiWindow?.Close();closeR53Inspection?.Invoke();e.Handled=true;}
        };
        PreviewKeyUp+=(_,e)=>{if(e.Key==Key.Space&&ViewingMode)viewingTable!.ReleaseSpaceForTest();if(e.Key==Key.B&&r53CompareHeld){ReleaseR53Compare();e.Handled=true;}};
        Loaded+=(_,_)=>{var window=Window.GetWindow(this);ApplyR53Layout();if(ReferenceEquals(window,r53Owner))return;if(r53Owner is not null)r53Owner.Deactivated-=R53WindowDeactivated;r53Owner=window;if(r53Owner is not null)r53Owner.Deactivated+=R53WindowDeactivated;};
        Unloaded+=(_,_)=>{ReleaseR53Compare();closeR53Inspection?.Invoke();if(r53Owner is not null)r53Owner.Deactivated-=R53WindowDeactivated;r53Owner=null;};
    }
    internal bool ExecuteR53PointerShortcut(Key key)
    {
        if(model is null)return false;
        if(key==Key.V){SelectTool(this,new());return true;}if(key==Key.H){HandTool(this,new());return true;}if(key==Key.Z){ZoomTool(this,new());return true;}if(key==Key.C){CropTool(this,new());return true;}
        if(key==Key.B&&activeTool=="画笔"){R53Brush(this,new());return true;}
        if(key==Key.Oem5){model.Editor.ViewMode=model.Editor.ViewMode=="原片"?"仿色结果":"原片";return true;}
        return false;
    }
    internal void ApplyR53Layout()
    {
        if(R53Resources is null)return;
        // Width here is the page client width in DIP, already excluding the host rail.
        var width=ActualWidth>0?ActualWidth:Width;if(!double.IsFinite(width)||width<=0)width=1200;
        var professional=model?.R53Professional==true;var film=EditModes.SelectedIndex==1;
        if(ActualWidth>0&&!r54DefaultSized){r53NodesCollapsed=model?.Layout.R54GraphCollapsed??(width<=1366);r54DefaultSized=true;}
        PaperPickerSection.SetCurrentValue(VisibilityProperty,Visibility.Collapsed);
        if(film&&r53ResourceTab!="即时胶片"){r53ResourceTab="即时胶片";RenderR53Resources();}
        var hosted=Window.GetWindow(this) is MainWindow;
        if(hosted&&System.Windows.Shell.WindowChrome.GetWindowChrome(Window.GetWindow(this)) is{} chrome)chrome.CaptionHeight=24;
        R53AppRailColumn.Width=new GridLength(hosted?0:40);AppRail.Visibility=hosted?Visibility.Collapsed:Visibility.Visible;
        var client=width-(hosted?0:40);var resources=r53ResourcesRequested&&client>=1160;
        ResourceColumn.Width=new GridLength(resources?264:0);R53Resources.Visibility=resources?Visibility.Visible:Visibility.Collapsed;
        var compact=client<1400;var open=film|| (compact?r53CompactPanelOpen:r53WidePanelOpen);var overlay=compact&&client<864;
        // Film recipe stays full height, including beside the bottom strips. On narrow clients
        // explicitly opened panes dock when >=560 DIP remains; otherwise use a recoverable drawer.
        SetPanelOpen(open);
        if(open&&overlay){PanelColumn.Width=new GridLength(40);PanelColumn.MinWidth=0;SplitterColumn.Width=new GridLength(0);PanelSplitter.Visibility=Visibility.Collapsed;RightPanel.Width=304;RightPanel.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(RightPanel,2);Grid.SetColumnSpan(RightPanel,3);Panel.SetZIndex(RightPanel,20);}
        else{RightPanel.Width=double.NaN;RightPanel.HorizontalAlignment=HorizontalAlignment.Stretch;Grid.SetColumn(RightPanel,4);Grid.SetColumnSpan(RightPanel,1);}
        ToolTip=client<1160?"资源已收起：点击工具栏资源打开；参数可从右侧图标恢复":null;
        R53ParameterRail.Visibility=open?Visibility.Collapsed:Visibility.Visible;
        if(!open){PanelColumn.MinWidth=0;PanelColumn.Width=new GridLength(40);SplitterColumn.Width=new GridLength(0);}
        R54ScopesArea.Visibility=professional?Visibility.Visible:Visibility.Collapsed;
        var scopeOpen=model?.Layout.R54ScopesOpen??false;
        R54ScopesColumn.Width=new GridLength(professional?(scopeOpen?Math.Min(232,Math.Max(120,width-650)):22):0);
        R54ScopesRail.Visibility=professional&&!scopeOpen?Visibility.Visible:Visibility.Collapsed;
        R54ScopesPanel.Visibility=professional&&scopeOpen?Visibility.Visible:Visibility.Collapsed;
        R54ScopesRow.Height=new GridLength(0);
        R53SerialNodes.Visibility=professional?Visibility.Visible:Visibility.Collapsed;R53NodeRow.Height=new GridLength(professional?(r53NodesCollapsed?22:r54GraphHeight):0);
        R53NodeContent.Visibility=r53NodesCollapsed?Visibility.Collapsed:Visibility.Visible;
        PhotoStrip.Visibility=r53FilmCollapsed?Visibility.Collapsed:Visibility.Visible;R53FilmContentRow.Height=new GridLength(r53FilmCollapsed?0:92);R53FilmRow.Height=new GridLength(r53FilmCollapsed?22:114);
        R53InlineVariants.Visibility=r53FilmCollapsed?Visibility.Collapsed:(model?.ActiveTarget?.VariantsExpanded==true?Visibility.Visible:Visibility.Collapsed);
        if(!r53FilmCollapsed&&model?.ActiveTarget?.VariantsExpanded==true)R53FilmRow.Height=GridLength.Auto;
        R53SimpleMode.Background=new SolidColorBrush(professional?Color.FromRgb(25,25,27):Color.FromRgb(48,68,62));R53ProMode.Background=new SolidColorBrush(professional?Color.FromRgb(48,68,62):Color.FromRgb(25,25,27));
        HistogramPanel.Visibility=EditModes.SelectedIndex==4?Visibility.Collapsed:Visibility.Visible;HistogramRow.Height=new GridLength(EditModes.SelectedIndex==4?0:98);
        ApplyR53ParameterHierarchy(professional,film);ApplyViewingHostLayout();
    }
    private void R53ParameterCategory(object sender,RoutedEventArgs e)
    {
        if(model is null||sender is not Button{Tag:string type})return;
        var parsed=Enum.Parse<ColorStudioNodeType>(type);var node=model.Editor.AdjustmentNodes.FirstOrDefault(n=>n.Type==parsed);
        if(node is null){model.Editor.AddAdjustmentNodeCommand.Execute(type);node=model.Editor.AdjustmentNodes.LastOrDefault(n=>n.Type==parsed);}
        if(node is not null)model.Editor.SelectedAdjustmentNode=node;EditModes.SelectedIndex=0;
        r53ParameterTab=type=="Curve"?"曲线":type=="ColorBalance"?"色轮":type=="ColorRange"?"HSL":"基础";ApplyR53Layout();OpenR53Panel();if(type=="Curve")PhotographerCurve.BringIntoView();else if(type=="ColorBalance")AdjustmentScroll.ScrollToVerticalOffset(380);else AdjustmentScroll.ScrollToTop();
    }
    private void R53Simple(object s,RoutedEventArgs e){r53ParameterTab="基础";if(model is not null)model.R53Professional=false;EditModes.SelectedIndex=0;ApplyR53Layout();}
    private void R53Professional(object s,RoutedEventArgs e){if(model is not null)model.R53Professional=true;r53ParameterTab="曲线";EditModes.SelectedIndex=0;ApplyR53Layout();}
    private void R53ToggleResources(object s,RoutedEventArgs e)
    {
        if(R53Resources.Visibility==Visibility.Visible||ActualWidth>=1240){r53ResourcesRequested=!r53ResourcesRequested;ApplyR53Layout();return;}
        if(r53ResourceWindow is not null){r53ResourceWindow.Activate();return;}
        var panel=new DockPanel{Margin=new Thickness(12)};var header=new StackPanel();var tabs=new WrapPanel();
        foreach(var name in new[]{"预设","滤镜","即时胶片","剧照"}){var tab=new Button{Content=name};tab.Click+=(_,_)=>{r53ResourceTab=name;RenderR53Resources();};tabs.Children.Add(tab);}header.Children.Add(tabs);
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
    private void R53PresetTab(object s,RoutedEventArgs e){model?.CancelLightPrintPreviewCommand.Execute(null);r53ResourceTab="预设";EditModes.SelectedIndex=0;RenderR53Resources();}
    private void R53FilterTab(object s,RoutedEventArgs e){model?.CancelLightPrintPreviewCommand.Execute(null);r53ResourceTab="滤镜";EditModes.SelectedIndex=3;RenderR53Resources();}
    private void R53InstantTab(object s,RoutedEventArgs e){r53ResourceTab="即时胶片";RenderR53Resources();EditModes.SelectedIndex=1;}
    private void RenderR53Resources()
    {
        r53PresetCancellation?.Cancel();
        var target=r53NarrowResourceContent??R53ResourceContent;if(target is null)return;
        // One real paper selector moves between the resource rail and legacy parameter host.
        // Never show duplicate active paper selectors in the R3 workspace.
        if(V3Editor.Parent is Panel priorPanel)priorPanel.Children.Remove(V3Editor);
        else if(V3Editor.Parent is ContentControl priorHost)priorHost.Content=null;
        target.Children.Clear();
        if(r53ResourceTab!="即时胶片") {PaperPickerSection.Content=V3Editor;PaperPickerSection.SetCurrentValue(VisibilityProperty,Visibility.Collapsed);}
        if(r53ResourceTab=="即时胶片"){PaperPickerSection.SetCurrentValue(VisibilityProperty,Visibility.Collapsed);FrameSection.SetCurrentValue(VisibilityProperty,model?.LightPrintRecipeVisible==true?Visibility.Visible:Visibility.Collapsed);V3Editor.SetResourceFilter(r53Search,r53FavoritesOnly?"收藏":r53RecentOnly?"最近":"全部");target.Children.Add(V3Editor);return;}
        FrameSection.SetCurrentValue(VisibilityProperty,model?.LightPrintRecipeVisible==true?Visibility.Visible:Visibility.Collapsed);if(r53ResourceTab=="剧照")RenderR54Stills(target);else RenderR53PresetCards(target);
    }
    private void R53SearchChanged(object sender,TextChangedEventArgs e){r53Search=(sender as TextBox)?.Text??"";RenderR53Resources();}
    private void R53ResourceAll(object sender,RoutedEventArgs e){r53FavoritesOnly=r53RecentOnly=false;RenderR53Resources();}
    private void R53ResourceFavorites(object sender,RoutedEventArgs e){r53FavoritesOnly=true;r53RecentOnly=false;RenderR53Resources();}
    private void R53ResourceRecent(object sender,RoutedEventArgs e){r53RecentOnly=true;r53FavoritesOnly=false;RenderR53Resources();}
    private void R53ToggleScopes(object sender,RoutedEventArgs e){if(model is null)return;model.Layout=model.Layout with{R54ScopesOpen=!(model.Layout.R54ScopesOpen??false)};model.NotifyR53Edit();ApplyR53Layout();}

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
        var back=new Button{Content="返回主图"};content.Children.Add(back);
        void Close()
        {
            ownerModel.PropertyChanged-=update;R53InspectDock.Child=null;R53InspectDock.Visibility=Visibility.Collapsed;R53InspectColumn.Width=new GridLength(0);
            var window=r53Inspect;r53Inspect=null;closeR53Inspection=null;window?.Close();
        }
        closeR53Inspection=Close;back.Click+=(_,_)=>Close();
        // A 420 DIP viewport cannot fit the 264 DIP resource rail. Use recoverable
        // nonmodal focus window instead of silently squeezing or obscuring the image.
        var owner=Window.GetWindow(this);var focus=ResourceColumn.ActualWidth<420;
        r53Inspect=new Window{Title=focus?"光镜 · 专注色彩检查 · 返回主图":"光镜 · 浮动色彩检查",Owner=owner,MinWidth=460,MinHeight=440,Width=620,Height=540,Content=content,Background=new SolidColorBrush(Color.FromRgb(29,29,32)),WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.CanResizeWithGrip,ShowInTaskbar=false};
        r53Inspect.ToolTip=focus?"左资源区不足420 DIP，使用可恢复专注窗口；可拖动、缩放，返回主图关闭。":"可拖动、缩放；Esc或返回主图关闭";
        r53Inspect.Closed+=(_,_)=>{r53Inspect=null;if(closeR53Inspection is not null)Close();};
        r53Inspect.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
        r53Inspect.Show();

    }
}
