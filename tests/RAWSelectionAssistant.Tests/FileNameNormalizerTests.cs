using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class FileNameNormalizerTests
{
    private readonly FileNameNormalizer _normalizer = new();

    [TestMethod]
    [DataRow("DSC01234.JPG", "DSC01234", "1234")]
    [DataRow("dsc01234.jpg", "DSC01234", "1234")]
    [DataRow("DSC01234 (1).JPG", "DSC01234", "1234")]
    [DataRow("DSC01234-副本.JPG", "DSC01234", "1234")]
    [DataRow("DSC01234_COPY.jpeg", "DSC01234", "1234")]
    [DataRow("01234", "01234", "1234")]
    [DataRow("1234", "1234", "1234")]
    public void Normalize_ProducesExpectedKeys(string input, string expectedName, string expectedNumber)
    {
        var result = _normalizer.Normalize(input);
        Assert.AreEqual(expectedName, result.ComparisonName);
        Assert.AreEqual(expectedNumber, result.NumericId);
    }

    [TestMethod]
    public void Normalize_StripsPathButPreservesRealName()
    {
        var result = _normalizer.Normalize(@"D:\中文 照片\DSC00042.ARW");
        Assert.AreEqual("DSC00042", result.ComparisonName);
        Assert.AreEqual("42", result.NumericId);
    }
}
