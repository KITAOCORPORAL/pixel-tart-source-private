using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryRecentLibrarySwitcherEndToEndTests
{
    [TestMethod]
    public async Task SwitchPersistsAcrossReloadAndRemoveRecentNeverDeletesLibrary()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RC12Recent", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var firstPath = Path.Combine(root, "first.ptlibrary");
        var secondPath = Path.Combine(root, "second.ptlibrary");
        var settingsPath = Path.Combine(root, "portable-settings.json");
        var legacyDatabasePath = Path.Combine(root, "legacy.db");
        var containers = new AssetLibraryContainerService();

        try
        {
            var first = await containers.CreateAsync(firstPath, "第一素材库");
            var second = await containers.CreateAsync(secondPath, "第二素材库");
            var settings = new AssetLibraryPortableSettings();
            var offlineLibraryId = Guid.NewGuid();
            settings.RecordOpened(offlineLibraryId, "离线素材库", Path.Combine(root, "offline.ptlibrary"), DateTimeOffset.UtcNow.AddMinutes(-10));
            settings.RecordOpened(second.LibraryId, second.DisplayName, second.ContainerPath, DateTimeOffset.UtcNow.AddMinutes(-5));
            settings.RecordOpened(first.LibraryId, first.DisplayName, first.ContainerPath, DateTimeOffset.UtcNow);

            await RunSta(async () =>
            {
                await PersistAsync(settings);
                await using (var host = CreateHost(settings))
                {
                    await host.InitializeForProductHarnessAsync();
                    Assert.AreEqual(first.LibraryId, host.CurrentDescriptor?.LibraryId);

                    await host.SwitchToContainerAsync(secondPath);
                    Assert.AreEqual(second.LibraryId, host.CurrentDescriptor?.LibraryId);
                    Assert.AreEqual(Path.GetFullPath(secondPath), settings.CurrentContainerPath);

                    var firstMenu = host.ProductHarnessMenu.Items
                        .OfType<MenuItem>()
                        .Single(item => item.Header?.ToString()?.StartsWith("第一素材库", StringComparison.Ordinal) == true);
                    StringAssert.Contains(firstMenu.ToolTip!.ToString()!, "可用");
                    StringAssert.Contains(firstMenu.ToolTip!.ToString()!, Path.GetFullPath(firstPath));
                    var offlineMenu = host.ProductHarnessMenu.Items
                        .OfType<MenuItem>()
                        .Single(item => item.Header?.ToString()?.StartsWith("离线素材库", StringComparison.Ordinal) == true);
                    StringAssert.Contains(offlineMenu.ToolTip!.ToString()!, "暂时不可用");
                    var remove = firstMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "从最近列表移除"));
                    remove.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    await WaitUntilAsync(() => settings.RecentLibraries.All(item => item.LibraryId != first.LibraryId));

                    Assert.IsTrue(Directory.Exists(firstPath), "Removing a recent entry must not delete the .ptlibrary directory.");
                    Assert.IsTrue(File.Exists(Path.Combine(firstPath, AssetLibraryContainerService.ManifestFileName)));
                }

                var reloaded = JsonSerializer.Deserialize<AssetLibraryPortableSettings>(await File.ReadAllTextAsync(settingsPath));
                Assert.IsNotNull(reloaded);
                reloaded.Normalize();
                Assert.AreEqual(Path.GetFullPath(secondPath), reloaded.CurrentContainerPath);
                Assert.IsFalse(reloaded.RecentLibraries.Any(item => item.LibraryId == first.LibraryId));

                await using var restarted = CreateHost(reloaded);
                await restarted.InitializeForProductHarnessAsync();
                Assert.AreEqual(second.LibraryId, restarted.CurrentDescriptor?.LibraryId);
                Assert.IsFalse(restarted.IsOffline);
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        AssetLibraryWorkspaceHost CreateHost(AssetLibraryPortableSettings portable) => new(
            legacyDatabasePath,
            path => new PixelTart.Modules.AssetLibrary.AssetLibraryPage(
                path,
                new TaskOperationBridge(),
                [],
                productDatabasePath: Path.Combine(root, "product.db"),
                onlineSelectionWorkspaceFile: Path.Combine(root, "online-selection.json"),
                inspirationTrayDatabasePath: Path.Combine(root, "inspiration-tray.db")),
            portable,
            () => PersistAsync(portable));

        async Task PersistAsync(AssetLibraryPortableSettings portable)
        {
            await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(portable));
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Timed out waiting for the recent-library command.");
            await Task.Delay(20);
        }
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var operation = action();
                _ = operation.ContinueWith(
                    _ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                Dispatcher.Run();
                operation.GetAwaiter().GetResult();
                completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
