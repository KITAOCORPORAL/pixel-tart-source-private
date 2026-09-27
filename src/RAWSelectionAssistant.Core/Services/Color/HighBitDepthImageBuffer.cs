using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Color;

/// <summary>Shared linear-ish RGB working storage for RAW, Color Studio and export adapters.</summary>
public sealed class HighBitDepthImageBuffer
{
    public HighBitDepthImageBuffer(int width, int height, ReadOnlyMemory<float> rgb32)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (rgb32.Length != checked(width * height * 3)) throw new ArgumentException("RGB float buffer length does not match dimensions.", nameof(rgb32));
        Width = width; Height = height; Rgb32 = rgb32;
    }

    public HighBitDepthImageBuffer(int width, int height, ReadOnlyMemory<ushort> rgb48, string sourceBitDepth = "16", string workingColorSpace = "sRGB")
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (rgb48.Length != checked(width * height * 3)) throw new ArgumentException("RGB48 buffer length does not match dimensions.", nameof(rgb48));
        Width = width; Height = height; Rgb32 = ToFloat(rgb48); SourceBitDepth = sourceBitDepth; WorkingColorSpace = workingColorSpace;
    }

    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<float> Rgb32 { get; }
    public string SourceBitDepth { get; } = "32f";
    public string WorkingColorSpace { get; } = "sRGB";
    public int PixelCount => Width * Height;

    public static HighBitDepthImageBuffer FromRgb24(RawDecodedImage image)
    {
        var output = new float[image.Width * image.Height * 3];
        for (var i = 0; i < output.Length; i++) output[i] = image.Rgb24Pixels[i] / 255f;
        return new(image.Width, image.Height, output);
    }

    public static HighBitDepthImageBuffer FromRaw(RawDecodedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Rgb48Pixels is { } rgb48 && image.BitsPerChannel >= 16)
            return new(image.Width, image.Height, rgb48, image.BitsPerChannel.ToString(), image.Metadata.ColorSpace);
        return FromRgb24(image);
    }

    public static HighBitDepthImageBuffer FromVisualRgb24(VisualPixelBuffer image)
    {
        var output = new float[image.Rgb24.Length];
        for (var i = 0; i < output.Length; i++) output[i] = image.Rgb24.Span[i] / 255f;
        return new(image.Width, image.Height, output);
    }

    public VisualPixelBuffer ToVisualRgb24()
    {
        var output = new byte[Rgb32.Length];
        for (var i = 0; i < output.Length; i++) output[i] = (byte)Math.Clamp(Math.Round(Rgb32.Span[i] * 255), 0, 255);
        return new(Width, Height, output);
    }

    public ushort[] ToRgb48()
    {
        var output = new ushort[Rgb32.Length];
        for (var i = 0; i < output.Length; i++) output[i] = (ushort)Math.Clamp(Math.Round(Rgb32.Span[i] * 65535), 0, 65535);
        return output;
    }

    private static float[] ToFloat(ReadOnlyMemory<ushort> input)
    {
        var output = new float[input.Length];
        for (var i = 0; i < output.Length; i++) output[i] = input.Span[i] / 65535f;
        return output;
    }
}
