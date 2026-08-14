using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

internal static class AssetLibrarySchema
{
    public const int Version = 1;

    public static async Task EnsureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            "CREATE TABLE IF NOT EXISTS AssetLibrarySchemaInfo(Version INTEGER NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL);",
            """
            CREATE TABLE IF NOT EXISTS AssetItems(
                AssetId TEXT NOT NULL PRIMARY KEY,
                SourcePath TEXT NOT NULL,
                NormalizedSourcePath TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                Extension TEXT NOT NULL,
                MediaType TEXT NOT NULL,
                FileSize INTEGER NOT NULL DEFAULT 0 CHECK(FileSize >= 0),
                ContentHash TEXT NULL,
                Width INTEGER NULL,
                Height INTEGER NULL,
                Orientation TEXT NULL,
                CaptureTime TEXT NULL,
                AddedAt TEXT NOT NULL,
                ModifiedAt TEXT NOT NULL,
                Rating INTEGER NOT NULL DEFAULT 0 CHECK(Rating BETWEEN 0 AND 5),
                Comment TEXT NOT NULL DEFAULT '',
                IsMissing INTEGER NOT NULL DEFAULT 0 CHECK(IsMissing IN(0,1)),
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
                ImportMode TEXT NOT NULL DEFAULT 'Reference',
                ManagedCopyPath TEXT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetFolders(
                FolderId TEXT NOT NULL PRIMARY KEY,
                ParentFolderId TEXT NULL,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                Icon TEXT NULL,
                Color TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
                IsSystem INTEGER NOT NULL DEFAULT 0 CHECK(IsSystem IN(0,1)),
                AutoTagIdsJson TEXT NOT NULL DEFAULT '[]',
                FOREIGN KEY(ParentFolderId) REFERENCES AssetFolders(FolderId) ON DELETE SET NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetFolderMemberships(
                AssetId TEXT NOT NULL,
                FolderId TEXT NOT NULL,
                AddedAt TEXT NOT NULL,
                PRIMARY KEY(AssetId,FolderId),
                FOREIGN KEY(AssetId) REFERENCES AssetItems(AssetId) ON DELETE CASCADE,
                FOREIGN KEY(FolderId) REFERENCES AssetFolders(FolderId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS TagGroups(
                TagGroupId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1))
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetTags(
                TagId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                TagGroupId TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                UsageCount INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1)),
                UNIQUE(TagGroupId,Name),
                FOREIGN KEY(TagGroupId) REFERENCES TagGroups(TagGroupId) ON DELETE SET NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AssetTagMemberships(
                AssetId TEXT NOT NULL,
                TagId TEXT NOT NULL,
                AddedAt TEXT NOT NULL,
                PRIMARY KEY(AssetId,TagId),
                FOREIGN KEY(AssetId) REFERENCES AssetItems(AssetId) ON DELETE CASCADE,
                FOREIGN KEY(TagId) REFERENCES AssetTags(TagId) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS SmartFolders(
                SmartFolderId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE,
                Logic TEXT NOT NULL DEFAULT 'And',
                Description TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN(0,1))
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS SmartFolderRules(
                RuleId TEXT NOT NULL PRIMARY KEY,
                SmartFolderId TEXT NOT NULL,
                Field TEXT NOT NULL,
                Operator TEXT NOT NULL,
                Value TEXT NOT NULL DEFAULT '',
                Negated INTEGER NOT NULL DEFAULT 0 CHECK(Negated IN(0,1)),
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(SmartFolderId) REFERENCES SmartFolders(SmartFolderId) ON DELETE CASCADE
            );
            """,
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_DisplayName ON AssetItems(DisplayName COLLATE NOCASE);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_AddedAt ON AssetItems(AddedAt DESC);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_CaptureTime ON AssetItems(CaptureTime DESC);",
            "CREATE INDEX IF NOT EXISTS IX_AssetItems_Rating ON AssetItems(Rating);",
            "CREATE INDEX IF NOT EXISTS IX_AssetFolderMemberships_Folder ON AssetFolderMemberships(FolderId,AssetId);",
            "CREATE INDEX IF NOT EXISTS IX_AssetTagMemberships_Tag ON AssetTagMemberships(TagId,AssetId);"
        };

        foreach (var statement in statements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var version = connection.CreateCommand();
        version.CommandText = "INSERT OR IGNORE INTO AssetLibrarySchemaInfo(Version,AppliedAt) VALUES($version,$at);";
        version.Parameters.AddWithValue("$version", Version);
        version.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        await version.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
