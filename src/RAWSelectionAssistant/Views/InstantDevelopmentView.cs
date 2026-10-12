using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

/// <summary>Original procedural camera silhouettes and presentation-only development. No external artwork.</summary>
public sealed class InstantDevelopmentView : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty=DependencyProperty.Register(nameof(Frame),typeof(InstantDevelopmentFrame),typeof(InstantDevelopmentView),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ImageProperty=DependencyProperty.Register(nameof(Image),typeof(BitmapSource),typeof(InstantDevelopmentView),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender,OnImageChanged));
    private BitmapSource? _gray;
    private static void OnImageChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {var view=(InstantDevelopmentView)sender;view._gray=args.NewValue is BitmapSource image?new FormatConvertedBitmap(image,PixelFormats.Gray8,null,0):null;view._gray?.Freeze();}
    public static readonly DependencyProperty CameraProperty=DependencyProperty.Register(nameof(Camera),typeof(string),typeof(InstantDevelopmentView),new FrameworkPropertyMetadata("SX-70/680",FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty DirectionProperty=DependencyProperty.Register(nameof(Direction),typeof(string),typeof(InstantDevelopmentView),new FrameworkPropertyMetadata("Color",FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ImageRegionProperty=DependencyProperty.Register(nameof(ImageRegion),typeof(Rect),typeof(InstantDevelopmentView),new FrameworkPropertyMetadata(new Rect(0,0,1,1),FrameworkPropertyMetadataOptions.AffectsRender));
    public Rect ImageRegion {get=>(Rect)GetValue(ImageRegionProperty);set=>SetValue(ImageRegionProperty,value);}
    public InstantDevelopmentFrame? Frame {get=>(InstantDevelopmentFrame?)GetValue(FrameProperty);set=>SetValue(FrameProperty,value);}
    public BitmapSource? Image {get=>(BitmapSource?)GetValue(ImageProperty);set=>SetValue(ImageProperty,value);}
    public string Camera {get=>(string)GetValue(CameraProperty);set=>SetValue(CameraProperty,value);}
    public string Direction {get=>(string)GetValue(DirectionProperty);set=>SetValue(DirectionProperty,value);}
    protected override void OnRender(DrawingContext dc)
    {
        if(Frame is not {} frame||frame.Stage is InstantDevelopmentStage.Computing||Image is null)return;
        var scale=Math.Min(ActualWidth*.76/Image.PixelWidth,ActualHeight*.8/Image.PixelHeight);
        var paper=new Rect((ActualWidth-Image.PixelWidth*scale)/2,(ActualHeight-Image.PixelHeight*scale)/2,Image.PixelWidth*scale,Image.PixelHeight*scale);
        if(frame.CameraVisible)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(170,18,20,23)),null,new Rect(RenderSize));
            var body=new Rect(ActualWidth/2-115,ActualHeight/2-110,230,145);
            var progress=frame.Stage==InstantDevelopmentStage.Ejecting?frame.Progress:0;
            var eject=new Rect(body.Left+28,body.Bottom-8,174,Math.Max(1,130*progress));
            dc.DrawRectangle(Brushes.Ivory,new Pen(Brushes.Silver,1),eject);
            if(progress>.15){dc.PushClip(new RectangleGeometry(eject));dc.DrawRectangle(Brushes.LightGray,null,new Rect(body.Left+35,body.Bottom-1,160,110));dc.Pop();}
            dc.DrawRoundedRectangle(Camera=="i-2"?Brushes.DimGray:Brushes.SaddleBrown,new Pen(Brushes.Silver,3),body,Camera=="i-2"?24:5,Camera=="i-2"?24:5);
            dc.DrawEllipse(Brushes.Black,new Pen(Brushes.DarkGray,9),new Point(body.Left+120,body.Top+73),43,43);
            dc.DrawEllipse(Brushes.DarkSlateBlue,new Pen(Brushes.SlateGray,3),new Point(body.Left+120,body.Top+73),26,26);
            dc.DrawRoundedRectangle(Brushes.LightGray,null,new Rect(body.Left+16,body.Top+16,44,24),3,3);
            dc.DrawEllipse(Brushes.IndianRed,null,new Point(body.Right-24,body.Top+25),9,9);
            dc.DrawRectangle(Brushes.Black,null,new Rect(body.Left+20,body.Bottom-9,190,6));
            return;
        }
        // Normal editor background remains visible. Paper is sharp from the first frame.
        dc.DrawImage(Image,paper);
        var p=frame.Progress;
        var imageRect=new Rect(paper.Left+paper.Width*ImageRegion.X,paper.Top+paper.Height*ImageRegion.Y,paper.Width*ImageRegion.Width,paper.Height*ImageRegion.Height);
        // Tonal contours first, then the chosen color family; BW has no forced cyan-to-warm sequence.
        if(_gray is not null&&Direction!="BW")
        {
            var colorProgress=Math.Clamp((p-(Direction=="Color"?.2:.1))/(Direction=="Color"?.65:.7),0,1);
            dc.PushClip(new RectangleGeometry(imageRect));dc.PushOpacity(1-colorProgress);dc.DrawImage(_gray,paper);dc.Pop();dc.Pop();
        }
        var fog=Direction switch{"BW"=>Color.FromRgb(220,222,215),"BlackBlue"=>Color.FromRgb(170,186,197),"WhiteBlue"=>Color.FromRgb(209,220,225),_=>Color.FromRgb(216,223,211)};
        dc.PushOpacity(1-p*p*(3-2*p));dc.DrawRectangle(new SolidColorBrush(fog),null,imageRect);dc.Pop();
    }
}
