using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;

/// <summary>Production graph surface; edges are the persisted renderer route, never decorative list connectors.</summary>
public sealed class StudioNodeGraphView:FrameworkElement
{
    private readonly TetherReferenceModeViewModel editor;
    private Point pan=new(8,12),start,initial,screen;private double zoom=1;
    private Guid? moving,wire;private bool panning,moved,wireFromOutput;private TextBox? rename;
    private StudioNodeGraph before=null!;
    public event Action? LayoutChanged;
    public Func<Guid,ImageSource?>? Thumbnail{get;set;}
    public Func<Guid,ImageSource?>? MaskThumbnail{get;set;}
    public double Zoom=>zoom;public Point Pan=>pan;
    public StudioNodeGraphView(TetherReferenceModeViewModel value)
    {
        editor=value;Focusable=true;ClipToBounds=true;MinHeight=24;
        ToolTip="拖动空白平移 · 滚轮缩放 · 端口拖线 · 拖到连线上串行插入 · F2重命名";
        System.ComponentModel.PropertyChangedEventManager.AddHandler(editor,(_,e)=>{if(e.PropertyName is nameof(editor.AdjustmentStack) or nameof(editor.SelectedAdjustmentNode))InvalidateVisual();},string.Empty);
        MouseDown+=Down;MouseMove+=Move;MouseUp+=Up;MouseWheel+=Wheel;KeyDown+=Keys;
        LostMouseCapture+=(_,_)=>{moving=null;wire=null;panning=false;InvalidateVisual();};
    }
    public void Restore(Point p,double z){pan=p;zoom=Math.Clamp(z,.25,2);InvalidateVisual();}
    public void Fit()
    {
        var ps=editor.GraphView.Positions;if(ps.Count==0)return;
        var l=ps.Min(p=>p.X)-20;var t=ps.Min(p=>p.Y)-20;var w=ps.Max(p=>p.X)+140-l;var h=ps.Max(p=>p.Y)+84-t;
        zoom=Math.Clamp(Math.Min(Math.Max(1,ActualWidth)/w,Math.Max(1,ActualHeight)/h),.25,1.4);pan=new(-l*zoom,-t*zoom);LayoutChanged?.Invoke();InvalidateVisual();
    }
    private Point World(Point p)=>new((p.X-pan.X)/zoom,(p.Y-pan.Y)/zoom);
    private StudioGraphPosition? Hit(Point w)=>editor.GraphView.Positions.LastOrDefault(p=>new Rect(p.X,p.Y,132,72).Contains(w));
    private Point Port(StudioGraphPosition p,bool output,string input="rgb")=>new(p.X+(output?132:0),p.Y+(input=="base"?23:input=="layer"?49:36));
    private bool IsMix(Guid id)=>editor.AdjustmentNodes.Any(n=>n.Id==id&&n.Type==ColorStudioNodeType.GraphMix);
    private IEnumerable<string> InputPorts(Guid id)=>IsMix(id)?new[]{"base","layer"}:new[]{"rgb"};
    private string wireInput="rgb";
    private static Brush Color(string s)=>(Brush)new BrushConverter().ConvertFromString(s)!;
    private static void Text(DrawingContext d,string text,Point p,double size=11,Brush? brush=null)=>d.DrawText(new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,brush??Brushes.Gainsboro,1){MaxTextWidth=121,Trimming=TextTrimming.CharacterEllipsis},p);
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);d.DrawRectangle(Color("#17171A"),null,new Rect(RenderSize));d.PushTransform(new MatrixTransform(zoom,0,0,zoom,pan.X,pan.Y));
        var graph=dragGraph??editor.GraphView;var by=graph.Positions.ToDictionary(p=>p.NodeId);
        foreach(var e in graph.Edges){var a=Port(by[e.From],true);var b=Port(by[e.To],false,e.Input);DrawEdge(d,a,b,Color("#6E9B91"));}
        if(wire is{} id)DrawEdge(d,wireFromOutput?Port(by[id],true):World(screen),wireFromOutput?World(screen):Port(by[id],false,wireInput),Brushes.White);
        int index=0;
        foreach(var p in graph.Positions)
        {
            var n=editor.AdjustmentNodes.FirstOrDefault(n=>n.Id==p.NodeId);bool selected=editor.SelectedAdjustmentNode?.Id==p.NodeId;
            d.PushOpacity(n?.Enabled==false?.45:1);d.DrawRoundedRectangle(Color("#25272A"),new Pen(selected?Brushes.White:Color("#6E9B91"),selected?2:1),new Rect(p.X,p.Y,132,72),3,3);
            var type=p.NodeId==StudioNodeGraph.Source?"输入":p.NodeId==StudioNodeGraph.Output?"输出":n?.Type==ColorStudioNodeType.GraphMix?"⊕ 混合":"◐ 调色";
            d.DrawRectangle(n?.Type==ColorStudioNodeType.GraphMix?Color("#BD9C67"):Color("#6E9B91"),null,new Rect(p.X+1,p.Y+1,130,3));
            if(n is not null)
            {
                var pen=new Pen(Color("#9CC4BA"),1.2);if(n.Type==ColorStudioNodeType.GraphMix){d.DrawEllipse(null,pen,new(p.X+10,p.Y+14),4,4);d.DrawEllipse(null,pen,new(p.X+15,p.Y+14),4,4);}
                else {for(int k=0;k<3;k++){double xx=p.X+7+k*4;d.DrawLine(pen,new(xx,p.Y+8),new(xx,p.Y+19));d.DrawLine(pen,new(xx-2,p.Y+11+k*2),new(xx+2,p.Y+11+k*2));}}
            }
            Text(d,(n is null?type:n.Type==ColorStudioNodeType.GraphMix?"混合":"调色")+(n is null?"":" · "+(++index).ToString("00")),new(p.X+(n is null?6:23),p.Y+6),11,Color("#9CC4BA"));
            if(n is not null){var thumb=Thumbnail?.Invoke(n.Id);if(thumb is not null)d.DrawImage(thumb,new Rect(p.X+6,p.Y+25,46,28));else Text(d,"待渲染",new(p.X+6,p.Y+31),11,Color("#86868C"));if(n.LocalMask is not null&&MaskThumbnail?.Invoke(n.Id) is{} mask)d.DrawImage(mask,new Rect(p.X+57,p.Y+27,24,24));Text(d,n.IsLocked?"锁":!n.Enabled?"旁路":"",new(p.X+91,p.Y+28),11);Text(d,n.Name,new(p.X+6,p.Y+55));}
            else Text(d,type,new(p.X+12,p.Y+32),12);
            if(p.NodeId!=StudioNodeGraph.Source)foreach(var port in InputPorts(p.NodeId)){d.DrawEllipse(Color("#9CC4BA"),new Pen(Color("#17171A"),1),Port(p,false,port),4,4);if(port!="rgb")Text(d,port=="base"?"底":"层",new(p.X+5,p.Y+(port=="base"?13:39)),8);}
            if(p.NodeId!=StudioNodeGraph.Output)d.DrawEllipse(Color("#9CC4BA"),new Pen(Color("#17171A"),1),Port(p,true),4,4);
            if(selected)d.DrawRoundedRectangle(null,new Pen(Brushes.White,2),new Rect(p.X,p.Y,132,72),3,3);
            d.Pop();
        }
        d.Pop();if(editor.AdjustmentStack.Graph is not null&&editor.GraphView.RoutedIds(editor.AdjustmentNodes.ToArray()).Count==0)Text(d,"输出未连通 · 显示原图，节点草稿保留",new(8,3),11,Brushes.Orange);
    }
    private static void DrawEdge(DrawingContext d,Point a,Point b,Brush brush)
    {
        var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(a,false,false);double bend=Math.Max(30,Math.Abs(b.X-a.X)*.45);c.BezierTo(new(a.X+bend,a.Y),new(b.X-bend,b.Y),b,true,false);}g.Freeze();d.DrawGeometry(null,new Pen(brush,1.5),g);
    }
    private StudioGraphEdge? EdgeAt(Point p,Guid? exclude=null)
    {
        var graph=editor.GraphView;var by=graph.Positions.ToDictionary(p=>p.NodeId);
        foreach(var e in graph.Edges.Where(e=>e.From!=exclude&&e.To!=exclude))
        {
            var a=Port(by[e.From],true);var b=Port(by[e.To],false,e.Input);double bend=Math.Max(30,Math.Abs(b.X-a.X)*.45);
            for(int i=0;i<=30;i++){double t=i/30d,u=1-t;var q=new Point(u*u*u*a.X+3*u*u*t*(a.X+bend)+3*u*t*t*(b.X-bend)+t*t*t*b.X,u*u*u*a.Y+3*u*u*t*a.Y+3*u*t*t*b.Y+t*t*t*b.Y);if((p-q).Length<10/zoom)return e;}
        }return null;
    }
    private void Down(object sender,MouseButtonEventArgs e)
    {
        Focus();var w=World(e.GetPosition(this));var graph=editor.GraphView;var hit=Hit(w);
        if(e.ChangedButton==MouseButton.Right){if(hit is not null)editor.SelectedAdjustmentNode=editor.AdjustmentNodes.FirstOrDefault(n=>n.Id==hit.NodeId);OpenMenu(e.GetPosition(this),EdgeAt(w));e.Handled=true;return;}
        if(e.ChangedButton!=MouseButton.Left&&e.ChangedButton!=MouseButton.Middle)return;
        before=graph;start=e.GetPosition(this);moved=false;
        foreach(var p in graph.Positions)
        {
            if(p.NodeId!=StudioNodeGraph.Output&&(Port(p,true)-w).Length<9/zoom){wire=p.NodeId;wireFromOutput=true;screen=start;CaptureMouse();e.Handled=true;return;}
            if(p.NodeId!=StudioNodeGraph.Source)foreach(var port in InputPorts(p.NodeId))if((Port(p,false,port)-w).Length<9/zoom){wire=p.NodeId;wireInput=port;wireFromOutput=false;screen=start;CaptureMouse();e.Handled=true;return;}
        }
        if(hit is not null&&e.ChangedButton==MouseButton.Left){editor.SelectedAdjustmentNode=editor.AdjustmentNodes.FirstOrDefault(n=>n.Id==hit.NodeId);if(editor.SelectedAdjustmentNode?.IsLocked!=true){moving=hit.NodeId;initial=new(hit.X,hit.Y);}}
        else{panning=true;initial=pan;}
        CaptureMouse();InvalidateVisual();e.Handled=true;
    }
    private StudioNodeGraph? dragGraph;
    private void Move(object sender,MouseEventArgs e)
    {
        screen=e.GetPosition(this);if(!IsMouseCaptured)return;var delta=screen-start;moved|=delta.Length>3;
        if(panning){pan=initial+delta;InvalidateVisual();}
        else if(moving is{} id&&moved){dragGraph=before.Move(id,Math.Clamp(initial.X+delta.X/zoom,-10000,10000),Math.Clamp(initial.Y+delta.Y/zoom,-10000,10000));}
        InvalidateVisual();
    }
    private void Up(object sender,MouseButtonEventArgs e)
    {
        if(!IsMouseCaptured)return;
        if(wire is{} id)
        {
            var w=World(e.GetPosition(this));
            if(wireFromOutput){foreach(var target in editor.GraphView.Positions.Where(p=>p.NodeId!=id&&p.NodeId!=StudioNodeGraph.Source))foreach(var port in InputPorts(target.NodeId))if((Port(target,false,port)-w).Length<12/zoom){editor.CommitGraph(before.Connect(id,target.NodeId,port));goto Connected;}}
            else{var target=editor.GraphView.Positions.FirstOrDefault(p=>p.NodeId!=id&&p.NodeId!=StudioNodeGraph.Output&&(Port(p,true)-w).Length<12/zoom);if(target is not null)editor.CommitGraph(before.Connect(target.NodeId,id,wireInput));}
            Connected:;

        }
        else if(moving is{} node&&moved&&dragGraph is{} changed)
        {
            var edge=IsMix(node)?null:EdgeAt(World(e.GetPosition(this)),node);if(edge is not null)try{changed=changed.Insert(node,edge);}catch(System.IO.InvalidDataException){edge=null;}editor.CommitGraph(changed,edge is not null);
        }
        else if(panning)LayoutChanged?.Invoke();
        moving=null;wire=null;panning=false;dragGraph=null;ReleaseMouseCapture();InvalidateVisual();e.Handled=true;
    }
    private void Wheel(object sender,MouseWheelEventArgs e){var p=e.GetPosition(this);var w=World(p);zoom=Math.Clamp(zoom*(e.Delta>0?1.12:1/1.12),.25,2);pan=new(p.X-w.X*zoom,p.Y-w.Y*zoom);LayoutChanged?.Invoke();InvalidateVisual();e.Handled=true;}
    private void Keys(object sender,KeyEventArgs e)
    {
        if(Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or PasswordBox)return;
        if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.C){editor.CopyGraphSettings();e.Handled=true;return;}
        if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.V){editor.PasteGraphSettings();e.Handled=true;return;}
        if(Keyboard.Modifiers==ModifierKeys.Alt&&e.SystemKey==Key.O){ToolTip="外部反向蒙版节点尚未实现";e.Handled=true;return;}
        if(Keyboard.Modifiers==ModifierKeys.Alt&&e.SystemKey==Key.S){editor.AddGraphSerialNode("BasicTone");e.Handled=true;return;}
        if(Keyboard.Modifiers==ModifierKeys.Alt&&e.SystemKey is Key.P or Key.L){editor.AddParallelGraphBranch();e.Handled=true;return;}
        if(Keyboard.Modifiers==ModifierKeys.None&&e.Key==Key.F2){Rename();e.Handled=true;}
        else if(Keyboard.Modifiers==ModifierKeys.None&&e.Key==Key.Delete){editor.DeleteGraphNode();e.Handled=true;}
        else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.D){editor.ToggleAdjustmentNodeCommand.Execute(null);e.Handled=true;}
    }
    public void ExecuteGraphKey(Key key,ModifierKeys modifiers)
    {if(key==Key.F2)Rename();else if(key==Key.Delete)editor.DeleteGraphNode();else if(key==Key.D&&modifiers==ModifierKeys.Control)editor.ToggleAdjustmentNodeCommand.Execute(null);}
    public void Rename()
    {
        if(editor.SelectedAdjustmentNode is not{IsLocked:false} node||Parent is not Panel host)return;
        var dialog=new TextBox{Text=node.Name,Width=190,Height=28,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(12),ToolTip="Enter保存；Esc取消"};host.Children.Add(dialog);Panel.SetZIndex(dialog,99);rename=dialog;
        void Close(){host.Children.Remove(dialog);rename=null;Focus();}
        dialog.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){editor.SelectedNodeName=dialog.Text;Close();e.Handled=true;}else if(e.Key==Key.Escape){Close();e.Handled=true;}};dialog.LostKeyboardFocus+=(_,_)=>{if(rename==dialog)Close();};dialog.Focus();dialog.SelectAll();
    }
    private void OpenMenu(Point p,StudioGraphEdge? edge)
    {
        var menu=new ContextMenu();void Add(string title,Action run,bool enabled=true,string? why=null){var i=new MenuItem{Header=title,IsEnabled=enabled,ToolTip=why};i.Click+=(_,_)=>run();menu.Items.Add(i);}
        Add("添加串行节点 · 基础",()=>editor.AddGraphSerialNode("BasicTone"));
        Add("添加并行节点",()=>editor.AddParallelGraphBranch(),editor.SelectedNodeEditable);
        Add("添加图层混合节点",()=>editor.AddParallelGraphBranch(),editor.SelectedNodeEditable);Add("添加外部节点",()=>{},false,"外部反向蒙版节点尚未实现");menu.Items.Add(new Separator());
        Add("启用 / 旁路  Ctrl+D",()=>editor.ToggleAdjustmentNodeCommand.Execute(null),editor.SelectedAdjustmentNode is not null);
        Add("重置本节点",editor.ResetGraphNode,editor.SelectedNodeEditable);Add("复制节点设置  Ctrl+C",editor.CopyGraphSettings,editor.SelectedAdjustmentNode is not null);Add("粘贴节点设置  Ctrl+V",editor.PasteGraphSettings,editor.SelectedNodeEditable);
        Add("重命名  F2",Rename,editor.SelectedNodeEditable);Add("复制节点",()=>editor.DuplicateGraphNode(),editor.SelectedNodeEditable);
        Add("锁定 / 解除锁定",()=>editor.SelectedNodeLocked=!editor.SelectedNodeLocked,editor.SelectedAdjustmentNode is{SystemLocked:false});
        Add("编辑画笔蒙版",()=>RequestMask?.Invoke(),editor.SelectedNodeEditable);Add("反相蒙版",editor.InvertLocalMask,editor.SelectedNodeEditable);Add("AI 主体 / 天空 / 皮肤",()=>{},false,"未接生产分割模型");
        if(edge is not null)Add("断开此连线 · 输出失连时保留原图",()=>editor.CommitGraph(editor.GraphView.Disconnect(edge)));
        Add("删除节点  Del",()=>editor.DeleteGraphNode(),editor.SelectedNodeEditable);menu.PlacementTarget=this;menu.IsOpen=true;
    }
    public event Action? RequestMask;
    public void OpenLibrary(FrameworkElement anchor)
    {
        var menu=new ContextMenu{PlacementTarget=anchor,StaysOpen=true};var panel=new StackPanel{Width=242};var search=new TextBox{Margin=new Thickness(4),ToolTip="搜索节点"};panel.Children.Add(search);var list=new StackPanel();panel.Children.Add(list);
        void Populate(){list.Children.Clear();foreach(var (group,items) in new[]{("◐ 调色",new[]{("基础 / 曝光","BasicTone"),("白平衡","WhiteBalance"),("RGB 曲线","Curve"),("色轮","ColorBalance"),("HSL 范围","ColorRange"),("色阶","Levels"),("肤色","SkinTone"),("细节","Details")}), ("▦ 透明图层",new[]{("画笔 / 修复 / 仿制","disabled:尚未接透明图层引擎")}), ("■ 填充",new[]{("纯色 / 渐变","disabled:填充节点尚未准入")}), ("⊕ 混合",new[]{("并行 / 图层混合","parallel")}), ("◌ 外部",new[]{("反向蒙版","disabled:尚未实现")}), ("▣ 即时胶片",new[]{("显影结果","disabled:请在即时胶片页显影，保留系统锁定配方")})}){var matched=items.Where(x=>(group+x.Item1).Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).ToArray();if(matched.Length==0)continue;list.Children.Add(new TextBlock{Text=group,Foreground=Color("#9CC4BA"),Margin=new Thickness(6)});foreach(var (name,id) in matched){var b=new Button{Content=name,IsEnabled=!id.StartsWith("disabled:"),ToolTip=id.StartsWith("disabled:")?id[9..]:name,HorizontalContentAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>{if(id=="parallel")editor.AddParallelGraphBranch();else editor.AddGraphSerialNode(id);menu.IsOpen=false;};list.Children.Add(b);}}}
        search.TextChanged+=(_,_)=>Populate();Populate();menu.Items.Add(new MenuItem{Header=panel,StaysOpenOnClick=true});menu.IsOpen=true;search.Focus();
    }
}
