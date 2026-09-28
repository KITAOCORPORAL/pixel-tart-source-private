using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Services;

/// <summary>Display-only RGB24 adapter. Never feed this bitmap into a professional TIFF export.</summary>
public static class RawDisplayBitmapAdapter
{
    public static BitmapSource ToBitmap(VisualPixelBuffer pixels)
    {
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Rgb24, null,
            pixels.Rgb24.ToArray(), checked(pixels.Width * 3));
        bitmap.Freeze();
        return bitmap;
    }
}
