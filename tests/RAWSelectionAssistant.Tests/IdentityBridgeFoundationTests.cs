using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class IdentityBridgeFoundationTests
{
    [TestMethod]
    public async Task ResolverDistinguishesAvailableChangedOfflineMissingAndAmbiguousWithoutWriting()
    {
        using var temp = new TempDirectory();
        var container = await new AssetLibraryContainerService().CreateAsync(temp.Combine("identity.ptlibrary"), "identity");
        var source = temp.Combine("source.bin"); await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
        await using var repository = new SqliteAssetLibraryRepository(container.DatabasePath);
        var imported = await repository.ImportWithIdentitiesAsync([new(source, ComputeContentHash: true)]);
        var identity = imported.Items.Single();
        var resolver = new AssetLibraryIdentityResolver(container, repository);

        var available = await resolver.ResolveAsync(new(container.LibraryId, identity.AssetId, identity.ContentHash));
        Assert.AreEqual(AssetResolutionStatus.Available, available.Status);
        Assert.AreEqual(Path.GetFullPath(source), available.ResolvedPath);

        var changed = await resolver.ResolveAsync(new(container.LibraryId, identity.AssetId, new string('a', 64)));
        Assert.AreEqual(AssetResolutionStatus.Changed, changed.Status);
        File.Delete(source);
        Assert.AreEqual(AssetResolutionStatus.Offline, (await resolver.ResolveAsync(new(container.LibraryId, identity.AssetId))).Status);
        Assert.AreEqual(AssetResolutionStatus.Missing, (await resolver.ResolveAsync(new(container.LibraryId, Guid.NewGuid()))).Status);

        var sourceA = temp.Combine("a.bin"); var sourceB = temp.Combine("b.bin");
        await File.WriteAllBytesAsync(sourceA, [7]); await File.WriteAllBytesAsync(sourceB, [8]);
        var noHash = (await repository.ImportWithIdentitiesAsync([new(sourceA)])).Items.Single();
        await using (var connection = new AssetLibraryDatabase(container.DatabasePath).OpenConnectionAsync(write: true).GetAwaiter().GetResult())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE AssetItems SET ManagedCopyPath=$path,ContentHash=NULL WHERE AssetId=$id;";
            command.Parameters.AddWithValue("$path", sourceB); command.Parameters.AddWithValue("$id", noHash.AssetId.ToString("D"));
            await command.ExecuteNonQueryAsync();
        }
        Assert.AreEqual(AssetResolutionStatus.Ambiguous, (await resolver.ResolveAsync(new(container.LibraryId, noHash.AssetId))).Status);
    }

    [TestMethod]
    public async Task IdentityReturningImportPreservesLegacyDedupAndRefusesAmbiguousHashChoice()
    {
        using var temp = new TempDirectory();
        var database = temp.Combine("library.db");
        var one = temp.Combine("one.raw"); var two = temp.Combine("two.raw"); var three = temp.Combine("three.raw");
        await File.WriteAllBytesAsync(one, [9, 8, 7]); File.Copy(one, two); File.Copy(one, three);
        await using var repository = new SqliteAssetLibraryRepository(database);

        var first = (await repository.ImportWithIdentitiesAsync([new(one, ComputeContentHash: true)])).Items.Single();
        var samePath = (await repository.ImportWithIdentitiesAsync([new(one, ComputeContentHash: true)])).Items.Single();
        var sameContent = (await repository.ImportWithIdentitiesAsync([new(two, ComputeContentHash: true)])).Items.Single();
        Assert.AreEqual(AssetImportIdentityDisposition.Imported, first.Disposition);
        Assert.AreEqual(AssetImportIdentityDisposition.ExistingPath, samePath.Disposition);
        Assert.AreEqual(AssetImportIdentityDisposition.ExistingContent, sameContent.Disposition);
        Assert.AreEqual(first.AssetId, samePath.AssetId); Assert.AreEqual(first.AssetId, sameContent.AssetId);

        var independent = (await repository.ImportWithIdentitiesAsync([
            new(two, ComputeContentHash: true, DuplicateBehavior: AssetDuplicateBehavior.ImportIndependentRecord)
        ])).Items.Single();
        Assert.AreNotEqual(first.AssetId, independent.AssetId);
        var ambiguous = (await repository.ImportWithIdentitiesAsync([new(three, ComputeContentHash: true)])).Items.Single();
        Assert.AreEqual(AssetImportIdentityDisposition.Ambiguous, ambiguous.Disposition);
        Assert.AreEqual(Guid.Empty, ambiguous.AssetId);
        var legacy = await repository.ImportAsync([new(three, ComputeContentHash: true)]);
        Assert.AreEqual(0, legacy.ImportedCount); Assert.AreEqual(1, legacy.SkippedCount);
    }

    [TestMethod]
    public async Task VersionSevenMigrationBacksUpV6AndBridgeWritesAreIdempotentAndConflictSafe()
    {
        using var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("product.db"));
        var backup = new DatabaseBackupService(database, temp.Combine("backups"));
        var v6 = new DatabaseMigrator(database, backup, [
            new InitialSchemaMigration(), new CalendarSchemaMigration(), new TetherSchemaMigration(),
            new BusinessSchemaMigration(), new CalendarWorkflowSchemaMigration(), new BookingSyncLifecycleSchemaMigration()
        ]);
        Assert.AreEqual(6, (await v6.MigrateAsync()).CurrentVersion);
        var tetherAssetId = await SeedTetherAssetAsync(database, temp);

        var upgraded = await new DatabaseMigrator(database, backup).MigrateAsync();
        Assert.IsTrue(upgraded.Success, upgraded.ErrorMessage); Assert.AreEqual(7, upgraded.CurrentVersion);
        Assert.IsTrue(File.Exists(upgraded.BackupPath)); Assert.HasCount(1, upgraded.AppliedMigrations);
        await using (var connection = await database.OpenConnectionAsync())
        {
            Assert.AreEqual("ok", await ScalarAsync(connection, "PRAGMA integrity_check;"));
            Assert.AreEqual(1L, Convert.ToInt64(await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='TetherAssetLibraryLinks';")));
            Assert.AreEqual(1L, Convert.ToInt64(await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='CrossModuleOperations';")));
        }

        var bridge = new IdentityBridgeRepository(database);
        var libraryId = Guid.NewGuid(); var assetId = Guid.NewGuid(); var hash = new string('b', 64);
        var first = await bridge.SaveLinkAsync(tetherAssetId, libraryId, assetId, hash, IdentityBridgeState.Ready);
        var repeated = await bridge.SaveLinkAsync(tetherAssetId, libraryId, assetId, hash, IdentityBridgeState.Ready);
        Assert.AreEqual(first.AssetId, repeated.AssetId); Assert.AreEqual(IdentityBridgeState.Ready, repeated.State);
        var conflict = await bridge.SaveLinkAsync(tetherAssetId, libraryId, Guid.NewGuid(), new string('c', 64), IdentityBridgeState.Ready);
        Assert.AreEqual(assetId, conflict.AssetId); Assert.AreEqual(IdentityBridgeState.Conflict, conflict.State);

        var operation = await bridge.BeginOperationAsync("tether:" + tetherAssetId.ToString("N"), "TetherToLibrary", "TetherAsset", tetherAssetId, libraryId, hash);
        var retry = await bridge.BeginOperationAsync(operation.IdempotencyKey, operation.OperationType, operation.SourceKind, operation.SourceId, libraryId, hash);
        Assert.AreEqual(operation.OperationId, retry.OperationId); Assert.AreEqual(2, retry.AttemptCount);
        var completed = await bridge.CompleteOperationAsync(operation.OperationId, assetId);
        Assert.AreEqual(CrossModuleOperationState.Ready, completed.State); Assert.AreEqual(assetId, completed.AssetId);
        var readyRetry = await bridge.BeginOperationAsync(operation.IdempotencyKey, operation.OperationType, operation.SourceKind, operation.SourceId, libraryId, hash);
        Assert.AreEqual(2, readyRetry.AttemptCount); Assert.AreEqual(CrossModuleOperationState.Ready, readyRetry.State);
    }

    [TestMethod]
    public async Task FailedVersionSevenMigrationRollsBackTablesAndSchemaVersion()
    {
        using var temp = new TempDirectory(); var database = new PixelTartDatabase(temp.Combine("rollback.db"));
        var backup = new DatabaseBackupService(database, temp.Combine("backups"));
        var v6Migrations = new IMigration[] { new InitialSchemaMigration(), new CalendarSchemaMigration(), new TetherSchemaMigration(), new BusinessSchemaMigration(), new CalendarWorkflowSchemaMigration(), new BookingSyncLifecycleSchemaMigration() };
        Assert.AreEqual(6, (await new DatabaseMigrator(database, backup, v6Migrations).MigrateAsync()).CurrentVersion);
        var failed = await new DatabaseMigrator(database, backup, [.. v6Migrations, new FailingIdentityBridgeMigration()]).MigrateAsync();
        Assert.IsFalse(failed.Success);
        await using var connection = await database.OpenConnectionAsync();
        Assert.AreEqual(6L, Convert.ToInt64(await ScalarAsync(connection, "SELECT MAX(Version) FROM SchemaInfo;")));
        Assert.AreEqual(0L, Convert.ToInt64(await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='TetherAssetLibraryLinks';")));
    }

    private static async Task<Guid> SeedTetherAssetAsync(PixelTartDatabase database, TempDirectory temp)
    {
        var session = Guid.NewGuid(); var asset = Guid.NewGuid(); var now = DateTimeOffset.UtcNow.ToString("O"); var path = temp.Combine("capture.raw"); await File.WriteAllBytesAsync(path, [1]);
        await using var connection = await database.OpenConnectionAsync(write: true); await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TetherSessions(Id,ProjectId,ProviderType,WatchDirectory,NormalizedWatchDirectory,State,StartedAtUtc,DiscoveryCutoffUtc,ImportExisting,CopyToProject,CopyToBackup,CreatedAtUtc,UpdatedAtUtc)
            VALUES($session,NULL,'WatchFolder',$dir,$dir,'Stopped',$now,$now,0,0,0,$now,$now);
            INSERT INTO TetherAssets(Id,SessionId,ProjectId,SourcePath,NormalizedSourcePath,FileName,Extension,MediaKind,FileSize,ModifiedAtUtc,FirstSeenAtUtc,ReadyAtUtc,StabilityState,ProcessingState,PreviewState,UpdatedAtUtc)
            VALUES($asset,$session,NULL,$path,$path,'capture.raw','.raw','Raw',1,$now,$now,$now,'Stable','Ready','None',$now);
            """;
        command.Parameters.AddWithValue("$session", session.ToString("D")); command.Parameters.AddWithValue("$asset", asset.ToString("D"));
        command.Parameters.AddWithValue("$dir", temp.Path); command.Parameters.AddWithValue("$path", path); command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(); return asset;
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    { await using var command = connection.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync(); }

    private sealed class FailingIdentityBridgeMigration : IMigration
    {
        public int Version => 7; public string Name => "FailingIdentityBridge";
        public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "CREATE TABLE TetherAssetLibraryLinks(Id TEXT PRIMARY KEY);";
            await command.ExecuteNonQueryAsync(cancellationToken); throw new SqliteException("simulated", 1);
        }
    }
}
