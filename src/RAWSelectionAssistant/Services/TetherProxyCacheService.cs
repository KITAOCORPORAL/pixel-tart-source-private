using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

public sealed class TetherProxyCacheService : ITetherProxyCache
{
    private const int LongestEdge = 2048;
    private const long MaximumCacheBytes = 512L * 1024 * 1024;
    private readonly string _cacheRoot;
    private readonly long _maximumCacheBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TetherProxyCacheService(string? cacheRoot = null, long? maximumCacheBytes = null)
    {
        _cacheRoot = cacheRoot ?? AppDataPaths.TetherProxyCacheDirectory;
        _maximumCacheBytes = maximumCacheBytes ?? MaximumCacheBytes;
        if (_maximumCacheBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCacheBytes));
    }

    public async Task<string?> GetOrCreateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default)
    {
        if (asset.MediaKind != TetherMediaKind.PreviewImage || !File.Exists(asset.SourcePath)) return null;
        var key = CreateOpaqueKey(asset);
        var destination = Path.Combine(_cacheRoot, key + ".jpg");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_cacheRoot);
            if (File.Exists(destination))
            {
                try
                {
                    ValidateProxy(destination);
                    File.SetLastAccessTimeUtc(destination, DateTime.UtcNow);
                    return key;
                }
                catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
                {
                    TryDeleteCacheFile(destination);
                }
            }

            var temporary = Path.Combine(_cacheRoot, key + ".tmp-" + Guid.NewGuid().ToString("N"));
            try
            {
                await Task.Run(() => RenderProxy(asset.SourcePath, temporary), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                try { File.Move(temporary, destination); }
                catch (IOException) when (File.Exists(destination)) { TryDeleteCacheFile(temporary); }
                await TrimAsync(cancellationToken).ConfigureAwait(false);
                return key;
            }
            catch
            {
                TryDeleteCacheFile(temporary);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public string? ResolvePath(string? cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || cacheKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || cacheKey.Contains("..", StringComparison.Ordinal)) return null;
        var path = Path.Combine(_cacheRoot, cacheKey + ".jpg");
        return File.Exists(path) ? path : null;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_cacheRoot)) return;
            foreach (var path in Directory.EnumerateFiles(_cacheRoot, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryDeleteCacheFile(path);
            }
        }
        finally { _gate.Release(); }
    }

    private async Task TrimAsync(CancellationToken cancellationToken)
    {
        var files = new DirectoryInfo(_cacheRoot).EnumerateFiles("*.jpg", SearchOption.TopDirectoryOnly).OrderBy(file => file.LastAccessTimeUtc).ToArray();
        var total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (total <= _maximumCacheBytes) break;
            cancellationToken.ThrowIfCancellationRequested();
            var length = file.Length;
            TryDeleteCacheFile(file.FullName);
            total -= length;
        }
        await Task.CompletedTask;
    }

    private static void RenderProxy(string sourcePath, string destinationPath)
    {
        BitmapFrame frame;
        using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            frame = BitmapFrame.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        BitmapSource bitmap = NormalizeEmbeddedProfile(frame);
        var longest = Math.Max(frame.PixelWidth, frame.PixelHeight);
        if (longest > LongestEdge)
        {
            var scale = LongestEdge / (double)longest;
            var transformed = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            transformed.Freeze();
            bitmap = transformed;
        }
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(output);
        output.Flush(true);
    }

    private static BitmapSource NormalizeEmbeddedProfile(BitmapFrame frame)
    {
        if (frame.ColorContexts is not { Count: > 0 }) return frame;
        try
        {
            var converted = new ColorConvertedBitmap(frame, frame.ColorContexts[0], new ColorContext(PixelFormats.Bgra32), PixelFormats.Bgra32);
            converted.Freeze();
            return converted;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or FileFormatException)
        {
            return frame;
        }
    }

    private static void ValidateProxy(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        _ = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
    }

    private static string CreateOpaqueKey(TetherAssetRecord asset)
    {
        var identity = $"{asset.NormalizedSourcePath}|{asset.FileSize}|{asset.ModifiedAtUtc:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static void TryDeleteCacheFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
