using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230StageCLiveMonitorCoreTests
{
    [TestMethod]
    public void StageC_KeepsSchemaVersionThreeAndExistingTetherTablesOnly()
    {
        var schema = Text("src/RAWSelectionAssistant.Core/Services/Database/TetherSchemaMigration.cs");
        StringAssert.Contains(schema, "public int Version => 3;");
        Assert.AreEqual(3, Count(schema, "CREATE TABLE "));
        foreach (var table in new[] { "TetherSessions", "TetherAssets", "TetherAnnotations" }) StringAssert.Contains(schema, $"CREATE TABLE {table}");
        Assert.DoesNotContain("ProjectRelationships", schema, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task AnnotationRepository_UpsertsAllStageCFieldsAtomically()
    {
        using var setup = await SetupAsync();
        var now = DateTimeOffset.UtcNow;
        var annotation = new TetherAnnotationRecord(Guid.NewGuid(), setup.Asset.Id, 5, "绿", "摄影师备注", now, now, true, "客户备注", true);
        await setup.Annotations.UpsertAsync(annotation);
        var loaded = await setup.Annotations.GetByAssetAsync(setup.Asset.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(5, loaded.Rating); Assert.AreEqual("绿", loaded.ColorLabel); Assert.AreEqual("摄影师备注", loaded.PhotographerNote);
        Assert.IsTrue(loaded.ClientFavorite); Assert.AreEqual("客户备注", loaded.ClientNote); Assert.IsTrue(loaded.IsRejected);
    }

    [TestMethod]
    public async Task AnnotationRepository_OneCurrentRecordPerAsset()
    {
        using var setup = await SetupAsync(); var now = DateTimeOffset.UtcNow;
        await setup.Annotations.UpsertAsync(new(Guid.NewGuid(), setup.Asset.Id, 1, "红", null, now, now));
        await setup.Annotations.UpsertAsync(new(Guid.NewGuid(), setup.Asset.Id, 4, "蓝", null, now, now.AddSeconds(1), true));
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM TetherAnnotations WHERE AssetId=$asset;"; command.Parameters.AddWithValue("$asset", setup.Asset.Id.ToString("D"));
        Assert.AreEqual(1L, (long)(await command.ExecuteScalarAsync())!);
        Assert.AreEqual(4, (await setup.Annotations.GetByAssetAsync(setup.Asset.Id))!.Rating);
    }

    [TestMethod]
    public async Task AnnotationRepository_NewConnectionImmediatelyReadsSavedValue()
    {
        using var setup = await SetupAsync(); var now = DateTimeOffset.UtcNow;
        await setup.Annotations.UpsertAsync(new(Guid.NewGuid(), setup.Asset.Id, 3, "黄", "本地", now, now, true, "确认", false));
        var restarted = new SqliteTetherAnnotationRepository(setup.Database);
        var loaded = await restarted.GetByAssetAsync(setup.Asset.Id);
        Assert.IsNotNull(loaded); Assert.AreEqual(3, loaded.Rating); Assert.IsTrue(loaded.ClientFavorite);
    }

    [TestMethod]
    public async Task AnnotationRepository_FailedUpdateRollsBackAndKeepsPreviousRecord()
    {
        using var setup = await SetupAsync(); var now = DateTimeOffset.UtcNow;
        await setup.Annotations.UpsertAsync(new(Guid.NewGuid(), setup.Asset.Id, 2, "红", "保留", now, now));
        await using (var connection = await setup.Database.OpenConnectionAsync(write: true))
        {
            await using var trigger = connection.CreateCommand();
            trigger.CommandText = "CREATE TRIGGER FailStageCAnnotation BEFORE UPDATE ON TetherAnnotations BEGIN SELECT RAISE(ABORT, 'test failure'); END;";
            await trigger.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsExactlyAsync<SqliteException>(() => setup.Annotations.UpsertAsync(new(Guid.NewGuid(), setup.Asset.Id, 5, "蓝", "不能落库", now, now.AddMinutes(1))));
        var loaded = await setup.Annotations.GetByAssetAsync(setup.Asset.Id);
        Assert.IsNotNull(loaded); Assert.AreEqual(2, loaded.Rating); Assert.AreEqual("保留", loaded.PhotographerNote);
    }

    [TestMethod]
    public async Task AnnotationRepository_ListBySessionReturnsOnlyCurrentSession()
    {
        using var setup = await SetupAsync(); var now = DateTimeOffset.UtcNow;
        await setup.Annotations.UpsertAsync(new(Guid.NewGuid(), setup.Asset.Id, 4, null, null, now, now));
        var result = await setup.Annotations.ListBySessionAsync(setup.Session.Id);
        Assert.HasCount(1, result); Assert.AreEqual(setup.Asset.Id, result[0].AssetId);
    }

    [TestMethod]
    public async Task AnnotationService_ValidatesRatingWithoutWriting()
    {
        var repository = new RecordingAnnotationRepository(); var service = new TetherAnnotationService(repository); var now = DateTimeOffset.UtcNow;
        var result = await service.SaveAsync(new(Guid.NewGuid(), Guid.NewGuid(), 6, null, null, now, now));
        Assert.IsFalse(result.Success); Assert.AreEqual(0, repository.SaveCount);
    }

    [TestMethod]
    public async Task AnnotationService_ValidatesColorWithoutWriting()
    {
        var repository = new RecordingAnnotationRepository(); var service = new TetherAnnotationService(repository); var now = DateTimeOffset.UtcNow;
        var result = await service.SaveAsync(new(Guid.NewGuid(), Guid.NewGuid(), 3, "橙", null, now, now));
        Assert.IsFalse(result.Success); Assert.AreEqual(0, repository.SaveCount);
    }

    [TestMethod]
    public async Task AnnotationService_DatabaseFailureReturnsExplicitFailure()
    {
        var service = new TetherAnnotationService(new ThrowingAnnotationRepository()); var now = DateTimeOffset.UtcNow;
        var result = await service.SaveAsync(new(Guid.NewGuid(), Guid.NewGuid(), 3, "蓝", "不会成功", now, now));
        Assert.IsFalse(result.Success); Assert.AreEqual(ErrorCodeCatalog.DatabaseUnavailable, result.ErrorCode); Assert.IsNull(result.Annotation);
    }

    [TestMethod]
    public async Task AnnotationAudit_DoesNotContainEitherNoteBody()
    {
        var repository = new RecordingAnnotationRepository(); var audit = new RecordingAuditLog(); var service = new TetherAnnotationService(repository, audit); var now = DateTimeOffset.UtcNow;
        var result = await service.SaveAsync(new(Guid.NewGuid(), Guid.NewGuid(), 5, "紫", "秘密摄影师备注", now, now, true, "秘密客户备注"));
        Assert.IsTrue(result.Success); Assert.DoesNotContain("秘密", audit.Message, StringComparison.Ordinal); Assert.DoesNotContain("备注", audit.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RejectedAnnotation_NeverDeletesOrMovesSourceFile()
    {
        using var setup = await SetupAsync(); var bytes = await File.ReadAllBytesAsync(setup.Asset.SourcePath); var now = DateTimeOffset.UtcNow;
        var service = new TetherAnnotationService(setup.Annotations);
        Assert.IsTrue((await service.SaveAsync(new(Guid.NewGuid(), setup.Asset.Id, 0, null, null, now, now, false, null, true))).Success);
        Assert.IsTrue(File.Exists(setup.Asset.SourcePath)); CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(setup.Asset.SourcePath));
        Assert.IsTrue((await service.SaveAsync(new(Guid.NewGuid(), setup.Asset.Id, 0, null, null, now, now.AddSeconds(1), false, null, false))).Success);
        Assert.IsTrue(File.Exists(setup.Asset.SourcePath));
    }

    [TestMethod]
    public void RejectedImplementation_HasNoDeleteMoveRecycleOrUndoJournalCall()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs");
        var start = source.IndexOf("private async Task ToggleRejectedAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task SaveAnnotationAsync", start, StringComparison.Ordinal);
        var method = source[start..end];
        foreach (var forbidden in new[] { "File.Delete", "File.Move", "Recycle", "UndoJournal", "Remove(" }) Assert.DoesNotContain(forbidden, method, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void AnnotationImplementation_DoesNotWriteXmpOrImageBinary()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/Tethering/TetherMonitoringServices.cs") + Text("src/RAWSelectionAssistant.Core/Services/Database/SqliteTetherRepositories.cs");
        foreach (var forbidden in new[] { "Xmp", "Bitmap", "BLOB", "File.WriteAllBytes" }) Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void SelectionCoordinator_DefaultAutoLatestSelectsNewest()
    {
        var coordinator = new LiveSelectionCoordinator(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        Assert.AreEqual(first, coordinator.OnReady(first)); Assert.AreEqual(second, coordinator.OnReady(second)); Assert.AreEqual(second, coordinator.SelectedAssetId);
    }

    [TestMethod]
    [DataRow("locked")]
    [DataRow("actual")]
    [DataRow("compare")]
    [DataRow("note")]
    [DataRow("interaction")]
    public void SelectionCoordinator_BusyUserStatePreventsFocusSteal(string state)
    {
        var coordinator = new LiveSelectionCoordinator(); var first = Guid.NewGuid(); var second = Guid.NewGuid(); coordinator.OnReady(first);
        switch (state)
        {
            case "locked": coordinator.SetLocked(true); break;
            case "actual": coordinator.IsActualSize = true; break;
            case "compare": coordinator.IsComparing = true; break;
            case "note": coordinator.IsEditingNote = true; break;
            case "interaction": coordinator.HasActiveInteraction = true; break;
        }
        Assert.IsNull(coordinator.OnReady(second)); Assert.AreEqual(first, coordinator.SelectedAssetId); Assert.AreEqual(1, coordinator.NewAssetCount);
    }

    [TestMethod]
    public void SelectionCoordinator_ManualOlderSelectionPreventsFocusSteal()
    {
        var coordinator = new LiveSelectionCoordinator(); var first = Guid.NewGuid(); var latest = Guid.NewGuid(); var incoming = Guid.NewGuid();
        coordinator.OnReady(first); coordinator.OnReady(latest); coordinator.SelectManually(first);
        Assert.IsNull(coordinator.OnReady(incoming)); Assert.AreEqual(first, coordinator.SelectedAssetId);
    }

    [TestMethod]
    public void SelectionCoordinator_UnlockReturnsLatestAndClearsCounter()
    {
        var coordinator = new LiveSelectionCoordinator(); var first = Guid.NewGuid(); var latest = Guid.NewGuid();
        coordinator.OnReady(first); coordinator.SetLocked(true); coordinator.OnReady(latest);
        Assert.AreEqual(latest, coordinator.UnlockAndSelectLatest()); Assert.AreEqual(0, coordinator.NewAssetCount); Assert.IsFalse(coordinator.IsLocked);
    }

    [TestMethod]
    public async Task MonitoringPrivacySource_LogsOnlyIdentifiersAndOperationResult()
    {
        var repository = new RecordingAnnotationRepository(); var audit = new RecordingAuditLog(); var service = new TetherAnnotationService(repository, audit); var now = DateTimeOffset.UtcNow;
        var assetId = Guid.NewGuid();
        Assert.IsTrue((await service.SaveAsync(new(Guid.NewGuid(), assetId, 4, "绿", @"C:\客户甲\秘密文件.jpg", now, now, true, "客户姓名与电话"))).Success);
        StringAssert.Contains(audit.Message, $"AssetId={assetId:D}"); StringAssert.Contains(audit.Message, "Result=Success");
        Assert.DoesNotContain("客户", audit.Message, StringComparison.Ordinal); Assert.DoesNotContain("秘密文件", audit.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\", audit.Message, StringComparison.Ordinal); Assert.DoesNotContain(".jpg", audit.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string text, string value) { var count = 0; for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) count++; return count; }

    private static async Task<Setup> SetupAsync()
    {
        var temp = new TempDirectory(); var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        Assert.IsTrue((await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync()).Success);
        var sessions = new SqliteTetherSessionRepository(database); var assets = new SqliteTetherAssetRepository(database); var annotations = new SqliteTetherAnnotationRepository(database);
        var watch = temp.Combine("watch"); Directory.CreateDirectory(watch); var now = DateTimeOffset.UtcNow;
        var session = new TetherSessionRecord(Guid.NewGuid(), null, CameraProviderType.WatchFolder, watch, Path.GetFullPath(watch).ToUpperInvariant(), TetherSessionState.Running, now, now, true, false, null, false, null, now);
        await sessions.AddAsync(session);
        var source = temp.CreateFile(Path.Combine("watch", "STAGE_C.jpg"), [1, 2, 3, 4]);
        var asset = new TetherAssetRecord(Guid.NewGuid(), session.Id, null, source, Path.GetFullPath(source).ToUpperInvariant(), Path.GetFileName(source), ".jpg", TetherMediaKind.PreviewImage, 4, now, now,
            TetherStabilityState.Stable, TetherProcessingState.Ready, TetherPreviewState.Ready, now, now);
        asset = await assets.UpsertDiscoveredAsync(asset);
        return new(temp, database, session, asset, annotations);
    }

    private static string Text(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return File.ReadAllText(Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar)));
        throw new DirectoryNotFoundException();
    }

    private sealed record Setup(TempDirectory Temp, PixelTartDatabase Database, TetherSessionRecord Session, TetherAssetRecord Asset, SqliteTetherAnnotationRepository Annotations) : IDisposable
    {
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }

    private sealed class RecordingAnnotationRepository : ITetherAnnotationRepository
    {
        private readonly Dictionary<Guid, TetherAnnotationRecord> _items = [];
        public int SaveCount { get; private set; }
        public Task UpsertAsync(TetherAnnotationRecord annotation, CancellationToken cancellationToken = default) { SaveCount++; _items[annotation.AssetId] = annotation; return Task.CompletedTask; }
        public Task<TetherAnnotationRecord?> GetByAssetAsync(Guid assetId, CancellationToken cancellationToken = default) => Task.FromResult(_items.GetValueOrDefault(assetId));
        public Task<IReadOnlyList<TetherAnnotationRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TetherAnnotationRecord>>(_items.Values.ToArray());
    }

    private sealed class ThrowingAnnotationRepository : ITetherAnnotationRepository
    {
        public Task UpsertAsync(TetherAnnotationRecord annotation, CancellationToken cancellationToken = default) => throw new SqliteException("failed", 5);
        public Task<TetherAnnotationRecord?> GetByAssetAsync(Guid assetId, CancellationToken cancellationToken = default) => Task.FromResult<TetherAnnotationRecord?>(null);
        public Task<IReadOnlyList<TetherAnnotationRecord>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TetherAnnotationRecord>>([]);
    }

    private sealed class RecordingAuditLog : IAuditLogService
    {
        public string Message { get; private set; } = string.Empty;
        public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default) { Message = message; return Task.CompletedTask; }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.StageCLiveMonitorCore", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);
        public string CreateFile(string relative, byte[] bytes) { var path = Combine(relative); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); return path; }
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}
