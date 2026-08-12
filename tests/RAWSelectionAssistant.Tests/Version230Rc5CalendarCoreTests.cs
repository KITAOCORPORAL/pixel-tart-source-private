using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version230Rc5CalendarCoreTests
{
    [TestMethod]
    public void BookingTimeDisplay_UsesBookingTimeZoneAndFriendlyChineseName()
    {
        var service = new BookingTimeDisplayService(TimeZoneInfo.Utc);
        var utc = new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.Zero);

        Assert.AreEqual(new DateTimeOffset(2026, 8, 15, 9, 0, 0, TimeSpan.FromHours(8)), service.ToBookingTime(utc, "China Standard Time"));
        StringAssert.Contains(service.FormatRange(utc, utc.AddHours(2), "China Standard Time", false), "09:00");
        StringAssert.Contains(service.FriendlyTimeZoneName("China Standard Time"), "中国标准时间 UTC+8");
    }

    [TestMethod]
    public void BookingTimeDisplay_InvalidZoneFallsBackWithoutThrowing()
    {
        var service = new BookingTimeDisplayService(TimeZoneInfo.Utc);
        Assert.AreEqual(0, service.ToBookingTime(DateTimeOffset.UnixEpoch, "not-a-real-zone").Hour);
        Assert.AreEqual("未知状态", CalendarWorkflowStatusMapper.DisplayName((CalendarWorkflowStatus)999));
    }

    [TestMethod]
    public void WorkflowMapper_CoversFiveCalendarStatesWithoutInternalLabels()
    {
        Assert.AreEqual(CalendarWorkflowStatus.Scheduled, CalendarWorkflowStatusMapper.FromBookingStatus(ShootBookingStatus.Confirmed));
        Assert.AreEqual(CalendarWorkflowStatus.PendingDelivery, CalendarWorkflowStatusMapper.FromBookingStatus(ShootBookingStatus.Completed));
        Assert.AreEqual(CalendarWorkflowStatus.PendingDelivery, CalendarWorkflowStatusMapper.FromBookingStatus(ShootBookingStatus.AwaitingDelivery));
        Assert.AreEqual(CalendarWorkflowStatus.Delivered, CalendarWorkflowStatusMapper.FromBookingStatus(ShootBookingStatus.Delivered));
        CollectionAssert.AreEquivalent(new[] { "有拍摄", "已拍摄", "待返图", "已返图" },
            Enum.GetValues<CalendarWorkflowStatus>().Select(CalendarWorkflowStatusMapper.DisplayName).ToArray());
    }

    [TestMethod]
    public async Task SetStatus_CommitsBeforeReturnAndNewConnectionReadsIt()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var saved = await setup.Service.SaveAsync(Draft(id));
        Assert.AreEqual(BookingSaveStatus.Saved, saved.Status);

        Assert.IsTrue(await setup.Service.SetStatusAsync(id, ShootBookingStatus.Completed));
        var reopened = new ShootBookingService(new SqliteShootBookingRepository(setup.Database), new BookingConflictDetector(new SqliteShootBookingRepository(setup.Database)));
        var persisted = await reopened.GetAsync(id, includeArchived: true);

        Assert.IsNotNull(persisted);
        Assert.AreEqual(ShootBookingStatus.Completed, persisted.Status);
        Assert.AreEqual(ShootBookingStatus.Completed, (await setup.Service.GetAsync(id, includeArchived: true))!.Status);
    }

    [TestMethod]
    public async Task SetStatus_IsIdempotentAndDoesNotCreateDuplicateBookingRows()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        await setup.Service.SaveAsync(Draft(id));
        Assert.IsTrue(await setup.Service.SetStatusAsync(id, ShootBookingStatus.AwaitingDelivery));
        Assert.IsFalse(await setup.Service.SetStatusAsync(id, ShootBookingStatus.AwaitingDelivery));
        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM ShootBookings WHERE Id=$id;", id));
    }

    [TestMethod]
    public async Task SetStatus_PersistsCrossDayBookingWithOneStableId()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 8, 24, 22, 0, 0, TimeSpan.FromHours(8));
        await setup.Service.SaveAsync(Draft(id) with { StartAt = start, EndAt = start.AddDays(2).AddHours(1) });
        Assert.IsTrue(await setup.Service.SetStatusAsync(id, ShootBookingStatus.Delivered));
        Assert.AreEqual(1L, await setup.ScalarAsync("SELECT COUNT(*) FROM ShootBookings WHERE Id=$id;", id));
        Assert.AreEqual(ShootBookingStatus.Delivered, (await setup.Service.GetAsync(id, includeArchived: true))!.Status);
    }

    [TestMethod]
    public async Task SetStatus_CompletedDisablesReminderInSameTransaction()
    {
        using var setup = await Setup.CreateAsync();
        var id = Guid.NewGuid();
        var saved = await setup.Service.SaveAsync(Draft(id));
        var reminder = new SqliteReminderRepository(setup.Database);
        await reminder.SaveAsync(new(Guid.NewGuid(), null, "拍摄提醒", string.Empty,
            new(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromHours(1)), ReminderStatus.Scheduled, id, true));

        Assert.IsTrue(await setup.Service.SetStatusAsync(id, ShootBookingStatus.Completed));
        var row = (await reminder.ListByBookingAsync(saved.Booking!.Id)).Single();
        Assert.IsFalse(row.IsEnabled);
        Assert.AreEqual(ReminderStatus.Disabled, row.Status);
    }

    [TestMethod]
    public async Task FinanceQuery_CurrencyFilterDoesNotMixCurrencies()
    {
        using var setup = await Setup.CreateAsync();
        var categories = await setup.Finance.ListCategoriesAsync();
        var category = categories.First();
        await setup.Finance.SaveAsync(new() { CategoryId = category.Id, AmountMinor = 1000, CurrencyCode = "CNY", Counterparty = "本地" });
        await setup.Finance.SaveAsync(new() { CategoryId = category.Id, AmountMinor = 1000, CurrencyCode = "USD", Counterparty = "海外" });

        var rows = await setup.Finance.QueryAsync(new(CurrencyCode: "usd"));
        Assert.HasCount(1, rows);
        Assert.AreEqual("USD", rows[0].CurrencyCode);
    }

    private static ShootBookingDraft Draft(Guid id) => new()
    {
        Id = id, EditorSessionId = Guid.NewGuid(), CreateIfMissing = true, ReplacePeople = true,
        Title = "RC5排期", ClientDisplayName = "客户代号",
        StartAt = new DateTimeOffset(2026, 8, 15, 9, 0, 0, TimeSpan.FromHours(8)),
        EndAt = new DateTimeOffset(2026, 8, 15, 11, 0, 0, TimeSpan.FromHours(8)),
        TimeZoneId = "China Standard Time", ShootingType = "Portrait"
    };

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database, ShootBookingService service, SqliteFinanceRepository finance) : IDisposable
    {
        public PixelTartDatabase Database { get; } = database;
        public ShootBookingService Service { get; } = service;
        public SqliteFinanceRepository Finance { get; } = finance;

        public static async Task<Setup> CreateAsync()
        {
            var temp = new TempDirectory();
            var database = new PixelTartDatabase(temp.Combine("data", "rc5.db"));
            var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
            Assert.IsTrue(migration.Success, migration.ErrorMessage);
            var repository = new SqliteShootBookingRepository(database);
            return new(temp, database, new ShootBookingService(repository, new BookingConflictDetector(repository)), new SqliteFinanceRepository(database));
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
