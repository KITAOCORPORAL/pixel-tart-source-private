using System.IO;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryProductRelationEndToEndTests
{
    [TestMethod]
    public async Task InspirationCollectionProjectUsesPickerAndPersistsSelectionAndRemoval()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RC12CollectionProject", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var assetDatabasePath = Path.Combine(root, "asset-library.db");
        var productDatabasePath = Path.Combine(root, "product.db");
        var trayDatabasePath = Path.Combine(root, "inspiration-tray.db");
        var selectedProject = new PhotoProjectRecord { Name = "应被选择的项目", Status = PhotoProjectStatus.Ready };
        var otherProject = new PhotoProjectRecord { Name = "不应自动选择的项目", Status = PhotoProjectStatus.Draft };
        try
        {
            await using (var assets = new SqliteAssetLibraryRepository(assetDatabasePath)) await assets.InitializeAsync();
            var productDatabase = new PixelTartDatabase(productDatabasePath);
            var migration = await new DatabaseMigrator(productDatabase, new DatabaseBackupService(productDatabase, Path.Combine(root, "backups"))).MigrateAsync();
            Assert.IsTrue(migration.Success, migration.ErrorMessage);
            await new SqliteProjectRepository(productDatabase).UpsertAsync(otherProject);
            await new SqliteProjectRepository(productDatabase).UpsertAsync(selectedProject);

            await RunSta(async () =>
            {
                await using (var first = CreateViewModel())
                {
                    await first.InitializeAsync();
                    first.OpenCollectionsCommand.Execute(null);
                    await first.OpenCollectionsCommand.ExecutionTask;
                    first.CreateCollectionCommand.Execute(null);
                    await first.CreateCollectionCommand.ExecutionTask;
                    var collection = first.InspirationCollections.Single();

                    first.SetCollectionProjectCommand.Execute(collection);
                    await first.SetCollectionProjectCommand.ExecutionTask;
                    Assert.IsTrue(first.IsProjectPickerOpen);
                    Assert.AreEqual("关联灵感集项目", first.ProjectPickerTitle);
                    var selected = first.ProjectPickerItems.Single(item => item.Id == selectedProject.Id);
                    first.SelectProjectRelationCommand.Execute(selected);
                    await first.SelectProjectRelationCommand.ExecutionTask;
                    Assert.AreEqual(selectedProject.Id, first.InspirationCollections.Single().ProjectId);
                    Assert.AreEqual(selectedProject.Name, first.InspirationCollections.Single().ProjectName);
                }

                await using (var restarted = CreateViewModel())
                {
                    await restarted.InitializeAsync();
                    restarted.OpenCollectionsCommand.Execute(null);
                    await restarted.OpenCollectionsCommand.ExecutionTask;
                    var persisted = restarted.InspirationCollections.Single();
                    Assert.AreEqual(selectedProject.Id, persisted.ProjectId);
                    Assert.AreEqual(selectedProject.Name, persisted.ProjectName);
                    restarted.RemoveCollectionProjectCommand.Execute(persisted);
                    await restarted.RemoveCollectionProjectCommand.ExecutionTask;
                    Assert.IsNull(restarted.InspirationCollections.Single().ProjectId);
                }

                await using var final = CreateViewModel();
                await final.InitializeAsync();
                final.OpenCollectionsCommand.Execute(null);
                await final.OpenCollectionsCommand.ExecutionTask;
                Assert.IsNull(final.InspirationCollections.Single().ProjectId);
                Assert.AreEqual("未关联项目", final.InspirationCollections.Single().ProjectName ?? "未关联项目");
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        AssetLibraryViewModel CreateViewModel() => new(
            assetDatabasePath,
            new TaskOperationBridge(),
            productDatabasePath: productDatabasePath,
            onlineSelectionWorkspaceFile: Path.Combine(root, "online-selection.json"),
            inspirationTrayDatabasePath: trayDatabasePath);
    }

    [TestMethod]
    public async Task ProjectBookingAndClientPersistAcrossRestartAndBookingOpensInCalendar()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RC12Relations", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var assetDatabasePath = Path.Combine(root, "asset-library.db");
        var productDatabasePath = Path.Combine(root, "product.db");
        var onlineWorkspacePath = Path.Combine(root, "online-selection.json");
        var trayDatabasePath = Path.Combine(root, "inspiration-tray.db");
        var sourcePath = Path.Combine(root, "relation-source.jpg");
        var secondSourcePath = Path.Combine(root, "relation-source-2.jpg");
        var unrelatedSourcePath = Path.Combine(root, "unrelated-source.jpg");
        var project = new PhotoProjectRecord { Name = "RC12 秋季人像项目", Status = PhotoProjectStatus.Ready };
        var booking = new ShootBooking
        {
            ProjectId = project.Id,
            Title = "RC12 客户拍摄",
            ClientDisplayName = "林女士",
            StartAtUtc = new DateTimeOffset(2026, 9, 18, 2, 0, 0, TimeSpan.Zero),
            EndAtUtc = new DateTimeOffset(2026, 9, 18, 4, 0, 0, TimeSpan.Zero),
            Location = "上海影棚"
        };

        try
        {
            await File.WriteAllTextAsync(sourcePath, "rc12-relation-fixture");
            await File.WriteAllTextAsync(secondSourcePath, "rc12-relation-fixture-2");
            await File.WriteAllTextAsync(unrelatedSourcePath, "rc12-unrelated-fixture");
            await using (var assets = new SqliteAssetLibraryRepository(assetDatabasePath))
            {
                await assets.InitializeAsync();
                await assets.ImportAsync([
                    new AssetImportRequest(sourcePath),
                    new AssetImportRequest(secondSourcePath),
                    new AssetImportRequest(unrelatedSourcePath)]);
            }

            var productDatabase = new PixelTartDatabase(productDatabasePath);
            var migration = await new DatabaseMigrator(
                productDatabase,
                new DatabaseBackupService(productDatabase, Path.Combine(root, "backups"))).MigrateAsync();
            Assert.IsTrue(migration.Success, migration.ErrorMessage);
            await new SqliteProjectRepository(productDatabase).UpsertAsync(project);
            await new SqliteShootBookingRepository(productDatabase).SaveAsync(booking, []);

            await RunSta(async () =>
            {
                await using (var first = CreateViewModel())
                {
                    await first.InitializeAsync();
                    first.SyncSelection(first.AssetCards
                        .Where(card => !string.Equals(card.Asset.SourcePath, unrelatedSourcePath, StringComparison.OrdinalIgnoreCase))
                        .Select(card => card.Asset));

                    first.OpenProjectPickerCommand.Execute(null);
                    await first.OpenProjectPickerCommand.ExecutionTask;
                    var projectItem = first.ProjectPickerItems.Single(item => item.Id == project.Id);
                    first.SelectProjectRelationCommand.Execute(projectItem);
                    await first.SelectProjectRelationCommand.ExecutionTask;
                    first.RemoveProjectRelationCommand.Execute(projectItem);
                    await first.RemoveProjectRelationCommand.ExecutionTask;
                    first.SelectProjectRelationCommand.Execute(projectItem);
                    await first.SelectProjectRelationCommand.ExecutionTask;

                    first.OpenBookingPickerCommand.Execute(null);
                    await first.OpenBookingPickerCommand.ExecutionTask;
                    var bookingItem = first.BookingPickerItems.Single(item => item.Id == booking.Id);
                    Assert.AreEqual(project.Id, bookingItem.ProjectId);
                    StringAssert.Contains(bookingItem.Details, "林女士");
                    first.SelectBookingRelationCommand.Execute(bookingItem);
                    await first.SelectBookingRelationCommand.ExecutionTask;
                    first.RemoveBookingRelationCommand.Execute(bookingItem);
                    await first.RemoveBookingRelationCommand.ExecutionTask;
                    first.SelectBookingRelationCommand.Execute(bookingItem);
                    await first.SelectBookingRelationCommand.ExecutionTask;

                    Assert.AreEqual(2, first.SelectionCount);
                }

                Guid? openedBookingId = null;
                await using var restarted = CreateViewModel(id => { openedBookingId = id; return Task.CompletedTask; });
                await restarted.InitializeAsync();
                restarted.SyncSelection([restarted.AssetCards.Single(card => string.Equals(card.Asset.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)).Asset]);
                await WaitUntilAsync(() =>
                    restarted.InspectorProject == project.Name &&
                    restarted.InspectorBooking == booking.Title &&
                    restarted.InspectorClient == "林女士");

                var persistedBooking = restarted.InspectorBookingLinks.Single(item => item.Id == booking.Id);
                restarted.OpenCalendarBookingCommand.Execute(persistedBooking);
                await restarted.OpenCalendarBookingCommand.ExecutionTask;
                Assert.AreEqual(booking.Id, openedBookingId);

                await using var calendarAssets = new SqliteAssetLibraryRepository(assetDatabasePath);
                await calendarAssets.InitializeAsync();
                var bookingRepository = new SqliteShootBookingRepository(productDatabase);
                var bookingService = new ShootBookingService(bookingRepository, new BookingConflictDetector(bookingRepository));
                var details = new ShootBookingDetailsViewModel(bookingService, assetRepository: calendarAssets);
                await details.LoadAsync(booking.Id);
                Assert.AreEqual(2, details.AssetCount);

                AssetLibraryNavigationRequestEventArgs? navigation = null;
                details.AssetLibraryRequested += (_, request) => navigation = request;
                details.ViewAllAssetsCommand.Execute(null);
                Assert.IsNotNull(navigation);
                Assert.AreEqual(booking.Id, navigation.BookingId);
                await restarted.ApplyBookingFilterAsync(navigation.BookingId!.Value);
                Assert.HasCount(2, restarted.AssetCards);

                navigation = null;
                details.ViewProjectAssetsCommand.Execute(null);
                Assert.IsNotNull(navigation);
                Assert.AreEqual(project.Id, navigation.ProjectId);
                await restarted.ApplyProjectFilterAsync(navigation.ProjectId!.Value);
                Assert.HasCount(2, restarted.AssetCards);
            });

            await using var verification = new SqliteAssetLibraryRepository(assetDatabasePath);
            await verification.InitializeAsync();
            var assetIds = (await verification.QueryAsync(new(PageSize: 10))).Items.Select(item => item.AssetId).ToArray();
            Assert.HasCount(3, assetIds);
            var linkedAssets = (await verification.QueryAsync(new(PageSize: 10))).Items
                .Where(item => !string.Equals(item.SourcePath, unrelatedSourcePath, StringComparison.OrdinalIgnoreCase));
            foreach (var assetId in linkedAssets.Select(item => item.AssetId))
            {
                Assert.IsTrue((await verification.ListProjectAssetLinksAsync(assetId: assetId)).Any(link => link.ProjectId == project.Id));
                Assert.IsTrue((await verification.ListBookingAssetLinksAsync(assetId: assetId)).Any(link => link.BookingId == booking.Id));
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        AssetLibraryViewModel CreateViewModel(Func<Guid, Task>? openCalendarBooking = null) => new(
            assetDatabasePath,
            new TaskOperationBridge(),
            openCalendarBooking: openCalendarBooking,
            productDatabasePath: productDatabasePath,
            onlineSelectionWorkspaceFile: onlineWorkspacePath,
            inspirationTrayDatabasePath: trayDatabasePath);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Timed out waiting for the relation inspector to settle.");
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
