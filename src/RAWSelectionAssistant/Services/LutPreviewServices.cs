using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

public sealed record LutPreviewRenderResult(BitmapSource Image, bool UsedFallback, string StatusText);

public interface ILutPreviewService
{
    Task<LutPreviewRenderResult> RenderAsync(BitmapSource source, LutDefinition definition, double strength, DisplayColorProfile? targetProfile, CancellationToken cancellationToken = default);
}

public interface IColorConversionService
{
    BitmapSource ConvertToDisplay(BitmapSource source, DisplayColorProfile? targetProfile, out bool usedFallback);
}

public sealed class WpfColorConversionService : IColorConversionService
{
    public BitmapSource ConvertToDisplay(BitmapSource source, DisplayColorProfile? targetProfile, out bool usedFallback)
    {
        usedFallback = targetProfile is null || targetProfile.Status != DisplayProfileStatus.Detected || string.IsNullOrWhiteSpace(targetProfile.ProfilePath) || !File.Exists(targetProfile.ProfilePath);
        if (usedFallback) return source;
        try
        {
            var sourceContext = new ColorContext(PixelFormats.Bgra32);
            var targetContext = new ColorContext(new Uri(targetProfile!.ProfilePath!, UriKind.Absolute));
            var converted = new ColorConvertedBitmap(source, sourceContext, targetContext, PixelFormats.Bgra32);
            converted.Freeze();
            return converted;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or FileFormatException or IOException or UnauthorizedAccessException)
        {
            usedFallback = true;
            return source;
        }
    }
}

public sealed class CpuLutPreviewService(ILutProcessor? processor = null, IColorConversionService? colorConversion = null) : ILutPreviewService
{
    public const int MaximumConcurrentRenders = 2;
    private static readonly SemaphoreSlim RenderGate = new(MaximumConcurrentRenders, MaximumConcurrentRenders);
    private readonly ILutProcessor _processor = processor ?? new CpuLutProcessor();
    private readonly IColorConversionService _colorConversion = colorConversion ?? new WpfColorConversionService();

    public async Task<LutPreviewRenderResult> RenderAsync(BitmapSource source, LutDefinition definition, double strength, DisplayColorProfile? targetProfile, CancellationToken cancellationToken = default)
    {
        await RenderGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => Render(source, definition, strength, targetProfile, cancellationToken), cancellationToken).ConfigureAwait(false); }
        finally { RenderGate.Release(); }
    }

    private LutPreviewRenderResult Render(BitmapSource source, LutDefinition definition, double strength, DisplayColorProfile? targetProfile, CancellationToken cancellationToken)
    {
        var input = HistogramService.EnsureBgra32(source);
        var stride = input.PixelWidth * 4;
        var pixels = new byte[stride * input.PixelHeight];
        input.CopyPixels(pixels, stride, 0);
        var amount = (float)Math.Clamp(strength, 0, 1);
        for (var y = 0; y < input.PixelHeight; y++)
        {
            if ((y & 15) == 0) cancellationToken.ThrowIfCancellationRequested();
            var row = y * stride;
            for (var x = 0; x < input.PixelWidth; x++)
            {
                var offset = row + x * 4;
                var transformed = _processor.Apply(definition, new(pixels[offset + 2] / 255f, pixels[offset + 1] / 255f, pixels[offset] / 255f), amount);
                pixels[offset] = Byte(transformed.Blue); pixels[offset + 1] = Byte(transformed.Green); pixels[offset + 2] = Byte(transformed.Red);
            }
        }
        var rendered = BitmapSource.Create(input.PixelWidth, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        rendered.Freeze();
        var converted = _colorConversion.ConvertToDisplay(rendered, targetProfile, out var fallback);
        if (!converted.IsFrozen && converted.CanFreeze) converted.Freeze();
        return new(converted, fallback, fallback ? "LUT已应用；目标显示器ICC不可用，已安全回退sRGB。" : "LUT已应用，并按目标显示器ICC转换。");
    }

    private static byte Byte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
}

public sealed class LutPreviewCacheService : ILutCacheService
{
    private readonly string _root;
    private readonly long _maximumBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public LutPreviewCacheService(string? root = null, long maximumBytes = 512L * 1024 * 1024) { _root = root ?? AppDataPaths.TetherLutCacheDirectory; _maximumBytes = maximumBytes > 0 ? maximumBytes : throw new ArgumentOutOfRangeException(nameof(maximumBytes)); }
    public string CreateOpaqueKey(LutCacheKey key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{key.AssetId:D}|{key.ProxyVersion}|{key.LutFingerprint}|{key.InputInterpretation}|{key.StrengthPercent}|{key.StableDisplayKey}|{key.IccFingerprint}|{key.RenderVersion}"))).ToLowerInvariant();
    public string? Resolve(string opaqueKey) { if (!SafeKey(opaqueKey)) return null; var path = Path.Combine(_root, opaqueKey + ".png"); if (!File.Exists(path)) return null; try { using var stream = File.OpenRead(path); _ = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad); File.SetLastAccessTimeUtc(path, DateTime.UtcNow); return path; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileFormatException or NotSupportedException) { TryDelete(path); return null; } }
    public async Task StoreAsync(string opaqueKey, Stream content, CancellationToken cancellationToken = default)
    {
        if (!SafeKey(opaqueKey)) throw new ArgumentException("缓存键无效。", nameof(opaqueKey));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { Directory.CreateDirectory(_root); var destination = Path.Combine(_root, opaqueKey + ".png"); var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N"); try { await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true)) { await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false); await output.FlushAsync(cancellationToken).ConfigureAwait(false); output.Flush(true); } File.Move(temporary, destination, true); } finally { TryDelete(temporary); } await TrimCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    public async Task TrimAsync(CancellationToken cancellationToken = default) { await _gate.WaitAsync(cancellationToken).ConfigureAwait(false); try { await TrimCoreAsync(cancellationToken).ConfigureAwait(false); } finally { _gate.Release(); } }
    public async Task InvalidateAsync(Func<string, bool> predicate, CancellationToken cancellationToken = default) { await _gate.WaitAsync(cancellationToken).ConfigureAwait(false); try { if (!Directory.Exists(_root)) return; foreach (var file in Directory.EnumerateFiles(_root, "*.png", SearchOption.TopDirectoryOnly)) { cancellationToken.ThrowIfCancellationRequested(); var key = Path.GetFileNameWithoutExtension(file); if (predicate(key)) TryDelete(file); } } finally { _gate.Release(); } }
    private Task TrimCoreAsync(CancellationToken cancellationToken) { if (!Directory.Exists(_root)) return Task.CompletedTask; var files = new DirectoryInfo(_root).EnumerateFiles("*.png").OrderBy(item => item.LastAccessTimeUtc).ToArray(); var total = files.Sum(item => item.Length); foreach (var file in files) { if (total <= _maximumBytes) break; cancellationToken.ThrowIfCancellationRequested(); var length = file.Length; TryDelete(file.FullName); total -= length; } return Task.CompletedTask; }
    private static bool SafeKey(string key) => key.Length == 64 && key.All(Uri.IsHexDigit);
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
}

public static class LutBitmapEncoding
{
    public static MemoryStream EncodePng(BitmapSource image) { var stream = new MemoryStream(); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(stream); stream.Position = 0; return stream; }
    public static BitmapSource Load(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap; }
}
