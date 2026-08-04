using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed record MigrationResult(
    bool Success,
    int PreviousVersion,
    int CurrentVersion,
    IReadOnlyList<string> AppliedMigrations,
    string? BackupPath = null,
    bool IsReadOnlyRecovery = false,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public interface IMigration
{
    int Version { get; }
    string Name { get; }
    Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken);
}

public interface IDatabaseMigrator
{
    int SupportedSchemaVersion { get; }
    Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default);
}

public interface IDatabaseBackupService
{
    Task<string?> BackupAsync(string reason, CancellationToken cancellationToken = default);
}

public interface IDatabaseRecoveryService
{
    Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default);
    Task RestoreAsync(string backupFile, bool userConfirmed, CancellationToken cancellationToken = default);
}

public interface IPixelTartDatabase
{
    string DatabasePath { get; }
    bool IsReadOnly { get; }
    Task<SqliteConnection> OpenConnectionAsync(bool write = false, CancellationToken cancellationToken = default);
}

