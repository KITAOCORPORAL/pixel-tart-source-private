using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class OnboardingRequirementTests
{
    [TestMethod]
    [DataRow(false, 1, false)]
    [DataRow(false, 18, false)]
    [DataRow(true, 1, false)]
    [DataRow(true, 22, true)]
    public async Task FirstInstallUpgradeInterruptedAndCompletedNeverActivateAndPreserveSettings(bool existing, int step, bool completed)
    {
        using var temp = new TempDirectory();
        var settings = new AppSettings { OnboardingCurrentStep=step, OnboardingCompleted=completed,
            OnboardingVersion="1.2.0", OnboardingCompletionProof="historical-proof", OnboardingLegacyUser=existing,
            RecentProjectName="真实项目保持", RecentOutputDirectory=temp.Combine("photos"), OnboardingUpgradeOfferShown=false };
        var settingsService = new SettingsService(new TestLogService(),temp.Combine("settings.json"));
        await settingsService.SaveAsync(settings);
        var bytes = await File.ReadAllBytesAsync(temp.Combine("settings.json"));
        var data = new TutorialDataService(temp.Combine("profile"));
        var service = new OnboardingService(settingsService,data);
        await service.InitializeAsync(settings,existing);
        await service.AcceptUpgradeOfferAsync(true);
        await service.StartReplayAsync();
        await service.PerformAsync(TutorialAction.BeginTutorial,new());
        Assert.IsFalse(service.State.IsActive); Assert.IsFalse(service.State.IsRequired);
        Assert.IsFalse(service.NeedsUpgradeOffer); Assert.HasCount(0,service.Steps);
        Assert.IsTrue(Enum.GetValues<TutorialAction>().All(service.CanPerform));
        Assert.IsFalse(await service.BackAsync());
        Assert.IsFalse(Directory.Exists(data.Paths.Root));
        Assert.AreEqual("真实项目保持",settings.RecentProjectName);
        Assert.AreEqual(step,settings.OnboardingCurrentStep);
        CollectionAssert.AreEqual(bytes,await File.ReadAllBytesAsync(temp.Combine("settings.json")));
        var reopened = await settingsService.LoadAsync();
        var restarted = new OnboardingService(settingsService,new TutorialDataService(temp.Combine("profile")));
        await restarted.InitializeAsync(reopened,existing);
        Assert.IsFalse(restarted.State.IsActive); Assert.IsFalse(restarted.NeedsUpgradeOffer);
        CollectionAssert.AreEqual(bytes,await File.ReadAllBytesAsync(temp.Combine("settings.json")));
    }
    [TestMethod]
    public async Task RetiredExampleMethodsNeitherCreateNorDeleteOrOverwriteExistingData()
    {
        using var temp = new TempDirectory();
        var data = new TutorialDataService(temp.Combine("profile"));
        await data.EnsureCreatedAsync(); await data.ResetAsync();
        Assert.IsFalse(Directory.Exists(data.Paths.Root));
        Directory.CreateDirectory(data.Paths.Root);
        var file=Path.Combine(data.Paths.Root,"user-kept.jpg");await File.WriteAllBytesAsync(file,[1,4,7]);
        var outside=temp.CreateFile("real-photo.jpg",[2,3,5]);
        var settingsService=new SettingsService(new TestLogService(),temp.Combine("settings.json"));
        var service=new OnboardingService(settingsService,data);
        await service.InitializeAsync(new(),false);
        await service.EnsureTutorialDataAsync();await service.ResetTutorialDataAsync();service.DeleteTutorialData(data.Paths.Root);
        await data.EnsureCreatedAsync();await data.ResetAsync();data.Delete(data.Paths.Root);data.Delete(outside);
        CollectionAssert.AreEqual(new byte[]{1,4,7},await File.ReadAllBytesAsync(file));
        CollectionAssert.AreEqual(new byte[]{2,3,5},await File.ReadAllBytesAsync(outside));
        Assert.HasCount(1,Directory.GetFiles(data.Paths.Root));
    }
    [TestMethod]
    public async Task RetirementDoesNotNeedWritableSettingsAndDoesNotForgeCompletion()
    {
        using var temp=new TempDirectory();var blocked=temp.Combine("settings-directory");Directory.CreateDirectory(blocked);
        var service=new OnboardingService(new SettingsService(new TestLogService(),blocked),new TutorialDataService(temp.Combine("profile")));
        var settings=new AppSettings();await service.InitializeAsync(settings,false);await service.ExitAsync();
        Assert.IsFalse(service.State.IsActive);Assert.IsFalse(settings.OnboardingCompleted);
        Assert.IsFalse(OnboardingService.IsCompletionProofValid(settings));Assert.IsTrue(Directory.Exists(blocked));
    }
}
