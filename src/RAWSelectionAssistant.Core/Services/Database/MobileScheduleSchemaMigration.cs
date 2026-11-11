using Microsoft.Data.Sqlite;
namespace RAWSelectionAssistant.Core.Services.Database;
public sealed class MobileScheduleSchemaMigration : IMigration
{
    public int Version => 9;
    public string Name => "MobileScheduleVersionedMutations";
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE BookingMutationReceipts(OperationId TEXT PRIMARY KEY,BookingId TEXT NOT NULL,RequestHash TEXT NOT NULL,ResultJson TEXT NOT NULL,CreatedAtUtc TEXT NOT NULL);
            CREATE TABLE ScheduleDevices(Id TEXT PRIMARY KEY,TokenHash TEXT NOT NULL UNIQUE,Name TEXT NOT NULL,RequestHash TEXT NOT NULL,ExpiresAtUtc TEXT NOT NULL,RevokedAtUtc TEXT NULL,CreatedAtUtc TEXT NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
