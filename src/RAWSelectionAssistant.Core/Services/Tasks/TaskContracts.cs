using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tasks;

public interface ITaskHandler
{
    string TaskType { get; }
    Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken);
}

public interface ITaskTerminalStateObserver
{
    Task OnTerminalStatePersistedAsync(Guid taskId, TaskLifecycleState terminalState,
        CancellationToken cancellationToken = default);
}

public interface ITaskRepository
{
    Task SaveAsync(TaskRuntimeState state, CancellationToken cancellationToken = default);
    Task<TaskRuntimeState?> GetAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskRuntimeState>> ListAsync(int limit = 200, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskRuntimeState>> ListUnfinishedAsync(CancellationToken cancellationToken = default);
    Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint, CancellationToken cancellationToken = default);
}

public interface ITaskScheduler
{
    Task<IDisposable> AcquireAsync(TaskDefinition definition, CancellationToken cancellationToken);
}

public interface ITaskRecoveryService
{
    Task<IReadOnlyList<TaskRuntimeState>> RecoverInterruptedAsync(CancellationToken cancellationToken = default);
}

public interface ITaskSnapshotProvider
{
    event EventHandler<TaskProgressSnapshot>? SnapshotChanged;
    IReadOnlyList<TaskProgressSnapshot> Current { get; }
}

public interface ITaskConflictResolver
{
    Task<string?> ResolveAsync(TaskAttentionRequest request, CancellationToken cancellationToken = default);
}

public interface ITaskEngine : ITaskSnapshotProvider
{
    Task<Guid> EnqueueAsync(TaskDefinition definition, CancellationToken cancellationToken = default);
    Task WaitForCompletionAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task PauseAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task ResumeAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task RetryAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task ResolveAttentionAsync(Guid taskId, string action, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskRuntimeState>> LoadHistoryAsync(int limit = 200, CancellationToken cancellationToken = default);
}

public sealed class TaskExecutionContext
{
    private readonly Func<string, int, string?, CancellationToken, Task> _safeBoundary;
    private readonly Func<TaskProgressSnapshot, CancellationToken, Task> _reportProgress;
    private readonly Func<TaskAttentionRequest, CancellationToken, Task<string?>> _attention;

    internal TaskExecutionContext(TaskDefinition definition, TaskRuntimeState runtimeState,
        Func<string, int, string?, CancellationToken, Task> safeBoundary,
        Func<TaskProgressSnapshot, CancellationToken, Task> reportProgress,
        Func<TaskAttentionRequest, CancellationToken, Task<string?>> attention)
    {
        Definition = definition;
        RuntimeState = runtimeState;
        _safeBoundary = safeBoundary;
        _reportProgress = reportProgress;
        _attention = attention;
    }

    public TaskDefinition Definition { get; }
    public TaskRuntimeState RuntimeState { get; }
    public Task SafeBoundaryAsync(string stepName, int completedItems, string? checkpointPayload = null, CancellationToken cancellationToken = default) =>
        _safeBoundary(stepName, completedItems, checkpointPayload, cancellationToken);
    public Task ReportProgressAsync(double progress, string step, string? currentFile, TaskResultSummary summary, CancellationToken cancellationToken = default) =>
        _reportProgress(new TaskProgressSnapshot(Definition.Id, Definition.ProjectId, Definition.DisplayName, RuntimeState.State, progress, step, currentFile, summary, null, null, RuntimeState.LastErrorCode, RuntimeState.LastErrorMessage, DateTimeOffset.UtcNow), cancellationToken);
    public Task<string?> RequestAttentionAsync(TaskAttentionRequest request, CancellationToken cancellationToken = default) => _attention(request, cancellationToken);
}

