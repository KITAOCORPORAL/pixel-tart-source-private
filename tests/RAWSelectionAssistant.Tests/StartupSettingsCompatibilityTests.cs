using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class StartupSettingsCompatibilityTests
{
    private sealed class Log : ILogService
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("[]")]
    [DataRow("null")]
    public async Task CorruptUserSettingsFallbackTests(string json)
    {
        var directory = Directory.CreateTempSubdirectory("pt-corrupt-settings-");
        var path = Path.Combine(directory.FullName, "settings.json");
        await File.WriteAllTextAsync(path, json);
        var service = new SettingsService(new Log(), path);
        var settings = await service.LoadAsync();
        Assert.IsTrue(service.WasSettingsFileCorrupted);
        Assert.AreEqual("Workbench", settings.LastPrimaryPage);
        Assert.AreEqual(json, await File.ReadAllTextAsync(path), "Loading must not overwrite evidence");
    }

    [TestMethod]
    public async Task MissingUserSettingsFallbackTests()
    {
        var service = new SettingsService(new Log(), Path.Combine(Directory.CreateTempSubdirectory("pt-missing-settings-").FullName, "settings.json"));
        var settings = await service.LoadAsync();
        Assert.IsFalse(service.WasSettingsFilePresent);
        Assert.IsNotNull(settings.Appearance);
        Assert.AreEqual("Workbench", settings.LastPrimaryPage);
    }

    [TestMethod]
    public async Task OldUserSettingsMigrationTests()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("pt-old-settings-").FullName, "settings.json");
        await File.WriteAllTextAsync(path, """{"Weather":null,"Appearance":null,"QuickToolLayout":{"OrderedToolIds":null},"EnabledRawExtensions":null,"RecentRawDirectories":null,"LastPrimaryPage":"removed-page","RecentProjectName":"保留的项目"}""");
        var service = new SettingsService(new Log(), path);
        var settings = await service.LoadAsync();
        Assert.IsTrue(service.WasLegacySettings);
        Assert.AreEqual("Workbench", settings.LastPrimaryPage);
        Assert.AreEqual("保留的项目", settings.RecentProjectName);
        Assert.IsNotNull(settings.Weather);
        Assert.IsNotEmpty(settings.EnabledRawExtensions);
        Assert.IsNotNull(settings.RecentRawDirectories);
    }
}
