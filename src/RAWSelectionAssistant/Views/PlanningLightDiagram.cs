using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;

/// <summary>Normalized persisted coordinates; metres are an explicit diagram scale, not inferred venue measurements.</summary>
public sealed class PlanningLightDiagram : Canvas
{
    private readonly Func<PlanningLight,Task<bool>> _save;
    private readonly Action<PlanningLight> _edit;
    private readonly PlanningLight[] _lights;
    private PlanningLight? _drag;private Button? _dragButton;private Line? _dragDirection;private readonly Dictionary<Guid,Line> _directions=[];private Point _down;private Point _origin;private bool _moved;
    public bool HasPendingDrag=>_drag is not null;
    internal void BeginPositionEdit(PlanningLight light){_drag=light;_origin=new(light.X,light.Y);_moved=false;}
    internal void CancelPositionEdit()=>Cancel();
    public PlanningLightDiagram(IEnumerable<PlanningLight> lights,Func<PlanningLight,Task<bool>> save,Action<PlanningLight> edit)
    {
        _lights=lights.ToArray();_save=save;_edit=edit;Height=360;ClipToBounds=true;Focusable=true;
        Background=new SolidColorBrush(Color.FromRgb(24,24,27));SizeChanged+=(_,_)=>Draw();
        MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource!=this)return;var point=e.GetPosition(this);_edit(new PlanningLight{X=Math.Clamp(point.X/Math.Max(1,ActualWidth-110),0,1),Y=Math.Clamp((point.Y-24)/280,0,1)});};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&_drag is not null){Cancel();e.Handled=true;}};
        LostMouseCapture+=(_,_)=>{if(_drag is not null)Cancel();};Unloaded+=(_,_)=>Cancel();
    }
    public static Point Snap(Point normalized)=>new(Math.Clamp(Math.Round(normalized.X*20)/20,0,1),Math.Clamp(Math.Round(normalized.Y*20)/20,0,1));
    public Task<bool> CommitPositionAsync(PlanningLight light,Point normalized)
    {var point=Snap(normalized);return _save(light with{X=point.X,Y=point.Y});}
    private void Draw()
    {
        if(_drag is not null)return;Children.Clear();_directions.Clear();var width=Math.Max(1,ActualWidth-110);const double height=280;
        for(var step=0;step<=10;step++)
        {
            var x=step*width/10;var y=step*height/10;
            Children.Add(new Line{X1=x,Y1=24,X2=x,Y2=304,Stroke=Brushes.DimGray,Opacity=.25,IsHitTestVisible=false});
            Children.Add(new Line{X1=0,Y1=y+24,X2=width+110,Y2=y+24,Stroke=Brushes.DimGray,Opacity=.25,IsHitTestVisible=false});
            var label=new TextBlock{Text=step+"m",Foreground=Brushes.Gray,FontSize=10,IsHitTestVisible=false};SetLeft(label,x);Children.Add(label);
        }
        var subject=new TextBlock{Text="主体",Foreground=Brushes.LightGray,IsHitTestVisible=false};SetLeft(subject,width/2);SetTop(subject,160);Children.Add(subject);
        foreach(var light in _lights.Where(x=>!x.Archived))
        {
            var button=new Button{Content="◉ "+light.Name,Padding=new Thickness(6),ToolTip=$"X {light.X*10:0.0}m · Y {light.Y*10:0.0}m · {light.DirectionDegrees:0}°"};
            SetLeft(button,light.X*width);SetTop(button,24+light.Y*height);Children.Add(button);
            var radians=light.DirectionDegrees*Math.PI/180;var direction=new Line{X1=light.X*width+20,Y1=24+light.Y*height+16,X2=light.X*width+20+Math.Cos(radians)*42,Y2=24+light.Y*height+16+Math.Sin(radians)*42,Stroke=Brushes.LightSeaGreen,IsHitTestVisible=false};_directions[light.Id]=direction;Children.Add(direction);
            button.PreviewMouseLeftButtonDown+=(_,e)=>{BeginPositionEdit(light);_dragButton=button;_dragDirection=_directions.GetValueOrDefault(light.Id);_down=e.GetPosition(this);_moved=false;Focus();CaptureMouse();e.Handled=true;};
        }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(_drag is null||_dragButton is null)return;var pt=e.GetPosition(this);if((pt-_down).Length>4)_moved=true;if(!_moved)return;
        var point=Snap(new(_origin.X+(pt.X-_down.X)/Math.Max(1,ActualWidth-110),_origin.Y+(pt.Y-_down.Y)/280));SetLeft(_dragButton,point.X*Math.Max(1,ActualWidth-110));SetTop(_dragButton,24+point.Y*280);_dragButton.ToolTip=$"X {point.X*10:0.0}m · Y {point.Y*10:0.0}m";
        if(_dragDirection is {} direction){var rad=_drag.DirectionDegrees*Math.PI/180;direction.X1=point.X*Math.Max(1,ActualWidth-110)+20;direction.Y1=24+point.Y*280+16;direction.X2=direction.X1+Math.Cos(rad)*42;direction.Y2=direction.Y1+Math.Sin(rad)*42;}
    }
    protected override async void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);if(_drag is not {} original)return;var moved=_moved;var button=_dragButton;var point=new Point(GetLeft(button!)/Math.Max(1,ActualWidth-110),(GetTop(button!)-24)/280);_drag=null;_dragButton=null;ReleaseMouseCapture();e.Handled=true;
        if(!moved){_edit(original);Draw();return;}
        // Single resource transaction, existing VM undo and persistence gate. Failure keeps old persisted object.
        try{if(await CommitPositionAsync(original,point)){var index=Array.FindIndex(_lights,l=>l.Id==original.Id);if(index>=0)_lights[index]=original with{X=Snap(point).X,Y=Snap(point).Y};}Draw();}
        catch(Exception error)when(error is System.IO.IOException or InvalidDataException or ArgumentException){Draw();ThemedMessageDialog.Show(Window.GetWindow(this),"灯位未保存",error.Message,ThemedMessageKind.Error);}
    }
    private void Cancel(){_drag=null;_dragButton=null;_dragDirection=null;_moved=false;if(IsMouseCaptured)ReleaseMouseCapture();Draw();}
}
