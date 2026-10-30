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
    public string Stage {get;init;}="Unknown";
    public int? NativeCode {get;init;}
    public string? NativeName {get;init;}
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

    /// <summary>Best-effort embedded preview. Failure returns null so professional decode can continue.</summary>
    public Task<RawDecodedImage?> TryDecodeEmbeddedPreviewAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return Task.Run(() => TryDecodeEmbeddedPreviewCore(Path.GetFullPath(sourcePath), cancellationToken), cancellationToken);
    }

    private static RawDecodedImage? TryDecodeEmbeddedPreviewCore(string sourcePath, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            using var context = RawContext.OpenFile(sourcePath);
            context.UnpackThumbnail(0);
            token.ThrowIfCancellationRequested();
            using var preview = context.MakeDcrawMemoryThumbnail();
            if (preview.ImageType != ProcessedImageType.Bitmap || preview.Bits != 8 || preview.Channels != 3 || preview.Width <= 0 || preview.Height <= 0)
                return null;
            var stride = checked(preview.Width * 3);
            var count = checked(stride * preview.Height);
            if (preview.DataSize < count) return null;
            var pixels = preview.AsSpan<byte>()[..count].ToArray();
            var parameters = context.ImageParams;
            return new RawDecodedImage(preview.Width, preview.Height, stride, pixels,
                new RawImageMetadata(string.IsNullOrWhiteSpace(parameters.Make) ? null : parameters.Make.Trim(),
                    string.IsNullOrWhiteSpace(parameters.Model) ? null : parameters.Model.Trim(), null, 1, "sRGB"));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is LibRawException or IOException or InvalidDataException or DllNotFoundException or BadImageFormatException)
        { return null; }
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

    /// <summary>Viewing-only exact sample extraction after unchanged full native development.
    /// Does not use embedded JPEG, half-size, alternate color math, or mutate normal export decode.</summary>
    public Task<RawDecodedImage> DecodeViewingProxyAsync(string path,int edge,CancellationToken token=default)
    {
        if(edge is <1 or >1600)throw new ArgumentOutOfRangeException(nameof(edge));
        return Task.Run(()=>DecodeCore(Path.GetFullPath(path),Path.GetExtension(path),new RawToJpegOptions(AutoRotate:false,DecodeMode:RawDecodeMode.ProfessionalDecode),token,edge),token);
    }

    private RawDecodedImage DecodeCore(string sourcePath, string extension, RawToJpegOptions options, CancellationToken cancellationToken,int viewingEdge=0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = new FileInfo(sourcePath);
        if (!before.Exists)
            throw new RawDecodeException(ErrorCodeCatalog.SourceNotFound, "The RAW source is unavailable.");

        var expectedLength = before.Length;
        var expectedModified = before.LastWriteTimeUtc;
        var stage="OpenFile";
        try
        {
            using var context = RawContext.OpenFile(sourcePath);
            using var viewingWork=viewingEdge>0?RAWSelectionAssistant.Core.Services.Projects.StudioGlobalResources.ReserveWork(checked((long)context.RawWidth*context.RawHeight*28)):null;
            var imageParams = context.ImageParams;
            var otherParams = context.ImageOtherParams;
            stage="Metadata";var sourceOrientation = viewingEdge>0?ReadViewingOrientation(sourcePath):ReadOrientation(sourcePath);
            stage="Unpack";context.Unpack();
            cancellationToken.ThrowIfCancellationRequested();
            var professional = options.DecodeMode == RawDecodeMode.ProfessionalDecode;
            stage="DcrawProcess";context.DcrawProcess(output =>
            {
                output.HalfSize = false;
                output.UseCameraWb = options.UseCameraWhiteBalance;
                output.UseAutoWb = !options.UseCameraWhiteBalance;
                output.UseCameraMatrix = true;
                output.OutputColor = LibRawColorSpace.SRGB;
                output.OutputBps = professional ? 16 : 8;
                output.OutputTiff = false;
                output.Interpolation = true;
                if (!options.AutoRotate) output.UserFlip = 0;
            });
            cancellationToken.ThrowIfCancellationRequested();
            using var processed = context.MakeDcrawMemoryImage();
            if (processed.ImageType != ProcessedImageType.Bitmap || processed.Channels != 3 || (processed.Bits != 8 && processed.Bits != 16))
                throw new RawDecodeException(ErrorCodeCatalog.DecodeFailed, "The decoder did not return an RGB bitmap with a supported bit depth.");

            var bytesPerSample = processed.Bits / 8;
            var stride = checked(processed.Width * processed.Channels * bytesPerSample);
            var required = checked(stride * processed.Height);
            if (processed.Width <= 0 || processed.Height <= 0 || processed.DataSize < required)
                throw new RawDecodeException(ErrorCodeCatalog.CorruptedImage, "The decoded bitmap is incomplete.");

            int resultWidth=processed.Width,resultHeight=processed.Height;
            if(viewingEdge>0&&Math.Max(resultWidth,resultHeight)>viewingEdge){var edge=Math.Max(resultWidth,resultHeight);resultWidth=Math.Max(1,(int)Math.Round(resultWidth*viewingEdge/(double)edge));resultHeight=Math.Max(1,(int)Math.Round(resultHeight*viewingEdge/(double)edge));}
            byte[] pixels;
            ushort[]? pixels48 = null;
            if (processed.Bits == 16)
            {
                var samples = checked(processed.Width * processed.Height * processed.Channels);
                if(viewingEdge>0){pixels48=new ushort[resultWidth*resultHeight*3];var original=processed.AsSpan<ushort>();for(int y=0;y<resultHeight;y++){cancellationToken.ThrowIfCancellationRequested();int sy=Math.Min(processed.Height-1,(int)((y+.5)*processed.Height/resultHeight));for(int x=0;x<resultWidth;x++){int sx=Math.Min(processed.Width-1,(int)((x+.5)*processed.Width/resultWidth));original.Slice((sy*processed.Width+sx)*3,3).CopyTo(pixels48.AsSpan((y*resultWidth+x)*3));}}}
                else pixels48 = processed.AsSpan<ushort>()[..samples].ToArray();
                pixels = Array.Empty<byte>();
            }
            else
            {
                pixels = processed.AsSpan<byte>()[..required].ToArray();
            }
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
            return new RawDecodedImage(resultWidth, resultHeight,checked(resultWidth*3*bytesPerSample),pixels, metadata)
            {
                Rgb48Pixels = pixels48, BitsPerChannel = processed.Bits,
                PixelFormat = processed.Bits == 16 ? "RGB48" : "RGB24"
            };
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
            if(stage=="OpenFile"&&ex is LibRawException unsupported&&unsupported.ErrorCode.ToString()=="FileUnsupported")code="RAW_NATIVE_FILE_UNSUPPORTED";
            throw new RawDecodeException(code, "The RAW file could not be decoded safely.", ex){Stage=stage,NativeCode=ex is LibRawException native?(int)native.ErrorCode:null,NativeName=ex is LibRawException named?named.ErrorCode.ToString():null};
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ushort ReadViewingOrientation(string sourcePath)
    {
        try
        {
            var directories=ImageMetadataReader.ReadMetadata(sourcePath).OfType<ExifIfd0Directory>().ToArray();
            var orientations=directories.Where(d=>d.ContainsTag(ExifDirectoryBase.TagOrientation)).Select(d=>d.GetInt32(ExifDirectoryBase.TagOrientation)).Distinct().ToArray();
            if(orientations.Length==0)return 1; // existing documented absent-tag rule
            if(orientations.Length!=1||orientations[0] is <1 or >8)throw new InvalidDataException("RAW orientation invalid/ambiguous");
            return (ushort)orientations[0];
        }
        catch(Exception e)when(e is IndexOutOfRangeException or ImageProcessingException or ArgumentException or NotSupportedException)
        {
            if(TryReadTiffIfd0Orientation(sourcePath,out var orientation))return orientation;
            throw new RawDecodeException("RAW_VIEWING_METADATA","RAW metadata orientation could not be independently established; no guessed orientation or embedded preview.",e){Stage="Metadata"};
        }
    }
    // Bounded standard TIFF IFD0 only. MakerNotes and image/color data are not interpreted.
    internal static bool TryReadTiffIfd0Orientation(string path,out ushort orientation)
    {
        orientation=0;using var stream=File.OpenRead(path);using var reader=new BinaryReader(stream);if(stream.Length<8)return false;
        var order=reader.ReadBytes(2);bool little=order.SequenceEqual(new byte[]{73,73});if(!little&&!order.SequenceEqual(new byte[]{77,77}))return false;
        ushort U16(){var b=reader.ReadBytes(2);if(b.Length!=2)throw new EndOfStreamException();return little?System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(b):System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(b);}
        uint U32(){var b=reader.ReadBytes(4);if(b.Length!=4)throw new EndOfStreamException();return little?System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b):System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b);}
        try{if(U16()!=42)return false;uint offset=U32();if(offset<8||offset>stream.Length-2)return false;stream.Position=offset;int count=U16();if(count>4096||stream.Position+(long)count*12+4>stream.Length)return false;ushort? found=null;for(int i=0;i<count;i++){var tag=U16();var type=U16();var n=U32();var value=reader.ReadBytes(4);if(tag!=274)continue;if(found is not null||type!=3||n!=1)return false;var v=little?System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(value):System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(value);if(v is <1 or >8)return false;found=v;}if(found is null)return false;orientation=found.Value;return true;}catch(EndOfStreamException){return false;}
    }

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
