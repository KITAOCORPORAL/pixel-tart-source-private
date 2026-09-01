using System.IO;
using System.Threading;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryLoadMoreHardeningTests
{
    [TestMethod]
    public void LoadMoreDeclaresCancellationGenerationAndVisibleStateContract()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.cs"));
        ContainsAll(source,
            "_loadMoreCancellation", "_loadMoreGeneration", "CancelLoadMoreRequest", "TryBeginLoadMore",
            "EnsureCurrentLoadMore", "cancellation.Token", "IsLoadingMore", "LoadMoreErrorMessage",
            "HasLoadMoreError", "HasMore", "CanLoadMore", "IsLoadMoreVisible", "LoadMoreStatus", "SetNextCursor");
        StringAssert.Contains(source, "分页游标未前进");
        StringAssert.Contains(source, "queryGeneration == Volatile.Read(ref _queryGeneration)");
    }

    [TestMethod]
    public Task LoadMoreAppendsOnlyTheNextCursorPageAndExposesRetryAffordance() => RunSta(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-LoadMore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "asset-library.db");
        try
        {
            var repository = new SqliteAssetLibraryRepository(databasePath);
            repository.InitializeAsync().GetAwaiter().GetResult();
            var requests = Enumerable.Range(0, 501)
                .Select(index => new AssetImportRequest(Path.Combine(root, $"missing-{index:000}.jpg")))
                .ToArray();
            var imported = repository.ImportAsync(requests).GetAwaiter().GetResult();
            Assert.AreEqual(501, imported.ImportedCount);
            repository.DisposeAsync().AsTask().GetAwaiter().GetResult();

            var viewModel = new AssetLibraryViewModel(databasePath, new TaskOperationBridge());
            viewModel.InitializeAsync().GetAwaiter().GetResult();
            Assert.IsTrue(viewModel.IsReady);
            Assert.HasCount(500, viewModel.AssetCards);
            Assert.IsTrue(viewModel.HasMore);
            Assert.IsTrue(viewModel.CanLoadMore);
            Assert.IsTrue(viewModel.IsLoadMoreVisible);

            var dispatcher = Dispatcher.CurrentDispatcher;
            var previousContext = SynchronizationContext.Current;
            var frame = new DispatcherFrame();
            var timedOut = false;
            var sawLoading = false;
            var timeout = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Send, (_, _) =>
            {
                timedOut = true;
                frame.Continue = false;
            }, dispatcher);
            EventHandler? changed = null;
            changed = (_, _) =>
            {
                if (viewModel.IsLoadingMore) sawLoading = true;
                else if (sawLoading) frame.Continue = false;
            };
            viewModel.LoadMoreCommand.CanExecuteChanged += changed;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                timeout.Start();
                viewModel.LoadMoreCommand.Execute(null);
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timeout.Stop();
                viewModel.LoadMoreCommand.CanExecuteChanged -= changed;
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }

            Assert.IsFalse(timedOut, "LoadMoreCommand did not finish within ten seconds.");
            Assert.IsTrue(sawLoading, "LoadMoreCommand did not expose its loading state.");
            Assert.HasCount(501, viewModel.AssetCards);
            Assert.AreEqual("已加载 501 个素材", viewModel.Status);
            Assert.IsFalse(viewModel.HasMore);
            Assert.IsFalse(viewModel.IsLoadMoreVisible);
            Assert.IsFalse(viewModel.HasLoadMoreError);
            Assert.AreEqual("已加载全部素材", viewModel.LoadMoreStatus);
            viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    });

    private static void ContainsAll(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }

    private static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
