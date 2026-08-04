using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tasks;

public sealed class TaskRecoveryService(ITaskRepository repository, IAuditLogService auditLog) : ITaskRecoveryService
{
    public async Task<IReadOnlyList<TaskRuntimeState>> RecoverInterruptedAsync(CancellationToken cancellationToken = default)
    {
        var unfinished = await repository.ListUnfinishedAsync(cancellationToken).ConfigureAwait(false);
        foreach (var runtime in unfinished)
        {
            if (!TaskStateMachine.IsUnexpectedActive(runtime.State)) continue;
            runtime.State = TaskLifecycleState.Interrupted;
            runtime.LastErrorCode = ErrorCodeCatalog.InterruptedByShutdown;
            runtime.LastErrorMessage = "应用上次运行意外结束。已保留检查点，等待用户选择继续、重试或放弃。";
            runtime.LastUpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveAsync(runtime, cancellationToken).ConfigureAwait(false);
            await auditLog.WriteAsync("Task", "CrashRecovery", "Warning", runtime.LastErrorMessage, runtime.Definition.Id, runtime.Definition.ProjectId, runtime.LastErrorCode, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        return unfinished.Where(x => x.State == TaskLifecycleState.Interrupted).ToArray();
    }
}

