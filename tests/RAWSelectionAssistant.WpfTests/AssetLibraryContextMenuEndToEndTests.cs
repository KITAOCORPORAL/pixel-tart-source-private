using System.IO;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryContextMenuEndToEndTests
{
    [TestMethod]
    public async Task MultiSelectMetadataLifecycleAndSourceSafetyPersistAcrossRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RC12Context", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "asset-library.db");
        var paths = new[] { Path.Combine(root, "context-1.jpg"), Path.Combine(root, "context-2.jpg") };
        try
        {
            await File.WriteAllTextAsync(paths[0], "context-one");
            await File.WriteAllTextAsync(paths[1], "context-two");
            await using (var repository = new SqliteAssetLibraryRepository(databasePath))
            {
                await repository.InitializeAsync();
                await repository.ImportAsync(paths.Select(path => new AssetImportRequest(path)));
            }

            await RunSta(async () =>
            {
                await using (var viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge(),
                    inspirationTrayDatabasePath: Path.Combine(root, "tray.db")))
                {
                    await viewModel.InitializeAsync();
                    viewModel.SyncSelection(viewModel.AssetCards.Select(card => card.Asset));
                    var contextCard = viewModel.AssetCards[0];

                    await ExecuteAsync(viewModel.RateContextFourCommand, contextCard);
                    await ExecuteAsync(viewModel.WorkflowPendingRetouchCommand, contextCard);
                    Assert.IsTrue(File.Exists(paths[0]) && File.Exists(paths[1]));

                    await ExecuteAsync(viewModel.ArchiveContextCommand, contextCard);
                    Assert.IsEmpty(viewModel.AssetCards);
                    Assert.IsTrue(File.Exists(paths[0]) && File.Exists(paths[1]));

                    viewModel.SystemCollections.Single(item => item.Collection == AssetLibrarySystemCollection.Archived).SelectCommand.Execute(null);
                    await WaitUntilAsync(() => !viewModel.IsLoading && viewModel.AssetCards.Count == 2);
                    viewModel.SyncSelection(viewModel.AssetCards.Select(card => card.Asset));
                    contextCard = viewModel.AssetCards[0];
                    await ExecuteAsync(viewModel.RestoreContextCommand, contextCard);
                    Assert.IsTrue(File.Exists(paths[0]) && File.Exists(paths[1]));

                    viewModel.SystemCollections.Single(item => item.Collection == AssetLibrarySystemCollection.AllAssets).SelectCommand.Execute(null);
                    await WaitUntilAsync(() => !viewModel.IsLoading && viewModel.AssetCards.Count == 2);
                    viewModel.SyncSelection(viewModel.AssetCards.Select(card => card.Asset));
                    contextCard = viewModel.AssetCards[0];
                    await ExecuteAsync(viewModel.TrashContextCommand, contextCard);
                    Assert.IsEmpty(viewModel.AssetCards);
                    Assert.IsTrue(File.Exists(paths[0]) && File.Exists(paths[1]));

                    viewModel.SystemCollections.Single(item => item.Collection == AssetLibrarySystemCollection.RecycleBin).SelectCommand.Execute(null);
                    await WaitUntilAsync(() => !viewModel.IsLoading && viewModel.AssetCards.Count == 2);
                    viewModel.SyncSelection(viewModel.AssetCards.Select(card => card.Asset));
                    await ExecuteAsync(viewModel.RestoreTrashContextCommand, viewModel.AssetCards[0]);
                    Assert.IsTrue(File.Exists(paths[0]) && File.Exists(paths[1]));
                }

                await using var restarted = new AssetLibraryViewModel(databasePath, new TaskOperationBridge(),
                    inspirationTrayDatabasePath: Path.Combine(root, "tray.db"));
                await restarted.InitializeAsync();
                Assert.HasCount(2, restarted.AssetCards);
                await using var verification = new SqliteAssetLibraryRepository(databasePath);
                await verification.InitializeAsync();
                foreach (var card in restarted.AssetCards)
                {
                    Assert.AreEqual(4, card.Asset.Rating);
                    var workflow = await verification.GetAssetWorkflowMetadataAsync(card.Asset.AssetId);
                    Assert.AreEqual(AssetWorkflowStatus.PendingRetouch, workflow?.WorkflowStatus);
                }
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task ExecuteAsync<T>(AsyncCommand<T> command, T parameter)
    {
        Assert.IsTrue(command.CanExecute(parameter));
        command.Execute(parameter);
        await command.ExecutionTask;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Timed out waiting for context-menu state refresh.");
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
