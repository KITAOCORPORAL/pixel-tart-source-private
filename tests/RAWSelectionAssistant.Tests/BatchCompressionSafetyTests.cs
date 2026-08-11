using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.BatchCompression;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;
using System.Runtime.Versioning;

namespace RAWSelectionAssistant.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class BatchCompressionSafetyTests
{
    [TestMethod]
    public async Task Compression_UsesAutoNumberAndPreservesSourceAndExistingOutput()
    {
        using var setup = await SetupAsync(new RecordingEncoder());
        var source = setup.Temp.CreateFile("source/portrait.jpg", [1, 2, 3, 4]);
        var existing = setup.Temp.CreateFile("output/portrait.jpg", [9, 8, 7]);
        var sourceBytes = await File.ReadAllBytesAsync(source);
        var sourceModified = File.GetLastWriteTimeUtc(source);
        var taskId = Guid.NewGuid();

        var result = await setup.Service.CompressAsync(taskId,
            new([source], setup.Temp.Combine("output"), new()));

        Assert.AreEqual(TaskLifecycleState.Completed, result.State);
        var output = result.Items.Single().DestinationPath!;
        Assert.AreNotEqual(existing, output);
        StringAssert.Contains(Path.GetFileName(output), "(1)");
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(existing));
        CollectionAssert.AreEqual(sourceBytes, await File.ReadAllBytesAsync(source));
        Assert.AreEqual(sourceModified, File.GetLastWriteTimeUtc(source));
        Assert.IsTrue(File.Exists(output));
        Assert.HasCount(1, await setup.Undo.ListAsync(taskId));
        StringAssert.Contains(setup.Audit.Message!, "SourceCountRedacted");
        Assert.IsFalse(setup.Audit.Message!.Contains(source, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SourceAndDestinationSame_IsRejectedBeforeEncoderRuns()
    {
        var encoder = new RecordingEncoder();
        using var setup = await SetupAsync(encoder);
        var source = setup.Temp.CreateFile("same/image.jpg", [1, 2]);

        var result = await setup.Service.CompressAsync(Guid.NewGuid(),
            new([source], setup.Temp.Combine("same"), new()));

        Assert.AreEqual(TaskLifecycleState.Failed, result.State);
        Assert.AreEqual(0, encoder.CallCount);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(source));
    }

    [TestMethod]
    public async Task EncoderFailure_RemovesTemporaryFragmentAndKeepsSource()
    {
        using var setup = await SetupAsync(new FailingEncoder());
        var source = setup.Temp.CreateFile("source/failure.png", [3, 4, 5]);
        var outputDirectory = setup.Temp.Combine("output");
        var taskId = Guid.NewGuid();

        var result = await setup.Service.CompressAsync(taskId,
            new([source], outputDirectory, new()));

        Assert.AreEqual(TaskLifecycleState.NeedsAttention, result.State);
        Assert.IsTrue(File.Exists(source));
        Assert.IsFalse(Directory.Exists(outputDirectory) && Directory.GetFiles(outputDirectory).Length > 0);
        Assert.IsFalse(Directory.Exists(TemporaryTaskPath(taskId)) &&
                       Directory.EnumerateFileSystemEntries(TemporaryTaskPath(taskId)).Any());
        CollectionAssert.AreEqual(new byte[] { 3, 4, 5 }, await File.ReadAllBytesAsync(source));
        Assert.IsEmpty(await setup.Undo.ListAsync(taskId));
    }

    [TestMethod]
    public async Task UndecodableJpeg_IsRejectedBeforeFileOperationCommit()
    {
        using var setup = await SetupAsync(new UndecodableEncoder());
        var source = setup.Temp.CreateFile("source/invalid.png", [4, 5, 6]);
        var taskId = Guid.NewGuid();

        var result = await setup.Service.CompressAsync(taskId,
            new([source], setup.Temp.Combine("output"), new()));

        Assert.AreEqual(TaskLifecycleState.Failed, result.State);
        Assert.AreEqual(ErrorCodeCatalog.CorruptedImage, result.Items.Single().ErrorCode);
        Assert.IsFalse(Directory.Exists(setup.Temp.Combine("output")) &&
                       Directory.EnumerateFiles(setup.Temp.Combine("output")).Any());
        Assert.IsFalse(Directory.Exists(TemporaryTaskPath(taskId)) &&
                       Directory.EnumerateFileSystemEntries(TemporaryTaskPath(taskId)).Any());
        Assert.IsEmpty(await setup.Undo.ListAsync(taskId));
    }

    [TestMethod]
    public async Task Cancellation_ReturnsCompletedDetailsAndCleansUncommittedFragment()
    {
        using var cancellation = new CancellationTokenSource();
        using var setup = await SetupAsync(new CancelSecondEncoder(cancellation));
        var first = setup.Temp.CreateFile("source/one.jpg", [1]);
        var second = setup.Temp.CreateFile("source/two.jpg", [2]);
        var taskId = Guid.NewGuid();

        var result = await setup.Service.CompressAsync(taskId,
            new([first, second], setup.Temp.Combine("output"), new()), cancellationToken: cancellation.Token);

        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, result.State);
        Assert.AreEqual(1, result.Summary.Succeeded);
        Assert.AreEqual(1, result.Summary.Cancelled);
        Assert.AreEqual(BatchCompressionItemState.Completed, result.Items[0].State);
        Assert.IsNotNull(result.Items[0].DestinationPath);
        Assert.AreEqual(BatchCompressionItemState.Cancelled, result.Items[1].State);
        Assert.IsNull(result.Items[1].DestinationPath);
        Assert.IsTrue(File.Exists(first));
        Assert.IsTrue(File.Exists(second));
        Assert.HasCount(1, await setup.Undo.ListAsync(taskId));
        Assert.IsFalse(Directory.Exists(TemporaryTaskPath(taskId)) &&
                       Directory.EnumerateFileSystemEntries(TemporaryTaskPath(taskId)).Any());
    }

    [TestMethod]
    public async Task TaskCoordinator_PersistsTerminalStateAndClearsCompletedCheckpoint()
    {
        using var setup = await SetupAsync(new RecordingEncoder());
        var source = setup.Temp.CreateFile("source/task.jpg", [1, 2, 3]);
        var requestDirectory = setup.Temp.Combine("recovery");
        var store = new BatchCompressionRequestStore(requestDirectory);
        var repository = new SqliteTaskRepository(setup.Database);
        var handler = new BatchCompressionTaskHandler(store, setup.Service);
        var engine = new TaskEngine(repository, new ConservativeTaskScheduler(), [handler], setup.Audit,
            setup.Notifications, TimeSpan.Zero);
        var coordinator = new BatchCompressionTaskCoordinator(engine, store);

        var taskId = await coordinator.StartAsync(new([source], setup.Temp.Combine("output"), new()));
        await coordinator.WaitForCompletionAsync(taskId);

        var persisted = await repository.GetAsync(taskId);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(TaskLifecycleState.Completed, persisted.State);
        Assert.AreEqual(1, persisted.ResultSummary.Succeeded);
        Assert.IsFalse(new BatchCompressionRequestStore(requestDirectory).TryGet(taskId, out _));
        Assert.HasCount(1, await setup.Undo.ListAsync(taskId));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(source));
    }

    [TestMethod]
    public async Task CancellationAfterCompletedWork_KeepsCheckpointUntilIdempotentRetryCompletes()
    {
        using var setup = await SetupAsync(new RecordingEncoder());
        var source = setup.Temp.CreateFile("source/cancel-after-complete.jpg", [4, 3, 2, 1]);
        var output = setup.Temp.Combine("output");
        var recoveryDirectory = setup.Temp.Combine("recovery");
        var store = new BatchCompressionRequestStore(recoveryDirectory);
        var innerRepository = new SqliteTaskRepository(setup.Database);
        var repository = new BlockingCompletedProgressTaskRepository(innerRepository);
        var compression = new CompletedAfterProgressCompressionService();
        var engine = new TaskEngine(repository, new ConservativeTaskScheduler(),
            [new BatchCompressionTaskHandler(store, compression)], setup.Audit, setup.Notifications, TimeSpan.Zero);
        var coordinator = new BatchCompressionTaskCoordinator(engine, store);

        var taskId = await coordinator.StartAsync(new([source], output, new()));
        var completion = engine.WaitForCompletionAsync(taskId);
        await repository.ProgressSaveStarted.WaitAsync(TimeSpan.FromSeconds(10));
        await coordinator.CancelAsync(taskId);
        repository.ReleaseProgressSave();
        await completion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, (await innerRepository.GetAsync(taskId))!.State);
        var restartedStore = new BatchCompressionRequestStore(recoveryDirectory);
        Assert.IsTrue(restartedStore.TryGet(taskId, out var checkpoint));
        Assert.IsEmpty(checkpoint.PendingSourceFiles);
        Assert.HasCount(1, checkpoint.StableResults);
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        Assert.AreEqual(1, compression.ExecutionCount);

        var restartedEngine = new TaskEngine(innerRepository, new ConservativeTaskScheduler(),
            [new BatchCompressionTaskHandler(restartedStore, compression)], setup.Audit, setup.Notifications, TimeSpan.Zero);
        await restartedEngine.RetryAsync(taskId);
        await restartedEngine.WaitForCompletionAsync(taskId);

        Assert.AreEqual(TaskLifecycleState.Completed, (await innerRepository.GetAsync(taskId))!.State);
        Assert.IsFalse(new BatchCompressionRequestStore(recoveryDirectory).TryGet(taskId, out _));
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        Assert.AreEqual(1, compression.ExecutionCount);
        CollectionAssert.AreEqual(new byte[] { 4, 3, 2, 1 }, await File.ReadAllBytesAsync(source));
    }

    [TestMethod]
    public async Task SafeService_AwaitsDurableItemCallbackBeforeStartingNextItem()
    {
        using var setup = await SetupAsync(new RecordingEncoder());
        var first = setup.Temp.CreateFile("source/first.jpg", [1]);
        var second = setup.Temp.CreateFile("source/second.jpg", [2]);
        var output = setup.Temp.Combine("output");
        var taskId = Guid.NewGuid();
        await new SqliteTaskRepository(setup.Database).SaveAsync(new TaskRuntimeState
        {
            Definition = new(taskId, null, BatchCompressionDefaults.TaskType, "Batch compression",
                "PathsRedacted", null, DateTimeOffset.UtcNow)
        });
        var firstCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCount = 0;

        var compression = setup.Service.CompressAsync(taskId, new([first, second], output, new()),
            itemCompleted: item =>
            {
                Assert.IsNotNull(item.DestinationPath);
                Assert.IsTrue(File.Exists(item.DestinationPath));
                if (Interlocked.Increment(ref callbackCount) != 1) return Task.CompletedTask;
                firstCallback.TrySetResult(true);
                return releaseCallback.Task;
            });

        await firstCallback.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsFalse(compression.IsCompleted, "The second item started before the first durable callback completed.");
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        releaseCallback.TrySetResult(true);
        var result = await compression.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(TaskLifecycleState.Completed, result.State);
        Assert.AreEqual(2, callbackCount);
        Assert.HasCount(2, Directory.GetFiles(output, "*.jpg"));
    }

    [TestMethod]
    public void Request_RejectsRelativePathsInvalidQualityAndSequenceMetadata()
    {
        Assert.Throws<ArgumentException>(() => new BatchCompressionRequest(
            ["relative.jpg"], Path.GetTempPath(), new()).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchCompressionOptions(JpegQuality: 1).Validate());
        Assert.Throws<ArgumentException>(() => new BatchCompressionRequest(
            [Path.Combine(Path.GetTempPath(), "a.jpg")], Path.GetTempPath(), new(), SourceSequences: [0, 1]).Validate());
    }

    private static async Task<Setup> SetupAsync(IBatchCompressionEncoder encoder)
    {
        var temp = new TemporaryDirectory();
        var database = new PixelTartDatabase(temp.Combine("data/pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database,
            new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var verification = new FileVerificationService();
        var undo = new SqliteUndoJournalRepository(database);
        var audit = new RecordingAudit();
        var notifications = new RecordingNotifications();
        var executor = new FileOperationExecutor(new FileOperationValidator(), verification, undo, database);
        var service = new BatchCompressionSafeService(encoder, new FileOperationValidator(),
            new FileConflictResolver(), verification, audit, notifications, executor);
        return new(temp, database, service, undo, audit, notifications);
    }

    private static string TemporaryTaskPath(Guid taskId) =>
        Path.Combine(Path.GetTempPath(), "PixelTartBatchCompression", taskId.ToString("N"));

    private sealed class RecordingEncoder : IBatchCompressionEncoder
    {
        public int CallCount { get; private set; }
        public async Task EncodeAsync(string sourcePath, Stream destination, BatchCompressionOptions options,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            await destination.WriteAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, cancellationToken);
        }

        public Task VerifyDecodableAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEncoder : IBatchCompressionEncoder
    {
        public async Task EncodeAsync(string sourcePath, Stream destination, BatchCompressionOptions options,
            CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync(new byte[] { 1, 2 }, cancellationToken);
            throw new IOException("isolated encoder failure");
        }

        public Task VerifyDecodableAsync(string imagePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class UndecodableEncoder : IBatchCompressionEncoder
    {
        public async Task EncodeAsync(string sourcePath, Stream destination, BatchCompressionOptions options,
            CancellationToken cancellationToken = default) =>
            await destination.WriteAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, cancellationToken);

        public Task VerifyDecodableAsync(string imagePath, CancellationToken cancellationToken = default) =>
            throw new InvalidDataException("simulated decode rejection");
    }

    private sealed class CancelSecondEncoder(CancellationTokenSource cancellation) : IBatchCompressionEncoder
    {
        private int _count;

        public async Task EncodeAsync(string sourcePath, Stream destination, BatchCompressionOptions options,
            CancellationToken cancellationToken = default)
        {
            _count++;
            await destination.WriteAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, cancellationToken);
            if (_count == 2)
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public Task VerifyDecodableAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class CompletedAfterProgressCompressionService : IBatchCompressionService
    {
        private int _executionCount;
        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public async Task<BatchCompressionResult> CompressAsync(Guid taskId, BatchCompressionRequest request,
            IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
            Func<BatchCompressionItemResult, Task>? itemCompleted = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executionCount);
            Directory.CreateDirectory(request.DestinationDirectory);
            var source = request.SourceFiles.Single();
            var destination = Path.Combine(request.DestinationDirectory,
                Path.GetFileNameWithoutExtension(source) + ".jpg");
            await File.WriteAllBytesAsync(destination, [0xFF, 0xD8, 0xFF, 0xD9], CancellationToken.None);
            var sequence = request.SourceSequences?.Single() ?? 0;
            var item = new BatchCompressionItemResult(sequence, BatchCompressionItemState.Completed,
                source, destination, 4, null, null);
            var summary = new TaskResultSummary(1, 1, 0, 0, 0, 0, 0, 4);
            if (itemCompleted is not null) await itemCompleted(item);
            progress?.Report((100, "Item 1", summary));
            return new(taskId, TaskLifecycleState.Completed, summary, [item]);
        }
    }

    private sealed class BlockingCompletedProgressTaskRepository(ITaskRepository inner) : ITaskRepository
    {
        private readonly TaskCompletionSource<bool> _progressSaveStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseProgressSave =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _intercepted;

        public Task ProgressSaveStarted => _progressSaveStarted.Task;
        public void ReleaseProgressSave() => _releaseProgressSave.TrySetResult(true);

        public async Task SaveAsync(TaskRuntimeState state, CancellationToken cancellationToken = default)
        {
            if (state.State == TaskLifecycleState.Running && state.Progress >= 100 &&
                state.ResultSummary.Succeeded == 1 && Interlocked.CompareExchange(ref _intercepted, 1, 0) == 0)
            {
                _progressSaveStarted.TrySetResult(true);
                await _releaseProgressSave.Task.WaitAsync(cancellationToken);
            }
            await inner.SaveAsync(state, cancellationToken);
        }

        public Task<TaskRuntimeState?> GetAsync(Guid taskId, CancellationToken cancellationToken = default) =>
            inner.GetAsync(taskId, cancellationToken);
        public Task<IReadOnlyList<TaskRuntimeState>> ListAsync(int limit = 200,
            CancellationToken cancellationToken = default) => inner.ListAsync(limit, cancellationToken);
        public Task<IReadOnlyList<TaskRuntimeState>> ListUnfinishedAsync(CancellationToken cancellationToken = default) =>
            inner.ListUnfinishedAsync(cancellationToken);
        public Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint,
            CancellationToken cancellationToken = default) => inner.SaveCheckpointAsync(taskId, checkpoint, cancellationToken);
    }

    private sealed class RecordingAudit : IAuditLogService
    {
        public string? Message { get; private set; }

        public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null,
            Guid? projectId = null, string? errorCode = null, string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotifications : INotificationCenter
    {
        public event EventHandler<NotificationMessage>? Published;
        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            Published?.Invoke(this, message);
            return Task.CompletedTask;
        }
        public void NotifyPersisted(NotificationMessage message) => Published?.Invoke(this, message);
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>([]);
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Setup(
        TemporaryDirectory temp,
        PixelTartDatabase database,
        BatchCompressionSafeService service,
        SqliteUndoJournalRepository undo,
        RecordingAudit audit,
        RecordingNotifications notifications) : IDisposable
    {
        public TemporaryDirectory Temp { get; } = temp;
        public PixelTartDatabase Database { get; } = database;
        public BatchCompressionSafeService Service { get; } = service;
        public SqliteUndoJournalRepository Undo { get; } = undo;
        public RecordingAudit Audit { get; } = audit;
        public RecordingNotifications Notifications { get; } = notifications;

        public void Dispose()
        {
            SqliteTestIsolation.ClearPool(Database);
            Temp.Dispose();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.BatchCompression",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public string Combine(string relative) =>
            System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        public string CreateFile(string relative, byte[] bytes)
        {
            var path = Combine(relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch
            {
            }
        }
    }
}
