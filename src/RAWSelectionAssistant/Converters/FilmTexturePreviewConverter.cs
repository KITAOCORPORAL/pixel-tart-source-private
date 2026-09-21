using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RAWSelectionAssistant.Converters;

public sealed class FilmTexturePreviewConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Cache.GetOrAdd(value?.ToString() ?? "None", Render);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    private static ImageSource Render(string id)
    {
        const int width = 100, height = 60; var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) { var noise = Noise(id, x, y); var shade = (byte)Math.Clamp(92 + noise * 52, 32, 180); var offset = (y * width + x) * 4; pixels[offset] = (byte)(shade + 3); pixels[offset + 1] = shade; pixels[offset + 2] = (byte)Math.Max(0, shade - 4); pixels[offset + 3] = 255; }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); bitmap.Freeze(); return bitmap;
    }
    private static double Noise(string id, int x, int y)
    {
        if (id == "None") return 0;
        static double H(int a, int b, int seed) { unchecked { var n = a * 374761393 + b * 668265263 + seed * 1274126177; n ^= n >> 13; return ((n * 15731) & 1023) / 511.5 - 1; } }
        return id switch { "FineFiber" => H(x / 2, y / 9, 11) * .65 + H(x, y / 4, 13) * .25, "Paper" => H(x / 7, y / 7, 17) * .55 + H(x / 2, y / 2, 19) * .25, "SoftMist" => H(x / 18, y / 18, 23) * .75 + H(x / 8, y / 8, 29) * .15, "Scanline" => H(x / 5, (y + (int)(H(x / 9, 0, 31) * 2)) / 3, 37) * .55 + ((y % 5) - 2) * .08, _ => 0 };
    }
}
