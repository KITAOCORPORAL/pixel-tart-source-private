using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

#pragma warning disable MSTEST0037

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220BookingDomainTests
{
    [TestMethod]
    public void MoneyCalculator_AllowsOverpaymentAndReturnsWarning()
    {
        var result = BookingMoneyCalculator.Calculate(10_000, 2_000, 12_500);
        Assert.AreEqual(-2_500, result.SignedBalanceMinor);
        Assert.AreEqual(2_500, result.DisplayAmountMinor);
        Assert.AreEqual(BookingMoneyDisplayKind.Overpaid, result.DisplayKind);
        Assert.IsTrue(result.Warnings.Any(x => x.Code == BookingMoneyCalculator.PaidExceedsTotalCode));
    }

    [TestMethod]
    public void MoneyCalculator_DoesNotModifyEnteredAmounts()
    {
        var result = BookingMoneyCalculator.Calculate(10_000, 8_000, 12_500);
        Assert.AreEqual(10_000, result.TotalAmountMinor);
        Assert.AreEqual(8_000, result.DepositAmountMinor);
        Assert.AreEqual(12_500, result.PaidAmountMinor);
    }

    [TestMethod]
    public void MoneyValidation_OnlyRejectsNegativeFields()
    {
        Assert.HasCount(0, BookingMoneyCalculator.Validate(10_000, 9_000, 12_000, 2));
        Assert.HasCount(3, BookingMoneyCalculator.Validate(-1, -1, -1, 2));
    }

    [TestMethod]
    public void CrossDayBooking_IsValid()
    {
        var start = new DateTimeOffset(2026, 8, 4, 22, 0, 0, TimeSpan.FromHours(8));
        var end = start.AddHours(5);
        Assert.HasCount(0, ShootBookingTimeRules.Validate(start, end, TimeZoneInfo.Local.Id, false));
    }

    [TestMethod]
    public void EndBeforeStart_IsRejected()
    {
        var start = DateTimeOffset.UtcNow;
        Assert.IsTrue(ShootBookingTimeRules.Validate(start, start.AddMinutes(-1), TimeZoneInfo.Utc.Id, false).Any());
    }

    [TestMethod]
    public void AllDayRange_UsesExclusiveEndAndUtcStorage()
    {
        var range = ShootBookingTimeRules.CreateAllDayRange(new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 6), TimeZoneInfo.Utc.Id);
        Assert.AreEqual(TimeSpan.FromDays(2), range.EndAtUtc - range.StartAtUtc);
        Assert.HasCount(0, ShootBookingTimeRules.Validate(range.StartAtUtc, range.EndAtUtc, TimeZoneInfo.Utc.Id, true));
    }

    [TestMethod]
    public void AllDayNonMidnightRange_IsRejected()
    {
        var start = new DateTimeOffset(2026, 8, 4, 1, 0, 0, TimeSpan.Zero);
        Assert.IsTrue(ShootBookingTimeRules.Validate(start, start.AddDays(1), TimeZoneInfo.Utc.Id, true).Any());
    }

    [TestMethod]
    public async Task Service_NormalizesStoredTimesToUtc()
    {
        using var setup = await SetupAsync();
        var start = new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.FromHours(8));
        var result = await setup.Service.SaveAsync(Draft(start, start.AddHours(2)));
        Assert.AreEqual(BookingSaveStatus.Saved, result.Status);
        Assert.AreEqual(TimeSpan.Zero, result.Booking!.StartAtUtc.Offset);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 4, 2, 0, 0, TimeSpan.Zero), result.Booking.StartAtUtc);
    }

    [TestMethod]
    public async Task BlockingOverlap_ReturnsNeedsAttentionWithoutSaving()
    {
        using var setup = await SetupAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        Assert.AreEqual(BookingSaveStatus.Saved, (await setup.Service.SaveAsync(Draft(start, start.AddHours(2)))).Status);
        var second = await setup.Service.SaveAsync(Draft(start.AddHours(1), start.AddHours(3)) with { Title = "冲突项目" });
        Assert.AreEqual(BookingSaveStatus.NeedsAttention, second.Status);
        Assert.IsTrue(second.Conflicts.Any(x => x.IsBlocking));
    }

    [TestMethod]
    public async Task SaveAnyway_PersistsConflictOverride()
    {
        using var setup = await SetupAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await setup.Service.SaveAsync(Draft(start, start.AddHours(2)));
        var second = await setup.Service.SaveAsync(Draft(start.AddMinutes(30), start.AddHours(3)) with { Title = "仍然保存" }, BookingConflictResolution.SaveAnyway);
        Assert.AreEqual(BookingSaveStatus.Saved, second.Status);
        Assert.IsTrue(second.Booking!.ConflictOverride);
    }

    [TestMethod]
    public async Task AllowOverlap_MakesConflictNonBlocking()
    {
        using var setup = await SetupAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await setup.Service.SaveAsync(Draft(start, start.AddHours(2)) with { AllowOverlap = true });
        var second = await setup.Service.SaveAsync(Draft(start.AddMinutes(30), start.AddHours(3)) with { Title = "可重叠" });
        Assert.AreEqual(BookingSaveStatus.Saved, second.Status);
        Assert.IsTrue(second.Conflicts.All(x => !x.IsBlocking));
    }

    [TestMethod]
    public async Task TouchingIntervals_DoNotConflict()
    {
        using var setup = await SetupAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await setup.Service.SaveAsync(Draft(start, start.AddHours(2)));
        var second = await setup.Service.SaveAsync(Draft(start.AddHours(2), start.AddHours(3)) with { Title = "首尾相接" });
        Assert.AreEqual(BookingSaveStatus.Saved, second.Status);
        Assert.HasCount(0, second.Conflicts);
    }

    [TestMethod]
    public async Task CancelledBooking_DoesNotBlockOverlap()
    {
        using var setup = await SetupAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await setup.Service.SaveAsync(Draft(start, start.AddHours(2)) with { Status = ShootBookingStatus.Cancelled });
        Assert.AreEqual(BookingSaveStatus.Saved, (await setup.Service.SaveAsync(Draft(start, start.AddHours(2)) with { Title = "新项目" })).Status);
    }

    [TestMethod]
    public async Task CurrentViewQuery_ReturnsOnlyOverlappingRange()
    {
        using var setup = await SetupAsync();
        var day = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        await setup.Service.SaveAsync(Draft(day.AddHours(10), day.AddHours(12)));
        await setup.Service.SaveAsync(Draft(day.AddDays(3), day.AddDays(3).AddHours(1)) with { Title = "范围外" });
        var rows = await setup.Service.QueryCurrentViewAsync(new(day, day.AddDays(1)));
        Assert.HasCount(1, rows);
        Assert.AreEqual("项目", rows[0].Title);
    }

    [TestMethod]
    public async Task GlobalSearch_DefaultPageSizeIsFiftyAndUsesCursor()
    {
        using var setup = await SetupAsync();
        await SeedAsync(setup.Database, 105);
        var first = await setup.Service.SearchAllUnarchivedAsync(new(Keyword: "分页项目"));
        Assert.HasCount(50, first.Items);
        Assert.IsNotNull(first.NextCursor);
        var second = await setup.Service.SearchAllUnarchivedAsync(new(Keyword: "分页项目", Cursor: first.NextCursor));
        Assert.HasCount(50, second.Items);
        var third = await setup.Service.SearchAllUnarchivedAsync(new(Keyword: "分页项目", Cursor: second.NextCursor));
        Assert.HasCount(5, third.Items);
        Assert.IsNull(third.NextCursor);
        Assert.HasCount(105, first.Items.Concat(second.Items).Concat(third.Items).Select(x => x.Id).Distinct());
    }

    [TestMethod]
    public async Task GlobalSearch_ClampsPageSizeToOneHundred()
    {
        using var setup = await SetupAsync();
        await SeedAsync(setup.Database, 105);
        var page = await setup.Service.SearchAllUnarchivedAsync(new(Keyword: "分页项目", PageSize: 500));
        Assert.HasCount(100, page.Items);
    }

    [TestMethod]
    public async Task GlobalSearch_ExcludesArchivedBookings()
    {
        using var setup = await SetupAsync();
        var saved = await setup.Service.SaveAsync(Draft(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1)) with { Title = "归档搜索项目" });
        await setup.Service.ArchiveAsync(saved.Booking!.Id);
        Assert.HasCount(0, (await setup.Service.SearchAllUnarchivedAsync(new(Keyword: "归档搜索项目"))).Items);
    }

    [TestMethod]
    public async Task ArchivedSearch_IsCursorPagedAtFiftyAndExcludesActiveBookings()
    {
        using var setup = await SetupAsync();
        await SeedArchivedAsync(setup.Database, 55);
        await setup.Service.SaveAsync(Draft(DateTimeOffset.UtcNow.AddDays(100), DateTimeOffset.UtcNow.AddDays(100).AddHours(1)) with { Title = "归档分页项目-未归档" });
        var first = await setup.Service.SearchArchivedAsync(new(Keyword: "归档分页项目"));
        Assert.HasCount(50, first.Items);
        Assert.IsNotNull(first.NextCursor);
        var second = await setup.Service.SearchArchivedAsync(new(Keyword: "归档分页项目", Cursor: first.NextCursor));
        Assert.HasCount(5, second.Items);
        Assert.IsNull(second.NextCursor);
        Assert.IsTrue(first.Items.Concat(second.Items).All(item => item.IsArchived));
    }

    [TestMethod]
    public async Task ConflictResult_IncludesExistingBookingStatusForUi()
    {
        using var setup = await SetupAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await setup.Service.SaveAsync(Draft(start, start.AddHours(2)) with { Status = ShootBookingStatus.Confirmed });
        var result = await setup.Service.SaveAsync(Draft(start.AddMinutes(30), start.AddHours(3)) with { Title = "状态冲突" });
        Assert.AreEqual(ShootBookingStatus.Confirmed, result.Conflicts.Single().Status);
    }

    [TestMethod]
    public async Task SearchSupportsKeywordStatusAndTypeFilters()
    {
        using var setup = await SetupAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await setup.Service.SaveAsync(Draft(start, start.AddHours(1)) with { Title = "商业人像", ShootingType = "Commercial", Status = ShootBookingStatus.Confirmed });
        await setup.Service.SaveAsync(Draft(start.AddHours(2), start.AddHours(3)) with { Title = "普通人像", ShootingType = "Portrait" });
        var rows = await setup.Service.SearchAllUnarchivedAsync(new("商业", ShootBookingStatus.Confirmed, "Commercial"));
        Assert.HasCount(1, rows.Items);
    }

    [TestMethod]
    public async Task ArchiveAndRestore_PreserveRequirementsAndKeepRemindersDisabled()
    {
        using var setup = await SetupAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var draft = Draft(start, start.AddHours(1)) with { Requirements = [new ShootRequirementItem { ItemText = "电池" }] };
        var saved = await setup.Service.SaveAsync(draft);
        var reminder = new SqliteReminderRepository(setup.Database);
        await reminder.SaveAsync(new(Guid.NewGuid(), null, "提醒", string.Empty, new(ReminderTriggerKind.AbsoluteTime, start.AddHours(-1), null), ReminderStatus.Scheduled, saved.Booking!.Id, true));
        Assert.IsTrue(await setup.Service.ArchiveAsync(saved.Booking.Id));
        Assert.IsNull(await setup.Service.GetAsync(saved.Booking.Id));
        Assert.HasCount(1, await setup.Service.GetRequirementsAsync(saved.Booking.Id));
        var archivedReminder = (await reminder.ListByBookingAsync(saved.Booking.Id)).Single();
        Assert.IsFalse(archivedReminder.IsEnabled);
        Assert.AreEqual(ReminderStatus.Disabled, archivedReminder.Status);
        Assert.IsTrue(await setup.Service.RestoreAsync(saved.Booking.Id));
        Assert.IsNotNull(await setup.Service.GetAsync(saved.Booking.Id));
        var restoredReminder = (await reminder.ListByBookingAsync(saved.Booking.Id)).Single();
        Assert.IsFalse(restoredReminder.IsEnabled);
        Assert.AreEqual(ReminderStatus.Disabled, restoredReminder.Status);
    }

    private static ShootBookingDraft Draft(DateTimeOffset start, DateTimeOffset end) => new()
    {
        Title = "项目", ClientDisplayName = "客户", StartAt = start, EndAt = end, TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait"
    };

    private static async Task SeedAsync(PixelTartDatabase database, int count)
    {
        await using var connection = await database.OpenConnectionAsync(write: true);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        for (var index = 0; index < count; index++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(index);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO ShootBookings(Id,Title,ClientDisplayName,StartAtUtc,EndAtUtc,TimeZoneId,IsAllDay,Status,ShootingType,CurrencyCode,CurrencyScale,AllowOverlap,ConflictOverride,CreatedAtUtc,UpdatedAtUtc,IsArchived) VALUES($id,$title,'客户',$start,$end,'UTC',0,'Tentative','Portrait','CNY',2,0,0,$created,$created,0);";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("$title", $"分页项目{index:D3}");
            command.Parameters.AddWithValue("$start", start.ToString("O")); command.Parameters.AddWithValue("$end", start.AddHours(1).ToString("O")); command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static async Task SeedArchivedAsync(PixelTartDatabase database, int count)
    {
        await using var connection = await database.OpenConnectionAsync(write: true);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        for (var index = 0; index < count; index++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(index);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO ShootBookings(Id,Title,ClientDisplayName,StartAtUtc,EndAtUtc,TimeZoneId,IsAllDay,Status,ShootingType,CurrencyCode,CurrencyScale,AllowOverlap,ConflictOverride,CreatedAtUtc,UpdatedAtUtc,IsArchived,ArchivedAtUtc) VALUES($id,$title,'客户',$start,$end,'UTC',0,'Tentative','Portrait','CNY',2,0,0,$created,$created,1,$created);";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("$title", $"归档分页项目{index:D3}");
            command.Parameters.AddWithValue("$start", start.ToString("O")); command.Parameters.AddWithValue("$end", start.AddHours(1).ToString("O")); command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static async Task<Setup> SetupAsync()
    {
        var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
        Assert.IsTrue(migration.Success, migration.ErrorMessage);
        var repository = new SqliteShootBookingRepository(database);
        return new(temp, database, new ShootBookingService(repository, new BookingConflictDetector(repository)));
    }

    private sealed class Setup(TempDirectory temp, PixelTartDatabase database, ShootBookingService service) : IDisposable
    {
        public PixelTartDatabase Database { get; } = database;
        public ShootBookingService Service { get; } = service;
        public void Dispose() { SqliteTestIsolation.ClearPool(Database); temp.Dispose(); }
    }
}
