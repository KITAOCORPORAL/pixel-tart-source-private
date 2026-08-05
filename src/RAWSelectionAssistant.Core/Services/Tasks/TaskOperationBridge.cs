using System.Collections.Concurrent;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tasks;

public sealed class TaskOperationBridge : ITaskHandler
{
    private readonly ConcurrentDictionary<Guid, PendingOperation> _operations = new();
    private readonly ConcurrentQueue<Guid> _operationOrder = new();
    private ITaskEngine? _engine;

    public string TaskType => "ExistingOperation";
    public void Attach(ITaskEngine engine) => _engine = engine;

    public async Task<Guid> RunAsync(string displayName, Func<TaskExecutionContext, CancellationToken, Task<TaskResultSummary>> operation, Guid? projectId = null, string inputSnapshot = "", CancellationToken cancellationToken = default)
    {
        var engine = _engine ?? throw new InvalidOperationException("Task operation bridge is not attached to an engine.");
        var id = Guid.NewGuid();
        var pending = new PendingOperation(operation, SynchronizationContext.Current);
        if (!_operations.TryAdd(id, pending)) throw new InvalidOperationException("Duplicate task id.");
        _operationOrder.Enqueue(id);
        while (_operationOrder.Count > 200 && _operationOrder.TryDequeue(out var expired)) _operations.TryRemove(expired, out _);
        var definition = new TaskDefinition(id, projectId, TaskType, displayName, inputSnapshot, null, DateTimeOffset.UtcNow);
        await engine.EnqueueAsync(definition, cancellationToken);
        await pending.Completion.Task.WaitAsync(cancellationToken);
        await engine.WaitForCompletionAsync(id, CancellationToken.None).ConfigureAwait(false);
        return id;
    }

    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
    {
        if (!_operations.TryGetValue(context.Definition.Id, out var pending))
            return new(TaskLifecycleState.Failed, TaskResultSummary.Empty, ErrorCodeCatalog.CheckpointInvalid, "任务执行委托不存在。请从任务中心重试原操作。");
        try
        {
            var previous = TaskExecutionAmbient.CurrentTaskId.Value;
            var previousContext = TaskExecutionAmbient.CurrentContext.Value;
            TaskExecutionAmbient.CurrentTaskId.Value = context.Definition.Id;
            TaskExecutionAmbient.CurrentContext.Value = context;
            TaskResultSummary summary;
            try { summary = await pending.InvokeAsync(context, cancellationToken); }
            finally { TaskExecutionAmbient.CurrentTaskId.Value = previous; TaskExecutionAmbient.CurrentContext.Value = previousContext; }
            pending.Completion.TrySetResult(summary);
            if (summary.WaitingForAttention > 0 && summary.Succeeded == 0)
                return new(TaskLifecycleState.NeedsAttention, summary, ErrorCodeCatalog.NeedsUserDecision, "任务需要用户确认后继续。");
            if (summary.Failed > 0 && summary.Succeeded == 0)
                return new(TaskLifecycleState.Failed, summary, ErrorCodeCatalog.DestinationNotWritable, "任务未产生可用输出。");
            if (summary.Cancelled > 0 && summary.Succeeded == 0)
                return new(TaskLifecycleState.Cancelled, summary, ErrorCodeCatalog.CancelledByUser, "任务已取消。");
            return summary.IsPartial ? new(TaskLifecycleState.PartiallyCompleted, summary) : TaskExecutionResult.Completed(summary);
        }
        catch (OperationCanceledException)
        {
            pending.Completion.TrySetCanceled(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            pending.Completion.TrySetException(ex);
            throw;
        }
    }

    private sealed class PendingOperation(Func<TaskExecutionContext, CancellationToken, Task<TaskResultSummary>> operation, SynchronizationContext? synchronizationContext)
    {
        public TaskCompletionSource<TaskResultSummary> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TaskResultSummary> InvokeAsync(TaskExecutionContext context, CancellationToken cancellationToken)
        {
            if (synchronizationContext is null || SynchronizationContext.Current == synchronizationContext) return operation(context, cancellationToken);
            var completion = new TaskCompletionSource<TaskResultSummary>(TaskCreationOptions.RunContinuationsAsynchronously);
            synchronizationContext.Post(async _ =>
            {
                var previous = TaskExecutionAmbient.CurrentTaskId.Value;
                var previousContext = TaskExecutionAmbient.CurrentContext.Value;
                TaskExecutionAmbient.CurrentTaskId.Value = context.Definition.Id;
                TaskExecutionAmbient.CurrentContext.Value = context;
                try { completion.TrySetResult(await operation(context, cancellationToken)); }
                catch (OperationCanceledException) { completion.TrySetCanceled(cancellationToken); }
                catch (Exception ex) { completion.TrySetException(ex); }
                finally { TaskExecutionAmbient.CurrentTaskId.Value = previous; TaskExecutionAmbient.CurrentContext.Value = previousContext; }
            }, null);
            return completion.Task;
        }
    }
}

public static class TaskExecutionAmbient
{
    public static AsyncLocal<Guid?> CurrentTaskId { get; } = new();
    public static AsyncLocal<TaskExecutionContext?> CurrentContext { get; } = new();
}
