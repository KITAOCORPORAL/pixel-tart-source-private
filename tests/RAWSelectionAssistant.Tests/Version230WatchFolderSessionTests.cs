using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230WatchFolderSessionTests
{
    [TestMethod]
    public async Task DefaultStart_DoesNotImportExistingTopLevelFile()
    {
        using var setup = await SetupAsync();
        var existing = setup.Temp.CreateFile("watch/existing.jpg", [1, 2, 3]);
        var (adapter, _) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory));
        await session.ReconcileAsync();
        Assert.HasCount(0, await setup.Assets.ListBySessionAsync(session.Session.Id));
        Assert.IsTrue(File.Exists(existing));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task ImportExisting_ProcessesOnlyTopLevelCandidates()
    {
        using var setup = await SetupAsync();
        setup.Temp.CreateFile("watch/existing.jpg", [1, 2, 3]);
        setup.Temp.CreateFile("watch/child/nested.jpg", [1, 2, 3]);
        var (adapter, _) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory, ImportExisting: true));
        await session.ReconcileAsync();
        var assets = await setup.Assets.ListBySessionAsync(session.Session.Id);
        Assert.HasCount(1, assets);
        Assert.AreEqual("existing.jpg", assets[0].FileName);
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task CreatedEvent_WaitsForStableAndPublishesReadySnapshot()
    {
        using var setup = await SetupAsync();
        var (adapter, source) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory));
        var ready = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.ProcessingState == TetherProcessingState.Ready));
        var path = setup.Temp.CreateFile("watch/new.jpg", [1, 2, 3]);
        source.Publish(WatchFolderEventKind.Created, path);
        var snapshot = await ready.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.HasCount(1, snapshot.Assets);
        Assert.AreEqual(TetherStabilityState.Stable, snapshot.Assets[0].StabilityState);
        Assert.IsTrue(File.Exists(path));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task DuplicateCreatedAndChangedEvents_CreateOneAsset()
    {
        using var setup = await SetupAsync();
        var (adapter, source) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory));
        var path = setup.Temp.CreateFile("watch/one.jpg", [1, 2, 3]);
        var ready = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.ProcessingState == TetherProcessingState.Ready));
        source.Publish(WatchFolderEventKind.Created, path);
        source.Publish(WatchFolderEventKind.Changed, path);
        await ready.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.HasCount(1, await setup.Assets.ListBySessionAsync(session.Session.Id));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task ChangedEvent_RetriesTimedOutAsset()
    {
        using var setup = await SetupAsync();
        var probe = new SequenceProbe(new(TetherStabilityState.TimedOut, 3, DateTimeOffset.UtcNow, ErrorCodeCatalog.FileLocked), new(TetherStabilityState.Stable, 3, DateTimeOffset.UtcNow));
        var (adapter, source) = setup.Adapter(probe); var session = await adapter.StartAsync(new(setup.WatchDirectory)); var path = setup.Temp.CreateFile("watch/changed.jpg", [1, 2, 3]);
        var attention = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.ProcessingState == TetherProcessingState.NeedsAttention));
        source.Publish(WatchFolderEventKind.Created, path); await attention.WaitAsync(TimeSpan.FromSeconds(3));
        source.Publish(WatchFolderEventKind.Changed, path);
        await session.ReconcileAsync();
        Assert.AreEqual(TetherStabilityState.Stable, (await setup.Assets.ListBySessionAsync(session.Session.Id)).Single().StabilityState);
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task TemporaryFile_RenamedToSupportedName_IsDiscoveredOnce()
    {
        using var setup = await SetupAsync(); var (adapter, source) = setup.Adapter(new StableProbe()); var session = await adapter.StartAsync(new(setup.WatchDirectory, ImportExisting: true));
        var temporary = setup.Temp.CreateFile("watch/capture.tmp", [1, 2, 3]); source.Publish(WatchFolderEventKind.Created, temporary);
        var final = Path.Combine(setup.WatchDirectory, "capture.jpg"); File.Move(temporary, final);
        var ready = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.FileName == "capture.jpg" && asset.StabilityState == TetherStabilityState.Stable));
        source.Publish(WatchFolderEventKind.Renamed, final); await ready.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.HasCount(1, await setup.Assets.ListBySessionAsync(session.Session.Id));
        Assert.IsTrue(File.Exists(final));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task WatcherError_TriggersRestrictedTopLevelReconciliation()
    {
        using var setup = await SetupAsync(); var (adapter, source) = setup.Adapter(new StableProbe()); var session = await adapter.StartAsync(new(setup.WatchDirectory, ImportExisting: true));
        setup.Temp.CreateFile("watch/missed.jpg", [1, 2, 3]);
        var ready = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.FileName == "missed.jpg"));
        source.Publish(WatchFolderEventKind.Error); await ready.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsNotNull(session.Session.LastReconciledAtUtc);
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task HundredFileBurst_IsRecoveredWithoutDuplicates()
    {
        using var setup = await SetupAsync(); var (adapter, source) = setup.Adapter(new StableProbe()); var session = await adapter.StartAsync(new(setup.WatchDirectory));
        for (var index = 0; index < 100; index++)
        {
            var path = setup.Temp.CreateFile($"watch/burst {index:D3}.jpg", [(byte)index, 1, 2]);
            source.Publish(WatchFolderEventKind.Created, path);
        }
        await session.ReconcileAsync();
        Assert.HasCount(100, await setup.Assets.ListBySessionAsync(session.Session.Id));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task QueuedCreatedEvents_WithCoarseOlderCreationTime_AreNotDroppedByReconciliation()
    {
        using var setup = await SetupAsync();
        var (adapter, source) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory));
        for (var index = 0; index < 100; index++)
        {
            var path = setup.Temp.CreateFile($"watch/coarse-time-{index:D3}.jpg", [(byte)index, 1, 2]);
            File.SetCreationTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
            source.Publish(WatchFolderEventKind.Created, path);
        }

        await session.ReconcileAsync();

        Assert.HasCount(100, await setup.Assets.ListBySessionAsync(session.Session.Id));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task MixedBurstEvents_DeduplicateToOneHundredReadyAssets()
    {
        using var setup = await SetupAsync();
        var (adapter, source) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory, ImportExisting: true));
        var paths = Enumerable.Range(0, 100)
            .Select(index => setup.Temp.CreateFile($"watch/mixed-{index:D3}{(index % 2 == 0 ? ".jpg" : ".nef")}", [(byte)index, 1, 2, 3]))
            .ToArray();
        foreach (var path in paths)
        {
            source.Publish(WatchFolderEventKind.Created, path);
            source.Publish(WatchFolderEventKind.Changed, path);
            source.Publish(WatchFolderEventKind.Renamed, path);
        }

        await session.ReconcileAsync();

        var assets = await setup.Assets.ListBySessionAsync(session.Session.Id);
        Assert.HasCount(100, assets);
        Assert.AreEqual(100, assets.Count(asset => asset.StabilityState == TetherStabilityState.Stable));
        Assert.HasCount(100, assets.Select(asset => asset.NormalizedSourcePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        Assert.IsTrue(paths.All(File.Exists));
        await session.DisposeAsync();
    }

    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public async Task BatchOfOneThousandSyntheticFiles_CompletesWithoutDuplicatesOrSourceChanges()
    {
        using var setup = await SetupAsync();
        var paths = Enumerable.Range(0, 1000)
            .Select(index => setup.Temp.CreateFile($"watch/batch-{index:D4}{(index % 2 == 0 ? ".jpg" : ".nef")}", [(byte)(index % 251), 2, 3, 4]))
            .ToArray();
        var before = paths.ToDictionary(path => path, path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))), StringComparer.OrdinalIgnoreCase);
        var (adapter, _) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory, ImportExisting: true));

        await session.ReconcileAsync();

        var assets = await setup.Assets.ListBySessionAsync(session.Session.Id);
        Assert.HasCount(1000, assets);
        Assert.AreEqual(1000, assets.Count(asset => asset.StabilityState == TetherStabilityState.Stable));
        Assert.HasCount(1000, assets.Select(asset => asset.NormalizedSourcePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        foreach (var path in paths)
        {
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(before[path], Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
        }
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task RawCandidate_UsesPlaceholderAndKeepsRawFile()
    {
        using var setup = await SetupAsync();
        var (adapter, source) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory));
        var path = setup.Temp.CreateFile("watch/capture.nef", [1, 2, 3]);
        var ready = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.PreviewState == TetherPreviewState.Placeholder));
        source.Publish(WatchFolderEventKind.Created, path);
        var snapshot = await ready.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(TetherMediaKind.Raw, snapshot.Assets[0].MediaKind);
        Assert.IsTrue(File.Exists(path));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task TimedOutCandidate_RetriesDuringReconciliationWithoutDeletingFile()
    {
        using var setup = await SetupAsync();
        var probe = new SequenceProbe(new(TetherStabilityState.TimedOut, 3, DateTimeOffset.UtcNow, ErrorCodeCatalog.FileLocked), new(TetherStabilityState.Stable, 3, DateTimeOffset.UtcNow));
        var (adapter, source) = setup.Adapter(probe);
        var session = await adapter.StartAsync(new(setup.WatchDirectory));
        var path = setup.Temp.CreateFile("watch/retry.jpg", [1, 2, 3]);
        var attention = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.ProcessingState == TetherProcessingState.NeedsAttention));
        source.Publish(WatchFolderEventKind.Created, path);
        await attention.WaitAsync(TimeSpan.FromSeconds(3));
        await session.ReconcileAsync();
        var loaded = (await setup.Assets.ListBySessionAsync(session.Session.Id)).Single();
        Assert.AreEqual(TetherStabilityState.Stable, loaded.StabilityState);
        Assert.IsTrue(File.Exists(path));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task OneBadCandidate_DoesNotTerminateSessionWorker()
    {
        using var setup = await SetupAsync();
        var probe = new ThrowThenStableProbe();
        var (adapter, source) = setup.Adapter(probe);
        var session = await adapter.StartAsync(new(setup.WatchDirectory));
        var first = setup.Temp.CreateFile("watch/bad.jpg", [1]);
        var firstHandled = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.FileName == "bad.jpg" && asset.StabilityState == TetherStabilityState.Probing));
        source.Publish(WatchFolderEventKind.Created, first);
        await firstHandled.WaitAsync(TimeSpan.FromSeconds(3));
        var second = setup.Temp.CreateFile("watch/good.jpg", [1, 2]);
        source.Publish(WatchFolderEventKind.Created, second);
        await session.ReconcileAsync();
        Assert.IsTrue((await setup.Assets.ListBySessionAsync(session.Session.Id)).Any(asset => asset.FileName == "good.jpg" && asset.StabilityState == TetherStabilityState.Stable));
        Assert.AreEqual(TetherSessionState.Running, session.Session.State);
        Assert.IsTrue(File.Exists(first));
        Assert.IsTrue(File.Exists(second));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task Stop_PersistsTerminalStateAndNeverDeletesDiscoveredFile()
    {
        using var setup = await SetupAsync();
        var (adapter, source) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory));
        var path = setup.Temp.CreateFile("watch/keep.jpg", [1]);
        var ready = SnapshotWhen(session, snapshot => snapshot.Assets.Count == 1);
        source.Publish(WatchFolderEventKind.Created, path); await ready.WaitAsync(TimeSpan.FromSeconds(3));
        await session.StopAsync();
        Assert.AreEqual(TetherSessionState.Stopped, (await setup.Sessions.GetAsync(session.Session.Id))!.State);
        Assert.IsTrue(File.Exists(path));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task RecoverLatest_RestartsPersistedRunningSession()
    {
        using var setup = await SetupAsync();
        var now = DateTimeOffset.UtcNow;
        var record = new TetherSessionRecord(Guid.NewGuid(), null, CameraProviderType.WatchFolder, setup.WatchDirectory, WatchFolderPathPolicy.NormalizeDirectory(setup.WatchDirectory),
            TetherSessionState.Running, now.AddMinutes(-2), now.AddMinutes(-2), false, false, null, false, null, now.AddMinutes(-1));
        await setup.Sessions.AddAsync(record);
        var (adapter, _) = setup.Adapter(new StableProbe());
        var recovered = await adapter.RecoverLatestAsync();
        Assert.IsNotNull(recovered);
        Assert.AreEqual(record.Id, recovered.Session.Id);
        Assert.AreEqual(CameraProviderType.WatchFolder, adapter.ActiveProvider);
        await recovered.DisposeAsync();
    }

    [TestMethod]
    public async Task RecoveredReadyAsset_DoesNotRegenerateProxy()
    {
        using var setup = await SetupAsync(); var now = DateTimeOffset.UtcNow; var sessionRecord = RunningRecord(setup, now, copyToProject: false); await setup.Sessions.AddAsync(sessionRecord);
        var path = setup.Temp.CreateFile("watch/ready.jpg", [1, 2, 3]); var info = new FileInfo(path);
        await setup.Assets.UpsertDiscoveredAsync(new(Guid.NewGuid(), sessionRecord.Id, null, path, WatchFolderPathPolicy.NormalizePath(path), info.Name, info.Extension, TetherMediaKind.PreviewImage,
            info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), now, TetherStabilityState.Stable, TetherProcessingState.Ready, TetherPreviewState.Ready, now, now, "existing-proxy"));
        var cache = new CountingProxyCache(); var (adapter, _) = setup.Adapter(new StableProbe(), proxy: cache); var recovered = await adapter.RecoverLatestAsync(); Assert.IsNotNull(recovered);
        await recovered.ReconcileAsync(); Assert.AreEqual(0, cache.CreateCalls); await recovered.DisposeAsync();
    }

    [TestMethod]
    public async Task RecoveredCopiedAsset_DoesNotCopyAgain()
    {
        using var setup = await SetupAsync(); var now = DateTimeOffset.UtcNow; var sessionRecord = RunningRecord(setup, now, copyToProject: true); await setup.Sessions.AddAsync(sessionRecord);
        var path = setup.Temp.CreateFile("watch/copied.jpg", [1, 2, 3]); var copy = setup.Temp.CreateFile("project/copied.jpg", [1, 2, 3]); var info = new FileInfo(path);
        await setup.Assets.UpsertDiscoveredAsync(new(Guid.NewGuid(), sessionRecord.Id, null, path, WatchFolderPathPolicy.NormalizePath(path), info.Name, info.Extension, TetherMediaKind.PreviewImage,
            info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), now, TetherStabilityState.Stable, TetherProcessingState.Copied, TetherPreviewState.Ready, now, now, "existing-proxy", ProjectCopyPath: copy));
        var transfer = new RecordingTransfer(); var (adapter, _) = setup.Adapter(new StableProbe(), transfer); var recovered = await adapter.RecoverLatestAsync(); Assert.IsNotNull(recovered);
        await recovered.ReconcileAsync(); Assert.AreEqual(0, transfer.ProjectCalls); await recovered.DisposeAsync();
    }

    [TestMethod]
    public async Task DeletedWatchDirectory_EntersNeedsAttentionWithoutTouchingFilesElsewhere()
    {
        using var setup = await SetupAsync(); var safe = setup.Temp.CreateFile("outside-safe.jpg", [1]); var (adapter, _) = setup.Adapter(new StableProbe()); var session = await adapter.StartAsync(new(setup.WatchDirectory));
        Directory.Delete(setup.WatchDirectory, true); await session.ReconcileAsync();
        Assert.AreEqual(TetherSessionState.NeedsAttention, session.Session.State);
        Assert.AreEqual(ErrorCodeCatalog.SourceNotFound, session.Session.LastErrorCode);
        Assert.IsTrue(File.Exists(safe));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task RestoredWatchDirectory_ReconcileReturnsSessionToRunningWithoutDuplicateAssets()
    {
        using var setup = await SetupAsync();
        var safe = setup.Temp.CreateFile("outside-safe.jpg", [1, 2, 3]);
        var (adapter, _) = setup.Adapter(new StableProbe());
        var session = await adapter.StartAsync(new(setup.WatchDirectory, ImportExisting: true));
        Directory.Delete(setup.WatchDirectory, true);
        await session.ReconcileAsync();
        Assert.AreEqual(TetherSessionState.NeedsAttention, session.Session.State);

        Directory.CreateDirectory(setup.WatchDirectory);
        var restored = setup.Temp.CreateFile("watch/restored.jpg", [4, 5, 6]);
        await session.ReconcileAsync();
        await session.ReconcileAsync();

        Assert.AreEqual(TetherSessionState.Running, session.Session.State);
        Assert.IsNull(session.Session.LastErrorCode);
        Assert.HasCount(1, await setup.Assets.ListBySessionAsync(session.Session.Id));
        Assert.IsTrue(File.Exists(restored));
        Assert.IsTrue(File.Exists(safe));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task PermissionFailure_UsesNeedsAttentionAndKeepsCandidate()
    {
        using var setup = await SetupAsync(); var probe = new SequenceProbe(new FileStabilityResult(TetherStabilityState.TimedOut, 1, DateTimeOffset.UtcNow, ErrorCodeCatalog.PermissionDenied));
        var (adapter, source) = setup.Adapter(probe); var session = await adapter.StartAsync(new(setup.WatchDirectory)); var path = setup.Temp.CreateFile("watch/permission.jpg", [1]);
        var attention = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.LastErrorCode == ErrorCodeCatalog.PermissionDenied));
        source.Publish(WatchFolderEventKind.Created, path); await attention.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsTrue(File.Exists(path)); await session.DisposeAsync();
    }

    [TestMethod]
    public async Task ChineseSpacesAndLongTopLevelName_AreHandled()
    {
        using var setup = await SetupAsync(); var (adapter, source) = setup.Adapter(new StableProbe()); var session = await adapter.StartAsync(new(setup.WatchDirectory));
        var name = "中文 空格 " + new string('长', 80) + ".jpg"; var path = setup.Temp.CreateFile(Path.Combine("watch", name), [1, 2, 3]);
        var ready = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.FileName == name)); source.Publish(WatchFolderEventKind.Created, path); await ready.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsTrue(File.Exists(path)); await session.DisposeAsync();
    }

    [TestMethod]
    public async Task OptionalProjectAndBackupCopies_AreIndependentAndDefaultOff()
    {
        using var setup = await SetupAsync();
        var transfer = new RecordingTransfer();
        var (adapter, source) = setup.Adapter(new StableProbe(), transfer);
        var session = await adapter.StartAsync(new(setup.WatchDirectory, CopyToProject: true, ProjectDestination: setup.Temp.Combine("project"), CopyToBackup: true, BackupDestination: setup.Temp.Combine("backup")));
        var path = setup.Temp.CreateFile("watch/copy.jpg", [1]);
        var copied = SnapshotWhen(session, snapshot => snapshot.Assets.Any(asset => asset.ProcessingState == TetherProcessingState.Copied));
        source.Publish(WatchFolderEventKind.Created, path); await copied.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(1, transfer.ProjectCalls);
        Assert.AreEqual(1, transfer.BackupCalls);
        Assert.IsTrue(File.Exists(path));
        await session.DisposeAsync();

        var (defaultAdapter, _) = setup.Adapter(new StableProbe());
        var defaultSession = await defaultAdapter.StartAsync(new(setup.WatchDirectory));
        Assert.IsFalse(defaultSession.Session.CopyToProject);
        Assert.IsFalse(defaultSession.Session.CopyToBackup);
        await defaultSession.DisposeAsync();
    }

    private static Task<TetherSessionSnapshot> SnapshotWhen(ICameraSession session, Func<TetherSessionSnapshot, bool> predicate)
    {
        var completion = new TaskCompletionSource<TetherSessionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<TetherSessionSnapshot>? handler = null;
        handler = (_, snapshot) => { if (!predicate(snapshot)) return; session.SnapshotChanged -= handler; completion.TrySetResult(snapshot); };
        session.SnapshotChanged += handler;
        return completion.Task;
    }

    private static async Task<Setup> SetupAsync()
    {
        var temp = new TempDirectory(); var watch = temp.Combine("watch"); Directory.CreateDirectory(watch);
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        return new(temp, watch, database, new SqliteTetherSessionRepository(database), new SqliteTetherAssetRepository(database));
    }

    private sealed class Setup(TempDirectory temp, string watchDirectory, PixelTartDatabase database, SqliteTetherSessionRepository sessions, SqliteTetherAssetRepository assets) : IDisposable
    {
        public TempDirectory Temp { get; } = temp; public string WatchDirectory { get; } = watchDirectory; public PixelTartDatabase Database { get; } = database;
        public SqliteTetherSessionRepository Sessions { get; } = sessions; public SqliteTetherAssetRepository Assets { get; } = assets;
        public (WatchFolderCameraAdapter Adapter, FakeEventSource Source) Adapter(IFileStabilityProbe probe, ICameraTransferService? transfer = null, ITetherProxyCache? proxy = null)
        {
            var source = new FakeEventSource(WatchDirectory);
            var adapter = new WatchFolderCameraAdapter(Sessions, Assets, probe, new TetherPairingService(Assets), proxy ?? new FakeProxyCache(), transfer ?? new RecordingTransfer(), new NullAudit(), new NullNotifications(), _ => source);
            return (adapter, source);
        }
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }

    private static TetherSessionRecord RunningRecord(Setup setup, DateTimeOffset now, bool copyToProject) => new(Guid.NewGuid(), null, CameraProviderType.WatchFolder, setup.WatchDirectory,
        WatchFolderPathPolicy.NormalizeDirectory(setup.WatchDirectory), TetherSessionState.Running, now.AddMinutes(-1), now.AddMinutes(-1), true, copyToProject,
        copyToProject ? setup.Temp.Combine("project") : null, false, null, now, CreatedAtUtc: now.AddMinutes(-1));

    private sealed class FakeEventSource(string directory) : IWatchFolderEventSource
    {
        public event EventHandler<WatchFolderEvent>? EventReceived; public string Directory { get; } = directory; public bool IncludeSubdirectories => false;
        public void Start() { } public void Stop() { } public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Publish(WatchFolderEventKind kind, string? path = null) => EventReceived?.Invoke(this, new(kind, path, null, DateTimeOffset.UtcNow));
    }

    private sealed class StableProbe : IFileStabilityProbe
    {
        public Task<FileStabilityResult> WaitForStableAsync(string path, CancellationToken cancellationToken = default)
        {
            var info = new FileInfo(path); return Task.FromResult(new FileStabilityResult(TetherStabilityState.Stable, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
        }
    }

    private sealed class SequenceProbe(params FileStabilityResult[] results) : IFileStabilityProbe
    {
        private readonly Queue<FileStabilityResult> _results = new(results);
        public Task<FileStabilityResult> WaitForStableAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(_results.Dequeue());
    }

    private sealed class ThrowThenStableProbe : IFileStabilityProbe
    {
        private int _calls;
        public Task<FileStabilityResult> WaitForStableAsync(string path, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1) throw new IOException("simulated candidate failure");
            var info = new FileInfo(path); return Task.FromResult(new FileStabilityResult(TetherStabilityState.Stable, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
        }
    }

    private sealed class FakeProxyCache : ITetherProxyCache
    {
        public Task<string?> GetOrCreateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => Task.FromResult<string?>("proxy-key");
        public string? ResolvePath(string? cacheKey) => null;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CountingProxyCache : ITetherProxyCache
    {
        public int CreateCalls { get; private set; }
        public Task<string?> GetOrCreateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) { CreateCalls++; return Task.FromResult<string?>("new-proxy"); }
        public string? ResolvePath(string? cacheKey) => null;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingTransfer : ICameraTransferService
    {
        public int ProjectCalls { get; private set; } public int BackupCalls { get; private set; }
        public Task<TetherCopyResult> CopyToProjectAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default) { ProjectCalls++; return Task.FromResult(new TetherCopyResult(asset.Id, Guid.NewGuid(), Path.Combine(destinationRoot, asset.FileName), TetherProcessingState.Copied)); }
        public Task<TetherCopyResult> CopyToBackupAsync(TetherAssetRecord asset, string destinationRoot, bool verifySha256, CancellationToken cancellationToken = default) { BackupCalls++; return Task.FromResult(new TetherCopyResult(asset.Id, Guid.NewGuid(), Path.Combine(destinationRoot, asset.FileName), TetherProcessingState.Copied)); }
    }

    private sealed class NullAudit : IAuditLogService
    {
        public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullNotifications : INotificationCenter
    {
        public event EventHandler<NotificationMessage>? Published { add { } remove { } }
        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void NotifyPersisted(NotificationMessage message) { }
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>([]);
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
