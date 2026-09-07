using System.Diagnostics;
using System.IO;
using System.Reflection;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3ScopeBehaviorTests
{
    [TestMethod]
    public Task NormalScopeBindingPublishesOnceAndCancelsPendingTextDebounce() => InLibrary(async (vm, repository) =>
    {
        vm.SearchText = "asset";
        var previous = Generation(vm);
        vm.IsP3AllAssetsScope = true;
        Assert.AreEqual(previous + 1, Generation(vm), "Explicit scope navigation must start immediately.");
        await Settled(vm);
        await Task.Delay(400);
        Assert.AreEqual(previous + 1, Generation(vm), "No delayed text query may run after the scope navigation.");
        vm.IsP3AllAssetsScope = true;
        Assert.AreEqual(previous + 1, Generation(vm), "Same-value binding must not manufacture a query.");
        vm.IsP3CurrentScope = true;
        await Settled(vm);
        Assert.AreEqual(previous + 2, Generation(vm));
        Assert.AreEqual(AssetQueryScope.Current, vm.P3QueryScope);
    });

    [TestMethod]
    public Task RapidScopeChangesCancelOldQueriesBeforePublishingTheLatestResult() => InLibrary(async (vm, repository) =>
    {
        var proxy = DispatchProxy.Create<IAssetLibraryRepository, DelayedQueryRepository>();
        var control = (DelayedQueryRepository)(object)proxy;
        control.Inner = repository;
        typeof(AssetLibraryViewModel).GetField("_repository", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, proxy);
        var before = Generation(vm);
        vm.IsP3AllAssetsScope = true;
        await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.IsP3CurrentScope = true;
        await Settled(vm);
        Assert.IsTrue(control.Canceled);
        Assert.AreEqual(before + 2, Generation(vm));
        Assert.AreEqual(AssetQueryScope.Current, vm.P3QueryScope);
        Assert.AreEqual(601, vm.P2QueryTotalCount);
        Assert.IsFalse(vm.IsLoading);
    });

    [TestMethod]
    public Task ScopeNavigationPrunesGhostSelectionAndRefreshesSelectedMetadata() => InLibrary(async (vm, repository) =>
    {
        var first = vm.AssetCards.First().Asset;
        var second = vm.AssetCards[1].Asset;
        var tag = await repository.SaveTagAsync(new(Guid.NewGuid(), "选区范围"));
        await repository.AddTagsAsync([first.AssetId], [tag.TagId]);
        vm.RefreshCommand.Execute(null);
        await vm.RefreshCommand.ExecutionTask;
        vm.SyncSelection([first, second]);
        vm.SelectedTag = tag;
        await Settled(vm);
        CollectionAssert.AreEqual(new[] { first.AssetId }, vm.SelectedAssetIds.ToArray());
        var request = new AssetBatchMetadataRequest([first.AssetId], Rating: 5);
        var preview = await repository.PreviewBatchMetadataAsync(request);
        await repository.ApplyBatchMetadataAsync(request, preview);
        vm.RefreshCommand.Execute(null);
        await vm.RefreshCommand.ExecutionTask;
        Assert.AreEqual(5, vm.SelectedAssets.Single().Rating, "A cached selected record is not authoritative after a mutation.");
        vm.IsP3AllAssetsScope = true;
        await Settled(vm);
        Assert.AreEqual(601, vm.P2QueryTotalCount);
        vm.IsP3CurrentScope = true;
        await Settled(vm);
        Assert.AreEqual(1, vm.P2QueryTotalCount);
    });

    [TestMethod]
    public Task OffPageSelectionIsRetainedOnlyWhenItMatchesTheWholeQuery() => InLibrary(async (vm, repository) =>
    {
        var visible = vm.AssetCards.Select(card => card.Asset.AssetId).ToHashSet();
        var firstPage = await repository.QueryAsync(new(PageSize: 500));
        var secondPage = await repository.QueryAsync(new(PageSize: 500, Cursor: firstPage.NextCursor));
        var offPage = secondPage.Items.First(asset => !visible.Contains(asset.AssetId));
        vm.SyncSelection([offPage]);
        vm.RefreshCommand.Execute(null);
        await vm.RefreshCommand.ExecutionTask;
        CollectionAssert.AreEqual(new[] { offPage.AssetId }, vm.SelectedAssetIds.ToArray());
        var tag = await repository.SaveTagAsync(new(Guid.NewGuid(), "空标签范围"));
        vm.SelectedTag = tag;
        await Settled(vm);
        Assert.IsEmpty(vm.SelectedAssetIds);
        Assert.IsEmpty(vm.SelectedAssets);
    });

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(999)]
    public async Task ReopenRestoresValidScopeAndNormalizesInvalidScope(int raw)
    {
        var path = Path.Combine(Path.GetTempPath(), "PixelTart-P3Scope", Guid.NewGuid().ToString("N"), "library.db");
        await AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async () =>
        {
            var settings = new AssetLibraryWorkspaceSettings { QueryScope = (AssetQueryScope)raw };
            await using var vm = new AssetLibraryViewModel(path, new TaskOperationBridge(), workspaceSettings: settings);
            await vm.InitializeAsync();
            Assert.AreEqual(raw == 1 ? AssetQueryScope.AllAssets : AssetQueryScope.Current, vm.P3QueryScope);
            Assert.AreEqual(vm.P3QueryScope, settings.QueryScope);
        });
    }

    private static async Task InLibrary(Func<AssetLibraryViewModel, SqliteAssetLibraryRepository, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-P3Scope", Guid.NewGuid().ToString("N"));
        var database = Path.Combine(root, "library.db");
        await using var repository = new SqliteAssetLibraryRepository(database);
        await repository.InitializeAsync();
        await repository.ImportAsync(Enumerable.Range(0, 601).Select(index => new AssetImportRequest(Path.Combine(root, $"asset-{index:D4}.jpg"))));
        await AssetLibraryP3PerformanceDiagnosticsTests.RunSta(async () =>
        {
            await using var vm = new AssetLibraryViewModel(database, new TaskOperationBridge());
            await vm.InitializeAsync();
            await test(vm, repository);
            await Settled(vm);
        });
    }

    private static long Generation(AssetLibraryViewModel vm) => (long)typeof(AssetLibraryViewModel)
        .GetField("_queryGeneration", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;

    private static async Task Settled(AssetLibraryViewModel vm)
    {
        var clock = Stopwatch.StartNew();
        while (vm.IsLoading || vm.P3PendingOperationCount != 0)
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(20)) Assert.Fail("Scope or selection did not settle.");
            await Task.Delay(5);
        }
        Assert.IsFalse(vm.HasLoadError, vm.LoadErrorMessage);
    }

    public class DelayedQueryRepository : DispatchProxy
    {
        public IAssetLibraryRepository Inner { get; set; } = null!;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Canceled { get; private set; }
        private int _queries;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IAssetLibraryRepository.QueryAsync) && Interlocked.Increment(ref _queries) == 1)
                return Delayed((CancellationToken)args![1]!);
            try { return method.Invoke(Inner, args); }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private async Task<RAWSelectionAssistant.Core.Models.AssetLibraryPage> Delayed(CancellationToken token)
        {
            Entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { Canceled = true; throw; }
            throw new InvalidOperationException("An obsolete query must be canceled, never released as success.");
        }
    }
}
