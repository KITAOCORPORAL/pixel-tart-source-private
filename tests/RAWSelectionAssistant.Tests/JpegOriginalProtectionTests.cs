using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class JpegOriginalProtectionTests
{
    private readonly FileNameNormalizer _normalizer = new();

    [TestMethod]
    public async Task SourceJpegAndCompressedCustomerJpeg_SourceAlwaysWins()
    {
        using var temp = new TempDirectory();
        var source = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"), Quality(7008, 4672, true, 14_600_000));
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [2]);
        var result = await Match(source, customerPath, CustomerJpegHandlingMode.AllowCustomerFile, Quality(1600, 1067, false, 428_000));

        Assert.AreEqual(source.FullPath, result.JpegResult?.SelectedFile?.FullPath);
        Assert.AreEqual(JpegFileSourceType.SourceDirectory, result.JpegResult?.FinalJpegSourceType);
        Assert.IsFalse(result.JpegResult?.UsedCustomerFile ?? true);
    }

    [TestMethod]
    public async Task CustomerJpegIsLarger_SourcePriorityStillWins()
    {
        using var temp = new TempDirectory();
        var source = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"), Quality(3000, 2000, true, 200_000));
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", new byte[1024]);
        var result = await Match(source, customerPath, CustomerJpegHandlingMode.AllowCustomerFile, Quality(8000, 6000, true, 30_000_000));

        Assert.AreEqual(source.FullPath, result.JpegResult?.SelectedFile?.FullPath);
    }

    [TestMethod]
    public async Task LargerSourceDimensions_AppearInRecommendationReason()
    {
        using var temp = new TempDirectory();
        var source = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"), Quality(7008, 4672, true, 10_000_000));
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [2]);
        var result = await Match(source, customerPath, CustomerJpegHandlingMode.Strict, Quality(1600, 1067, false, 400_000));

        StringAssert.Contains(result.JpegResult?.RecommendedCandidateReason ?? string.Empty, "像素尺寸");
    }

    [TestMethod]
    public async Task SourceExifAndMissingCustomerExif_AppearInComparison()
    {
        using var temp = new TempDirectory();
        var sourceQuality = Quality(6000, 4000, true, 8_000_000);
        sourceQuality.CameraMake = "Sony";
        sourceQuality.CameraModel = "ILCE-7M4";
        var source = Entry(temp.CreateFile("source/DSC01234.JPG", [1]), temp.Combine("source"), sourceQuality);
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [2]);
        var result = await Match(source, customerPath, CustomerJpegHandlingMode.Strict, Quality(1600, 1067, false, 300_000));

        StringAssert.Contains(result.JpegResult?.JpegComparisonSummary ?? string.Empty, "EXIF");
        StringAssert.Contains(result.JpegResult?.RecommendedCandidateReason ?? string.Empty, "EXIF");
    }

    [TestMethod]
    public async Task StrictMode_DoesNotAdoptCustomerJpeg()
    {
        using var temp = new TempDirectory();
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [1]);
        var result = await Match(null, customerPath, CustomerJpegHandlingMode.Strict, Quality(1600, 1067, false, 300_000));

        Assert.IsNull(result.JpegResult?.SelectedFile);
        Assert.AreEqual(MatchStatus.NotFound, result.JpegResult?.Status);
        StringAssert.Contains(result.Note, "客户返回文件未自动采用");
    }

    [TestMethod]
    public async Task SmartBackupMode_CustomerJpegWaitsForManualConfirmation()
    {
        using var temp = new TempDirectory();
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [1]);
        var result = await Match(null, customerPath, CustomerJpegHandlingMode.SmartBackup, Quality(1600, 1067, false, 300_000));

        var jpg = result.JpegResult!;
        Assert.AreEqual(MatchStatus.WaitingManualConfirmation, jpg.Status);
        Assert.IsNull(jpg.SelectedFile);
        Assert.IsTrue(jpg.RequiresManualConfirmation);

        jpg.ConfirmSelection(jpg.Candidates.Single());
        Assert.AreEqual(MatchStatus.ManuallyConfirmed, jpg.Status);
        Assert.AreEqual(JpegFileSourceType.ManuallySelectedFile, jpg.FinalJpegSourceType);
        Assert.IsTrue(jpg.CustomerJpgManualConfirmation);
    }

    [TestMethod]
    public async Task AllowCustomerMode_CanAdoptCustomerJpegWithWarning()
    {
        using var temp = new TempDirectory();
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [1]);
        var result = await Match(null, customerPath, CustomerJpegHandlingMode.AllowCustomerFile, Quality(1600, 1067, false, 300_000));

        Assert.AreEqual(Path.GetFullPath(customerPath), result.JpegResult?.SelectedFile?.FullPath);
        Assert.IsTrue(result.JpegResult?.UsedCustomerFile);
        StringAssert.Contains(result.Note, "原始质量未经确认");
    }

    [TestMethod]
    public async Task CustomerJpegAdoption_IsExplicitInReport()
    {
        using var temp = new TempDirectory();
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [1]);
        var decision = await Match(null, customerPath, CustomerJpegHandlingMode.AllowCustomerFile, Quality(1600, 1067, false, 300_000));
        var item = new MediaSelectionItem { OriginalInput = "DSC01234.JPG", CustomerInputFilePath = customerPath };
        item.ApplyMatch(decision);

        await new MediaReportService(new TestLogService()).ExportAsync(temp.Combine("report"), CollectionCategory.JpegOnly, [item]);
        var json = await File.ReadAllTextAsync(temp.Combine("report", "匹配报告.json"));
        StringAssert.Contains(json, "\"JpgSourceType\": \"CustomerReturnedFile\"");
        StringAssert.Contains(json, "\"UsedCustomerReturnedJpg\": true");
        StringAssert.Contains(json, "\"CustomerJpgManualConfirmation\": false");
    }

    [TestMethod]
    public void FileSizeThreshold_DoesNotProduceOriginalConclusion()
    {
        var assessment = new JpegQualityAssessmentService();
        var large = Quality(8000, 6000, false, 40_000_000);
        var small = Quality(8000, 6000, true, 200_000);

        assessment.Assess(large, "large.JPG");
        assessment.Assess(small, "small.JPG");

        CollectionAssert.Contains(large.QualityWarnings, "无法确认是否为原图");
        CollectionAssert.Contains(small.QualityWarnings, "无法确认是否为原图");
    }

    [TestMethod]
    public async Task MultipleSourceJpegs_RemainConflictRegardlessOfQuality()
    {
        using var temp = new TempDirectory();
        var first = Entry(temp.CreateFile("A/DSC01234.JPG", [1]), temp.Combine("A"), Quality(7008, 4672, true, 12_000_000), priority: 0);
        var second = Entry(temp.CreateFile("B/DSC01234.JPG", [2]), temp.Combine("B"), Quality(1600, 1067, false, 300_000), priority: 1);
        var options = MediaMatchOptions.Default(CollectionCategory.JpegOnly) with { CustomerJpegMode = CustomerJpegHandlingMode.Strict };
        var decision = (await new MediaMatchService(_normalizer).MatchAsync(
            [new MediaSelectionItem { OriginalInput = "DSC01234" }], MediaIndexSnapshot.Create([first, second]), options, CancellationToken.None)).Single();

        Assert.AreEqual(MatchStatus.Conflict, decision.JpegResult?.Status);
        Assert.IsNull(decision.JpegResult?.SelectedFile);
        Assert.IsNotNull(decision.JpegResult?.RecommendedFile);
    }

    [TestMethod]
    public void CorruptedMetadata_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("坏图/损坏.JPG", [1, 2, 3, 4]);

        var quality = new JpegMetadataService(new TestLogService()).Read(path);

        Assert.IsFalse(string.IsNullOrWhiteSpace(quality.MetadataReadError));
        Assert.IsNull(quality.PixelWidth);
    }

    [TestMethod]
    public void ZeroLengthAndLockedFiles_ReportMetadataErrors()
    {
        using var temp = new TempDirectory();
        var zero = temp.CreateFile("zero.JPG");
        var locked = temp.CreateFile("locked.JPG", MinimalJpeg(320, 240));
        var service = new JpegMetadataService(new TestLogService());

        var zeroInfo = service.Read(zero);
        using var lockStream = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var lockedInfo = service.Read(locked);

        Assert.IsFalse(string.IsNullOrWhiteSpace(zeroInfo.MetadataReadError));
        Assert.IsFalse(string.IsNullOrWhiteSpace(lockedInfo.MetadataReadError));
    }

    [TestMethod]
    public void ChinesePath_CanReadJpegDimensions()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("中文项目/原片/照片01234.JPG", MinimalJpeg(640, 480));

        var quality = new JpegMetadataService(new TestLogService()).Read(path);

        Assert.AreEqual(640, quality.PixelWidth);
        Assert.AreEqual(480, quality.PixelHeight);
        Assert.AreEqual(307_200L, quality.TotalPixels);
    }

    [TestMethod]
    public async Task JpegAndRaw_SameNumberStillMatchTogether()
    {
        using var temp = new TempDirectory();
        var jpg = Entry(temp.CreateFile("jpg/DSC01234.JPG", [1]), temp.Combine("jpg"), Quality(6000, 4000, true, 8_000_000));
        var raw = MediaFileRecord.FromFile(temp.CreateFile("raw/DSC01234.ARW", [2]), temp.Combine("raw"), _normalizer, []);
        var options = MediaMatchOptions.Default(CollectionCategory.JpegAndRaw) with { CustomerJpegMode = CustomerJpegHandlingMode.Strict };

        var result = (await new MediaMatchService(_normalizer).MatchAsync(
            [new MediaSelectionItem { OriginalInput = "1234" }], MediaIndexSnapshot.Create([jpg, raw]), options, CancellationToken.None)).Single();

        Assert.AreEqual(MatchStatus.Matched, result.JpegResult?.Status);
        Assert.AreEqual(MatchStatus.Matched, result.RawResult?.Status);
    }

    [TestMethod]
    public async Task CustomerJpegUsedOnlyAsInput_IsNotPlacedInCopyQueue()
    {
        using var temp = new TempDirectory();
        var customerPath = temp.CreateFile("customer/DSC01234.JPG", [1]);
        var decision = await Match(null, customerPath, CustomerJpegHandlingMode.Strict, Quality(1600, 1067, false, 300_000));
        var item = new MediaSelectionItem { OriginalInput = "DSC01234.JPG", CustomerInputFilePath = customerPath };
        item.ApplyMatch(decision);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new MediaCopyService(new TestLogService()).CopyAsync(
            [item], temp.Combine("output"), OutputMode.Flat, null, CancellationToken.None));
    }

    [TestMethod]
    public async Task JpegSourceDirectoryType_FiltersRawDuringScan()
    {
        using var temp = new TempDirectory();
        var root = temp.Combine("mixed-source");
        temp.CreateFile("mixed-source/DSC01234.JPG", MinimalJpeg(320, 240));
        temp.CreateFile("mixed-source/DSC01234.ARW", [1]);
        var service = new MediaIndexService(
            _normalizer,
            new TestLogService(),
            cacheFilePath: temp.Combine("media-index.json"),
            jpegMetadataService: new StubJpegMetadataService(string.Empty, new JpegQualityInfo()));

        var snapshot = await service.ScanAsync(
            [new SourceDirectoryEntry { Path = root, DirectoryType = SourceDirectoryType.Jpeg, Priority = 0 }],
            [".JPG", ".ARW"], null, CancellationToken.None);

        Assert.HasCount(1, snapshot.Files);
        Assert.AreEqual(".JPG", snapshot.Files.Single().Extension);
        Assert.AreEqual(SourceDirectoryType.Jpeg, snapshot.Files.Single().SourceDirectoryType);
    }

    [TestMethod]
    public async Task JpegModeAndSourceDirectorySettings_RoundTrip()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        var service = new SettingsService(new TestLogService(), path);
        var settings = new AppSettings
        {
            CustomerJpegMode = CustomerJpegHandlingMode.SmartBackup,
            SourceDirectories = [new SourceDirectorySetting("D:\\项目A\\JPG", SourceDirectoryType.Jpeg, 0)]
        };

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.AreEqual(CustomerJpegHandlingMode.SmartBackup, loaded.CustomerJpegMode);
        Assert.HasCount(1, loaded.SourceDirectories);
        Assert.AreEqual(SourceDirectoryType.Jpeg, loaded.SourceDirectories[0].DirectoryType);
    }

    private async Task<MediaMatchDecision> Match(
        MediaFileRecord? source,
        string customerPath,
        CustomerJpegHandlingMode mode,
        JpegQualityInfo customerQuality)
    {
        var reader = new StubJpegMetadataService(customerPath, customerQuality);
        var service = new MediaMatchService(_normalizer, reader, new JpegQualityAssessmentService());
        var options = MediaMatchOptions.Default(CollectionCategory.JpegOnly) with { CustomerJpegMode = mode };
        return (await service.MatchAsync(
            [new MediaSelectionItem { OriginalInput = "DSC01234.JPG", CustomerInputFilePath = customerPath }],
            MediaIndexSnapshot.Create(source is null ? [] : [source]), options, CancellationToken.None)).Single();
    }

    private MediaFileRecord Entry(string path, string root, JpegQualityInfo quality, int priority = 0)
    {
        var entry = MediaFileRecord.FromFile(path, root, _normalizer, []);
        entry.JpegQuality = quality;
        entry.JpegSourceType = JpegFileSourceType.SourceDirectory;
        entry.SourcePriority = priority;
        return entry;
    }

    private static JpegQualityInfo Quality(int width, int height, bool hasExif, long size) => new()
    {
        FileSizeBytes = size,
        PixelWidth = width,
        PixelHeight = height,
        HasExif = hasExif,
        HasIccProfile = false
    };

    private static byte[] MinimalJpeg(int width, int height) =>
    [
        0xFF, 0xD8,
        0xFF, 0xC0, 0x00, 0x11, 0x08,
        (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width,
        0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00,
        0xFF, 0xDA, 0x00, 0x0C, 0x03, 0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x00, 0x3F, 0x00,
        0xFF, 0xD9
    ];

    private sealed class StubJpegMetadataService(string path, JpegQualityInfo quality) : IJpegMetadataService
    {
        public JpegQualityInfo Read(string filePath) => string.Equals(filePath, path, StringComparison.OrdinalIgnoreCase)
            ? quality
            : new JpegQualityInfo();
    }
}
