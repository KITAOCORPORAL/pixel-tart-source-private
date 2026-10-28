using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace PixelTart.Modules.AssetLibrary.FreeCanvas;

public sealed class FreeCanvasView : UserControl
{
    private readonly CanvasDocumentStore _store;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private readonly SemaphoreSlim _saveGate = new(1,1);
    private CanvasDocument? _saved, _observed;
    private readonly TextBlock _status = new() { Margin = new(10,5,10,5) };
    private readonly StackPanel _floating = new() { Orientation=Orientation.Horizontal };
    private readonly Border _floatingBorder = new() { Padding=new(4),CornerRadius=new(6),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top };
    private readonly Border _drawer = new() { Width=245,Padding=new(8),Visibility=Visibility.Collapsed };
    private readonly ListBox _sources = new() { SelectionMode=SelectionMode.Extended };
    private readonly ComboBox _sourceKind = new() { ItemsSource=new[] { "素材库","灵感板","临时收集","当前项目","最近使用" },SelectedIndex=0,Margin=new(0,6,0,6) };
    private readonly TextBox _sourceSearch = new() { ToolTip="搜索素材",Margin=new(0,4,0,6) };
    private readonly Button _zoomButton = new() { Padding=new(8,5,8,5),Margin=new(2) };
    private readonly TextBox _name = new() { Width=160,Margin=new(8,2,8,2) };
    private Point? _sourceDrag;
    private bool _discardPending;
    private readonly Button _undoButton, _redoButton;
    private readonly Button _moreButton;
    public Panel ProjectPanel { get; private set; } = null!;
    public FreeCanvasView(CanvasEditor editor, IAssetPreviewProvider provider, CanvasDocumentStore store)
    {
        Editor=editor;_store=store;_observed=editor.Document;Surface=new(editor,provider);
        if(provider is IAssetThumbnailProvider thumbnails)AsyncThumbnail.SetScopedProvider(this,thumbnails);
        _sources.ItemTemplate=(DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:local="clr-namespace:PixelTart.Modules.AssetLibrary;assembly=PixelTart.Modules.AssetLibrary">
              <StackPanel Orientation="Horizontal" Margin="2,4">
                <Image Width="56" Height="50" Stretch="Uniform" local:AsyncThumbnail.SourcePath="{Binding SourcePath}" local:AsyncThumbnail.AssetId="{Binding AssetId}" local:AsyncThumbnail.ContentHash="{Binding ContentHash}" local:AsyncThumbnail.DecodeWidth="180" />
                <TextBlock Text="{Binding Name}" Width="130" Margin="8,0,0,0" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" ToolTip="{Binding Name}" />
              </StackPanel>
            </DataTemplate>
            """);
        SetResourceReference(BackgroundProperty,"Brush.Background");
        var root=new DockPanel();Content=root;
        var bar=new WrapPanel { Margin=new(8,6,8,6) };DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);
        bar.Children.Add(Button("导入图片",()=>_=ImportFilesAsync()));bar.Children.Add(Button("导出透明PNG",ExportTransparent));bar.Children.Add(Button("画布尺寸",CanvasSizeDialog));bar.Children.Add(Button("图层",ShowLayers));bar.Children.Add(Button("画笔",BrushOptions));
        _name.Text=editor.Document.Name;_name.LostKeyboardFocus+=(_,_)=>editor.Rename(_name.Text);bar.Children.Add(_name);
        // Primary actions stay in one compact row; secondary actions live in contextual menus.
        bar.Children.Add(Button("选择",()=>{Surface.Tool="选择";Surface.Focus();}));
        bar.Children.Add(Button("平移",()=>{Surface.Tool="移动画布";Surface.Focus();}));
        _undoButton=Button("撤销",editor.Undo); _redoButton=Button("重做",editor.Redo);
        bar.Children.Add(_undoButton); bar.Children.Add(_redoButton);
        bar.Children.Add(Button("Fit",()=>Surface.Fit()));
        bar.Children.Add(MenuButton("工具",new[]{("文字",(Action)(()=>{Surface.Tool="文本";Surface.Focus();})),
            ("素材",()=>{_drawer.Visibility=_drawer.IsVisible?Visibility.Collapsed:Visibility.Visible;if(_drawer.IsVisible)_=RefreshSourcesAsync();})}));
        var edit=Button("编辑",()=>{}); edit.Click+=(_,_)=>OpenEditMenu(edit); bar.Children.Add(edit);
        _zoomButton.SetResourceReference(StyleProperty,"PixelTart.Button.Ghost");
        _zoomButton.Click+=(_,_)=>OpenZoomMenu(); bar.Children.Add(_zoomButton);
        var arrange=Button("排列",()=>{}); arrange.Click+=(_,_)=>OpenArrangeMenu(arrange); bar.Children.Add(arrange);
        _moreButton=Button("画布",OpenMoreMenu); bar.Children.Add(_moreButton);
        ProjectPanel=new StackPanel { Margin=new Thickness(8) };
        var project=Button("项目",()=>{}); project.Click+=(_,_)=>
        {
            if(ProjectPanel.Parent is MenuItem previous) previous.Header=null;
            var menu=new ContextMenu { Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom, PlacementTarget=project };
            menu.Items.Add(new MenuItem { Header=ProjectPanel, StaysOpenOnClick=true });
            menu.IsOpen=true;
        }; bar.Children.Add(project);
        DockPanel.SetDock(_status,Dock.Bottom);root.Children.Add(_status);
        _drawer.SetResourceReference(BackgroundProperty,"Brush.Panel");DockPanel.SetDock(_drawer,Dock.Left);root.Children.Add(_drawer);
        var drawerPanel=new DockPanel();_drawer.Child=drawerPanel;
        var sourceHeader=new StackPanel();sourceHeader.Children.Add(new TextBlock{Text="素材",FontWeight=FontWeights.SemiBold});sourceHeader.Children.Add(_sourceKind);sourceHeader.Children.Add(_sourceSearch);sourceHeader.Children.Add(Button("加入所选素材",()=>AddSources()));DockPanel.SetDock(sourceHeader,Dock.Top);drawerPanel.Children.Add(sourceHeader);drawerPanel.Children.Add(_sources);
        _sourceKind.SelectionChanged+=(_,_)=>_=RefreshSourcesAsync();_sourceSearch.TextChanged+=(_,_)=>_=RefreshSourcesAsync();
        _sources.PreviewMouseLeftButtonDown+=(_,e)=>_sourceDrag=e.GetPosition(_sources);
        _sources.PreviewMouseMove+=(_,e)=>{if(e.LeftButton!=MouseButtonState.Pressed||_sourceDrag is not Point start||(e.GetPosition(_sources)-start).Length<8)return;_sourceDrag=null;var values=_sources.SelectedItems.Cast<CanvasObject>().ToArray();if(values.Length>0)DragDrop.DoDragDrop(_sources,new DataObject("PixelTart.CanvasSources",values),DragDropEffects.Copy);};
        _sources.MouseDoubleClick+=(_,_)=>AddSources();
        Surface.Drop+=(_,e)=>{if(e.Data.GetData("PixelTart.CanvasSources") is CanvasObject[] objects){var point=Surface.ScreenToWorld(e.GetPosition(Surface));editor.Add(objects.Select((item,index)=>item with{X=point.X+index*24,Y=point.Y+index*24}));e.Handled=true;}};
        var stage=new Grid();root.Children.Add(stage);stage.Children.Add(Surface);
        _floatingBorder.SetResourceReference(BackgroundProperty,"Brush.Surface.Elevated");_floatingBorder.SetResourceReference(BorderBrushProperty,"Brush.Border");_floatingBorder.BorderThickness=new(1);_floatingBorder.Child=_floating;stage.Children.Add(_floatingBorder);
        editor.Changed+=EditorChanged;Surface.ViewChanged+=(_,_)=>{if(Surface.Tool=="画笔")_status.Text=Surface.BrushStatus;UpdateTools();};Surface.ContextRequested+=(_,_)=>OpenContextMenu();Surface.TextEditRequested+=(_,_)=>EditText();
        _saveTimer.Tick+=async(_,_)=>{_saveTimer.Stop();await FlushAsync();};
        Loaded+=async(_,_)=>{Surface.Fit();await Surface.LoadPreviewsAsync();await RefreshSourcesAsync();await FlushAsync();UpdateTools();};
        Unloaded+=async(_,_)=>{_saveTimer.Stop();if(!_discardPending)await FlushAsync();};
        PreviewKeyDown+=(_,e)=>
        {
            if(Surface.IsKeyboardFocusWithin||Keyboard.FocusedElement is TextBox or ComboBox)return;
            Surface.HandleShortcut(e);
        };
        UpdateTools();
    }
    public CanvasEditor Editor { get; }
    public FreeCanvasSurface Surface { get; }
    public Func<CanvasObject,Task>? RelocateAsset { get; set; }
    public Func<string,string,Task<IReadOnlyList<CanvasObject>>>? SourceLoader { get; set; }
    public Func<IReadOnlyList<CanvasObject>,Guid?,Task>? SaveBoard { get; set; }
    public Func<Task<IReadOnlyList<(Guid Id,string Name)>>>? BoardLoader { get; set; }
    public Func<CanvasObject,Task>? ViewAsset { get; set; }
    public Func<CanvasObject,Task>? RevealAsset { get; set; }
    public Func<IReadOnlyList<CanvasObject>,int,Task<CanvasPalette>>? AnalyzePalette { get; set; }
    public Func<Task>? CloseRequested { get; set; }
    public Func<CanvasDocument,Task>? OpenDocument { get; set; }
    public Panel HeaderPanel => (Panel)((DockPanel)Content).Children[0];
    public void ShowSources(string source) { _drawer.Visibility=Visibility.Visible;_sourceKind.SelectedItem=source;_=RefreshSourcesAsync(); }
    private void EditorChanged(object? sender,EventArgs args)
    {
        UpdateTools();
        if(ReferenceEquals(_observed,Editor.Document))return;
        _observed=Editor.Document;_status.Text="正在保存…";_saveTimer.Stop();_saveTimer.Start();
    }
    public async Task<bool> FlushAsync()
    {
        if(_discardPending)return true;
        _saveTimer.Stop();await _saveGate.WaitAsync();
        try { while(!ReferenceEquals(_saved,Editor.Document)){var snapshot=Editor.Document;await _store.SaveAsync(snapshot);_saved=snapshot;}_status.Text="已保存";return true; }
        catch(Exception exception)when(exception is IOException or UnauthorizedAccessException or InvalidDataException){new RAWSelectionAssistant.Core.Services.FileLogService().Error("画布保存失败",exception);_status.Text="保存失败，请重试";return false;}
        finally{_saveGate.Release();}
    }
    public Task<bool> PrepareDocumentChangeAsync() => PrepareDocumentChangeAsync(() => MessageBox.Show(Window.GetWindow(this),
        "保存当前画布的未保存修改？\n是：保存；否：放弃；取消：继续编辑。", "未保存的画布", MessageBoxButton.YesNoCancel, MessageBoxImage.Question));
    internal async Task<bool> PrepareDocumentChangeAsync(Func<MessageBoxResult> prompt)
    {
        _saveTimer.Stop();
        await _saveGate.WaitAsync(); _saveGate.Release();
        if(_discardPending || ReferenceEquals(_saved,Editor.Document))return true;
        var choice=prompt();
        if(choice==MessageBoxResult.Cancel){_saveTimer.Start();return false;}
        if(choice==MessageBoxResult.Yes)return await FlushAsync();
        _discardPending=true;return true;
    }
    public async Task CloseAsync() { if(await PrepareDocumentChangeAsync() && CloseRequested is not null)await CloseRequested(); }
    private void OpenZoomMenu()
    {
        var menu=new ContextMenu { Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom, PlacementTarget=_zoomButton };
        void Add(string header, Action action){var item=new MenuItem{Header=header};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        foreach(var zoom in new[]{.5,.75,1d,1.25,1.5,2d}){var value=zoom;Add($"{value:P0}",()=>Surface.SetZoom(value));} menu.IsOpen=true;
    }
    public async Task ImportFilesAsync(IReadOnlyList<string>? paths=null)
    {
        if(paths is null){var dialog=new Microsoft.Win32.OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.tif;*.tiff",Multiselect=true};if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;paths=dialog.FileNames;}
        try{var objects=new List<CanvasObject>();foreach(var path in paths){var item=await CanvasPixelRenderer.ImportAsync(path,Editor.Document,_store.DirectoryPath);objects.Add(item with{X=32+objects.Count*24,Y=32+objects.Count*24});}Editor.Add(objects);await Surface.LoadPreviewsAsync();_status.Text="已导入画布自有副本，原图未修改";}catch(Exception error)when(error is IOException or InvalidDataException or NotSupportedException or ArgumentException or UnauthorizedAccessException){_status.Text="导入未完成："+error.Message;}
    }
    public void ExportTransparentTo(string path){CanvasPixelRenderer.ExportPng(Editor.Document,path);_status.Text="透明PNG已导出 · sRGB";}
    private async void ExportTransparent(){var dialog=new Microsoft.Win32.SaveFileDialog{Filter="透明PNG|*.png",FileName=Editor.Document.Name+".png"};if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;try{if(!await FlushAsync())return;ExportTransparentTo(dialog.FileName);}catch(Exception error)when(error is IOException or InvalidDataException or NotSupportedException or ArgumentException){_status.Text="导出未完成："+error.Message;}}
    private void CanvasSizeDialog(){var panel=new StackPanel{Margin=new Thickness(16)};var width=new TextBox{Text=Editor.Document.PixelWidth.ToString()};var height=new TextBox{Text=Editor.Document.PixelHeight.ToString()};panel.Children.Add(new TextBlock{Text="导出宽度 px"});panel.Children.Add(width);panel.Children.Add(new TextBlock{Text="导出高度 px"});panel.Children.Add(height);var window=new Window{Title="画布尺寸",Width=280,Height=220,Content=panel,Owner=Window.GetWindow(this)};panel.Children.Add(Button("应用",()=>{if(int.TryParse(width.Text,out var w)&&int.TryParse(height.Text,out var h)){try{Editor.SetCanvasSize(w,h);window.Close();}catch(InvalidDataException error){_status.Text=error.Message;}}}));window.ShowDialog();}
    private void ShowLayers(){var list=new StackPanel();var window=new Window{Title="图层 · 上方在前",Width=600,Height=480,Content=new ScrollViewer{Content=list},Owner=Window.GetWindow(this)};void Build(){list.Children.Clear();foreach(var item in Editor.Document.Objects.OrderByDescending(o=>o.ZIndex)){var id=item.ObjectId;var row=new WrapPanel();row.Children.Add(Button(item.Name,()=>{Editor.Select(id);Surface.Focus();}));var visibility=Button(item.Hidden?"显示":"隐藏",()=>{Editor.Select(id);Editor.SetVisible(Editor.Document.Objects.Single(o=>o.ObjectId==id).Hidden);Build();});visibility.IsEnabled=!item.Locked;row.Children.Add(visibility);row.Children.Add(Button(item.Locked?"解锁":"锁定",()=>{Editor.Select(id);Editor.SetLocked(!Editor.Document.Objects.Single(o=>o.ObjectId==id).Locked);Build();}));row.Children.Add(Button("↑",()=>{Editor.Select(id);Editor.StepLayer(true);Build();}));row.Children.Add(Button("↓",()=>{Editor.Select(id);Editor.StepLayer(false);Build();}));var opacity=new Slider{Width=80,Minimum=0,Maximum=1,Value=item.Opacity??1,IsEnabled=!item.Locked,ToolTip="图层不透明度"};opacity.PreviewMouseLeftButtonDown+=(_,_)=>{Editor.Select(id);Editor.BeginGesture();};opacity.PreviewMouseLeftButtonUp+=(_,_)=>Editor.EndGesture();opacity.LostMouseCapture+=(_,_)=>Editor.EndGesture();opacity.ValueChanged+=(_,_)=>{Editor.Select(id);Editor.SetOpacity(opacity.Value);};row.Children.Add(opacity);list.Children.Add(row);}}Build();window.Show();}
    private void BrushOptions(){var panel=new StackPanel{Margin=new Thickness(14)};var color=new TextBox{Text=Surface.BrushColor};panel.Children.Add(new TextBlock{Text="颜色 #RRGGBB"});panel.Children.Add(color);void Slider(string label,double min,double max,double value,Action<double> set){panel.Children.Add(new TextBlock{Text=label});var control=new Slider{Minimum=min,Maximum=max,Value=value};control.ValueChanged+=(_,_)=>set(control.Value);panel.Children.Add(control);}Slider("大小（文档像素）",1,300,Surface.BrushSize,v=>Surface.BrushSize=v);Slider("不透明度",0,1,Surface.BrushOpacity,v=>Surface.BrushOpacity=v);Slider("硬度",0,1,Surface.BrushHardness,v=>Surface.BrushHardness=v);var window=new Window{Title="普通画笔",Width=300,Height=340,Content=panel,Owner=Window.GetWindow(this)};panel.Children.Add(Button("新建画笔层并绘制",()=>{try{_=ColorConverter.ConvertFromString(color.Text);Surface.BrushColor=color.Text;Editor.AddBrushLayer();Surface.Tool="画笔";Surface.Focus();window.Close();}catch(FormatException){_status.Text="请输入#RRGGBB颜色";}}));panel.Children.Add(Button("绘制当前画笔层",()=>{if(Editor.Selected.Count==1&&Editor.Selected[0] is{IsBrush:true,Locked:false}){try{_=ColorConverter.ConvertFromString(color.Text);}catch(FormatException){_status.Text="请输入#RRGGBB颜色";return;}Surface.BrushColor=color.Text;Surface.Tool="画笔";Surface.Focus();window.Close();}else _status.Text="请选择未锁定画笔层";}));window.ShowDialog();}
    private void OpenMoreMenu()
    {
        var anchor=_moreButton; var menu=new ContextMenu{Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom, PlacementTarget=anchor};
        void Add(string header,Action action){var item=new MenuItem{Header=header};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Add("查看预览问题详情",()=>MessageBox.Show(Window.GetWindow(this),Surface.SourceErrors.Count==0?"没有预览问题。":string.Join("\n\n",Surface.SourceErrors.Select(x=>x.Key+"\n"+x.Value)),"预览详情",MessageBoxButton.OK,MessageBoxImage.Information));
        Add("重新定位选中素材",async ()=>{if(Editor.Selected.FirstOrDefault(x=>x.IsImage) is not{} item)return;if(item.AssetId is not null&&RelocateAsset is not null)await RelocateAsset(item);else{var picker=new Microsoft.Win32.OpenFileDialog{Filter="原素材副本|*.png;*.jpg;*.jpeg;*.tif;*.tiff"};if(picker.ShowDialog(Window.GetWindow(this))!=true)return;try{var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(picker.FileName)));if(!hash.Equals(item.ContentHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("文件哈希与原素材不一致，未替换。");var copy=await RAWSelectionAssistant.Core.Services.Projects.PlanningProjectCopyStorage.ImportCanvasAsync(_store.DirectoryPath,Editor.Document.CanvasId,Guid.NewGuid(),picker.FileName);Editor.ReplaceSource(item with{PlanningSource=CanvasPlanningSource.From(copy),SourcePath=""});}catch(Exception error)when(error is IOException or InvalidDataException){_status.Text="恢复未完成："+error.Message;}}await Surface.LoadPreviewsAsync();});
        Add("新建画布",async ()=>{if(await PrepareDocumentChangeAsync() && OpenDocument is not null)await OpenDocument(new CanvasDocument());});
        Add("打开画布…",()=>_=OpenSavedAsync()); Add("关闭画布",()=>_=CloseAsync());
        AddMenuAction(menu,"保存到灵感板",()=>_=SaveBoardAsync(false),SaveBoard is not null && Editor.Document.Objects.Any(item=>item.IsImage)); menu.IsOpen=true;
    }
    private void OpenEditMenu(Button anchor)
    {
        var menu=new ContextMenu { Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom, PlacementTarget=anchor };
        // Expose the current menu through its owner; an old closing Popup HWND can
        // remain in PresentationSource.CurrentSources until the dispatcher drains.
        if(anchor.ContextMenu is { } previous)previous.IsOpen=false;
        anchor.ContextMenu=menu;
        AddMenuAction(menu,"复制",Editor.Copy,Editor.Selected.Count>0,"Ctrl+C");
        AddMenuAction(menu,"粘贴",Editor.Paste,Editor.CanPaste,"Ctrl+V");
        AddMenuAction(menu,"创建副本",Editor.Duplicate,Editor.Selected.Count>0,"Ctrl+D");
        AddMenuAction(menu,"组合",Editor.Group,Editor.Selected.Count>1,"Ctrl+G");
        AddMenuAction(menu,"解除组合",Editor.Ungroup,Editor.Selected.Any(x=>x.GroupId is not null),"Ctrl+Shift+G");
        AddMenuAction(menu,"移出画布",Editor.Remove,Editor.Selected.Count>0,"Del"); menu.IsOpen=true;
    }
    private void OpenArrangeMenu(Button anchor)
    {
        var menu=new ContextMenu { Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom, PlacementTarget=anchor }; var selected=Editor.Selected.Count>0;
        AddMenuAction(menu,"前移",()=>Editor.StepLayer(true),selected); AddMenuAction(menu,"后移",()=>Editor.StepLayer(false),selected);
        AddMenuAction(menu,"置顶",()=>Editor.Layer(true),selected); AddMenuAction(menu,"置底",()=>Editor.Layer(false),selected);
        var alignment=new MenuItem { Header="对齐",IsEnabled=Editor.Selected.Count>1 };
        foreach(var (label,mode) in new[]{("左对齐","left"),("顶对齐","top"),("居中","center")})
        {var item=new MenuItem { Header=label };item.Click+=(_,_)=>Editor.Align(mode);alignment.Items.Add(item);}
        menu.Items.Add(alignment);
        var automatic=new MenuItem { Header="自动整理",IsEnabled=Editor.Document.Objects.Count>0 };
        foreach(var (label,mode) in new[]{("横向","horizontal"),("网格","grid"),("紧凑","compact")})
        {var item=new MenuItem { Header=label }; item.Click+=(_,_)=>{var all=Editor.Selected.Count==0;if(all)Editor.SelectAll();Editor.Arrange(mode);if(all)Editor.Select(null);Surface.Fit();};automatic.Items.Add(item);}
        menu.Items.Add(automatic);foreach(var item in menu.Items.OfType<MenuItem>())AssetLibraryPage.AttachContextSubmenuPlacement(item);menu.IsOpen=true;
    }
    private static void AddMenuAction(ContextMenu menu,string label,Action action,bool enabled,string gesture="")
    {var item=new MenuItem { Header=label,IsEnabled=enabled,InputGestureText=gesture };item.Click+=(_,_)=>action();menu.Items.Add(item);}
    private async Task OpenSavedAsync()
    {
        var menu=new ContextMenu();foreach(var document in await _store.ListAsync()){var item=new MenuItem{Header=document.Name};item.Click+=async(_,_)=>{if(await PrepareDocumentChangeAsync() && OpenDocument is not null)await OpenDocument(await _store.LoadAsync(document.CanvasId) ?? document);};menu.Items.Add(item);}if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header="暂无已保存画布",IsEnabled=false});menu.PlacementTarget=this;menu.IsOpen=true;
    }
    private async Task RefreshSourcesAsync()
    {
        if(SourceLoader is null)return;var source=_sourceKind.SelectedItem?.ToString()??"素材库";var search=_sourceSearch.Text;
        try{var items=await SourceLoader(source,search);if(source==_sourceKind.SelectedItem?.ToString()&&search==_sourceSearch.Text)_sources.ItemsSource=items;}
        catch(Exception exception)when(exception is IOException or InvalidOperationException){_status.Text=$"素材暂不可用：{exception.Message}";}
    }
    private void AddSources(){var center=Surface.ScreenToWorld(new(Surface.ActualWidth/2,Surface.ActualHeight/2));Editor.Add(_sources.SelectedItems.Cast<CanvasObject>().Select((item,index)=>item with{X=center.X+index*24,Y=center.Y+index*24}));Surface.Focus();}
    private async Task SaveBoardAsync(bool selection,Guid? board=null)
    {
        var objects=(selection?Editor.Selected:Editor.Document.Objects).Where(item=>item.IsImage).ToArray();
        if(objects.Length==0||SaveBoard is null)return;
        try{await SaveBoard(objects,board);_status.Text="已加入灵感板 · 保留素材引用";}catch(Exception exception)when(exception is IOException or InvalidOperationException or ArgumentException){_status.Text=$"未能加入灵感板：{exception.Message}";}
    }
    private bool opacityDragging;
    private void UpdateTools()
    {
        if(opacityDragging)return;
        _undoButton.IsEnabled=Editor.CanUndo; _redoButton.IsEnabled=Editor.CanRedo;
        _zoomButton.Content=$"视图 · {Surface.Zoom:P0} ▾";_floating.Children.Clear();_floatingBorder.Visibility=Editor.Selected.Count==0?Visibility.Collapsed:Visibility.Visible;
        if(Editor.Selected.Count==0)return;
        var bounds=Editor.Bounds();var top=Surface.WorldToScreen(new(bounds.X,bounds.Y));_floatingBorder.Margin=new(Math.Clamp(top.X,8,Math.Max(8,Surface.ActualWidth-580)),Math.Clamp(top.Y-48,8,Math.Max(8,Surface.ActualHeight-44)),0,0);
        if(Surface.CropMode)
        {
            foreach(var (label,ratio) in new (string,double?)[]{("自由",null),("原始比例",Editor.Selected[0].SourceWidth/Math.Max(1,Editor.Selected[0].SourceHeight)),("1:1",1),("4:5",.8),("3:2",1.5),("16:9",16d/9)})_floating.Children.Add(Button(label,()=>{Surface.PendingCrop=CanvasEditor.CropForAspect(Editor.Selected[0],ratio);Surface.InvalidateVisual();Surface.Focus();}));
            _floating.Children.Add(Button("应用",()=>Surface.FinishCrop(true)));_floating.Children.Add(Button("取消",()=>Surface.FinishCrop(false)));return;
        }
        if(Editor.Selected.Count==1)
        {
            if(Editor.Selected[0].IsText)_floating.Children.Add(Button("编辑文字",EditText));else if(Editor.Selected[0].IsImage)_floating.Children.Add(Button("裁切",Surface.BeginCrop));
            _floating.Children.Add(MenuButton("旋转",RotationActions()));_floating.Children.Add(MenuButton("镜像",new[]{("水平翻转",(Action)(()=>Editor.Flip(true))),("垂直翻转",()=>Editor.Flip(false))}));
        }


        var opacity=new Slider{Minimum=0,Maximum=1,Value=Editor.Selected.FirstOrDefault()?.Opacity??1,Width=75,IsEnabled=Editor.Selected.All(x=>!x.Locked),ToolTip="对象透明度"};opacity.PreviewMouseLeftButtonDown+=(_,_)=>{opacityDragging=true;Editor.BeginGesture();};opacity.PreviewMouseLeftButtonUp+=(_,_)=>{Editor.EndGesture();opacityDragging=false;UpdateTools();};opacity.LostMouseCapture+=(_,_)=>{Editor.EndGesture();opacityDragging=false;UpdateTools();};opacity.ValueChanged+=(_,_)=>Editor.SetOpacity(opacity.Value);_floating.Children.Add(opacity);
        _floating.Children.Add(Button(Editor.Selected.All(x=>x.Hidden)?"显示":"隐藏",()=>Editor.SetVisible(Editor.Selected.All(x=>x.Hidden))));
        _floating.Children.Add(Button(Editor.Selected.All(item=>item.Locked)?"解锁":"锁定",()=>Editor.SetLocked(!Editor.Selected.All(item=>item.Locked))));
        if(Editor.Selected.Count>1)_floating.Children.Add(Button("移出画布",Editor.Remove));
        _floating.Children.Add(Button("更多",OpenContextMenu));
    }
    private (string,Action)[] RotationActions()=>[("左转 90°",()=>Editor.Rotate(-90)),("右转 90°",()=>Editor.Rotate(90)),("旋转 180°",()=>Editor.Rotate(180)),("旋转 15°",()=>Editor.Rotate(15))];
    private void EditText()
    {
        if(Editor.Selected.FirstOrDefault() is not {IsText:true} item||item.Locked)return;
        var content=new TextBox{Text=item.Text,AcceptsReturn=true,MinHeight=80,TextWrapping=TextWrapping.Wrap};var size=new TextBox{Text=item.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture),Margin=new(0,6,0,6)};var color=new TextBox{Text=item.TextColor};
        var panel=new StackPanel{Margin=new(16)};panel.Children.Add(new TextBlock{Text="文字内容"});panel.Children.Add(content);panel.Children.Add(new TextBlock{Text="字号"});panel.Children.Add(size);panel.Children.Add(new TextBlock{Text="颜色 #RRGGBB"});panel.Children.Add(color);
        var window=new Window{Title="编辑文字",Width=380,Height=350,Content=panel,Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner};
        panel.Children.Add(Button("应用",()=>{if(!double.TryParse(size.Text,out var font))return;try{_=ColorConverter.ConvertFromString(color.Text);}catch{return;}Editor.EditText(content.Text??"",font,color.Text);window.Close();}));window.ShowDialog();Surface.Focus();
    }
    private async void OpenContextMenu()
    {
        var menu = await CreateSelectionMenuAsync();
        if (menu.Items.Count == 0) return;
        menu.PlacementTarget = Surface;
        foreach (var item in menu.Items.OfType<MenuItem>()) AssetLibraryPage.AttachContextSubmenuPlacement(item);
        menu.IsOpen = true;
    }
    internal async Task<ContextMenu> CreateSelectionMenuAsync()
    {
        var menu=new ContextMenu();menu.SetResourceReference(StyleProperty,"PixelTart.Menu.Context");
        if(Editor.Selected.Count==0)return menu;
        void Add(string title,Action action){var entry=new MenuItem{Header=title};entry.Click+=(_,_)=>action();menu.Items.Add(entry);}
        if(Editor.Selected.Count==1&&Editor.Selected[0].IsImage){var item=Editor.Selected[0];if(ViewAsset is not null)Add("查看大图",()=>_=ViewAsset(item));if(RevealAsset is not null)Add("在素材库中显示",()=>_=RevealAsset(item));}
        if (SaveBoard is not null && Editor.Selected.Any(item => item.IsImage))
        {
            var boards=new MenuItem{Header="灵感板"};
            if(BoardLoader is not null)
                foreach(var board in await BoardLoader()){var entry=new MenuItem{Header=board.Name};entry.Click+=async(_,_)=>await SaveBoardAsync(true,board.Id);boards.Items.Add(entry);}
            var save=new MenuItem{Header="保存为新灵感板"};save.Click+=(_,_)=>_=SaveBoardAsync(true);boards.Items.Add(save);menu.Items.Add(boards);
        }
        if(menu.Items.Count>0)menu.Items.Add(new Separator());
        void Submenu(string label,IEnumerable<(string,Action)> actions){var parent=new MenuItem{Header=label};foreach(var(title,action)in actions){var child=new MenuItem{Header=title};child.Click+=(_,_)=>action();parent.Items.Add(child);}menu.Items.Add(parent);}
        if(Editor.Selected.Any(item=>item.IsImage)&&AnalyzePalette is not null)
        {
            var visual=new MenuItem{Header=Editor.Selected.Count==1?"视觉分析":"比较视觉"};
            foreach(var count in new[]{3,5,7}){var entry=new MenuItem{Header=$"提取 {count} 色到画布"};entry.Click+=async(_,_)=>await AddAnalyzedPaletteAsync(count);visual.Items.Add(entry);}
            if(Editor.Selected.Count==1){var monochrome=new MenuItem{Header="创建黑白参考副本"};monochrome.Click+=(_,_)=>{var selected=Editor.Selected[0];Editor.Add([selected with{X=selected.X+32,Y=selected.Y+32,Monochrome=true,Locked=false,GroupId=null,Name=selected.Name+" · 黑白"}]);};visual.Items.Add(monochrome);}
            menu.Items.Add(visual);
        }
        // Use the same clipboard and duplicate commands as the editing toolbar.
        AddMenuAction(menu,"复制",Editor.Copy,true,"Ctrl+C");
        AddMenuAction(menu,"粘贴",Editor.Paste,Editor.CanPaste,"Ctrl+V");
        AddMenuAction(menu,"创建副本",Editor.Duplicate,true,"Ctrl+D");
        if(Editor.Selected.Count==1&&Editor.Selected[0].IsImage&&!Editor.Selected[0].Locked)Add("裁切",Surface.BeginCrop);
        Submenu("变换",RotationActions().Concat(new (string,Action)[]{("水平翻转",()=>Editor.Flip(true)),("垂直翻转",()=>Editor.Flip(false))}));
        Submenu("排列",[("前移",()=>Editor.StepLayer(true)),("后移",()=>Editor.StepLayer(false)),("置于顶层",()=>Editor.Layer(true)),("置于底层",()=>Editor.Layer(false))]);
        AddMenuAction(menu,"组合",Editor.Group,Editor.Selected.Count>1,"Ctrl+G");
        AddMenuAction(menu,"解除组合",Editor.Ungroup,Editor.Selected.Any(item=>item.GroupId is not null),"Ctrl+Shift+G");
        Add(Editor.Selected.All(item=>item.Locked)?"解锁":"锁定",()=>Editor.SetLocked(!Editor.Selected.All(item=>item.Locked)));
        menu.Items.Add(new Separator());AddMenuAction(menu,"移出画布",Editor.Remove,true,"Del");
        return menu;
    }
    private async Task AddAnalyzedPaletteAsync(int count)
    {
        if(AnalyzePalette is null)return;var images=Editor.Selected.Where(item=>item.IsImage).ToArray();if(images.Length==0)return;
        try{var palette=await AnalyzePalette(images,count);var bounds=Editor.Bounds();Editor.AddPalette(bounds.X+bounds.Width+32,bounds.Y,palette);_status.Text="配色已作为可编辑对象加入画布";}
        catch(Exception exception)when(exception is IOException or InvalidOperationException or NotSupportedException){_status.Text=$"视觉分析暂不可用：{exception.Message}";}
    }
    public static Button Button(string label,Action action){var b=new Button{Content=label,Padding=new(8,5,8,5),Margin=new(2)};b.SetResourceReference(StyleProperty,"PixelTart.Button.Ghost");b.Click+=(_,_)=>action();return b;}
    private static Button MenuButton(string label,IEnumerable<(string,Action)> actions){var b=Button(label,()=>{});b.Click+=(_,_)=>{var menu=new ContextMenu{Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom, PlacementTarget=b};foreach(var(title,action)in actions){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}menu.IsOpen=true;};return b;}
}
