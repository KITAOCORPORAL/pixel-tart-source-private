using System.Runtime.Versioning;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.BatchCompression;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class RawBatchTaskRecoveryClosureTests
{
    [TestMethod]
    public async Task RawCancel_PersistsCommittedItemAndRetryDoesNotDuplicateOutput()
    {
        using var temp = new TempDirectory("RawCancelRecovery-" + Guid.NewGuid().ToString("N"));
        var database = await CreateDatabaseAsync(temp);
        var repository = new SqliteTaskRepository(database);
        var store = new RawToJpegRequestStore(temp.Combine("recovery"));
        var service = new DeterministicRawService(gateAfterFirstItem: true);
        var engine = CreateEngine(repository, new RawToJpegTaskHandler(store, service));
        var coordinator = new RawToJpegTaskCoordinator(engine, store, new FakeRawDecoder());
        var first = temp.CreateFile("source/first.ARW", [1]);
        var second = temp.CreateFile("source/second.ARW", [2]);
        var output = temp.Combine("output");

        var taskId = await coordinator.StartAsync(new([first, second], output, new()));
        await service.FirstItemCheckpointPersisted.WaitAsync(TimeSpan.FromSeconds(10));
        await coordinator.CancelAsync(taskId);
        await coordinator.WaitForCompletionAsync(taskId).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsTrue(new RawToJpegRequestStore(temp.Combine("recovery")).TryGet(taskId, out var checkpoint));
        Assert.HasCount(1, checkpoint.StableResults);
        CollectionAssert.AreEqual(new[] { second }, checkpoint.PendingSourceFiles.ToArray());
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        Assert.IsTrue((await repository.GetAsync(taskId))!.State is
            TaskLifecycleState.Cancelled or TaskLifecycleState.PartiallyCompleted);

        var restartedStore = new RawToJpegRequestStore(temp.Combine("recovery"));
        var restartedEngine = CreateEngine(repository, new RawToJpegTaskHandler(restartedStore, service));
        await restartedEngine.RetryAsync(taskId);
        await restartedEngine.WaitForCompletionAsync(taskId).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(TaskLifecycleState.Completed, (await repository.GetAsync(taskId))!.State);
        Assert.IsFalse(new RawToJpegRequestStore(temp.Combine("recovery")).TryGet(taskId, out _));
        Assert.HasCount(2, Directory.GetFiles(output, "*.jpg"));
        Assert.AreEqual(1, service.ProcessedSources.Count(path => PathEquals(path, first)));
        Assert.AreEqual(1, service.ProcessedSources.Count(path => PathEquals(path, second)));
        CollectionAssert.AreEqual(new byte[] { 1 }, await File.ReadAllBytesAsync(first));
        CollectionAssert.AreEqual(new byte[] { 2 }, await File.ReadAllBytesAsync(second));
        SqliteTestIsolation.ClearPool(database);
    }

    [TestMethod]
    public async Task BatchCancel_PersistsCommittedItemAndRetryDoesNotDuplicateOutput()
    {
        using var temp = new TempDirectory("BatchCancelRecovery-" + Guid.NewGuid().ToString("N"));
        var database = await CreateDatabaseAsync(temp);
        var repository = new SqliteTaskRepository(database);
        var store = new BatchCompressionRequestStore(temp.Combine("recovery"));
        var service = new DeterministicBatchService(gateAfterFirstItem: true);
        var engine = CreateEngine(repository, new BatchCompressionTaskHandler(store, service));
        var coordinator = new BatchCompressionTaskCoordinator(engine, store);
        var first = temp.CreateFile("source/first.jpg", [1]);
        var second = temp.CreateFile("source/second.jpg", [2]);
        var output = temp.Combine("output");

        var taskId = await coordinator.StartAsync(new([first, second], output, new()));
        await service.FirstItemCheckpointPersisted.WaitAsync(TimeSpan.FromSeconds(10));
        await coordinator.CancelAsync(taskId);
        await coordinator.WaitForCompletionAsync(taskId).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsTrue(new BatchCompressionRequestStore(temp.Combine("recovery")).TryGet(taskId, out var checkpoint));
        Assert.HasCount(1, checkpoint.StableResults);
        CollectionAssert.AreEqual(new[] { second }, checkpoint.PendingSourceFiles.ToArray());
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));

        var restartedStore = new BatchCompressionRequestStore(temp.Combine("recovery"));
        var restartedEngine = CreateEngine(repository, new BatchCompressionTaskHandler(restartedStore, service));
        await restartedEngine.RetryAsync(taskId);
        await restartedEngine.WaitForCompletionAsync(taskId).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(TaskLifecycleState.Completed, (await repository.GetAsync(taskId))!.State);
        Assert.IsFalse(new BatchCompressionRequestStore(temp.Combine("recovery")).TryGet(taskId, out _));
        Assert.HasCount(2, Directory.GetFiles(output, "*.jpg"));
        Assert.AreEqual(1, service.ProcessedSources.Count(path => PathEquals(path, first)));
        Assert.AreEqual(1, service.ProcessedSources.Count(path => PathEquals(path, second)));
        CollectionAssert.AreEqual(new byte[] { 1 }, await File.ReadAllBytesAsync(first));
        CollectionAssert.AreEqual(new byte[] { 2 }, await File.ReadAllBytesAsync(second));
        SqliteTestIsolation.ClearPool(database);
    }

    [TestMethod]
    public async Task RawTerminalPersistenceFailure_KeepsCheckpointAndInterruptedRecoveryUsesRawHandler()
    {
        using var temp = new TempDirectory("RawTerminalFailure-" + Guid.NewGuid().ToString("N"));
        var database = await CreateDatabaseAsync(temp);
        var durableRepository = new SqliteTaskRepository(database);
        var failingRepository = new TerminalStateFailingRepository(durableRepository);
        var store = new RawToJpegRequestStore(temp.Combine("recovery"));
        var service = new DeterministicRawService();
        var engine = CreateEngine(failingRepository, new RawToJpegTaskHandler(store, service));
        var source = temp.CreateFile("source/only.ARW", [7]);
        var output = temp.Combine("output");
        var taskId = await new RawToJpegTaskCoordinator(engine, store, new FakeRawDecoder())
            .StartAsync(new([source], output, new()));

        await Assert.ThrowsAsync<IOException>(() => engine.WaitForCompletionAsync(taskId));

        Assert.IsTrue(new RawToJpegRequestStore(temp.Combine("recovery")).TryGet(taskId, out var checkpoint));
        Assert.IsEmpty(checkpoint.PendingSourceFiles);
        Assert.HasCount(1, checkpoint.StableResults);
        Assert.AreEqual(TaskLifecycleState.Running, (await durableRepository.GetAsync(taskId))!.State);
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));

        await new TaskRecoveryService(durableRepository, NoopAudit.Instance).RecoverInterruptedAsync();
        Assert.AreEqual(TaskLifecycleState.Interrupted, (await durableRepository.GetAsync(taskId))!.State);
        var restartedStore = new RawToJpegRequestStore(temp.Combine("recovery"));
        var restartedEngine = CreateEngine(durableRepository, new RawToJpegTaskHandler(restartedStore, service));
        var recovery = CreateRecovery(database, durableRepository, restartedEngine);

        Assert.IsTrue(await recovery.ContinueAsync(taskId));
        Assert.AreEqual(TaskLifecycleState.Completed, (await durableRepository.GetAsync(taskId))!.State);
        Assert.AreEqual(1, service.ExecutionCount);
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        Assert.IsFalse(new RawToJpegRequestStore(temp.Combine("recovery")).TryGet(taskId, out _));
        CollectionAssert.AreEqual(new byte[] { 7 }, await File.ReadAllBytesAsync(source));
        SqliteTestIsolation.ClearPool(database);
    }

    [TestMethod]
    public async Task BatchTerminalPersistenceFailure_KeepsCheckpointAndInterruptedRecoveryUsesBatchHandler()
    {
        using var temp = new TempDirectory("BatchTerminalFailure-" + Guid.NewGuid().ToString("N"));
        var database = await CreateDatabaseAsync(temp);
        var durableRepository = new SqliteTaskRepository(database);
        var failingRepository = new TerminalStateFailingRepository(durableRepository);
        var store = new BatchCompressionRequestStore(temp.Combine("recovery"));
        var service = new DeterministicBatchService();
        var engine = CreateEngine(failingRepository, new BatchCompressionTaskHandler(store, service));
        var source = temp.CreateFile("source/only.jpg", [8]);
        var output = temp.Combine("output");
        var taskId = await new BatchCompressionTaskCoordinator(engine, store)
            .StartAsync(new([source], output, new()));

        await Assert.ThrowsAsync<IOException>(() => engine.WaitForCompletionAsync(taskId));

        Assert.IsTrue(new BatchCompressionRequestStore(temp.Combine("recovery")).TryGet(taskId, out var checkpoint));
        Assert.IsEmpty(checkpoint.PendingSourceFiles);
        Assert.HasCount(1, checkpoint.StableResults);
        Assert.AreEqual(TaskLifecycleState.Running, (await durableRepository.GetAsync(taskId))!.State);
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));

        await new TaskRecoveryService(durableRepository, NoopAudit.Instance).RecoverInterruptedAsync();
        Assert.AreEqual(TaskLifecycleState.Interrupted, (await durableRepository.GetAsync(taskId))!.State);
        var restartedStore = new BatchCompressionRequestStore(temp.Combine("recovery"));
        var restartedEngine = CreateEngine(durableRepository, new BatchCompressionTaskHandler(restartedStore, service));
        var recovery = CreateRecovery(database, durableRepository, restartedEngine);

        Assert.IsTrue(await recovery.ContinueAsync(taskId));
        Assert.AreEqual(TaskLifecycleState.Completed, (await durableRepository.GetAsync(taskId))!.State);
        Assert.AreEqual(1, service.ExecutionCount);
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        Assert.IsFalse(new BatchCompressionRequestStore(temp.Combine("recovery")).TryGet(taskId, out _));
        CollectionAssert.AreEqual(new byte[] { 8 }, await File.ReadAllBytesAsync(source));
        SqliteTestIsolation.ClearPool(database);
    }

    private static async Task<PixelTartDatabase> CreateDatabaseAsync(TempDirectory temp)
    {
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database,
            new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        return database;
    }

    private static TaskEngine CreateEngine(ITaskRepository repository, ITaskHandler handler) =>
        new(repository, new ConservativeTaskScheduler(), [handler], NoopAudit.Instance,
            NoopNotifications.Instance, TimeSpan.Zero);

    private static RecoveryCoordinator CreateRecovery(PixelTartDatabase database, ITaskRepository repository,
        ITaskEngine engine)
    {
        var verification = new FileVerificationService();
        var journal = new SqliteUndoJournalRepository(database);
        var executor = new FileOperationExecutor(new FileOperationValidator(), verification, journal, database);
        return new(database, repository, executor, new UndoJournalService(journal, verification), NoopAudit.Instance,
            engine);
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private sealed class DeterministicRawService(bool gateAfterFirstItem = false) : IRawToJpegSafeConversionService
    {
        private readonly TaskCompletionSource<bool> _firstItemCheckpointPersisted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseGate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _gateUsed;
        private int _executionCount;

        public Task FirstItemCheckpointPersisted => _firstItemCheckpointPersisted.Task;
        public int ExecutionCount => Volatile.Read(ref _executionCount);
        public List<string> ProcessedSources { get; } = [];

        public async Task<RawToJpegBatchResult> ConvertAsync(Guid taskId, RawToJpegBatchRequest request,
            IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
            Func<RawToJpegItemResult, Task>? itemCompleted = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executionCount);
            var results = new List<RawToJpegItemResult>();
            Directory.CreateDirectory(request.DestinationRoot);
            for (var index = 0; index < request.SourceFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = request.SourceFiles[index];
                ProcessedSources.Add(source);
                var sequence = request.SourceSequences?[index] ?? index;
                var destination = Path.Combine(request.DestinationRoot,
                    Path.GetFileNameWithoutExtension(source) + ".jpg");
                await using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
                                 FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, cancellationToken);
                    await stream.FlushAsync(CancellationToken.None);
                    stream.Flush(true);
                }
                var item = new RawToJpegItemResult(sequence, RawToJpegItemState.Completed, source, destination, 4,
                    null, null, null);
                results.Add(item);
                if (itemCompleted is not null) await itemCompleted(item);
                var summary = RawSummary(request.SourceFiles.Count, results);
                progress?.Report((results.Count * 100d / request.SourceFiles.Count, "ItemCommitted", summary));
                if (gateAfterFirstItem && Interlocked.CompareExchange(ref _gateUsed, 1, 0) == 0)
                {
                    _firstItemCheckpointPersisted.TrySetResult(true);
                    await _releaseGate.Task.WaitAsync(cancellationToken);
                }
            }
            var finalSummary = RawSummary(request.SourceFiles.Count, results);
            return new(taskId, TaskLifecycleState.Completed, finalSummary, results);
        }
    }

    private sealed class DeterministicBatchService(bool gateAfterFirstItem = false) : IBatchCompressionService
    {
        private readonly TaskCompletionSource<bool> _firstItemCheckpointPersisted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseGate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _gateUsed;
        private int _executionCount;

        public Task FirstItemCheckpointPersisted => _firstItemCheckpointPersisted.Task;
        public int ExecutionCount => Volatile.Read(ref _executionCount);
        public List<string> ProcessedSources { get; } = [];

        public async Task<BatchCompressionResult> CompressAsync(Guid taskId, BatchCompressionRequest request,
            IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
            Func<BatchCompressionItemResult, Task>? itemCompleted = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executionCount);
            var results = new List<BatchCompressionItemResult>();
            Directory.CreateDirectory(request.DestinationDirectory);
            for (var index = 0; index < request.SourceFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = request.SourceFiles[index];
                ProcessedSources.Add(source);
                var sequence = request.SourceSequences?[index] ?? index;
                var destination = Path.Combine(request.DestinationDirectory,
                    Path.GetFileNameWithoutExtension(source) + ".jpg");
                await using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
                                 FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, cancellationToken);
                    await stream.FlushAsync(CancellationToken.None);
                    stream.Flush(true);
                }
                var item = new BatchCompressionItemResult(sequence, BatchCompressionItemState.Completed, source,
                    destination, 4, null, null);
                results.Add(item);
                if (itemCompleted is not null) await itemCompleted(item);
                var summary = BatchSummary(request.SourceFiles.Count, results);
                progress?.Report((results.Count * 100d / request.SourceFiles.Count, "ItemCommitted", summary));
                if (gateAfterFirstItem && Interlocked.CompareExchange(ref _gateUsed, 1, 0) == 0)
                {
                    _firstItemCheckpointPersisted.TrySetResult(true);
                    await _releaseGate.Task.WaitAsync(cancellationToken);
                }
            }
            var finalSummary = BatchSummary(request.SourceFiles.Count, results);
            return new(taskId, TaskLifecycleState.Completed, finalSummary, results);
        }
    }

    private static TaskResultSummary RawSummary(int total, IReadOnlyCollection<RawToJpegItemResult> items) =>
        new(total, items.Count(item => item.State == RawToJpegItemState.Completed), 0, 0, 0, 0, 0,
            items.Sum(item => item.BytesWritten));

    private static TaskResultSummary BatchSummary(int total,
        IReadOnlyCollection<BatchCompressionItemResult> items) =>
        new(total, items.Count(item => item.State == BatchCompressionItemState.Completed), 0, 0, 0, 0, 0,
            items.Sum(item => item.BytesWritten));

    private sealed class TerminalStateFailingRepository(ITaskRepository inner) : ITaskRepository
    {
        public Task SaveAsync(TaskRuntimeState state, CancellationToken cancellationToken = default) =>
            TaskStateMachine.IsTerminal(state.State)
                ? Task.FromException(new IOException("simulated terminal persistence failure"))
                : inner.SaveAsync(state, cancellationToken);

        public Task<TaskRuntimeState?> GetAsync(Guid taskId, CancellationToken cancellationToken = default) =>
            inner.GetAsync(taskId, cancellationToken);

        public Task<IReadOnlyList<TaskRuntimeState>> ListAsync(int limit = 200,
            CancellationToken cancellationToken = default) => inner.ListAsync(limit, cancellationToken);

        public Task<IReadOnlyList<TaskRuntimeState>> ListUnfinishedAsync(
            CancellationToken cancellationToken = default) => inner.ListUnfinishedAsync(cancellationToken);

        public Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint,
            CancellationToken cancellationToken = default) =>
            inner.SaveCheckpointAsync(taskId, checkpoint, cancellationToken);
    }

    private sealed class FakeRawDecoder : IRawDecoder
    {
        public RawDecoderCapability GetCapability() => new(true, "test", "1", [".ARW"], [".ARW"]);
        public Task<RawDecodedImage> DecodeAsync(string sourcePath, RawToJpegOptions options,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The task-level test service owns decoding.");
    }

    private sealed class NoopAudit : IAuditLogService
    {
        public static NoopAudit Instance { get; } = new();
        public Task WriteAsync(string category, string eventType, string severity, string message,
            Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopNotifications : INotificationCenter
    {
        public static NoopNotifications Instance { get; } = new();
        public event EventHandler<NotificationMessage>? Published { add { } remove { } }
        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void NotifyPersisted(NotificationMessage message) { }
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>([]);
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
