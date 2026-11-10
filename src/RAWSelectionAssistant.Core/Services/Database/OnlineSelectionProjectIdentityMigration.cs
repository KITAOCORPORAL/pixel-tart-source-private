using Microsoft.Data.Sqlite;
namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class OnlineSelectionProjectIdentityMigration : IMigration
{
    public int Version => 8;
    public string Name => "OnlineSelectionProjectIdentity";
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE Projects ADD COLUMN SourceOnlineSelectionProjectId TEXT NULL;
            CREATE UNIQUE INDEX IX_Projects_OnlineSelectionIdentity ON Projects(SourceOnlineSelectionProjectId)
                WHERE SourceOnlineSelectionProjectId IS NOT NULL;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
