using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

/// <summary>
/// Additive identity bridge between product-owned tether records and portable
/// asset libraries. Portable-library rows remain in their own database.
/// </summary>
public sealed class IdentityBridgeSchemaMigration : IMigration
{
    public int Version => 7;
    public string Name => "CrossModuleAssetIdentityBridge";

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            """
            CREATE TABLE TetherAssetLibraryLinks (
                TetherAssetId TEXT NOT NULL,
                LibraryId TEXT NOT NULL,
                AssetId TEXT NOT NULL,
                SourceContentHash TEXT NULL,
                State TEXT NOT NULL CHECK(State IN ('Pending','Ready','Conflict','Failed')),
                LastErrorCode TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                PRIMARY KEY(TetherAssetId, LibraryId),
                FOREIGN KEY(TetherAssetId) REFERENCES TetherAssets(Id) ON DELETE RESTRICT
            );
            """,
            """
            CREATE TABLE CrossModuleOperations (
                OperationId TEXT NOT NULL PRIMARY KEY,
                IdempotencyKey TEXT NOT NULL UNIQUE,
                OperationType TEXT NOT NULL,
                SourceKind TEXT NOT NULL,
                SourceId TEXT NOT NULL,
                LibraryId TEXT NULL,
                AssetId TEXT NULL,
                State TEXT NOT NULL CHECK(State IN ('Pending','Running','Ready','Conflict','Failed')),
                AttemptCount INTEGER NOT NULL DEFAULT 0 CHECK(AttemptCount >= 0),
                PayloadHash TEXT NULL,
                LastErrorCode TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """,
            "CREATE INDEX IX_TetherAssetLibraryLinks_LibraryAsset ON TetherAssetLibraryLinks(LibraryId, AssetId);",
            "CREATE INDEX IX_TetherAssetLibraryLinks_State ON TetherAssetLibraryLinks(State, UpdatedAtUtc);",
            "CREATE INDEX IX_CrossModuleOperations_Source ON CrossModuleOperations(SourceKind, SourceId, UpdatedAtUtc DESC);",
            "CREATE INDEX IX_CrossModuleOperations_State ON CrossModuleOperations(State, UpdatedAtUtc);"
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
