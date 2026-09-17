using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelTart.Modules.AssetLibrary;

public enum AssetThumbnailState
{
    Available,
    Missing,
    Offline
}

public enum AssetPreviewPurpose
{
    GalleryThumbnail,
    QuickLoupe,
    ViewerPreview,
    Original,
    Canvas
}

public enum AssetPreviewQuality
{
    Balanced,
    High,
    Original
}

public sealed record AssetPreviewRequest(
    string? SourcePath,
    AssetPreviewPurpose Purpose,
    AssetPreviewQuality Quality = AssetPreviewQuality.Balanced,
    int RequestedPixelWidth = 0,
    AssetThumbnailState KnownState = AssetThumbnailState.Available,
    Guid? AssetId = null,
    string? ContentHash = null,
    DateTimeOffset? SourceModifiedUtc = null,
    string? CacheDirectory = null,
    string? Orientation = null);

public sealed record AssetPreviewResult(
    AssetThumbnailState State,
    AssetPreviewPurpose Purpose,
    AssetPreviewQuality Quality,
    BitmapSource? Bitmap = null,
    string? PlaceholderMessage = null)
{
    public bool IsAvailable => State == AssetThumbnailState.Available && Bitmap is not null;
}

public interface IAssetPreviewProvider
{
    Task<AssetPreviewResult> GetAsync(AssetPreviewRequest request, CancellationToken cancellationToken = default);
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

/// <summary>Compatibility seam for existing gallery bindings. The implementation delegates to the unified preview provider.</summary>
public interface IAssetThumbnailProvider
{
    Task<AssetThumbnailResult> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// One cancellable, byte-budgeted preview pipeline for gallery thumbnails, Quick Loupe,
/// full preview and original pixels. Viewer and Loupe deliberately own no image cache.
/// </summary>
public sealed class WpfAssetThumbnailProvider : IAssetPreviewProvider, IAssetThumbnailProvider
{
    public const long DefaultMemoryBudgetBytes = 64L * 1024 * 1024;
    public const long DefaultDiskBudgetBytes = 512L * 1024 * 1024;

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    private readonly string? _defaultCacheDirectory;
    private readonly long _memoryBudgetBytes;
    private readonly long _diskBudgetBytes;
    private long _cacheBytes;

    public WpfAssetThumbnailProvider(
        string? diskCacheDirectory = null,
        long memoryBudgetBytes = DefaultMemoryBudgetBytes,
        long diskBudgetBytes = DefaultDiskBudgetBytes)
    {
        _defaultCacheDirectory = string.IsNullOrWhiteSpace(diskCacheDirectory) ? null : Path.GetFullPath(diskCacheDirectory);
        _memoryBudgetBytes = Math.Max(4L * 1024 * 1024, memoryBudgetBytes);
        _diskBudgetBytes = Math.Max(16L * 1024 * 1024, diskBudgetBytes);
    }

    public long MemoryBudgetBytes => _memoryBudgetBytes;
    public long CachedBytes { get { lock (_gate) return _cacheBytes; } }

    public async Task<AssetThumbnailResult> GetAsync(AssetThumbnailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await GetAsync(new AssetPreviewRequest(
            request.SourcePath,
            AssetPreviewPurpose.GalleryThumbnail,
            AssetPreviewQuality.Balanced,
            request.DecodePixelWidth,
            request.KnownState,
            request.AssetId,
            request.ContentHash,
            request.SourceModifiedUtc,
            request.CacheDirectory,
            request.Orientation), cancellationToken).ConfigureAwait(false);
        return new(result.State, result.Bitmap, result.PlaceholderMessage);
    }

    public Task<AssetPreviewResult> GetAsync(AssetPreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var width = ResolveDecodeWidth(request);
        return Task.Run(() => GetPreview(request, width, cancellationToken), cancellationToken);
    }

    private AssetPreviewResult GetPreview(AssetPreviewRequest request, int width, CancellationToken cancellationToken)
    {
        var path = string.IsNullOrWhiteSpace(request.SourcePath) ? null : Path.GetFullPath(request.SourcePath);
        var key = Fingerprint(request, path, width, cancellationToken);
        var cacheDirectory = request.CacheDirectory ?? _defaultCacheDirectory;

        if (TryGetMemory(key, out var memory))
            return new(AssetThumbnailState.Available, request.Purpose, request.Quality, memory);
        if (request.Purpose != AssetPreviewPurpose.Original && TryLoadDisk(cacheDirectory, key, out var disk))
            return new(AssetThumbnailState.Available, request.Purpose, request.Quality, AddCache(key, disk));
        if (request.KnownState != AssetThumbnailState.Available || path is null || !File.Exists(path))
            return new(
                request.KnownState == AssetThumbnailState.Offline ? AssetThumbnailState.Offline : AssetThumbnailState.Missing,
                request.Purpose,
                request.Quality,
                PlaceholderMessage: request.KnownState == AssetThumbnailState.Offline ? "素材库离线。" : "图片不可用：文件不存在。");

        var bitmap = Decode(path, width, cancellationToken);
        if (request.Purpose != AssetPreviewPurpose.Original) WriteDisk(cacheDirectory, key, bitmap);
        return new(AssetThumbnailState.Available, request.Purpose, request.Quality, AddCache(key, bitmap));
    }

    private static int ResolveDecodeWidth(AssetPreviewRequest request) => request.Purpose switch
    {
        AssetPreviewPurpose.GalleryThumbnail => Math.Clamp(request.RequestedPixelWidth <= 0 ? 320 : request.RequestedPixelWidth, 96, 512),
        AssetPreviewPurpose.QuickLoupe => Math.Clamp(request.RequestedPixelWidth <= 0 ? 1600 : request.RequestedPixelWidth, 768, 2048),
        AssetPreviewPurpose.ViewerPreview => Math.Clamp(request.RequestedPixelWidth <= 0 ? 2048 : request.RequestedPixelWidth, 1024, 3072),
        AssetPreviewPurpose.Original => 0,
        AssetPreviewPurpose.Canvas => Math.Clamp(request.RequestedPixelWidth <= 0 ? 1024 : request.RequestedPixelWidth, 256, 4096),
        _ => 512
    };

    private static string Fingerprint(AssetPreviewRequest request, string? path, int width, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = path is not null && File.Exists(path) ? new FileInfo(path) : null;
        var modified = request.SourceModifiedUtc?.UtcTicks ?? (string.IsNullOrWhiteSpace(request.ContentHash) ? info?.LastWriteTimeUtc.Ticks ?? 0 : 0);
        var length = string.IsNullOrWhiteSpace(request.ContentHash) ? info?.Length ?? 0 : 0;
        var durableIdentity = !string.IsNullOrWhiteSpace(request.ContentHash) ? $"{request.AssetId:D}|{request.ContentHash}" : path;
        var text = $"{durableIdentity}|{length}|{modified}|{request.Purpose}|{request.Quality}|{width}|{request.Orientation}|preview-v1";
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }

    private bool TryGetMemory(string key, out BitmapSource bitmap)
    {
        if (!_cache.TryGetValue(key, out var cached)) { bitmap = null!; return false; }
        lock (_gate)
        {
            _lru.Remove(key);
            _lru.AddLast(key);
        }
        bitmap = cached.Bitmap;
        return true;
    }

    private static bool TryLoadDisk(string? directory, string key, out BitmapImage bitmap)
    {
        bitmap = null!;
        if (string.IsNullOrWhiteSpace(directory)) return false;
        var path = Path.Combine(directory, key + ".png");
        try
        {
            if (!File.Exists(path)) return false;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
            bitmap = image;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private void WriteDisk(string? directory, string key, BitmapSource bitmap)
    {
        if (string.IsNullOrWhiteSpace(directory)) return;
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, key + ".png");
            if (!File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
            TrimDisk(directory);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void TrimDisk(string directory)
    {
        var files = new DirectoryInfo(directory).EnumerateFiles("*.png").OrderBy(file => file.LastAccessTimeUtc).ToList();
        long total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (total <= _diskBudgetBytes) break;
            try { total -= file.Length; file.Delete(); } catch (IOException) { }
        }
    }

    private static BitmapImage Decode(string path, int width, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (width > 0) image.DecodePixelWidth = width;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        cancellationToken.ThrowIfCancellationRequested();
        return image;
    }

    private BitmapSource AddCache(string key, BitmapSource bitmap)
    {
        var bytesPerPixel = Math.Max(1, (bitmap.Format.BitsPerPixel + 7) / 8);
        var bytes = Math.Max(1L, bitmap.PixelWidth * (long)bitmap.PixelHeight * bytesPerPixel);
        if (bytes > _memoryBudgetBytes) return bitmap;

        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                _lru.Remove(key);
                _lru.AddLast(key);
                return existing.Bitmap;
            }
            while (_cacheBytes + bytes > _memoryBudgetBytes && _lru.First is { } oldest)
            {
                _lru.RemoveFirst();
                if (_cache.TryRemove(oldest.Value, out var removed)) _cacheBytes -= removed.Bytes;
            }
            _cache[key] = new(bitmap, bytes);
            _lru.AddLast(key);
            _cacheBytes += bytes;
            return bitmap;
        }
    }

    private sealed record CacheEntry(BitmapSource Bitmap, long Bytes);
}
