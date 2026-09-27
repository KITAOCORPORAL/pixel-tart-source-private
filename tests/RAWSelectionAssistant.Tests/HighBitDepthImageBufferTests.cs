using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class HighBitDepthImageBufferTests
{
    [TestMethod]
    public void Rgb24PromotesToFloatAndRgb48Without8BitLossInWorkingBuffer()
    {
        var raw = new RawDecodedImage(2, 1, 6, [0, 127, 255, 25, 128, 250], new(null, null, null, 1, "sRGB"));
        var buffer = HighBitDepthImageBuffer.FromRgb24(raw);
        Assert.AreEqual(127 / 255f, buffer.Rgb32.Span[1]); Assert.AreEqual((ushort)Math.Round(127 / 255d * 65535), buffer.ToRgb48()[1]);
        CollectionAssert.AreEqual(raw.Rgb24Pixels, buffer.ToVisualRgb24().Rgb24.ToArray());
    }

    [TestMethod]
    public void HighBitBufferWrites16BitTiff()
    {
        var buffer = new HighBitDepthImageBuffer(1, 1, new float[] { .1f, .5f, 1f }); using var stream = new MemoryStream();
        var result = TiffExport.WriteRgb48(stream, buffer);
        Assert.AreEqual(TiffBitDepth.Sixteen, result.BitDepth); Assert.IsGreaterThan(6, stream.Length); Assert.AreEqual(1, result.Width);
    }
}
