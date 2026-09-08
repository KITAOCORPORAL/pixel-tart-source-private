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

    [TestMethod]
    public async Task Open_RejectsTamperedOrDuplicateManifestProperties()
    {
        using var temp = new TempDirectory();
        var service = new AssetLibraryContainerService();
        var tampered = temp.Combine("tampered.ptlibrary");
        await service.CreateAsync(tampered, "可信名称");
        var manifestPath = Path.Combine(tampered, AssetLibraryContainerService.ManifestFileName);
        var json = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, System.Text.RegularExpressions.Regex.Replace(
            json,
            "\\\"payload_sha256\\\": \\\"[0-9a-f]{64}\\\"",
            "\\\"payload_sha256\\\": \\\"0000000000000000000000000000000000000000000000000000000000000000\\\""));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.OpenAsync(tampered));

        var duplicate = temp.Combine("duplicate.ptlibrary");
        await service.CreateAsync(duplicate, "重复属性");
        manifestPath = Path.Combine(duplicate, AssetLibraryContainerService.ManifestFileName);
        json = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, json.Replace("{", "{\n  \"display_name\": \"伪造\",", StringComparison.Ordinal));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => service.OpenAsync(duplicate));
    }

    [TestMethod]
    public async Task WriterLease_RejectsSecondWriterAndAllowsReopenAfterRelease()
    {
        using var temp = new TempDirectory();
        var service = new AssetLibraryContainerService();
        var descriptor = await service.CreateAsync(temp.Combine("locked.ptlibrary"), "锁测试");

        await using (var first = await service.AcquireWriteLeaseAsync(descriptor))
        {
            Assert.AreEqual(descriptor.LibraryId, first.Owner.LibraryId);
            Assert.IsGreaterThan(0, first.Owner.ProcessId);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.AcquireWriteLeaseAsync(descriptor));
        }

        await using var reopened = await service.AcquireWriteLeaseAsync(descriptor);
        Assert.AreNotEqual(Guid.Empty, reopened.Owner.LeaseId);
    }

    [TestMethod]
    public async Task Create_TargetExists_DoesNotOverwriteOrDeleteIt()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("existing.ptlibrary");
        Directory.CreateDirectory(path);
        var sentinel = Path.Combine(path, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "keep");

        await Assert.ThrowsExactlyAsync<IOException>(() => new AssetLibraryContainerService().CreateAsync(path, "existing"));
        Assert.AreEqual("keep", await File.ReadAllTextAsync(sentinel));
        Assert.HasCount(0, Directory.GetDirectories(temp.Path, ".existing.ptlibrary.staging-*"));
    }
}
