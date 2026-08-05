using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MetadataExtractor;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

public sealed record PreviewImageLoadResult(
    Guid AssetId,
    BitmapSource? Image,
    bool IsPlaceholder,
    bool UsedPairedPreview,
    string? ErrorCode = null,
    string? Message = null);

public interface IPreviewImageLoader
{
    Task<PreviewImageLoadResult> LoadAsync(TetherAssetRecord asset, int decodePixelWidth = 2048, CancellationToken cancellationToken = default);
}

public interface IFullResolutionImageLoader
{
    Task<PreviewImageLoadResult> LoadAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default);
    void ReleaseExcept(Guid? assetId);
}

public interface IHistogramService
{
    Task<TetherHistogramData> CalculateAsync(BitmapSource image, bool basedOnProxy, CancellationToken cancellationToken = default);
}

public interface IClippingOverlayService
{
    Task<BitmapSource?> CreateAsync(BitmapSource image, bool highlightEnabled, int highlightThreshold, bool shadowEnabled, int shadowThreshold, CancellationToken cancellationToken = default);
}

public interface IPreviewRequestCoordinator : IDisposable
{
    PreviewRequest Begin(Guid assetId, CancellationToken outerToken = default);
    bool IsCurrent(Guid assetId, long version);
    void CancelCurrent();
}

public interface IPreviewMemoryManager
{
    bool TryGet(Guid assetId, out BitmapSource? image);
    void Add(Guid assetId, BitmapSource image);
    void ReleaseExcept(Guid? assetId);
    void Clear();
    int CachedImageCount { get; }
    long EstimatedBytes { get; }
}

public sealed record PreviewRequest(Guid AssetId, long Version, CancellationToken Token);

public sealed class PreviewRequestCoordinator : IPreviewRequestCoordinator
{
    private readonly object _sync = new();
    private CancellationTokenSource? _current;
    private Guid _assetId;
    private long _version;

    public PreviewRequest Begin(Guid assetId, CancellationToken outerToken = default)
    {
        lock (_sync)
        {
            _current?.Cancel();
            _current?.Dispose();
            _current = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
            _assetId = assetId;
            return new(assetId, ++_version, _current.Token);
        }
    }

    public bool IsCurrent(Guid assetId, long version)
    {
        lock (_sync) return _assetId == assetId && _version == version && _current?.IsCancellationRequested == false;
    }

    public void CancelCurrent()
    {
        lock (_sync) _current?.Cancel();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _current?.Cancel();
            _current?.Dispose();
            _current = null;
        }
    }
}

public sealed class PreviewMemoryManager(int maximumImages = 3, long maximumBytes = 384L * 1024 * 1024) : IPreviewMemoryManager
{
    private sealed record CacheEntry(BitmapSource Image, long Bytes, long Sequence);
    private readonly object _sync = new();
    private readonly Dictionary<Guid, CacheEntry> _cache = [];
    private readonly int _maximumImages = Math.Max(1, maximumImages);
    private readonly long _maximumBytes = Math.Max(32L * 1024 * 1024, maximumBytes);
    private long _sequence;

    public int CachedImageCount { get { lock (_sync) return _cache.Count; } }
    public long EstimatedBytes { get { lock (_sync) return _cache.Values.Sum(item => item.Bytes); } }

    public bool TryGet(Guid assetId, out BitmapSource? image)
    {
        lock (_sync)
        {
            if (!_cache.TryGetValue(assetId, out var entry)) { image = null; return false; }
            _cache[assetId] = entry with { Sequence = ++_sequence };
            image = entry.Image;
            return true;
        }
    }

    public void Add(Guid assetId, BitmapSource image)
    {
        var bytes = checked((long)image.PixelWidth * image.PixelHeight * Math.Max(4, (image.Format.BitsPerPixel + 7) / 8));
        lock (_sync)
        {
            _cache[assetId] = new(image, bytes, ++_sequence);
            Trim(assetId);
        }
    }

    public void ReleaseExcept(Guid? assetId)
    {
        lock (_sync)
            foreach (var key in _cache.Keys.Where(key => key != assetId).ToArray()) _cache.Remove(key);
    }

    public void Clear() { lock (_sync) _cache.Clear(); }

    private void Trim(Guid protectedAssetId)
    {
        while (_cache.Count > _maximumImages || _cache.Values.Sum(item => item.Bytes) > _maximumBytes)
        {
            var candidate = _cache.Where(pair => pair.Key != protectedAssetId).OrderBy(pair => pair.Value.Sequence).FirstOrDefault();
            if (candidate.Key == Guid.Empty) break;
            _cache.Remove(candidate.Key);
        }
    }
}

public sealed class PreviewImageLoader(ITetherProxyCache proxyCache, ITetherAssetRepository assetRepository) : IPreviewImageLoader
{
    public async Task<PreviewImageLoadResult> LoadAsync(TetherAssetRecord asset, int decodePixelWidth = 2048, CancellationToken cancellationToken = default)
    {
        try
        {
            var (previewAsset, paired) = await ResolvePreviewAssetAsync(asset, assetRepository, cancellationToken).ConfigureAwait(false);
            if (previewAsset is null) return new(asset.Id, null, true, false, ErrorCodeCatalog.RawPreviewUnavailable, "RAW尚无可用的配对JPG预览。");
            var key = previewAsset.ProxyCacheKey;
            var proxyPath = proxyCache.ResolvePath(key);
            if (proxyPath is null)
            {
                key = await proxyCache.GetOrCreateAsync(previewAsset, cancellationToken).ConfigureAwait(false);
                proxyPath = proxyCache.ResolvePath(key);
            }
            if (proxyPath is null) return new(asset.Id, null, true, paired, ErrorCodeCatalog.DecodeFailed, "监看代理图不可用。");
            var image = await BitmapFileLoader.LoadAsync(proxyPath, Math.Clamp(decodePixelWidth, 64, 2048), cancellationToken).ConfigureAwait(false);
            return new(asset.Id, image, false, paired);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            return new(asset.Id, null, true, false, ErrorCodeCatalog.DecodeFailed, "监看代理图加载失败。");
        }
    }

    internal static async Task<(TetherAssetRecord? Asset, bool Paired)> ResolvePreviewAssetAsync(TetherAssetRecord asset, ITetherAssetRepository repository, CancellationToken cancellationToken)
    {
        if (asset.MediaKind == TetherMediaKind.PreviewImage) return (asset, false);
        if (asset.MediaKind != TetherMediaKind.Raw || !asset.PairedAssetId.HasValue) return (null, false);
        var paired = await repository.GetAsync(asset.PairedAssetId.Value, cancellationToken).ConfigureAwait(false);
        return paired?.MediaKind == TetherMediaKind.PreviewImage ? (paired, true) : (null, false);
    }
}

public sealed class FullResolutionImageLoader(ITetherAssetRepository assetRepository, IPreviewMemoryManager memoryManager) : IFullResolutionImageLoader
{
    public async Task<PreviewImageLoadResult> LoadAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default)
    {
        if (memoryManager.TryGet(asset.Id, out var cached) && cached is not null) return new(asset.Id, cached, false, asset.MediaKind == TetherMediaKind.Raw);
        try
        {
            var (sourceAsset, paired) = await PreviewImageLoader.ResolvePreviewAssetAsync(asset, assetRepository, cancellationToken).ConfigureAwait(false);
            if (sourceAsset is null) return new(asset.Id, null, true, false, ErrorCodeCatalog.RawPreviewUnavailable, "RAW没有配对JPG，无法进行100%查看。");
            if (!File.Exists(sourceAsset.SourcePath)) return new(asset.Id, null, true, paired, ErrorCodeCatalog.SourceNotFound, "原文件暂时不可访问。");
            var image = await BitmapFileLoader.LoadAsync(sourceAsset.SourcePath, null, cancellationToken).ConfigureAwait(false);
            memoryManager.Add(asset.Id, image);
            return new(asset.Id, image, false, paired);
        }
        catch (OperationCanceledException) { throw; }
        catch (OutOfMemoryException)
        {
            memoryManager.Clear();
            return new(asset.Id, null, true, false, ErrorCodeCatalog.DecodeFailed, "内存压力较高，已回退监看代理图。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            return new(asset.Id, null, true, false, ErrorCodeCatalog.DecodeFailed, "100%图像加载失败。");
        }
    }

    public void ReleaseExcept(Guid? assetId) => memoryManager.ReleaseExcept(assetId);
}

internal static class BitmapFileLoader
{
    public static Task<BitmapSource> LoadAsync(string path, int? decodePixelWidth, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            if (decodePixelWidth is > 0) bitmap.DecodePixelWidth = decodePixelWidth.Value;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            cancellationToken.ThrowIfCancellationRequested();
            return (BitmapSource)bitmap;
        }, cancellationToken);
}

public sealed class HistogramService : IHistogramService
{
    public Task<TetherHistogramData> CalculateAsync(BitmapSource image, bool basedOnProxy, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var source = EnsureBgra32(image);
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            var red = new int[256]; var green = new int[256]; var blue = new int[256]; var luminance = new int[256];
            for (var y = 0; y < source.PixelHeight; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                var row = y * stride;
                for (var x = 0; x < source.PixelWidth; x++)
                {
                    var offset = row + x * 4;
                    var b = pixels[offset]; var g = pixels[offset + 1]; var r = pixels[offset + 2];
                    blue[b]++; green[g]++; red[r]++; luminance[(54 * r + 183 * g + 19 * b) >> 8]++;
                }
            }
            return new TetherHistogramData(red, green, blue, luminance, basedOnProxy);
        }, cancellationToken);

    internal static BitmapSource EnsureBgra32(BitmapSource image)
    {
        if (image.Format == PixelFormats.Bgra32 || image.Format == PixelFormats.Pbgra32) return image;
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }
}

public sealed class ClippingOverlayService : IClippingOverlayService
{
    public Task<BitmapSource?> CreateAsync(BitmapSource image, bool highlightEnabled, int highlightThreshold, bool shadowEnabled, int shadowThreshold, CancellationToken cancellationToken = default)
    {
        if (!highlightEnabled && !shadowEnabled) return Task.FromResult<BitmapSource?>(null);
        return Task.Run(() =>
        {
            var source = HistogramService.EnsureBgra32(image);
            var stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            var overlay = new byte[pixels.Length];
            source.CopyPixels(pixels, stride, 0);
            var high = Math.Clamp(highlightThreshold, 1, 255); var low = Math.Clamp(shadowThreshold, 0, 254);
            for (var y = 0; y < source.PixelHeight; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                var row = y * stride;
                for (var x = 0; x < source.PixelWidth; x++)
                {
                    var offset = row + x * 4;
                    var b = pixels[offset]; var g = pixels[offset + 1]; var r = pixels[offset + 2];
                    if (highlightEnabled && Math.Max(r, Math.Max(g, b)) >= high)
                    {
                        overlay[offset] = 40; overlay[offset + 1] = 40; overlay[offset + 2] = 255; overlay[offset + 3] = 180;
                    }
                    else if (shadowEnabled && Math.Min(r, Math.Min(g, b)) <= low)
                    {
                        overlay[offset] = 255; overlay[offset + 1] = 190; overlay[offset + 2] = 20; overlay[offset + 3] = 180;
                    }
                }
            }
            var bitmap = BitmapSource.Create(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, overlay, stride);
            bitmap.Freeze();
            return (BitmapSource?)bitmap;
        }, cancellationToken);
    }
}

public interface ITetherExifService
{
    Task<TetherExifInfo> ReadAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default);
}

public sealed class TetherExifService : ITetherExifService
{
    public Task<TetherExifInfo> ReadAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(asset, cancellationToken), cancellationToken);

    private static TetherExifInfo Read(TetherAssetRecord asset, CancellationToken cancellationToken)
    {
        var fallback = TetherExifInfo.Unavailable(asset);
        if (!File.Exists(asset.SourcePath)) return fallback;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new FileStream(asset.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var directories = ImageMetadataReader.ReadMetadata(stream);
            string Tag(params string[] names) => directories.SelectMany(directory => directory.Tags)
                .FirstOrDefault(tag => names.Any(name => string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase)))?.Description?.Trim() ?? "未提供";
            var dimensions = Dimensions(asset.SourcePath, asset.MediaKind);
            return new(
                fallback.FileType,
                Tag("Date/Time Original", "Date/Time Digitized") is { } capture && capture != "未提供" ? capture : fallback.CaptureTime,
                Tag("Make"), Tag("Model"), Tag("Lens Model", "Lens"), Tag("Focal Length"), Tag("F-Number", "Aperture Value"),
                Tag("Exposure Time", "Shutter Speed Value"), Tag("ISO Speed Ratings", "ISO Speed"), Tag("Exposure Bias Value"),
                Tag("White Balance Mode", "White Balance"), Tag("Color Space"), dimensions, fallback.FileSize, fallback.PairingStatus, asset.SourcePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ImageProcessingException or NotSupportedException)
        {
            return fallback;
        }
    }

    private static string Dimensions(string path, TetherMediaKind kind)
    {
        if (kind != TetherMediaKind.PreviewImage) return "未提供";
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
            return $"{frame.PixelWidth} × {frame.PixelHeight}";
        }
        catch { return "未提供"; }
    }
}

public interface ITetherDisplaySettingsStore
{
    Task<TetherDisplaySettings> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task SaveAsync(TetherDisplaySettings settings, CancellationToken cancellationToken = default);
}

public sealed class JsonTetherDisplaySettingsStore(string? root = null) : ITetherDisplaySettingsStore
{
    private readonly string _root = root ?? AppDataPaths.TetherDisplaySettingsDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<TetherDisplaySettings> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = PathFor(sessionId);
            if (!File.Exists(path)) return new(sessionId);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<TetherDisplaySettings>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? new(sessionId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(sessionId); }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(TetherDisplaySettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            System.IO.Directory.CreateDirectory(_root);
            var path = PathFor(settings.SessionId);
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { _gate.Release(); }
    }

    private string PathFor(Guid sessionId) => Path.Combine(_root, sessionId.ToString("N") + ".json");
}
