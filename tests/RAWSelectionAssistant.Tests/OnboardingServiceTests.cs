using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class OnboardingServiceTests
{
    [TestMethod]
    public void ExistingUserDetection_RecognizesAllSupportedHistorySignals()
    {
        var detector = new ExistingUserDetectionService();
        Assert.IsTrue(detector.IsExistingUser(new AppSettings(), true, false, false, false, false));
        Assert.IsTrue(detector.IsExistingUser(new AppSettings(), false, false, true, false, false));
        Assert.IsTrue(detector.IsExistingUser(new AppSettings(), false, false, false, true, false));
        Assert.IsTrue(detector.IsExistingUser(new AppSettings(), false, false, false, false, true));
        Assert.IsFalse(detector.IsExistingUser(new AppSettings(), false, false, false, false, false));
    }

    [TestMethod]
    public async Task OrdinarySplit_UsesRealIndexMatchCopyAndReportServicesWithoutTutorial()
    {
        using var temp = new TempDirectory();
        var paths = await SyntheticSplitFixture.CreateAsync(temp.Combine("AppData"));
        var log = new TestLogService();
        var normalizer = new FileNameNormalizer();
        var metadata = new JpegMetadataService(log);
        var index = await new MediaIndexService(normalizer, log, cacheFilePath: temp.Combine("index.json"), jpegMetadataService: metadata).ScanAsync(
            [new SourceDirectoryEntry { Path = paths.SourceRoot, DirectoryType = SourceDirectoryType.Mixed, Priority = 0 }],
            [".JPG", ".JPEG", ".ARW"], null, CancellationToken.None);
        Assert.HasCount(6, index.Files);

        var inputs = new[] { "DSC01234.JPG", "1235", "DSC01236.JPG" }.Select(value =>
        {
            var parsed = normalizer.Normalize(value);
            return new MediaSelectionItem { OriginalInput = value, NormalizedName = parsed.ComparisonName, NumericId = parsed.NumericId };
        }).ToList();
        var decisions = await new MediaMatchService(normalizer, metadata, new JpegQualityAssessmentService()).MatchAsync(
            inputs, index, new MediaMatchOptions(CollectionCategory.JpegAndRaw, [".JPG", ".JPEG"], [".ARW"], [], false), CancellationToken.None);
        foreach (var decision in decisions) inputs.First(x => x.Id == decision.ItemId).ApplyMatch(decision);
        Assert.IsTrue(inputs.All(x => x.OverallStatus == MediaOverallStatus.CompleteMatched));

        var output = Path.Combine(paths.Output, "正常分片验收");
        var copy = await new MediaCopyService(log).CopyAsync(inputs, output, OutputMode.ByFileCategory, null, CancellationToken.None);
        Assert.AreEqual(6, copy.CopiedCount);
        foreach (var outcome in copy.Outcomes)
        {
            var result = inputs.First(x => x.Id == outcome.ItemId).FormatResults.First(x => x.Key == outcome.FormatKey);
            result.Status = outcome.Status;
            result.OutputPath = outcome.DestinationPath;
        }
        await new MediaReportService(log).ExportAsync(output, CollectionCategory.JpegAndRaw, inputs);
        Assert.HasCount(3, Directory.GetFiles(Path.Combine(output, "JPG"), "*.JPG"));
        Assert.HasCount(3, Directory.GetFiles(Path.Combine(output, "RAW"), "*.ARW"));
        Assert.IsTrue(File.Exists(Path.Combine(output, "匹配报告.csv")));
        Assert.IsTrue(File.ReadAllBytes(Path.Combine(output, "匹配报告.csv")).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.IsTrue(File.Exists(Path.Combine(output, "匹配报告.json")));
        Assert.IsTrue(File.Exists(Path.Combine(output, "操作日志.txt")));
        StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(output, "匹配报告.csv")), "软件名称");
        StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(output, "匹配报告.json")), Branding.ProductName);
        Assert.IsTrue(File.Exists(Path.Combine(paths.JpegSource, "DSC01234.JPG")));
        Assert.IsTrue(File.Exists(Path.Combine(paths.RawSource, "DSC01234.ARW")));
    }

    [TestMethod]
    public async Task Migration_CopiesButDoesNotMoveOrOverwriteLegacyData()
    {
        using var temp = new TempDirectory();
        var legacy = temp.Combine("RAWSelectionAssistant");
        var target = temp.Combine("KitaoPhotoSelector");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(legacy, "settings.json"), "legacy");
        await File.WriteAllTextAsync(Path.Combine(target, "settings.json"), "newer");
        Directory.CreateDirectory(Path.Combine(legacy, "Indexes"));
        await File.WriteAllTextAsync(Path.Combine(legacy, "Indexes", "raw-index.json"), "index");

        Assert.IsTrue(new AppDataMigrationService(new TestLogService()).MigrateLegacyData(legacy, target));
        Assert.AreEqual("newer", await File.ReadAllTextAsync(Path.Combine(target, "settings.json")));
        Assert.AreEqual("index", await File.ReadAllTextAsync(Path.Combine(target, "Indexes", "raw-index.json")));
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "settings.json")));
    }

    [TestMethod]
    public async Task CorruptSettings_RestoreSafeNewUserState()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        await File.WriteAllTextAsync(path, "{broken");
        var service = new SettingsService(new TestLogService(), path);
        var settings = await service.LoadAsync();
        Assert.IsTrue(service.WasSettingsFileCorrupted);
        Assert.IsFalse(settings.OnboardingCompleted);
        Assert.AreEqual(1, settings.OnboardingCurrentStep);
    }

    [TestMethod]
    public void SpotlightLayout_StaysInViewportAtCommonDpiScales()
    {
        var service = new TutorialSpotlightLayoutService();
        foreach (var scale in new[] { 1.25, 1.5, 1.75 })
        {
            var layout = service.Calculate(1920 / scale, 1080 / scale, 1500 / scale, 900 / scale, 300 / scale, 90 / scale);
            Assert.IsGreaterThanOrEqualTo(0d, layout.TargetLeft);
            Assert.IsGreaterThanOrEqualTo(0d, layout.TargetTop);
            Assert.IsLessThanOrEqualTo(1920 / scale, layout.TargetLeft + layout.TargetWidth);
            Assert.IsLessThanOrEqualTo(1080 / scale, layout.TargetTop + layout.TargetHeight);
            Assert.IsLessThanOrEqualTo(1920 / scale, layout.CardLeft + 360);
            Assert.IsLessThanOrEqualTo(1080 / scale, layout.CardTop + 230);
        }
    }

    [TestMethod]
    public void Branding_UserVisibleSourcesContainOnlyNewName()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "src", "RAWSelectionAssistant", "MainWindow.xaml"),
            Path.Combine(root, "installer", "RAWSelectionAssistant.iss"),
            Path.Combine(root, "README.md")
        };
        foreach (var file in files)
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            StringAssert.Contains(text, Branding.ProductName);
            Assert.IsFalse(text.Contains("RAW 归片助手", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("RAW归片助手", StringComparison.Ordinal));
        }
    }

    private static string FindRepositoryRoot()
    { return TestResources.Root; }

}
