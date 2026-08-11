using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Services.OnlineSelection;

public sealed class WpfSelectionProxyRenderer(IRawDecoder rawDecoder) : ISelectionProxyRenderer
{
    public string Name => "WPF sRGB proxy";

    public async Task RenderJpegAsync(
        string sourcePath,
        Stream destination,
        SelectionProxyOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveLongEdge = Math.Clamp(options.LongEdge, 1, SelectionProxyOptions.OnlineDefault.LongEdge);
        var effectiveQuality = Math.Clamp(options.Quality, 1, SelectionProxyOptions.OnlineDefault.Quality);
        var fullPath = Path.GetFullPath(sourcePath);
        BitmapSource source;
        if (RawToJpegDefaults.CandidateRawExtensions.Contains(Path.GetExtension(fullPath)))
        {
            var decoded = await rawDecoder.DecodeAsync(fullPath,
                new RawToJpegOptions(effectiveQuality, effectiveLongEdge, UseCameraWhiteBalance: true,
                    VerifySha256: false, PreserveExif: false, AutoRotate: true), cancellationToken).ConfigureAwait(false);
            ValidateDecodedRaw(decoded);
            source = BitmapSource.Create(decoded.Width, decoded.Height, 96, 96, PixelFormats.Rgb24,
                null, decoded.Rgb24Pixels, decoded.Stride);
        }
        else
        {
            source = LoadOrdinaryImage(fullPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        source = NormalizePixelFormat(source);
        source = Resize(source, effectiveLongEdge);
        source.Freeze();
        var encoder = new JpegBitmapEncoder { QualityLevel = effectiveQuality };
        encoder.Frames.Add(BitmapFrame.Create(source, null, null, null));
        encoder.Save(destination);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static BitmapSource LoadOrdinaryImage(string sourcePath)
    {
        using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 128 * 1024, FileOptions.SequentialScan);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) throw new InvalidDataException("The image has no decodable frame.");
        var frame = decoder.Frames[0];
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
            throw new InvalidDataException("The decoded image has invalid dimensions.");
        BitmapSource source = frame;
        if (frame.ColorContexts is { Count: > 0 })
        {
            try
            {
                var converted = new ColorConvertedBitmap(frame, frame.ColorContexts[0],
                    new ColorContext(PixelFormats.Bgra32), PixelFormats.Bgra32);
                converted.Freeze();
                source = converted;
            }
            catch (Exception exception) when (exception is NotSupportedException or ArgumentException or InvalidOperationException)
            {
            }
        }
        return ApplyOrientation(source, ReadOrientation(frame.Metadata as BitmapMetadata));
    }

    private static BitmapSource NormalizePixelFormat(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgr24 || source.Format == PixelFormats.Rgb24 ||
            source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32)
            return source;
        var fallback = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
        fallback.Freeze();
        return fallback;
    }

    private static BitmapSource Resize(BitmapSource source, int longestEdge)
    {
        var currentEdge = Math.Max(source.PixelWidth, source.PixelHeight);
        if (currentEdge <= longestEdge) return source;
        var scale = longestEdge / (double)currentEdge;
        var resized = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        resized.Freeze();
        return resized;
    }

    private static BitmapSource ApplyOrientation(BitmapSource source, ushort orientation)
    {
        Transform? transform = orientation switch
        {
            2 => new ScaleTransform(-1, 1, source.PixelWidth / 2d, source.PixelHeight / 2d),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1, source.PixelWidth / 2d, source.PixelHeight / 2d),
            5 => Combined(new ScaleTransform(-1, 1), new RotateTransform(90)),
            6 => new RotateTransform(90),
            7 => Combined(new ScaleTransform(-1, 1), new RotateTransform(270)),
            8 => new RotateTransform(270),
            _ => null
        };
        if (transform is null) return source;
        var oriented = new TransformedBitmap(source, transform);
        oriented.Freeze();
        return oriented;
    }

    private static Transform Combined(params Transform[] transforms)
    {
        var group = new TransformGroup();
        foreach (var transform in transforms) group.Children.Add(transform);
        group.Freeze();
        return group;
    }

    private static ushort ReadOrientation(BitmapMetadata? metadata)
    {
        if (metadata is null) return 1;
        try
        {
            var value = metadata.GetQuery("/app1/ifd/{ushort=274}");
            return value switch
            {
                ushort orientation when orientation is >= 1 and <= 8 => orientation,
                uint orientation when orientation is >= 1 and <= 8 => (ushort)orientation,
                _ => (ushort)1
            };
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return 1;
        }
    }

    private static void ValidateDecodedRaw(RawDecodedImage image)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Stride < image.Width * 3 ||
            image.Rgb24Pixels.Length < image.RequiredByteCount)
            throw new InvalidDataException("The RAW decoder returned an incomplete bitmap.");
        if (!string.Equals(image.Metadata.ColorSpace, "sRGB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The RAW proxy must be decoded to sRGB.");
    }
}
