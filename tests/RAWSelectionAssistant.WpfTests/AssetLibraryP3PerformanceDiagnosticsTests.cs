using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3PerformanceDiagnosticsTests
{
    [TestMethod]
    [TestCategory("P3Diagnostic")]
    public async Task ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture()
    {
        var fixture = Environment.GetEnvironmentVariable("PIXEL_TART_P3_DIAGNOSTIC_FIXTURE");
        var output = Environment.GetEnvironmentVariable("PIXEL_TART_P3_DIAGNOSTIC_OUTPUT");
        if (string.IsNullOrWhiteSpace(fixture) || string.IsNullOrWhiteSpace(output))
            Assert.Inconclusive("Opt-in diagnostic: provide a new synthetic fixture and new output directory.");
        Assert.IsTrue(Path.IsPathFullyQualified(fixture));
        Assert.IsTrue(Path.IsPathFullyQualified(output));
        Assert.IsFalse(Directory.Exists(output), "Never overwrite an earlier diagnostic.");
        Directory.CreateDirectory(output);
        await RunSta(async () =>
        {
            for (var sample = 0; sample < 3; sample++)
            {
                var samples = new ConcurrentQueue<object>();
                var database = Path.Combine(output, $"sample-{sample}.db");
                File.Copy(fixture, database, overwrite: false);
                var tag100 = new AssetTag(Guid.NewGuid(), $"诊断100-{sample}");
                var tag500 = new AssetTag(Guid.NewGuid(), $"诊断500-{sample}");
                await using var repository = new SqliteAssetLibraryRepository(database);
                await repository.InitializeAsync();
                Assert.AreEqual(10128, (await repository.QueryAsync(new(IncludeArchived: true, PageSize: 1))).TotalCount);
                await repository.SaveTagAsync(tag100);
                await repository.SaveTagAsync(tag500);
                var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(database, new TaskOperationBridge(), []);
                try
                {
                    await page.ViewModel.InitializeAsync();
                    page.Measure(new Size(1600, 1000));
                    page.Arrange(new Rect(0, 0, 1600, 1000));
                    page.UpdateLayout();
                    // Finish the initial page's queued binding/render work, not a
                    // warm-up batch. First-screen time is a separate contract metric.
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    var grid = (ListBox)page.FindName("AssetGrid");
                    foreach (var (size, tag) in new[] { (100, tag100), (500, tag500) })
                    {
                        var stage = "selection";
                        using var capture = AssetLibraryOperationTiming.Capture(value => samples.Enqueue(new { size, stage, timing = value }));
                        var gaps = new List<double>();
                        var gapStages = new List<string>();
                        var lastTick = Stopwatch.GetTimestamp();
                        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
                        timer.Tick += (_, _) => { gaps.Add(Stopwatch.GetElapsedTime(lastTick).TotalMilliseconds); gapStages.Add(stage); lastTick = Stopwatch.GetTimestamp(); };
                        timer.Start();
                        var gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                        try
                        {
                            using (AssetLibraryOperationTiming.Measure("public.selection"))
                            {
                                ((AssetLibrarySelectionListBox)grid).ReplaceSelection(grid.Items.Cast<object>().Take(size).ToArray());
                                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                                await WaitUntil(() => page.ViewModel.P3PendingOperationCount == 0);
                                Assert.AreEqual(size, page.ViewModel.SelectionCount);
                            }
                            page.ViewModel.P3BatchTag = page.ViewModel.Tags.Single(item => item.TagId == tag.TagId);
                            page.ViewModel.P3BatchTagAction = "添加";
                            stage = "preview";
                            using (AssetLibraryOperationTiming.Measure("public.preview"))
                            {
                                Assert.IsTrue(page.ViewModel.PreviewP3BatchMetadataCommand.CanExecute(null));
                                page.ViewModel.PreviewP3BatchMetadataCommand.Execute(null);
                                await page.ViewModel.PreviewP3BatchMetadataCommand.ExecutionTask;
                                await WaitUntil(() => page.ViewModel.P3PendingOperationCount == 0);
                                Assert.IsTrue(page.ViewModel.P3BatchPreviewReady, page.ViewModel.P3BatchPreviewSummary);
                            }
                            stage = "apply";
                            using (AssetLibraryOperationTiming.Measure("public.apply-complete"))
                            {
                                Assert.IsTrue(page.ViewModel.ApplyP3BatchMetadataCommand.CanExecute(null));
                                page.ViewModel.ApplyP3BatchMetadataCommand.Execute(null);
                                await page.ViewModel.ApplyP3BatchMetadataCommand.ExecutionTask;
                                await WaitUntil(() => page.ViewModel.P3PendingOperationCount == 0);
                                StringAssert.Contains(page.ViewModel.P3BatchPreviewSummary, "已安全更新");
                                Assert.IsFalse(page.ViewModel.IsLoading || page.ViewModel.HasLoadError || page.ViewModel.IsOrganizationLoading);
                            }
                            Assert.HasCount(size, await repository.ListTagMembershipsAsync(tagId: tag.TagId));
                            stage = "undo";
                            using (AssetLibraryOperationTiming.Measure("public.undo-refresh"))
                            {
                                Assert.IsTrue(page.ViewModel.P2UndoCommand.CanExecute(null));
                                page.ViewModel.P2UndoCommand.Execute(null);
                                await page.ViewModel.P2UndoCommand.ExecutionTask;
                                await WaitUntil(() => page.ViewModel.P2RedoCommand.CanExecute(null) && !page.ViewModel.IsLoading && !page.ViewModel.IsOrganizationLoading);
                                Assert.IsEmpty(await repository.ListTagMembershipsAsync(tagId: tag.TagId));
                            }
                            stage = "redo";
                            using (AssetLibraryOperationTiming.Measure("public.redo-refresh"))
                            {
                                Assert.IsTrue(page.ViewModel.P2RedoCommand.CanExecute(null));
                                page.ViewModel.P2RedoCommand.Execute(null);
                                await page.ViewModel.P2RedoCommand.ExecutionTask;
                                await WaitUntil(() => page.ViewModel.P2UndoCommand.CanExecute(null) && !page.ViewModel.IsLoading && !page.ViewModel.IsOrganizationLoading);
                                Assert.HasCount(size, await repository.ListTagMembershipsAsync(tagId: tag.TagId));
                            }
                            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                            stage = "scope-current";
                            page.ViewModel.IsP3CurrentScope = true;
                            await WaitUntil(() => !page.ViewModel.IsLoading && page.ViewModel.P3PendingOperationCount == 0);
                            stage = "scope-all";
                            using (AssetLibraryOperationTiming.Measure("public.scope-current-to-all"))
                            {
                                page.ViewModel.IsP3AllAssetsScope = true;
                                await WaitUntil(() => !page.ViewModel.IsLoading && page.ViewModel.P3PendingOperationCount == 0);
                                Assert.AreEqual(10000, page.ViewModel.P2QueryTotalCount);
                            }
                        }
                        finally
                        {
                            gaps.Add(Stopwatch.GetElapsedTime(lastTick).TotalMilliseconds);
                            gapStages.Add(stage);
                            timer.Stop();
                            samples.Enqueue(new { size, stage = "dispatcher", max_gap_ms = gaps.Max(), gaps, gapStages,
                                gc_counts = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gcBefore[i]).ToArray() });
                        }
                    }
                }
                finally
                {
                    await File.WriteAllTextAsync(Path.Combine(output, $"sample-{sample}.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
                    await page.DisposeAsync();
                }
            }

            var scaleRoot = Environment.GetEnvironmentVariable("PIXEL_TART_RC12_SCALE_FIXTURE_ROOT");
            if (!string.IsNullOrWhiteSpace(scaleRoot))
                await RunScaleMatrixAsync(scaleRoot, Path.Combine(output, "scale-matrix"));
        });
    }

    private static async Task RunScaleMatrixAsync(string fixtureRoot, string outputRoot)
    {
        Assert.IsTrue(Path.IsPathFullyQualified(fixtureRoot));
        Assert.IsFalse(Directory.Exists(outputRoot));
        Directory.CreateDirectory(outputRoot);
        foreach (var size in new[] { 10000, 50000, 100000 })
        {
            var fixture = Path.Combine(fixtureRoot, $"asset-library-{size}.db");
            Assert.IsTrue(File.Exists(fixture), fixture);
            for (var sample = 0; sample < 3; sample++)
            {
                var database = Path.Combine(outputRoot, $"scale-{size}-{sample}.db");
                File.Copy(fixture, database, overwrite: false);
                await SeedVisibleThumbnailsAsync(database, outputRoot, size, sample);
                var process = Process.GetCurrentProcess();
                process.Refresh();
                var workingSetBefore = process.WorkingSet64;
                var metrics = new Dictionary<string, object?> { ["size"] = size, ["sample"] = sample };
                await using var repository = new SqliteAssetLibraryRepository(database);
                await repository.InitializeAsync();
                var candidates = (await repository.QueryAsync(new(PageSize: 50))).Items.Select(item => item.AssetId).ToArray();
                var projectId = Guid.NewGuid();
                var bookingId = Guid.NewGuid();
                foreach (var assetId in candidates)
                {
                    await repository.SaveProjectAssetLinkAsync(new(projectId, assetId, "Asset", DateTimeOffset.UtcNow));
                    await repository.SaveBookingAssetLinkAsync(new(bookingId, assetId, DateTimeOffset.UtcNow));
                }

                var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(database, new TaskOperationBridge(), []);
                try
                {
                    var open = Stopwatch.StartNew();
                    await page.ViewModel.InitializeAsync();
                    page.Measure(new Size(1600, 1000));
                    page.Arrange(new Rect(0, 0, 1600, 1000));
                    page.UpdateLayout();
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    metrics["library_open_ms"] = open.Elapsed.TotalMilliseconds;
                    metrics["total_count"] = page.ViewModel.P2QueryTotalCount;
                    metrics["loaded_items"] = page.ViewModel.AssetCards.Count;
                    Assert.AreEqual(size, page.ViewModel.P2QueryTotalCount);
                    Assert.IsLessThanOrEqualTo(500, page.ViewModel.AssetCards.Count);

                    var queueMax = AsyncThumbnail.PendingRequestCount;
                    var firstVisible = Stopwatch.StartNew();
                    await WaitUntil(() =>
                    {
                        queueMax = Math.Max(queueMax, AsyncThumbnail.PendingRequestCount);
                        return FindVisualChildren<Image>(page).Any(image => image.Source is not null);
                    });
                    metrics["first_visible_thumbnail_ms"] = firstVisible.Elapsed.TotalMilliseconds;
                    await WaitUntil(() => AsyncThumbnail.PendingRequestCount == 0);
                    metrics["thumbnail_queue_drained_ms"] = firstVisible.Elapsed.TotalMilliseconds;
                    metrics["thumbnail_queue_max"] = queueMax;

                    var grid = (ListBox)page.FindName("AssetGrid");
                    var panel = FindVisualChild<VirtualizingAssetPanel>(grid);
                    Assert.IsNotNull(panel);
                    metrics["realized_items_initial"] = panel.RealizedItemCount;
                    Assert.IsLessThan(page.ViewModel.AssetCards.Count, panel.RealizedItemCount);
                    metrics["scroll_ms"] = await MeasureAsync(async () =>
                    {
                        grid.ScrollIntoView(grid.Items[^1]);
                        page.UpdateLayout();
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    });
                    metrics["realized_items_after_scroll"] = panel.RealizedItemCount;

                    page.ViewModel.SearchText = $"RC12_SCALE_{size}_{size / 2:000000}";
                    metrics["search_ms"] = await MeasureAsync(async () =>
                    {
                        await Task.Delay(350);
                        await WaitUntil(() => !page.ViewModel.IsLoading && page.ViewModel.P3PendingOperationCount == 0);
                    });
                    Assert.AreEqual(1, page.ViewModel.P2QueryTotalCount);
                    page.ViewModel.ClearFilters();
                    await WaitUntil(() => !page.ViewModel.IsLoading && page.ViewModel.P2QueryTotalCount == size);

                    page.ViewModel.MinimumRatingFilterText = "4";
                    metrics["rating_filter_ms"] = await MeasureAsync(async () =>
                    {
                        page.ViewModel.RefreshCommand.Execute(null);
                        await page.ViewModel.RefreshCommand.ExecutionTask;
                    });
                    Assert.IsGreaterThan(0, page.ViewModel.P2QueryTotalCount);
                    page.ViewModel.MinimumRatingFilterText = string.Empty;
                    page.ViewModel.ClearFilters();
                    await WaitUntil(() => !page.ViewModel.IsLoading && page.ViewModel.P2QueryTotalCount == size);

                    metrics["project_filter_ms"] = await MeasureAsync(() => page.ViewModel.ApplyProjectFilterAsync(projectId));
                    Assert.AreEqual(candidates.Length, page.ViewModel.P2QueryTotalCount);
                    metrics["booking_filter_ms"] = await MeasureAsync(() => page.ViewModel.ApplyBookingFilterAsync(bookingId));
                    Assert.AreEqual(candidates.Length, page.ViewModel.P2QueryTotalCount);

                    var cacheDirectory = Path.Combine(outputRoot, $"preview-cache-{size}-{sample}");
                    var source = Path.Combine(outputRoot, $"preview-source-{size}-{sample}.png");
                    WriteDiagnosticPng(source);
                    var request = new AssetThumbnailRequest(source, 160, AssetThumbnailState.Available, Guid.NewGuid(), new string('A', 64));
                    var provider = new WpfAssetThumbnailProvider(cacheDirectory);
                    var firstThumbnail = await provider.GetAsync(request);
                    Assert.IsTrue(firstThumbnail.IsAvailable);
                    File.Delete(source);
                    var restartedProvider = new WpfAssetThumbnailProvider(cacheDirectory);
                    metrics["restart_cached_preview_ms"] = await MeasureAsync(async () =>
                    {
                        var cached = await restartedProvider.GetAsync(request with { KnownState = AssetThumbnailState.Offline });
                        Assert.IsTrue(cached.IsAvailable);
                    });
                }
                finally { await page.DisposeAsync(); }
                process.Refresh();
                metrics["working_set_before_bytes"] = workingSetBefore;
                metrics["working_set_after_bytes"] = process.WorkingSet64;
                metrics["working_set_delta_bytes"] = process.WorkingSet64 - workingSetBefore;
                await File.WriteAllTextAsync(
                    Path.Combine(outputRoot, $"scale-{size}-{sample}.json"),
                    JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }

    private static async Task<double> MeasureAsync(Func<Task> action)
    {
        var clock = Stopwatch.StartNew();
        await action();
        return clock.Elapsed.TotalMilliseconds;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject grid) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(grid); index++)
        {
            var child = VisualTreeHelper.GetChild(grid, index);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private static async Task SeedVisibleThumbnailsAsync(string database, string outputRoot, int size, int sample)
    {
        var sources = new List<string>();
        for (var index = 0; index < 20; index++)
        {
            var source = Path.Combine(outputRoot, $"visible-{size}-{sample}-{index}.png");
            WriteDiagnosticPng(source);
            sources.Add(source);
        }
        await using var connection = new SqliteConnection($"Data Source={database}");
        await connection.OpenAsync();
        var ids = new List<string>();
        await using (var read = connection.CreateCommand())
        {
            read.CommandText = "SELECT AssetId FROM AssetItems ORDER BY AddedAt DESC,AssetId LIMIT 20;";
            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync()) ids.Add(reader.GetString(0));
        }
        Assert.HasCount(20, ids);
        for (var index = 0; index < ids.Count; index++)
        {
            await using var update = connection.CreateCommand();
            update.CommandText = "UPDATE AssetItems SET SourcePath=$path,NormalizedSourcePath=$normalized,DisplayName=$name,Extension='.png',MediaType='Image',IsMissing=0 WHERE AssetId=$id;";
            update.Parameters.AddWithValue("$path", sources[index]);
            update.Parameters.AddWithValue("$normalized", sources[index].ToUpperInvariant());
            update.Parameters.AddWithValue("$name", $"VISIBLE_{index:00}.png");
            update.Parameters.AddWithValue("$id", ids[index]);
            Assert.AreEqual(1, await update.ExecuteNonQueryAsync());
        }
    }

    private static void WriteDiagnosticPng(string path)
    {
        var pixels = Enumerable.Repeat<byte>(96, 64 * 48 * 4).ToArray();
        var bitmap = BitmapSource.Create(64, 48, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(60)) Assert.Fail("Public command timed out; evidence retained.");
            await Task.Delay(10);
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    internal static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var operation = action();
            _ = operation.ContinueWith(_ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background), TaskScheduler.Default);
            Dispatcher.Run();
            try { operation.GetAwaiter().GetResult(); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
