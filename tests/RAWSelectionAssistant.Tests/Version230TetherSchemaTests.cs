using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Services.Tethering;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230TetherSchemaTests
{
    [TestMethod]
    public async Task DefaultMigration_RecordsSchemaVersionFour()
    {
        using var setup = await SetupAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(Version) FROM SchemaInfo;";
        Assert.AreEqual(4L, (long)(await command.ExecuteScalarAsync())!);
    }

    [TestMethod]
    [DataRow("TetherSessions")]
    [DataRow("TetherAssets")]
    [DataRow("TetherAnnotations")]
    public async Task SchemaThree_CreatesRequiredTetherTable(string table)
    {
        using var setup = await SetupAsync();
        Assert.AreEqual(1L, await CountAsync(setup.Database, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;", ("$name", table)));
    }

    [TestMethod]
    public async Task SchemaThree_AddsExactlyThreeTetherBusinessTables()
    {
        using var setup = await SetupAsync();
        await using var connection = await setup.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'Tether%' ORDER BY name;";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        CollectionAssert.AreEqual(new[] { "TetherAnnotations", "TetherAssets", "TetherSessions" }, names.ToArray());
    }

    [TestMethod]
    public async Task SchemaThree_PreservesFourCalendarTablesAndNoProjectRelationships()
    {
        using var setup = await SetupAsync();
        Assert.AreEqual(4L, await CountAsync(setup.Database, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('ShootBookings','ShootRequirementItems','BookingDocuments','BookingReminders');"));
        Assert.AreEqual(0L, await CountAsync(setup.Database, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ProjectRelationships';"));
    }

    [TestMethod]
    public async Task CurrentSchema_UsesIntegrityCheckAndIsIdempotent()
    {
        using var setup = await SetupAsync();
        var migrator = new DatabaseMigrator(setup.Database, new DatabaseBackupService(setup.Database, setup.Temp.Combine("second-backups")));
        var second = await migrator.MigrateAsync();
        Assert.IsTrue(second.Success);
        Assert.AreEqual(4, second.CurrentVersion);
        Assert.HasCount(0, second.AppliedMigrations);
        await using var connection = await setup.Database.OpenConnectionAsync(); await using var command = connection.CreateCommand(); command.CommandText = "PRAGMA integrity_check;";
        Assert.AreEqual("ok", Convert.ToString(await command.ExecuteScalarAsync()));
    }

    [TestMethod]
    public async Task FailedSchemaThreeMigration_RollsBackAllTetherObjects()
    {
        using var temp = new TempDirectory(); var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db")); var backup = new DatabaseBackupService(database, temp.Combine("backups"));
        Assert.IsTrue((await new DatabaseMigrator(database, backup, [new InitialSchemaMigration(), new CalendarSchemaMigration()]).MigrateAsync()).Success);
        var result = await new DatabaseMigrator(database, backup, [new InitialSchemaMigration(), new CalendarSchemaMigration(), new FailingTetherMigration()]).MigrateAsync();
        Assert.IsFalse(result.Success);
        Assert.AreEqual(0L, await CountAsync(database, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name LIKE 'Tether%';"));
        Assert.AreEqual(2L, await CountAsync(database, "SELECT MAX(Version) FROM SchemaInfo;"));
    }

    [TestMethod]
    public async Task TetherTables_ContainNoBinaryPayloadColumns()
    {
        using var setup = await SetupAsync();
        await using var connection = await setup.Database.OpenConnectionAsync(); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT group_concat(name || ':' || type, '|') FROM pragma_table_info('TetherAssets');";
        var columns = Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
        foreach (var forbidden in new[] { "BLOB", "ImageBytes", "RawBytes", "ThumbnailBytes", "Lut", "Icc" }) Assert.DoesNotContain(forbidden, columns, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task SessionRepository_PersistsCreatedAndLastReconciledTimes()
    {
        using var setup = await SetupAsync(); var repository = new SqliteTetherSessionRepository(setup.Database); var directory = setup.Temp.Combine("watch"); Directory.CreateDirectory(directory);
        var created = DateTimeOffset.UtcNow.AddMinutes(-1); var reconciled = DateTimeOffset.UtcNow; var session = Session(directory) with { CreatedAtUtc = created, LastReconciledAtUtc = reconciled };
        await repository.AddAsync(session); var loaded = await repository.GetAsync(session.Id);
        Assert.AreEqual(created, loaded!.CreatedAtUtc);
        Assert.AreEqual(reconciled, loaded.LastReconciledAtUtc);
    }

    [TestMethod]
    [DataRow("TetherSessions")]
    [DataRow("TetherAssets")]
    [DataRow("TetherAnnotations")]
    public async Task TetherTables_HaveForeignKeyContracts(string table)
    {
        using var setup = await SetupAsync();
        Assert.IsGreaterThan(0L, await CountAsync(setup.Database, $"SELECT COUNT(*) FROM pragma_foreign_key_list('{table}');"));
    }

    [TestMethod]
    [DataRow("Rating")]
    [DataRow("ColorLabel")]
    [DataRow("PhotographerNote")]
    [DataRow("ClientFavorite")]
    [DataRow("ClientNote")]
    [DataRow("IsRejected")]
    public async Task TetherAnnotations_ReservesStageCCompatibleFieldsWithoutIdentityData(string column)
    {
        using var setup = await SetupAsync();
        Assert.AreEqual(1L, await CountAsync(setup.Database, "SELECT COUNT(*) FROM pragma_table_info('TetherAnnotations') WHERE name=$name;", ("$name", column)));
        Assert.AreEqual(0L, await CountAsync(setup.Database, "SELECT COUNT(*) FROM pragma_table_info('TetherAnnotations') WHERE lower(name) LIKE '%face%' OR lower(name) LIKE '%identity%';"));
    }

    [TestMethod]
    public async Task SchemaTwoToThree_CreatesBackupAndPreservesCalendarData()
    {
        using var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        var backup = new DatabaseBackupService(database, temp.Combine("backups"));
        var schemaTwo = new DatabaseMigrator(database, backup, [new InitialSchemaMigration(), new CalendarSchemaMigration()]);
        Assert.IsTrue((await schemaTwo.MigrateAsync()).Success);
        await using (var connection = await database.OpenConnectionAsync(write: true))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO ShootBookings(Id,ProjectId,Title,ClientDisplayName,StartAtUtc,EndAtUtc,TimeZoneId,IsAllDay,Status,Location,ShootingType,ShootingRequirements,PreparationNotes,TotalAmountMinor,DepositAmountMinor,PaidAmountMinor,CurrencyCode,CurrencyScale,ContactName,ContactPhone,AllowOverlap,ConflictOverride,Notes,CreatedAtUtc,UpdatedAtUtc,IsArchived,ArchivedAtUtc) VALUES($id,NULL,'t','c','2026-08-05T01:00:00Z','2026-08-05T02:00:00Z','UTC',0,'Draft',NULL,'Other',NULL,NULL,NULL,NULL,NULL,'CNY',2,NULL,NULL,0,0,NULL,'2026-08-05T00:00:00Z','2026-08-05T00:00:00Z',0,NULL);";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            await command.ExecuteNonQueryAsync();
        }
        var result = await new DatabaseMigrator(database, backup).MigrateAsync();
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.PreviousVersion);
        Assert.AreEqual(4, result.CurrentVersion);
        Assert.IsNotNull(result.BackupPath);
        Assert.IsTrue(File.Exists(result.BackupPath));
        Assert.AreEqual(1L, await CountAsync(database, "SELECT COUNT(*) FROM ShootBookings;"));
    }

    [TestMethod]
    public async Task SessionRepository_PersistsAndStopsWithoutDeletingAssets()
    {
        using var setup = await SetupAsync();
        var sessions = new SqliteTetherSessionRepository(setup.Database);
        var assets = new SqliteTetherAssetRepository(setup.Database);
        var session = Session(setup.Temp.Combine("watch"));
        Directory.CreateDirectory(session.WatchDirectory);
        await sessions.AddAsync(session);
        var asset = Asset(session, setup.Temp.CreateFile("watch/a.jpg", [1, 2, 3]));
        await assets.UpsertDiscoveredAsync(asset);
        await sessions.UpdateAsync(session with { State = TetherSessionState.Stopped, StoppedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
        Assert.AreEqual(TetherSessionState.Stopped, (await sessions.GetAsync(session.Id))!.State);
        Assert.HasCount(1, await assets.ListBySessionAsync(session.Id));
        Assert.IsTrue(File.Exists(asset.SourcePath));
    }

    [TestMethod]
    public async Task AssetRepository_DeduplicatesBySessionAndNormalizedPath()
    {
        using var setup = await SetupAsync();
        var sessions = new SqliteTetherSessionRepository(setup.Database);
        var assets = new SqliteTetherAssetRepository(setup.Database);
        var session = Session(setup.Temp.Combine("watch")); Directory.CreateDirectory(session.WatchDirectory); await sessions.AddAsync(session);
        var path = setup.Temp.CreateFile("watch/a.jpg", [1]);
        await assets.UpsertDiscoveredAsync(Asset(session, path));
        await assets.UpsertDiscoveredAsync(Asset(session, path) with { Id = Guid.NewGuid() });
        Assert.HasCount(1, await assets.ListBySessionAsync(session.Id));
    }

    [TestMethod]
    public async Task AnnotationRepository_UpsertsSingleLocalAnnotation()
    {
        using var setup = await SetupAsync();
        var sessions = new SqliteTetherSessionRepository(setup.Database); var assets = new SqliteTetherAssetRepository(setup.Database); var annotations = new SqliteTetherAnnotationRepository(setup.Database);
        var session = Session(setup.Temp.Combine("watch")); Directory.CreateDirectory(session.WatchDirectory); await sessions.AddAsync(session);
        var asset = await assets.UpsertDiscoveredAsync(Asset(session, setup.Temp.CreateFile("watch/a.jpg", [1])));
        var now = DateTimeOffset.UtcNow;
        await annotations.UpsertAsync(new(Guid.NewGuid(), asset.Id, 3, "红", "第一次", now, now));
        await annotations.UpsertAsync(new(Guid.NewGuid(), asset.Id, 5, "绿", "更新", now, now.AddSeconds(1)));
        var loaded = await annotations.GetByAssetAsync(asset.Id);
        Assert.AreEqual(5, loaded!.Rating);
        Assert.AreEqual("更新", loaded.PhotographerNote);
        Assert.AreEqual(1L, await CountAsync(setup.Database, "SELECT COUNT(*) FROM TetherAnnotations;"));
    }

    [TestMethod]
    public async Task RejectedAnnotation_IsOnlyAMarkerAndKeepsAssetFile()
    {
        using var setup = await SetupAsync();
        var sessions = new SqliteTetherSessionRepository(setup.Database); var assets = new SqliteTetherAssetRepository(setup.Database); var annotations = new SqliteTetherAnnotationRepository(setup.Database);
        var session = Session(setup.Temp.Combine("watch")); Directory.CreateDirectory(session.WatchDirectory); await sessions.AddAsync(session);
        var path = setup.Temp.CreateFile("watch/keep.jpg", [1, 2, 3]); var asset = await assets.UpsertDiscoveredAsync(Asset(session, path)); var now = DateTimeOffset.UtcNow;
        await annotations.UpsertAsync(new(Guid.NewGuid(), asset.Id, 0, null, null, now, now, IsRejected: true));
        Assert.IsTrue((await annotations.GetByAssetAsync(asset.Id))!.IsRejected);
        Assert.IsTrue(File.Exists(path));
        Assert.IsNotNull(await assets.GetAsync(asset.Id));
    }

    [TestMethod]
    public async Task AnnotationRating_RejectsOutOfRangeValue()
    {
        using var setup = await SetupAsync();
        var session = Session(setup.Temp.Combine("watch")); Directory.CreateDirectory(session.WatchDirectory); var sessions = new SqliteTetherSessionRepository(setup.Database); await sessions.AddAsync(session);
        var asset = await new SqliteTetherAssetRepository(setup.Database).UpsertDiscoveredAsync(Asset(session, setup.Temp.CreateFile("watch/a.jpg", [1])));
        await using var connection = await setup.Database.OpenConnectionAsync(write: true);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO TetherAnnotations(Id,AssetId,Rating,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$asset,6,$at,$at);";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("$asset", asset.Id.ToString("D")); command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        await Assert.ThrowsExactlyAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
    }

    [TestMethod]
    public async Task ActiveDirectoryUniqueIndex_PreventsTwoRunningSessions()
    {
        using var setup = await SetupAsync();
        var repository = new SqliteTetherSessionRepository(setup.Database);
        var directory = setup.Temp.Combine("watch"); Directory.CreateDirectory(directory);
        var first = Session(directory); await repository.AddAsync(first);
        await Assert.ThrowsExactlyAsync<SqliteException>(() => repository.AddAsync(Session(directory)));
    }

    private static async Task<long> CountAsync(PixelTartDatabase database, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await database.OpenConnectionAsync(); await using var command = connection.CreateCommand(); command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static TetherSessionRecord Session(string directory)
    {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), null, CameraProviderType.WatchFolder, Path.GetFullPath(directory), WatchFolderPathPolicy.NormalizeDirectory(directory), TetherSessionState.Running, now, now, false, false, null, false, null, now);
    }

    private static TetherAssetRecord Asset(TetherSessionRecord session, string path)
    {
        var info = new FileInfo(path); var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), session.Id, null, info.FullName, WatchFolderPathPolicy.NormalizePath(path), info.Name, info.Extension.ToLowerInvariant(), TetherMediaKind.PreviewImage,
            info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), now, TetherStabilityState.Stable, TetherProcessingState.Ready, TetherPreviewState.None, now, now);
    }

    private static async Task<Setup> SetupAsync()
    {
        var temp = new TempDirectory(); var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        var result = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
        Assert.IsTrue(result.Success); return new(temp, database);
    }

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database) : IDisposable
    {
        public TempDirectory Temp { get; } = temp; public PixelTartDatabase Database { get; } = database;
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }

    private sealed class FailingTetherMigration : IMigration
    {
        public int Version => 3; public string Name => "FailingTether";
        public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "CREATE TABLE TetherSessions(Id TEXT PRIMARY KEY);";
            await command.ExecuteNonQueryAsync(cancellationToken); throw new SqliteException("simulated", 1);
        }
    }
}
