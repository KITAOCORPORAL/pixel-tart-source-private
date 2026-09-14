using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace PixelTart.Modules.AssetLibrary;

public enum AssetThumbnailState
{
    Available,
    Missing,
    Offline
}

public sealed record AssetThumbnailRequest(
    string? SourcePath,
    int DecodePixelWidth,
    AssetThumbnailState KnownState = AssetThumbnailState.Available,
    Guid? AssetId = null,
    string? ContentHash = null,
    DateTimeOffset? SourceModifiedUtc = null,
    string? CacheDirectory = null,
    string? Orientation = null);

public sealed record AssetThumbnailResult(
    AssetThumbnailState State,
    BitmapSource? Bitmap = null,
    string? PlaceholderMessage = null)
{
    public bool IsAvailable => State == AssetThumbnailState.Available && Bitmap is not null;
}

/// <summary>Single thumbnail loading seam shared by the library and future creative surfaces.</summary>
public interface IAssetThumbnailProvider
{
    Task<AssetThumbnailResult> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Bounded, fingerprinted WPF thumbnail provider used by every asset-library thumbnail.</summary>
public sealed class WpfAssetThumbnailProvider : IAssetThumbnailProvider
{
    private const long MaxCacheBytes = 64L * 1024 * 1024;
    private const long MaxDiskCacheBytes = 512L * 1024 * 1024;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    private long _cacheBytes;
    private readonly string? _defaultCacheDirectory;

    public WpfAssetThumbnailProvider(string? diskCacheDirectory = null)
    {
        _defaultCacheDirectory = string.IsNullOrWhiteSpace(diskCacheDirectory) ? null : Path.GetFullPath(diskCacheDirectory);
    }

    public Task<AssetThumbnailResult> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var width = Math.Clamp(request.DecodePixelWidth, 96, 512);
        return Task.Run(
            () => GetThumbnail(request, width, cancellationToken),
            cancellationToken);
    }

    private AssetThumbnailResult GetThumbnail(AssetThumbnailRequest request, int width, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(request.SourcePath) ? null : Path.GetFullPath(request.SourcePath);
        var key = Fingerprint(request, path, width, cancellationToken);
        var cacheDirectory = request.CacheDirectory ?? _defaultCacheDirectory;
        if (TryGetMemory(key, out var memory)) return new(AssetThumbnailState.Available, memory);
        if (TryLoadDisk(cacheDirectory, key, out var disk)) return new(AssetThumbnailState.Available, AddCache(key, disk).Bitmap);
        if (request.KnownState != AssetThumbnailState.Available || path is null || !File.Exists(path))
            return new(request.KnownState == AssetThumbnailState.Offline ? AssetThumbnailState.Offline : AssetThumbnailState.Missing,
                PlaceholderMessage: request.KnownState == AssetThumbnailState.Offline ? "素材库离线。" : "缩略图不可用：文件不存在。");
        var bitmap = Decode(path, width, cancellationToken);
        WriteDisk(cacheDirectory, key, bitmap);
        return new(AssetThumbnailState.Available, AddCache(key, bitmap).Bitmap);
    }

    private static string Fingerprint(AssetThumbnailRequest request, string? path, int width, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = path is not null && File.Exists(path) ? new FileInfo(path) : null;
        // A content hash is the durable identity for offline references. When it is
        // available, do not make the cache key depend on metadata that cannot be read
        // after the source drive is disconnected.
        var modified = request.SourceModifiedUtc?.UtcTicks ?? (string.IsNullOrWhiteSpace(request.ContentHash) ? info?.LastWriteTimeUtc.Ticks ?? 0 : 0);
        var length = string.IsNullOrWhiteSpace(request.ContentHash) ? info?.Length ?? 0 : 0;
        var durableIdentity = !string.IsNullOrWhiteSpace(request.ContentHash)
            ? $"{request.AssetId:D}|{request.ContentHash}"
            : path;
        var text = $"{durableIdentity}|{length}|{modified}|{width}|{request.Orientation}|thumb-v3";
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }

    private bool TryGetMemory(string key, out BitmapSource bitmap)
    {
        if (_cache.TryGetValue(key, out var cached)) { bitmap = cached.Bitmap; return true; }
        bitmap = null!; return false;
    }

    private static bool TryLoadDisk(string? directory, string key, out BitmapImage bitmap)
    {
        bitmap = null!;
        if (string.IsNullOrWhiteSpace(directory)) return false;
        var path = Path.Combine(directory, key + ".png");
        try
        {
            if (!File.Exists(path)) return false;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); bitmap = image; return true;
        }
        catch (IOException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static void WriteDisk(string? directory, string key, BitmapImage bitmap)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, key + ".png");
            if (!File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
            }
            TrimDisk(directory);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TrimDisk(string directory)
    {
        var files = new DirectoryInfo(directory).EnumerateFiles("*.png").OrderBy(file => file.LastAccessTimeUtc).ToList();
        long total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (total <= MaxDiskCacheBytes) break;
            try { total -= file.Length; file.Delete(); } catch (IOException) { }
        }
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
