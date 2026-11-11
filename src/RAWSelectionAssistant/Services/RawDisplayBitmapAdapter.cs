using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Services;

/// <summary>Display-only RGB24 adapter. Never feed this bitmap into a professional TIFF export.</summary>
public static class RawDisplayBitmapAdapter
{
    public static BitmapSource ToBitmap(VisualPixelBuffer pixels)
    {
        // WIC copies the pixels during Create; an exact backing array needs no managed duplicate.
        // A slice still needs a copy so its offset and length remain authoritative.
        var data = MemoryMarshal.TryGetArray(pixels.Rgb24, out ArraySegment<byte> segment)
            && segment.Offset == 0 && segment.Array is not null && segment.Count == segment.Array.Length
            ? segment.Array : pixels.Rgb24.ToArray();
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Rgb24, null,
            data, checked(pixels.Width * 3));
        bitmap.Freeze();
        return bitmap;
    }
}
