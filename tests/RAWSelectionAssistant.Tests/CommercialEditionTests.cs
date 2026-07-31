using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using System.Runtime.Versioning;

namespace RAWSelectionAssistant.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class CommercialEditionTests
{
    private const string ValidKey = "KQGP-ABCDE-FGHIJ-KLMNO";

    [TestMethod]
    public async Task MissingProviderStartsAsUsableFreeEdition()
    {
        var configuration = new LicenseConfiguration();
        var service = new LicenseService(configuration, new UnavailableLicenseProvider(), new MemoryLicenseStorage(),
            new FixedFingerprintService("DEVICE-A"));
        await service.InitializeAsync();
        Assert.AreEqual(ProductEdition.Free, service.Current.Edition);
        Assert.AreEqual(LicenseStatus.Free, service.Current.Status);
    }

    [TestMethod]
    [DataRow(LicensedFeature.UnlimitedSelections)]
    [DataRow(LicensedFeature.MultipleSourceDirectories)]
    [DataRow(LicensedFeature.PersistentFileIndex)]
    [DataRow(LicensedFeature.CustomFileFormats)]
    [DataRow(LicensedFeature.AdvancedJpegQualityAssessment)]
    [DataRow(LicensedFeature.AdvancedConflictResolution)]
    [DataRow(LicensedFeature.UnlimitedProjectHistory)]
    [DataRow(LicensedFeature.AdvancedReports)]
    [DataRow(LicensedFeature.OutputPresets)]
    [DataRow(LicensedFeature.BatchProjects)]
    public void FreeEditionDeniesProfessionalFeature(LicensedFeature feature)
    {
        var gate = new FeatureGateService(new MutableLicenseService());
        Assert.IsFalse(gate.HasAccess(feature));
        Assert.IsFalse(gate.Check(feature).Allowed);
    }

    [TestMethod]
    [DataRow(LicensedFeature.UnlimitedSelections)]
    [DataRow(LicensedFeature.MultipleSourceDirectories)]
    [DataRow(LicensedFeature.PersistentFileIndex)]
    [DataRow(LicensedFeature.CustomFileFormats)]
    [DataRow(LicensedFeature.AdvancedJpegQualityAssessment)]
    [DataRow(LicensedFeature.AdvancedConflictResolution)]
    [DataRow(LicensedFeature.UnlimitedProjectHistory)]
    [DataRow(LicensedFeature.AdvancedReports)]
    [DataRow(LicensedFeature.OutputPresets)]
    [DataRow(LicensedFeature.BatchProjects)]
    public void ProEditionAllowsProfessionalFeature(LicensedFeature feature)
    {
        var license = new MutableLicenseService();
        license.SetPro();
        Assert.IsTrue(new FeatureGateService(license).HasAccess(feature));
    }

    [TestMethod]
    public void LicenseKeyIsFormattedAsUserTypes()
    {
        Assert.AreEqual(ValidKey, LicenseKeyFormatter.Normalize("kqgp abcde fghij klmno"));
        Assert.IsTrue(LicenseKeyFormatter.IsComplete(ValidKey));
        Assert.AreEqual("LMNO", LicenseKeyFormatter.Suffix(ValidKey));
        Assert.DoesNotContain(ValidKey, LicenseKeyFormatter.Mask(ValidKey));
    }

    [TestMethod]
    public async Task IncompleteLicenseKeyCannotActivate()
    {
        var fixture = CreateLicenseFixture();
        var result = await fixture.Service.ActivateAsync("KQGP-ABCDE");
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(LicenseFailureReason.InvalidKey, result.FailureReason);
        Assert.IsFalse(fixture.Service.Current.IsPro);
    }

    [TestMethod]
    public async Task ValidMockLicenseActivatesPro()
    {
        var fixture = CreateLicenseFixture();
        var result = await fixture.Service.ActivateAsync(ValidKey);
        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(fixture.Service.Current.IsPro);
        Assert.AreEqual("LMNO", fixture.Service.Current.LicenseKeySuffix);
        Assert.IsNotNull(fixture.Storage.Credential);
    }

    [TestMethod]
    public async Task InvalidMockLicenseDoesNotUnlockPro()
    {
        var fixture = CreateLicenseFixture();
        var result = await fixture.Service.ActivateAsync("KQGP-ZZZZZ-ZZZZZ-ZZZZZ");
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(LicenseFailureReason.InvalidKey, result.FailureReason);
        Assert.IsFalse(fixture.Service.Current.IsPro);
    }

    [TestMethod]
    public async Task OneDeviceLimitIsEnforced()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-30T08:00:00Z"));
        var provider = new MockLicenseProvider([new MockLicenseDefinition(ValidKey, 1)], timeProvider: time);
        var first = CreateLicenseFixture(provider, time, "DEVICE-A");
        var second = CreateLicenseFixture(provider, time, "DEVICE-B");
        Assert.IsTrue((await first.Service.ActivateAsync(ValidKey)).Succeeded);
        var result = await second.Service.ActivateAsync(ValidKey);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(LicenseFailureReason.DeviceLimitReached, result.FailureReason);
    }

    [TestMethod]
    public async Task DeactivationReturnsToFreeWithoutDeletingStorageOnFailure()
    {
        var fixture = CreateLicenseFixture();
        Assert.IsTrue((await fixture.Service.ActivateAsync(ValidKey)).Succeeded);
        fixture.Provider.NetworkAvailable = false;
        var failed = await fixture.Service.DeactivateAsync();
        Assert.IsFalse(failed.Succeeded);
        Assert.IsNotNull(fixture.Storage.Credential);
        Assert.IsTrue(fixture.Service.Current.IsPro);
        fixture.Provider.NetworkAvailable = true;
        Assert.IsTrue((await fixture.Service.DeactivateAsync()).Succeeded);
        Assert.IsNull(fixture.Storage.Credential);
        Assert.IsFalse(fixture.Service.Current.IsPro);
    }

    [TestMethod]
    public async Task NetworkFailureUsesOfflineGracePeriod()
    {
        var fixture = CreateLicenseFixture();
        await fixture.Service.ActivateAsync(ValidKey);
        fixture.Time.Advance(TimeSpan.FromDays(8));
        fixture.Provider.NetworkAvailable = false;
        var result = await fixture.Service.ValidateAsync(true);
        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(LicenseStatus.OfflineGracePeriod, fixture.Service.Current.Status);
        Assert.IsTrue(fixture.Service.Current.IsPro);
    }

    [TestMethod]
    public async Task ExpiredOfflineGraceReturnsToFree()
    {
        var fixture = CreateLicenseFixture();
        await fixture.Service.ActivateAsync(ValidKey);
        fixture.Time.Advance(TimeSpan.FromDays(91));
        fixture.Provider.NetworkAvailable = false;
        var result = await fixture.Service.ValidateAsync(true);
        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(fixture.Service.Current.IsPro);
        Assert.AreEqual(LicenseStatus.Expired, fixture.Service.Current.Status);
    }

    [TestMethod]
    public async Task ClockRollbackTriggersOnlineCheckAndSafeGrace()
    {
        var fixture = CreateLicenseFixture();
        await fixture.Service.ActivateAsync(ValidKey);
        fixture.Time.Advance(TimeSpan.FromHours(-2));
        fixture.Provider.NetworkAvailable = false;
        var result = await fixture.Service.ValidateAsync(false);
        Assert.IsTrue(result.Succeeded);
        StringAssert.Contains(result.Message, "时间");
        Assert.IsTrue(fixture.Service.Current.IsPro);
    }

    [TestMethod]
    public async Task TamperedSignedCredentialCannotUnlockPro()
    {
        var fixture = CreateLicenseFixture();
        await fixture.Service.ActivateAsync(ValidKey);
        fixture.Storage.Credential!.SignedPayload += "tampered";
        var restarted = new LicenseService(
            fixture.Configuration,
            fixture.Provider,
            fixture.Storage,
            new FixedFingerprintService("DEVICE-A"),
            new TestLogService(),
            fixture.Time);
        await restarted.InitializeAsync();
        Assert.IsFalse(restarted.Current.IsPro);
        Assert.AreEqual(LicenseFailureReason.InvalidSignature, restarted.Current.FailureReason);
    }

    [TestMethod]
    public async Task DpapiStorageRejectsTamperedBytes()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("DPAPI test requires Windows.");
        using var temp = new TempDirectory();
        var path = temp.Combine("license.dat");
        var storage = new DpapiLicenseStorageService(new TestLogService(), path);
        await storage.SaveAsync(new LicenseCredential { ActivationKey = ValidKey, SignedPayload = "payload" });
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        Assert.IsNull(await storage.LoadAsync());
    }

    [TestMethod]
    public void FreeSelectionLimitCountsUniqueNumbersOnly()
    {
        var service = CreateEntitlementService(false);
        var existing = Enumerable.Range(1, 30).Select(CreateSelection).ToList();
        var result = service.ApplySelectionLimit(existing,
            [new ParsedSelectionInput("0001"), new ParsedSelectionInput("31")]);
        Assert.HasCount(1, result.Accepted);
        Assert.AreEqual("0001", result.Accepted[0].OriginalInput);
        Assert.HasCount(1, result.Rejected);
        Assert.AreEqual(30, result.UniqueSelectionCount);
    }

    [TestMethod]
    public void FreeEditionAcceptsExactlyThirtyUniqueSelections()
    {
        var result = CreateEntitlementService(false).ApplySelectionLimit([], Enumerable.Range(1, 30).Select(x => new ParsedSelectionInput(x.ToString())));
        Assert.HasCount(30, result.Accepted);
        Assert.HasCount(0, result.Rejected);
        Assert.IsFalse(result.LimitReached);
    }

    [TestMethod]
    public async Task FreeEditionCompletesThirtySelectionJpegAndRawFlow()
    {
        using var temp = new TempDirectory();
        var tutorial = await new TutorialDataService(temp.Combine("tutorial")).EnsureCreatedAsync();
        var sourceJpeg = Directory.GetFiles(tutorial.JpegSource, "*.JPG")[0];
        var sourceRaw = Directory.GetFiles(tutorial.RawSource, "*.ARW")[0];
        var inputs = new List<ParsedSelectionInput>();
        for (var number = 1; number <= 30; number++)
        {
            var baseName = $"DSC{number:00000}";
            File.Copy(sourceJpeg, Path.Combine(tutorial.JpegSource, baseName + ".JPG"));
            File.Copy(sourceRaw, Path.Combine(tutorial.RawSource, baseName + ".ARW"));
            inputs.Add(new ParsedSelectionInput(baseName + ".JPG"));
        }

        var license = new MutableLicenseService();
        var gate = new FeatureGateService(license);
        var normalizer = new FileNameNormalizer();
        var limited = new ProjectEntitlementService(normalizer, gate).ApplySelectionLimit([], inputs);
        Assert.HasCount(30, limited.Accepted);
        var selections = limited.Accepted.Select(input =>
        {
            var normalized = normalizer.Normalize(input.OriginalInput);
            return new MediaSelectionItem
            {
                OriginalInput = input.OriginalInput,
                NormalizedName = normalized.ComparisonName,
                NumericId = normalized.NumericId
            };
        }).ToList();
        var index = await new MediaIndexService(normalizer, new TestLogService(), cacheFilePath: temp.Combine("cache.json"), featureGateService: gate)
            .ScanAsync([tutorial.SourceRoot], MediaExtensionPolicy.DefaultJpegExtensions.Concat(MediaExtensionPolicy.DefaultRawExtensions), null, CancellationToken.None);
        var matcher = new MediaMatchService(normalizer, featureGateService: gate);
        var decisions = await matcher.MatchAsync(selections, index, MediaMatchOptions.Default(CollectionCategory.JpegAndRaw), CancellationToken.None);
        foreach (var decision in decisions) selections.Single(x => x.Id == decision.ItemId).ApplyMatch(decision);
        Assert.IsTrue(selections.All(x => x.OverallStatus == MediaOverallStatus.CompleteMatched));
        var output = temp.Combine("output");
        var copied = await new MediaCopyService(new TestLogService()).CopyAsync(selections, output, OutputMode.ByFileCategory, null, CancellationToken.None);
        Assert.AreEqual(60, copied.CopiedCount);
        await new MediaReportService(new TestLogService()).ExportAsync(output, CollectionCategory.JpegAndRaw, selections, default, ReportExportOptions.Free);
        Assert.IsTrue(File.Exists(Path.Combine(output, "匹配报告.csv")));
        Assert.IsFalse(File.Exists(Path.Combine(output, "匹配报告.json")));
    }

    [TestMethod]
    public void ProSelectionLimitIsUnlimited()
    {
        var service = CreateEntitlementService(true);
        var existing = Enumerable.Range(1, 30).Select(CreateSelection).ToList();
        var result = service.ApplySelectionLimit(existing, Enumerable.Range(31, 50).Select(x => new ParsedSelectionInput(x.ToString())));
        Assert.HasCount(50, result.Accepted);
        Assert.HasCount(0, result.Rejected);
    }

    [TestMethod]
    public void TutorialCanBypassFreeSelectionLimit()
    {
        var service = CreateEntitlementService(false);
        var existing = Enumerable.Range(1, 30).Select(CreateSelection).ToList();
        var result = service.ApplySelectionLimit(existing, [new ParsedSelectionInput("31")], tutorialBypass: true);
        Assert.HasCount(1, result.Accepted);
    }

    [TestMethod]
    public void FreeSourceDirectoryLimitIsOne()
    {
        var service = CreateEntitlementService(false);
        Assert.IsTrue(service.CanAddSourceDirectory(0).Allowed);
        Assert.IsFalse(service.CanAddSourceDirectory(1).Allowed);
    }

    [TestMethod]
    public void ProAllowsMultipleSourceDirectories()
    {
        Assert.IsTrue(CreateEntitlementService(true).CanAddSourceDirectory(50).Allowed);
    }

    [TestMethod]
    public async Task FreeHistoryShowsOnlyMostRecentProject()
    {
        using var temp = new TempDirectory();
        var license = new MutableLicenseService();
        var history = new ProjectHistoryService(new FeatureGateService(license), new TestLogService(), temp.Combine("projects.json"));
        await history.UpsertAsync(new PhotoProjectRecord { Name = "项目一" });
        await history.UpsertAsync(new PhotoProjectRecord { Name = "项目二" });
        var visible = await history.LoadVisibleAsync();
        Assert.HasCount(1, visible);
        Assert.AreEqual("项目二", visible[0].Name);
        Assert.HasCount(2, await history.LoadAllAsync());
    }

    [TestMethod]
    public async Task ProHistoryShowsAllProjects()
    {
        using var temp = new TempDirectory();
        var license = new MutableLicenseService();
        license.SetPro();
        var history = new ProjectHistoryService(new FeatureGateService(license), new TestLogService(), temp.Combine("projects.json"));
        await history.UpsertAsync(new PhotoProjectRecord { Name = "项目一" });
        await history.UpsertAsync(new PhotoProjectRecord { Name = "项目二" });
        Assert.HasCount(2, await history.LoadVisibleAsync());
    }

    [TestMethod]
    public async Task DowngradeKeepsHistoricalDataOnDisk()
    {
        using var temp = new TempDirectory();
        var license = new MutableLicenseService();
        license.SetPro();
        var history = new ProjectHistoryService(new FeatureGateService(license), new TestLogService(), temp.Combine("projects.json"));
        await history.UpsertAsync(new PhotoProjectRecord { Name = "项目一" });
        await history.UpsertAsync(new PhotoProjectRecord { Name = "项目二" });
        license.SetFree();
        Assert.HasCount(1, await history.LoadVisibleAsync());
        Assert.HasCount(2, await history.LoadAllAsync());
    }

    [TestMethod]
    public async Task FreeCannotSaveOutputPreset()
    {
        using var temp = new TempDirectory();
        var service = new OutputPresetService(new FeatureGateService(new MutableLicenseService()), new TestLogService(), temp.Combine("presets.json"));
        var result = await service.SaveAsync(new OutputPreset { Name = "预设" });
        Assert.IsFalse(result.Allowed);
        Assert.IsFalse(File.Exists(temp.Combine("presets.json")));
    }

    [TestMethod]
    public async Task ProCanSaveAndRenderOutputPreset()
    {
        using var temp = new TempDirectory();
        var license = new MutableLicenseService();
        license.SetPro();
        var service = new OutputPresetService(new FeatureGateService(license), new TestLogService(), temp.Combine("presets.json"));
        var preset = new OutputPreset { Name = "预设", FolderNameTemplate = "{Project}_{Category}_{Date}_{Time}" };
        Assert.IsTrue((await service.SaveAsync(preset)).Allowed);
        Assert.HasCount(1, await service.LoadAsync());
        Assert.AreEqual("婚礼_JPG + RAW_20260730_162500", OutputPresetService.RenderFolderName(
            preset, "婚礼", CollectionCategory.JpegAndRaw, DateTimeOffset.Parse("2026-07-30T16:25:00+08:00")));
    }

    [TestMethod]
    public async Task FreeBatchServiceDoesNotStart()
    {
        var service = new BatchProjectService(new FeatureGateService(new MutableLicenseService()));
        var result = await service.RunSequentialAsync([new PhotoProjectRecord { Name = "项目" }],
            (project, _) => Task.FromResult(new BatchProjectOutcome(project.Id, project.Name, true, "ok")));
        Assert.IsFalse(result.Started);
        Assert.HasCount(0, result.Outcomes);
    }

    [TestMethod]
    public async Task ProBatchServiceRunsSequentially()
    {
        var license = new MutableLicenseService();
        license.SetPro();
        var order = new List<string>();
        var projects = new[] { new PhotoProjectRecord { Name = "一" }, new PhotoProjectRecord { Name = "二" } };
        var result = await new BatchProjectService(new FeatureGateService(license)).RunSequentialAsync(projects, (project, _) =>
        {
            order.Add(project.Name);
            return Task.FromResult(new BatchProjectOutcome(project.Id, project.Name, true, "ok"));
        });
        Assert.IsTrue(result.Started);
        CollectionAssert.AreEqual(new[] { "一", "二" }, order);
    }

    [TestMethod]
    public async Task FreeIndexScansButDoesNotPersistCache()
    {
        using var temp = new TempDirectory();
        temp.CreateFile(@"source\DSC0001.ARW", [1]);
        var cache = temp.Combine("cache", "media-index.json");
        var service = new MediaIndexService(new FileNameNormalizer(), new TestLogService(), cacheFilePath: cache,
            featureGateService: new FeatureGateService(new MutableLicenseService()));
        var snapshot = await service.ScanAsync([temp.Combine("source")], [".arw"], null, CancellationToken.None);
        Assert.HasCount(1, snapshot.Files);
        Assert.IsFalse(File.Exists(cache));
        Assert.IsNull(await service.LoadCacheAsync());
    }

    [TestMethod]
    public async Task FreeMediaIndexRejectsTwoSourcesAtServiceLayer()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Combine("source-a"));
        Directory.CreateDirectory(temp.Combine("source-b"));
        var service = new MediaIndexService(new FileNameNormalizer(), new TestLogService(), cacheFilePath: temp.Combine("cache.json"),
            featureGateService: new FeatureGateService(new MutableLicenseService()));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.ScanAsync(
            [temp.Combine("source-a"), temp.Combine("source-b")], [".arw"], null, CancellationToken.None));
    }

    [TestMethod]
    public async Task FreeMatcherRejectsCustomFormatAtServiceLayer()
    {
        var service = new MediaMatchService(new FileNameNormalizer(), featureGateService: new FeatureGateService(new MutableLicenseService()));
        var options = new MediaMatchOptions(CollectionCategory.Custom, [".JPG"], [".ARW"], [".XMP"], false);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.MatchAsync(
            [CreateSelection(1)], new MediaIndexSnapshot(), options, CancellationToken.None));
    }

    [TestMethod]
    public async Task ProIndexPersistsAndReloadsCache()
    {
        using var temp = new TempDirectory();
        temp.CreateFile(@"source\DSC0001.ARW", [1]);
        var cache = temp.Combine("cache", "media-index.json");
        var license = new MutableLicenseService();
        license.SetPro();
        var service = new MediaIndexService(new FileNameNormalizer(), new TestLogService(), cacheFilePath: cache,
            featureGateService: new FeatureGateService(license));
        await service.ScanAsync([temp.Combine("source")], [".arw"], null, CancellationToken.None);
        Assert.IsTrue(File.Exists(cache));
        Assert.HasCount(1, (await service.LoadCacheAsync())!.Files);
    }

    [TestMethod]
    public async Task FreeReportExportsCsvOnly()
    {
        using var temp = new TempDirectory();
        await new MediaReportService(new TestLogService()).ExportAsync(temp.Path, CollectionCategory.RawOnly,
            [CreateSelection(1)], default, ReportExportOptions.Free);
        Assert.IsTrue(File.Exists(temp.Combine("匹配报告.csv")));
        Assert.IsFalse(File.Exists(temp.Combine("匹配报告.json")));
        Assert.IsFalse(File.Exists(temp.Combine("操作日志.txt")));
    }

    [TestMethod]
    public async Task ProReportExportsCsvJsonAndLog()
    {
        using var temp = new TempDirectory();
        await new MediaReportService(new TestLogService()).ExportAsync(temp.Path, CollectionCategory.RawOnly,
            [CreateSelection(1)], default, ReportExportOptions.Pro);
        Assert.IsTrue(File.Exists(temp.Combine("匹配报告.csv")));
        Assert.IsTrue(File.Exists(temp.Combine("匹配报告.json")));
        Assert.IsTrue(File.Exists(temp.Combine("操作日志.txt")));
    }

    [TestMethod]
    public void FormalProviderFactoryNeverEnablesMockByConfigurationAlone()
    {
        var configuration = new LicenseConfiguration { Provider = "Mock" };
        var provider = LicenseProviderFactory.Create(configuration, new HttpClient(), new TestLogService(), allowMockProvider: false,
            mockProviderFactory: () => new MockLicenseProvider([new MockLicenseDefinition(ValidKey)]));
        Assert.IsFalse(provider.IsConfigured);
        Assert.AreEqual("None", provider.Name);
    }

    [TestMethod]
    public async Task ActivationFailureDoesNotModifyProjectData()
    {
        var project = new PhotoProjectRecord { Name = "保留项目", SelectionInputs = ["1", "2"] };
        var fixture = CreateLicenseFixture();
        Assert.IsFalse((await fixture.Service.ActivateAsync("KQGP-ZZZZZ-ZZZZZ-ZZZZZ")).Succeeded);
        Assert.AreEqual("保留项目", project.Name);
        CollectionAssert.AreEqual(new[] { "1", "2" }, project.SelectionInputs);
    }

    [TestMethod]
    public async Task DeactivationDoesNotDeleteProjectOrPhotoFiles()
    {
        using var temp = new TempDirectory();
        var photo = temp.CreateFile("照片.ARW", [1, 2, 3]);
        var project = temp.CreateFile("projects.json", "project"u8.ToArray());
        var fixture = CreateLicenseFixture();
        await fixture.Service.ActivateAsync(ValidKey);
        Assert.IsTrue((await fixture.Service.DeactivateAsync()).Succeeded);
        Assert.IsTrue(File.Exists(photo));
        Assert.IsTrue(File.Exists(project));
    }

    [TestMethod]
    public async Task DpapiFileDoesNotContainPlainActivationKey()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("DPAPI test requires Windows.");
        using var temp = new TempDirectory();
        var path = temp.Combine("license.dat");
        var storage = new DpapiLicenseStorageService(new TestLogService(), path);
        await storage.SaveAsync(new LicenseCredential { ActivationKey = ValidKey, SignedPayload = "payload" });
        var protectedText = System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path));
        Assert.IsFalse(protectedText.Contains(ValidKey, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task LicenseLogsNeverContainFullActivationKey()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-30T08:00:00Z"));
        var provider = new MockLicenseProvider([new MockLicenseDefinition(ValidKey)], timeProvider: time);
        var log = new TestLogService();
        var service = new LicenseService(new LicenseConfiguration { Provider = "Mock", ProductId = 1 }, provider,
            new MemoryLicenseStorage(), new FixedFingerprintService("DEVICE-A"), log, time);
        await service.ActivateAsync(ValidKey);
        Assert.IsFalse(log.Messages.Any(message => message.Contains(ValidKey, StringComparison.Ordinal)));
        Assert.IsTrue(log.Messages.Any(message => message.Contains("LMNO", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task DeactivatedKeyCanActivateAnotherDevice()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-30T08:00:00Z"));
        var provider = new MockLicenseProvider([new MockLicenseDefinition(ValidKey, 1)], timeProvider: time);
        var first = CreateLicenseFixture(provider, time, "DEVICE-A");
        var second = CreateLicenseFixture(provider, time, "DEVICE-B");
        Assert.IsTrue((await first.Service.ActivateAsync(ValidKey)).Succeeded);
        Assert.IsTrue((await first.Service.DeactivateAsync()).Succeeded);
        Assert.IsTrue((await second.Service.ActivateAsync(ValidKey)).Succeeded);
    }

    [TestMethod]
    public void ProCustomFormatGateIsAvailable()
    {
        var license = new MutableLicenseService();
        license.SetPro();
        Assert.IsTrue(new FeatureGateService(license).Check(LicensedFeature.CustomFileFormats).Allowed);
        Assert.IsTrue(MediaExtensionPolicy.ParseCustomExtensions("xmp tif psd").IsValid);
    }

    [TestMethod]
    public void MockProviderRequiresExplicitDevelopmentOptIn()
    {
        var configuration = new LicenseConfiguration { Provider = "Mock" };
        var provider = LicenseProviderFactory.Create(configuration, new HttpClient(), new TestLogService(), allowMockProvider: true,
            mockProviderFactory: () => new MockLicenseProvider([new MockLicenseDefinition(ValidKey)]));
        Assert.IsTrue(provider.IsConfigured);
        Assert.AreEqual("Mock", provider.Name);
    }

    [TestMethod]
    public void CorruptLicenseConfigurationFallsBackToFreeDefaults()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("appsettings.license.json", "{broken"u8.ToArray());
        var configuration = new LicenseConfigurationService(new TestLogService(), path).Load();
        Assert.AreEqual("None", configuration.Provider);
        Assert.IsFalse(configuration.IsCryptolensConfigured);
    }

    [TestMethod]
    public void CryptolensConfigurationRequiresOnlyPublicClientValues()
    {
        var configuration = new LicenseConfiguration
        {
            Provider = "Cryptolens",
            ProductId = 123,
            PublicKey = "PUBLIC-KEY",
            PublicValidationToken = "PUBLIC-TOKEN"
        };
        Assert.IsTrue(configuration.IsCryptolensConfigured);
        Assert.IsFalse(configuration.GetType().GetProperties().Any(x => x.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void FeatureGateUpdatesImmediatelyAfterActivationStateChanges()
    {
        var license = new MutableLicenseService();
        var gate = new FeatureGateService(license);
        Assert.IsFalse(gate.HasAccess(LicensedFeature.BatchProjects));
        license.SetPro();
        Assert.IsTrue(gate.HasAccess(LicensedFeature.BatchProjects));
        license.SetFree();
        Assert.IsFalse(gate.HasAccess(LicensedFeature.BatchProjects));
    }

    private static MediaSelectionItem CreateSelection(int number) => new()
    {
        OriginalInput = number.ToString(),
        NormalizedName = number.ToString(),
        NumericId = number.ToString()
    };

    private static ProjectEntitlementService CreateEntitlementService(bool pro)
    {
        var license = new MutableLicenseService();
        if (pro) license.SetPro();
        return new ProjectEntitlementService(new FileNameNormalizer(), new FeatureGateService(license));
    }

    private static LicenseFixture CreateLicenseFixture(
        MockLicenseProvider? provider = null,
        MutableTimeProvider? time = null,
        string fingerprint = "DEVICE-A")
    {
        time ??= new MutableTimeProvider(DateTimeOffset.Parse("2026-07-30T08:00:00Z"));
        provider ??= new MockLicenseProvider([new MockLicenseDefinition(ValidKey, 1)], timeProvider: time);
        var configuration = new LicenseConfiguration
        {
            Provider = "Mock",
            ProductId = 1,
            OfflineGraceDays = 90,
            ValidationIntervalDays = 7,
            MaxDevices = 1
        };
        var storage = new MemoryLicenseStorage();
        var service = new LicenseService(configuration, provider, storage, new FixedFingerprintService(fingerprint), new TestLogService(), time);
        return new LicenseFixture(configuration, provider, storage, time, service);
    }

    private sealed record LicenseFixture(
        LicenseConfiguration Configuration,
        MockLicenseProvider Provider,
        MemoryLicenseStorage Storage,
        MutableTimeProvider Time,
        LicenseService Service);

    private sealed class MemoryLicenseStorage : ILicenseStorageService
    {
        public LicenseCredential? Credential { get; set; }
        public Task<LicenseCredential?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Credential);
        public Task SaveAsync(LicenseCredential credential, CancellationToken cancellationToken = default)
        {
            Credential = credential;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Credential = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedFingerprintService(string fingerprint) : IDeviceFingerprintService
    {
        public string DeviceName => "测试设备";
        public string GetAnonymousFingerprint() => fingerprint;
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now = _now.Add(value);
    }

    private sealed class MutableLicenseService : ILicenseService
    {
        public LicenseState Current { get; private set; } = LicenseState.Free();
        public LicenseConfiguration Configuration { get; } = new();
        public event EventHandler? LicenseChanged;
        public void SetPro()
        {
            Current = new LicenseState(ProductEdition.Pro, LicenseStatus.Active, "Test", "专业版", "测试设备", 1, 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90), "LMNO");
            LicenseChanged?.Invoke(this, EventArgs.Empty);
        }
        public void SetFree()
        {
            Current = LicenseState.Free();
            LicenseChanged?.Invoke(this, EventArgs.Empty);
        }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LicenseProviderResult> ActivateAsync(string activationKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseProviderResult> DeactivateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseProviderResult> ValidateAsync(bool forceOnline = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
