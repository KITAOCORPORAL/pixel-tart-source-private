using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class CalendarSchemaMigration : IMigration
{
    public int Version => 2;
    public string Name => "ShootBookingFoundation";

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            """
            CREATE TABLE ShootBookings (
                Id TEXT NOT NULL PRIMARY KEY,
                ProjectId TEXT NULL,
                Title TEXT NOT NULL,
                ClientDisplayName TEXT NOT NULL,
                StartAtUtc TEXT NOT NULL,
                EndAtUtc TEXT NOT NULL,
                TimeZoneId TEXT NOT NULL,
                IsAllDay INTEGER NOT NULL DEFAULT 0 CHECK(IsAllDay IN (0,1)),
                Status TEXT NOT NULL,
                Location TEXT NULL,
                ShootingType TEXT NOT NULL,
                ShootingRequirements TEXT NULL,
                PreparationNotes TEXT NULL,
                TotalAmountMinor INTEGER NULL CHECK(TotalAmountMinor IS NULL OR TotalAmountMinor >= 0),
                DepositAmountMinor INTEGER NULL CHECK(DepositAmountMinor IS NULL OR DepositAmountMinor >= 0),
                PaidAmountMinor INTEGER NULL CHECK(PaidAmountMinor IS NULL OR PaidAmountMinor >= 0),
                CurrencyCode TEXT NOT NULL DEFAULT 'CNY',
                CurrencyScale INTEGER NOT NULL DEFAULT 2 CHECK(CurrencyScale BETWEEN 0 AND 4),
                ContactName TEXT NULL,
                ContactPhone TEXT NULL,
                AllowOverlap INTEGER NOT NULL DEFAULT 0 CHECK(AllowOverlap IN (0,1)),
                ConflictOverride INTEGER NOT NULL DEFAULT 0 CHECK(ConflictOverride IN (0,1)),
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0 CHECK(IsArchived IN (0,1)),
                ArchivedAtUtc TEXT NULL,
                CHECK(EndAtUtc > StartAtUtc),
                FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL
            );
            """,
            """
            CREATE TABLE ShootRequirementItems (
                Id TEXT NOT NULL PRIMARY KEY,
                BookingId TEXT NOT NULL,
                ItemText TEXT NOT NULL,
                IsCompleted INTEGER NOT NULL DEFAULT 0 CHECK(IsCompleted IN (0,1)),
                Priority TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                CompletedAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY(BookingId) REFERENCES ShootBookings(Id) ON DELETE RESTRICT
            );
            """,
            """
            CREATE TABLE BookingDocuments (
                Id TEXT NOT NULL PRIMARY KEY,
                BookingId TEXT NOT NULL,
                ProjectId TEXT NULL,
                DocumentType TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                NormalizedPath TEXT NOT NULL,
                FileExtension TEXT NOT NULL,
                FileSize INTEGER NULL CHECK(FileSize IS NULL OR FileSize >= 0),
                LastKnownModifiedAtUtc TEXT NULL,
                OptionalHash TEXT NULL,
                LinkMode TEXT NOT NULL,
                ImportTaskId TEXT NULL,
                AddedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                LastVerifiedAtUtc TEXT NULL,
                IsMissing INTEGER NOT NULL DEFAULT 0 CHECK(IsMissing IN (0,1)),
                MissingSinceAtUtc TEXT NULL,
                Notes TEXT NULL,
                FOREIGN KEY(BookingId) REFERENCES ShootBookings(Id) ON DELETE RESTRICT,
                FOREIGN KEY(ProjectId) REFERENCES Projects(Id) ON DELETE SET NULL,
                FOREIGN KEY(ImportTaskId) REFERENCES Tasks(Id) ON DELETE SET NULL,
                UNIQUE(BookingId, NormalizedPath)
            );
            """,
            """
            CREATE TABLE BookingReminders (
                Id TEXT NOT NULL PRIMARY KEY,
                BookingId TEXT NOT NULL,
                TriggerKind TEXT NOT NULL,
                OffsetMinutes INTEGER NULL CHECK(OffsetMinutes IS NULL OR OffsetMinutes >= 0),
                TriggerAtUtc TEXT NOT NULL,
                IsEnabled INTEGER NOT NULL DEFAULT 0 CHECK(IsEnabled IN (0,1)),
                Status TEXT NOT NULL DEFAULT 'Disabled',
                LastTriggeredAtUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY(BookingId) REFERENCES ShootBookings(Id) ON DELETE RESTRICT,
                UNIQUE(BookingId, TriggerAtUtc)
            );
            """,
            "CREATE INDEX IX_ShootBookings_Archived_StartAtUtc_Id ON ShootBookings(IsArchived, StartAtUtc DESC, Id DESC);",
            "CREATE INDEX IX_ShootBookings_Archived_EndAtUtc ON ShootBookings(IsArchived, EndAtUtc);",
            "CREATE INDEX IX_ShootBookings_Status_StartAtUtc ON ShootBookings(Status, StartAtUtc) WHERE IsArchived=0;",
            "CREATE INDEX IX_ShootBookings_ShootingType_StartAtUtc ON ShootBookings(ShootingType, StartAtUtc) WHERE IsArchived=0;",
            "CREATE INDEX IX_ShootBookings_ProjectId_Archived ON ShootBookings(ProjectId, IsArchived);",
            "CREATE INDEX IX_ShootRequirementItems_BookingId_SortOrder ON ShootRequirementItems(BookingId, SortOrder);",
            "CREATE INDEX IX_ShootRequirementItems_BookingId_Completed ON ShootRequirementItems(BookingId, IsCompleted);",
            "CREATE INDEX IX_BookingDocuments_BookingId_Type ON BookingDocuments(BookingId, DocumentType);",
            "CREATE INDEX IX_BookingDocuments_BookingId_Missing ON BookingDocuments(BookingId, IsMissing);",
            "CREATE INDEX IX_BookingDocuments_ProjectId ON BookingDocuments(ProjectId);",
            "CREATE INDEX IX_BookingReminders_Enabled_Status_TriggerAtUtc ON BookingReminders(IsEnabled, Status, TriggerAtUtc);",
            "CREATE INDEX IX_BookingReminders_BookingId ON BookingReminders(BookingId);"
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
