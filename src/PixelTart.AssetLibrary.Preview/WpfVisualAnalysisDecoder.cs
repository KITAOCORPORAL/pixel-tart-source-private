using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace PixelTart.AssetLibrary.Preview;

internal static class WpfVisualAnalysisDecoder
{
    public static async Task<AssetVisualAnalysisRequest> DecodeAsync(AssetItem asset, int paletteSize, CancellationToken cancellationToken)
    {
        return await Task.Run(() => Decode(asset, paletteSize, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private static AssetVisualAnalysisRequest Decode(AssetItem asset, int paletteSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePath = asset.ManagedCopyPath is not null && File.Exists(asset.ManagedCopyPath) ? asset.ManagedCopyPath : asset.SourcePath;
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("素材文件已移动。", sourcePath);
        if (asset.MediaType == "Raw") throw new NotSupportedException("RAW 视觉分析需要已有代理图或内嵌预览；本预览不会执行完整 RAW 解码。");
        using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var scale = Math.Min(1d, 512d / Math.Max(frame.PixelWidth, frame.PixelHeight));
        BitmapSource proxy = scale < 1 ? new TransformedBitmap(frame, new ScaleTransform(scale, scale)) : frame;
        var sourceProfile = "UnknownAssumedSrgb";
        var converted = false;
        if (frame.ColorContexts is { Count: > 0 } && frame.ColorContexts[0] is { } sourceContext)
        {
            try
            {
                var destinationContext = new ColorContext(PixelFormats.Bgra32);
                proxy = new ColorConvertedBitmap(proxy, sourceContext, destinationContext, PixelFormats.Bgra32);
                sourceProfile = "EmbeddedICC"; converted = true;
            }
            catch (NotSupportedException) { sourceProfile = "EmbeddedICCUnsupportedAssumedSrgb"; }
            catch (FileFormatException) { sourceProfile = "EmbeddedICCInvalidAssumedSrgb"; }
        }
        var formatted = new FormatConvertedBitmap(proxy, PixelFormats.Bgr24, null, 0);
        var stride = checked(formatted.PixelWidth * 3); var bgr = new byte[checked(stride * formatted.PixelHeight)]; formatted.CopyPixels(bgr, stride, 0);
        var rgb = new byte[bgr.Length];
        for (var index = 0; index < bgr.Length; index += 3) { rgb[index] = bgr[index + 2]; rgb[index + 1] = bgr[index + 1]; rgb[index + 2] = bgr[index]; }
        cancellationToken.ThrowIfCancellationRequested();
        var fingerprint = asset.ContentHash ?? Convert.ToHexString(SHA256.HashData(rgb));
        return new(asset.AssetId, fingerprint, new(formatted.PixelWidth, formatted.PixelHeight, rgb), paletteSize, AnalysisSource: VisualAnalysisSourceKind.RasterOriginal, SourceProfile: sourceProfile, AnalysisProfile: "sRGB IEC61966-2.1", PixelsConvertedToAnalysisProfile: converted || sourceProfile.StartsWith("UnknownAssumedSrgb", StringComparison.Ordinal) || sourceProfile.EndsWith("AssumedSrgb", StringComparison.Ordinal));
    }
}
