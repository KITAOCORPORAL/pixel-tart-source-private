using System.Collections.Concurrent;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Core.Services.Publishing;

public interface IPublishingTaskCoordinator : ITaskCompletionStateProvider
{
    Task<Guid> StartAsync(PublishingExportRequest request, CancellationToken cancellationToken = default);
    Task WaitForCompletionAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid taskId, CancellationToken cancellationToken = default);
}

public sealed class PublishingRequestStore
{
    private readonly ConcurrentDictionary<Guid, PublishingExportRequest> _requests = [];
    public void Register(Guid taskId, PublishingExportRequest request) => _requests[taskId] = request;
    public bool TryGet(Guid taskId, out PublishingExportRequest request) => _requests.TryGetValue(taskId, out request!);
    public void Remove(Guid taskId) => _requests.TryRemove(taskId, out _);
}

public sealed class PublishingTaskHandler(PublishingRequestStore requests, IPublishingExportService publishing) : ITaskHandler, ITaskTerminalStateObserver
{
    public string TaskType => PublishingDefaults.TaskType;
    public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
    {
        if (!requests.TryGet(context.Definition.Id, out var request)) return new(TaskLifecycleState.Failed, TaskResultSummary.Empty, "PUBLISHING_REQUEST_MISSING", "发布任务资料不可用，请重新提交。");
        var progress = new Progress<(double Progress, string CurrentFile, TaskResultSummary Summary)>(value => _ = context.ReportProgressAsync(value.Progress, "发布导出", value.CurrentFile, value.Summary, CancellationToken.None));
        var result = await publishing.ExportAsync(context.Definition.Id, request, progress, cancellationToken).ConfigureAwait(false);
        var failure = result.Items.FirstOrDefault(item => item.State == PublishingItemState.Failed)?.ErrorMessage;
        return new(result.State, result.Summary, failure is null ? null : "PUBLISHING_ITEM_FAILED", failure);
    }
    public Task OnTerminalStatePersistedAsync(Guid taskId, TaskLifecycleState terminalState, CancellationToken cancellationToken = default) { requests.Remove(taskId); return Task.CompletedTask; }
}

public sealed class PublishingTaskCoordinator(ITaskEngine engine, PublishingRequestStore requests) : IPublishingTaskCoordinator
{
    public async Task<Guid> StartAsync(PublishingExportRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate(); var taskId = Guid.NewGuid(); requests.Register(taskId, request);
        try
        {
            await engine.EnqueueAsync(new(taskId, request.ProjectId, PublishingDefaults.TaskType, "发布导出", $"SourceCount={request.SourceFiles.Count};PathsRedacted", null, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
            return taskId;
        }
        catch { requests.Remove(taskId); throw; }
    }
    public Task WaitForCompletionAsync(Guid taskId, CancellationToken cancellationToken = default) => engine.WaitForCompletionAsync(taskId, cancellationToken);
    public Task CancelAsync(Guid taskId, CancellationToken cancellationToken = default) => engine.CancelAsync(taskId, cancellationToken);
    public async Task<TaskRuntimeState?> GetTaskStateAsync(Guid taskId, CancellationToken cancellationToken = default) => (await engine.LoadHistoryAsync(200, cancellationToken).ConfigureAwait(false)).FirstOrDefault(item => item.Definition.Id == taskId);
}
