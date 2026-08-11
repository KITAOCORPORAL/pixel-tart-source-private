using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

public sealed class RawToJpegTaskHandler(IRawToJpegRequestStore requests, IRawToJpegSafeConversionService conversion) : ITaskHandler
{
    public string TaskType => RawToJpegDefaults.TaskType;

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
    {
        if (!requests.TryGet(context.Definition.Id, out var checkpoint))
            return new(TaskLifecycleState.Failed, TaskResultSummary.Empty, ErrorCodeCatalog.CheckpointInvalid,
                "The protected RAW conversion checkpoint is unavailable; no source path was written to diagnostics.");

        var progress = new AwaitableProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>(value =>
            context.ReportProgressAsync(value.Progress, "RAW 转 JPG", null, value.Summary, CancellationToken.None));
        TaskExecutionResult executionResult;
        var removeCheckpoint = false;
        try
        {
            RawToJpegBatchResult current;
            if (checkpoint.PendingSourceFiles.Count == 0)
            {
                current = new(context.Definition.Id, ResolveState(checkpoint.OriginalRequest.SourceFiles.Count, checkpoint.StableResults),
                    Summarize(checkpoint.OriginalRequest.SourceFiles.Count, checkpoint.StableResults), checkpoint.StableResults);
            }
            else
            {
                var sourceSequences = checkpoint.PendingSourceFiles.Select(source =>
                    checkpoint.OriginalRequest.SourceFiles.ToList().FindIndex(candidate =>
                        string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))).ToArray();
                var pendingRequest = checkpoint.OriginalRequest with
                {
                    SourceFiles = checkpoint.PendingSourceFiles,
                    SourceSequences = sourceSequences
                };
                current = await conversion.ConvertAsync(context.Definition.Id, pendingRequest, progress, cancellationToken).ConfigureAwait(false);
            }

            var aggregate = Merge(checkpoint, current.Items);
            var pendingSources = aggregate
                .Where(item => string.IsNullOrWhiteSpace(item.DestinationPath) && item.State != RawToJpegItemState.Completed)
                .Select(item => item.SourcePath)
                .ToArray();
            var stable = aggregate.Where(item => !string.IsNullOrWhiteSpace(item.DestinationPath)).ToArray();
            var summary = Summarize(checkpoint.OriginalRequest.SourceFiles.Count, aggregate);
            var state = ResolveState(checkpoint.OriginalRequest.SourceFiles.Count, aggregate);
            requests.Update(context.Definition.Id, new(checkpoint.OriginalRequest, pendingSources, stable));
            var cancellationWithStableOutput = stable.Length > 0 &&
                (cancellationToken.IsCancellationRequested || context.RuntimeState.State == TaskLifecycleState.Cancelling);
            var finalState = state == TaskLifecycleState.Completed && cancellationWithStableOutput
                ? TaskLifecycleState.PartiallyCompleted
                : state;
            removeCheckpoint = finalState == TaskLifecycleState.Completed;
            executionResult = new(finalState, summary,
                aggregate.FirstOrDefault(x => x.ErrorCode is not null)?.ErrorCode,
                finalState == TaskLifecycleState.Completed ? null : "One or more RAW items require attention; completed outputs are not repeated on retry.");
        }
        finally
        {
            await progress.DrainAsync().ConfigureAwait(false);
        }
        if (removeCheckpoint) requests.Remove(context.Definition.Id);
        return executionResult;
    }

    private static IReadOnlyList<RawToJpegItemResult> Merge(RawToJpegRecoveryCheckpoint checkpoint,
        IReadOnlyList<RawToJpegItemResult> current)
    {
        var bySource = checkpoint.StableResults.Concat(current)
            .GroupBy(item => Path.GetFullPath(item.SourcePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        return checkpoint.OriginalRequest.SourceFiles.Select((source, sequence) =>
        {
            var fullPath = Path.GetFullPath(source);
            return bySource.TryGetValue(fullPath, out var item)
                ? item with { Sequence = sequence, SourcePath = source }
                : new RawToJpegItemResult(sequence, RawToJpegItemState.Cancelled, source, null, 0, null,
                    ErrorCodeCatalog.CancelledByUser, "Conversion pending.");
        }).ToArray();
    }

    private static TaskResultSummary Summarize(int total, IReadOnlyCollection<RawToJpegItemResult> items) => new(total,
        items.Count(item => item.State == RawToJpegItemState.Completed),
        items.Count(item => item.State == RawToJpegItemState.Failed), 0,
        items.Count(item => item.State == RawToJpegItemState.Cancelled),
        items.Count(item => item.State is RawToJpegItemState.NeedsAttention or RawToJpegItemState.PartiallyCompleted),
        0, items.Sum(item => item.BytesWritten));

    private static TaskLifecycleState ResolveState(int total, IReadOnlyCollection<RawToJpegItemResult> items)
    {
        var summary = Summarize(total, items);
        if (summary.Succeeded == total) return TaskLifecycleState.Completed;
        if (items.Any(item => !string.IsNullOrWhiteSpace(item.DestinationPath))) return TaskLifecycleState.PartiallyCompleted;
        if (summary.WaitingForAttention > 0) return TaskLifecycleState.NeedsAttention;
        if (summary.Cancelled > 0 && summary.Failed == 0) return TaskLifecycleState.Cancelled;
        return TaskLifecycleState.Failed;
    }
}

public sealed class RawToJpegTaskCoordinator(ITaskEngine taskEngine, IRawToJpegRequestStore requests, IRawDecoder decoder) : IRawToJpegTaskCoordinator
{
    public RawDecoderCapability GetCapability() => decoder.GetCapability();

    public async Task<Guid> StartAsync(RawToJpegBatchRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        var taskId = Guid.NewGuid();
        requests.Register(taskId, request);
        try
        {
            await taskEngine.EnqueueAsync(new(taskId, request.ProjectId, RawToJpegDefaults.TaskType, "RAW 转 JPG",
                $"SourceCount={request.SourceFiles.Count};PathsRedacted;Quality={request.Options.JpegQuality}", null,
                DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
            return taskId;
        }
        catch
        {
            requests.Remove(taskId);
            throw;
        }
    }
}
