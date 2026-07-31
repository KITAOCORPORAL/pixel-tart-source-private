using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class OnboardingServiceTests
{
    [TestMethod]
    public async Task NewInstall_RequiresTutorialAndCannotSkip()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await fixture.Service.InitializeAsync(fixture.Settings, existingUser: false);

        Assert.AreEqual(TutorialMode.Required, fixture.Service.State.Mode);
        Assert.AreEqual(1, fixture.Service.State.CurrentStep);
        Assert.IsFalse(fixture.Settings.OnboardingCompleted);
        Assert.IsFalse(fixture.Service.CanPerform(TutorialAction.MatchFiles));
        var skipped = await fixture.Service.PerformAsync(TutorialAction.MatchFiles, new());
        Assert.IsFalse(skipped.Succeeded);
        Assert.AreEqual(1, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task Progress_IsSavedAndRestoredAfterExit()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await fixture.Service.InitializeAsync(fixture.Settings, false);
        await fixture.Service.PerformAsync(TutorialAction.BeginTutorial, new());
        await fixture.Service.PerformAsync(TutorialAction.AddSourceDirectory, new(SourceDirectoryCount: 1));
        Assert.AreEqual(3, fixture.Settings.OnboardingCurrentStep);

        var loaded = await fixture.SettingsService.LoadAsync();
        var resumed = new OnboardingService(fixture.SettingsService, fixture.DataService, new TestLogService());
        await resumed.InitializeAsync(loaded, false);
        Assert.AreEqual(TutorialMode.Required, resumed.State.Mode);
        Assert.AreEqual(3, resumed.State.CurrentStep);
        Assert.IsFalse(loaded.OnboardingCompleted);
    }

    [TestMethod]
    public async Task OnlyFinalAction_CreatesValidCompletionProof()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await fixture.Service.InitializeAsync(fixture.Settings, false);
        fixture.Settings.OnboardingCompleted = true;
        fixture.Settings.OnboardingCompletedAt = DateTimeOffset.Now;
        Assert.IsFalse(OnboardingService.IsCompletionProofValid(fixture.Settings));

        var restarted = new OnboardingService(fixture.SettingsService, fixture.DataService, new TestLogService());
        await restarted.InitializeAsync(fixture.Settings, false);
        Assert.AreEqual(TutorialMode.Required, restarted.State.Mode);
        Assert.IsFalse(fixture.Settings.OnboardingCompleted);

        await AdvanceToFinalStep(restarted, fixture.DataService.Paths);
        Assert.IsFalse(fixture.Settings.OnboardingCompleted);
        await restarted.PerformAsync(TutorialAction.FinishTutorial, new());
        Assert.IsTrue(fixture.Settings.OnboardingCompleted);
        Assert.AreEqual(Branding.ProductVersion, fixture.Settings.OnboardingVersion);
        Assert.IsNotNull(fixture.Settings.OnboardingCompletedAt);
        Assert.IsTrue(OnboardingService.IsCompletionProofValid(fixture.Settings));
        Assert.AreEqual(TutorialMode.Inactive, restarted.State.Mode);
    }

    [TestMethod]
    public async Task ExistingUser_IsNotLockedAndCanReplayTutorial()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        fixture.Settings.SourceDirectories.Add(new SourceDirectorySetting("D:\\旧项目", SourceDirectoryType.Mixed, 0));
        await fixture.Service.InitializeAsync(fixture.Settings, existingUser: true);
        Assert.AreEqual(TutorialMode.Inactive, fixture.Service.State.Mode);
        Assert.IsTrue(fixture.Service.NeedsUpgradeOffer);

        await fixture.Service.AcceptUpgradeOfferAsync(startTutorial: true);
        Assert.AreEqual(TutorialMode.Replay, fixture.Service.State.Mode);
        fixture.Service.ExitReplay();
        Assert.AreEqual(TutorialMode.Inactive, fixture.Service.State.Mode);
    }

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
    public async Task TutorialSandbox_CreatesValidJpegsAndRawPlaceholders()
    {
        using var temp = new TempDirectory();
        var data = new TutorialDataService(temp.Path);
        var paths = await data.EnsureCreatedAsync();
        Assert.HasCount(3, Directory.GetFiles(paths.JpegSource, "*.JPG"));
        Assert.HasCount(3, Directory.GetFiles(paths.RawSource, "*.ARW"));
        Assert.IsGreaterThan(0L, new FileInfo(paths.CustomerJpeg).Length);
        var metadata = new JpegMetadataService().Read(paths.CustomerJpeg);
        Assert.IsGreaterThan(0, metadata.PixelWidth ?? 0);
        Assert.IsGreaterThan(0, metadata.PixelHeight ?? 0);
        Assert.IsTrue(data.IsWithinTutorial(paths.Output));
    }

    [TestMethod]
    public async Task TutorialDelete_RejectsExternalPathAndPreservesUserFile()
    {
        using var temp = new TempDirectory();
        var data = new TutorialDataService(temp.Combine("AppData"));
        await data.EnsureCreatedAsync();
        var userFile = temp.CreateFile("真实照片/客户原片.JPG", [1, 2, 3]);

        Assert.Throws<InvalidOperationException>(() => data.Delete(Path.GetDirectoryName(userFile)!));
        Assert.IsTrue(File.Exists(userFile));
        data.Delete(data.Paths.Root);
        Assert.IsFalse(Directory.Exists(data.Paths.Root));
        Assert.IsTrue(File.Exists(userFile));
    }

    [TestMethod]
    public async Task TutorialFlow_UsesRealIndexMatchCopyAndReportServices()
    {
        using var temp = new TempDirectory();
        var data = new TutorialDataService(temp.Combine("AppData"));
        var paths = await data.EnsureCreatedAsync();
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

        var output = Path.Combine(paths.Output, "教程流程验收");
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

    private static async Task AdvanceToFinalStep(OnboardingService service, TutorialSandboxPaths paths)
    {
        await service.PerformAsync(TutorialAction.BeginTutorial, new());
        await service.PerformAsync(TutorialAction.AddSourceDirectory, new(SourceDirectoryCount: 1));
        await service.PerformAsync(TutorialAction.RemoveSourceDirectory, new());
        foreach (var category in Enum.GetValues<CollectionCategory>())
        {
            await service.PerformAsync(TutorialAction.SelectCollectionCategories, new(CollectionCategory: category));
        }
        await service.PerformAsync(TutorialAction.SelectCollectionCategories, new(CollectionCategory: CollectionCategory.JpegAndRaw));
        await service.PerformAsync(TutorialAction.ScanSourceFiles, new(IndexedJpegCount: 3, IndexedRawCount: 3));
        await service.PerformAsync(TutorialAction.CancelSimulatedTask, new());
        await service.PerformAsync(TutorialAction.LoadCustomerSelection, new(SelectionCount: 1));
        await service.PerformAsync(TutorialAction.PasteNumbers, new());
        await service.PerformAsync(TutorialAction.ParseNumbers, new(SelectionCount: 3));
        await service.PerformAsync(TutorialAction.ClearSelections, new(SelectionCount: 3));
        await service.PerformAsync(TutorialAction.MatchFiles, new(CompleteMatchCount: 3));
        await service.PerformAsync(TutorialAction.ViewDetails, new(DetailsViewed: true));
        await service.PerformAsync(TutorialAction.AcknowledgeJpegQuality, new());
        await service.PerformAsync(TutorialAction.SelectOutputDirectory, new(OutputDirectory: paths.Output));
        await service.PerformAsync(TutorialAction.EnterProjectName, new(ProjectName: Branding.TutorialProjectName));
        foreach (var mode in Enum.GetValues<OutputMode>())
        {
            await service.PerformAsync(TutorialAction.SelectOutputModes, new(OutputMode: mode));
        }
        await service.PerformAsync(TutorialAction.SelectOutputModes, new(OutputMode: OutputMode.ByFileCategory));
        await service.PerformAsync(TutorialAction.CopyMatchedFiles, new(CopiedJpegCount: 3, CopiedRawCount: 3));
        await service.PerformAsync(TutorialAction.ExportReports, new(ReportsExist: true));
        await service.PerformAsync(TutorialAction.OpenOutputDirectory, new(OutputOpened: true));
        await service.PerformAsync(TutorialAction.ClearCurrentTask, new(OutputPreserved: true));
        await service.PerformAsync(TutorialAction.AcknowledgeEditions, new());
        Assert.AreEqual(22, service.State.CurrentStep);
    }

    private static OnboardingFixture CreateFixture(TempDirectory temp)
    {
        var settingsService = new SettingsService(new TestLogService(), temp.Combine("settings.json"));
        var dataService = new TutorialDataService(temp.Combine("KitaoPhotoSelector"));
        var settings = new AppSettings();
        return new OnboardingFixture(
            settings,
            settingsService,
            dataService,
            new OnboardingService(settingsService, dataService, new TestLogService()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("未找到解决方案目录。");
    }

    private sealed record OnboardingFixture(
        AppSettings Settings,
        SettingsService SettingsService,
        TutorialDataService DataService,
        OnboardingService Service);
}
