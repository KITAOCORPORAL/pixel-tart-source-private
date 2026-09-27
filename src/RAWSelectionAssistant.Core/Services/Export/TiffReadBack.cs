using System.Buffers.Binary;

namespace RAWSelectionAssistant.Core.Services.Export;

public sealed record TiffReadBackResult(int Width, int Height, int BitsPerSample, int SamplesPerPixel,
    ReadOnlyMemory<ushort> Rgb48Samples, ReadOnlyMemory<byte> IccProfile, int Orientation = 1, int Dpi = 72);

/// <summary>Bounded reader for the deterministic little-endian RGB TIFF emitted by TiffExport.</summary>
public static class TiffReadBack
{
    public static TiffReadBackResult Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var memory = new MemoryStream(); source.CopyTo(memory); var data = memory.ToArray();
        if (data.Length < 10 || data[0] != 'I' || data[1] != 'I' || BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2)) != 42)
            throw new InvalidDataException("Unsupported TIFF byte order or signature.");
        var ifd = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)));
        if (ifd < 8 || ifd + 2 > data.Length) throw new InvalidDataException("Invalid TIFF directory.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(ifd, 2)); var tags = new Dictionary<ushort, (ushort Type, uint Count, uint Value)>();
        for (var i = 0; i < count; i++)
        {
            var offset = ifd + 2 + i * 12; if (offset + 12 > data.Length) throw new InvalidDataException("Truncated TIFF directory.");
            var span = data.AsSpan(offset); tags[BinaryPrimitives.ReadUInt16LittleEndian(span)] = (BinaryPrimitives.ReadUInt16LittleEndian(span[2..]), BinaryPrimitives.ReadUInt32LittleEndian(span[4..]), BinaryPrimitives.ReadUInt32LittleEndian(span[8..]));
        }
        int Tag(ushort id) => tags.TryGetValue(id, out var tag) ? checked((int)tag.Value) : throw new InvalidDataException($"Missing TIFF tag {id}.");
        var width = Tag(256); var height = Tag(257); var bitsOffset = Tag(258); var pixelOffset = Tag(273);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(bitsOffset, 2)); var samplesPerPixel = tags.TryGetValue(277, out var spp) ? checked((int)spp.Value) : 3;
        var byteCount = checked(width * height * samplesPerPixel * (bits / 8));
        if (bits != 16 || samplesPerPixel != 3 || pixelOffset < 0 || pixelOffset + byteCount > data.Length) throw new InvalidDataException("Unsupported TIFF sample layout.");
        var samples = new ushort[width * height * 3]; var sourceSamples = data.AsSpan(pixelOffset, byteCount);
        for (var i = 0; i < samples.Length; i++) samples[i] = BinaryPrimitives.ReadUInt16LittleEndian(sourceSamples[(i * 2)..]);
        byte[] icc = [];
        if (tags.TryGetValue(34675, out var iccTag) && iccTag.Count > 0) icc = data.AsSpan(checked((int)iccTag.Value), checked((int)iccTag.Count)).ToArray();
        return new(width, height, bits, samplesPerPixel, samples, icc);
    }
}
