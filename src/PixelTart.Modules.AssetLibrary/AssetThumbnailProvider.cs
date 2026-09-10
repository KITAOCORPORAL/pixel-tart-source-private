using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace PixelTart.Modules.AssetLibrary;

public sealed record AssetThumbnailRequest(string SourcePath, int DecodePixelWidth);

/// <summary>Single thumbnail loading seam shared by the library and future creative surfaces.</summary>
public interface IAssetThumbnailProvider
{
    Task<BitmapSource> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Bounded, fingerprinted WPF thumbnail provider used by every asset-library thumbnail.</summary>
public sealed class WpfAssetThumbnailProvider : IAssetThumbnailProvider
{
    private const long MaxCacheBytes = 64L * 1024 * 1024;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    private long _cacheBytes;

    public Task<BitmapSource> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = Path.GetFullPath(request.SourcePath);
        var width = Math.Clamp(request.DecodePixelWidth, 96, 512);
        return Task.Run(() => GetOrDecode(path, width, cancellationToken), cancellationToken);
    }

    private BitmapSource GetOrDecode(string path, int width, CancellationToken cancellationToken)
    {
        var key = Fingerprint(path, width, cancellationToken);
        if (_cache.TryGetValue(key, out var cached)) return cached.Bitmap;
        var bitmap = Decode(path, width, cancellationToken);
        return AddCache(key, bitmap).Bitmap;
    }

    private static string Fingerprint(string path, int width, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new FileInfo(path);
        var text = $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{width}|thumb-v1";
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }

    private static BitmapImage Decode(string path, int width, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = width;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        cancellationToken.ThrowIfCancellationRequested();
        return image;
    }

    private CacheEntry AddCache(string key, BitmapImage bitmap)
    {
        var bytes = Math.Max(1L, bitmap.PixelWidth * (long)bitmap.PixelHeight * 4);
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                _lru.Remove(key);
                _lru.AddLast(key);
                return existing;
            }

            while (_cacheBytes + bytes > MaxCacheBytes && _lru.First is { } oldest)
            {
                _lru.RemoveFirst();
                if (_cache.TryRemove(oldest.Value, out var removed)) _cacheBytes -= removed.Bytes;
            }

            var entry = new CacheEntry(bitmap, bytes);
            _cache[key] = entry;
            _lru.AddLast(key);
            _cacheBytes += bytes;
            return entry;
        }
    }

    private sealed record CacheEntry(BitmapImage Bitmap, long Bytes);
}
