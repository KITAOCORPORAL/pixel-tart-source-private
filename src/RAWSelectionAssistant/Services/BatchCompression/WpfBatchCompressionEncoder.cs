using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.BatchCompression;

namespace RAWSelectionAssistant.Services.BatchCompression;

public sealed class WpfBatchCompressionEncoder : IBatchCompressionEncoder
{
    public Task EncodeAsync(string sourcePath, Stream destination, BatchCompressionOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        if (!BatchCompressionDefaults.SupportedExtensions.Contains(Path.GetExtension(sourcePath)))
            throw new InvalidDataException("Unsupported image format.");

        var decoder = BitmapDecoder.Create(new Uri(Path.GetFullPath(sourcePath)),
            BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var image = Resize(frame, options.LongestEdge);
        BitmapMetadata? metadata = null;
        if (options.PreserveMetadata && frame.Metadata is BitmapMetadata original)
        {
            try { metadata = original.Clone() as BitmapMetadata; }
            catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException) { }
        }

        var colorContexts = options.PreserveIccProfile ? frame.ColorContexts : null;
        var outputFrame = BitmapFrame.Create(image, options.PreserveMetadata ? frame.Thumbnail : null, metadata, colorContexts);
        var encoder = new JpegBitmapEncoder { QualityLevel = options.JpegQuality };
        encoder.Frames.Add(outputFrame);
        encoder.Save(destination);
        return Task.CompletedTask;
    }

    public Task VerifyDecodableAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(Path.GetFullPath(imagePath), FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0 || decoder.Frames[0].PixelWidth <= 0 || decoder.Frames[0].PixelHeight <= 0)
            throw new InvalidDataException("The compressed output cannot be decoded.");
        return Task.CompletedTask;
    }

    private static BitmapSource Resize(BitmapSource source, int longestEdge)
    {
        var currentEdge = Math.Max(source.PixelWidth, source.PixelHeight);
        if (currentEdge <= longestEdge) return source;
        var scale = longestEdge / (double)currentEdge;
        var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        transformed.Freeze();
        return transformed;
    }
}
