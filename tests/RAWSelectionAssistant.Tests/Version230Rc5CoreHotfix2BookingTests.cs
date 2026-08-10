using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230Rc5CoreHotfix2BookingTests
{
    [TestMethod]
    public async Task EditBooking_UpdatesSameIdAndAllEditableFields()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var created = await setup.Service.SaveAsync(Draft(id, true));
        Assert.AreEqual(BookingSaveStatus.Saved, created.Status);

        var edited = Draft(id, false) with
        {
            Title = "CoreHotfix2 edited",
            ClientDisplayName = "Client B",
            StartAt = new DateTimeOffset(2026, 8, 18, 13, 30, 0, TimeSpan.FromHours(8)),
            EndAt = new DateTimeOffset(2026, 8, 18, 16, 0, 0, TimeSpan.FromHours(8)),
            Location = "Studio B",
            Status = ShootBookingStatus.AwaitingDelivery,
            TotalAmountMinor = 880_000,
            DepositAmountMinor = 200_000,
            PaidAmountMinor = 300_000,
            Notes = "updated note"
        };
        var saved = await setup.Service.SaveAsync(edited);

        Assert.AreEqual(BookingSaveStatus.Saved, saved.Status);
        Assert.AreEqual(id, saved.Booking!.Id);
        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM ShootBookings WHERE Id=$id;", id));
        var reloaded = await setup.Service.GetAsync(id, true);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("CoreHotfix2 edited", reloaded.Title);
        Assert.AreEqual("Client B", reloaded.ClientDisplayName);
        Assert.AreEqual("Studio B", reloaded.Location);
        Assert.AreEqual(ShootBookingStatus.AwaitingDelivery, reloaded.Status);
        Assert.AreEqual(880_000, reloaded.TotalAmountMinor);
        Assert.AreEqual(200_000, reloaded.DepositAmountMinor);
        Assert.AreEqual(300_000, reloaded.PaidAmountMinor);
        Assert.AreEqual("updated note", reloaded.Notes);
    }

    [TestMethod]
    public async Task EditBooking_SecondSaveRemainsIdempotent()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        await setup.Service.SaveAsync(Draft(id, true));
        var edit = Draft(id, false) with { Title = "same session", EditorSessionId = Guid.NewGuid() };
        Assert.AreEqual(BookingSaveStatus.Saved, (await setup.Service.SaveAsync(edit)).Status);
        Assert.AreEqual(BookingSaveStatus.Saved, (await setup.Service.SaveAsync(edit)).Status);
        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM ShootBookings WHERE Id=$id;", id));
    }

    [TestMethod]
    public async Task EditBooking_PreservesPeopleUsingStableAggregateIds()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var draft = Draft(id, true) with
        {
            Contacts = [new() { Id = contactId, BookingId = id, DisplayName = "Contact", IsPrimary = true }],
            Staff = [new() { Id = staffId, BookingId = id, DisplayName = "Staff", Role = BookingStaffRole.Photographer }]
        };
        await setup.Service.SaveAsync(draft);
        var edited = draft with { CreateIfMissing = false, Title = "people preserved" };
        await setup.Service.SaveAsync(edited);

        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM BookingContacts WHERE Id=$id;", contactId));
        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM BookingStaffMembers WHERE Id=$id;", staffId));
        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM ShootBookings WHERE Id=$id;", id));
    }

    [TestMethod]
    public async Task EditBooking_MissingIdNeverCreatesNewRow()
    {
        using var setup = await Setup.CreateAsync();
        var missing = Guid.NewGuid();
        var result = await setup.Service.SaveAsync(Draft(missing, false));
        Assert.AreEqual(BookingSaveStatus.NotFound, result.Status);
        Assert.AreEqual(0L, await setup.ScalarAsync("SELECT COUNT(*) FROM ShootBookings WHERE Id=$id;", missing));
    }

    private static ShootBookingDraft Draft(Guid id, bool createIfMissing) => new()
    {
        Id = id,
        EditorSessionId = Guid.NewGuid(),
        CreateIfMissing = createIfMissing,
        ReplacePeople = true,
        Title = "CoreHotfix2 booking",
        ClientDisplayName = "Client A",
        StartAt = new DateTimeOffset(2026, 8, 15, 9, 0, 0, TimeSpan.FromHours(8)),
        EndAt = new DateTimeOffset(2026, 8, 15, 11, 0, 0, TimeSpan.FromHours(8)),
        TimeZoneId = "China Standard Time",
        ShootingType = "Portrait",
        Status = ShootBookingStatus.Confirmed,
        Location = "Studio A"
    };

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database, ShootBookingService service) : IDisposable
    {
        public PixelTartDatabase Database { get; } = database;
        public ShootBookingService Service { get; } = service;

        public static async Task<Setup> CreateAsync()
        {
            var temp = new TempDirectory();
            var database = new PixelTartDatabase(temp.Combine("data", "corehotfix2.db"));
            var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
            Assert.IsTrue(migration.Success, migration.ErrorMessage);
            var repository = new SqliteShootBookingRepository(database);
            return new(temp, database, new ShootBookingService(repository, new BookingConflictDetector(repository)));
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
