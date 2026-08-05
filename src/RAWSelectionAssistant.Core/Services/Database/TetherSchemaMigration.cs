using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class TetherSchemaMigration : IMigration
{
    public int Version => 3;
    public string Name => "WatchFolderTetheringMvp";

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            """
            CREATE TABLE TetherSessions (
                Id TEXT NOT NULL PRIMARY KEY,
                ProjectId TEXT NULL,
                ProviderType TEXT NOT NULL CHECK(ProviderType IN ('None','WatchFolder')),
                WatchDirectory TEXT NOT NULL,
                NormalizedWatchDirectory TEXT NOT NULL,
                State TEXT NOT NULL CHECK(State IN ('Running','Stopped','NeedsAttention')),
                StartedAtUtc TEXT NOT NULL,
                DiscoveryCutoffUtc TEXT NOT NULL,
                ImportExisting INTEGER NOT NULL DEFAULT 0 CHECK(ImportExisting IN (0,1)),
                CopyToProject INTEGER NOT NULL DEFAULT 0 CHECK(CopyToProject IN (0,1)),
                ProjectDestination TEXT NULL,
                CopyToBackup INTEGER NOT NULL DEFAULT 0 CHECK(CopyToBackup IN (0,1)),
                BackupDestination TEXT NULL,
                LastReconciledAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                StoppedAtUtc TEXT NULL,
                LastErrorCode TEXT NULL,
                FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL
            );
            """,
            """
            CREATE TABLE TetherAssets (
                Id TEXT NOT NULL PRIMARY KEY,
                SessionId TEXT NOT NULL,
                ProjectId TEXT NULL,
                SourcePath TEXT NOT NULL,
                NormalizedSourcePath TEXT NOT NULL,
                FileName TEXT NOT NULL,
                Extension TEXT NOT NULL,
                MediaKind TEXT NOT NULL CHECK(MediaKind IN ('PreviewImage','Raw','Unsupported')),
                FileSize INTEGER NULL CHECK(FileSize IS NULL OR FileSize >= 0),
                ModifiedAtUtc TEXT NULL,
                FirstSeenAtUtc TEXT NOT NULL,
                ReadyAtUtc TEXT NULL,
                StabilityState TEXT NOT NULL,
                ProcessingState TEXT NOT NULL,
                PreviewState TEXT NOT NULL,
                ProxyCacheKey TEXT NULL,
                PairingKey TEXT NULL,
                PairedAssetId TEXT NULL,
                ProjectCopyTaskId TEXT NULL,
                ProjectCopyPath TEXT NULL,
                BackupCopyTaskId TEXT NULL,
                BackupCopyPath TEXT NULL,
                LastErrorCode TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES TetherSessions(Id) ON DELETE RESTRICT,
                FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL,
                FOREIGN KEY(PairedAssetId) REFERENCES TetherAssets(Id) ON DELETE SET NULL,
                FOREIGN KEY(ProjectCopyTaskId) REFERENCES Tasks(Id) ON DELETE SET NULL,
                FOREIGN KEY(BackupCopyTaskId) REFERENCES Tasks(Id) ON DELETE SET NULL,
                UNIQUE(SessionId, NormalizedSourcePath)
            );
            """,
            """
            CREATE TABLE TetherAnnotations (
                Id TEXT NOT NULL PRIMARY KEY,
                AssetId TEXT NOT NULL UNIQUE,
                Rating INTEGER NOT NULL DEFAULT 0 CHECK(Rating BETWEEN 0 AND 5),
                ColorLabel TEXT NULL,
                PhotographerNote TEXT NULL,
                ClientFavorite INTEGER NOT NULL DEFAULT 0 CHECK(ClientFavorite IN (0,1)),
                ClientNote TEXT NULL,
                IsRejected INTEGER NOT NULL DEFAULT 0 CHECK(IsRejected IN (0,1)),
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY(AssetId) REFERENCES TetherAssets(Id) ON DELETE RESTRICT
            );
            """,
            "CREATE INDEX IX_TetherSessions_State_UpdatedAtUtc ON TetherSessions(State, UpdatedAtUtc DESC);",
            "CREATE INDEX IX_TetherSessions_ProjectId ON TetherSessions(ProjectId);",
            "CREATE UNIQUE INDEX UX_TetherSessions_ActiveDirectory ON TetherSessions(NormalizedWatchDirectory) WHERE State IN ('Running','NeedsAttention');",
            "CREATE INDEX IX_TetherAssets_SessionId_FirstSeenAtUtc ON TetherAssets(SessionId, FirstSeenAtUtc DESC);",
            "CREATE INDEX IX_TetherAssets_SessionId_ProcessingState ON TetherAssets(SessionId, ProcessingState);",
            "CREATE INDEX IX_TetherAssets_PairingKey ON TetherAssets(SessionId, PairingKey) WHERE PairingKey IS NOT NULL;",
            "CREATE INDEX IX_TetherAssets_ProjectId ON TetherAssets(ProjectId);"
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
