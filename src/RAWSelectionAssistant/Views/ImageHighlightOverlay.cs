using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Views;

public sealed class ImageHighlightOverlay : FrameworkElement
{
    private BitmapSource? _mask;
    public static readonly DependencyProperty PixelIndicesProperty = DependencyProperty.Register(nameof(PixelIndices), typeof(IReadOnlyList<int>), typeof(ImageHighlightOverlay), new FrameworkPropertyMetadata(Array.Empty<int>(), FrameworkPropertyMetadataOptions.AffectsRender, InvalidateMask));
    public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register(nameof(ImageWidth), typeof(int), typeof(ImageHighlightOverlay), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateMask));
    public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register(nameof(ImageHeight), typeof(int), typeof(ImageHighlightOverlay), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateMask));
    public IReadOnlyList<int> PixelIndices { get => (IReadOnlyList<int>)GetValue(PixelIndicesProperty); set => SetValue(PixelIndicesProperty, value); }
    public int ImageWidth { get => (int)GetValue(ImageWidthProperty); set => SetValue(ImageWidthProperty, value); }
    public int ImageHeight { get => (int)GetValue(ImageHeightProperty); set => SetValue(ImageHeightProperty, value); }
    public ColorStudioZoomPanState? ViewState { get; set; }
    public string ViewMode { get; set; } = "原片";
    public bool IsPreviewOnly => true;
    private static void InvalidateMask(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((ImageHighlightOverlay)sender)._mask = null;
    protected override void OnRender(DrawingContext dc)
    {
        if (ImageWidth <= 0 || ImageHeight <= 0 || PixelIndices.Count == 0) return;
        if (_mask is null)
        {
            var pixels = new byte[checked(ImageWidth * ImageHeight * 4)];
            foreach (var index in PixelIndices)
            {
                if (index < 0 || index >= pixels.Length / 4) continue;
                var offset = index * 4;
                pixels[offset] = 190; pixels[offset + 1] = 214; pixels[offset + 2] = 42; pixels[offset + 3] = 150;
            }
            _mask = BitmapSource.Create(ImageWidth, ImageHeight, 96, 96, PixelFormats.Bgra32, null, pixels, ImageWidth * 4);
            _mask.Freeze();
        }
        // Source membership uses the same viewport geometry as the original image.
        var viewport = ColorStudioSampleMapping.Viewport(RenderSize, ViewMode == "并排对比", false);
        var imageRect = ViewState is null ? viewport : ViewState.ImageRect(viewport);
        dc.PushClip(new RectangleGeometry(viewport));
        dc.DrawImage(_mask, imageRect);
        dc.Pop();
    }
}
