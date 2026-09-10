using System.Buffers.Binary;
using System.Text;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetTechnicalMetadataExtractorTests
{
    private readonly IAssetMetadataExtractor _extractor = new MetadataExtractorAssetMetadataExtractor();

    [TestMethod]
    public async Task PngHeaderProvidesPhysicalDimensionsWithoutInventingExif()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("landscape.png", MinimalPng(321, 123));

        var result = await _extractor.ExtractAsync(path);

        Assert.AreEqual(321, result.PixelWidth);
        Assert.AreEqual(123, result.PixelHeight);
        Assert.IsNull(result.ExifOrientation);
        Assert.IsNull(result.CaptureTime);
        Assert.AreEqual(AssetMetadataStatus.Complete, result.Status);
        Assert.IsEmpty(result.Warnings);
        Assert.IsNull(result.ErrorCode);
    }

    [TestMethod]
    public async Task ExplicitExifOrientationDoesNotSwapStoredPhysicalDimensions()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("rotated.jpg", JpegWithExif(
            width: 1200,
            height: 800,
            orientation: 6,
            captureTime: "2026:09:10 14:15:16",
            offset: "+08:00"));

        var result = await _extractor.ExtractAsync(path);

        Assert.AreEqual(1200, result.PixelWidth, "EXIF rotation must not exchange the physical dimensions.");
        Assert.AreEqual(800, result.PixelHeight, "EXIF rotation must not exchange the physical dimensions.");
        Assert.AreEqual(AssetExifOrientation.Rotate90Clockwise, result.ExifOrientation);
        Assert.AreEqual(AssetMetadataStatus.Complete, result.Status);
    }

    [TestMethod]
    public async Task ExifOriginalWithOffsetPreservesWallClockOffsetAndInstant()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("offset.jpg", JpegWithExif(
            width: 640,
            height: 480,
            orientation: 1,
            captureTime: "2026:09:10 14:15:16",
            offset: "+08:00"));

        var result = await _extractor.ExtractAsync(path);

        Assert.IsNotNull(result.CaptureTime);
        Assert.AreEqual(new DateTime(2026, 9, 10, 14, 15, 16, DateTimeKind.Unspecified), result.CaptureTime.LocalTime);
        Assert.AreEqual(DateTimeKind.Unspecified, result.CaptureTime.LocalTime.Kind);
        Assert.AreEqual(TimeSpan.FromHours(8), result.CaptureTime.Offset);
        Assert.AreEqual(AssetCaptureTimeSource.ExifOriginal, result.CaptureTime.Source);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 10, 14, 15, 16, TimeSpan.FromHours(8)), result.CaptureInstant);
    }

    [TestMethod]
    public async Task ExifOriginalWithoutOffsetDoesNotAssumeComputerTimeZone()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("unknown-zone.jpg", JpegWithExif(
            width: 640,
            height: 480,
            orientation: 1,
            captureTime: "2026:09:10 14:15:16",
            offset: null));

        var result = await _extractor.ExtractAsync(path);

        Assert.IsNotNull(result.CaptureTime);
        Assert.AreEqual(DateTimeKind.Unspecified, result.CaptureTime.LocalTime.Kind);
        Assert.IsNull(result.CaptureTime.Offset);
        Assert.IsNull(result.CaptureInstant);
        Assert.AreEqual(AssetCaptureTimeSource.ExifOriginal, result.CaptureTimeSource);
    }

    [TestMethod]
    public async Task ValidJpegWithoutExifIsCompleteWithOptionalFieldsUnknown()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("plain.jpg", MinimalJpeg(800, 533));

        var result = await _extractor.ExtractAsync(path);

        Assert.AreEqual(800, result.PixelWidth);
        Assert.AreEqual(533, result.PixelHeight);
        Assert.IsNull(result.ExifOrientation);
        Assert.IsNull(result.CaptureTime);
        Assert.AreEqual(AssetMetadataStatus.Complete, result.Status);
        Assert.IsEmpty(result.Warnings);
    }

    [TestMethod]
    public async Task InvalidExifOrientationIsPartialButKeepsTrustedDimensions()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("invalid-orientation.jpg", JpegWithExif(
            width: 900,
            height: 600,
            orientation: 9,
            captureTime: null,
            offset: null));

        var result = await _extractor.ExtractAsync(path);

        Assert.AreEqual(900, result.PixelWidth);
        Assert.AreEqual(600, result.PixelHeight);
        Assert.IsNull(result.ExifOrientation);
        Assert.AreEqual(AssetMetadataStatus.Partial, result.Status);
        Assert.IsTrue(result.Warnings.Any(warning => warning.Contains("Orientation", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CorruptImageReturnsFailedStateInsteadOfThrowing()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("corrupt.jpg", [1, 2, 3, 4, 5]);

        var result = await _extractor.ExtractAsync(path);

        Assert.AreEqual(AssetMetadataStatus.Failed, result.Status);
        Assert.AreEqual(ErrorCodeCatalog.MetadataReadFailed, result.ErrorCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.IsNull(result.PixelWidth);
        Assert.IsNull(result.PixelHeight);
    }

    [TestMethod]
    public async Task UnicodePathReadsMetadataWithoutChangingTheFile()
    {
        using var temp = new TempDirectory();
        var bytes = MinimalPng(77, 101);
        var path = temp.CreateFile("中文项目/原片/照片🌿.png", bytes);
        var before = File.ReadAllBytes(path);

        var result = await _extractor.ExtractAsync(path);

        Assert.AreEqual(77, result.PixelWidth);
        Assert.AreEqual(101, result.PixelHeight);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
    }

    [TestMethod]
    public async Task CancellationIsPropagatedRatherThanConvertedToMetadataFailure()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("cancel.png", MinimalPng(8, 6));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => _extractor.ExtractAsync(path, cancellation.Token));
    }

    private static byte[] MinimalPng(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 2;
        WritePngChunk(stream, "IHDR", header);
        WritePngChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WritePngChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> integer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(integer, data.Length);
        stream.Write(integer);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(integer, Crc32(typeBytes.Concat(data).ToArray()));
        stream.Write(integer);
    }

    private static uint Crc32(IEnumerable<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320u;
        }
        return ~crc;
    }

    private static byte[] JpegWithExif(int width, int height, ushort orientation, string? captureTime, string? offset)
    {
        var tiff = LittleEndianExif(orientation, captureTime, offset);
        using var stream = new MemoryStream();
        stream.Write([0xff, 0xd8, 0xff, 0xe1]);
        WriteUInt16BigEndian(stream, checked((ushort)(2 + 6 + tiff.Length)));
        stream.Write("Exif\0\0"u8);
        stream.Write(tiff);
        stream.Write(MinimalJpeg(width, height).AsSpan(2));
        return stream.ToArray();
    }

    private static byte[] LittleEndianExif(ushort orientation, string? captureTime, string? offset)
    {
        var hasSubIfd = captureTime is not null;
        var ifd0Count = hasSubIfd ? 2 : 1;
        var ifd0Length = 2 + ifd0Count * 12 + 4;
        var subIfdOffset = 8 + ifd0Length;
        var subIfdCount = captureTime is null ? 0 : offset is null ? 1 : 2;
        var subIfdLength = captureTime is null ? 0 : 2 + subIfdCount * 12 + 4;
        var dateBytes = captureTime is null ? [] : Encoding.ASCII.GetBytes(captureTime + "\0");
        var offsetBytes = offset is null ? [] : Encoding.ASCII.GetBytes(offset + "\0");
        var dateOffset = subIfdOffset + subIfdLength;
        var offsetOffset = dateOffset + dateBytes.Length;

        using var stream = new MemoryStream();
        stream.Write("II"u8);
        WriteUInt16LittleEndian(stream, 42);
        WriteUInt32LittleEndian(stream, 8);
        WriteUInt16LittleEndian(stream, checked((ushort)ifd0Count));
        WriteIfdEntry(stream, 0x0112, 3, 1, orientation);
        if (hasSubIfd) WriteIfdEntry(stream, 0x8769, 4, 1, checked((uint)subIfdOffset));
        WriteUInt32LittleEndian(stream, 0);

        if (hasSubIfd)
        {
            WriteUInt16LittleEndian(stream, checked((ushort)subIfdCount));
            WriteIfdEntry(stream, 0x9003, 2, checked((uint)dateBytes.Length), checked((uint)dateOffset));
            if (offset is not null)
                WriteIfdEntry(stream, 0x9011, 2, checked((uint)offsetBytes.Length), checked((uint)offsetOffset));
            WriteUInt32LittleEndian(stream, 0);
            stream.Write(dateBytes);
            stream.Write(offsetBytes);
        }

        return stream.ToArray();
    }

    private static void WriteIfdEntry(Stream stream, ushort tag, ushort type, uint count, uint value)
    {
        WriteUInt16LittleEndian(stream, tag);
        WriteUInt16LittleEndian(stream, type);
        WriteUInt32LittleEndian(stream, count);
        WriteUInt32LittleEndian(stream, value);
    }

    private static void WriteUInt16LittleEndian(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32LittleEndian(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt16BigEndian(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static byte[] MinimalJpeg(int width, int height) =>
    [
        0xff, 0xd8,
        0xff, 0xc0, 0x00, 0x11, 0x08,
        (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width,
        0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00,
        0xff, 0xda, 0x00, 0x0c, 0x03, 0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x00, 0x3f, 0x00,
        0xff, 0xd9
    ];
}
