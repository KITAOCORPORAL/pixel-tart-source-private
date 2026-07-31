using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class InputParserTests
{
    [TestMethod]
    public void ParseText_HandlesChineseAndCommonSeparators()
    {
        var parser = new InputParserService(new TestLogService());
        var result = parser.ParseText("客户选择：\r\n1234，1235、1236; IMG_3288.JPG|DSC01234.JPG");
        CollectionAssert.AreEqual(new[] { "1234", "1235", "1236", "IMG_3288.JPG", "DSC01234.JPG" }, result.ToArray());
    }

    [TestMethod]
    public void ParseText_KeepsNumberedCopySuffixTogether()
    {
        var parser = new InputParserService(new TestLogService());
        var result = parser.ParseText("DSC01234 (1).JPG\nDSC01235");
        CollectionAssert.AreEqual(new[] { "DSC01234(1).JPG", "DSC01235" }, result.ToArray());
    }

    [TestMethod]
    public async Task ParseDroppedFolder_ReadsJpgTxtAndCsvRecursively()
    {
        using var temp = new TempDirectory();
        temp.CreateFile("客户/A.JPG");
        temp.CreateFile("客户/ignore.png");
        await File.WriteAllTextAsync(temp.Combine("客户", "list.txt"), "1235、1236");
        await File.WriteAllTextAsync(temp.Combine("客户", "more.csv"), "IMG_3288.JPG,9999");
        var parser = new InputParserService(new TestLogService());
        var result = await parser.ParseDroppedItemsAsync([temp.Combine("客户")], null, CancellationToken.None);
        CollectionAssert.AreEquivalent(new[] { "A.JPG", "1235", "1236", "IMG_3288.JPG", "9999" }, result.ToArray());
    }
}
