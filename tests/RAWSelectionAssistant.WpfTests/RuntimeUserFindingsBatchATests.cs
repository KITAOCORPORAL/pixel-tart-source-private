using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class RuntimeUserFindingsBatchATests
{
    [TestMethod]
    public Task InspectorRelationsPersistRefreshQueryAndSurviveReload() => RunSta(async () =>
    {
        var root = await Fixture();
        var db = Path.Combine(root, "assets.db");
        await using var repository = new SqliteAssetLibraryRepository(db);
        await repository.InitializeAsync();
        await using (var vm = ViewModel(root))
        {
            await vm.InitializeAsync();
            vm.SyncSelection([vm.AssetCards[0].Asset]);
            await Ready(vm);
            vm.InspectorTagSearch = "人像";
            await Execute(vm.CreateInspectorTagCommand);
            vm.InspectorTagSearch = "旅行";
            await Execute(vm.CreateInspectorTagCommand);
            Assert.HasCount(2, vm.InspectorTagChips);
            Assert.HasCount(2, await repository.ListTagMembershipsAsync(assetId: vm.SelectedAsset!.AssetId));
            Assert.IsTrue(vm.AssetCards.Any(card => card.TagSummary.Contains("人像") && card.TagSummary.Contains("旅行")));
            Assert.AreEqual(1, vm.OrganizationTagGroups.SelectMany(group => group.Children).Single(tag => tag.Name == "人像").UsageCount);
            await Execute(vm.InspectorTagChips.Single(chip => chip.Name == "人像").RemoveCommand);
            Assert.HasCount(1, vm.InspectorTagChips);
            Assert.AreEqual("旅行", vm.InspectorTagChips[0].Name);
            vm.InspectorFolderTarget = vm.Folders.Single(folder => folder.Name == "海边");
            await Execute(vm.AddInspectorFolderCommand);
            Assert.HasCount(1, vm.InspectorFolderChips);
            Assert.HasCount(1, await repository.ListFolderMembershipsAsync(assetId: vm.SelectedAsset!.AssetId));
            await Execute(vm.InspectorFolderChips[0].RemoveCommand);
            Assert.IsEmpty(await repository.ListFolderMembershipsAsync(assetId: vm.SelectedAsset!.AssetId));
            vm.SearchText = "旅行";
            await Execute(vm.SubmitP3SearchCommand);
            Assert.HasCount(1, vm.AssetCards, "Text query must immediately see the persisted tag.");
            await Execute(vm.InspectorTagChips[0].RemoveCommand);
            Assert.IsEmpty(vm.AssetCards, "Removing the matching relationship must refresh the live query.");
            await Execute(vm.P2UndoCommand);
            Assert.HasCount(1, vm.AssetCards, "Relation removal must use the durable undo pipeline.");
        }
        await using var restored = ViewModel(root);
        await restored.InitializeAsync();
        restored.SyncSelection([restored.AssetCards.Single(card => card.TagSummary.Contains("旅行")).Asset]);
        await Ready(restored);
        Assert.AreEqual("旅行", restored.InspectorTagChips.Single().Name);
        Assert.IsEmpty(restored.InspectorFolderChips);
    });

    [TestMethod]
    public Task MixedSelectionAndStalePickerNeverWriteWrongAssets() => RunSta(async () =>
    {
        var root = await Fixture();
        await using var vm = ViewModel(root);
        await vm.InitializeAsync();
        var assets = vm.AssetCards.Select(card => card.Asset).ToArray();
        vm.SyncSelection([assets[0]]); await Ready(vm);
        vm.InspectorTagSearch = "共同标签"; await Execute(vm.CreateInspectorTagCommand);
        var staleRemove = vm.InspectorTagChips.Single().RemoveCommand;
        vm.SyncSelection([assets[1]]); await Ready(vm);
        await Execute(staleRemove);
        vm.SyncSelection(assets); await Ready(vm);
        var mixed = vm.InspectorSelectedTags.Single();
        Assert.IsNull(mixed.IsChecked);
        Assert.AreEqual(1, mixed.MemberCount);
        await Execute(mixed.ToggleCommand);
        Assert.IsTrue(vm.InspectorSelectedTags.Single().IsChecked);
        await Execute(vm.InspectorSelectedTags.Single().ToggleCommand);
        Assert.IsEmpty(vm.InspectorTagChips);
        vm.InspectorTagSearch = "共同";
        Assert.HasCount(1, vm.InspectorAvailableTags);
        vm.SyncSelection([]);
        Assert.IsFalse(vm.CanEditInspectorRelations);
        Assert.IsFalse(vm.CreateInspectorTagCommand.CanExecute(null));
    });

    [TestMethod]
    public Task FolderNameSearchKeepsAncestorsExpansionAndAssetQuery() => RunSta(async () =>
    {
        var root = await Fixture();
        await using var vm = ViewModel(root);
        await vm.InitializeAsync();
        var parent = vm.OrganizationFolders.Single(folder => folder.Name == "旅途");
        parent.IsExpanded = false;
        var visibleIds = vm.AssetCards.Select(card => card.Asset.AssetId).ToArray();
        vm.OrganizationFolderSearch = "海边";
        Assert.IsTrue(parent.IsVisible);
        Assert.IsTrue(parent.IsExpanded);
        Assert.IsTrue(parent.Children.Single(child => child.Name == "海边").IsVisible);
        Assert.IsFalse(parent.Children.Single(child => child.Name == "山林").IsVisible);
        Assert.IsFalse(vm.OrganizationFolders.Single(folder => folder.Name == "工作").IsVisible);
        CollectionAssert.AreEqual(visibleIds, vm.AssetCards.Select(card => card.Asset.AssetId).ToArray());
        Assert.AreEqual("", vm.SearchText);
        vm.OrganizationFolderSearch = "";
        Assert.IsFalse(parent.IsExpanded, "Search expansion is temporary.");
        Assert.IsTrue(parent.Children.All(child => child.IsVisible));
    });

    [TestMethod]
    public Task TextSuggestionDoesNotOpenAdvancedFilters() => RunSta(async () =>
    {
        var root = await Fixture();
        await using var vm = ViewModel(root);
        await vm.InitializeAsync();
        var suggestion = new AssetQuerySuggestion("tag", "人像", "id:" + Guid.NewGuid(), "标签");
        vm.ApplyP3SuggestionCommand.Execute(suggestion);
        await vm.ApplyP3SuggestionCommand.ExecutionTask;
        Assert.AreEqual("人像", vm.SearchText);
        Assert.IsFalse(vm.P3QueryPanelOpen);
        Assert.IsEmpty(vm.P3QueryRoot.Children);
        Assert.IsFalse(vm.IsTemporaryVisualMode);
        vm.SearchText = "评分";
        await Task.Delay(400);
        Assert.IsFalse(vm.P3QuerySuggestions.Any(item => item.Kind == "field"), "Ordinary search must not offer structured filter fields.");
    });

    [TestMethod]
    public Task SearchWidthAndFolderRowsRemainBoundedAcrossLogicalDpiSizes() => RunSta(async () =>
    {
        var root = await Fixture();
        await using var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(Path.Combine(root, "assets.db"), new TaskOperationBridge(), []);
        await page.InitializeForSessionAsync();
        foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
        foreach (var size in new[] { new Size(1180, 720), new Size(1600, 920), new Size(1920, 1080) })
        {
            var logical = new Size(size.Width / scale, size.Height / scale);
            page.Measure(logical); page.Arrange(new Rect(logical)); page.UpdateLayout();
            var search = (TextBox)page.FindName("AssetLibrarySearchBox");
            Assert.IsTrue(search.ActualWidth >= 200 && search.ActualWidth <= 320, $"{size}/{scale}: {search.ActualWidth}");
        }
        // Layout-only evidence; deliberately no screenshot and no runtime PASS claim.
    });

    private static AssetLibraryViewModel ViewModel(string root) => new(Path.Combine(root, "assets.db"), new TaskOperationBridge(),
        productDatabasePath: Path.Combine(root, "product.db"), onlineSelectionWorkspaceFile: Path.Combine(root, "online.json"), inspirationTrayDatabasePath: Path.Combine(root, "tray.db"));

    private static async Task<string> Fixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-RUX-A", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var product = new PixelTartDatabase(Path.Combine(root, "product.db"));
        Assert.IsTrue((await new DatabaseMigrator(product, new DatabaseBackupService(product, Path.Combine(root, "backups"))).MigrateAsync()).Success);
        await using var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "assets.db"));
        await repository.InitializeAsync();
        for (var i = 0; i < 2; i++)
        {
            var path = Path.Combine(root, $"photo-{i}.png");
            var pixels = Enumerable.Repeat((byte)(80 + i * 80), 48 * 32 * 3).ToArray();
            var bitmap = BitmapSource.Create(48, 32, 96, 96, PixelFormats.Rgb24, null, pixels, 48 * 3);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
            await repository.ImportAsync([new AssetImportRequest(path)]);
        }
        var parent = Guid.NewGuid();
        await repository.SaveFolderAsync(new(parent, null, "旅途"));
        await repository.SaveFolderAsync(new(Guid.NewGuid(), parent, "海边"));
        await repository.SaveFolderAsync(new(Guid.NewGuid(), parent, "山林"));
        await repository.SaveFolderAsync(new(Guid.NewGuid(), null, "工作"));
        return root;
    }

    private static async Task Ready(AssetLibraryViewModel vm)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!vm.CanEditInspectorRelations) await Task.Delay(10, timeout.Token);
    }
    private static async Task Execute(AsyncCommand command)
    {
        Assert.IsTrue(command.CanExecute(null)); command.Execute(null); await command.ExecutionTask;
    }
    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var task = action();
            _ = task.ContinueWith(_ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background), TaskScheduler.Default);
            Dispatcher.Run();
            try { task.GetAwaiter().GetResult(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
