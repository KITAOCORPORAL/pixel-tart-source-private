using System.IO;
using System.Windows.Media.Imaging;

namespace PixelTart.Modules.AssetLibrary;

internal sealed class AssetQuickLoupePreviewProvider
{
    internal const int DecodePixelWidth = 1600;
    private const int CacheLimit = 4;
    private readonly Dictionary<string, BitmapSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _recent = new();

    internal async Task<BitmapSource> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(path, out var cached)) { Touch(path); return cached; }
        var bitmap = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path)) throw new FileNotFoundException("Quick preview source is unavailable.", path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = DecodePixelWidth;
            image.UriSource = new Uri(Path.GetFullPath(path));
            image.EndInit();
            image.Freeze();
            cancellationToken.ThrowIfCancellationRequested();
            return (BitmapSource)image;
        }, cancellationToken);
        _cache[path] = bitmap;
        Touch(path);
        while (_cache.Count > CacheLimit && _recent.First is { } oldest)
        {
            _recent.RemoveFirst();
            _cache.Remove(oldest.Value);
        }
        return bitmap;
    }

    private void Touch(string path)
    {
        _recent.Remove(path);
        _recent.AddLast(path);
    }
}
