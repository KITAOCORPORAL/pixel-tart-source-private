using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class RawPrecisionTraceTests
{
    [TestMethod]
    public void LoadPolicyKeepsProfessionalModeExplicit()
    {
        Assert.AreEqual(RawDecodeMode.FastPreview, RawLoadPolicy.DecodeModeFor(RawLoadLevel.EmbeddedPreview));
        Assert.AreEqual(RawDecodeMode.ProfessionalDecode, RawLoadPolicy.DecodeModeFor(RawLoadLevel.ProfessionalDecode));
    }

    [TestMethod]
    public void CacheIsBoundedAndRetainsHighPrecisionImage()
    {
        var cache = new RawDecodeCache(12);
        var key = new RawDecodeCacheKey("a.raw", 10, DateTime.UnixEpoch, RawDecodeMode.ProfessionalDecode, true, "test");
        var image = RawDecodedImage.FromRgb48(1, 1, 6, [0, 1000, 65535], new(null, null, null, 1, "sRGB"));
        cache.Put(key, image);
        Assert.IsTrue(cache.TryGet(key, out var hit));
        CollectionAssert.AreEqual(image.Rgb48Pixels!, hit.Rgb48Pixels!);
        Assert.IsLessThanOrEqualTo(12, cache.CurrentBytes);
    }

    [TestMethod]
    public void CacheKeySeparatesPreviewAndProfessionalDecode()
    {
        var common = (FullPath: "a.raw", Length: 10L, LastWriteTimeUtc: DateTime.UnixEpoch, AutoRotate: true, DecoderVersion: "test");
        var preview = new RawDecodeCacheKey(common.FullPath, common.Length, common.LastWriteTimeUtc, RawDecodeMode.FastPreview, common.AutoRotate, common.DecoderVersion);
        var professional = preview with { Mode = RawDecodeMode.ProfessionalDecode };
        Assert.AreNotEqual(preview, professional);
    }
}
