using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class MediaMatchServiceTests
{
    private readonly FileNameNormalizer _normalizer = new();

    [TestMethod]
    public async Task JpegOnly_FullNameMatchesJpg()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("source/DSC01234.JPG"), temp.Combine("source"));
        var result = await MatchOne("DSC01234", MediaIndexSnapshot.Create([jpg]), CollectionCategory.JpegOnly);
        Assert.AreEqual(MediaOverallStatus.CompleteMatched, result.OverallStatus);
        Assert.AreEqual("DSC01234.JPG", result.JpegResult?.SelectedFile?.FileName);
        Assert.IsNull(result.RawResult);
    }

    [TestMethod]
    public async Task JpegOnly_NumberMatchesJpeg()
    {
        using var temp = new TempDirectory();
        var jpeg = Entry(temp.CreateFile("source/DSC01234.JPEG"), temp.Combine("source"));
        var result = await MatchOne("1234", MediaIndexSnapshot.Create([jpeg]), CollectionCategory.JpegOnly);
        Assert.AreEqual("DSC01234.JPEG", result.JpegResult?.SelectedFile?.FileName);
    }

    [TestMethod]
    public async Task RawOnly_JpegInputMatchesRaw()
    {
        using var temp = new TempDirectory();
        var raw = Entry(temp.CreateFile("source/DSC01234.ARW"), temp.Combine("source"));
        var result = await MatchOne("DSC01234.JPG", MediaIndexSnapshot.Create([raw]), CollectionCategory.RawOnly);
        Assert.AreEqual(MediaOverallStatus.CompleteMatched, result.OverallStatus);
        Assert.AreEqual("DSC01234.ARW", result.RawResult?.SelectedFile?.FileName);
    }

    [TestMethod]
    public async Task JpegAndRaw_NumberMatchesBothFiles()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("jpg/DSC01234.JPG"), temp.Combine("jpg"));
        var raw = Entry(temp.CreateFile("raw/DSC01234.ARW"), temp.Combine("raw"));
        var result = await MatchOne("1234", MediaIndexSnapshot.Create([jpg, raw]), CollectionCategory.JpegAndRaw);
        Assert.AreEqual(MediaOverallStatus.CompleteMatched, result.OverallStatus);
        Assert.AreEqual(MatchStatus.Matched, result.JpegResult?.Status);
        Assert.AreEqual(MatchStatus.Matched, result.RawResult?.Status);
        Assert.AreEqual(2, result.MatchedFileCount);
    }

    [TestMethod]
    public async Task JpegFoundRawMissing_ReturnsPartialMatch()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("source/DSC01234.JPG"), temp.Combine("source"));
        var result = await MatchOne("1234", MediaIndexSnapshot.Create([jpg]), CollectionCategory.JpegAndRaw);
        Assert.AreEqual(MediaOverallStatus.PartialMatched, result.OverallStatus);
        Assert.AreEqual(MatchStatus.Matched, result.JpegResult?.Status);
        Assert.AreEqual(MatchStatus.NotFound, result.RawResult?.Status);
        StringAssert.Contains(result.Note, "RAW 未找到");
    }

    [TestMethod]
    public async Task RawFoundJpegMissing_ReturnsPartialMatch()
    {
        using var temp = new TempDirectory();
        var raw = Entry(temp.CreateFile("source/DSC01234.ARW"), temp.Combine("source"));
        var result = await MatchOne("1234", MediaIndexSnapshot.Create([raw]), CollectionCategory.JpegAndRaw);
        Assert.AreEqual(MediaOverallStatus.PartialMatched, result.OverallStatus);
        Assert.AreEqual(MatchStatus.NotFound, result.JpegResult?.Status);
        Assert.AreEqual(MatchStatus.Matched, result.RawResult?.Status);
        StringAssert.Contains(result.Note, "JPG 未找到");
    }

    [TestMethod]
    public async Task TwoJpegs_OnlyJpegResultIsConflict()
    {
        using var temp = new TempDirectory();
        var first = Entry(temp.CreateFile("A/DSC01234.JPG"), temp.Combine("A"));
        var second = Entry(temp.CreateFile("B/DSC01234.JPG"), temp.Combine("B"));
        var raw = Entry(temp.CreateFile("raw/DSC01234.ARW"), temp.Combine("raw"));
        var result = await MatchOne("1234", MediaIndexSnapshot.Create([first, second, raw]), CollectionCategory.JpegAndRaw);
        Assert.AreEqual(MatchStatus.Conflict, result.JpegResult?.Status);
        Assert.AreEqual(MatchStatus.Matched, result.RawResult?.Status);
        Assert.AreEqual(1, result.ConflictCount);
    }

    [TestMethod]
    public async Task TwoRaws_OnlyRawResultIsConflict()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("jpg/DSC01234.JPG"), temp.Combine("jpg"));
        var first = Entry(temp.CreateFile("A/DSC01234.ARW"), temp.Combine("A"));
        var second = Entry(temp.CreateFile("B/DSC01234.ARW"), temp.Combine("B"));
        var result = await MatchOne("1234", MediaIndexSnapshot.Create([jpg, first, second]), CollectionCategory.JpegAndRaw);
        Assert.AreEqual(MatchStatus.Matched, result.JpegResult?.Status);
        Assert.AreEqual(MatchStatus.Conflict, result.RawResult?.Status);
        Assert.AreEqual(1, result.MatchedFileCount);
    }

    [TestMethod]
    public async Task CustomerJpeg_DoesNotFallbackByDefault()
    {
        using var temp = new TempDirectory();
        var customerJpeg = temp.CreateFile("customer/DSC01234.JPG", [1]);
        var item = new MediaSelectionItem { OriginalInput = "DSC01234.JPG", CustomerInputFilePath = customerJpeg };
        var options = MediaMatchOptions.Default(CollectionCategory.JpegOnly) with { AllowCustomerJpegFallback = false };
        var result = (await new MediaMatchService(_normalizer).MatchAsync([item], new MediaIndexSnapshot(), options, CancellationToken.None)).Single();
        Assert.AreEqual(MatchStatus.NotFound, result.JpegResult?.Status);
        Assert.IsFalse(result.JpegResult?.UsedCustomerFile ?? true);
    }

    [TestMethod]
    public async Task CustomerJpeg_CanFallbackWhenEnabled()
    {
        using var temp = new TempDirectory();
        var customerJpeg = temp.CreateFile("customer/DSC01234.JPG", [1]);
        var item = new MediaSelectionItem { OriginalInput = "DSC01234.JPG", CustomerInputFilePath = customerJpeg };
        var options = MediaMatchOptions.Default(CollectionCategory.JpegOnly) with { AllowCustomerJpegFallback = true };
        var result = (await new MediaMatchService(_normalizer).MatchAsync([item], new MediaIndexSnapshot(), options, CancellationToken.None)).Single();
        Assert.AreEqual(MatchStatus.Matched, result.JpegResult?.Status);
        Assert.IsTrue(result.JpegResult?.UsedCustomerFile);
        Assert.IsTrue(result.JpegResult?.SelectedFile?.IsCustomerProvided);
    }

    [TestMethod]
    public async Task CustomFormat_MatchesXmpCaseInsensitively()
    {
        using var temp = new TempDirectory();
        var xmp = Entry(temp.CreateFile("source/DSC01234.xmp"), temp.Combine("source"), [".XMP"]);
        var options = MediaMatchOptions.Default(CollectionCategory.Custom) with { CustomExtensions = ["xmp", ".XMP"] };
        var item = new MediaSelectionItem { OriginalInput = "DSC01234.JPG" };
        var result = (await new MediaMatchService(_normalizer).MatchAsync([item], MediaIndexSnapshot.Create([xmp]), options, CancellationToken.None)).Single();
        Assert.AreEqual(MediaOverallStatus.CompleteMatched, result.OverallStatus);
        Assert.AreEqual(".XMP", result.FormatResults.Single().TargetExtensions.Single());
        Assert.AreEqual(FileCategory.Sidecar, result.FormatResults.Single().SelectedFile?.Category);
    }

    [TestMethod]
    public async Task JpegAndRaw_CanMatchAcrossDifferentSourceRoots()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("disk-one/DSC01234.JPG"), temp.Combine("disk-one"));
        var raw = Entry(temp.CreateFile("disk-two/DSC01234.ARW"), temp.Combine("disk-two"));
        var result = await MatchOne("DSC01234", MediaIndexSnapshot.Create([jpg, raw]), CollectionCategory.JpegAndRaw);
        Assert.AreEqual(MediaOverallStatus.CompleteMatched, result.OverallStatus);
        Assert.AreNotEqual(result.JpegResult?.SelectedFile?.SourceRoot, result.RawResult?.SelectedFile?.SourceRoot);
    }

    private async Task<MediaMatchDecision> MatchOne(string input, MediaIndexSnapshot index, CollectionCategory category)
    {
        var item = new MediaSelectionItem { OriginalInput = input };
        return (await new MediaMatchService(_normalizer).MatchAsync(
            [item], index, MediaMatchOptions.Default(category), CancellationToken.None)).Single();
    }

    private MediaFileRecord Entry(string path, string sourceRoot, IEnumerable<string>? customExtensions = null) =>
        MediaFileRecord.FromFile(path, sourceRoot, _normalizer, customExtensions ?? []);
}
