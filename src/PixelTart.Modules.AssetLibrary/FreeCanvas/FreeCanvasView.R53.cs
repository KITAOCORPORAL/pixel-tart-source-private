using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace PixelTart.Modules.AssetLibrary.FreeCanvas;
public sealed partial class FreeCanvasView
{
    private readonly Grid _inspector=new(){Width=300,Background=new SolidColorBrush(Color.FromRgb(29,29,32))};
    private readonly StackPanel _properties=new(){Margin=new Thickness(8)};
    private readonly StackPanel _layerList=new(){Margin=new Thickness(4)};
    private readonly StackPanel _brushProperties=new();
    private void InstallR53Inspector(DockPanel root)
    {
        _inspector.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star),MinHeight=160});_inspector.RowDefinitions.Add(new RowDefinition{Height=new GridLength(4)});_inspector.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star),MinHeight=180});
        _properties.Children.Add(new TextBlock{Text="属性",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});_properties.Children.Add(_brushProperties);
        var properties=new ScrollViewer{Content=_properties,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};_inspector.Children.Add(properties);
        var splitter=new GridSplitter{Height=4,HorizontalAlignment=HorizontalAlignment.Stretch,Background=new SolidColorBrush(Color.FromRgb(48,48,52))};Grid.SetRow(splitter,1);_inspector.Children.Add(splitter);
        var layers=new DockPanel();Grid.SetRow(layers,2);var header=new TextBlock{Text="图层",FontWeight=FontWeights.SemiBold,Margin=new Thickness(8)};DockPanel.SetDock(header,Dock.Top);layers.Children.Add(header);layers.Children.Add(new ScrollViewer{Content=_layerList,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});_inspector.Children.Add(layers);DockPanel.SetDock(_inspector,Dock.Right);root.Children.Add(_inspector);
        _floating.Orientation=Orientation.Vertical;
        SizeChanged+=(_,_)=>{_inspector.Width=ActualWidth<1100?280:300;_drawer.Width=ActualWidth<1100?220:264;};
    }
    private void UpdateR53Layers()
    {
        _layerList.Children.Clear();
        foreach(var item in Editor.Document.Objects.OrderByDescending(o=>o.ZIndex))
        {
            var id=item.ObjectId;var row=new DockPanel{Height=34,Background=Editor.Selected.Any(o=>o.ObjectId==id)?new SolidColorBrush(Color.FromRgb(48,68,62)):Brushes.Transparent};
            var locked=Button(item.Locked?"解锁":"锁",()=>{Editor.Select(id);Editor.SetLocked(!item.Locked);});DockPanel.SetDock(locked,Dock.Right);row.Children.Add(locked);
            var visible=Button(item.Hidden?"显示":"隐藏",()=>{Editor.Select(id);Editor.SetVisible(item.Hidden);});visible.IsEnabled=!item.Locked;DockPanel.SetDock(visible,Dock.Left);row.Children.Add(visible);
            var name=Button(item.Name,()=>{Editor.Select(id);Surface.Focus();});name.HorizontalContentAlignment=HorizontalAlignment.Left;row.Children.Add(name);_layerList.Children.Add(row);
        }
        _brushProperties.Visibility=Surface.Tool=="画笔"?Visibility.Visible:Visibility.Collapsed;
    }
    private void ShowR53BrushOptions()
    {
        _brushProperties.Children.Clear();_brushProperties.Visibility=Visibility.Visible;
        _brushProperties.Children.Add(new TextBlock{Text="画笔 · 当前绘画层",Margin=new Thickness(0,0,0,8)});
        var color=new TextBox{Text=Surface.BrushColor,ToolTip="颜色 #RRGGBB"};_brushProperties.Children.Add(color);
        color.LostKeyboardFocus+=(_,_)=>{try{_=ColorConverter.ConvertFromString(color.Text);Surface.BrushColor=color.Text;}catch(FormatException){_status.Text="请输入#RRGGBB颜色";}};
        void Slider(string name,double min,double max,double value,Action<double> set){_brushProperties.Children.Add(new TextBlock{Text=name});var slider=new Slider{Minimum=min,Maximum=max,Value=value};System.Windows.Automation.AutomationProperties.SetName(slider,name);slider.ValueChanged+=(_,_)=>set(slider.Value);_brushProperties.Children.Add(slider);}
        Slider("大小",1,300,Surface.BrushSize,v=>Surface.BrushSize=v);Slider("不透明度",0,1,Surface.BrushOpacity,v=>Surface.BrushOpacity=v);Slider("硬度",0,1,Surface.BrushHardness,v=>Surface.BrushHardness=v);
        _brushProperties.Children.Add(Button("新建绘画层",()=>{Editor.AddBrushLayer();Surface.Tool="画笔";Surface.Focus();}));
        _brushProperties.Children.Add(Button("绘制当前层",()=>{if(Editor.Selected.Count==1&&Editor.Selected[0] is {IsBrush:true,Locked:false}){Surface.Tool="画笔";Surface.Focus();}else _status.Text="请选择未锁定绘画层";}));
        _brushProperties.Children.Add(new TextBlock{Text="纹理画笔",Foreground=Brushes.Gray,ToolTip="未实现",IsEnabled=false});
    }
}
