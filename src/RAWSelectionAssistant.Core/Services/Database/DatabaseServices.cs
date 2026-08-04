using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class DatabaseBackupService(IPixelTartDatabase database, string? backupRoot = null) : IDatabaseBackupService
{
    private readonly string _backupRoot = backupRoot ?? AppDataPaths.DatabaseBackupDirectory;

    public async Task<string?> BackupAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(database.DatabasePath)) return null;
        var destinationDirectory = Path.Combine(_backupRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(destinationDirectory);
        var destination = Path.Combine(destinationDirectory, "pixel-tart.db");
        await using var source = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using (var checkpoint = source.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        await target.OpenAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(target);
        await File.WriteAllTextAsync(Path.Combine(destinationDirectory, "backup-reason.txt"), reason, cancellationToken).ConfigureAwait(false);
        return destination;
    }
}

public sealed class DatabaseRecoveryService(IPixelTartDatabase database) : IDatabaseRecoveryService
{
    public async Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(database.DatabasePath)) return true;
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            SqliteConnection.ClearAllPools();
            return false;
        }
    }

    public async Task RestoreAsync(string backupFile, bool userConfirmed, CancellationToken cancellationToken = default)
    {
        if (!userConfirmed) throw new InvalidOperationException("Database restore requires explicit user confirmation.");
        if (!File.Exists(backupFile)) throw new FileNotFoundException("Backup database was not found.", backupFile);
        var destinationDirectory = Path.GetDirectoryName(database.DatabasePath)!;
        Directory.CreateDirectory(destinationDirectory);
        var staging = database.DatabasePath + ".restore-" + Guid.NewGuid().ToString("N");
        await using (var input = new FileStream(backupFile, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true))
        await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
        {
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        var verification = new PixelTartDatabase(staging);
        if (!await new DatabaseRecoveryService(verification).VerifyIntegrityAsync(cancellationToken).ConfigureAwait(false))
        {
            File.Delete(staging);
            throw new InvalidDataException("The selected database backup is corrupted.");
        }
        if (File.Exists(database.DatabasePath)) File.Replace(staging, database.DatabasePath, database.DatabasePath + ".pre-restore.bak", true);
        else File.Move(staging, database.DatabasePath);
    }
}

public sealed class DatabaseMigrator : IDatabaseMigrator
{
    private readonly PixelTartDatabase _database;
    private readonly IDatabaseBackupService _backupService;
    private readonly IReadOnlyList<IMigration> _migrations;

    public DatabaseMigrator(PixelTartDatabase database, IDatabaseBackupService backupService, IEnumerable<IMigration>? migrations = null)
    {
        _database = database;
        _backupService = backupService;
        _migrations = (migrations ?? [new InitialSchemaMigration(), new CalendarSchemaMigration()]).OrderBy(x => x.Version).ToArray();
        if (_migrations.Select(x => x.Version).Distinct().Count() != _migrations.Count ||
            _migrations.Select(x => x.Version).Where((version, index) => version != index + 1).Any())
            throw new InvalidOperationException("Database migrations must be unique, contiguous and start at version 1.");
    }

    public int SupportedSchemaVersion => _migrations.Count == 0 ? 0 : _migrations[^1].Version;

    public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var databaseExisted = File.Exists(_database.DatabasePath);
        string? backup = null;
        try
        {
            if (databaseExisted && !await new DatabaseRecoveryService(_database).VerifyIntegrityAsync(cancellationToken).ConfigureAwait(false))
            {
                _database.EnterReadOnlyRecoveryMode();
                return new(false, -1, -1, [], null, true, ErrorCodeCatalog.DatabaseCorrupted, "数据库完整性检查失败，原数据库已保留。");
            }

            var currentVersion = await ReadCurrentVersionAsync(cancellationToken).ConfigureAwait(false);
            if (currentVersion > SupportedSchemaVersion)
            {
                _database.EnterReadOnlyRecoveryMode();
                return new(false, currentVersion, currentVersion, [], null, true, ErrorCodeCatalog.UnsupportedSchemaVersion, "数据库版本高于当前应用支持范围，已阻止写入。");
            }
            if (currentVersion == SupportedSchemaVersion)
                return new(true, currentVersion, currentVersion, []);

            if (databaseExisted) backup = await _backupService.BackupAsync($"schema-{currentVersion}-to-{SupportedSchemaVersion}", cancellationToken).ConfigureAwait(false);
            var applied = new List<string>();
            var previousVersion = currentVersion;
            await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
            foreach (var migration in _migrations.Where(x => x.Version > currentVersion))
            {
                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await migration.ApplyAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                    await ValidateMigrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                    await using var record = connection.CreateCommand();
                    record.Transaction = transaction;
                    record.CommandText = "INSERT INTO SchemaInfo(Version, AppliedAt, ApplicationVersion, MigrationName) VALUES($version,$at,$app,$name);";
                    record.Parameters.AddWithValue("$version", migration.Version);
                    record.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
                    record.Parameters.AddWithValue("$app", Branding.ProductVersion);
                    record.Parameters.AddWithValue("$name", migration.Name);
                    await record.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    currentVersion = migration.Version;
                    applied.Add(migration.Name);
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
            return new(true, previousVersion, currentVersion, applied, backup);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (databaseExisted) _database.EnterReadOnlyRecoveryMode();
            return new(false, -1, -1, [], backup, databaseExisted, ErrorCodeCatalog.MigrationFailed, ex.Message);
        }
    }

    private async Task<int> ReadCurrentVersionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_database.DatabasePath)) return 0;
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='SchemaInfo';";
        if (Convert.ToInt32(await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 0) return 0;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaInfo;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static async Task ValidateMigrationAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.Transaction = transaction;
            foreignKeys.CommandText = "PRAGMA foreign_key_check;";
            await using var reader = await foreignKeys.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Database migration produced a foreign-key violation.");
        }

        await using var integrity = connection.CreateCommand();
        integrity.Transaction = transaction;
        integrity.CommandText = "PRAGMA quick_check;";
        var result = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Database migration integrity verification failed.");
    }
}
