using System.Collections.Concurrent;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tasks;

public sealed class TaskEngine : ITaskEngine, ITaskCompletionStateProvider
{
    private readonly ITaskRepository _repository;
    private readonly ITaskScheduler _scheduler;
    private readonly IAuditLogService _auditLog;
    private readonly INotificationCenter _notifications;
    private readonly Dictionary<string, ITaskHandler> _handlers;
    private readonly ConcurrentDictionary<Guid, ExecutionControl> _controls = new();
    private readonly ConcurrentDictionary<Guid, TaskProgressSnapshot> _snapshots = new();
    private readonly TimeSpan _progressThrottle;

    public TaskEngine(ITaskRepository repository, ITaskScheduler scheduler, IEnumerable<ITaskHandler> handlers, IAuditLogService auditLog, INotificationCenter notifications, TimeSpan? progressThrottle = null)
    {
        _repository = repository;
        _scheduler = scheduler;
        _auditLog = auditLog;
        _notifications = notifications;
        _handlers = handlers.ToDictionary(x => x.TaskType, StringComparer.OrdinalIgnoreCase);
        _progressThrottle = progressThrottle ?? TimeSpan.FromMilliseconds(100);
    }

    public event EventHandler<TaskProgressSnapshot>? SnapshotChanged;
    public IReadOnlyList<TaskProgressSnapshot> Current => _snapshots.Values.OrderByDescending(x => x.UpdatedAt).ToArray();

    public async Task<Guid> EnqueueAsync(TaskDefinition definition, CancellationToken cancellationToken = default)
    {
        if (!_handlers.ContainsKey(definition.Type)) throw new InvalidOperationException($"No task handler is registered for '{definition.Type}'.");
        var runtime = new TaskRuntimeState { Definition = definition };
        await _repository.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
        await _auditLog.WriteAsync("Task", "Created", "Information", $"任务已创建：{definition.DisplayName}", definition.Id, definition.ProjectId, correlationId: definition.Id.ToString("N"), cancellationToken: cancellationToken).ConfigureAwait(false);
        Publish(runtime, force: true);
        var control = new ExecutionControl(runtime);
        if (!_controls.TryAdd(definition.Id, control)) throw new InvalidOperationException("Task id already exists.");
        control.Execution = RunAsync(control);
        return definition.Id;
    }

    public async Task WaitForCompletionAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var execution = GetControl(taskId).Execution;
        if (execution is not null)
            await execution.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PauseAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var control = GetControl(taskId);
        if (control.Runtime.State is not (TaskLifecycleState.Running or TaskLifecycleState.Scanning)) return;
        await TransitionAsync(control.Runtime, TaskLifecycleState.Pausing, cancellationToken).ConfigureAwait(false);
        control.PauseRequested = true;
    }

    public async Task ResumeAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var control = GetControl(taskId);
        if (control.Runtime.State != TaskLifecycleState.Paused) return;
        control.PauseRequested = false;
        await TransitionAsync(control.Runtime, TaskLifecycleState.Running, cancellationToken).ConfigureAwait(false);
        control.ResumeSignal.TrySetResult(true);
    }

    public async Task CancelAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var control = GetControl(taskId);
        if (TaskStateMachine.IsTerminal(control.Runtime.State)) return;
        if (control.Runtime.State != TaskLifecycleState.Cancelling)
            await TransitionAsync(control.Runtime, TaskLifecycleState.Cancelling, cancellationToken).ConfigureAwait(false);
        control.Cancellation.Cancel();
        control.ResumeSignal.TrySetResult(true);
        control.AttentionSignal?.TrySetCanceled();
    }

    public async Task RetryAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetAsync(taskId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Task not found.");
        if (existing.RetryCount >= existing.Definition.MaximumRetryCount)
            throw new InvalidOperationException(ErrorCodeCatalog.RetryLimitReached);
        if (existing.State is not (TaskLifecycleState.Failed or TaskLifecycleState.PartiallyCompleted or TaskLifecycleState.Cancelled or TaskLifecycleState.Interrupted or TaskLifecycleState.NeedsAttention))
            throw new InvalidOperationException(ErrorCodeCatalog.InvalidStateTransition);
        existing.RetryCount++;
        existing.LastErrorCode = null;
        existing.LastErrorMessage = null;
        existing.CompletedAt = null;
        await TransitionAsync(existing, TaskLifecycleState.Retrying, cancellationToken).ConfigureAwait(false);
        var control = new ExecutionControl(existing);
        _controls[taskId] = control;
        control.Execution = RunAsync(control);
    }

    public Task ResolveAttentionAsync(Guid taskId, string action, CancellationToken cancellationToken = default)
    {
        var control = GetControl(taskId);
        if (control.Runtime.State != TaskLifecycleState.NeedsAttention || control.AttentionSignal is null)
            throw new InvalidOperationException(ErrorCodeCatalog.InvalidStateTransition);
        control.AttentionSignal.TrySetResult(action);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TaskRuntimeState>> LoadHistoryAsync(int limit = 200, CancellationToken cancellationToken = default) => _repository.ListAsync(limit, cancellationToken);

    public Task<TaskRuntimeState?> GetTaskStateAsync(Guid taskId, CancellationToken cancellationToken = default) => _repository.GetAsync(taskId, cancellationToken);

    private async Task RunAsync(ExecutionControl control)
    {
        var runtime = control.Runtime;
        try
        {
            using var lease = await _scheduler.AcquireAsync(runtime.Definition, control.Cancellation.Token).ConfigureAwait(false);
            if (runtime.State == TaskLifecycleState.Pending)
                await TransitionAsync(runtime, TaskLifecycleState.Preparing, control.Cancellation.Token).ConfigureAwait(false);
            runtime.StartedAt ??= DateTimeOffset.UtcNow;
            await TransitionAsync(runtime, TaskLifecycleState.Running, control.Cancellation.Token).ConfigureAwait(false);
            var context = new TaskExecutionContext(runtime.Definition, runtime,
                (step, count, payload, token) => SafeBoundaryAsync(control, step, count, payload, token),
                (snapshot, token) => ReportProgressAsync(control, snapshot, token),
                (request, token) => RequestAttentionAsync(control, request, token));
            var result = await _handlers[runtime.Definition.Type].ExecuteAsync(context, control.Cancellation.Token).ConfigureAwait(false);
            runtime.ResultSummary = result.Summary;
            runtime.LastErrorCode = result.ErrorCode;
            runtime.LastErrorMessage = result.ErrorMessage;
            var final = result.FinalState == TaskLifecycleState.Completed && result.Summary.IsPartial ? TaskLifecycleState.PartiallyCompleted : result.FinalState;
            if (final == TaskLifecycleState.Failed && string.IsNullOrWhiteSpace(runtime.LastErrorMessage))
                runtime.LastErrorMessage = "验证失败：任务未生成可验证的完整输出，请查看任务诊断后重试。";
            if (runtime.State == TaskLifecycleState.Cancelling)
                final = result.Summary.Succeeded > 0 ? TaskLifecycleState.PartiallyCompleted : TaskLifecycleState.Cancelled;
            await TransitionAsync(runtime, final, CancellationToken.None).ConfigureAwait(false);
            runtime.Progress = final == TaskLifecycleState.Completed ? 100 : runtime.Progress;
            runtime.CompletedAt = DateTimeOffset.UtcNow;
            await PersistAndPublishAsync(runtime, true, CancellationToken.None).ConfigureAwait(false);
            await NotifyTerminalStatePersistedAsync(runtime, CancellationToken.None).ConfigureAwait(false);
            await PublishCompletionNotificationAsync(runtime).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            runtime.LastErrorCode = ErrorCodeCatalog.CancelledByUser;
            runtime.LastErrorMessage = "任务已在安全边界停止；已完成输出被保留，未完成临时文件已清理。";
            var final = runtime.ResultSummary.Succeeded > 0 ? TaskLifecycleState.PartiallyCompleted : TaskLifecycleState.Cancelled;
            if (runtime.State != TaskLifecycleState.Cancelling && TaskStateMachine.CanTransition(runtime.State, TaskLifecycleState.Cancelling))
                await TransitionAsync(runtime, TaskLifecycleState.Cancelling, CancellationToken.None).ConfigureAwait(false);
            await TransitionAsync(runtime, final, CancellationToken.None).ConfigureAwait(false);
            runtime.CompletedAt = DateTimeOffset.UtcNow;
            await PersistAndPublishAsync(runtime, true, CancellationToken.None).ConfigureAwait(false);
            await NotifyTerminalStatePersistedAsync(runtime, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            runtime.LastErrorCode ??= MapException(ex);
            runtime.LastErrorMessage = ex.Message;
            if (!TaskStateMachine.IsTerminal(runtime.State) && TaskStateMachine.CanTransition(runtime.State, TaskLifecycleState.Failed))
                await TransitionAsync(runtime, TaskLifecycleState.Failed, CancellationToken.None).ConfigureAwait(false);
            runtime.CompletedAt = DateTimeOffset.UtcNow;
            await PersistAndPublishAsync(runtime, true, CancellationToken.None).ConfigureAwait(false);
            await NotifyTerminalStatePersistedAsync(runtime, CancellationToken.None).ConfigureAwait(false);
            await _notifications.PublishAsync(new NotificationMessage(Guid.NewGuid(), NotificationType.TaskNotification, NotificationSeverity.Error, runtime.Definition.DisplayName, $"任务未完成：{ErrorCodeCatalog.Describe(runtime.LastErrorCode)}", runtime.Definition.Id, runtime.Definition.ProjectId, [], false, DateTimeOffset.UtcNow, DeduplicationKey: $"task-error-{runtime.Definition.Id}"), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task SafeBoundaryAsync(ExecutionControl control, string stepName, int completedItems, string? payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        control.Cancellation.Token.ThrowIfCancellationRequested();
        var runtime = control.Runtime;
        runtime.CurrentStep = stepName;
        runtime.Checkpoint = new TaskCheckpoint(stepName, completedItems, payload, DateTimeOffset.UtcNow);
        await _repository.SaveCheckpointAsync(runtime.Definition.Id, runtime.Checkpoint, cancellationToken).ConfigureAwait(false);
        await PersistAndPublishAsync(runtime, true, cancellationToken).ConfigureAwait(false);
        if (!control.PauseRequested) return;
        if (runtime.State == TaskLifecycleState.Pausing) await TransitionAsync(runtime, TaskLifecycleState.Paused, cancellationToken).ConfigureAwait(false);
        control.ResumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await control.ResumeSignal.Task.WaitAsync(control.Cancellation.Token).ConfigureAwait(false);
        control.Cancellation.Token.ThrowIfCancellationRequested();
    }

    private async Task ReportProgressAsync(ExecutionControl control, TaskProgressSnapshot snapshot, CancellationToken cancellationToken)
    {
        var runtime = control.Runtime;
        runtime.Progress = Math.Clamp(snapshot.Progress, 0, 100);
        runtime.CurrentStep = snapshot.CurrentStep;
        runtime.CurrentFile = snapshot.CurrentFile;
        runtime.ResultSummary = snapshot.Summary;
        var now = DateTimeOffset.UtcNow;
        var force = now - control.LastProgressPublished >= _progressThrottle || runtime.Progress >= 100;
        if (force) control.LastProgressPublished = now;
        await PersistAndPublishAsync(runtime, force, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> RequestAttentionAsync(ExecutionControl control, TaskAttentionRequest request, CancellationToken cancellationToken)
    {
        var runtime = control.Runtime;
        runtime.AttentionRequest = request;
        await TransitionAsync(runtime, TaskLifecycleState.NeedsAttention, cancellationToken).ConfigureAwait(false);
        control.AttentionSignal = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var action = await control.AttentionSignal.Task.WaitAsync(control.Cancellation.Token).ConfigureAwait(false);
        runtime.AttentionRequest = null;
        await TransitionAsync(runtime, TaskLifecycleState.Running, cancellationToken).ConfigureAwait(false);
        return action;
    }

    private async Task TransitionAsync(TaskRuntimeState runtime, TaskLifecycleState next, CancellationToken cancellationToken)
    {
        var previous = runtime.State;
        TaskStateMachine.EnsureTransition(previous, next);
        runtime.State = next;
        runtime.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
        await _auditLog.WriteAsync("Task", "StateTransition", "Information", $"{previous} -> {next}", runtime.Definition.Id, runtime.Definition.ProjectId, correlationId: runtime.Definition.Id.ToString("N"), cancellationToken: cancellationToken).ConfigureAwait(false);
        Publish(runtime, force: true);
    }

    private async Task PersistAndPublishAsync(TaskRuntimeState runtime, bool force, CancellationToken cancellationToken)
    {
        runtime.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
        Publish(runtime, force);
    }

    private Task NotifyTerminalStatePersistedAsync(TaskRuntimeState runtime, CancellationToken cancellationToken) =>
        _handlers[runtime.Definition.Type] is ITaskTerminalStateObserver observer
            ? observer.OnTerminalStatePersistedAsync(runtime.Definition.Id, runtime.State, cancellationToken)
            : Task.CompletedTask;

    private void Publish(TaskRuntimeState runtime, bool force)
    {
        if (!force) return;
        var snapshot = new TaskProgressSnapshot(runtime.Definition.Id, runtime.Definition.ProjectId, runtime.Definition.DisplayName, runtime.State, runtime.Progress, runtime.CurrentStep, runtime.CurrentFile, runtime.ResultSummary, null, null, runtime.LastErrorCode, runtime.LastErrorMessage, runtime.LastUpdatedAt);
        _snapshots[runtime.Definition.Id] = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    private Task PublishCompletionNotificationAsync(TaskRuntimeState runtime) => _notifications.PublishAsync(new NotificationMessage(Guid.NewGuid(), NotificationType.TaskNotification,
        runtime.State == TaskLifecycleState.Completed ? NotificationSeverity.Success : NotificationSeverity.Warning,
        runtime.Definition.DisplayName,
        runtime.State == TaskLifecycleState.Completed ? "任务已完成。" : $"任务部分完成：成功 {runtime.ResultSummary.Succeeded}，失败 {runtime.ResultSummary.Failed}，跳过 {runtime.ResultSummary.Skipped}。",
        runtime.Definition.Id, runtime.Definition.ProjectId, [], false, DateTimeOffset.UtcNow, DeduplicationKey: $"task-complete-{runtime.Definition.Id}"));

    private ExecutionControl GetControl(Guid taskId) => _controls.TryGetValue(taskId, out var control) ? control : throw new KeyNotFoundException("Task not found or is not active.");
    private static string MapException(Exception ex) => ex switch
    {
        FileNotFoundException or DirectoryNotFoundException => ErrorCodeCatalog.SourceNotFound,
        UnauthorizedAccessException => ErrorCodeCatalog.PermissionDenied,
        IOException io when io.HResult == unchecked((int)0x80070020) => ErrorCodeCatalog.FileLocked,
        IOException => ErrorCodeCatalog.DestinationNotWritable,
        _ => "UnhandledTaskError"
    };

    private sealed class ExecutionControl(TaskRuntimeState runtime)
    {
        public TaskRuntimeState Runtime { get; } = runtime;
        public CancellationTokenSource Cancellation { get; } = new();
        public volatile bool PauseRequested;
        public TaskCompletionSource<bool> ResumeSignal { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string?>? AttentionSignal { get; set; }
        public Task? Execution { get; set; }
        public DateTimeOffset LastProgressPublished { get; set; }
    }
}

public sealed class DelegateTaskHandler(string taskType, Func<TaskExecutionContext, CancellationToken, Task<TaskExecutionResult>> execute) : ITaskHandler
{
    public string TaskType { get; } = taskType;
    public Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken) => execute(context, cancellationToken);
}
