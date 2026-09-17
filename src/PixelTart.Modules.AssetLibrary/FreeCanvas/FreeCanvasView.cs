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
    private readonly TextBlock _zoom = new() { VerticalAlignment=VerticalAlignment.Center,Margin=new(6,0,6,0) };
    private readonly TextBox _name = new() { Width=160,Margin=new(8,2,8,2) };
    private Point? _sourceDrag;
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
        _name.Text=editor.Document.Name;_name.LostKeyboardFocus+=(_,_)=>editor.Rename(_name.Text);bar.Children.Add(_name);
        foreach(var tool in new[]{"选择","移动画布","文本"})bar.Children.Add(Button(tool,()=>{Surface.Tool=tool;Surface.Focus();}));
        bar.Children.Add(Button("素材",()=>{_drawer.Visibility=_drawer.IsVisible?Visibility.Collapsed:Visibility.Visible;if(_drawer.IsVisible)_=RefreshSourcesAsync();}));
        bar.Children.Add(Button("撤销",editor.Undo));bar.Children.Add(Button("重做",editor.Redo));
        bar.Children.Add(Button("适合全部",()=>Surface.Fit()));bar.Children.Add(Button("100%",Surface.ActualSize));bar.Children.Add(Button("聚焦选择",()=>Surface.Fit(true)));bar.Children.Add(_zoom);
        void Arrange(string mode){var all=editor.Selected.Count==0;if(all)editor.SelectAll();editor.Arrange(mode);if(all)editor.Select(null);Surface.Fit();}
        bar.Children.Add(MenuButton("自动整理",new[]{("横向排列",(Action)(()=>Arrange("horizontal"))),("网格排列",()=>Arrange("grid")),("紧凑排列",()=>Arrange("compact"))}));
        bar.Children.Add(Button("整个画布存为灵感板",()=>_=SaveBoardAsync(false)));
        bar.Children.Add(Button("打开画布",()=>_=OpenSavedAsync()));
        bar.Children.Add(Button("关闭画布",()=>_=CloseAsync()));
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
        editor.Changed+=EditorChanged;Surface.ViewChanged+=(_,_)=>UpdateTools();Surface.ContextRequested+=(_,_)=>OpenContextMenu();Surface.TextEditRequested+=(_,_)=>EditText();
        _saveTimer.Tick+=async(_,_)=>{_saveTimer.Stop();await FlushAsync();};
        Loaded+=async(_,_)=>{Surface.Fit();await Surface.LoadPreviewsAsync();await RefreshSourcesAsync();await FlushAsync();UpdateTools();};
        Unloaded+=async(_,_)=>{_saveTimer.Stop();await FlushAsync();};
        PreviewKeyDown+=(_,e)=>
        {
            if(Surface.IsKeyboardFocusWithin||Keyboard.FocusedElement is TextBox or ComboBox)return;
            Surface.HandleShortcut(e);
        };
        UpdateTools();
    }
    public CanvasEditor Editor { get; }
    public FreeCanvasSurface Surface { get; }
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
        _saveTimer.Stop();await _saveGate.WaitAsync();
        try { while(!ReferenceEquals(_saved,Editor.Document)){var snapshot=Editor.Document;await _store.SaveAsync(snapshot);_saved=snapshot;}_status.Text="已保存 · 源照片不会被修改";return true; }
        catch(Exception exception)when(exception is IOException or UnauthorizedAccessException or InvalidDataException){_status.Text=$"保存失败：{exception.Message}";return false;}
        finally{_saveGate.Release();}
    }
    public async Task CloseAsync() { if(!await FlushAsync()){MessageBox.Show(Window.GetWindow(this),"画布保存失败，请检查存储位置后重试。画布将保持打开。","无法关闭画布",MessageBoxButton.OK,MessageBoxImage.Warning);return;}if(CloseRequested is not null)await CloseRequested(); }
    private async Task OpenSavedAsync()
    {
        if(!await FlushAsync())return;
        var menu=new ContextMenu();foreach(var document in await _store.ListAsync()){var item=new MenuItem{Header=document.Name};item.Click+=async(_,_)=>{if(OpenDocument is not null)await OpenDocument(document);};menu.Items.Add(item);}if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header="暂无已保存画布",IsEnabled=false});menu.PlacementTarget=this;menu.IsOpen=true;
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
    private void UpdateTools()
    {
        _zoom.Text=$"{Surface.Zoom:P0}";_floating.Children.Clear();_floatingBorder.Visibility=Editor.Selected.Count==0?Visibility.Collapsed:Visibility.Visible;
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
        else{_floating.Children.Add(Button("组合",Editor.Group));_floating.Children.Add(Button("解组",Editor.Ungroup));_floating.Children.Add(MenuButton("对齐",new[]{("左对齐",(Action)(()=>Editor.Align("left"))),("顶对齐",()=>Editor.Align("top")),("居中",()=>Editor.Align("center"))}));}
        _floating.Children.Add(MenuButton("层级",new[]{("置顶",(Action)(()=>Editor.Layer(true))),("置底",()=>Editor.Layer(false))}));
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
        if(Editor.Selected.Count==0)return;var menu=new ContextMenu();menu.SetResourceReference(StyleProperty,"PixelTart.Menu.Context");
        void Add(string title,Action action){var entry=new MenuItem{Header=title};entry.Click+=(_,_)=>action();menu.Items.Add(entry);}
        if(Editor.Selected.Count==1&&Editor.Selected[0].IsImage){var item=Editor.Selected[0];Add("查看大图",()=>{if(ViewAsset is not null)_=ViewAsset(item);});Add("在素材库中显示",()=>{if(RevealAsset is not null)_=RevealAsset(item);});}
        var boards=new MenuItem{Header="加入灵感板"};menu.Items.Add(boards);
        if(BoardLoader is not null)foreach(var board in await BoardLoader()){var entry=new MenuItem{Header=board.Name};entry.Click+=async(_,_)=>await SaveBoardAsync(true,board.Id);boards.Items.Add(entry);}
        Add("保存为灵感板",()=>_=SaveBoardAsync(true));menu.Items.Add(new Separator());
        void Submenu(string label,IEnumerable<(string,Action)> actions){var parent=new MenuItem{Header=label};foreach(var(title,action)in actions){var child=new MenuItem{Header=title};child.Click+=(_,_)=>action();parent.Items.Add(child);}menu.Items.Add(parent);}
        if(Editor.Selected.Any(item=>item.IsImage)&&AnalyzePalette is not null)
        {
            var visual=new MenuItem{Header=Editor.Selected.Count==1?"视觉分析":"比较视觉"};
            foreach(var count in new[]{3,5,7}){var entry=new MenuItem{Header=$"提取 {count} 色到画布"};entry.Click+=async(_,_)=>await AddAnalyzedPaletteAsync(count);visual.Items.Add(entry);}
            if(Editor.Selected.Count==1){var monochrome=new MenuItem{Header="创建黑白参考副本"};monochrome.Click+=(_,_)=>{var selected=Editor.Selected[0];Editor.Add([selected with{X=selected.X+32,Y=selected.Y+32,Monochrome=true,Locked=false,GroupId=null,Name=selected.Name+" · 黑白"}]);};visual.Items.Add(monochrome);}
            menu.Items.Add(visual);
        }
        Add("复制",Editor.Duplicate);if(Editor.Selected.Count==1&&Editor.Selected[0].IsImage)Add("裁切",Surface.BeginCrop);
        Submenu("旋转",RotationActions());
        Submenu("镜像",[("水平翻转",()=>Editor.Flip(true)),("垂直翻转",()=>Editor.Flip(false))]);
        Submenu("层级",[("置于顶层",()=>Editor.Layer(true)),("置于底层",()=>Editor.Layer(false))]);
        Add("组合",Editor.Group);Add("解除组合",Editor.Ungroup);Add(Editor.Selected.All(item=>item.Locked)?"解锁":"锁定",()=>Editor.SetLocked(!Editor.Selected.All(item=>item.Locked)));
        menu.Items.Add(new Separator());Add("移出画布",Editor.Remove);menu.PlacementTarget=Surface;menu.IsOpen=true;
    }
    private async Task AddAnalyzedPaletteAsync(int count)
    {
        if(AnalyzePalette is null)return;var images=Editor.Selected.Where(item=>item.IsImage).ToArray();if(images.Length==0)return;
        try{var palette=await AnalyzePalette(images,count);var bounds=Editor.Bounds();Editor.AddPalette(bounds.X+bounds.Width+32,bounds.Y,palette);_status.Text="配色已作为可编辑对象加入画布";}
        catch(Exception exception)when(exception is IOException or InvalidOperationException or NotSupportedException){_status.Text=$"视觉分析暂不可用：{exception.Message}";}
    }
    public static Button Button(string label,Action action){var b=new Button{Content=label,Padding=new(8,5,8,5),Margin=new(2)};b.SetResourceReference(StyleProperty,"PixelTart.Button.Ghost");b.Click+=(_,_)=>action();return b;}
    private static Button MenuButton(string label,IEnumerable<(string,Action)> actions){var b=Button(label,()=>{});b.Click+=(_,_)=>{var menu=new ContextMenu{PlacementTarget=b};foreach(var(title,action)in actions){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}menu.IsOpen=true;};return b;}
}
