using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Views;

/// <summary>Displays processed bitmaps; color processing remains in the existing renderer.</summary>
public sealed partial class ColorStudioImageViewport : FrameworkElement
{
    public static readonly DependencyProperty SourcePixelSizeProperty = DependencyProperty.Register(nameof(SourcePixelSize), typeof(Size), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata(Size.Empty, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public Size SourcePixelSize { get => (Size)GetValue(SourcePixelSizeProperty); set => SetValue(SourcePixelSizeProperty, value); }
    public static readonly DependencyProperty OriginalProperty = DependencyProperty.Register(nameof(Original), typeof(BitmapSource), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty MatchedProperty = DependencyProperty.Register(nameof(Matched), typeof(BitmapSource), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(string), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata("原片", FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty SplitProperty = DependencyProperty.Register(nameof(Split), typeof(double), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata(.5, FrameworkPropertyMetadataOptions.AffectsRender));
    public BitmapSource? Original { get => (BitmapSource?)GetValue(OriginalProperty); set => SetValue(OriginalProperty, value); }
    public BitmapSource? Matched { get => (BitmapSource?)GetValue(MatchedProperty); set => SetValue(MatchedProperty, value); }
    public string Mode { get => (string)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public double Split { get => (double)GetValue(SplitProperty); set => SetValue(SplitProperty, value); }
    public static readonly DependencyProperty TilesProperty=DependencyProperty.Register(nameof(Tiles),typeof(IReadOnlyList<RAWSelectionAssistant.ViewModels.StudioDetailTile>),typeof(ColorStudioImageViewport),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<RAWSelectionAssistant.ViewModels.StudioDetailTile>? Tiles {get=>(IReadOnlyList<RAWSelectionAssistant.ViewModels.StudioDetailTile>?)GetValue(TilesProperty);set=>SetValue(TilesProperty,value);}
    public static readonly DependencyProperty DetailOriginalProperty=DependencyProperty.Register(nameof(DetailOriginal),typeof(BitmapSource),typeof(ColorStudioImageViewport),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty DetailMatchedProperty=DependencyProperty.Register(nameof(DetailMatched),typeof(BitmapSource),typeof(ColorStudioImageViewport),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty DetailRegionProperty=DependencyProperty.Register(nameof(DetailRegion),typeof(Rect),typeof(ColorStudioImageViewport),new FrameworkPropertyMetadata(Rect.Empty,FrameworkPropertyMetadataOptions.AffectsRender));
    public BitmapSource? DetailOriginal {get=>(BitmapSource?)GetValue(DetailOriginalProperty);set=>SetValue(DetailOriginalProperty,value);}
    public BitmapSource? DetailMatched {get=>(BitmapSource?)GetValue(DetailMatchedProperty);set=>SetValue(DetailMatchedProperty,value);}
    public Rect DetailRegion {get=>(Rect)GetValue(DetailRegionProperty);set=>SetValue(DetailRegionProperty,value);}
    public Rect VisibleSourceRegion()
    {
        var viewport=ColorStudioSampleMapping.Viewport(RenderSize,Mode=="并排对比",false);var image=State.ImageRect(viewport);
        if(image.IsEmpty||State.Zoom<=0)return Rect.Empty;var visible=Rect.Intersect(viewport,image);if(visible.IsEmpty)return Rect.Empty;
        return new((visible.Left-image.Left)/State.Zoom,(visible.Top-image.Top)/State.Zoom,visible.Width/State.Zoom,visible.Height/State.Zoom);
    }
    public ColorStudioZoomPanState State { get; } = new();
    public ColorStudioImageViewport() { AttachProfileDiagnostics(); ClipToBounds = true; SizeChanged += (_, _) => Configure(); State.Changed += (_, _) => InvalidateVisual(); }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((ColorStudioImageViewport)sender).Configure();
    // Layout only: use the displayed paper aspect, while keeping full-resolution
    // output units for 100%, pan limits and source-coordinate mapping.
    public static readonly DependencyProperty PreferDisplayedDimensionsProperty = DependencyProperty.Register(
        nameof(PreferDisplayedDimensions), typeof(bool), typeof(ColorStudioImageViewport),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public bool PreferDisplayedDimensions
    { get => (bool)GetValue(PreferDisplayedDimensionsProperty); set => SetValue(PreferDisplayedDimensionsProperty, value); }
    public void Configure()
    {
        QueueDisplayImages();
        var image = PreferDisplayedDimensions && (Mode is "仿色结果" or "仿色") ? Matched ?? Original : Original ?? Matched;
        if (image is null) return;
        var size = SourcePixelSize;
        if (size.IsEmpty || !double.IsFinite(size.Width + size.Height) || size.Width <= 0 || size.Height <= 0)
            size = new Size(image.PixelWidth, image.PixelHeight);
        if (PreferDisplayedDimensions)
        {
            // The model publishes full output dimensions. Proxy raster rounding must
            // not replace them; correct only a genuinely different source aspect.
            var sourceRatio = size.Width / size.Height;
            var displayRatio = (double)image.PixelWidth / image.PixelHeight;
            if (Math.Abs(sourceRatio - displayRatio) > 2d / Math.Min(image.PixelWidth, image.PixelHeight))
            {
                var longest = Math.Max(size.Width, size.Height);
                size = displayRatio >= 1 ? new Size(longest, longest / displayRatio) : new Size(longest * displayRatio, longest);
            }
        }
        State.Configure(ColorStudioSampleMapping.Viewport(RenderSize, Mode == "并排对比", false).Size, size);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Original is null && Matched is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var full = new Rect(RenderSize);
        var brush = TryFindResource("TextValueBrush") as Brush ?? Brushes.Gray;
        void Draw(BitmapSource? image, Rect viewport, Rect clip,BitmapSource? detail=null,bool original=false)
        {
            if (image is null) return;
            dc.PushClip(new RectangleGeometry(clip));var imageRect=State.ImageRect(viewport);dc.DrawImage(DisplayPixels(image),imageRect);
            if(!DisplayColorEnabled&&Tiles is {Count:>0})foreach(var tile in Tiles){var r=tile.Region;dc.DrawImage(DisplayPixels(original?tile.Before:tile.After),new Rect(imageRect.Left+r.Left*State.Zoom,imageRect.Top+r.Top*State.Zoom,r.Width*State.Zoom,r.Height*State.Zoom));}
            if((DisplayColorEnabled||Tiles is not {Count:>0})&&detail is not null&&CanPresentDetail(detail)&&!DetailRegion.IsEmpty){var region=DetailRegion;var target=new Rect(imageRect.Left+region.Left*State.Zoom,imageRect.Top+region.Top*State.Zoom,region.Width*State.Zoom,region.Height*State.Zoom);dc.DrawImage(DisplayPixels(detail),target);}dc.Pop();
        }
        if (Mode == "并排对比" && Matched is not null)
        {
            var left = ColorStudioSampleMapping.Viewport(RenderSize, true, false); var right = ColorStudioSampleMapping.Viewport(RenderSize, true, true);
            Draw(Original, left, left,DetailOriginal,true); Draw(Matched, right, right,DetailMatched);
            dc.DrawLine(new Pen(brush, 1), new Point(ActualWidth / 2, 0), new Point(ActualWidth / 2, ActualHeight));
        }
        else if (Mode == "左右对比" && Matched is not null)
        {
            var x = ActualWidth * Math.Clamp(Split, 0, 1);
            Draw(Original, full, new Rect(0, 0, x, ActualHeight),DetailOriginal,true); Draw(Matched, full, new Rect(x, 0, ActualWidth - x, ActualHeight),DetailMatched);
            dc.DrawLine(new Pen(brush, 1), new Point(x, 0), new Point(x, ActualHeight));
            dc.DrawRoundedRectangle(brush, null, new Rect(x - 4, ActualHeight / 2 - 15, 8, 30), 4, 4);
        }
        else Draw(Mode is "仿色结果" or "仿色" ? Matched ?? Original : Original, full, full,Mode is "仿色结果" or "仿色"?DetailMatched:DetailOriginal,Mode is not ("仿色结果" or "仿色"));
    }
}
