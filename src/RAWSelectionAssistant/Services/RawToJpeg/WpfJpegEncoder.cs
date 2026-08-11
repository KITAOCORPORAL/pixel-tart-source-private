using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Services.RawToJpeg;

public sealed class WpfJpegEncoder : IRawJpegEncoder
{
    public Task EncodeAsync(RawDecodedImage image, Stream destination, RawToJpegOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        var source = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Rgb24, null, image.Rgb24Pixels, image.Stride);
        if (options.AutoRotate) source = ApplyOrientation(source, image.Metadata.Orientation);
        source = Resize(source, options.LongestEdge);
        var metadata = CreateMetadata(image.Metadata, options);
        var encoder = new JpegBitmapEncoder { QualityLevel = options.JpegQuality };
        encoder.Frames.Add(BitmapFrame.Create(source, null, metadata, null));
        encoder.Save(destination);
        return Task.CompletedTask;
    }

    private static BitmapMetadata CreateMetadata(RawImageMetadata source, RawToJpegOptions options)
    {
        var metadata = new BitmapMetadata("jpg")
        {
            ApplicationName = "像素蛋挞",
            Comment = "sRGB; RAW conversion"
        };
        if (!options.PreserveExif) return metadata;
        if (!string.IsNullOrWhiteSpace(source.CameraMake)) metadata.CameraManufacturer = source.CameraMake;
        if (!string.IsNullOrWhiteSpace(source.CameraModel)) metadata.CameraModel = source.CameraModel;
        if (source.CapturedAt is { } capturedAt)
        {
            var timestamp = capturedAt.ToString("yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            metadata.SetQuery("/app1/ifd/exif/{uint=36867}", timestamp);
            metadata.SetQuery("/app1/ifd/{ushort=306}", timestamp);
        }
        metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)(options.AutoRotate ? 1 : source.Orientation));
        return metadata;
    }

    private static BitmapSource Resize(BitmapSource source, int? longestEdge)
    {
        if (longestEdge is not int edge || Math.Max(source.PixelWidth, source.PixelHeight) <= edge) return source;
        var scale = edge / (double)Math.Max(source.PixelWidth, source.PixelHeight);
        var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        transformed.Freeze();
        return transformed;
    }

    private static BitmapSource ApplyOrientation(BitmapSource source, ushort orientation) => orientation switch
    {
        3 => new TransformedBitmap(source, new RotateTransform(180)),
        6 => new TransformedBitmap(source, new RotateTransform(90)),
        8 => new TransformedBitmap(source, new RotateTransform(270)),
        _ => source
    };
}
