using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class OnboardingRequirementTests
{
    [TestMethod]
    public void Tutorial_HasExactlyTwentyTwoOrderedSteps()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        Assert.HasCount(22, fixture.Service.Steps);
        CollectionAssert.AreEqual(Enumerable.Range(1, 22).ToArray(), fixture.Service.Steps.Select(x => x.Number).ToArray());
        Assert.HasCount(22, fixture.Service.Steps.Select(x => x.RequiredAction).Distinct().ToList());
    }

    [TestMethod]
    public void DetailsStep_TargetsFirstButtonAndExplainsHorizontalScrolling()
    {
        using var temp = new TempDirectory();
        var step = CreateFixture(temp).Service.Steps.Single(x => x.RequiredAction == TutorialAction.ViewDetails);
        Assert.AreEqual(TutorialTarget.FirstDetailsButton, step.Target);
        StringAssert.Contains(step.Instruction, "向右");
        StringAssert.Contains(step.Instruction, "查看明细");
        StringAssert.Contains(step.Instruction, "下方按钮");
    }

    [TestMethod]
    public async Task WrongAction_DoesNotAdvanceAndShowsUsefulError()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await fixture.Service.InitializeAsync(fixture.Settings, false);
        var result = await fixture.Service.PerformAsync(TutorialAction.CopyMatchedFiles, new(CopiedJpegCount: 3, CopiedRawCount: 3));
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(1, fixture.Service.State.CurrentStep);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Message));
    }

    [TestMethod]
    public async Task SourceStep_RequiresAnActuallyAddedDirectory()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await fixture.Service.InitializeAsync(fixture.Settings, false);
        await fixture.Service.PerformAsync(TutorialAction.BeginTutorial, new());
        Assert.IsFalse((await fixture.Service.PerformAsync(TutorialAction.AddSourceDirectory, new())).Succeeded);
        Assert.AreEqual(2, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task CategoryStep_RequiresEveryOptionAndReturnsToJpegAndRaw()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await MoveToCategoryStep(fixture);
        foreach (var category in Enum.GetValues<CollectionCategory>().Where(x => x != CollectionCategory.JpegAndRaw))
            await fixture.Service.PerformAsync(TutorialAction.SelectCollectionCategories, new(CollectionCategory: category));
        Assert.AreEqual(4, fixture.Service.State.CurrentStep);
        await fixture.Service.PerformAsync(TutorialAction.SelectCollectionCategories, new(CollectionCategory: CollectionCategory.JpegAndRaw));
        Assert.AreEqual(5, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task ScanStep_RequiresThreeJpegsAndThreeRaws()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await MoveToScanStep(fixture);
        Assert.IsFalse((await fixture.Service.PerformAsync(TutorialAction.ScanSourceFiles, new(IndexedJpegCount: 3, IndexedRawCount: 2))).Succeeded);
        Assert.AreEqual(5, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task OutputStep_RejectsPathOutsideTutorialSandbox()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await MoveToStep(fixture, 14);
        var result = await fixture.Service.PerformAsync(TutorialAction.SelectOutputDirectory, new(OutputDirectory: temp.Combine("真实输出")));
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(14, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task ProjectNameStep_RequiresExactTutorialName()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await MoveToStep(fixture, 15);
        Assert.IsFalse((await fixture.Service.PerformAsync(TutorialAction.EnterProjectName, new(ProjectName: "其他项目"))).Succeeded);
        Assert.AreEqual(15, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task OutputModeStep_RequiresAllModesAndReturnsToCategoryMode()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await MoveToStep(fixture, 16);
        foreach (var mode in Enum.GetValues<OutputMode>())
            await fixture.Service.PerformAsync(TutorialAction.SelectOutputModes, new(OutputMode: mode));
        Assert.AreEqual(17, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task CopyStep_RequiresAllSixTutorialFiles()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await MoveToStep(fixture, 17);
        Assert.IsFalse((await fixture.Service.PerformAsync(TutorialAction.CopyMatchedFiles, new(CopiedJpegCount: 3, CopiedRawCount: 2))).Succeeded);
        Assert.AreEqual(17, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task ReportOpenAndClearSteps_RequireRealOutcomes()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await MoveToStep(fixture, 18);
        Assert.IsFalse((await fixture.Service.PerformAsync(TutorialAction.ExportReports, new(ReportsExist: false))).Succeeded);
        await fixture.Service.PerformAsync(TutorialAction.ExportReports, new(ReportsExist: true));
        Assert.IsFalse((await fixture.Service.PerformAsync(TutorialAction.OpenOutputDirectory, new(OutputOpened: false))).Succeeded);
        await fixture.Service.PerformAsync(TutorialAction.OpenOutputDirectory, new(OutputOpened: true));
        Assert.IsFalse((await fixture.Service.PerformAsync(TutorialAction.ClearCurrentTask, new(OutputPreserved: false))).Succeeded);
        Assert.AreEqual(20, fixture.Service.State.CurrentStep);
    }

    [TestMethod]
    public async Task BackButton_PersistsRequiredTutorialProgress()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await fixture.Service.InitializeAsync(fixture.Settings, false);
        await fixture.Service.PerformAsync(TutorialAction.BeginTutorial, new());
        await fixture.Service.PerformAsync(TutorialAction.AddSourceDirectory, new(SourceDirectoryCount: 1));
        Assert.IsTrue(await fixture.Service.BackAsync());
        Assert.AreEqual(2, (await fixture.SettingsService.LoadAsync()).OnboardingCurrentStep);
    }

    [TestMethod]
    public async Task UpgradeOffer_IsShownOnlyOnceWhenDeferred()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await fixture.Service.InitializeAsync(fixture.Settings, true);
        Assert.IsTrue(fixture.Service.NeedsUpgradeOffer);
        await fixture.Service.AcceptUpgradeOfferAsync(false);
        var restarted = new OnboardingService(fixture.SettingsService, fixture.DataService);
        await restarted.InitializeAsync(await fixture.SettingsService.LoadAsync(), true);
        Assert.IsFalse(restarted.NeedsUpgradeOffer);
        Assert.AreEqual(TutorialMode.Inactive, restarted.State.Mode);
    }

    [TestMethod]
    public void InProgressTutorial_IndexAndLogDoNotReclassifyNewUserAsUpgrade()
    {
        var detector = new ExistingUserDetectionService();
        var settings = new AppSettings { OnboardingCompleted = false, OnboardingLegacyUser = false };
        var inProgress = detector.IsCurrentTutorialInProgress(settings, settingsFileWasPresent: true, legacySettingsDetected: false);
        Assert.IsTrue(inProgress);
        Assert.IsFalse(detector.IsExistingUser(settings, false, true, true, true, true, inProgress));
    }

    [TestMethod]
    public async Task ReplayCompletion_DoesNotChangeOriginalCompletionProofOrTime()
    {
        using var temp = new TempDirectory();
        var fixture = CreateFixture(temp);
        await fixture.Service.InitializeAsync(fixture.Settings, false);
        await MoveToStep(fixture, 22, alreadyInitialized: true);
        await fixture.Service.PerformAsync(TutorialAction.FinishTutorial, new());
        var completedAt = fixture.Settings.OnboardingCompletedAt;
        var proof = fixture.Settings.OnboardingCompletionProof;
        await fixture.Service.StartReplayAsync();
        await MoveToStep(fixture, 22, alreadyInitialized: true);
        await fixture.Service.PerformAsync(TutorialAction.FinishTutorial, new());
        Assert.AreEqual(completedAt, fixture.Settings.OnboardingCompletedAt);
        Assert.AreEqual(proof, fixture.Settings.OnboardingCompletionProof);
    }

    [TestMethod]
    public async Task TutorialDataEnsure_IsIdempotentAndPreservesExistingOutput()
    {
        using var temp = new TempDirectory();
        var service = new TutorialDataService(temp.Combine("KitaoPhotoSelector"));
        var paths = await service.EnsureCreatedAsync();
        var marker = Path.Combine(paths.Output, "已完成.txt");
        await File.WriteAllTextAsync(marker, "keep");
        await service.EnsureCreatedAsync();
        Assert.AreEqual("keep", await File.ReadAllTextAsync(marker));
    }

    [TestMethod]
    public async Task TutorialReset_DeletesOnlySandboxAndPreservesExternalFile()
    {
        using var temp = new TempDirectory();
        var service = new TutorialDataService(temp.Combine("KitaoPhotoSelector"));
        var paths = await service.EnsureCreatedAsync();
        await File.WriteAllTextAsync(Path.Combine(paths.Output, "旧输出.txt"), "old");
        var external = temp.CreateFile("用户照片/保留.JPG", [1, 2, 3]);
        await service.ResetAsync();
        Assert.IsFalse(File.Exists(Path.Combine(paths.Output, "旧输出.txt")));
        Assert.IsTrue(File.Exists(external));
        Assert.HasCount(3, Directory.GetFiles(paths.JpegSource, "*.JPG"));
    }

    [TestMethod]
    public async Task Settings_WritesRequiredOnboardingFieldsInLowerCamelCase()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        await new SettingsService(new TestLogService(), path).SaveAsync(new AppSettings());
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.IsTrue(json.RootElement.TryGetProperty("onboardingCompleted", out _));
        Assert.IsTrue(json.RootElement.TryGetProperty("onboardingVersion", out _));
        Assert.IsTrue(json.RootElement.TryGetProperty("onboardingCurrentStep", out _));
        Assert.IsFalse(json.RootElement.TryGetProperty("OnboardingCompleted", out _));
    }

    [TestMethod]
    public async Task Reports_AppendSoftwareNameAndVersionWithoutRemovingOldFields()
    {
        using var temp = new TempDirectory();
        var output = temp.Combine("Report");
        await new MediaReportService(new TestLogService()).ExportAsync(output, CollectionCategory.JpegAndRaw,
            [new MediaSelectionItem { OriginalInput = "1234", NormalizedName = "1234", NumericId = "1234" }]);
        var csv = await File.ReadAllTextAsync(Path.Combine(output, "匹配报告.csv"));
        StringAssert.Contains(csv, "原始输入");
        StringAssert.Contains(csv, "软件名称");
        StringAssert.Contains(csv, Branding.ProductName);
        StringAssert.Contains(await File.ReadAllTextAsync(Path.Combine(output, "匹配报告.json")), Branding.ProductVersion);
    }

    private static async Task MoveToCategoryStep(Fixture fixture)
    {
        await fixture.Service.InitializeAsync(fixture.Settings, false);
        await fixture.Service.PerformAsync(TutorialAction.BeginTutorial, new());
        await fixture.Service.PerformAsync(TutorialAction.AddSourceDirectory, new(SourceDirectoryCount: 1));
        await fixture.Service.PerformAsync(TutorialAction.RemoveSourceDirectory, new());
    }

    private static async Task MoveToScanStep(Fixture fixture)
    {
        await MoveToCategoryStep(fixture);
        foreach (var category in Enum.GetValues<CollectionCategory>())
            await fixture.Service.PerformAsync(TutorialAction.SelectCollectionCategories, new(CollectionCategory: category));
        await fixture.Service.PerformAsync(TutorialAction.SelectCollectionCategories, new(CollectionCategory: CollectionCategory.JpegAndRaw));
    }

    private static async Task MoveToStep(Fixture fixture, int target, bool alreadyInitialized = false)
    {
        if (!alreadyInitialized) await fixture.Service.InitializeAsync(fixture.Settings, false);
        while (fixture.Service.State.CurrentStep < target)
        {
            var action = fixture.Service.CurrentStep.RequiredAction;
            if (action == TutorialAction.SelectCollectionCategories)
            {
                foreach (var value in Enum.GetValues<CollectionCategory>())
                    await fixture.Service.PerformAsync(action, new(CollectionCategory: value));
                await fixture.Service.PerformAsync(action, new(CollectionCategory: CollectionCategory.JpegAndRaw));
                continue;
            }
            if (action == TutorialAction.SelectOutputModes)
            {
                foreach (var value in Enum.GetValues<OutputMode>())
                    await fixture.Service.PerformAsync(action, new(OutputMode: value));
                await fixture.Service.PerformAsync(action, new(OutputMode: OutputMode.ByFileCategory));
                continue;
            }
            var context = action switch
            {
                TutorialAction.AddSourceDirectory => new TutorialActionContext(SourceDirectoryCount: 1),
                TutorialAction.ScanSourceFiles => new TutorialActionContext(IndexedJpegCount: 3, IndexedRawCount: 3),
                TutorialAction.LoadCustomerSelection => new TutorialActionContext(SelectionCount: 1),
                TutorialAction.ParseNumbers or TutorialAction.ClearSelections => new TutorialActionContext(SelectionCount: 3),
                TutorialAction.MatchFiles => new TutorialActionContext(CompleteMatchCount: 3),
                TutorialAction.ViewDetails => new TutorialActionContext(DetailsViewed: true),
                TutorialAction.SelectOutputDirectory => new TutorialActionContext(OutputDirectory: fixture.DataService.Paths.Output),
                TutorialAction.EnterProjectName => new TutorialActionContext(ProjectName: Branding.TutorialProjectName),
                TutorialAction.CopyMatchedFiles => new TutorialActionContext(CopiedJpegCount: 3, CopiedRawCount: 3),
                TutorialAction.ExportReports => new TutorialActionContext(ReportsExist: true),
                TutorialAction.OpenOutputDirectory => new TutorialActionContext(OutputOpened: true),
                TutorialAction.ClearCurrentTask => new TutorialActionContext(OutputPreserved: true),
                _ => new TutorialActionContext()
            };
            var result = await fixture.Service.PerformAsync(action, context);
            Assert.IsTrue(result.Succeeded, $"步骤 {fixture.Service.State.CurrentStep} 未通过：{result.Message}");
        }
    }

    private static Fixture CreateFixture(TempDirectory temp)
    {
        var settingsService = new SettingsService(new TestLogService(), temp.Combine("settings.json"));
        var dataService = new TutorialDataService(temp.Combine("KitaoPhotoSelector"));
        return new Fixture(new AppSettings(), settingsService, dataService,
            new OnboardingService(settingsService, dataService, new TestLogService()));
    }

    private sealed record Fixture(
        AppSettings Settings,
        SettingsService SettingsService,
        TutorialDataService DataService,
        OnboardingService Service);
}
