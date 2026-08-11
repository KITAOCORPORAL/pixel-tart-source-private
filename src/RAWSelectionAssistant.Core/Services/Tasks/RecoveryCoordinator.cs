using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.BatchCompression;
using RAWSelectionAssistant.Core.Services.RawToJpeg;

namespace RAWSelectionAssistant.Core.Services.Tasks;

public interface IRecoveryCoordinator
{
    Task<bool> ContinueAsync(Guid taskId, bool userConfirmedHighRisk = false, CancellationToken cancellationToken = default);
    Task<bool> RetryFailedAsync(Guid taskId, bool userConfirmedHighRisk = false, CancellationToken cancellationToken = default);
    Task<TaskResultSummary> RollbackSafeOutputsAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task AbandonAsync(Guid taskId, CancellationToken cancellationToken = default);
}

public sealed class RecoveryCoordinator(IPixelTartDatabase database, ITaskRepository tasks, IFileOperationExecutor executor,
    IUndoJournalService undo, IAuditLogService audit, ITaskEngine? handlerRecovery = null) : IRecoveryCoordinator
{
    public Task<bool> ContinueAsync(Guid taskId, bool userConfirmedHighRisk = false, CancellationToken cancellationToken = default) => RecoverFileItemsAsync(taskId, includeFailed: false, userConfirmedHighRisk, cancellationToken);
    public Task<bool> RetryFailedAsync(Guid taskId, bool userConfirmedHighRisk = false, CancellationToken cancellationToken = default) => RecoverFileItemsAsync(taskId, includeFailed: true, userConfirmedHighRisk, cancellationToken);
    public Task<TaskResultSummary> RollbackSafeOutputsAsync(Guid taskId, CancellationToken cancellationToken = default) => undo.UndoAsync(taskId, cancellationToken);

    public async Task AbandonAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var runtime = await tasks.GetAsync(taskId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Task not found.");
        if (runtime.State == TaskLifecycleState.Interrupted)
        {
            TaskStateMachine.EnsureTransition(runtime.State, TaskLifecycleState.Cancelled);
            runtime.State = TaskLifecycleState.Cancelled;
            runtime.LastErrorCode = ErrorCodeCatalog.CancelledByUser;
            runtime.LastErrorMessage = "用户已放弃恢复；已有输出和源文件保持现状。";
            runtime.CompletedAt = DateTimeOffset.UtcNow;
            runtime.LastUpdatedAt = DateTimeOffset.UtcNow;
            await tasks.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
            await audit.WriteAsync("Task", "RecoveryAbandoned", "Warning", runtime.LastErrorMessage, taskId, runtime.Definition.ProjectId, runtime.LastErrorCode, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> RecoverFileItemsAsync(Guid taskId, bool includeFailed, bool userConfirmedHighRisk, CancellationToken cancellationToken)
    {
        var runtime = await tasks.GetAsync(taskId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Task not found.");
        if (runtime.State == TaskLifecycleState.Interrupted && handlerRecovery is not null &&
            IsHandlerManaged(runtime.Definition.Type))
        {
            await handlerRecovery.RetryAsync(taskId, cancellationToken).ConfigureAwait(false);
            await handlerRecovery.WaitForCompletionAsync(taskId, cancellationToken).ConfigureAwait(false);
            return true;
        }
        var items = await LoadItemsAsync(taskId, includeFailed, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            runtime.State = TaskLifecycleState.NeedsAttention;
            runtime.LastErrorCode = ErrorCodeCatalog.CheckpointInvalid;
            runtime.LastErrorMessage = "该任务没有可自动恢复的文件操作清单。请重新打开原功能并核对输入。";
            runtime.LastUpdatedAt = DateTimeOffset.UtcNow;
            await tasks.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
            return false;
        }
        if (items.Any(x => x.OperationType is FileOperationType.Move or FileOperationType.DeleteCreatedOutput) && !userConfirmedHighRisk)
        {
            runtime.State = TaskLifecycleState.NeedsAttention;
            runtime.LastErrorCode = ErrorCodeCatalog.NeedsUserDecision;
            runtime.LastErrorMessage = "移动或删除类恢复需要用户再次确认，不会自动继续。";
            runtime.LastUpdatedAt = DateTimeOffset.UtcNow;
            await tasks.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
            return false;
        }
        if (runtime.State is TaskLifecycleState.Interrupted or TaskLifecycleState.Failed or TaskLifecycleState.PartiallyCompleted or TaskLifecycleState.NeedsAttention)
        {
            TaskStateMachine.EnsureTransition(runtime.State, TaskLifecycleState.Retrying);
            runtime.State = TaskLifecycleState.Retrying;
            runtime.RetryCount++;
            runtime.LastUpdatedAt = DateTimeOffset.UtcNow;
            await tasks.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
        }
        TaskStateMachine.EnsureTransition(runtime.State, TaskLifecycleState.Running);
        runtime.State = TaskLifecycleState.Running;
        await tasks.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
        var sourceRoot = CommonRoot(items.Select(x => x.SourcePath));
        var destinationRoot = CommonRoot(items.Select(x => x.DestinationPath));
        var operation = items.Select(x => x.OperationType).Distinct().Count() == 1 ? items[0].OperationType : FileOperationType.Copy;
        var plan = new FileOperationPlan(1, runtime.Definition.OperationPlanId ?? Guid.NewGuid(), taskId, runtime.Definition.ProjectId, operation, sourceRoot, destinationRoot, FileConflictPolicy.AutoNumber, items, items.Sum(x => x.ExpectedSourceSize ?? 0), operation == FileOperationType.Move ? FileOperationRiskLevel.Medium : FileOperationRiskLevel.Low, DateTimeOffset.UtcNow);
        var prior = await CountCompletedAsync(taskId, cancellationToken).ConfigureAwait(false);
        var result = await executor.ExecuteAsync(plan, cancellationToken: cancellationToken).ConfigureAwait(false);
        runtime.ResultSummary = result.Summary with { Total = Math.Max(prior + result.Summary.Total, prior), Succeeded = prior + result.Summary.Succeeded };
        var final = runtime.ResultSummary.Failed + runtime.ResultSummary.WaitingForAttention > 0 ? TaskLifecycleState.PartiallyCompleted : TaskLifecycleState.Completed;
        TaskStateMachine.EnsureTransition(runtime.State, final);
        runtime.State = final;
        runtime.Progress = 100;
        runtime.CompletedAt = DateTimeOffset.UtcNow;
        runtime.LastUpdatedAt = DateTimeOffset.UtcNow;
        runtime.LastErrorCode = final == TaskLifecycleState.Completed ? null : runtime.LastErrorCode;
        await tasks.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync("Task", "RecoveryExecuted", final == TaskLifecycleState.Completed ? "Information" : "Warning", $"恢复完成：成功 {runtime.ResultSummary.Succeeded}，失败 {runtime.ResultSummary.Failed}。", taskId, runtime.Definition.ProjectId, runtime.LastErrorCode, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<IReadOnlyList<FileOperationItem>> LoadItemsAsync(Guid taskId, bool includeFailed, CancellationToken cancellationToken)
    {
        var states = includeFailed ? "('Pending','Failed','NeedsAttention','Cancelled')" : "('Pending','NeedsAttention')";
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Id,Sequence,SourcePath,DestinationPath,OperationType,ConflictPolicy,ExpectedSourceSize,ExpectedSourceModifiedAt,OptionalSourceHash FROM OperationItems WHERE TaskId=$task AND State IN {states} ORDER BY Sequence;";
        command.Parameters.AddWithValue("$task", taskId.ToString("D"));
        var result = new List<FileOperationItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), Enum.Parse<FileOperationType>(reader.GetString(4)), Enum.Parse<FileConflictPolicy>(reader.GetString(5)), reader.IsDBNull(6) ? null : reader.GetInt64(6), reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)), reader.IsDBNull(8) ? null : reader.GetString(8)));
        return result;
    }

    private async Task<int> CountCompletedAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false); await using var command = connection.CreateCommand(); command.CommandText = "SELECT count(*) FROM OperationItems WHERE TaskId=$task AND State='Completed';"; command.Parameters.AddWithValue("$task", taskId.ToString("D")); return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static string CommonRoot(IEnumerable<string> paths)
    {
        var directories = paths.Select(path => Path.GetDirectoryName(Path.GetFullPath(path)) ?? Path.GetPathRoot(path) ?? path).ToArray();
        if (directories.Length == 0) return Path.GetTempPath();
        var root = directories[0];
        while (directories.Any(x => !x.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !string.Equals(x, root, StringComparison.OrdinalIgnoreCase))) root = Path.GetDirectoryName(root) ?? Path.GetPathRoot(root) ?? root;
        return root;
    }

    private static bool IsHandlerManaged(string taskType) =>
        string.Equals(taskType, RawToJpegDefaults.TaskType, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(taskType, BatchCompressionDefaults.TaskType, StringComparison.OrdinalIgnoreCase);
}
