using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace PixelTart.SelectionApi.Server;

public sealed record LocalDevJpegVariants(byte[] Thumb, byte[] Preview, byte[] Proxy);

/// <summary>
/// LocalDev defense-in-depth: every accepted JPEG is decoded and re-encoded
/// without metadata. The three delivery variants are real, distinct sizes.
/// </summary>
public sealed class LocalDevJpegVariantService
{
    public async Task<LocalDevJpegVariants> CreateAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        using var copy = new MemoryStream();
        await source.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        if (copy.Length < 4 || copy.Length > 64L * 1024 * 1024)
            throw new SelectionApiException(413, "ProxySizeInvalid", "Proxy JPEG must be between 4 bytes and 64 MB.");
        var bytes = copy.ToArray();
        if (bytes[0] != 0xff || bytes[1] != 0xd8)
            throw new SelectionApiException(415, "ProxyJpegRequired", "Only a decodable JPEG proxy is accepted.");

        BitmapFrame frame;
        try
        {
            using var decode = new MemoryStream(bytes, writable: false);
            var decoder = BitmapDecoder.Create(decode, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) throw new InvalidDataException("JPEG has no frame.");
            frame = decoder.Frames[0];
            if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || (long)frame.PixelWidth * frame.PixelHeight > 200_000_000)
                throw new InvalidDataException("JPEG dimensions are invalid.");
        }
        catch (Exception exception) when (exception is not SelectionApiException)
        {
            throw new SelectionApiException(415, "ProxyJpegInvalid", "The proxy JPEG could not be decoded.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalized = Normalize(frame);
        var thumb = Encode(Resize(normalized, 480), 82);
        var preview = Encode(Resize(normalized, 1600), 85);
        var proxy = Encode(Resize(normalized, 2560), 85);
        return new LocalDevJpegVariants(thumb, preview, proxy);
    }

    private static BitmapSource Normalize(BitmapFrame source)
    {
        BitmapSource normalized = source;
        if (source.ColorContexts is { Count: > 0 })
        {
            try
            {
                normalized = new ColorConvertedBitmap(source, source.ColorContexts[0],
                    new ColorContext(PixelFormats.Bgra32), PixelFormats.Bgra32);
            }
            catch (Exception exception) when (exception is NotSupportedException or ArgumentException or InvalidOperationException)
            {
            }
        }
        var format = normalized.Format;
        if (format != PixelFormats.Bgr24 && format != PixelFormats.Rgb24 && format != PixelFormats.Bgra32 && format != PixelFormats.Pbgra32)
            normalized = new FormatConvertedBitmap(normalized, PixelFormats.Bgr24, null, 0);
        normalized.Freeze();
        return normalized;
    }

    private static BitmapSource Resize(BitmapSource source, int longestEdge)
    {
        var current = Math.Max(source.PixelWidth, source.PixelHeight);
        if (current <= longestEdge) return source;
        var scale = longestEdge / (double)current;
        var resized = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        resized.Freeze();
        return resized;
    }

    private static byte[] Encode(BitmapSource source, int quality)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(source, null, null, null));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
