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
    public static readonly DependencyProperty CameraProperty=DependencyProperty.Register(nameof(Camera),typeof(string),typeof(InstantDevelopmentView),new FrameworkPropertyMetadata("SX-70",FrameworkPropertyMetadataOptions.AffectsRender));
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
            var cameraScale=Math.Min(1,Math.Min(ActualWidth/340,ActualHeight/420));
            var body=new Rect(ActualWidth/2-115*cameraScale,ActualHeight/2-125*cameraScale,230*cameraScale,160*cameraScale);
            var slot=InstantCameraArtwork.Slot(Camera);
            var progress=frame.Stage==InstantDevelopmentStage.Ejecting?frame.Progress:0;
            var width=slot.Width*cameraScale;var height=width*Image.PixelHeight/Image.PixelWidth;
            var eject=new Rect(body.Left+slot.X*cameraScale,body.Top+slot.Y*cameraScale-height*(1-progress),width,height);
            dc.PushClip(new RectangleGeometry(new Rect(body.Left,body.Top+slot.Y*cameraScale,body.Width,height)));
            dc.DrawRectangle(Brushes.Ivory,new Pen(Brushes.Silver,1),eject);
            dc.DrawRectangle(Brushes.LightGray,null,new Rect(eject.X+ImageRegion.X*width,eject.Y+ImageRegion.Y*height,ImageRegion.Width*width,ImageRegion.Height*height));
            dc.Pop();
            dc.PushTransform(new TranslateTransform(body.Left,body.Top));dc.PushTransform(new ScaleTransform(cameraScale,cameraScale));
            InstantCameraArtwork.Draw(dc,Camera);dc.Pop();dc.Pop();
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
