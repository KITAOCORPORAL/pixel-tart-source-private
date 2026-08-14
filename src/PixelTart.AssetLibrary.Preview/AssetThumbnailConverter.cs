using System.Globalization;
using System.IO;
using System.Collections.Concurrent;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PixelTart.AssetLibrary.Preview;

public sealed class AssetThumbnailConverter : IValueConverter
{
    private const int MaxEntries = 128;
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> CacheOrder = new();
    private static readonly object CacheGate = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;
        if (Path.GetExtension(path).ToUpperInvariant() is not (".JPG" or ".JPEG" or ".PNG" or ".WEBP" or ".TIF" or ".TIFF")) return null;
        var key = Path.GetFullPath(path);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        try
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 280; image.UriSource = new Uri(key); image.EndInit(); image.Freeze();
            lock (CacheGate)
            {
                if (!Cache.ContainsKey(key))
                {
                    while (Cache.Count >= MaxEntries && CacheOrder.TryDequeue(out var oldest)) Cache.TryRemove(oldest, out _);
                    Cache[key] = image; CacheOrder.Enqueue(key);
                }
                return Cache.TryGetValue(key, out var stored) ? stored : image;
            }
        }
        catch { return null; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
