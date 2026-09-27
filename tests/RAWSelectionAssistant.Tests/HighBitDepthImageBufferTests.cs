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

    [TestMethod]
    public void RawRgb48PreservesMoreThan256TonalLevels()
    {
        var samples = new ushort[300 * 3];
        for (var i = 0; i < 300; i++) samples[i * 3] = (ushort)(i * 200);
        var raw = RawDecodedImage.FromRgb48(300, 1, 300 * 6, samples, new(null, null, null, 1, "sRGB"));
        var buffer = HighBitDepthImageBuffer.FromRaw(raw);
        var unique = buffer.ToRgb48().Where((_, i) => i % 3 == 0).Distinct().Count();
        Assert.IsGreaterThan(256, unique);
        Assert.AreEqual("16", buffer.SourceBitDepth);
    }

    [TestMethod]
    public void FastPreviewRemainsExplicitly8Bit()
    {
        var raw = new RawDecodedImage(1, 1, 3, [1, 2, 3], new(null, null, null, 1, "sRGB"));
        Assert.IsFalse(raw.IsProfessionalPrecision);
        Assert.AreEqual(8, raw.BitsPerChannel);
    }
}
