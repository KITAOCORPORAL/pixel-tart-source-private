namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

public enum VisualAnalysisPriority
{
    Interactive = 0,
    Visible = 1,
    Background = 2
}

public enum VisualAnalysisBatchItemState
{
    Succeeded,
    Failed,
    Cancelled
}

public sealed record VisualAnalysisBatchItem(
    Guid AssetId,
    string? SourceContentHash,
    VisualAnalysisPriority Priority,
    Func<CancellationToken, Task<AssetVisualAnalysisRequest>> CreateRequestAsync);

public sealed record VisualAnalysisBatchItemResult(
    Guid AssetId,
    VisualAnalysisBatchItemState State,
    string? FailureReason = null,
    bool CacheHit = false);

public sealed record VisualAnalysisBatchProgress(
    int Total,
    int Completed,
    int Succeeded,
    int Failed,
    int Cancelled,
    Guid? CurrentAssetId);

public sealed record VisualAnalysisBatchRunResult(
    IReadOnlyList<VisualAnalysisBatchItemResult> Items,
    bool Cancelled)
{
    public int Succeeded => Items.Count(item => item.State == VisualAnalysisBatchItemState.Succeeded);
    public int Failed => Items.Count(item => item.State == VisualAnalysisBatchItemState.Failed);
    public int CancelledCount => Items.Count(item => item.State == VisualAnalysisBatchItemState.Cancelled);
}

/// <summary>
/// Bounded, priority-aware local analysis.  Only worker tasks are materialized;
/// the queue may contain a large metadata selection without Task.WhenAll fan-out.
/// Interactive analysis deliberately bypasses the batch worker slots.
/// </summary>
public sealed class AssetVisualAnalysisBatchProcessor(
    AssetVisualAnalysisService analysisService,
    IAssetVisualFeatureStore featureStore,
    int maxConcurrency = 2)
{
    private readonly int _maxConcurrency = Math.Clamp(maxConcurrency, 1, 8);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, CancellationTokenSource> _backgroundByAsset = new();

    public async Task<AssetVisualAnalysisResult> AnalyzeInteractiveAsync(
        Guid assetId,
        Func<CancellationToken, Task<AssetVisualAnalysisRequest>> createRequestAsync,
        CancellationToken cancellationToken = default)
    {
        if (_backgroundByAsset.TryGetValue(assetId, out var background)) background.Cancel();
        var request = await createRequestAsync(cancellationToken).ConfigureAwait(false);
        if (_backgroundByAsset.TryGetValue(request.AssetId, out background)) background.Cancel();
        return await analysisService.AnalyzeAsync(Canonicalize(request), cancellationToken).ConfigureAwait(false);
    }

    public async Task<VisualAnalysisBatchRunResult> ProcessAsync(
        IEnumerable<VisualAnalysisBatchItem> source,
        IProgress<VisualAnalysisBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var items = source.ToArray();
        var queue = new PriorityQueue<VisualAnalysisBatchItem, (int Priority, long Sequence)>();
        for (var index = 0; index < items.Length; index++) queue.Enqueue(items[index], ((int)items[index].Priority, index));
        var gate = new object();
        var results = new List<(int Sequence, VisualAnalysisBatchItemResult Result)>(items.Length);
        var sequences = items.Select((item, index) => (item.AssetId, index)).GroupBy(entry => entry.AssetId).ToDictionary(group => group.Key, group => new Queue<int>(group.Select(entry => entry.index)));
        var succeeded = 0; var failed = 0; var cancelled = 0;

        void AddResult(VisualAnalysisBatchItemResult result)
        {
            lock (gate)
            {
                var sequence = sequences[result.AssetId].Dequeue(); results.Add((sequence, result));
                if (result.State == VisualAnalysisBatchItemState.Succeeded) succeeded++;
                else if (result.State == VisualAnalysisBatchItemState.Failed) failed++;
                else cancelled++;
                progress?.Report(new(items.Length, results.Count, succeeded, failed, cancelled, result.AssetId));
            }
        }

        async Task WorkerAsync()
        {
            while (true)
            {
                VisualAnalysisBatchItem item;
                lock (gate)
                {
                    if (queue.Count == 0 || cancellationToken.IsCancellationRequested) return;
                    item = queue.Dequeue();
                }
                AssetVisualAnalysisRequest? request = null;
                using var itemCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _backgroundByAsset.AddOrUpdate(item.AssetId, itemCancellation, (_, existing) => { existing.Cancel(); return itemCancellation; });
                try
                {
                    request = Canonicalize(await item.CreateRequestAsync(itemCancellation.Token).ConfigureAwait(false));
                    var result = await analysisService.AnalyzeAsync(request, itemCancellation.Token).ConfigureAwait(false);
                    AddResult(new(item.AssetId, VisualAnalysisBatchItemState.Succeeded, CacheHit: result.CacheHit));
                }
                catch (OperationCanceledException) when (itemCancellation.IsCancellationRequested)
                {
                    AddResult(new(item.AssetId, VisualAnalysisBatchItemState.Cancelled));
                    if (cancellationToken.IsCancellationRequested) return;
                }
                catch (Exception exception)
                {
                    var reason = $"{exception.GetType().Name}: {exception.Message}";
                    try { await featureStore.RecordFailureAsync(item.AssetId, request?.SourceContentHash ?? item.SourceContentHash, reason, CancellationToken.None, request?.PreviousSourceContentHash ?? item.SourceContentHash).ConfigureAwait(false); }
                    catch { /* The original item failure remains isolated from the rest of the queue. */ }
                    AddResult(new(item.AssetId, VisualAnalysisBatchItemState.Failed, reason));
                }
                finally { _backgroundByAsset.TryRemove(new(item.AssetId, itemCancellation)); }
            }
        }

        var workers = Enumerable.Range(0, Math.Min(_maxConcurrency, Math.Max(1, items.Length))).Select(_ => WorkerAsync()).ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            lock (gate)
            {
                while (queue.Count > 0)
                {
                    var pending = queue.Dequeue();
                    var result = new VisualAnalysisBatchItemResult(pending.AssetId, VisualAnalysisBatchItemState.Cancelled); results.Add((sequences[pending.AssetId].Dequeue(), result)); cancelled++;
                }
                progress?.Report(new(items.Length, results.Count, succeeded, failed, cancelled, null));
            }
        }
        return new(results.OrderBy(entry => entry.Sequence).Select(entry => entry.Result).ToArray(), cancellationToken.IsCancellationRequested);
    }

    private static AssetVisualAnalysisRequest Canonicalize(AssetVisualAnalysisRequest request) => request with
    {
        PaletteSize = AssetVisualFeatureContract.PaletteSize,
        PaletteSort = AssetVisualFeatureContract.PaletteSort
    };
}
