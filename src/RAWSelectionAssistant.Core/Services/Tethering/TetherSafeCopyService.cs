using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Core.Services.Tethering;

public sealed class TetherSafeCopyService(
    ITetherAssetRepository repository,
    IFileOperationPlanner planner,
    IFileOperationExecutor executor,
    IFileVerificationService verification,
    TaskOperationBridge operationBridge,
    IAuditLogService auditLog,
    INotificationCenter notifications) : ICameraTransferService
{
    public Task<TetherCopyResult> CopyToProjectAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default) =>
        CopyAsync(asset, destinationRoot, verifySha256, projectCopy: true, cancellationToken);

    public Task<TetherCopyResult> CopyToBackupAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default) =>
        CopyAsync(asset, destinationRoot, verifySha256, projectCopy: false, cancellationToken);

    private async Task<TetherCopyResult> CopyAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, bool projectCopy, CancellationToken cancellationToken)
    {
        if (asset.StabilityState != TetherStabilityState.Stable || !File.Exists(asset.SourcePath))
            return new(asset.Id, null, null, TetherProcessingState.NeedsAttention, ErrorCodeCatalog.SourceNotFound);
        if (string.IsNullOrWhiteSpace(destinationRoot) || !Path.IsPathFullyQualified(destinationRoot))
            return new(asset.Id, null, null, TetherProcessingState.NeedsAttention, ErrorCodeCatalog.DestinationNotWritable);

        TetherCopyResult? result = null;
        Guid? taskId = null;
        try
        {
            taskId = await operationBridge.RunAsync(projectCopy ? "联机文件复制到项目" : "联机文件独立备份", async (context, token) =>
            {
                var sourceRoot = Path.GetDirectoryName(asset.SourcePath) ?? Path.GetPathRoot(asset.SourcePath)!;
                var plan = await planner.CreateAsync(context.Definition.Id, asset.ProjectId, FileOperationType.Copy, sourceRoot, Path.GetFullPath(destinationRoot), [asset.SourcePath], FileConflictPolicy.AutoNumber, token).ConfigureAwait(false);
                if (verifySha256)
                {
                    var item = plan.Items[0];
                    var hash = await verification.ComputeSha256Async(item.SourcePath, token).ConfigureAwait(false);
                    plan = plan with { Items = [item with { OptionalSourceHash = hash }] };
                }

                var progress = new AwaitableProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>(value =>
                    context.ReportProgressAsync(value.Progress, projectCopy ? "复制到项目资料目录" : "复制到独立备份目录", null, value.Summary, token));
                FileOperationExecutionResult execution;
                try { execution = await executor.ExecuteAsync(plan, context.SafeBoundaryAsync, progress, token).ConfigureAwait(false); }
                finally { await progress.DrainAsync().ConfigureAwait(false); }

                var completed = execution.Items.SingleOrDefault(item => item.State == FileOperationItemState.Completed);
                if (completed is null || string.IsNullOrWhiteSpace(completed.DestinationPath))
                {
                    var failed = execution.Items.FirstOrDefault();
                    result = new(asset.Id, context.Definition.Id, failed?.DestinationPath, TetherProcessingState.NeedsAttention, failed?.ErrorCode ?? ErrorCodeCatalog.DestinationNotWritable);
                    await WriteAuditAsync(projectCopy ? "ProjectCopy" : "BackupCopy", "NeedsAttention", context.Definition.Id, asset.ProjectId, result.ErrorCode, token).ConfigureAwait(false);
                    return execution.Summary.WaitingForAttention > 0 ? execution.Summary : execution.Summary with { WaitingForAttention = 1 };
                }

                try
                {
                    var latest = await repository.GetAsync(asset.Id, token).ConfigureAwait(false) ?? asset;
                    latest = projectCopy
                        ? latest with { ProjectCopyTaskId = context.Definition.Id, ProjectCopyPath = completed.DestinationPath, ProcessingState = TetherProcessingState.Copied, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow }
                        : latest with { BackupCopyTaskId = context.Definition.Id, BackupCopyPath = completed.DestinationPath, ProcessingState = TetherProcessingState.Copied, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow };
                    await repository.UpdateAsync(latest, token).ConfigureAwait(false);
                    result = new(asset.Id, context.Definition.Id, completed.DestinationPath, TetherProcessingState.Copied);
                    await WriteAuditAsync(projectCopy ? "ProjectCopy" : "BackupCopy", "Succeeded", context.Definition.Id, asset.ProjectId, null, token).ConfigureAwait(false);
                    return execution.Summary;
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    result = new(asset.Id, context.Definition.Id, completed.DestinationPath, TetherProcessingState.PartiallyCompleted, ErrorCodeCatalog.DatabaseUnavailable);
                    await WriteAuditAsync(projectCopy ? "ProjectCopy" : "BackupCopy", "PartiallyCompleted", context.Definition.Id, asset.ProjectId, ErrorCodeCatalog.DatabaseUnavailable, token).ConfigureAwait(false);
                    return new(1, 1, 0, 0, 0, 1, completed.BytesWritten, completed.BytesWritten);
                }
            }, asset.ProjectId, projectCopy ? "tether-project-copy" : "tether-backup-copy", cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            result = new(asset.Id, taskId, null, TetherProcessingState.NeedsAttention, ErrorCodeCatalog.DestinationNotWritable);
        }

        result ??= new(asset.Id, taskId, null, TetherProcessingState.NeedsAttention, ErrorCodeCatalog.DestinationNotWritable);
        if (result.State is TetherProcessingState.NeedsAttention or TetherProcessingState.PartiallyCompleted)
            await NotifyAttentionAsync(projectCopy, asset.ProjectId, result.TaskId, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task WriteAuditAsync(string operation, string result, Guid? taskId, Guid? projectId, string? errorCode, CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync("Tether", operation, result == "Succeeded" ? "Information" : "Warning",
                $"Operation={operation};Result={result}", taskId, projectId, errorCode, taskId?.ToString("N"), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
    }

    private async Task NotifyAttentionAsync(bool projectCopy, Guid? projectId, Guid? taskId, CancellationToken cancellationToken)
    {
        try
        {
            await notifications.PublishAsync(new(Guid.NewGuid(), NotificationType.TaskNotification, NotificationSeverity.Warning,
                projectCopy ? "项目复制需要处理" : "独立备份需要处理", "源文件保持不变，可在任务中心查看并重试。", taskId, projectId, [], false,
                DateTimeOffset.UtcNow, DeduplicationKey: $"tether-copy-{taskId:N}"), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
    }
}
