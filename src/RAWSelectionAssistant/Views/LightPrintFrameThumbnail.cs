using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Views;

/// <summary>Selector icon only. The full photo continues through the versioned renderer.</summary>
public sealed class LightPrintFrameThumbnail : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(LightPrintFrame), typeof(LightPrintFrameThumbnail),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public LightPrintFrame? Frame { get => (LightPrintFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public static readonly DependencyProperty PhotoPreviewsProperty=DependencyProperty.Register(nameof(PhotoPreviews),typeof(IReadOnlyDictionary<string,BitmapSource>),typeof(LightPrintFrameThumbnail),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyDictionary<string,BitmapSource>? PhotoPreviews{get=>(IReadOnlyDictionary<string,BitmapSource>?)GetValue(PhotoPreviewsProperty);set=>SetValue(PhotoPreviewsProperty,value);}
    internal BitmapSource? ActualPhotoPreview=>Frame is{} f?PhotoPreviews?.GetValueOrDefault(f.Id):null;
    private string? loadedFrame;private BitmapSource? loadedArtwork;
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Frame is not { } frame || ActualWidth <= 0 || ActualHeight <= 0) return;
        if(ActualPhotoPreview is{} actual){double ratio=actual.PixelWidth/(double)actual.PixelHeight;double w=Math.Min(ActualWidth,ActualHeight*ratio),h=w/ratio;dc.DrawImage(actual,new Rect((ActualWidth-w)/2,(ActualHeight-h)/2,w,h));return;}
        if(frame.Id.StartsWith("orig.",StringComparison.Ordinal))
        {
            try
            {
                if(loadedFrame!=frame.Id){var art=OriginalFrameLibrary.Find(frame.Id);var rgba=OriginalFrameLibrary.Load(art);
                for(int i=0;i<rgba.Length;i+=4)(rgba[i],rgba[i+2])=(rgba[i+2],rgba[i]);var bitmap=BitmapSource.Create(art.Width,art.Height,96,96,PixelFormats.Bgra32,null,rgba,art.Width*4);bitmap.Freeze();loadedFrame=frame.Id;loadedArtwork=bitmap;}
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30,31,33)),null,new Rect(0,0,ActualWidth,ActualHeight));var width=Math.Min(ActualWidth,ActualHeight*loadedArtwork!.PixelWidth/loadedArtwork.PixelHeight);var height=width*loadedArtwork.PixelHeight/loadedArtwork.PixelWidth;dc.DrawImage(loadedArtwork,new Rect((ActualWidth-width)/2,(ActualHeight-height)/2,width,height));
            }
            catch(Exception error)when(error is System.IO.IOException or InvalidOperationException){ToolTip="相框不可用："+error.Message;}
            return;
        }
        var paper = frame.Color switch { "Cool" => Color.FromRgb(221,227,232), "Neutral" => Color.FromRgb(230,230,228), _ => Color.FromRgb(236,232,219) };
        Brush fill = new SolidColorBrush(paper);
        if (frame.Pattern == "Foil") fill = new LinearGradientBrush(Color.FromRgb(161,172,181), Color.FromRgb(239,241,242), 35);
        var bounds = new Rect(1,1,Math.Max(1,ActualWidth-2),Math.Max(1,ActualHeight-2));
        dc.DrawRectangle(fill, null, bounds);
        if (frame.Pattern == "Dots")
            for (double y=5; y<bounds.Bottom; y+=7)
                for (double x=5; x<bounds.Right; x+=7)
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(176,163,145)), null, new Point(x,y), 1, 1);
        var window = new Rect(7,6,Math.Max(1,ActualWidth-14),Math.Max(1,ActualHeight-22));
        var ink = new SolidColorBrush(Color.FromRgb(30,31,33));
        if (frame.Window == "Circle") dc.DrawEllipse(ink, null, new Point(window.Left+window.Width/2,window.Top+window.Height/2), Math.Min(window.Width,window.Height)/2, Math.Min(window.Width,window.Height)/2);
        else dc.DrawRectangle(ink, null, window);
    }
}
