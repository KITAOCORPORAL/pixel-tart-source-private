using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services.FileOperations;

public sealed class FileOperationExecutor(
    IFileOperationValidator validator,
    IFileVerificationService verification,
    IUndoJournalRepository undoRepository,
    IPixelTartDatabase database) : IFileOperationExecutor
{
    public async Task<FileOperationExecutionResult> ExecuteAsync(FileOperationPlan plan, Func<string, int, string?, CancellationToken, Task>? safeBoundary = null, IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null, CancellationToken cancellationToken = default)
    {
        await EnsureTaskRecordAsync(plan, cancellationToken).ConfigureAwait(false);
        var validation = await validator.ValidateAsync(plan, cancellationToken).ConfigureAwait(false);
        var planIssues = validation.Issues.Where(x => x.ItemId is null).ToArray();
        if (planIssues.Length > 0)
        {
            var issues = planIssues.Select(issue => new FileOperationItemResult(Guid.Empty, issue.RequiresAttention ? FileOperationItemState.NeedsAttention : FileOperationItemState.Failed, null, 0, null, issue.ErrorCode, issue.Message)).ToArray();
            var summary = new TaskResultSummary(plan.Items.Count, 0, issues.Count(x => x.State == FileOperationItemState.Failed), 0, 0, issues.Count(x => x.State == FileOperationItemState.NeedsAttention), 0, 0);
            return new(summary, issues);
        }

        var itemIssues = validation.Issues.Where(x => x.ItemId is not null).GroupBy(x => x.ItemId!.Value).ToDictionary(x => x.Key, x => x.First());
        var results = itemIssues.Select(pair => new FileOperationItemResult(pair.Key, pair.Value.RequiresAttention ? FileOperationItemState.NeedsAttention : FileOperationItemState.Failed, null, 0, null, pair.Value.ErrorCode, pair.Value.Message)).ToList();
        long processed = 0;
        long written = 0;
        foreach (var item in plan.Items.OrderBy(x => x.Sequence))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (itemIssues.ContainsKey(item.Id)) continue;
            if (safeBoundary is not null)
                await safeBoundary("文件操作", item.Sequence, item.Id.ToString("D"), cancellationToken).ConfigureAwait(false);
            var result = await ExecuteItemAsync(plan.TaskId, item, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            processed += item.ExpectedSourceSize ?? 0;
            written += result.BytesWritten;
            var summary = BuildSummary(plan.Items.Count, results, processed, written);
            progress?.Report((plan.Items.Count == 0 ? 100 : results.Count * 100d / plan.Items.Count, Path.GetFileName(item.SourcePath), summary));
        }
        return new(BuildSummary(plan.Items.Count, results, processed, written), results);
    }

    private async Task<FileOperationItemResult> ExecuteItemAsync(Guid taskId, FileOperationItem item, CancellationToken cancellationToken)
    {
        var destination = item.DestinationPath;
        var created = false;
        try
        {
            await SaveItemStateAsync(taskId, item, FileOperationItemState.Running, null, null, cancellationToken).ConfigureAwait(false);
            var sourceInfo = new FileInfo(item.SourcePath);
            if (!sourceInfo.Exists) throw new FileNotFoundException("Source file not found.", item.SourcePath);
            var originalSize = sourceInfo.Length;
            var originalModified = sourceInfo.LastWriteTimeUtc;
            var sourceHash = item.OptionalSourceHash;
            var verifyHash = item.OperationType == FileOperationType.Move || !string.IsNullOrWhiteSpace(sourceHash);
            if (verifyHash && string.IsNullOrWhiteSpace(sourceHash)) sourceHash = await verification.ComputeSha256Async(item.SourcePath, cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await CopyCreateNewAsync(item.SourcePath, destination, cancellationToken).ConfigureAwait(false);
            created = true;
            if (!await verification.VerifyAsync(item.SourcePath, destination, verifyHash, cancellationToken).ConfigureAwait(false))
                throw new InvalidDataException(ErrorCodeCatalog.HashMismatch);
            var destinationHash = await verification.ComputeSha256Async(destination, cancellationToken).ConfigureAwait(false);

            if (item.OperationType == FileOperationType.Move)
            {
                sourceInfo.Refresh();
                if (!sourceInfo.Exists || sourceInfo.Length != originalSize || Math.Abs((sourceInfo.LastWriteTimeUtc - originalModified).TotalSeconds) > 1)
                    throw new IOException(ErrorCodeCatalog.SourceChanged);
                if (!string.Equals(sourceHash, await verification.ComputeSha256Async(item.SourcePath, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
                    throw new IOException(ErrorCodeCatalog.SourceChanged);
                await SaveItemStateAsync(taskId, item, FileOperationItemState.Completed, destinationHash, null, cancellationToken).ConfigureAwait(false);
                File.Delete(item.SourcePath);
                await undoRepository.AppendAsync(new UndoJournalEntry(Guid.NewGuid(), taskId, item.Sequence, FileOperationType.Move, destination, item.SourcePath, originalSize, destinationHash, "destination unchanged; original path absent", UndoJournalState.Pending, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SaveItemStateAsync(taskId, item, FileOperationItemState.Completed, destinationHash, null, cancellationToken).ConfigureAwait(false);
                await undoRepository.AppendAsync(new UndoJournalEntry(Guid.NewGuid(), taskId, item.Sequence, FileOperationType.DeleteCreatedOutput, item.SourcePath, destination, originalSize, destinationHash, "output created by task and unchanged", UndoJournalState.Pending, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
            }
            return new(item.Id, FileOperationItemState.Completed, destination, originalSize, destinationHash, null, null);
        }
        catch (OperationCanceledException)
        {
            if (created) TryDeleteTaskOutput(destination);
            await SaveItemStateAsync(taskId, item, FileOperationItemState.Cancelled, null, ErrorCodeCatalog.CancelledByUser, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (created && File.Exists(item.SourcePath)) TryDeleteTaskOutput(destination);
            var code = Map(ex);
            await SaveItemStateAsync(taskId, item, FileOperationItemState.Failed, null, code, CancellationToken.None).ConfigureAwait(false);
            return new(item.Id, FileOperationItemState.Failed, created && File.Exists(destination) ? destination : null, 0, null, code, ex.Message);
        }
    }

    private static async Task CopyCreateNewAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 262144, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 262144, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
        await source.CopyToAsync(destination, 262144, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        destination.Flush(true);
    }

    private async Task SaveItemStateAsync(Guid taskId, FileOperationItem item, FileOperationItemState state, string? outputHash, string? errorCode, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO OperationItems(Id,TaskId,Sequence,SourcePath,DestinationPath,OperationType,ConflictPolicy,ExpectedSourceSize,ExpectedSourceModifiedAt,OptionalSourceHash,ActualOutputSize,OptionalOutputHash,State,ErrorCode,StartedAt,CompletedAt)
            VALUES($id,$task,$sequence,$source,$destination,$operation,$policy,$expectedSize,$expectedModified,$sourceHash,$actualSize,$outputHash,$state,$error,$started,$completed)
            ON CONFLICT(TaskId,Sequence) DO UPDATE SET DestinationPath=excluded.DestinationPath,ActualOutputSize=excluded.ActualOutputSize,OptionalOutputHash=excluded.OptionalOutputHash,State=excluded.State,ErrorCode=excluded.ErrorCode,CompletedAt=excluded.CompletedAt;
            """;
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$task", taskId.ToString("D"));
        command.Parameters.AddWithValue("$sequence", item.Sequence);
        command.Parameters.AddWithValue("$source", item.SourcePath);
        command.Parameters.AddWithValue("$destination", item.DestinationPath);
        command.Parameters.AddWithValue("$operation", item.OperationType.ToString());
        command.Parameters.AddWithValue("$policy", item.ConflictPolicy.ToString());
        command.Parameters.AddWithValue("$expectedSize", (object?)item.ExpectedSourceSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$expectedModified", (object?)item.ExpectedSourceModifiedAt?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceHash", (object?)item.OptionalSourceHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$actualSize", state == FileOperationItemState.Completed && File.Exists(item.DestinationPath) ? new FileInfo(item.DestinationPath).Length : DBNull.Value);
        command.Parameters.AddWithValue("$outputHash", (object?)outputHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$error", (object?)errorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$started", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$completed", state is FileOperationItemState.Completed or FileOperationItemState.Failed or FileOperationItemState.Cancelled ? DateTimeOffset.UtcNow.ToString("O") : DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureTaskRecordAsync(FileOperationPlan plan, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO Tasks(Id,ProjectId,Type,DisplayName,State,Progress,CurrentStep,CreatedAt,StartedAt,CompletedAt,LastUpdatedAt,LastErrorCode,LastErrorMessage,RetryCount,InputSnapshot,ResultSummary,Priority,MaximumRetryCount,OperationPlanId)
            VALUES($id,$project,'FileOperation','文件操作','Running',0,'准备文件',$created,$created,NULL,$created,NULL,NULL,0,$input,NULL,1,3,$plan);
            """;
        command.Parameters.AddWithValue("$id", plan.TaskId.ToString("D"));
        command.Parameters.AddWithValue("$project", (object?)plan.ProjectId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", plan.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$input", $"write-root:{plan.DestinationRoot}");
        command.Parameters.AddWithValue("$plan", plan.PlanId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TaskResultSummary BuildSummary(int total, IReadOnlyList<FileOperationItemResult> results, long processed, long written) => new(total,
        results.Count(x => x.State == FileOperationItemState.Completed), results.Count(x => x.State == FileOperationItemState.Failed), results.Count(x => x.State == FileOperationItemState.Skipped), results.Count(x => x.State == FileOperationItemState.Cancelled), results.Count(x => x.State == FileOperationItemState.NeedsAttention), processed, written);
    private static void TryDeleteTaskOutput(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static string Map(Exception ex) => ex switch
    {
        UnauthorizedAccessException => ErrorCodeCatalog.PermissionDenied,
        FileNotFoundException or DirectoryNotFoundException => ErrorCodeCatalog.SourceNotFound,
        InvalidDataException => ErrorCodeCatalog.HashMismatch,
        IOException io when io.Message.Contains(ErrorCodeCatalog.SourceChanged, StringComparison.Ordinal) => ErrorCodeCatalog.SourceChanged,
        IOException io when io.HResult == unchecked((int)0x80070020) => ErrorCodeCatalog.FileLocked,
        IOException => ErrorCodeCatalog.DestinationNotWritable,
        _ => ErrorCodeCatalog.DestinationNotWritable
    };
}

public sealed class UndoJournalService(IUndoJournalRepository repository, IFileVerificationService verification) : IUndoJournalService
{
    public async Task<TaskResultSummary> UndoAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var entries = await repository.ListAsync(taskId, cancellationToken).ConfigureAwait(false);
        var succeeded = 0;
        var failed = 0;
        var attention = 0;
        long bytes = 0;
        foreach (var entry in entries.Where(x => x.State == UndoJournalState.Pending))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var currentPath = entry.ReverseOperation == FileOperationType.DeleteCreatedOutput ? entry.DestinationPath : entry.SourcePath;
                if (!File.Exists(currentPath) || entry.ExpectedCurrentSize is long size && new FileInfo(currentPath).Length != size ||
                    !string.IsNullOrWhiteSpace(entry.ExpectedCurrentHash) && !string.Equals(entry.ExpectedCurrentHash, await verification.ComputeSha256Async(currentPath, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
                {
                    attention++;
                    await repository.UpdateStateAsync(entry.Id, UndoJournalState.Rejected, null, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                bytes += new FileInfo(currentPath).Length;
                if (entry.ReverseOperation == FileOperationType.DeleteCreatedOutput)
                {
                    File.Delete(currentPath);
                }
                else if (entry.ReverseOperation is FileOperationType.Move or FileOperationType.Rename)
                {
                    if (File.Exists(entry.DestinationPath) || Directory.Exists(entry.DestinationPath))
                    {
                        attention++;
                        await repository.UpdateStateAsync(entry.Id, UndoJournalState.Rejected, null, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(entry.DestinationPath)!);
                    await CopyCreateNewAsync(entry.SourcePath, entry.DestinationPath, cancellationToken).ConfigureAwait(false);
                    if (!await verification.VerifyAsync(entry.SourcePath, entry.DestinationPath, verifyHash: true, cancellationToken).ConfigureAwait(false))
                    {
                        try { File.Delete(entry.DestinationPath); } catch { }
                        attention++;
                        await repository.UpdateStateAsync(entry.Id, UndoJournalState.Rejected, null, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    File.Delete(entry.SourcePath);
                }
                succeeded++;
                await repository.UpdateStateAsync(entry.Id, UndoJournalState.Applied, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
                await repository.UpdateStateAsync(entry.Id, UndoJournalState.Failed, null, cancellationToken).ConfigureAwait(false);
            }
        }
        return new(entries.Count, succeeded, failed, 0, 0, attention, bytes, 0);
    }

    private static async Task CopyCreateNewAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await source.CopyToAsync(destination, 131072, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        destination.Flush(true);
    }
}
