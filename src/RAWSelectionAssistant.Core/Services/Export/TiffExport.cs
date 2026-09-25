using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Core.Services.Export;

public enum TiffBitDepth { Eight = 8, Sixteen = 16 }

public enum TiffCompression { None = 1 }

public sealed record TiffExportOptions(
    TiffBitDepth BitDepth = TiffBitDepth.Sixteen,
    ReadOnlyMemory<byte> IccProfile = default,
    string Software = "Pixel Tart");

public sealed record TiffExportResult(int Width, int Height, TiffBitDepth BitDepth, TiffCompression Compression,
    long BytesWritten, bool IccEmbedded);

/// <summary>
/// Small deterministic baseline TIFF writer for RGB fixtures and managed buffers.
/// It deliberately supports uncompressed RGB only; compression and camera metadata
/// stay in the production export adapter until their round-trip contracts are tested.
/// </summary>
public static class TiffExport
{
    public static TiffExportResult WriteRgb24(Stream destination, VisualPixelBuffer pixels, TiffExportOptions? options = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(destination); ArgumentNullException.ThrowIfNull(pixels);
        if (!destination.CanWrite) throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        options ??= new();
        if (options.IccProfile.Length > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(options), "ICC profile is too large.");
        if (options.IccProfile.Length > 0 && !IsValidIcc(options.IccProfile.Span)) throw new ArgumentException("ICC profile is invalid.", nameof(options));
        using var writer = new BinaryWriter(destination, System.Text.Encoding.ASCII, leaveOpen: true);
        var entries = BuildEntries(pixels, options, out var bitsOffset, out var xResolutionOffset, out var yResolutionOffset, out var softwareOffset, out var iccOffset, out var pixelOffset);
        writer.Write((byte)'I'); writer.Write((byte)'I'); writer.Write((ushort)42); writer.Write((uint)8);
        writer.Write((ushort)entries.Count);
        foreach (var entry in entries) { writer.Write(entry.Tag); writer.Write(entry.Type); writer.Write(entry.Count); writer.Write(entry.InlineValue); }
        writer.Write((uint)0);
        PadTo(writer, bitsOffset); writer.Write((ushort)options.BitDepth); writer.Write((ushort)options.BitDepth); writer.Write((ushort)options.BitDepth);
        PadTo(writer, xResolutionOffset); writer.Write((uint)72); writer.Write((uint)1);
        PadTo(writer, yResolutionOffset); writer.Write((uint)72); writer.Write((uint)1);
        PadTo(writer, softwareOffset); writer.Write(System.Text.Encoding.ASCII.GetBytes(options.Software + "\0"));
        if (iccOffset > 0) { PadTo(writer, iccOffset); writer.Write(options.IccProfile.Span); }
        PadTo(writer, pixelOffset);
        if (options.BitDepth == TiffBitDepth.Eight)
        {
            for (var index = 0; index < pixels.PixelCount; index++) { if ((index & 4095) == 0) token.ThrowIfCancellationRequested(); var offset = index * 3; writer.Write(pixels.Rgb24.Span[offset]); writer.Write(pixels.Rgb24.Span[offset + 1]); writer.Write(pixels.Rgb24.Span[offset + 2]); }
        }
        else
        {
            for (var index = 0; index < pixels.PixelCount; index++) { if ((index & 4095) == 0) token.ThrowIfCancellationRequested(); var offset = index * 3; writer.Write((ushort)(pixels.Rgb24.Span[offset] * 257)); writer.Write((ushort)(pixels.Rgb24.Span[offset + 1] * 257)); writer.Write((ushort)(pixels.Rgb24.Span[offset + 2] * 257)); }
        }
        writer.Flush(); return new(pixels.Width, pixels.Height, options.BitDepth, TiffCompression.None, destination.Position, options.IccProfile.Length > 0);
    }

    private sealed record Entry(ushort Tag, ushort Type, uint Count, uint InlineValue);

    private static List<Entry> BuildEntries(VisualPixelBuffer pixels, TiffExportOptions options, out int bitsOffset, out int xResolutionOffset, out int yResolutionOffset, out int softwareOffset, out int iccOffset, out int pixelOffset)
    {
        var entryCount = options.IccProfile.Length == 0 ? 12 : 13; var baseOffset = 8 + 2 + entryCount * 12 + 4; bitsOffset = baseOffset; xResolutionOffset = bitsOffset + 6; yResolutionOffset = xResolutionOffset + 8; softwareOffset = yResolutionOffset + 8; var softwareLength = System.Text.Encoding.ASCII.GetByteCount(options.Software) + 1; iccOffset = options.IccProfile.Length == 0 ? 0 : softwareOffset + softwareLength; var dataEnd = (iccOffset == 0 ? softwareOffset + softwareLength : iccOffset + options.IccProfile.Length); pixelOffset = dataEnd;
        var bits = options.BitDepth == TiffBitDepth.Eight ? 8u : 16u; var bytesPerPixel = options.BitDepth == TiffBitDepth.Eight ? 3u : 6u;
        var entries = new List<Entry>
        {
            new(256, 4, 1, (uint)pixels.Width), new(257, 4, 1, (uint)pixels.Height), new(258, 3, 3, (uint)bitsOffset),
            new(259, 3, 1, 1), new(262, 3, 1, 2), new(273, 4, 1, (uint)pixelOffset), new(277, 3, 1, 3),
            new(278, 4, 1, (uint)pixels.Height), new(279, 4, 1, checked((uint)(pixels.PixelCount * bytesPerPixel))), new(282, 5, 1, (uint)xResolutionOffset),
            new(283, 5, 1, (uint)yResolutionOffset), new(305, 2, (uint)softwareLength, (uint)softwareOffset)
        };
        if (options.IccProfile.Length > 0) entries.Add(new(34675, 7, (uint)options.IccProfile.Length, (uint)iccOffset));
        return entries.OrderBy(entry => entry.Tag).ToList();
    }

    private static void PadTo(BinaryWriter writer, int offset)
    { while (writer.BaseStream.Position < offset) writer.Write((byte)0); if (writer.BaseStream.Position != offset) throw new InvalidOperationException("TIFF layout offset was exceeded."); }

    private static bool IsValidIcc(ReadOnlySpan<byte> data)
    {
        if (data.Length < 128 || data[36] != (byte)'a' || data[37] != (byte)'c' || data[38] != (byte)'s' || data[39] != (byte)'p') return false;
        var declared = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
        return declared == data.Length;
    }
}
