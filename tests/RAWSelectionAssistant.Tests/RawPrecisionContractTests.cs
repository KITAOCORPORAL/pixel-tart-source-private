using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class RawPrecisionContractTests
{
    [TestMethod]
    public void DecodeModesAreExplicitAndDoNotChangeLegacyDefault()
    {
        Assert.AreEqual(RawDecodeMode.FastPreview, new RawToJpegOptions().DecodeMode);
        Assert.AreEqual(RawDecodeMode.ProfessionalDecode, new RawToJpegOptions(DecodeMode: RawDecodeMode.ProfessionalDecode).DecodeMode);
    }

    [TestMethod]
    public void CandidateExtensionsRemainRecognitionOnly()
    {
        Assert.Contains(".ARW", RawToJpegDefaults.CandidateRawExtensions);
        Assert.Contains(".X3F", RawToJpegDefaults.CandidateRawExtensions);
    }
}
