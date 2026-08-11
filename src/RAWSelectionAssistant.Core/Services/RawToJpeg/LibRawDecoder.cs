using System.Collections.Concurrent;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Sdcb.LibRaw;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

public sealed class RawDecodeException(string errorCode, string message, Exception? innerException = null)
    : IOException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class LibRawDecoder : IRawDecoder
{
    private readonly ConcurrentDictionary<string, byte> _verifiedExtensions = new(StringComparer.OrdinalIgnoreCase);

    public RawDecoderCapability GetCapability()
    {
        try
        {
            return new(true, "LibRaw", RawContext.Version,
                RawToJpegDefaults.CandidateRawExtensions.OrderBy(x => x).ToArray(),
                _verifiedExtensions.Keys.OrderBy(x => x).ToArray());
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or TypeInitializationException)
        {
            return new(false, "LibRaw", null,
                RawToJpegDefaults.CandidateRawExtensions.OrderBy(x => x).ToArray(), [], "Native decoder unavailable.");
        }
    }

    public Task<RawDecodedImage> DecodeAsync(string sourcePath, RawToJpegOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        options.Validate();
        var fullPath = Path.GetFullPath(sourcePath);
        var extension = Path.GetExtension(fullPath);
        if (!RawToJpegDefaults.CandidateRawExtensions.Contains(extension))
            throw new RawDecodeException(ErrorCodeCatalog.UnsupportedFormat, "The selected file is not a candidate RAW format.");

        return Task.Run(() => DecodeCore(fullPath, extension, options, cancellationToken), cancellationToken);
    }

    private RawDecodedImage DecodeCore(string sourcePath, string extension, RawToJpegOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = new FileInfo(sourcePath);
        if (!before.Exists)
            throw new RawDecodeException(ErrorCodeCatalog.SourceNotFound, "The RAW source is unavailable.");

        var expectedLength = before.Length;
        var expectedModified = before.LastWriteTimeUtc;
        try
        {
            using var context = RawContext.OpenFile(sourcePath);
            var imageParams = context.ImageParams;
            var otherParams = context.ImageOtherParams;
            var sourceOrientation = ReadOrientation(sourcePath);
            context.Unpack();
            cancellationToken.ThrowIfCancellationRequested();
            context.DcrawProcess(output =>
            {
                output.HalfSize = false;
                output.UseCameraWb = options.UseCameraWhiteBalance;
                output.UseAutoWb = !options.UseCameraWhiteBalance;
                output.UseCameraMatrix = true;
                output.OutputColor = LibRawColorSpace.SRGB;
                output.OutputBps = 8;
                output.OutputTiff = false;
                output.Interpolation = true;
                if (!options.AutoRotate) output.UserFlip = 0;
            });
            cancellationToken.ThrowIfCancellationRequested();
            using var processed = context.MakeDcrawMemoryImage();
            if (processed.ImageType != ProcessedImageType.Bitmap || processed.Bits != 8 || processed.Channels != 3)
                throw new RawDecodeException(ErrorCodeCatalog.DecodeFailed, "The decoder did not return an 8-bit RGB bitmap.");

            var stride = checked(processed.Width * processed.Channels);
            var required = checked(stride * processed.Height);
            if (processed.Width <= 0 || processed.Height <= 0 || processed.DataSize < required)
                throw new RawDecodeException(ErrorCodeCatalog.CorruptedImage, "The decoded bitmap is incomplete.");

            var pixels = processed.AsSpan<byte>()[..required].ToArray();
            DateTimeOffset? capturedAt = otherParams.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(otherParams.Timestamp)
                : null;
            var metadata = new RawImageMetadata(
                NullIfBlank(imageParams.Make), NullIfBlank(imageParams.Model), capturedAt,
                options.AutoRotate ? (ushort)1 : sourceOrientation, "sRGB");

            before.Refresh();
            if (!before.Exists || before.Length != expectedLength || before.LastWriteTimeUtc != expectedModified)
                throw new RawDecodeException(ErrorCodeCatalog.SourceChanged, "The RAW source changed during decoding.");

            _verifiedExtensions.TryAdd(extension.ToUpperInvariant(), 0);
            return new(processed.Width, processed.Height, stride, pixels, metadata);
        }
        catch (OperationCanceledException) { throw; }
        catch (RawDecodeException) { throw; }
        catch (FileNotFoundException ex)
        {
            throw new RawDecodeException(ErrorCodeCatalog.SourceNotFound, "The RAW source is unavailable.", ex);
        }
        catch (Exception ex) when (ex is LibRawException or DllNotFoundException or BadImageFormatException or InvalidDataException)
        {
            var code = ex is DllNotFoundException or BadImageFormatException
                ? ErrorCodeCatalog.UnsupportedFormat
                : ErrorCodeCatalog.DecodeFailed;
            throw new RawDecodeException(code, "The RAW file could not be decoded safely.", ex);
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ushort ReadOrientation(string sourcePath)
    {
        try
        {
            var directory = ImageMetadataReader.ReadMetadata(sourcePath).OfType<ExifIfd0Directory>().FirstOrDefault();
            return directory?.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientation) == true && orientation is >= 1 and <= 8
                ? (ushort)orientation
                : (ushort)1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ImageProcessingException or ArgumentException or NotSupportedException)
        {
            return 1;
        }
    }
}
