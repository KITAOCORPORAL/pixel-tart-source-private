using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ProfessionalRawTiffTests
{
    [TestMethod]
    public void RawCapabilityListsFormatsWithoutClaimingVerification()
    {
        var capability = new LibRawDecoder().GetCapability();
        CollectionAssert.Contains(capability.CandidateExtensions.ToArray(), ".ARW");
        CollectionAssert.Contains(capability.CandidateExtensions.ToArray(), ".CR3");
        Assert.IsFalse(capability.VerifiedExtensions.Contains(".ARW") && capability.VerifiedExtensions.Contains(".CR3"));
    }

    [TestMethod]
    public void Tiff16BitPreservesDimensions()
    {
        var pixels = new VisualPixelBuffer(3, 2, new byte[] { 0, 20, 255, 30, 100, 200, 50, 120, 10, 90, 230, 80, 120, 30, 60, 255, 255, 255 });
        using var stream = new MemoryStream();
        var result = TiffExport.WriteRgb24(stream, pixels, new(TiffBitDepth.Sixteen));
        Assert.AreEqual(TiffBitDepth.Sixteen, result.BitDepth);
        Assert.AreEqual(3, result.Width); Assert.AreEqual(2, result.Height); Assert.IsFalse(result.IccEmbedded);
        var bytes = stream.ToArray();
        CollectionAssert.AreEqual(new byte[] { (byte)'I', (byte)'I', 42, 0 }, bytes[..4]);
        Assert.IsGreaterThan(pixels.PixelCount * 6, bytes.Length);
    }

    [TestMethod]
    public void TiffIccRejectsInvalidProfile()
    {
        var pixels = new VisualPixelBuffer(1, 1, new byte[] { 1, 2, 3 }); using var stream = new MemoryStream();
        Assert.ThrowsExactly<ArgumentException>(() => TiffExport.WriteRgb24(stream, pixels, new(TiffBitDepth.Sixteen, new byte[] { 1, 2, 3, 4 })));
    }

    [TestMethod]
    public void TiffCancellationStopsBeforeCompletion()
    {
        var pixels = new VisualPixelBuffer(1024, 1024, new byte[1024 * 1024 * 3]); using var stream = new MemoryStream(); using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => TiffExport.WriteRgb24(stream, pixels, token: cts.Token));
    }
}
