namespace RAWSelectionAssistant.Core.Models;

public enum FileOperationType { Copy, Move, Rename, DeleteCreatedOutput }
public enum FileConflictPolicy { AutoNumber, Skip, Ask, Fail }
public enum FileOperationItemState { Pending, Validating, Running, Completed, Skipped, Failed, Cancelled, NeedsAttention }
public enum FileOperationRiskLevel { Low, Medium, High }

public sealed record FileOperationPlan(
    int SchemaVersion,
    Guid PlanId,
    Guid TaskId,
    Guid? ProjectId,
    FileOperationType OperationType,
    string SourceRoot,
    string DestinationRoot,
    FileConflictPolicy ConflictPolicy,
    IReadOnlyList<FileOperationItem> Items,
    long EstimatedBytes,
    FileOperationRiskLevel RiskLevel,
    DateTimeOffset CreatedAt);

public sealed record FileOperationItem(
    Guid Id,
    int Sequence,
    string SourcePath,
    string DestinationPath,
    FileOperationType OperationType,
    FileConflictPolicy ConflictPolicy,
    long? ExpectedSourceSize = null,
    DateTimeOffset? ExpectedSourceModifiedAt = null,
    string? OptionalSourceHash = null);

public sealed record FileOperationValidationIssue(string ErrorCode, string Message, Guid? ItemId = null, bool RequiresAttention = false);
public sealed record FileOperationValidationResult(bool IsValid, IReadOnlyList<FileOperationValidationIssue> Issues, long EstimatedBytes, FileOperationRiskLevel RiskLevel);
public sealed record FileOperationItemResult(Guid ItemId, FileOperationItemState State, string? DestinationPath, long BytesWritten, string? Hash, string? ErrorCode, string? ErrorMessage);
public sealed record FileOperationExecutionResult(TaskResultSummary Summary, IReadOnlyList<FileOperationItemResult> Items);

public enum UndoJournalState { Pending, Applied, Rejected, Failed }

public sealed record UndoJournalEntry(
    Guid Id,
    Guid TaskId,
    int Sequence,
    FileOperationType ReverseOperation,
    string SourcePath,
    string DestinationPath,
    long? ExpectedCurrentSize,
    string? ExpectedCurrentHash,
    string Preconditions,
    UndoJournalState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt = null);

