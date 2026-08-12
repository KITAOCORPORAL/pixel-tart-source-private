using Microsoft.Data.Sqlite;
using System.Runtime.Versioning;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class RawToJpegSafeConversionTests
{
    [TestMethod]
    public void StructuredFailurePayload_RedactsLocalPathsAndRoundTripsRequiredFields()
    {
        var detail = new MediaTaskFailureDetail("DSC09403.ARW", MediaTaskStages.RawDecode,
            ErrorCodeCatalog.DecodeFailed, "无法完成 RAW 解码。",
            @"LibRawException C:\Users\Example\Downloads\DSC09403.ARW", true, false);

        var payload = MediaTaskFailurePayload.Serialize(detail);

        Assert.IsTrue(MediaTaskFailurePayload.TryParse(payload, out var restored));
        Assert.AreEqual("DSC09403.ARW", restored!.FileName);
        Assert.AreEqual(MediaTaskStages.RawDecode, restored.Stage);
        Assert.AreEqual(ErrorCodeCatalog.DecodeFailed, restored.ErrorCode);
        Assert.IsTrue(restored.Retryable);
        Assert.IsFalse(restored.OutputOwned);
        Assert.DoesNotContain(@"C:\Users\Example", payload);
        StringAssert.Contains(payload, "PATH_REDACTED");
    }

    [TestMethod]
    public async Task Conversion_UsesCreateNewAutoNumberAndKeepsRawUnchanged()
    {
        using var setup = await SetupAsync();
        var source = setup.Temp.CreateFile("raw/portrait.ARW", [1, 2, 3, 4, 5]);
        var existing = setup.Temp.CreateFile("jpg/portrait.jpg", [9, 8, 7]);
        var sourceBytes = await File.ReadAllBytesAsync(source);
        var sourceInfo = new FileInfo(source);

        var result = await setup.ConvertAsync(new([source], setup.Temp.Combine("jpg"), new()), cancellationToken: CancellationToken.None);

        Assert.AreEqual(TaskLifecycleState.Completed, result.State);
        Assert.AreEqual(1, result.Summary.Succeeded);
        var output = result.Items.Single().DestinationPath!;
        Assert.AreNotEqual(existing, output);
        StringAssert.Contains(Path.GetFileName(output), "(1)");
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(existing));
        CollectionAssert.AreEqual(sourceBytes, await File.ReadAllBytesAsync(source));
        var after = new FileInfo(source);
        Assert.AreEqual(sourceInfo.Length, after.Length);
        Assert.AreEqual(sourceInfo.LastWriteTimeUtc, after.LastWriteTimeUtc);
        Assert.IsTrue(File.ReadAllBytes(output).AsSpan().StartsWith(new byte[] { 0xFF, 0xD8 }));
        Assert.IsTrue(File.ReadAllBytes(output).AsSpan().EndsWith(new byte[] { 0xFF, 0xD9 }));
        var journal = await setup.Undo.ListAsync(result.TaskId);
        Assert.HasCount(1, journal);
        Assert.AreEqual(FileOperationType.DeleteCreatedOutput, journal[0].ReverseOperation);
        Assert.AreEqual(UndoJournalState.Pending, journal[0].State);
    }

    [TestMethod]
    public async Task Conversion_AllowsSameDirectoryButStillAutoNumbersAndNeverTouchesRaw()
    {
        using var setup = await SetupAsync(useOperationExecutor: true);
        var source = setup.Temp.CreateFile("same/portrait.ARW", [1, 2, 3, 4]);
        var sourceBytes = await File.ReadAllBytesAsync(source);
        var sourceModified = File.GetLastWriteTimeUtc(source);
        var existing = setup.Temp.CreateFile("same/portrait.jpg", [9, 8, 7]);

        var result = await setup.ConvertAsync(new([source], setup.Temp.Combine("same"), new()));

        Assert.AreEqual(TaskLifecycleState.Completed, result.State);
        var output = result.Items.Single().DestinationPath!;
        Assert.AreNotEqual(source, output);
        Assert.AreNotEqual(existing, output);
        StringAssert.Contains(Path.GetFileName(output), "(1)");
        CollectionAssert.AreEqual(sourceBytes, await File.ReadAllBytesAsync(source));
        Assert.AreEqual(sourceModified, File.GetLastWriteTimeUtc(source));
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(existing));
    }

    [TestMethod]
    public async Task EncoderFailure_DoesNotLeaveOutputOrTouchRaw()
    {
        using var setup = await SetupAsync(new ThrowingEncoder());
        var source = setup.Temp.CreateFile("raw/failure.NEF", [5, 4, 3]);
        var result = await setup.ConvertAsync(new([source], setup.Temp.Combine("jpg"), new()));

        Assert.AreEqual(TaskLifecycleState.Failed, result.State);
        Assert.AreEqual(1, result.Summary.Failed);
        Assert.AreEqual(MediaTaskStages.JpegEncode, result.Items.Single().Failure?.Stage);
        Assert.AreEqual("无法完成 JPEG 编码。", result.Items.Single().Failure?.UserMessage);
        Assert.IsFalse(result.Items.Single().Failure?.OutputOwned);
        Assert.IsFalse(Directory.Exists(setup.Temp.Combine("jpg")) && Directory.GetFiles(setup.Temp.Combine("jpg")).Length > 0);
        CollectionAssert.AreEqual(new byte[] { 5, 4, 3 }, await File.ReadAllBytesAsync(source));
        Assert.IsEmpty(await setup.Undo.ListAsync(result.TaskId));
    }

    [TestMethod]
    public async Task Cancellation_RemovesOnlyOwnedPartialOutput()
    {
        using var cancellation = new CancellationTokenSource();
        using var setup = await SetupAsync(new CancellingEncoder(cancellation));
        var source = setup.Temp.CreateFile("raw/cancel.CR2", [1, 1, 1]);
        var token = cancellation.Token;

        var result = await setup.ConvertAsync(new([source], setup.Temp.Combine("jpg"), new()), cancellationToken: token);

        Assert.AreEqual(TaskLifecycleState.Cancelled, result.State);
        Assert.AreEqual(RawToJpegItemState.Cancelled, result.Items.Single().State);
        Assert.IsFalse(Directory.Exists(setup.Temp.Combine("jpg")) && Directory.GetFiles(setup.Temp.Combine("jpg")).Length > 0);
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task AuditAndNotificationNeverReceivePathsOrFileNames()
    {
        using var setup = await SetupAsync(new ThrowingEncoder());
        var source = setup.Temp.CreateFile("private/customer-name.RAF", [1]);
        var result = await setup.ConvertAsync(new([source], setup.Temp.Combine("private-output"), new()));

        Assert.AreEqual(TaskLifecycleState.Failed, result.State);
        Assert.IsNotNull(setup.AuditMessage);
        Assert.DoesNotContain(source, setup.AuditMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFileName(source), setup.AuditMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(setup.NotificationMessage);
        Assert.DoesNotContain(source, setup.NotificationMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFileName(source), setup.NotificationMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task BatchFailureDoesNotPreventOtherItemsAndReportsPartialCompletion()
    {
        using var setup = await SetupAsync(new FailSecondEncoder());
        var first = setup.Temp.CreateFile("raw/one.DNG", [1]);
        var second = setup.Temp.CreateFile("raw/two.DNG", [2]);
        var result = await setup.ConvertAsync(new([first, second], setup.Temp.Combine("jpg"), new()));

        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, result.State);
        Assert.AreEqual(1, result.Summary.Succeeded);
        Assert.AreEqual(1, result.Summary.Failed);
        Assert.IsTrue(File.Exists(result.Items.Single(item => item.State == RawToJpegItemState.Completed).DestinationPath));
        Assert.IsTrue(File.Exists(first));
        Assert.IsTrue(File.Exists(second));
    }

    [TestMethod]
    public async Task ExistingFileOperationExecutor_CopiesCreateNewAndWritesOneUndoEntry()
    {
        using var setup = await SetupAsync(useOperationExecutor: true);
        var source = setup.Temp.CreateFile("raw/executor.ARW", [7, 6, 5, 4]);
        var existing = setup.Temp.CreateFile("jpg/executor.jpg", [3, 2, 1]);
        var sourceBytes = await File.ReadAllBytesAsync(source);

        var result = await setup.ConvertAsync(new([source], setup.Temp.Combine("jpg"), new()));

        Assert.AreEqual(TaskLifecycleState.Completed, result.State);
        var output = result.Items.Single().DestinationPath!;
        Assert.AreNotEqual(existing, output);
        StringAssert.Contains(Path.GetFileName(output), "(1)");
        CollectionAssert.AreEqual(new byte[] { 3, 2, 1 }, await File.ReadAllBytesAsync(existing));
        CollectionAssert.AreEqual(sourceBytes, await File.ReadAllBytesAsync(source));
        Assert.HasCount(1, await setup.Undo.ListAsync(result.TaskId));
        var staging = Path.Combine(Path.GetTempPath(), "PixelTartRawToJpeg", result.TaskId.ToString("N"));
        Assert.IsFalse(Directory.Exists(staging) && Directory.EnumerateFiles(staging).Any());
    }

    [TestMethod]
    public async Task TaskEngine_PartialFailureRestartAndRetryProcessesOnlyPendingRaw()
    {
        using var temp = new TempDirectory("RawToJpegRecovery-" + Guid.NewGuid().ToString("N"));
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database,
            new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var repository = new SqliteTaskRepository(database);
        var recoveryDirectory = temp.Combine("recovery");
        var store = new RawToJpegRequestStore(recoveryDirectory);
        var conversion = new RetryOnceConversionService();
        var handler = new RawToJpegTaskHandler(store, conversion);
        var engine = new TaskEngine(repository, new ConservativeTaskScheduler(), [handler],
            new RecordingAudit(), new RecordingNotifications(), TimeSpan.Zero);
        var coordinator = new RawToJpegTaskCoordinator(engine, store, new FakeDecoder());
        var first = temp.CreateFile("raw/customer-one.ARW", [1]);
        var second = temp.CreateFile("raw/customer-two.ARW", [2]);
        var output = temp.Combine("jpg");

        var taskId = await coordinator.StartAsync(new([first, second], output, new()));
        await engine.WaitForCompletionAsync(taskId);

        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, (await repository.GetAsync(taskId))!.State);
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        var restartedStore = new RawToJpegRequestStore(recoveryDirectory);
        Assert.IsTrue(restartedStore.TryGet(taskId, out var checkpoint));
        CollectionAssert.AreEqual(new[] { second }, checkpoint.PendingSourceFiles.ToArray());
        var protectedBytes = await File.ReadAllBytesAsync(Path.Combine(recoveryDirectory, taskId.ToString("N") + ".dat"));
        Assert.IsFalse(System.Text.Encoding.UTF8.GetString(protectedBytes).Contains(Path.GetFileName(second), StringComparison.OrdinalIgnoreCase));

        var restartedEngine = new TaskEngine(repository, new ConservativeTaskScheduler(),
            [new RawToJpegTaskHandler(restartedStore, conversion)], new RecordingAudit(),
            new RecordingNotifications(), TimeSpan.Zero);
        await restartedEngine.RetryAsync(taskId);
        await restartedEngine.WaitForCompletionAsync(taskId);

        Assert.AreEqual(TaskLifecycleState.Completed, (await repository.GetAsync(taskId))!.State);
        Assert.HasCount(2, Directory.GetFiles(output, "*.jpg"));
        Assert.HasCount(3, conversion.ProcessedSources);
        Assert.AreEqual(1, conversion.ProcessedSources.Count(path => string.Equals(path, first, StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(2, conversion.ProcessedSources.Count(path => string.Equals(path, second, StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(new RawToJpegRequestStore(recoveryDirectory).TryGet(taskId, out _));
        CollectionAssert.AreEqual(new byte[] { 1 }, await File.ReadAllBytesAsync(first));
        CollectionAssert.AreEqual(new byte[] { 2 }, await File.ReadAllBytesAsync(second));
        SqliteTestIsolation.ClearPool(database);
    }

    [TestMethod]
    public async Task TaskHandler_UnexpectedConversionFailureDrainsProgressAndKeepsRecoveryCheckpoint()
    {
        using var temp = new TempDirectory("RawToJpegHandlerFailure-" + Guid.NewGuid().ToString("N"));
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database,
            new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var innerRepository = new SqliteTaskRepository(database);
        var repository = new BlockingProgressTaskRepository(innerRepository);
        var recoveryDirectory = temp.Combine("recovery");
        var store = new RawToJpegRequestStore(recoveryDirectory);
        var handler = new RawToJpegTaskHandler(store, new ThrowAfterProgressConversionService());
        var engine = new TaskEngine(repository, new ConservativeTaskScheduler(), [handler],
            new RecordingAudit(), new RecordingNotifications(), TimeSpan.Zero);
        var coordinator = new RawToJpegTaskCoordinator(engine, store, new FakeDecoder());
        var source = temp.CreateFile("raw/private-session.ARW", [4, 3, 2, 1]);

        var taskId = await coordinator.StartAsync(new([source], temp.Combine("jpg"), new()));
        var completion = engine.WaitForCompletionAsync(taskId);
        await repository.ProgressSaveStarted.WaitAsync(TimeSpan.FromSeconds(10));
        var waitedForProgress = !completion.IsCompleted;
        var checkpointPresentWhileDraining = new RawToJpegRequestStore(recoveryDirectory)
            .TryGet(taskId, out var checkpointWhileDraining);
        repository.ReleaseProgressSave();
        await completion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsTrue(waitedForProgress, "Handler returned before its reported progress finished persisting.");
        Assert.IsTrue(checkpointPresentWhileDraining);
        CollectionAssert.AreEqual(new[] { source }, checkpointWhileDraining.PendingSourceFiles.ToArray());
        Assert.AreEqual(0, repository.InFlightProgressSaves);
        Assert.IsTrue(repository.ProgressSaveCompleted.IsCompletedSuccessfully);
        Assert.AreEqual(TaskLifecycleState.Failed, (await innerRepository.GetAsync(taskId))!.State);
        Assert.IsTrue(new RawToJpegRequestStore(recoveryDirectory).TryGet(taskId, out var recovered));
        CollectionAssert.AreEqual(new[] { source }, recovered.PendingSourceFiles.ToArray());
        Assert.IsEmpty(recovered.StableResults);
        CollectionAssert.AreEqual(new byte[] { 4, 3, 2, 1 }, await File.ReadAllBytesAsync(source));
        SqliteTestIsolation.ClearPool(database);
    }

    [TestMethod]
    public async Task ExecutorCommit_CancellationDuringHashKeepsStableOutputAndRetryDoesNotDuplicate()
    {
        using var temp = new TempDirectory("RawToJpegCommittedCancellation-" + Guid.NewGuid().ToString("N"));
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database,
            new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var repository = new SqliteTaskRepository(database);
        var recoveryDirectory = temp.Combine("recovery");
        var store = new RawToJpegRequestStore(recoveryDirectory);
        var output = temp.Combine("jpg");
        var executor = new CompletingExecutor();
        var service = CreateService(database, executor, new CancellationSensitiveVerification());
        var engine = new TaskEngine(repository, new ConservativeTaskScheduler(),
            [new RawToJpegTaskHandler(store, service)], new RecordingAudit(), new RecordingNotifications(), TimeSpan.Zero);
        var source = temp.CreateFile("raw/committed.ARW", [1, 2, 3, 4]);
        var taskId = await new RawToJpegTaskCoordinator(engine, store, new FakeDecoder())
            .StartAsync(new([source], output, new()), CancellationToken.None);

        await executor.CommitReached.WaitAsync(TimeSpan.FromSeconds(10));
        await engine.CancelAsync(taskId);
        executor.Release();
        await engine.WaitForCompletionAsync(taskId);

        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, (await repository.GetAsync(taskId))!.State);
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        Assert.AreEqual(1, executor.ExecutionCount);
        Assert.IsTrue(new RawToJpegRequestStore(recoveryDirectory).TryGet(taskId, out var checkpoint));
        Assert.IsEmpty(checkpoint.PendingSourceFiles);
        Assert.HasCount(1, checkpoint.StableResults);
        Assert.AreEqual(Path.GetFullPath(Directory.GetFiles(output, "*.jpg").Single()),
            Path.GetFullPath(checkpoint.StableResults.Single().DestinationPath!));

        await engine.RetryAsync(taskId);
        await engine.WaitForCompletionAsync(taskId);

        Assert.AreEqual(TaskLifecycleState.Completed, (await repository.GetAsync(taskId))!.State);
        Assert.IsFalse(new RawToJpegRequestStore(recoveryDirectory).TryGet(taskId, out _));
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        Assert.AreEqual(1, executor.ExecutionCount);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(source));
        SqliteTestIsolation.ClearPool(database);
    }

    [TestMethod]
    public async Task ExecutorCollision_DoesNotClaimUnownedExistingFileAsStableOutput()
    {
        using var temp = new TempDirectory("RawToJpegCollisionOwnership-" + Guid.NewGuid().ToString("N"));
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database,
            new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var source = temp.CreateFile("raw/collision.NEF", [5, 6, 7]);
        var output = temp.Combine("jpg");
        var executor = new CollisionExecutor([0xFF, 0xD8, 0xFF, 0xD9]);
        var service = CreateService(database, executor, new FileVerificationService());
        var taskId = Guid.NewGuid();
        await new SqliteTaskRepository(database).SaveAsync(new TaskRuntimeState
        {
            Definition = new(taskId, null, RawToJpegDefaults.TaskType, "RAW 杞?JPG", "PathsRedacted", null,
                DateTimeOffset.UtcNow)
        });

        var result = await service.ConvertAsync(taskId, new([source], output, new()));

        Assert.AreEqual(TaskLifecycleState.NeedsAttention, result.State);
        Assert.AreEqual(RawToJpegItemState.NeedsAttention, result.Items.Single().State);
        Assert.IsNull(result.Items.Single().DestinationPath);
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        CollectionAssert.AreEqual(executor.UnownedBytes, await File.ReadAllBytesAsync(Directory.GetFiles(output, "*.jpg").Single()));
        Assert.IsEmpty(await new SqliteUndoJournalRepository(database).ListAsync(taskId));
        CollectionAssert.AreEqual(new byte[] { 5, 6, 7 }, await File.ReadAllBytesAsync(source));
        SqliteTestIsolation.ClearPool(database);
    }

    [TestMethod]
    public async Task SafeService_AwaitsDurableItemCallbackBeforeStartingNextItem()
    {
        using var setup = await SetupAsync();
        var first = setup.Temp.CreateFile("raw/first.ARW", [1]);
        var second = setup.Temp.CreateFile("raw/second.ARW", [2]);
        var output = setup.Temp.Combine("jpg");
        var taskId = Guid.NewGuid();
        await new SqliteTaskRepository(setup.Database).SaveAsync(new TaskRuntimeState
        {
            Definition = new(taskId, null, RawToJpegDefaults.TaskType, "RAW to JPG", "PathsRedacted", null,
                DateTimeOffset.UtcNow)
        });
        var firstCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCount = 0;

        var conversion = setup.Service.ConvertAsync(taskId, new([first, second], output, new()),
            itemCompleted: item =>
            {
                Assert.IsNotNull(item.DestinationPath);
                Assert.IsTrue(File.Exists(item.DestinationPath));
                if (Interlocked.Increment(ref callbackCount) != 1) return Task.CompletedTask;
                firstCallback.TrySetResult(true);
                return releaseCallback.Task;
            });

        await firstCallback.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsFalse(conversion.IsCompleted, "The second item started before the first durable callback completed.");
        Assert.HasCount(1, Directory.GetFiles(output, "*.jpg"));
        releaseCallback.TrySetResult(true);
        var result = await conversion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(TaskLifecycleState.Completed, result.State);
        Assert.AreEqual(2, callbackCount);
        Assert.HasCount(2, Directory.GetFiles(output, "*.jpg"));
    }

    private static async Task<Setup> SetupAsync(IRawJpegEncoder? encoder = null, bool useOperationExecutor = false)
    {
        var temp = new TempDirectory("RawToJpeg-" + Guid.NewGuid().ToString("N"));
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var audit = new RecordingAudit();
        var notifications = new RecordingNotifications();
        var journal = new SqliteUndoJournalRepository(database);
        var validator = new FileOperationValidator();
        var verification = new FileVerificationService();
        var executor = useOperationExecutor ? new FileOperationExecutor(validator, verification, journal, database) : null;
        var service = new RawToJpegSafeConversionService(new FakeDecoder(), encoder ?? new JpegEncoder(),
            new FileConflictResolver(), validator, verification, journal, audit, notifications, executor);
        return new(temp, database, service, journal, audit, notifications);
    }

    private static RawToJpegSafeConversionService CreateService(
        PixelTartDatabase database,
        IFileOperationExecutor executor,
        IFileVerificationService verification) => new(new FakeDecoder(), new JpegEncoder(),
        new FileConflictResolver(), new FileOperationValidator(), verification,
        new SqliteUndoJournalRepository(database), new RecordingAudit(), new RecordingNotifications(), executor);

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database, RawToJpegSafeConversionService service,
        SqliteUndoJournalRepository undo, RecordingAudit audit, RecordingNotifications notifications) : IDisposable
    {
        public TempDirectory Temp { get; } = temp;
        public PixelTartDatabase Database { get; } = database;
        public RawToJpegSafeConversionService Service { get; } = service;
        public SqliteUndoJournalRepository Undo { get; } = undo;
        public string? AuditMessage => audit.Message;
        public string? NotificationMessage => notifications.Message;
        public async Task<RawToJpegBatchResult> ConvertAsync(RawToJpegBatchRequest request, CancellationToken cancellationToken = default)
        {
            var taskId = Guid.NewGuid();
            var definition = new TaskDefinition(taskId, request.ProjectId, RawToJpegDefaults.TaskType, "RAW 转 JPG", "PathsRedacted", null, DateTimeOffset.UtcNow);
            await new SqliteTaskRepository(Database).SaveAsync(new TaskRuntimeState { Definition = definition });
            return await Service.ConvertAsync(taskId, request, cancellationToken: cancellationToken);
        }
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }

    private sealed class FakeDecoder : IRawDecoder
    {
        public RawDecoderCapability GetCapability() => new(true, "test", "test", [".ARW"], [".ARW"]);
        public Task<RawDecodedImage> DecodeAsync(string sourcePath, RawToJpegOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RawDecodedImage(2, 1, 6, [255, 0, 0, 0, 255, 0], new(null, null, null, 1, "sRGB")));
    }

    private sealed class JpegEncoder : IRawJpegEncoder
    {
        public async Task EncodeAsync(RawDecodedImage image, Stream destination, RawToJpegOptions options, CancellationToken cancellationToken = default) =>
            await destination.WriteAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, cancellationToken);
    }

    private sealed class ThrowingEncoder : IRawJpegEncoder
    {
        public async Task EncodeAsync(RawDecodedImage image, Stream destination, RawToJpegOptions options, CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync(new byte[] { 0xFF, 0xD8 }, cancellationToken);
            throw new InvalidDataException("simulated encoder error");
        }
    }

    private sealed class CancellingEncoder(CancellationTokenSource cancellation) : IRawJpegEncoder
    {
        public async Task EncodeAsync(RawDecodedImage image, Stream destination, RawToJpegOptions options, CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync(new byte[] { 0xFF, 0xD8 }, cancellationToken);
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class FailSecondEncoder : IRawJpegEncoder
    {
        private int _calls;
        public async Task EncodeAsync(RawDecodedImage image, Stream destination, RawToJpegOptions options, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 2) throw new InvalidDataException("simulated second item error");
            await destination.WriteAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, cancellationToken);
        }
    }

    private sealed class CompletingExecutor : IFileOperationExecutor
    {
        private readonly TaskCompletionSource<bool> _commitReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _executionCount;
        public Task CommitReached => _commitReached.Task;
        public int ExecutionCount => Volatile.Read(ref _executionCount);
        public void Release() => _release.TrySetResult(true);

        public async Task<FileOperationExecutionResult> ExecuteAsync(FileOperationPlan plan,
            Func<string, int, string?, CancellationToken, Task>? safeBoundary = null,
            IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executionCount);
            Assert.HasCount(1, plan.Items);
            var item = plan.Items.Single();
            Directory.CreateDirectory(Path.GetDirectoryName(item.DestinationPath)!);
            await File.WriteAllBytesAsync(item.DestinationPath, [0xFF, 0xD8, 0xFF, 0xD9], CancellationToken.None);
            _commitReached.TrySetResult(true);
            await _release.Task.ConfigureAwait(false);
            var result = new FileOperationItemResult(item.Id, FileOperationItemState.Completed,
                item.DestinationPath, 4, null, null, null);
            return new(new(1, 1, 0, 0, 0, 0, 4, 4), [result]);
        }
    }

    private sealed class CollisionExecutor(byte[] unownedBytes) : IFileOperationExecutor
    {
        public byte[] UnownedBytes { get; } = unownedBytes;

        public async Task<FileOperationExecutionResult> ExecuteAsync(FileOperationPlan plan,
            Func<string, int, string?, CancellationToken, Task>? safeBoundary = null,
            IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Assert.HasCount(1, plan.Items);
            var item = plan.Items.Single();
            Directory.CreateDirectory(Path.GetDirectoryName(item.DestinationPath)!);
            await File.WriteAllBytesAsync(item.DestinationPath, UnownedBytes, CancellationToken.None);
            var result = new FileOperationItemResult(item.Id, FileOperationItemState.Failed,
                null, 0, null, ErrorCodeCatalog.DestinationNotWritable, "destination collision");
            return new(new(1, 0, 1, 0, 0, 0, 0, 0), [result]);
        }
    }

    private sealed class CancellationSensitiveVerification : IFileVerificationService
    {
        public Task<string?> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>("verified-hash");
        }

        public Task<bool> VerifyAsync(string sourcePath, string destinationPath, bool verifyHash,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RetryOnceConversionService : IRawToJpegSafeConversionService
    {
        private int _batchAttempt;
        public List<string> ProcessedSources { get; } = [];

        public async Task<RawToJpegBatchResult> ConvertAsync(Guid taskId, RawToJpegBatchRequest request,
            IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
            Func<RawToJpegItemResult, Task>? itemCompleted = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<RawToJpegItemResult>();
            var batchAttempt = Interlocked.Increment(ref _batchAttempt);
            foreach (var source in request.SourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProcessedSources.Add(source);
                var localIndex = request.SourceFiles.ToList().IndexOf(source);
                var sequence = request.SourceSequences?[localIndex] ?? localIndex;
                if (batchAttempt == 1 && localIndex == 1)
                {
                    var failed = new RawToJpegItemResult(sequence, RawToJpegItemState.Failed, source, null, 0, null,
                        ErrorCodeCatalog.DecodeFailed, "Conversion failed.");
                    results.Add(failed);
                    if (itemCompleted is not null) await itemCompleted(failed);
                    continue;
                }

                Directory.CreateDirectory(request.DestinationRoot);
                var destination = Path.Combine(request.DestinationRoot, Path.GetFileNameWithoutExtension(source) + ".jpg");
                await File.WriteAllBytesAsync(destination, [0xFF, 0xD8, 0xFF, 0xD9], cancellationToken);
                var completed = new RawToJpegItemResult(sequence, RawToJpegItemState.Completed, source, destination,
                    4, null, null, null);
                results.Add(completed);
                if (itemCompleted is not null) await itemCompleted(completed);
            }

            var summary = new TaskResultSummary(request.SourceFiles.Count,
                results.Count(item => item.State == RawToJpegItemState.Completed),
                results.Count(item => item.State == RawToJpegItemState.Failed), 0, 0, 0, 0,
                results.Sum(item => item.BytesWritten));
            return new(taskId, summary.IsPartial ? TaskLifecycleState.PartiallyCompleted : TaskLifecycleState.Completed,
                summary, results);
        }
    }

    private sealed class ThrowAfterProgressConversionService : IRawToJpegSafeConversionService
    {
        public Task<RawToJpegBatchResult> ConvertAsync(Guid taskId, RawToJpegBatchRequest request,
            IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
            Func<RawToJpegItemResult, Task>? itemCompleted = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report((50, "redacted", new(request.SourceFiles.Count, 0, 0, 0, 0, 0, 0, 0)));
            throw new InvalidOperationException("simulated unexpected conversion failure");
        }
    }

    private sealed class BlockingProgressTaskRepository(ITaskRepository inner) : ITaskRepository
    {
        private readonly TaskCompletionSource<bool> _progressSaveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseProgressSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _progressSaveCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _progressSaveIntercepted;
        private int _inFlightProgressSaves;

        public Task ProgressSaveStarted => _progressSaveStarted.Task;
        public Task ProgressSaveCompleted => _progressSaveCompleted.Task;
        public int InFlightProgressSaves => Volatile.Read(ref _inFlightProgressSaves);
        public void ReleaseProgressSave() => _releaseProgressSave.TrySetResult(true);

        public async Task SaveAsync(TaskRuntimeState state, CancellationToken cancellationToken = default)
        {
            if (string.Equals(state.CurrentStep, "RAW 转 JPG", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref _progressSaveIntercepted, 1, 0) == 0)
            {
                Interlocked.Increment(ref _inFlightProgressSaves);
                _progressSaveStarted.TrySetResult(true);
                try
                {
                    await _releaseProgressSave.Task.WaitAsync(cancellationToken);
                    await inner.SaveAsync(state, cancellationToken);
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlightProgressSaves);
                    _progressSaveCompleted.TrySetResult(true);
                }
                return;
            }
            await inner.SaveAsync(state, cancellationToken);
        }

        public Task<TaskRuntimeState?> GetAsync(Guid taskId, CancellationToken cancellationToken = default) =>
            inner.GetAsync(taskId, cancellationToken);
        public Task<IReadOnlyList<TaskRuntimeState>> ListAsync(int limit = 200, CancellationToken cancellationToken = default) =>
            inner.ListAsync(limit, cancellationToken);
        public Task<IReadOnlyList<TaskRuntimeState>> ListUnfinishedAsync(CancellationToken cancellationToken = default) =>
            inner.ListUnfinishedAsync(cancellationToken);
        public Task SaveCheckpointAsync(Guid taskId, TaskCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            inner.SaveCheckpointAsync(taskId, checkpoint, cancellationToken);
    }

    private sealed class RecordingAudit : IAuditLogService
    {
        public string? Message { get; private set; }
        public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default) { Message = message; return Task.CompletedTask; }
    }

    private sealed class RecordingNotifications : INotificationCenter
    {
        public string? Message { get; private set; }
        public event EventHandler<NotificationMessage>? Published { add { } remove { } }
        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default) { Message = message.Message; return Task.CompletedTask; }
        public void NotifyPersisted(NotificationMessage message) { }
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>([]);
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
