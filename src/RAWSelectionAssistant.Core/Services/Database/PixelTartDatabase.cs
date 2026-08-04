using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class PixelTartDatabase : IPixelTartDatabase
{
    private readonly string _databasePath;
    private volatile bool _readOnly;

    public PixelTartDatabase(string? databasePath = null)
    {
        _databasePath = databasePath ?? AppDataPaths.DatabaseFile;
    }

    public string DatabasePath => _databasePath;
    public bool IsReadOnly => _readOnly;
    public void EnterReadOnlyRecoveryMode() => _readOnly = true;

    public async Task<SqliteConnection> OpenConnectionAsync(bool write = false, CancellationToken cancellationToken = default)
    {
        if (write && _readOnly)
        {
            throw new InvalidOperationException("Database is in read-only recovery mode.");
        }

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory) && !File.Exists(_databasePath))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = _readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 5
        };
        var connection = new SqliteConnection(builder.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ConfigureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (connection.State == System.Data.ConnectionState.Open && !connection.ConnectionString.Contains("Mode=ReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            await using var wal = connection.CreateCommand();
            wal.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            await wal.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
