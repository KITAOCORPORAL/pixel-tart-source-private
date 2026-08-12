using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.FileOperations;

namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

public sealed class RawToJpegSafeConversionService(
    IRawDecoder decoder,
    IRawJpegEncoder encoder,
    IFileConflictResolver conflictResolver,
    IFileOperationValidator validator,
    IFileVerificationService verification,
    IUndoJournalRepository undoJournal,
    IAuditLogService auditLog,
    INotificationCenter notifications,
    IFileOperationExecutor? operationExecutor = null) : IRawToJpegSafeConversionService
{
    public async Task<RawToJpegBatchResult> ConvertAsync(Guid taskId, RawToJpegBatchRequest request,
        IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
        Func<RawToJpegItemResult, Task>? itemCompleted = null,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        var sourceFiles = request.SourceFiles.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var destinationRoot = Path.GetFullPath(request.DestinationRoot);
        var sequences = request.SourceSequences?.ToArray() ?? Enumerable.Range(0, sourceFiles.Length).ToArray();
        var plan = CreatePlan(taskId, request.ProjectId, sourceFiles, sequences, destinationRoot);
        var validation = await validator.ValidateAsync(plan, cancellationToken).ConfigureAwait(false);
        var results = new List<RawToJpegItemResult>();
        var planIssue = validation.Issues.FirstOrDefault(x => x.ItemId is null);
        if (planIssue is not null)
        {
            results.AddRange(plan.Items.Select(item => Failure(item, planIssue)));
            var rejected = Summarize(plan.Items.Count, results);
            await WriteAuditAsync("Rejected", taskId, request.ProjectId, planIssue.ErrorCode, cancellationToken).ConfigureAwait(false);
            await NotifyAttentionAsync(taskId, request.ProjectId, cancellationToken).ConfigureAwait(false);
            return new(taskId, ResolveState(rejected), rejected, results);
        }

        var itemIssues = validation.Issues.Where(x => x.ItemId is not null)
            .GroupBy(x => x.ItemId!.Value).ToDictionary(x => x.Key, x => x.First());
        try
        {
            foreach (var item in plan.Items.OrderBy(x => x.Sequence))
            {
                cancellationToken.ThrowIfCancellationRequested();
                RawToJpegItemResult result;
                if (itemIssues.TryGetValue(item.Id, out var issue))
                    result = Failure(item, issue);
                else
                    result = await ConvertItemAsync(taskId, item, request.Options, cancellationToken).ConfigureAwait(false);
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
                results.Add(new(item.Sequence, RawToJpegItemState.Cancelled, item.SourcePath, null, 0, null,
                    ErrorCodeCatalog.CancelledByUser, "Conversion cancelled."));
            var cancelledSummary = Summarize(plan.Items.Count, results);
            return new(taskId, ResolveState(cancelledSummary), cancelledSummary, results.OrderBy(x => x.Sequence).ToArray());
        }

        var finalSummary = Summarize(plan.Items.Count, results);
        var state = ResolveState(finalSummary);
        await WriteAuditAsync(state.ToString(), taskId, request.ProjectId,
            results.FirstOrDefault(x => x.ErrorCode is not null)?.ErrorCode, CancellationToken.None).ConfigureAwait(false);
        if (state is TaskLifecycleState.NeedsAttention or TaskLifecycleState.PartiallyCompleted or TaskLifecycleState.Failed)
            await NotifyAttentionAsync(taskId, request.ProjectId, CancellationToken.None).ConfigureAwait(false);
        return new(taskId, state, finalSummary, results);
    }

    private FileOperationPlan CreatePlan(Guid taskId, Guid? projectId, IReadOnlyList<string> sources,
        IReadOnlyList<int> sequences, string destinationRoot)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<FileOperationItem>(sources.Count);
        long estimatedBytes = 0;
        for (var sequence = 0; sequence < sources.Count; sequence++)
        {
            var source = sources[sequence];
            var sourceInfo = new FileInfo(source);
            var desired = Path.Combine(destinationRoot, Path.GetFileNameWithoutExtension(source) + ".jpg");
            var destination = conflictResolver.ResolveDestination(desired, FileConflictPolicy.AutoNumber, reserved);
            reserved.Add(destination);
            items.Add(new(Guid.NewGuid(), sequences[sequence], source, destination, FileOperationType.Copy, FileConflictPolicy.AutoNumber,
                sourceInfo.Exists ? sourceInfo.Length : null, sourceInfo.Exists ? sourceInfo.LastWriteTimeUtc : null));
            if (sourceInfo.Exists) estimatedBytes += sourceInfo.Length;
        }

        var sourceRoot = Path.GetDirectoryName(sources[0]) ?? Path.GetPathRoot(sources[0])!;
        return new(1, Guid.NewGuid(), taskId, projectId, FileOperationType.Copy, sourceRoot, destinationRoot,
            FileConflictPolicy.AutoNumber, items, estimatedBytes, FileOperationRiskLevel.Low, DateTimeOffset.UtcNow,
            AllowSourceAndDestinationRootSame: true);
    }

    private async Task<RawToJpegItemResult> ConvertItemAsync(Guid taskId, FileOperationItem item, RawToJpegOptions options, CancellationToken cancellationToken)
    {
        var created = false;
        var committed = false;
        var journalWrittenByExecutor = false;
        FileOperationItemResult? ownedCopy = null;
        var outputPath = item.DestinationPath;
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "PixelTartRawToJpeg", taskId.ToString("N"));
        var temporaryPath = Path.Combine(temporaryRoot, Guid.NewGuid().ToString("N") + ".jpg");
        var encodePath = operationExecutor is null ? item.DestinationPath : temporaryPath;
        var stage = MediaTaskStages.FileOpen;
        try
        {
            var sourceInfo = new FileInfo(item.SourcePath);
            if (!sourceInfo.Exists) throw new FileNotFoundException("The RAW source is unavailable.");
            var originalLength = sourceInfo.Length;
            var originalModified = sourceInfo.LastWriteTimeUtc;
            stage = MediaTaskStages.RawDecode;
            var decoded = await decoder.DecodeAsync(item.SourcePath, options, cancellationToken).ConfigureAwait(false);
            ValidateDecoded(decoded);

            stage = MediaTaskStages.TemporaryWrite;
            Directory.CreateDirectory(Path.GetDirectoryName(encodePath)!);
            await using (var output = new FileStream(encodePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                262144, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                created = true;
                stage = MediaTaskStages.JpegEncode;
                await encoder.EncodeAsync(decoded, output, options, cancellationToken).ConfigureAwait(false);
                stage = MediaTaskStages.TemporaryWrite;
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(true);
            }

            stage = MediaTaskStages.OutputVerification;
            ValidateJpeg(encodePath);
            stage = MediaTaskStages.SourceVerification;
            sourceInfo.Refresh();
            if (!sourceInfo.Exists || sourceInfo.Length != originalLength || sourceInfo.LastWriteTimeUtc != originalModified)
                throw new RawDecodeException(ErrorCodeCatalog.SourceChanged, "The RAW source changed during conversion.");

            if (operationExecutor is not null)
            {
                stage = MediaTaskStages.OutputCommit;
                var temporaryInfo = new FileInfo(temporaryPath);
                var copyItem = new FileOperationItem(Guid.NewGuid(), item.Sequence, temporaryPath, item.DestinationPath,
                    FileOperationType.Copy, FileConflictPolicy.AutoNumber, temporaryInfo.Length, temporaryInfo.LastWriteTimeUtc);
                var copyPlan = new FileOperationPlan(1, Guid.NewGuid(), taskId, null, FileOperationType.Copy,
                    temporaryRoot, Path.GetDirectoryName(item.DestinationPath)!, FileConflictPolicy.AutoNumber,
                    [copyItem], temporaryInfo.Length, FileOperationRiskLevel.Low, DateTimeOffset.UtcNow);
                var execution = await operationExecutor.ExecuteAsync(copyPlan, cancellationToken: cancellationToken).ConfigureAwait(false);
                var copied = execution.Items.SingleOrDefault(x => x.ItemId == copyItem.Id && x.State == FileOperationItemState.Completed);
                if (copied is null || string.IsNullOrWhiteSpace(copied.DestinationPath))
                    throw new IOException(ErrorCodeCatalog.DestinationNotWritable);
                ownedCopy = copied;
                outputPath = copied.DestinationPath;
                journalWrittenByExecutor = true;
                stage = MediaTaskStages.OutputVerification;
                ValidateJpeg(outputPath);
            }

            stage = MediaTaskStages.OutputVerification;
            var outputInfo = new FileInfo(outputPath);
            var hash = options.VerifySha256
                ? ownedCopy?.Hash ?? await verification.ComputeSha256Async(outputPath,
                    journalWrittenByExecutor ? CancellationToken.None : cancellationToken).ConfigureAwait(false)
                : ownedCopy?.Hash;
            committed = true;
            if (!journalWrittenByExecutor) try
            {
                await undoJournal.AppendAsync(new(Guid.NewGuid(), taskId, item.Sequence, FileOperationType.DeleteCreatedOutput,
                    item.SourcePath, outputPath, outputInfo.Length, hash,
                    "output created by RAW conversion and unchanged", UndoJournalState.Pending, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                return new(item.Sequence, RawToJpegItemState.PartiallyCompleted, item.SourcePath, outputPath,
                    outputInfo.Length, hash, ErrorCodeCatalog.DatabaseUnavailable, SafeMessage(ex),
                    CreateFailure(item.SourcePath, MediaTaskStages.TaskPersistence, ErrorCodeCatalog.DatabaseUnavailable, ex, true));
            }
            return new(item.Sequence, RawToJpegItemState.Completed, item.SourcePath, outputPath,
                outputInfo.Length, hash, null, null);
        }
        catch (OperationCanceledException)
        {
            if (created && !committed && operationExecutor is null) TryDeleteOwnedOutput(outputPath);
            throw;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (created && !committed && operationExecutor is null) TryDeleteOwnedOutput(outputPath);
            var code = MapError(ex);
            var failure = CreateFailure(item.SourcePath, stage, code, ex,
                ownedCopy is not null && !string.IsNullOrWhiteSpace(ownedCopy.DestinationPath));
            if (ownedCopy is not null && !string.IsNullOrWhiteSpace(ownedCopy.DestinationPath))
            {
                outputPath = ownedCopy.DestinationPath;
                var outputInfo = new FileInfo(outputPath);
                string? outputHash = null;
                try
                {
                    ValidateJpeg(outputPath);
                    outputHash = ownedCopy.Hash ?? (options.VerifySha256
                        ? await verification.ComputeSha256Async(outputPath, CancellationToken.None).ConfigureAwait(false)
                        : null);
                }
                catch { }
                return new(item.Sequence, RawToJpegItemState.PartiallyCompleted, item.SourcePath, outputPath,
                    outputInfo.Length, outputHash, code, SafeMessage(ex), failure);
            }
            var state = ex is UnauthorizedAccessException || code is ErrorCodeCatalog.FileLocked or ErrorCodeCatalog.DestinationNotWritable
                ? RawToJpegItemState.NeedsAttention : RawToJpegItemState.Failed;
            return new(item.Sequence, state, item.SourcePath, null, 0, null, code, SafeMessage(ex), failure);
        }
        finally { TryDeleteOwnedOutput(temporaryPath); }
    }

    private static void ValidateDecoded(RawDecodedImage image)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Stride < image.Width * 3 || image.Rgb24Pixels.Length < image.RequiredByteCount)
            throw new RawDecodeException(ErrorCodeCatalog.CorruptedImage, "The decoded image buffer is incomplete.");
        if (!string.Equals(image.Metadata.ColorSpace, "sRGB", StringComparison.OrdinalIgnoreCase))
            throw new RawDecodeException(ErrorCodeCatalog.ColorProfileUnsupported, "Only sRGB output is accepted.");
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

    private static RawToJpegItemResult Failure(FileOperationItem item, FileOperationValidationIssue issue)
    {
        var failure = new MediaTaskFailureDetail(Path.GetFileName(item.SourcePath), MediaTaskStages.InputValidation,
            issue.ErrorCode, MediaTaskFailureMessages.UserMessage(MediaTaskStages.InputValidation, issue.ErrorCode),
            issue.Message, MediaTaskFailureMessages.Retryable(issue.ErrorCode), false);
        return new(item.Sequence, issue.RequiresAttention ? RawToJpegItemState.NeedsAttention : RawToJpegItemState.Failed,
            item.SourcePath, null, 0, null, issue.ErrorCode, issue.Message, failure);
    }

    private static MediaTaskFailureDetail CreateFailure(string sourcePath, string stage, string errorCode,
        Exception exception, bool outputOwned)
    {
        var technical = exception is Sdcb.LibRaw.LibRawException libRaw
            ? $"LibRawCode={libRaw.ErrorCode};LibRawMessage={libRaw.ErrorExplain};Exception={exception.Message}"
            : $"{exception.GetType().Name}: {exception.Message}";
        return new(Path.GetFileName(sourcePath), stage, errorCode,
            MediaTaskFailureMessages.UserMessage(stage, errorCode), MediaTaskFailurePayload.SanitizeTechnical(technical),
            MediaTaskFailureMessages.Retryable(errorCode), outputOwned);
    }

    private static TaskResultSummary Summarize(int total, IReadOnlyCollection<RawToJpegItemResult> results) => new(total,
        results.Count(x => x.State == RawToJpegItemState.Completed),
        results.Count(x => x.State == RawToJpegItemState.Failed), 0,
        results.Count(x => x.State == RawToJpegItemState.Cancelled),
        results.Count(x => x.State is RawToJpegItemState.NeedsAttention or RawToJpegItemState.PartiallyCompleted),
        0, results.Sum(x => x.BytesWritten));

    private static TaskLifecycleState ResolveState(TaskResultSummary summary)
    {
        if (summary.Succeeded == summary.Total) return TaskLifecycleState.Completed;
        if (summary.Succeeded > 0) return TaskLifecycleState.PartiallyCompleted;
        if (summary.WaitingForAttention > 0) return TaskLifecycleState.NeedsAttention;
        if (summary.Cancelled > 0 && summary.Failed == 0) return TaskLifecycleState.Cancelled;
        return TaskLifecycleState.Failed;
    }

    private async Task WriteAuditAsync(string result, Guid taskId, Guid? projectId, string? code, CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync("RawToJpeg", "BatchConversion", result == "Completed" ? "Information" : "Warning",
                $"Result={result};SourceCountRedacted", taskId, projectId, code, taskId.ToString("N"), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
    }

    private async Task NotifyAttentionAsync(Guid taskId, Guid? projectId, CancellationToken cancellationToken)
    {
        try
        {
            await notifications.PublishAsync(new(Guid.NewGuid(), NotificationType.TaskNotification, NotificationSeverity.Warning,
                "RAW 转 JPG 需要处理", "源 RAW 保持不变；请在任务中心查看失败项并重试。", taskId, projectId, [], false,
                DateTimeOffset.UtcNow, DeduplicationKey: $"raw-jpeg-{taskId:N}"), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
    }

    private static string MapError(Exception ex) => ex switch
    {
        RawDecodeException raw => raw.ErrorCode,
        FileNotFoundException or DirectoryNotFoundException => ErrorCodeCatalog.SourceNotFound,
        UnauthorizedAccessException => ErrorCodeCatalog.PermissionDenied,
        InvalidDataException => ErrorCodeCatalog.CorruptedImage,
        IOException io when io.HResult == unchecked((int)0x80070020) => ErrorCodeCatalog.FileLocked,
        IOException => ErrorCodeCatalog.DestinationNotWritable,
        _ => ErrorCodeCatalog.DecodeFailed
    };

    private static string SafeMessage(Exception ex) => ex switch
    {
        RawDecodeException => "RAW decode failed.",
        UnauthorizedAccessException => "Access denied.",
        IOException => "File output failed.",
        _ => "Conversion failed."
    };

    private static void TryDeleteOwnedOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
