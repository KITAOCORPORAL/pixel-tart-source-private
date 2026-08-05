using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.FileOperations;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Core.Services.Tethering;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230TetherSafeCopyTests
{
    [TestMethod]
    public async Task ProjectCopy_UsesTaskEngineAndPreservesSource()
    {
        using var setup = await SetupAsync();
        var result = await setup.Service.CopyToProjectAsync(setup.Asset, setup.ProjectDestination, verifySha256: true);
        Assert.AreEqual(TetherProcessingState.Copied, result.State);
        Assert.IsTrue(File.Exists(setup.SourcePath));
        Assert.IsTrue(File.Exists(result.DestinationPath));
        CollectionAssert.AreEqual(File.ReadAllBytes(setup.SourcePath), File.ReadAllBytes(result.DestinationPath!));
        var task = await setup.TaskRepository.GetAsync(result.TaskId!.Value);
        Assert.AreEqual(TaskLifecycleState.Completed, task!.State);
    }

    [TestMethod]
    public async Task CopyConflict_AutoNumbersAndNeverOverwritesExistingFile()
    {
        using var setup = await SetupAsync();
        Directory.CreateDirectory(setup.ProjectDestination);
        var existing = Path.Combine(setup.ProjectDestination, Path.GetFileName(setup.SourcePath));
        await File.WriteAllBytesAsync(existing, [9, 9, 9]);
        var result = await setup.Service.CopyToProjectAsync(setup.Asset, setup.ProjectDestination, verifySha256: false);
        CollectionAssert.AreEqual(new byte[] { 9, 9, 9 }, File.ReadAllBytes(existing));
        Assert.AreNotEqual(existing, result.DestinationPath);
        StringAssert.Contains(Path.GetFileName(result.DestinationPath), "(1)");
        Assert.IsTrue(File.Exists(setup.SourcePath));
    }

    [TestMethod]
    public async Task SuccessfulCopy_WritesUndoJournalButDoesNotInvokeUndo()
    {
        using var setup = await SetupAsync();
        var result = await setup.Service.CopyToProjectAsync(setup.Asset, setup.ProjectDestination, false);
        var entries = await setup.UndoRepository.ListAsync(result.TaskId!.Value);
        Assert.HasCount(1, entries);
        Assert.AreEqual(FileOperationType.DeleteCreatedOutput, entries[0].ReverseOperation);
        Assert.AreEqual(UndoJournalState.Pending, entries[0].State);
        Assert.IsTrue(File.Exists(result.DestinationPath));
        Assert.IsTrue(File.Exists(setup.SourcePath));
    }

    [TestMethod]
    public async Task InvalidDestination_ReturnsNeedsAttentionAndKeepsSource()
    {
        using var setup = await SetupAsync();
        var destinationInsideSource = Path.Combine(Path.GetDirectoryName(setup.SourcePath)!, "nested-output");
        var result = await setup.Service.CopyToBackupAsync(setup.Asset, destinationInsideSource, false);
        Assert.AreEqual(TetherProcessingState.NeedsAttention, result.State);
        Assert.IsTrue(File.Exists(setup.SourcePath));
        Assert.IsFalse(File.Exists(Path.Combine(destinationInsideSource, Path.GetFileName(setup.SourcePath))));
    }

    [TestMethod]
    public async Task BackupFailure_DoesNotUndoSuccessfulProjectCopy()
    {
        using var setup = await SetupAsync();
        var project = await setup.Service.CopyToProjectAsync(setup.Asset, setup.ProjectDestination, false);
        var backup = await setup.Service.CopyToBackupAsync(setup.Asset, Path.Combine(Path.GetDirectoryName(setup.SourcePath)!, "inside"), false);
        Assert.AreEqual(TetherProcessingState.Copied, project.State);
        Assert.AreEqual(TetherProcessingState.NeedsAttention, backup.State);
        Assert.IsTrue(File.Exists(project.DestinationPath));
        Assert.IsTrue(File.Exists(setup.SourcePath));
    }

    [TestMethod]
    public async Task DatabaseAssociationFailure_ReturnsPartiallyCompletedAndKeepsBothFiles()
    {
        using var setup = await SetupAsync(useThrowingRepository: true);
        var result = await setup.Service.CopyToProjectAsync(setup.Asset, setup.ProjectDestination, false);
        Assert.AreEqual(TetherProcessingState.PartiallyCompleted, result.State);
        Assert.IsTrue(File.Exists(setup.SourcePath));
        Assert.IsTrue(File.Exists(result.DestinationPath));
        Assert.AreEqual(TaskLifecycleState.PartiallyCompleted, (await setup.TaskRepository.GetAsync(result.TaskId!.Value))!.State);
    }

    [TestMethod]
    public async Task CopyCommand_ReturnsOnlyAfterTerminalStateIsPersisted()
    {
        using var setup = await SetupAsync();
        var result = await setup.Service.CopyToProjectAsync(setup.Asset, setup.ProjectDestination, false);
        var freshRepository = new SqliteTaskRepository(new PixelTartDatabase(setup.Database.DatabasePath));
        var persisted = await freshRepository.GetAsync(result.TaskId!.Value);
        Assert.IsNotNull(persisted);
        Assert.IsTrue(TaskStateMachine.IsTerminal(persisted.State));
    }

    [TestMethod]
    public async Task TetherAuditMessages_DoNotContainFullPathsOrFileNames()
    {
        using var setup = await SetupAsync();
        await setup.Service.CopyToProjectAsync(setup.Asset, setup.ProjectDestination, true);
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT group_concat(SanitizedMessage,'|') FROM AuditLogs;";
        var messages = Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
        Assert.DoesNotContain(setup.SourcePath, messages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFileName(setup.SourcePath), messages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(setup.ProjectDestination, messages, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void SafeCopyImplementation_ReusesApprovedStackAndContainsNoMoveOrSourceDelete()
    {
        var text = Text("src/RAWSelectionAssistant.Core/Services/Tethering/TetherSafeCopyService.cs");
        var bridge = Text("src/RAWSelectionAssistant.Core/Services/Tasks/TaskOperationBridge.cs");
        foreach (var required in new[] { "TaskOperationBridge", "FileOperationPlan", "FileOperationType.Copy", "FileConflictPolicy.AutoNumber", "AwaitableProgress", "DrainAsync", "IFileVerificationService", "IAuditLogService", "INotificationCenter" })
            StringAssert.Contains(text, required);
        StringAssert.Contains(bridge, "WaitForCompletionAsync");
        StringAssert.Contains(bridge, "await engine.WaitForCompletionAsync");
        Assert.DoesNotContain("File.Move", text, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FileOperationType.Move", text, StringComparison.Ordinal);
    }

    private static async Task<Setup> SetupAsync(bool useThrowingRepository = false)
    {
        var temp = new TempDirectory(); var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var sessionRepository = new SqliteTetherSessionRepository(database); var sqliteAssets = new SqliteTetherAssetRepository(database);
        var watch = temp.Combine("watch"); Directory.CreateDirectory(watch); var source = temp.CreateFile("watch/unique-customer-file.jpg", Enumerable.Range(0, 128).Select(value => (byte)value).ToArray()); var now = DateTimeOffset.UtcNow;
        var session = new TetherSessionRecord(Guid.NewGuid(), null, CameraProviderType.WatchFolder, watch, WatchFolderPathPolicy.NormalizeDirectory(watch), TetherSessionState.Running, now, now, true, false, null, false, null, now);
        await sessionRepository.AddAsync(session);
        var info = new FileInfo(source); var asset = new TetherAssetRecord(Guid.NewGuid(), session.Id, null, source, WatchFolderPathPolicy.NormalizePath(source), info.Name, info.Extension, TetherMediaKind.PreviewImage,
            info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), now, TetherStabilityState.Stable, TetherProcessingState.Ready, TetherPreviewState.Ready, now, now);
        await sqliteAssets.UpsertDiscoveredAsync(asset);

        var verification = new FileVerificationService(); var undoRepository = new SqliteUndoJournalRepository(database);
        var planner = new FileOperationPlanner(new FileConflictResolver()); var executor = new FileOperationExecutor(new FileOperationValidator(), verification, undoRepository, database);
        var bridge = new TaskOperationBridge(); var audit = new AuditLogService(database); var notifications = new NotificationCenter(database, TimeSpan.Zero);
        var taskRepository = new SqliteTaskRepository(database); var engine = new TaskEngine(taskRepository, new ConservativeTaskScheduler(), [bridge], audit, notifications, TimeSpan.Zero); bridge.Attach(engine);
        ITetherAssetRepository assetRepository = useThrowingRepository ? new ThrowingAssetRepository(sqliteAssets) : sqliteAssets;
        var service = new TetherSafeCopyService(assetRepository, planner, executor, verification, bridge, audit, notifications);
        return new(temp, database, source, temp.Combine("project"), asset, service, taskRepository, undoRepository);
    }

    private static string Text(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return File.ReadAllText(Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar)));
        throw new DirectoryNotFoundException();
    }

    private sealed class ThrowingAssetRepository(ITetherAssetRepository inner) : ITetherAssetRepository
    {
        public Task<TetherAssetRecord> UpsertDiscoveredAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => inner.UpsertDiscoveredAsync(asset, cancellationToken);
        public Task UpdateAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default) => throw new Microsoft.Data.Sqlite.SqliteException("simulated", 5);
        public Task<TetherAssetRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetAsync(id, cancellationToken);
        public Task<TetherAssetRecord?> GetByPathAsync(Guid sessionId, string normalizedPath, CancellationToken cancellationToken = default) => inner.GetByPathAsync(sessionId, normalizedPath, cancellationToken);
        public Task<IReadOnlyList<TetherAssetRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => inner.ListBySessionAsync(sessionId, cancellationToken);
        public Task<bool> PairAsync(Guid sessionId, Guid leftAssetId, Guid rightAssetId, string pairingKey, CancellationToken cancellationToken = default) => inner.PairAsync(sessionId, leftAssetId, rightAssetId, pairingKey, cancellationToken);
    }

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database, string sourcePath, string projectDestination, TetherAssetRecord asset,
        TetherSafeCopyService service, SqliteTaskRepository taskRepository, SqliteUndoJournalRepository undoRepository) : IDisposable
    {
        public TempDirectory Temp { get; } = temp; public PixelTartDatabase Database { get; } = database; public string SourcePath { get; } = sourcePath;
        public string ProjectDestination { get; } = projectDestination; public TetherAssetRecord Asset { get; } = asset; public TetherSafeCopyService Service { get; } = service;
        public SqliteTaskRepository TaskRepository { get; } = taskRepository; public SqliteUndoJournalRepository UndoRepository { get; } = undoRepository;
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }
}
