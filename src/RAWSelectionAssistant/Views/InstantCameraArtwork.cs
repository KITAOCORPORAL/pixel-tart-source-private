using System.Windows;
using System.Windows.Media;
namespace RAWSelectionAssistant.Views;

/// <summary>Original layered vector artwork; no copied vendor artwork, textures, or logos.</summary>
public sealed class InstantCameraArtwork : FrameworkElement
{
    public string Model {get;set;}="SX-70";
    public static Rect Slot(string model)=>model=="i-2"?new(23,148,184,6):new(19,144,192,5);
    protected override void OnRender(DrawingContext dc)
    {dc.PushTransform(new ScaleTransform(ActualWidth/230,ActualHeight/160));Draw(dc,Model);dc.Pop();}
    public static void Draw(DrawingContext dc,string model)
    {
        var metal=new SolidColorBrush(Color.FromRgb(177,173,163));var leather=new SolidColorBrush(Color.FromRgb(99,63,43));
        var dark=new SolidColorBrush(Color.FromRgb(35,38,39));var edge=new Pen(metal,2);
        if(model=="i-2")
        {
            dc.DrawGeometry(dark,edge,Geometry.Parse("M 8,58 Q 8,23 41,23 L 75,23 85,9 169,9 Q 221,9 222,58 L 222,149 8,149 Z"));
            dc.DrawRoundedRectangle(Brushes.Black,new Pen(Brushes.DimGray,2),new Rect(16,31,51,30),5,5);
            dc.DrawRectangle(Brushes.LightSlateGray,null,new Rect(22,37,37,17));
            dc.DrawRoundedRectangle(Brushes.Black,edge,new Rect(178,24,30,22),5,5);
            dc.DrawEllipse(Brushes.Black,new Pen(Brushes.Gray,9),new Point(129,79),48,48);
            for(var i=0;i<16;i++){var angle=i*Math.PI/8;dc.DrawLine(new Pen(Brushes.DarkGray,1),new Point(129+43*Math.Cos(angle),79+43*Math.Sin(angle)),new Point(129+47*Math.Cos(angle),79+47*Math.Sin(angle)));}
            dc.DrawEllipse(Brushes.OrangeRed,null,new Point(31,88),10,10);
            dc.DrawRoundedRectangle(Brushes.Black,null,new Rect(169,108,38,16),3,3);
        }
        else
        {
            dc.DrawGeometry(metal,edge,Geometry.Parse("M 4,127 L 48,50 190,50 226,127 226,149 4,149 Z"));
            dc.DrawGeometry(dark,null,Geometry.Parse("M 50,53 L 77,15 174,15 191,54 171,115 35,115 Z"));
            for(var i=0;i<6;i++)dc.DrawLine(new Pen(Brushes.DimGray,2),new Point(53+i*4,54+i*9),new Point(187-i*3,54+i*9));
            dc.DrawGeometry(leather,edge,Geometry.Parse("M 76,9 L 174,9 187,44 57,44 Z"));
            dc.DrawRoundedRectangle(Brushes.Black,edge,new Rect(18,74,184,52),4,4);
            dc.DrawRectangle(leather,null,new Rect(12,129,206,12));
            dc.DrawRectangle(Brushes.DarkSlateGray,edge,new Rect(28,80,29,19));
            dc.DrawEllipse(Brushes.IndianRed,null,new Point(184,96),9,9);
            dc.DrawEllipse(Brushes.Black,new Pen(metal,5),new Point(121,98),29,29);
        }
        var lens=model=="i-2"?new Point(129,79):new Point(121,98);var radius=model=="i-2"?32:20;
        dc.DrawEllipse(Brushes.DarkSlateGray,new Pen(Brushes.Black,4),lens,radius,radius);
        dc.DrawEllipse(Brushes.Black,null,lens,radius*.65,radius*.65);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(100,135,180,199)),null,new Point(lens.X-6,lens.Y-7),5,3);
        dc.DrawRectangle(Brushes.Black,new Pen(Brushes.DimGray,1),Slot(model));
    }
}

