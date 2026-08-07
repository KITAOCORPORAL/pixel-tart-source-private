using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230Rc4RuntimeWorkflowTests
{
    [TestMethod]
    public async Task BookingAggregate_CommitsBookingRequirementsContactsAndStaffTogether()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var result = await setup.Service.SaveAsync(Draft(id));

        Assert.AreEqual(BookingSaveStatus.Saved, result.Status);
        Assert.IsNotNull(await setup.Service.GetAsync(id, includeArchived: true));
        Assert.HasCount(1, await setup.Service.GetRequirementsAsync(id));
        Assert.HasCount(1, await setup.People.ListContactsAsync(id));
        Assert.HasCount(1, await setup.People.ListStaffAsync(id));
    }

    [TestMethod]
    public async Task DuplicateContactFailure_RollsBackEntireBookingAggregate()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var duplicate = Guid.NewGuid();
        var draft = Draft(id) with
        {
            Contacts =
            [
                new() { Id = duplicate, BookingId = id, DisplayName = "联系人甲" },
                new() { Id = duplicate, BookingId = id, DisplayName = "联系人乙" }
            ]
        };

        await Assert.ThrowsAsync<SqliteException>(() => setup.Service.SaveAsync(draft));
        Assert.IsNull(await setup.Service.GetAsync(id, includeArchived: true));
        Assert.HasCount(0, await setup.People.ListContactsAsync(id));
        Assert.HasCount(0, await setup.People.ListStaffAsync(id));
    }

    [TestMethod]
    public async Task DuplicateStaffFailure_RollsBackBookingAndContacts()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var duplicate = Guid.NewGuid();
        var draft = Draft(id) with
        {
            Staff =
            [
                new() { Id = duplicate, BookingId = id, DisplayName = "摄影师甲", Role = BookingStaffRole.Photographer },
                new() { Id = duplicate, BookingId = id, DisplayName = "摄影师乙", Role = BookingStaffRole.Photographer }
            ]
        };

        await Assert.ThrowsAsync<SqliteException>(() => setup.Service.SaveAsync(draft));
        Assert.IsNull(await setup.Service.GetAsync(id, includeArchived: true));
        Assert.HasCount(0, await setup.People.ListContactsAsync(id));
        Assert.HasCount(0, await setup.People.ListStaffAsync(id));
    }

    [TestMethod]
    public async Task StableBookingIdRetry_UpdatesOneBookingAndDoesNotDuplicatePeople()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var draft = Draft(id);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.AreEqual(BookingSaveStatus.Saved, (await setup.Service.SaveAsync(draft with { Title = $"同一排期-{attempt}" })).Status);

        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM ShootBookings WHERE Id=$id;", id));
        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM BookingContacts WHERE BookingId=$id;", id));
        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM BookingStaffMembers WHERE BookingId=$id;", id));
        Assert.AreEqual("同一排期-4", (await setup.Service.GetAsync(id, includeArchived: true))!.Title);
    }

    [TestMethod]
    public async Task LegacyCoreOnlyUpdate_DoesNotEraseExistingPeople()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        await setup.Service.SaveAsync(Draft(id));

        var update = Draft(id) with { ReplacePeople = false, CreateIfMissing = false, Contacts = [], Staff = [], Title = "仅更新核心字段" };
        await setup.Service.SaveAsync(update);

        Assert.HasCount(1, await setup.People.ListContactsAsync(id));
        Assert.HasCount(1, await setup.People.ListStaffAsync(id));
    }

    [TestMethod]
    public void BookingSaveContract_ExposesExplicitRc4Outcomes()
    {
        CollectionAssert.AreEquivalent(
            new[] { "DraftSaved", "Created", "NeedsDocumentAttention", "ValidationFailed", "DatabaseFailed", "FileOperationFailed" },
            Enum.GetNames<BookingEditorSaveStatus>());
    }

    private static ShootBookingDraft Draft(Guid id)
    {
        var start = new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.FromHours(8));
        return new()
        {
            Id = id,
            EditorSessionId = Guid.NewGuid(),
            CreateIfMissing = true,
            ReplacePeople = true,
            Title = "RC4原子排期",
            ClientDisplayName = "客户代号",
            StartAt = start,
            EndAt = start.AddHours(2),
            TimeZoneId = "China Standard Time",
            ShootingType = "Portrait",
            Requirements = [new() { BookingId = id, ItemText = "电池" }],
            Contacts = [new() { BookingId = id, DisplayName = "联系人", IsPrimary = true }],
            Staff = [new() { BookingId = id, DisplayName = "摄影师", Role = BookingStaffRole.Photographer }]
        };
    }

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database, ShootBookingService service, SqliteBookingPeopleRepository people) : IDisposable
    {
        public PixelTartDatabase Database { get; } = database;
        public ShootBookingService Service { get; } = service;
        public SqliteBookingPeopleRepository People { get; } = people;

        public static async Task<Setup> CreateAsync()
        {
            var temp = new TempDirectory();
            var database = new PixelTartDatabase(temp.Combine("data", "rc4.db"));
            var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
            Assert.IsTrue(migration.Success, migration.ErrorMessage);
            var repository = new SqliteShootBookingRepository(database);
            return new(temp, database, new ShootBookingService(repository, new BookingConflictDetector(repository)), new SqliteBookingPeopleRepository(database));
        }

        public async Task<long> ScalarAsync(string sql, Guid id)
        {
            await using var connection = await Database.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            return (long)(await command.ExecuteScalarAsync())!;
        }

        public void Dispose()
        {
            SqliteTestIsolation.ClearPool(Database);
            temp.Dispose();
        }
    }
}
