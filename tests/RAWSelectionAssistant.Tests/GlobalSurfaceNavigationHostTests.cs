using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class GlobalSurfaceNavigationHostTests
{
    [TestMethod]
    public void CloseCurrentSurface_ReturnsThroughOriginHistory()
    {
        var host = new SurfaceNavigationHost();

        Assert.AreEqual("Toolbox", host.Navigate("Toolbox"));
        Assert.AreEqual("RawToJpeg", host.Navigate("RawToJpeg"));
        Assert.AreEqual("Toolbox", host.OriginSurface);
        CollectionAssert.AreEqual(new[] { "Workbench", "Toolbox", "RawToJpeg" }, host.NavigationHistory.ToArray());

        Assert.AreEqual("Toolbox", host.CloseCurrentSurface());
        Assert.AreEqual("Workbench", host.CloseCurrentSurface());
        Assert.AreEqual("Workbench", host.CurrentSurface);
    }

    [TestMethod]
    public async Task CloseCurrentSurfaceAsync_HasSameOriginContract()
    {
        var host = new SurfaceNavigationHost();
        host.Navigate("WorkbenchQuickTools");
        host.Navigate("BatchCompress");

        Assert.AreEqual("WorkbenchQuickTools", await host.CloseCurrentSurfaceAsync());
        Assert.AreEqual("WorkbenchQuickTools", host.CurrentSurface);
    }

    [TestMethod]
    public void InvalidOrExpiredOrigin_FallsBackToWorkbench()
    {
        var valid = new HashSet<string>(StringComparer.Ordinal) { "Toolbox", "RawToJpeg" };
        var host = new SurfaceNavigationHost(isSurfaceValid: valid.Contains);
        host.Navigate("Toolbox");
        host.Navigate("RawToJpeg");
        valid.Remove("Toolbox");

        Assert.AreEqual("Workbench", host.ReturnToOrigin());
        CollectionAssert.AreEqual(new[] { "Workbench" }, host.NavigationHistory.ToArray());
    }

    [TestMethod]
    public void InvalidDestinationAndDuplicateNavigation_DoNotTrapTheShell()
    {
        var host = new SurfaceNavigationHost(isSurfaceValid: surface => surface is "Toolbox" or "Collage");

        host.Navigate("Toolbox");
        host.Navigate("Toolbox");
        CollectionAssert.AreEqual(new[] { "Workbench", "Toolbox" }, host.NavigationHistory.ToArray());

        Assert.AreEqual("Workbench", host.Navigate("DeletedProjectSurface"));
        Assert.AreEqual("Workbench", host.CurrentSurface);
        CollectionAssert.AreEqual(new[] { "Workbench" }, host.NavigationHistory.ToArray());
    }

    [TestMethod]
    public void FailureStateOwnedByModule_DoesNotParticipateInShellClose()
    {
        var moduleFailed = true;
        var moduleCancelCalls = 0;
        var host = new SurfaceNavigationHost();
        host.Navigate("OnlineSelection");

        var result = host.CloseCurrentSurface();

        Assert.IsTrue(moduleFailed);
        Assert.AreEqual(0, moduleCancelCalls);
        Assert.AreEqual("Workbench", result);
    }

    [TestMethod]
    public void ReturnToWorkbench_IsIdempotentAndClearsObsoleteHistory()
    {
        var host = new SurfaceNavigationHost();
        host.Navigate("Toolbox");
        host.Navigate("PhotoGrouping");

        Assert.AreEqual("Workbench", host.ReturnToWorkbench());
        Assert.AreEqual("Workbench", host.ReturnToWorkbench());
        CollectionAssert.AreEqual(new[] { "Workbench" }, host.NavigationHistory.ToArray());
    }

    [TestMethod]
    public async Task BusyTask_SurfaceCloseDoesNotCancelTask_AndTaskCenterSnapshotCompletes()
    {
        var handler = new BlockingHandler();
        var repository = new MemoryTaskRepository();
        var notifications = new RecordingNotificationCenter();
        var engine = new TaskEngine(repository, new ConservativeTaskScheduler(), [handler],
            new NullAuditLog(), notifications, TimeSpan.Zero);
        var host = new SurfaceNavigationHost();
        host.Navigate("RawToJpeg");
        var taskId = Guid.NewGuid();

        await engine.EnqueueAsync(new TaskDefinition(taskId, null, BlockingHandler.Type, "RAW 转 JPG", string.Empty,
            null, DateTimeOffset.UtcNow));
        await handler.Entered.Task;

        Assert.AreEqual("Workbench", host.CloseCurrentSurface());
        Assert.AreEqual(TaskLifecycleState.Running, engine.Current.Single(snapshot => snapshot.TaskId == taskId).State);
        Assert.IsFalse(handler.CancellationRequested);

        handler.Release.SetResult();
        await engine.WaitForCompletionAsync(taskId);

        Assert.AreEqual(TaskLifecycleState.Completed, engine.Current.Single(snapshot => snapshot.TaskId == taskId).State);
        Assert.IsFalse(handler.CancellationRequested);
        Assert.IsTrue(notifications.Messages.Any(message => message.TaskId == taskId));
    }

    private sealed class BlockingHandler : ITaskHandler
    {
        public const string Type = "global-surface-close-test";
        public string TaskType => Type;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CancellationRequested { get; private set; }

        public async Task<TaskExecutionResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => CancellationRequested = true);
            Entered.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return TaskExecutionResult.Completed(new TaskResultSummary(1, 1, 0, 0, 0, 0, 1, 1));
        }
    }

    private sealed class MemoryTaskRepository : ITaskRepository
    {
        private readonly Dictionary<Guid, TaskRuntimeState> _states = [];

        public Task SaveAsync(TaskRuntimeState state, CancellationToken cancellationToken = default)
        {
            _states[state.Definition.Id] = state;
            return Task.CompletedTask;
        }

        public Task<TaskRuntimeState?> GetAsync(Guid taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_states.GetValueOrDefault(taskId));

        public Task<IReadOnlyList<TaskRuntimeState>> ListAsync(int limit = 200, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaskRuntimeState>>(_states.Values.Take(limit).ToArray());

        public Task<IReadOnlyList<TaskRuntimeState>> ListUnfinishedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaskRuntimeState>>(_states.Values.Where(state => !TaskStateMachine.IsTerminal(state.State)).ToArray());

        public Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            if (_states.TryGetValue(taskId, out var state)) state.Checkpoint = checkpoint;
            return Task.CompletedTask;
        }
    }

    private sealed class NullAuditLog : IAuditLogService
    {
        public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null,
            Guid? projectId = null, string? errorCode = null, string? correlationId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingNotificationCenter : INotificationCenter
    {
        public event EventHandler<NotificationMessage>? Published;
        public List<NotificationMessage> Messages { get; } = [];

        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            Published?.Invoke(this, message);
            return Task.CompletedTask;
        }

        public void NotifyPersisted(NotificationMessage message) => Published?.Invoke(this, message);
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NotificationMessage>>(Messages.Take(limit).ToArray());
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
