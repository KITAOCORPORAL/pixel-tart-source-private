using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Views;

/// <summary>Displays processed bitmaps; color processing remains in the existing renderer.</summary>
public sealed class ColorStudioImageViewport : FrameworkElement
{
    public static readonly DependencyProperty OriginalProperty = DependencyProperty.Register(nameof(Original), typeof(BitmapSource), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty MatchedProperty = DependencyProperty.Register(nameof(Matched), typeof(BitmapSource), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(string), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata("原片", FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty SplitProperty = DependencyProperty.Register(nameof(Split), typeof(double), typeof(ColorStudioImageViewport), new FrameworkPropertyMetadata(.5, FrameworkPropertyMetadataOptions.AffectsRender));
    public BitmapSource? Original { get => (BitmapSource?)GetValue(OriginalProperty); set => SetValue(OriginalProperty, value); }
    public BitmapSource? Matched { get => (BitmapSource?)GetValue(MatchedProperty); set => SetValue(MatchedProperty, value); }
    public string Mode { get => (string)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public double Split { get => (double)GetValue(SplitProperty); set => SetValue(SplitProperty, value); }
    public ColorStudioZoomPanState State { get; } = new();
    public ColorStudioImageViewport() { ClipToBounds = true; SizeChanged += (_, _) => Configure(); State.Changed += (_, _) => InvalidateVisual(); }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((ColorStudioImageViewport)sender).Configure();
    public void Configure() { if (Original is { } image) State.Configure(ColorStudioSampleMapping.Viewport(RenderSize, Mode == "并排对比", false).Size, new Size(image.PixelWidth, image.PixelHeight)); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Original is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var full = new Rect(RenderSize);
        var brush = TryFindResource("TextValueBrush") as Brush ?? Brushes.Gray;
        void Draw(BitmapSource? image, Rect viewport, Rect clip)
        {
            if (image is null) return;
            dc.PushClip(new RectangleGeometry(clip)); dc.DrawImage(image, State.ImageRect(viewport)); dc.Pop();
        }
        if (Mode == "并排对比" && Matched is not null)
        {
            var left = ColorStudioSampleMapping.Viewport(RenderSize, true, false); var right = ColorStudioSampleMapping.Viewport(RenderSize, true, true);
            Draw(Original, left, left); Draw(Matched, right, right);
            dc.DrawLine(new Pen(brush, 1), new Point(ActualWidth / 2, 0), new Point(ActualWidth / 2, ActualHeight));
        }
        else if (Mode == "左右对比" && Matched is not null)
        {
            var x = ActualWidth * Math.Clamp(Split, 0, 1);
            Draw(Original, full, new Rect(0, 0, x, ActualHeight)); Draw(Matched, full, new Rect(x, 0, ActualWidth - x, ActualHeight));
            dc.DrawLine(new Pen(brush, 1), new Point(x, 0), new Point(x, ActualHeight));
            dc.DrawRoundedRectangle(brush, null, new Rect(x - 4, ActualHeight / 2 - 15, 8, 30), 4, 4);
        }
        else Draw(Mode is "仿色结果" or "仿色" ? Matched ?? Original : Original, full, full);
    }
}
