using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class CalendarWorkflowSchemaMigration : IMigration
{
    public int Version => 5;
    public string Name => "CalendarWorkflowCompletionTimestamp";

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using (var add = connection.CreateCommand())
        {
            add.Transaction = transaction;
            add.CommandText = "ALTER TABLE ShootBookings ADD COLUMN ShotCompletedAtUtc TEXT NULL;";
            await add.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var backfill = connection.CreateCommand();
        backfill.Transaction = transaction;
            backfill.CommandText = "UPDATE ShootBookings SET ShotCompletedAtUtc=UpdatedAtUtc WHERE ShotCompletedAtUtc IS NULL AND Status IN ('Shooting','Completed','AwaitingSelectionDelivery','AwaitingSelection','Selected','AwaitingRetouch','Retouched','AwaitingDelivery');";
        await backfill.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
