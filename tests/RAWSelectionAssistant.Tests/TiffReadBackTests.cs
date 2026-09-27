using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using System.Buffers.Binary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class TiffReadBackTests
{
    [TestMethod]
    public void WriterRoundTripsRgb48DimensionsSamplesAndTonalLevels()
    {
        var samples = new ushort[300 * 3]; for (var i = 0; i < 300; i++) samples[i * 3] = (ushort)(i * 200);
        var buffer = new HighBitDepthImageBuffer(300, 1, samples);
        using var stream = new MemoryStream(); TiffExport.WriteRgb48(stream, buffer); stream.Position = 0;
        var result = TiffReadBack.Read(stream);
        Assert.AreEqual(300, result.Width); Assert.AreEqual(1, result.Height); Assert.AreEqual(16, result.BitsPerSample);
        var red = Enumerable.Range(0, 300).Select(i => result.Rgb48Samples.Span[i * 3]).Distinct().Count();
        Assert.IsGreaterThan(256, red);
    }

    [TestMethod]
    public void WriterRoundTripsIccBinaryPayload()
    {
        var icc = new byte[128]; BinaryPrimitives.WriteUInt32BigEndian(icc.AsSpan(0, 4), 128); icc[36] = (byte)'a'; icc[37] = (byte)'c'; icc[38] = (byte)'s'; icc[39] = (byte)'p';
        using var stream = new MemoryStream(); TiffExport.WriteRgb48(stream, new HighBitDepthImageBuffer(1, 1, new ushort[] { 1, 2, 3 }), new(TiffBitDepth.Sixteen, icc)); stream.Position = 0;
        CollectionAssert.AreEqual(icc, TiffReadBack.Read(stream).IccProfile.ToArray());
    }

    [TestMethod]
    public void WriterRoundTripsDpiAndCanonicalOrientation()
    {
        using var stream = new MemoryStream();
        TiffExport.WriteRgb48(stream, new HighBitDepthImageBuffer(2, 1, new ushort[] { 1, 2, 3, 400, 500, 600 }),
            new(TiffBitDepth.Sixteen, default, "Pixel Tart", 300, 1));
        stream.Position = 0;
        var result = TiffReadBack.Read(stream);
        Assert.AreEqual(300, result.Dpi);
        Assert.AreEqual(1, result.Orientation);
    }
}
