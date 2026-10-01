using System.Windows;
using System.Windows.Media;

namespace RAWSelectionAssistant.Views;

public sealed class ImageHighlightOverlay : FrameworkElement
{
    public static readonly DependencyProperty PixelIndicesProperty = DependencyProperty.Register(nameof(PixelIndices), typeof(IReadOnlyList<int>), typeof(ImageHighlightOverlay), new FrameworkPropertyMetadata(Array.Empty<int>(), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register(nameof(ImageWidth), typeof(int), typeof(ImageHighlightOverlay), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register(nameof(ImageHeight), typeof(int), typeof(ImageHighlightOverlay), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<int> PixelIndices { get => (IReadOnlyList<int>)GetValue(PixelIndicesProperty); set => SetValue(PixelIndicesProperty, value); }
    public int ImageWidth { get => (int)GetValue(ImageWidthProperty); set => SetValue(ImageWidthProperty, value); }
    public int ImageHeight { get => (int)GetValue(ImageHeightProperty); set => SetValue(ImageHeightProperty, value); }
    public ColorStudioZoomPanState? ViewState { get; set; }
    public bool IsPreviewOnly => true;
    protected override void OnRender(DrawingContext dc)
    {
        if (ImageWidth <= 0 || ImageHeight <= 0 || PixelIndices.Count == 0) return;
        var brush = new SolidColorBrush(Color.FromArgb(82, 42, 214, 190)); brush.Freeze();
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(190, 70, 238, 207)), 1); pen.Freeze();
        var state = ViewState;
        var imageRect = state is null ? new Rect(0, 0, ActualWidth, ActualHeight) : state.ImageRect(new Rect(0, 0, ActualWidth, ActualHeight));
        var cellW = imageRect.Width / ImageWidth; var cellH = imageRect.Height / ImageHeight;
        foreach (var index in PixelIndices.Take(12000))
        {
            var y = index / ImageWidth; var x = index % ImageWidth;
            dc.DrawRectangle(brush, pen, new Rect(imageRect.Left + x * cellW, imageRect.Top + y * cellH, Math.Max(1, cellW), Math.Max(1, cellH)));
        }
    }
}
