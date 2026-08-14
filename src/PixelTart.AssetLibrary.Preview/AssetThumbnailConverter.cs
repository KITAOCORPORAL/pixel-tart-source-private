using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PixelTart.AssetLibrary.Preview;

public sealed class AssetThumbnailConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;
        if (Path.GetExtension(path).ToUpperInvariant() is not (".JPG" or ".JPEG" or ".PNG" or ".WEBP" or ".TIF" or ".TIFF")) return null;
        try { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 280; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); return image; }
        catch { return null; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
