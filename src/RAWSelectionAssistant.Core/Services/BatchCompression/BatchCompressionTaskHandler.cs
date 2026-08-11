using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Core.Services.BatchCompression;

public sealed class BatchCompressionTaskHandler(
    IBatchCompressionRequestStore requests,
    IBatchCompressionService compression) : ITaskHandler, ITaskTerminalStateObserver
{
    public string TaskType => BatchCompressionDefaults.TaskType;

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
    {
        if (!requests.TryGet(context.Definition.Id, out var checkpoint))
            return new(TaskLifecycleState.Failed, TaskResultSummary.Empty, ErrorCodeCatalog.CheckpointInvalid,
                "The protected batch compression checkpoint is unavailable; no source path was written to diagnostics.");

        var progress = new AwaitableProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>(value =>
            context.ReportProgressAsync(value.Progress, "批量压缩", null, value.Summary, CancellationToken.None));
        TaskExecutionResult executionResult;
        var completedThisRun = new List<BatchCompressionItemResult>();
        async Task PersistItemCheckpointAsync(BatchCompressionItemResult item)
        {
            completedThisRun.RemoveAll(candidate => candidate.Sequence == item.Sequence);
            completedThisRun.Add(item);
            var itemAggregate = Merge(checkpoint, completedThisRun);
            var durableCheckpoint = CreateCheckpoint(checkpoint.OriginalRequest, itemAggregate);
            requests.Update(context.Definition.Id, durableCheckpoint);
            await context.SafeBoundaryAsync("BatchCompression.ItemCommitted", durableCheckpoint.StableResults.Count,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        try
        {
            BatchCompressionResult current;
            if (checkpoint.PendingSourceFiles.Count == 0)
            {
                var restoredSummary = Summarize(checkpoint.OriginalRequest.SourceFiles.Count, checkpoint.StableResults);
                current = new(context.Definition.Id, ResolveState(checkpoint.OriginalRequest.SourceFiles.Count,
                    checkpoint.StableResults), restoredSummary, checkpoint.StableResults);
            }
            else
            {
                var originalSources = checkpoint.OriginalRequest.SourceFiles.ToList();
                var sourceSequences = checkpoint.PendingSourceFiles.Select(source => originalSources.FindIndex(candidate =>
                    string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))).ToArray();
                var pendingRequest = checkpoint.OriginalRequest with
                {
                    SourceFiles = checkpoint.PendingSourceFiles,
                    SourceSequences = sourceSequences
                };
                current = await compression.CompressAsync(context.Definition.Id, pendingRequest, progress,
                        PersistItemCheckpointAsync, cancellationToken)
                    .ConfigureAwait(false);
            }

            var aggregate = Merge(checkpoint, current.Items);
            var durableCheckpoint = CreateCheckpoint(checkpoint.OriginalRequest, aggregate);
            var summary = Summarize(checkpoint.OriginalRequest.SourceFiles.Count, aggregate);
            var state = ResolveState(checkpoint.OriginalRequest.SourceFiles.Count, aggregate);
            requests.Update(context.Definition.Id, durableCheckpoint);
            executionResult = new(state, summary,
                aggregate.FirstOrDefault(item => item.ErrorCode is not null)?.ErrorCode,
                state == TaskLifecycleState.Completed
                    ? null
                    : "One or more compression items require attention; completed outputs are not repeated on retry.");
        }
        finally
        {
            await progress.DrainAsync().ConfigureAwait(false);
        }

        return executionResult;
    }

    public Task OnTerminalStatePersistedAsync(Guid taskId, TaskLifecycleState terminalState,
        CancellationToken cancellationToken = default)
    {
        if (terminalState == TaskLifecycleState.Completed) requests.Remove(taskId);
        return Task.CompletedTask;
    }

    private static BatchCompressionRecoveryCheckpoint CreateCheckpoint(BatchCompressionRequest originalRequest,
        IReadOnlyList<BatchCompressionItemResult> aggregate)
    {
        var pendingSources = aggregate
            .Where(item => string.IsNullOrWhiteSpace(item.DestinationPath) && item.State != BatchCompressionItemState.Completed)
            .Select(item => item.SourcePath)
            .ToArray();
        var stable = aggregate.Where(item => !string.IsNullOrWhiteSpace(item.DestinationPath)).ToArray();
        return new(originalRequest, pendingSources, stable);
    }

    private static IReadOnlyList<BatchCompressionItemResult> Merge(
        BatchCompressionRecoveryCheckpoint checkpoint,
        IReadOnlyList<BatchCompressionItemResult> current)
    {
        var bySource = checkpoint.StableResults.Concat(current)
            .GroupBy(item => Path.GetFullPath(item.SourcePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        return checkpoint.OriginalRequest.SourceFiles.Select((source, sequence) =>
        {
            var fullPath = Path.GetFullPath(source);
            return bySource.TryGetValue(fullPath, out var item)
                ? item with { Sequence = sequence, SourcePath = source }
                : new BatchCompressionItemResult(sequence, BatchCompressionItemState.Cancelled, source, null, 0,
                    ErrorCodeCatalog.CancelledByUser, "Compression pending.");
        }).ToArray();
    }

    private static TaskResultSummary Summarize(int total, IReadOnlyCollection<BatchCompressionItemResult> items) => new(total,
        items.Count(item => item.State == BatchCompressionItemState.Completed),
        items.Count(item => item.State == BatchCompressionItemState.Failed), 0,
        items.Count(item => item.State == BatchCompressionItemState.Cancelled),
        items.Count(item => item.State is BatchCompressionItemState.NeedsAttention or BatchCompressionItemState.PartiallyCompleted),
        0, items.Sum(item => item.BytesWritten));

    private static TaskLifecycleState ResolveState(int total, IReadOnlyCollection<BatchCompressionItemResult> items)
    {
        var summary = Summarize(total, items);
        if (summary.Succeeded == total) return TaskLifecycleState.Completed;
        if (items.Any(item => !string.IsNullOrWhiteSpace(item.DestinationPath))) return TaskLifecycleState.PartiallyCompleted;
        if (summary.WaitingForAttention > 0) return TaskLifecycleState.NeedsAttention;
        if (summary.Cancelled > 0 && summary.Failed == 0) return TaskLifecycleState.Cancelled;
        return TaskLifecycleState.Failed;
    }
}

public sealed class BatchCompressionTaskCoordinator(
    ITaskEngine taskEngine,
    IBatchCompressionRequestStore requests) : IBatchCompressionTaskCoordinator
{
    public async Task<Guid> StartAsync(BatchCompressionRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        var taskId = Guid.NewGuid();
        requests.Register(taskId, request);
        try
        {
            await taskEngine.EnqueueAsync(new(taskId, request.ProjectId, BatchCompressionDefaults.TaskType, "批量压缩",
                $"SourceCount={request.SourceFiles.Count};PathsRedacted;Quality={request.Options.JpegQuality};LongestEdge={request.Options.LongestEdge}",
                null, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
            return taskId;
        }
        catch
        {
            requests.Remove(taskId);
            throw;
        }
    }

    public Task CancelAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        taskEngine.CancelAsync(taskId, cancellationToken);

    public Task WaitForCompletionAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        taskEngine.WaitForCompletionAsync(taskId, cancellationToken);
}
