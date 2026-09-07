using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
        });
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
