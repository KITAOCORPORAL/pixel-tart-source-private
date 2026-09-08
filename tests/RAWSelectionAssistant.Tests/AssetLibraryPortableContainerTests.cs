using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryPortableContainerTests
{
    [TestMethod]
    public async Task CreateOpenAndRestartResolution_RoundTripsPortableContainer()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("中文 资料库.ptlibrary");
        var service = new AssetLibraryContainerService();

        var created = await service.CreateAsync(path, "旅行参考");
        var opened = await service.OpenAsync(path);

        Assert.AreEqual(created.LibraryId, opened.LibraryId);
        Assert.AreEqual("旅行参考", opened.DisplayName);
        Assert.IsTrue(File.Exists(opened.DatabasePath));
        Assert.IsTrue(File.Exists(Path.Combine(path, AssetLibraryContainerService.ManifestFileName)));
        Assert.IsTrue(Directory.Exists(Path.Combine(path, "assets", "managed")));

        var settingsPath = temp.Combine("settings.json");
        var settings = new AppSettings();
        settings.AssetLibraryPortable.RecordOpened(opened.LibraryId, opened.DisplayName, opened.ContainerPath, DateTimeOffset.UtcNow);
        await new SettingsService(new TestLogService(), settingsPath).SaveAsync(settings);
        var restored = await new SettingsService(new TestLogService(), settingsPath).LoadAsync();

        Assert.IsTrue(AssetLibraryContainerService.TryResolveStartupDatabasePath(restored.AssetLibraryPortable, out var databasePath));
        Assert.AreEqual(opened.DatabasePath, databasePath);
    }

    [TestMethod]
    public async Task Open_RejectsMissingOrEscapingDatabaseWithoutCreatingFiles()
    {
        using var temp = new TempDirectory();
        var missing = temp.Combine("missing.ptlibrary");
        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() => new AssetLibraryContainerService().OpenAsync(missing));
        Assert.IsFalse(Directory.Exists(missing));

        var corrupt = temp.Combine("corrupt.ptlibrary");
        Directory.CreateDirectory(corrupt);
        await File.WriteAllTextAsync(Path.Combine(corrupt, AssetLibraryContainerService.ManifestFileName),
            "{\"container_format_version\":1,\"library_id\":\"11111111-1111-1111-1111-111111111111\",\"display_name\":\"bad\",\"created_at\":\"2026-09-08T00:00:00Z\",\"database_relative_path\":\"../outside.db\"}");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new AssetLibraryContainerService().OpenAsync(corrupt));
        Assert.IsFalse(File.Exists(temp.Combine("outside.db")));
    }

    [TestMethod]
    public void RecentRegistry_NormalizesDeduplicatesAndKeepsOfflineSelection()
    {
        using var temp = new TempDirectory();
        var settings = new AssetLibraryPortableSettings();
        var id = Guid.NewGuid();
        var path = temp.Combine("A.ptlibrary");
        settings.RecordOpened(id, "A", path, DateTimeOffset.Parse("2026-09-08T01:00:00Z"));
        settings.RecordOpened(id, "A renamed", path + Path.DirectorySeparatorChar, DateTimeOffset.Parse("2026-09-08T02:00:00Z"));

        Assert.HasCount(1, settings.RecentLibraries);
        Assert.AreEqual("A renamed", settings.RecentLibraries[0].DisplayName);
        Assert.AreEqual(Path.GetFullPath(path), settings.CurrentContainerPath);
        Assert.IsFalse(AssetLibraryContainerService.TryResolveStartupDatabasePath(settings, out _));
        Assert.AreEqual(Path.GetFullPath(path), settings.CurrentContainerPath, "离线库路径应保留用于稍后重新定位，而不是被静默覆盖。");
    }

    [TestMethod]
    public async Task Open_RejectsFutureContainerVersion()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("future.ptlibrary");
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, AssetLibraryContainerService.ManifestFileName),
            JsonSerializer.Serialize(new
            {
                container_format_version = 99,
                library_id = Guid.NewGuid(),
                display_name = "future",
                created_at = DateTimeOffset.UtcNow,
                database_relative_path = AssetLibraryContainerService.DatabaseRelativePath
            }));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => new AssetLibraryContainerService().OpenAsync(path));
    }
}
