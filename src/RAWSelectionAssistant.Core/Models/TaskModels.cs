namespace RAWSelectionAssistant.Core.Models;

public enum TaskLifecycleState
{
    Pending,
    Preparing,
    Scanning,
    Validating,
    WaitingForConfirmation,
    Running,
    Pausing,
    Paused,
    NeedsAttention,
    Retrying,
    Cancelling,
    Cancelled,
    PartiallyCompleted,
    Failed,
    Completed,
    Interrupted
}

public enum TaskPriority { Low = 0, Normal = 1, High = 2 }

public sealed record TaskDefinition(
    Guid Id,
    Guid? ProjectId,
    string Type,
    string DisplayName,
    string InputSnapshot,
    Guid? OperationPlanId,
    DateTimeOffset CreatedAt,
    TaskPriority Priority = TaskPriority.Normal,
    int MaximumRetryCount = 3);

public sealed record TaskStepDefinition(Guid Id, Guid TaskId, int Sequence, string Name);

public sealed record TaskCheckpoint(string StepName, int CompletedItems, string? Payload, DateTimeOffset CreatedAt);

public sealed record TaskResultSummary(
    int Total,
    int Succeeded,
    int Failed,
    int Skipped,
    int Cancelled,
    int WaitingForAttention,
    long BytesProcessed,
    long BytesWritten)
{
    public static TaskResultSummary Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);
    public bool IsPartial => Succeeded > 0 && (Failed + Skipped + Cancelled + WaitingForAttention) > 0;
}

public sealed record TaskProgressSnapshot(
    Guid TaskId,
    Guid? ProjectId,
    string DisplayName,
    TaskLifecycleState State,
    double Progress,
    string CurrentStep,
    string? CurrentFile,
    TaskResultSummary Summary,
    double? BytesPerSecond,
    TimeSpan? EstimatedRemaining,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset UpdatedAt);

public enum TaskAttentionType
{
    DuplicateConflict,
    MultipleCandidates,
    SourceChanged,
    DestinationDisconnected,
    DestinationInvalid,
    FileLocked,
    PermissionChanged,
    DatabaseLocked,
    DiskSpaceInsufficient,
    ExternalModification,
    Other
}

public sealed record TaskAttentionRequest(
    Guid Id,
    Guid TaskId,
    TaskAttentionType Type,
    string Title,
    string Description,
    int AffectedItemCount,
    IReadOnlyList<string> AllowedActions,
    string DefaultAction,
    bool IsDestructive,
    DateTimeOffset CreatedAt);

public sealed class TaskRuntimeState
{
    public required TaskDefinition Definition { get; init; }
    public TaskLifecycleState State { get; set; } = TaskLifecycleState.Pending;
    public double Progress { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public string? CurrentFile { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public TaskCheckpoint? Checkpoint { get; set; }
    public TaskResultSummary ResultSummary { get; set; } = TaskResultSummary.Empty;
    public TaskAttentionRequest? AttentionRequest { get; set; }
}

public sealed record TaskExecutionResult(TaskLifecycleState FinalState, TaskResultSummary Summary, string? ErrorCode = null, string? ErrorMessage = null)
{
    public static TaskExecutionResult Completed(TaskResultSummary summary) => new(TaskLifecycleState.Completed, summary);
}

