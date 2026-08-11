using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.FileOperations;

namespace RAWSelectionAssistant.Core.Services.BatchCompression;

public sealed class BatchCompressionSafeService(
    IBatchCompressionEncoder encoder,
    IFileOperationValidator validator,
    IFileConflictResolver conflictResolver,
    IFileVerificationService verification,
    IAuditLogService auditLog,
    INotificationCenter notifications,
    IFileOperationExecutor operationExecutor) : IBatchCompressionService
{
    public async Task<BatchCompressionResult> CompressAsync(
        Guid taskId,
        BatchCompressionRequest request,
        IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
        Func<BatchCompressionItemResult, Task>? itemCompleted = null,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        var sources = request.SourceFiles.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var sequences = request.SourceSequences?.ToArray() ?? Enumerable.Range(0, sources.Length).ToArray();
        var destinationRoot = Path.GetFullPath(request.DestinationDirectory);
        var plan = CreatePlan(taskId, request.ProjectId, sources, sequences, destinationRoot);
        var validation = await validator.ValidateAsync(plan, cancellationToken).ConfigureAwait(false);
        var planIssue = validation.Issues.FirstOrDefault(issue => issue.ItemId is null);
        var results = new List<BatchCompressionItemResult>(plan.Items.Count);
        if (planIssue is not null)
        {
            results.AddRange(plan.Items.Select(item => Failure(item, planIssue)));
            var rejected = Summarize(plan.Items.Count, results);
            await WriteAuditAsync("Rejected", taskId, request.ProjectId, planIssue.ErrorCode, cancellationToken).ConfigureAwait(false);
            await NotifyAttentionAsync(taskId, request.ProjectId, cancellationToken).ConfigureAwait(false);
            return new(taskId, ResolveState(rejected), rejected, results);
        }

        var itemIssues = validation.Issues.Where(issue => issue.ItemId is not null)
            .GroupBy(issue => issue.ItemId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        try
        {
            foreach (var item in plan.Items.OrderBy(item => item.Sequence))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = itemIssues.TryGetValue(item.Id, out var issue)
                    ? Failure(item, issue)
                    : await CompressItemAsync(taskId, request.ProjectId, item, request.Options, cancellationToken)
                        .ConfigureAwait(false);
                results.Add(result);
                if (itemCompleted is not null)
                    await itemCompleted(result).ConfigureAwait(false);
                var summary = Summarize(plan.Items.Count, results);
                progress?.Report((results.Count * 100d / plan.Items.Count, $"Item {item.Sequence + 1}", summary));
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var item in plan.Items.Where(item => results.All(result => result.Sequence != item.Sequence)))
                results.Add(new(item.Sequence, BatchCompressionItemState.Cancelled, item.SourcePath, null, 0,
                    ErrorCodeCatalog.CancelledByUser, "Compression cancelled."));
            var cancelled = results.OrderBy(item => item.Sequence).ToArray();
            var cancelledSummary = Summarize(plan.Items.Count, cancelled);
            await WriteAuditAsync("Cancelled", taskId, request.ProjectId, ErrorCodeCatalog.CancelledByUser,
                CancellationToken.None).ConfigureAwait(false);
            return new(taskId, ResolveState(cancelledSummary), cancelledSummary, cancelled);
        }

        var finalSummary = Summarize(plan.Items.Count, results);
        var state = ResolveState(finalSummary);
        await WriteAuditAsync(state.ToString(), taskId, request.ProjectId,
            results.FirstOrDefault(item => item.ErrorCode is not null)?.ErrorCode, CancellationToken.None).ConfigureAwait(false);
        if (state is TaskLifecycleState.NeedsAttention or TaskLifecycleState.PartiallyCompleted or TaskLifecycleState.Failed)
            await NotifyAttentionAsync(taskId, request.ProjectId, CancellationToken.None).ConfigureAwait(false);
        return new(taskId, state, finalSummary, results.OrderBy(item => item.Sequence).ToArray());
    }

    private FileOperationPlan CreatePlan(
        Guid taskId,
        Guid? projectId,
        IReadOnlyList<string> sources,
        IReadOnlyList<int> sequences,
        string destinationRoot)
    {
        var sourceRoot = Path.GetDirectoryName(sources[0]) ?? Path.GetPathRoot(sources[0])!;
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<FileOperationItem>(sources.Count);
        long estimatedBytes = 0;
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var sourceInfo = new FileInfo(source);
            var desired = Path.Combine(destinationRoot, Path.GetFileNameWithoutExtension(source) + ".jpg");
            var destination = conflictResolver.ResolveDestination(desired, FileConflictPolicy.AutoNumber, reserved);
            reserved.Add(destination);
            items.Add(new(Guid.NewGuid(), sequences[index], source, destination, FileOperationType.Copy,
                FileConflictPolicy.AutoNumber, sourceInfo.Exists ? sourceInfo.Length : null,
                sourceInfo.Exists ? sourceInfo.LastWriteTimeUtc : null));
            if (sourceInfo.Exists) estimatedBytes += sourceInfo.Length;
        }

        return new(1, Guid.NewGuid(), taskId, projectId, FileOperationType.Copy, sourceRoot,
            destinationRoot, FileConflictPolicy.AutoNumber, items, estimatedBytes,
            FileOperationRiskLevel.Low, DateTimeOffset.UtcNow);
    }

    private async Task<BatchCompressionItemResult> CompressItemAsync(
        Guid taskId,
        Guid? projectId,
        FileOperationItem item,
        BatchCompressionOptions options,
        CancellationToken cancellationToken)
    {
        FileOperationItemResult? ownedCopy = null;
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "PixelTartBatchCompression", taskId.ToString("N"));
        var temporaryPath = Path.Combine(temporaryRoot, Guid.NewGuid().ToString("N") + ".jpg");
        try
        {
            var sourceInfo = new FileInfo(item.SourcePath);
            if (!sourceInfo.Exists) throw new FileNotFoundException("The source image is unavailable.");
            var originalLength = sourceInfo.Length;
            var originalModified = sourceInfo.LastWriteTimeUtc;

            Directory.CreateDirectory(temporaryRoot);
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                262144, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                await encoder.EncodeAsync(item.SourcePath, output, options, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(true);
            }

            ValidateJpeg(temporaryPath);
            await encoder.VerifyDecodableAsync(temporaryPath, cancellationToken).ConfigureAwait(false);
            sourceInfo.Refresh();
            if (!sourceInfo.Exists || sourceInfo.Length != originalLength || sourceInfo.LastWriteTimeUtc != originalModified)
                throw new IOException(ErrorCodeCatalog.SourceChanged);

            var temporaryInfo = new FileInfo(temporaryPath);
            var temporaryHash = await verification.ComputeSha256Async(temporaryPath, cancellationToken).ConfigureAwait(false);
            var copyItem = new FileOperationItem(Guid.NewGuid(), item.Sequence, temporaryPath, item.DestinationPath,
                FileOperationType.Copy, FileConflictPolicy.AutoNumber, temporaryInfo.Length,
                temporaryInfo.LastWriteTimeUtc, temporaryHash);
            var copyPlan = new FileOperationPlan(1, Guid.NewGuid(), taskId, projectId, FileOperationType.Copy,
                temporaryRoot, Path.GetDirectoryName(item.DestinationPath)!, FileConflictPolicy.AutoNumber,
                [copyItem], temporaryInfo.Length, FileOperationRiskLevel.Low, DateTimeOffset.UtcNow);
            var execution = await operationExecutor.ExecuteAsync(copyPlan, cancellationToken: cancellationToken).ConfigureAwait(false);
            ownedCopy = execution.Items.SingleOrDefault(result =>
                result.ItemId == copyItem.Id && result.State == FileOperationItemState.Completed);
            if (ownedCopy is null || string.IsNullOrWhiteSpace(ownedCopy.DestinationPath))
            {
                var failed = execution.Items.FirstOrDefault(result => result.ItemId == copyItem.Id)
                    ?? execution.Items.FirstOrDefault();
                return new(item.Sequence,
                    failed?.State == FileOperationItemState.NeedsAttention
                        ? BatchCompressionItemState.NeedsAttention
                        : BatchCompressionItemState.Failed,
                    item.SourcePath, null, 0, failed?.ErrorCode ?? ErrorCodeCatalog.DestinationNotWritable,
                    "The compression output could not be committed safely.");
            }

            ValidateJpeg(ownedCopy.DestinationPath);
            await encoder.VerifyDecodableAsync(ownedCopy.DestinationPath, CancellationToken.None).ConfigureAwait(false);
            var outputInfo = new FileInfo(ownedCopy.DestinationPath);
            return new(item.Sequence, BatchCompressionItemState.Completed, item.SourcePath,
                ownedCopy.DestinationPath, outputInfo.Length, null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var code = MapError(exception);
            if (ownedCopy is not null && !string.IsNullOrWhiteSpace(ownedCopy.DestinationPath))
            {
                var outputInfo = new FileInfo(ownedCopy.DestinationPath);
                return new(item.Sequence, BatchCompressionItemState.PartiallyCompleted, item.SourcePath,
                    ownedCopy.DestinationPath, outputInfo.Exists ? outputInfo.Length : 0, code,
                    "The committed output needs attention.");
            }

            var state = exception is UnauthorizedAccessException ||
                        code is ErrorCodeCatalog.FileLocked or ErrorCodeCatalog.DestinationNotWritable or ErrorCodeCatalog.SourceChanged
                ? BatchCompressionItemState.NeedsAttention
                : BatchCompressionItemState.Failed;
            return new(item.Sequence, state, item.SourcePath, null, 0, code,
                state == BatchCompressionItemState.NeedsAttention
                    ? "The compression output needs attention."
                    : "Compression failed.");
        }
        finally
        {
            TryDeleteOwnedOutput(temporaryPath);
            TryDeleteEmptyDirectory(temporaryRoot);
        }
    }

    private static void ValidateJpeg(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 4) throw new InvalidDataException("The JPEG output is empty.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
            throw new InvalidDataException("The output does not have a JPEG signature.");
        stream.Seek(-2, SeekOrigin.End);
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD9)
            throw new InvalidDataException("The JPEG output was not fully written.");
    }

    private static BatchCompressionItemResult Failure(FileOperationItem item, FileOperationValidationIssue issue) =>
        new(item.Sequence, issue.RequiresAttention ? BatchCompressionItemState.NeedsAttention : BatchCompressionItemState.Failed,
            item.SourcePath, null, 0, issue.ErrorCode, issue.Message);

    private static TaskResultSummary Summarize(int total, IReadOnlyCollection<BatchCompressionItemResult> results) =>
        new(total, results.Count(item => item.State == BatchCompressionItemState.Completed),
            results.Count(item => item.State == BatchCompressionItemState.Failed), 0,
            results.Count(item => item.State == BatchCompressionItemState.Cancelled),
            results.Count(item => item.State is BatchCompressionItemState.NeedsAttention or BatchCompressionItemState.PartiallyCompleted),
            0, results.Sum(item => item.BytesWritten));

    private static TaskLifecycleState ResolveState(TaskResultSummary summary)
    {
        if (summary.Succeeded == summary.Total) return TaskLifecycleState.Completed;
        if (summary.Succeeded > 0) return TaskLifecycleState.PartiallyCompleted;
        if (summary.WaitingForAttention > 0) return TaskLifecycleState.NeedsAttention;
        if (summary.Cancelled > 0 && summary.Failed == 0) return TaskLifecycleState.Cancelled;
        return TaskLifecycleState.Failed;
    }

    private async Task WriteAuditAsync(
        string result,
        Guid taskId,
        Guid? projectId,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync("BatchCompression", "Compression", result == "Completed" ? "Information" : "Warning",
                $"Result={result};SourceCountRedacted", taskId, projectId, errorCode, taskId.ToString("N"), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task NotifyAttentionAsync(Guid taskId, Guid? projectId, CancellationToken cancellationToken)
    {
        try
        {
            await notifications.PublishAsync(new(Guid.NewGuid(), NotificationType.TaskNotification,
                NotificationSeverity.Warning, "批量压缩需要处理", "源照片保持不变；请在任务中心查看失败项并重试。",
                taskId, projectId, [], false, DateTimeOffset.UtcNow,
                DeduplicationKey: $"batch-compression-{taskId:N}"), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static string MapError(Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => ErrorCodeCatalog.SourceNotFound,
        UnauthorizedAccessException => ErrorCodeCatalog.PermissionDenied,
        InvalidDataException => ErrorCodeCatalog.CorruptedImage,
        IOException io when io.Message.Contains(ErrorCodeCatalog.SourceChanged, StringComparison.Ordinal) => ErrorCodeCatalog.SourceChanged,
        IOException io when io.HResult == unchecked((int)0x80070020) => ErrorCodeCatalog.FileLocked,
        IOException => ErrorCodeCatalog.DestinationNotWritable,
        _ => ErrorCodeCatalog.DecodeFailed
    };

    private static void TryDeleteOwnedOutput(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
        }
        catch
        {
        }
    }
}
