using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class BookingSyncLifecycleSchemaMigration : IMigration
{
    public int Version => 6;
    public string Name => "BookingSyncLifecycle";

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            "ALTER TABLE ShootBookings ADD COLUMN PreBufferMinutes INTEGER NOT NULL DEFAULT 0 CHECK(PreBufferMinutes BETWEEN 0 AND 1440);",
            "ALTER TABLE ShootBookings ADD COLUMN PostBufferMinutes INTEGER NOT NULL DEFAULT 0 CHECK(PostBufferMinutes BETWEEN 0 AND 1440);",
            "ALTER TABLE ShootBookings ADD COLUMN HoldExpiresAtUtc TEXT NULL;",
            "ALTER TABLE ShootBookings ADD COLUMN PaymentState TEXT NOT NULL DEFAULT 'Unknown';",
            "ALTER TABLE ShootBookings ADD COLUMN Revision INTEGER NOT NULL DEFAULT 1 CHECK(Revision > 0);",
            "ALTER TABLE ShootBookings ADD COLUMN DeviceId TEXT NOT NULL DEFAULT 'desktop';",
            "ALTER TABLE ShootBookings ADD COLUMN DeletedAtUtc TEXT NULL;",
            "CREATE INDEX IX_ShootBookings_HoldExpiry ON ShootBookings(Status, HoldExpiresAtUtc) WHERE IsArchived=0 AND Status='Tentative';"
        };
        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
