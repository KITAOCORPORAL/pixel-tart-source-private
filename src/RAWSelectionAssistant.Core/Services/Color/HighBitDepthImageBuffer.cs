using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Color;

/// <summary>Logical encoded sRGB float RGB; an owned exact RGB48 source may be read by strips until legacy Rgb32 access requests expansion. Three channels, no alpha.</summary>
public sealed class HighBitDepthImageBuffer : IDisposable
{
    public HighBitDepthImageBuffer(int width, int height, ReadOnlyMemory<float> rgb32, string sourceBitDepth = "32f", string workingColorSpace = "sRGB", ushort orientation = 1, RawImageMetadata? metadata = null)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (rgb32.Length != checked(width * height * 3)) throw new ArgumentException("RGB float buffer length does not match dimensions.", nameof(rgb32));
        Width = width; Height = height; Rgb32 = rgb32;pixelReturn=GraphPixelAllocation.Claim(rgb32); SourceBitDepth = sourceBitDepth; WorkingColorSpace = workingColorSpace; Orientation = orientation; Metadata = metadata;
    }

    public HighBitDepthImageBuffer(int width, int height, ReadOnlyMemory<ushort> rgb48, string sourceBitDepth = "16", string workingColorSpace = "sRGB", ushort orientation = 1, RawImageMetadata? metadata = null)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (rgb48.Length != checked(width * height * 3)) throw new ArgumentException("RGB48 buffer length does not match dimensions.", nameof(rgb48));
        Width = width; Height = height; Rgb32 = ToFloat(rgb48); SourceBitDepth = sourceBitDepth; WorkingColorSpace = workingColorSpace; Orientation = orientation; Metadata = metadata;
    }

    public int Width { get; }
    public int Height { get; }
    private ReadOnlyMemory<float> pixels;
    private ReadOnlyMemory<ushort> ownedRgb48;
    private readonly object backingGate=new();
    internal static HighBitDepthImageBuffer TakeOwnedRgb48(int width,int height,ushort[] values,string sourceBitDepth,string space,ushort orientation,RawImageMetadata? metadata)
        => new(width,height,values,sourceBitDepth,space,orientation,metadata,true);
    private HighBitDepthImageBuffer(int width,int height,ushort[] values,string bits,string space,ushort orientation,RawImageMetadata? metadata,bool owned)
    {
        if(width<=0||height<=0||values.Length!=checked(width*height*3))throw new ArgumentException("Owned RGB48 dimensions mismatch.");
        Width=width;Height=height;ownedRgb48=values;SourceBitDepth=bits;WorkingColorSpace=space;Orientation=orientation;Metadata=metadata;
    }
    public int SampleCount=>checked(Width*Height*3);
    public long StorageBytes {get {lock(backingGate)return (long)pixels.Length*4+(long)ownedRgb48.Length*2;}}
    public bool IsFloatMaterialized {get {lock(backingGate)return !pixels.IsEmpty;}}
    internal SampleReader ReadSamples()
    {lock(backingGate){if(disposed)throw new ObjectDisposedException(nameof(HighBitDepthImageBuffer));return new(pixels,ownedRgb48);}}
    internal readonly struct SampleReader(ReadOnlyMemory<float> floats,ReadOnlyMemory<ushort> words)
    {public float this[int index]=>words.IsEmpty?floats.Span[index]:words.Span[index]/65535f;}
    public void CopyRgb32(int offset,Span<float> destination)
    {
        if(offset<0||offset>SampleCount-destination.Length)throw new ArgumentOutOfRangeException(nameof(offset));
        var input=ReadSamples();for(int i=0;i<destination.Length;i++)destination[i]=input[offset+i];
    }
    private IDisposable? ownedReservation;
    private IDisposable? pixelReturn;
    private bool disposed;
    public ReadOnlyMemory<float> Rgb32
    {
        get {lock(backingGate){if(disposed)throw new ObjectDisposedException(nameof(HighBitDepthImageBuffer));if(pixels.IsEmpty&&!ownedRgb48.IsEmpty){pixels=ToFloat(ownedRgb48);ownedRgb48=default;}return pixels;}}
        private set=>pixels=value;
    }
    public bool OwnsReservation=>ownedReservation is not null;
    internal bool HasGraphBacking=>pixelReturn is not null;
    public void TakeReservation(IDisposable reservation)
    {
        if(disposed||ownedReservation is not null)throw new InvalidOperationException("Buffer already owned/disposed");
        ownedReservation=reservation;
    }
    public void Dispose(){lock(backingGate){if(disposed)return;disposed=true;pixels=default;ownedRgb48=default;}Interlocked.Exchange(ref pixelReturn,null)?.Dispose();Interlocked.Exchange(ref ownedReservation,null)?.Dispose();}
    public string SourceBitDepth { get; } = "32f";
    public string WorkingColorSpace { get; } = "sRGB";
    public ushort Orientation { get; } = 1;
    public RawImageMetadata? Metadata { get; }
    public int PixelCount => Width * Height;

    public static HighBitDepthImageBuffer FromRgb24(RawDecodedImage image)
    {
        var output = new float[image.Width * image.Height * 3];
        for (var i = 0; i < output.Length; i++) output[i] = image.Rgb24Pixels[i] / 255f;
        return new(image.Width, image.Height, output, image.BitsPerChannel.ToString(), image.Metadata.ColorSpace, image.Metadata.Orientation, image.Metadata);
    }

    public static HighBitDepthImageBuffer FromRaw(RawDecodedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Rgb48Pixels is { } rgb48 && image.BitsPerChannel >= 16)
            return new(image.Width, image.Height, rgb48, image.BitsPerChannel.ToString(), image.Metadata.ColorSpace, image.Metadata.Orientation, image.Metadata);
        return FromRgb24(image);
    }

    public static HighBitDepthImageBuffer FromVisualRgb24(VisualPixelBuffer image)
    {
        var output = new float[image.Rgb24.Length];
        for (var i = 0; i < output.Length; i++) output[i] = image.Rgb24.Span[i] / 255f;
        return new(image.Width, image.Height, output, "8", "sRGB");
    }

    public VisualPixelBuffer ToVisualRgb24() => ToVisualRgb24(CancellationToken.None);

    public VisualPixelBuffer ToVisualRgb24(CancellationToken token)
    {
        var input=ReadSamples();var output = new byte[SampleCount];
        for (var i = 0; i < output.Length; i++){if((i & 8191)==0)token.ThrowIfCancellationRequested();output[i] = (byte)Math.Clamp(Math.Round(input[i] * 255), 0, 255);}
        return new(Width, Height, output);
    }

    public HighBitDepthImageBuffer Clone() => new(Width, Height, GraphPixelAllocation.Copy(Rgb32), SourceBitDepth, WorkingColorSpace, Orientation, Metadata);

    public ushort[] ToRgb48()
    {
        var input=ReadSamples();var output = new ushort[SampleCount];
        for (var i = 0; i < output.Length; i++) output[i] = (ushort)Math.Clamp(Math.Round(input[i] * 65535), 0, 65535);
        return output;
    }

    private static float[] ToFloat(ReadOnlyMemory<ushort> input)
    {
        var output = new float[input.Length];
        for (var i = 0; i < output.Length; i++) output[i] = input.Span[i] / 65535f;
        return output;
    }
}
