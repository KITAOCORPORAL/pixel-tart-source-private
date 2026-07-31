using System.Text;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class MediaCopyReportSettingsTests
{
    private readonly FileNameNormalizer _normalizer = new();

    [TestMethod]
    public async Task JpegAndRaw_CopiesBothFormats()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"));
        var raw = Entry(temp.CreateFile("source/DSC01234.ARW", [2]), temp.Combine("source"));
        var item = await MatchedItem("1234", [jpg, raw], CollectionCategory.JpegAndRaw);
        var summary = await new MediaCopyService(new TestLogService()).CopyAsync(
            [item], temp.Combine("output"), OutputMode.Flat, null, CancellationToken.None);
        Assert.AreEqual(2, summary.CopiedCount);
        Assert.IsTrue(File.Exists(temp.Combine("output", "DSC01234.JPG")));
        Assert.IsTrue(File.Exists(temp.Combine("output", "DSC01234.ARW")));
    }

    [TestMethod]
    public async Task DuplicateInputs_CopyEachSourcePathOnlyOnce()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"));
        var raw = Entry(temp.CreateFile("source/DSC01234.ARW", [2]), temp.Combine("source"));
        var first = await MatchedItem("1234", [jpg, raw], CollectionCategory.JpegAndRaw);
        var second = await MatchedItem("DSC01234.JPG", [jpg, raw], CollectionCategory.JpegAndRaw);
        var summary = await new MediaCopyService(new TestLogService()).CopyAsync(
            [first, second], temp.Combine("output"), OutputMode.Flat, null, CancellationToken.None);
        Assert.HasCount(2, summary.Outcomes);
        Assert.HasCount(2, Directory.GetFiles(temp.Combine("output")));
    }

    [TestMethod]
    public async Task CategoryOutput_PlacesFilesInJpgRawAndOtherFolders()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"));
        var raw = Entry(temp.CreateFile("source/DSC01234.ARW", [2]), temp.Combine("source"));
        var xmp = Entry(temp.CreateFile("source/DSC01234.XMP", [3]), temp.Combine("source"), [".XMP"]);
        var item = await MatchedItem("1234", [jpg, raw], CollectionCategory.JpegAndRaw);
        var custom = await MatchedItem("1234", [xmp], CollectionCategory.Custom, [".XMP"]);
        var summary = await new MediaCopyService(new TestLogService()).CopyAsync(
            [item, custom], temp.Combine("output"), OutputMode.ByFileCategory, null, CancellationToken.None);
        Assert.AreEqual(3, summary.CopiedCount);
        Assert.IsTrue(File.Exists(temp.Combine("output", "JPG", "DSC01234.JPG")));
        Assert.IsTrue(File.Exists(temp.Combine("output", "RAW", "DSC01234.ARW")));
        Assert.IsTrue(File.Exists(temp.Combine("output", "OTHER", "DSC01234.XMP")));
    }

    [TestMethod]
    public async Task OldSettingsJson_UsesNewSafeDefaults()
    {
        using var temp = new TempDirectory();
        var settingsPath = temp.Combine("settings.json");
        await File.WriteAllTextAsync(settingsPath, """
        {
          "RecentRawDirectories": ["D:\\旧照片"],
          "RecentOutputDirectory": "D:\\输出",
          "OutputMode": 0,
          "CustomRawExtensions": [".MOS"]
        }
        """, Encoding.UTF8);
        var settings = await new SettingsService(new TestLogService(), settingsPath).LoadAsync();
        Assert.AreEqual(CollectionCategory.JpegAndRaw, settings.DefaultCollectionCategory);
        CollectionAssert.Contains(settings.EnabledJpegExtensions, ".JPG");
        CollectionAssert.Contains(settings.EnabledRawExtensions, ".ARW");
        CollectionAssert.Contains(settings.EnabledRawExtensions, ".MOS");
        Assert.IsFalse(settings.AllowCustomerJpegFallback);
        Assert.AreEqual(OutputMode.Flat, settings.OutputMode);
    }

    [TestMethod]
    public void CustomExtensions_AreNormalizedDeduplicatedAndValidated()
    {
        var valid = MediaExtensionPolicy.ParseCustomExtensions("jpg, .JPG; xmp TIFF");
        Assert.IsTrue(valid.IsValid);
        CollectionAssert.AreEqual(new[] { ".JPG", ".XMP", ".TIFF" }, valid.Extensions.ToArray());
        var invalid = MediaExtensionPolicy.ParseCustomExtensions(".JPG, .BAD/EXT");
        Assert.IsFalse(invalid.IsValid);
        Assert.IsFalse(string.IsNullOrWhiteSpace(invalid.ErrorMessage));
    }

    [TestMethod]
    public async Task Report_DistinguishesCustomerJpegFromSourceJpeg()
    {
        using var temp = new TempDirectory();
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [9]);
        var sourceJpeg = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"));
        var sourceItem = await MatchedItem("DSC01234.JPG", [sourceJpeg], CollectionCategory.JpegOnly, customerInputPath: customerPath);
        var fallbackItem = new MediaSelectionItem { OriginalInput = "DSC05678.JPG", CustomerInputFilePath = temp.CreateFile("customer/DSC05678.JPG", [8]) };
        var fallbackDecision = (await new MediaMatchService(_normalizer).MatchAsync(
            [fallbackItem], new MediaIndexSnapshot(), MediaMatchOptions.Default(CollectionCategory.JpegOnly) with { AllowCustomerJpegFallback = true }, CancellationToken.None)).Single();
        fallbackItem.ApplyMatch(fallbackDecision);

        await new MediaReportService(new TestLogService()).ExportAsync(
            temp.Combine("reports"), CollectionCategory.JpegOnly, [sourceItem, fallbackItem]);

        var csv = await File.ReadAllTextAsync(temp.Combine("reports", "匹配报告.csv"), Encoding.UTF8);
        StringAssert.Contains(csv, "客户输入文件路径");
        StringAssert.Contains(csv, "是否使用客户返回文件");
        StringAssert.Contains(csv, customerPath);
        StringAssert.Contains(csv, sourceJpeg.FullPath);
        StringAssert.Contains(csv, "是");
        StringAssert.Contains(csv, "否");
    }

    [TestMethod]
    public async Task JpegConflict_DoesNotPreventCopyingMatchedRaw()
    {
        using var temp = new TempDirectory();
        var jpgA = Entry(temp.CreateFile("A/DSC01234.JPG", [1]), temp.Combine("A"));
        var jpgB = Entry(temp.CreateFile("B/DSC01234.JPG", [2]), temp.Combine("B"));
        var raw = Entry(temp.CreateFile("RAW/DSC01234.ARW", [3]), temp.Combine("RAW"));
        var item = await MatchedItem("1234", [jpgA, jpgB, raw], CollectionCategory.JpegAndRaw);
        var summary = await new MediaCopyService(new TestLogService()).CopyAsync(
            [item], temp.Combine("output"), OutputMode.Flat, null, CancellationToken.None);
        Assert.AreEqual(1, summary.CopiedCount);
        Assert.IsTrue(File.Exists(temp.Combine("output", "DSC01234.ARW")));
        Assert.IsFalse(File.Exists(temp.Combine("output", "DSC01234.JPG")));
    }

    [TestMethod]
    public async Task PartialMatch_ReportsMissingFormatReason()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"));
        var item = await MatchedItem("1234", [jpg], CollectionCategory.JpegAndRaw);
        await new MediaReportService(new TestLogService()).ExportAsync(
            temp.Combine("reports"), CollectionCategory.JpegAndRaw, [item]);
        var json = await File.ReadAllTextAsync(temp.Combine("reports", "匹配报告.json"));
        StringAssert.Contains(json, "RAW 未找到");
        StringAssert.Contains(json, "PartialMatched");
    }

    private async Task<MediaSelectionItem> MatchedItem(
        string input,
        IEnumerable<MediaFileRecord> files,
        CollectionCategory category,
        IReadOnlyList<string>? customExtensions = null,
        string customerInputPath = "")
    {
        var item = new MediaSelectionItem { OriginalInput = input, CustomerInputFilePath = customerInputPath };
        var options = MediaMatchOptions.Default(category) with { CustomExtensions = customExtensions ?? [] };
        var decision = (await new MediaMatchService(_normalizer).MatchAsync(
            [item], MediaIndexSnapshot.Create(files), options, CancellationToken.None)).Single();
        item.ApplyMatch(decision);
        return item;
    }

    private MediaFileRecord Entry(string path, string sourceRoot, IEnumerable<string>? customExtensions = null) =>
        MediaFileRecord.FromFile(path, sourceRoot, _normalizer, customExtensions ?? []);
}
