using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class InitialSchemaMigration : IMigration
{
    public int Version => 1;
    public string Name => "InitialTaskAndSafetyFoundation";

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS SchemaInfo (
                Version INTEGER NOT NULL PRIMARY KEY,
                AppliedAt TEXT NOT NULL,
                ApplicationVersion TEXT NOT NULL,
                MigrationName TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS Projects (
                Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, ProjectType TEXT NOT NULL,
                WorkflowState TEXT NOT NULL, RootPath TEXT NULL, CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL, LastOpenedAt TEXT NULL, IsArchived INTEGER NOT NULL DEFAULT 0
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS ProjectSources (
                Id TEXT NOT NULL PRIMARY KEY, ProjectId TEXT NOT NULL, Path TEXT NOT NULL,
                Purpose TEXT NOT NULL, Priority INTEGER NOT NULL, IsAvailable INTEGER NOT NULL,
                LastCheckedAt TEXT NULL, FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS SelectionInputs (
                Id TEXT NOT NULL PRIMARY KEY, ProjectId TEXT NOT NULL, OriginalInput TEXT NOT NULL,
                NormalizedName TEXT NOT NULL, NumericId INTEGER NULL, SourceType TEXT NOT NULL,
                CreatedAt TEXT NOT NULL, FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS MediaFiles (
                Id TEXT NOT NULL PRIMARY KEY, ProjectId TEXT NULL, FullPath TEXT NOT NULL,
                NormalizedPath TEXT NOT NULL, FileName TEXT NOT NULL, Extension TEXT NOT NULL,
                FileSize INTEGER NOT NULL, ModifiedAt TEXT NOT NULL, Format TEXT NOT NULL,
                MetadataState TEXT NOT NULL, OptionalHash TEXT NULL, IsAvailable INTEGER NOT NULL,
                FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS MatchDecisions (
                Id TEXT NOT NULL PRIMARY KEY, ProjectId TEXT NOT NULL, SelectionInputId TEXT NOT NULL,
                Format TEXT NOT NULL, Status TEXT NOT NULL, SelectedMediaFileId TEXT NULL,
                Reason TEXT NULL, IsManual INTEGER NOT NULL, UpdatedAt TEXT NOT NULL,
                FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
                FOREIGN KEY(SelectionInputId) REFERENCES SelectionInputs(Id) ON DELETE CASCADE,
                FOREIGN KEY(SelectedMediaFileId) REFERENCES MediaFiles(Id) ON DELETE SET NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT NOT NULL PRIMARY KEY, ProjectId TEXT NULL, Type TEXT NOT NULL,
                DisplayName TEXT NOT NULL, State TEXT NOT NULL, Progress REAL NOT NULL,
                CurrentStep TEXT NULL, CreatedAt TEXT NOT NULL, StartedAt TEXT NULL,
                CompletedAt TEXT NULL, LastUpdatedAt TEXT NOT NULL, LastErrorCode TEXT NULL,
                LastErrorMessage TEXT NULL, RetryCount INTEGER NOT NULL, InputSnapshot TEXT NOT NULL,
                ResultSummary TEXT NULL, Priority INTEGER NOT NULL DEFAULT 1,
                MaximumRetryCount INTEGER NOT NULL DEFAULT 3, OperationPlanId TEXT NULL,
                FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS TaskSteps (
                Id TEXT NOT NULL PRIMARY KEY, TaskId TEXT NOT NULL, Sequence INTEGER NOT NULL,
                Name TEXT NOT NULL, State TEXT NOT NULL, Progress REAL NOT NULL, Checkpoint TEXT NULL,
                StartedAt TEXT NULL, CompletedAt TEXT NULL, LastErrorCode TEXT NULL,
                FOREIGN KEY(TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                UNIQUE(TaskId, Sequence)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS OperationItems (
                Id TEXT NOT NULL PRIMARY KEY, TaskId TEXT NOT NULL, Sequence INTEGER NOT NULL,
                SourcePath TEXT NOT NULL, DestinationPath TEXT NOT NULL, OperationType TEXT NOT NULL,
                ConflictPolicy TEXT NOT NULL, ExpectedSourceSize INTEGER NULL,
                ExpectedSourceModifiedAt TEXT NULL, OptionalSourceHash TEXT NULL,
                ActualOutputSize INTEGER NULL, OptionalOutputHash TEXT NULL, State TEXT NOT NULL,
                ErrorCode TEXT NULL, StartedAt TEXT NULL, CompletedAt TEXT NULL,
                FOREIGN KEY(TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                UNIQUE(TaskId, Sequence)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS UndoJournals (
                Id TEXT NOT NULL PRIMARY KEY, TaskId TEXT NOT NULL, Sequence INTEGER NOT NULL,
                ReverseOperation TEXT NOT NULL, SourcePath TEXT NOT NULL, DestinationPath TEXT NOT NULL,
                ExpectedCurrentSize INTEGER NULL, ExpectedCurrentHash TEXT NULL, Preconditions TEXT NOT NULL,
                State TEXT NOT NULL, CreatedAt TEXT NOT NULL, AppliedAt TEXT NULL,
                FOREIGN KEY(TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                UNIQUE(TaskId, Sequence)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS QuickTools (
                ToolId TEXT NOT NULL PRIMARY KEY, SortOrder INTEGER NOT NULL,
                IsPinned INTEGER NOT NULL, UpdatedAt TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id TEXT NOT NULL PRIMARY KEY, Timestamp TEXT NOT NULL, Category TEXT NOT NULL,
                EventType TEXT NOT NULL, Severity TEXT NOT NULL, TaskId TEXT NULL, ProjectId TEXT NULL,
                ErrorCode TEXT NULL, SanitizedMessage TEXT NOT NULL, CorrelationId TEXT NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS Notifications (
                Id TEXT NOT NULL PRIMARY KEY, Type TEXT NOT NULL, Severity TEXT NOT NULL,
                Title TEXT NOT NULL, Message TEXT NOT NULL, TaskId TEXT NULL, ProjectId TEXT NULL,
                IsRead INTEGER NOT NULL, CreatedAt TEXT NOT NULL, ExpiresAt TEXT NULL,
                DeduplicationKey TEXT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS IX_ProjectSources_ProjectId ON ProjectSources(ProjectId);",
            "CREATE INDEX IF NOT EXISTS IX_SelectionInputs_ProjectId ON SelectionInputs(ProjectId);",
            "CREATE INDEX IF NOT EXISTS IX_MediaFiles_ProjectId_NormalizedPath ON MediaFiles(ProjectId, NormalizedPath);",
            "CREATE INDEX IF NOT EXISTS IX_Tasks_State_LastUpdatedAt ON Tasks(State, LastUpdatedAt DESC);",
            "CREATE INDEX IF NOT EXISTS IX_OperationItems_TaskId_State ON OperationItems(TaskId, State);",
            "CREATE INDEX IF NOT EXISTS IX_AuditLogs_Timestamp ON AuditLogs(Timestamp DESC);",
            "CREATE INDEX IF NOT EXISTS IX_Notifications_IsRead_CreatedAt ON Notifications(IsRead, CreatedAt DESC);"
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

