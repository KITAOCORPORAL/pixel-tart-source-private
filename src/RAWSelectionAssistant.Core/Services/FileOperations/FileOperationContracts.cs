using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.FileOperations;

public interface IFileOperationPlanner
{
    Task<FileOperationPlan> CreateAsync(Guid taskId, Guid? projectId, FileOperationType operationType, string sourceRoot, string destinationRoot, IEnumerable<string> sourceFiles, FileConflictPolicy conflictPolicy = FileConflictPolicy.AutoNumber, CancellationToken cancellationToken = default);
}

public interface IFileOperationExecutor
{
    Task<FileOperationExecutionResult> ExecuteAsync(FileOperationPlan plan, Func<string, int, string?, CancellationToken, Task>? safeBoundary = null, IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null, CancellationToken cancellationToken = default);
}

public interface IFileOperationValidator
{
    Task<FileOperationValidationResult> ValidateAsync(FileOperationPlan plan, CancellationToken cancellationToken = default);
}

public interface IFileConflictResolver
{
    string ResolveDestination(string desiredPath, FileConflictPolicy policy, ISet<string>? reservedPaths = null);
}

public interface IFileVerificationService
{
    Task<string?> ComputeSha256Async(string path, CancellationToken cancellationToken = default);
    Task<bool> VerifyAsync(string sourcePath, string destinationPath, bool verifyHash, CancellationToken cancellationToken = default);
}

public interface IUndoJournalRepository
{
    Task AppendAsync(UndoJournalEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UndoJournalEntry>> ListAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task UpdateStateAsync(Guid id, UndoJournalState state, DateTimeOffset? appliedAt, CancellationToken cancellationToken = default);
}

public interface IUndoJournalService
{
    Task<TaskResultSummary> UndoAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<TaskResultSummary> UndoFileAsync(Guid taskId, string destinationPath, CancellationToken cancellationToken = default);
}

