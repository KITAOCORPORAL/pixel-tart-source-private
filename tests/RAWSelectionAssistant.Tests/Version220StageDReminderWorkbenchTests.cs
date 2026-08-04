using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Bookings;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220StageDReminderWorkbenchTests
{
    [TestMethod] public void MissedReminderWindow_IsExactlyTwentyFourHours() => Assert.AreEqual(TimeSpan.FromHours(24), BookingReminderScheduler.MissedWindow);

    [TestMethod]
    public async Task RelativeReminder_ReadsResolvedUtcTrigger()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now.AddHours(1), TimeSpan.FromMinutes(30));
        var saved = await s.Reminders.GetAsync(id);
        Assert.IsNotNull(saved?.Trigger.At);
        Assert.AreEqual(s.BookingStart.AddMinutes(-30), saved.Trigger.At);
    }

    [TestMethod]
    public async Task TriggerClaim_IsAtomicAndCanOnlySucceedOnce()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now.AddMinutes(-1));
        Assert.IsTrue(await s.Reminders.TryClaimTriggeredAsync(id, s.Now));
        Assert.IsFalse(await s.Reminders.TryClaimTriggeredAsync(id, s.Now));
        var saved = await s.Reminders.GetAsync(id);
        Assert.AreEqual(ReminderStatus.Triggered, saved!.Status);
        Assert.IsFalse(saved.IsEnabled);
        Assert.AreEqual(s.Now, saved.LastTriggeredAt);
        Assert.IsFalse(await s.Service.SetEnabledAsync(id, true));
    }

    [TestMethod]
    public async Task FailedNotification_ReleasesClaimForSafeRetry()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now.AddMinutes(-1));
        var notifications = new RecordingReminderNotification { ThrowOnCreate = true };
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.ProcessMissedAsync();
        var saved = await s.Reminders.GetAsync(id);
        Assert.AreEqual(ReminderStatus.Scheduled, saved!.Status);
        Assert.IsTrue(saved.IsEnabled);
        Assert.IsNull(saved.LastTriggeredAt);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task TriggerState_IsPersistedBeforeNotificationPublish()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now.AddMinutes(-1));
        var persisted = false;
        var notifications = new RecordingReminderNotification
        {
            BeforeRecord = async dispatch =>
            {
                var saved = await s.Reminders.GetAsync(dispatch.Reminder.Id);
                persisted = saved?.Status == ReminderStatus.Triggered && saved.LastTriggeredAt == s.Now;
            }
        };
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.ProcessMissedAsync();
        Assert.IsTrue(persisted);
        Assert.HasCount(1, notifications.Dispatches);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task TriggerAndNotification_AreCommittedTogetherEvenIfUiSubscriberFails()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now.AddMinutes(-1));
        var center = new NotificationCenter(s.Database, TimeSpan.Zero);
        center.Published += (_, _) => throw new InvalidOperationException("simulated UI failure");
        var publisher = new BookingReminderNotificationService(center, TimeZoneInfo.Utc);
        var scheduler = new BookingReminderScheduler(s.Reminders, s.Bookings, publisher, s.Audit, s.Time);
        await scheduler.ProcessMissedAsync();
        var reminder = await s.Reminders.GetAsync(id);
        var history = await center.GetHistoryAsync();
        Assert.AreEqual(ReminderStatus.Triggered, reminder!.Status);
        Assert.HasCount(1, history);
        StringAssert.Contains(history[0].DeduplicationKey!, id.ToString("D"));
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task MissedReminder_InsideTwentyFourHours_IsReported()
    {
        using var s = await SetupAsync();
        await s.AddReminderAsync(s.Now.AddHours(-23));
        var notifications = new RecordingReminderNotification();
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.ProcessMissedAsync();
        Assert.HasCount(1, notifications.Dispatches);
        Assert.IsTrue(notifications.Dispatches[0].IsMissed);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task MissedReminder_OlderThanTwentyFourHours_IsNotReported()
    {
        using var s = await SetupAsync();
        await s.AddReminderAsync(s.Now.AddHours(-25));
        var notifications = new RecordingReminderNotification();
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.ProcessMissedAsync();
        Assert.IsEmpty(notifications.Dispatches);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task FinishedBooking_IsNotReportedAsMissed()
    {
        using var s = await SetupAsync(bookingStartOffset: TimeSpan.FromHours(-3), bookingDuration: TimeSpan.FromHours(1));
        await s.AddReminderAsync(s.Now.AddHours(-2.5));
        var notifications = new RecordingReminderNotification();
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.ProcessMissedAsync();
        Assert.IsEmpty(notifications.Dispatches);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task TriggeredDismissedAndCancelledReminders_AreNotReportedAgain()
    {
        using var s = await SetupAsync();
        var triggered = await s.AddReminderAsync(s.Now.AddMinutes(-5));
        Assert.IsTrue(await s.Reminders.TryClaimTriggeredAsync(triggered, s.Now.AddMinutes(-4)));
        var dismissed = await s.AddReminderAsync(s.Now.AddMinutes(-6));
        Assert.IsTrue(await s.Reminders.TryClaimTriggeredAsync(dismissed, s.Now.AddMinutes(-4)));
        Assert.IsTrue(await s.Reminders.MarkDismissedAsync(dismissed, s.Now.AddMinutes(-3)));
        await s.AddReminderAsync(s.Now.AddMinutes(-7));
        await s.Reminders.DisableForBookingAsync(s.BookingId);
        var notifications = new RecordingReminderNotification();
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.ProcessMissedAsync();
        Assert.IsEmpty(notifications.Dispatches);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task Scheduler_DoesNotDuplicateAcrossRepeatedChecks()
    {
        using var s = await SetupAsync();
        await s.AddReminderAsync(s.Now.AddMinutes(-1));
        var notifications = new RecordingReminderNotification();
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.ProcessMissedAsync();
        await scheduler.ProcessMissedAsync();
        await scheduler.CheckDueAsync();
        Assert.HasCount(1, notifications.Dispatches);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task Scheduler_StartsOnceAndStopsWithoutResidency()
    {
        using var s = await SetupAsync();
        var scheduler = s.CreateScheduler(new RecordingReminderNotification());
        await scheduler.StartAsync();
        await scheduler.StartAsync();
        Assert.IsTrue(scheduler.IsRunning);
        await scheduler.StopAsync();
        Assert.IsFalse(scheduler.IsRunning);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task SystemClockMovingBackward_DoesNotRepeatClaimedReminder()
    {
        using var s = await SetupAsync();
        await s.AddReminderAsync(s.Now.AddMinutes(-1));
        var notifications = new RecordingReminderNotification();
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.ProcessMissedAsync();
        s.Time.SetUtcNow(s.Now.AddHours(-2));
        await scheduler.CheckDueAsync();
        Assert.HasCount(1, notifications.Dispatches);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task SystemClockMovingForward_ProcessesTheElapsedWindow()
    {
        using var s = await SetupAsync();
        await s.AddReminderAsync(s.Now.AddHours(2));
        var notifications = new RecordingReminderNotification();
        var scheduler = s.CreateScheduler(notifications);
        await scheduler.CheckDueAsync();
        s.Time.SetUtcNow(s.Now.AddHours(3));
        await scheduler.CheckDueAsync();
        Assert.HasCount(1, notifications.Dispatches);
        await scheduler.DisposeAsync();
    }

    [TestMethod]
    public async Task ReminderService_NewReminderDefaultsOff()
    {
        using var s = await SetupAsync();
        var saved = await s.Service.SaveAsync(new(Guid.NewGuid(), null, "", "", new(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromMinutes(10)), BookingId: s.BookingId));
        Assert.IsFalse(saved.IsEnabled);
        Assert.AreEqual(ReminderStatus.Disabled, saved.Status);
    }

    [TestMethod]
    public async Task ReminderService_EnableAndDisableRoundTrips()
    {
        using var s = await SetupAsync();
        var saved = await s.Service.SaveAsync(new(Guid.NewGuid(), null, "", "", new(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromMinutes(10)), BookingId: s.BookingId));
        Assert.IsTrue(await s.Service.SetEnabledAsync(saved.Id, true));
        Assert.IsTrue((await s.Reminders.GetAsync(saved.Id))!.IsEnabled);
        Assert.IsTrue(await s.Service.SetEnabledAsync(saved.Id, false));
        Assert.IsFalse((await s.Reminders.GetAsync(saved.Id))!.IsEnabled);
    }

    [TestMethod]
    public async Task ReminderEdit_UpdatesSameRecordWithoutCreatingDuplicate()
    {
        using var s = await SetupAsync();
        var original = await s.Service.SaveAsync(new(Guid.NewGuid(), null, "", "", new(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromMinutes(10)), BookingId: s.BookingId));
        var changedAt = s.Now.AddHours(1);
        await s.Service.SaveAsync(original with { Trigger = new(ReminderTriggerKind.AbsoluteTime, changedAt, null), IsEnabled = true, Status = ReminderStatus.Scheduled });
        var rows = await s.Service.ListAsync(s.BookingId);
        Assert.HasCount(1, rows);
        Assert.AreEqual(original.Id, rows[0].Id);
        Assert.AreEqual(changedAt, rows[0].Trigger.At);
        Assert.IsTrue(rows[0].IsEnabled);
    }

    [TestMethod]
    public async Task ArchivedBooking_CannotReenableReminder()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now.AddHours(1));
        await s.Bookings.ArchiveAsync(s.BookingId);
        Assert.IsFalse(await s.Service.SetEnabledAsync(id, true));
    }

    [TestMethod]
    public async Task ReminderAfterBookingEnd_IsRejected()
    {
        using var s = await SetupAsync();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => s.Service.SaveAsync(new(Guid.NewGuid(), null, "", "", new(ReminderTriggerKind.AbsoluteTime, s.BookingEnd.AddMinutes(1), null), ReminderStatus.Scheduled, s.BookingId, true)));
    }

    [TestMethod]
    public async Task BookingTimeEdit_RecalculatesRelativeReminderAtomically()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now, TimeSpan.FromHours(1));
        var newStart = s.BookingStart.AddDays(1);
        var existing = await s.Bookings.GetAsync(s.BookingId, true);
        var result = await s.Bookings.SaveAsync(Draft(existing! with { StartAtUtc = newStart, EndAtUtc = newStart.AddHours(2) }));
        Assert.AreEqual(BookingSaveStatus.Saved, result.Status);
        Assert.AreEqual(newStart.AddHours(-1), (await s.Reminders.GetAsync(id))!.Trigger.At);
    }

    [TestMethod]
    public async Task BookingChanges_NotifyWorkbenchAndSchedulerRefreshHooks()
    {
        using var s = await SetupAsync();
        var changed = new List<Guid>();
        ((IBookingChangeNotifier)s.Bookings).BookingChanged += (_, id) => changed.Add(id);
        var existing = await s.Bookings.GetAsync(s.BookingId, true);
        await s.Bookings.SaveAsync(Draft(existing! with { Location = "新地点" }));
        await s.Bookings.ArchiveAsync(s.BookingId);
        await s.Bookings.RestoreAsync(s.BookingId);
        Assert.HasCount(3, changed);
        Assert.IsTrue(changed.All(id => id == s.BookingId));
    }

    [TestMethod]
    public async Task CancellingBooking_CancelsScheduledReminder()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now.AddHours(1));
        var existing = await s.Bookings.GetAsync(s.BookingId, true);
        var result = await s.Bookings.SaveAsync(Draft(existing! with { Status = ShootBookingStatus.Cancelled }));
        Assert.AreEqual(BookingSaveStatus.Saved, result.Status);
        var saved = await s.Reminders.GetAsync(id);
        Assert.IsFalse(saved!.IsEnabled);
        Assert.AreEqual(ReminderStatus.Cancelled, saved.Status);
    }

    [TestMethod]
    public async Task RemoveReminder_OnlyDeletesReminderRecord()
    {
        using var s = await SetupAsync();
        var id = await s.AddReminderAsync(s.Now.AddHours(1));
        Assert.IsTrue(await s.Service.DeleteAsync(id));
        Assert.IsNull(await s.Reminders.GetAsync(id));
        Assert.IsNotNull(await s.Bookings.GetAsync(s.BookingId));
    }

    [TestMethod]
    public async Task NotificationText_IsPrivacySanitizedAndHasActions()
    {
        var center = new RecordingNotificationCenter();
        var publisher = new BookingReminderNotificationService(center, TimeZoneInfo.Utc);
        var booking = new ShootBooking { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Title = "秘密客户项目", ClientDisplayName = "张三 13800138000", Location = "上海市完整门牌地址", StartAtUtc = DateTimeOffset.Parse("2026-08-04T10:00:00Z"), EndAtUtc = DateTimeOffset.Parse("2026-08-04T11:00:00Z"), TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait" };
        var reminder = new ReminderDefinition(Guid.NewGuid(), booking.ProjectId, booking.Title, "", new(ReminderTriggerKind.RelativeToBookingStart, booking.StartAtUtc.AddMinutes(-30), TimeSpan.FromMinutes(30)), ReminderStatus.Triggered, booking.Id);
        await publisher.PublishAsync(new(reminder, booking, reminder.Trigger.At!.Value, DateTimeOffset.Parse("2026-08-04T09:30:00Z"), false));
        var message = center.Messages.Single();
        Assert.IsFalse(message.Message.Contains(booking.Title, StringComparison.Ordinal));
        Assert.IsFalse(message.Message.Contains(booking.ClientDisplayName, StringComparison.Ordinal));
        Assert.IsFalse(message.Message.Contains(booking.Location, StringComparison.Ordinal));
        StringAssert.Contains(message.Message, "地点已记录");
        CollectionAssert.AreEquivalent(new[] { "open", "later", "ack" }, message.Actions.Select(x => x.Id).ToArray());
    }

    [TestMethod]
    public async Task ReminderAudit_ContainsIdentifiersButNoClientOrAddress()
    {
        using var s = await SetupAsync();
        await s.Service.SaveAsync(new(Guid.NewGuid(), null, "", "", new(ReminderTriggerKind.RelativeToBookingStart, null, TimeSpan.FromMinutes(30)), BookingId: s.BookingId));
        var text = string.Join("\n", s.Audit.Messages);
        StringAssert.Contains(text, s.BookingId.ToString("D"));
        Assert.IsFalse(text.Contains("隐私客户", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("完整地址", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task WorkbenchToday_IncludesCrossMidnightAndOrdersOngoingFirst()
    {
        using var s = await SetupAsync(bookingStartOffset: TimeSpan.FromHours(-1), bookingDuration: TimeSpan.FromHours(3));
        await s.CreateBookingAsync("稍后", s.Now.AddHours(2), s.Now.AddHours(3));
        var snapshot = await s.Workbench.LoadAsync();
        Assert.IsGreaterThanOrEqualTo(2, snapshot.Today.Count);
        Assert.IsTrue(snapshot.Today[0].IsOngoing);
    }

    [TestMethod]
    public async Task WorkbenchToday_IncludesBookingStartedOnPreviousDate()
    {
        using var s = await SetupAsync(nowUtc: DateTimeOffset.Parse("2026-08-04T00:30:00Z"), bookingStartOffset: TimeSpan.FromHours(-1), bookingDuration: TimeSpan.FromHours(2));
        var snapshot = await s.Workbench.LoadAsync();
        Assert.IsTrue(snapshot.Today.Any(x => x.BookingId == s.BookingId && x.IsOngoing));
    }

    [TestMethod]
    public async Task WorkbenchFuture_GroupsTomorrowThroughSevenDays()
    {
        using var s = await SetupAsync();
        await s.CreateBookingAsync("明天", s.Now.AddDays(1), s.Now.AddDays(1).AddHours(1));
        await s.CreateBookingAsync("第七天", s.Now.AddDays(7), s.Now.AddDays(7).AddHours(1));
        await s.CreateBookingAsync("第八天", s.Now.AddDays(8), s.Now.AddDays(8).AddHours(1));
        var snapshot = await s.Workbench.LoadAsync();
        var titles = snapshot.FutureSevenDays.SelectMany(x => x.Items).Select(x => x.Title).ToArray();
        CollectionAssert.Contains(titles, "明天");
        CollectionAssert.Contains(titles, "第七天");
        CollectionAssert.DoesNotContain(titles, "第八天");
    }

    [TestMethod]
    public async Task WorkbenchFuture_CrossesMonthAndYearBoundaries()
    {
        using var s = await SetupAsync(nowUtc: DateTimeOffset.Parse("2026-12-30T08:00:00Z"));
        await s.CreateBookingAsync("跨年拍摄", DateTimeOffset.Parse("2027-01-01T09:00:00Z"), DateTimeOffset.Parse("2027-01-01T10:00:00Z"));
        var snapshot = await s.Workbench.LoadAsync();
        Assert.IsTrue(snapshot.FutureSevenDays.SelectMany(x => x.Items).Any(x => x.Title == "跨年拍摄"));
    }

    [TestMethod]
    public async Task WorkbenchFuture_UsesTimeZoneBoundariesAcrossDst()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        using var s = await SetupAsync(nowUtc: DateTimeOffset.Parse("2026-03-08T08:30:00Z"));
        await s.CreateBookingAsync("夏令时后拍摄", DateTimeOffset.Parse("2026-03-09T16:00:00Z"), DateTimeOffset.Parse("2026-03-09T17:00:00Z"));
        var service = new WorkbenchScheduleService(s.Bookings, s.Documents, s.Reminders, s.Time, zone);
        var snapshot = await service.LoadAsync();
        Assert.IsTrue(snapshot.FutureSevenDays.SelectMany(x => x.Items).Any(x => x.Title == "夏令时后拍摄"));
    }

    [TestMethod]
    public async Task Workbench_ExcludesCancelledBookings()
    {
        using var s = await SetupAsync();
        await s.CreateBookingAsync("已取消", s.Now.AddHours(1), s.Now.AddHours(2), ShootBookingStatus.Cancelled);
        var snapshot = await s.Workbench.LoadAsync();
        Assert.IsFalse(snapshot.Today.Any(x => x.Title == "已取消"));
    }

    [TestMethod]
    public async Task Workbench_ShowsDocumentCountAndEnabledReminderFlag()
    {
        using var s = await SetupAsync(bookingStartOffset: TimeSpan.FromHours(1));
        var path = s.Temp.CreateFile("docs/brief.pdf", [1, 2]);
        await s.Documents.AddAsync(new() { BookingId = s.BookingId, DocumentType = BookingDocumentType.PhotographyPlan, DisplayName = "brief.pdf", FilePath = path, NormalizedPath = Path.GetFullPath(path).ToUpperInvariant(), FileExtension = ".pdf", FileSize = 2 });
        await s.AddReminderAsync(s.Now.AddMinutes(30));
        var item = (await s.Workbench.LoadAsync()).Today.Single(x => x.BookingId == s.BookingId);
        Assert.AreEqual(1, item.DocumentCount);
        Assert.IsTrue(item.HasEnabledReminder);
    }

    [TestMethod]
    public void StageDSource_UsesTimeProviderAndNoBlockingSleep()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "RAWSelectionAssistant.Core", "Services", "Bookings", "BookingReminderServices.cs"));
        StringAssert.Contains(source, "TimeProvider");
        Assert.IsFalse(source.Contains("Thread.Sleep", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("DateTime.Now", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StageD_DoesNotAddSchemaMigrationOrProjectRelationships()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "RAWSelectionAssistant.Core", "Services", "Bookings", "BookingReminderServices.cs"));
        Assert.IsFalse(source.Contains("ProjectRelationships", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("SchemaVersion", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase));
    }

    private static ShootBookingDraft Draft(ShootBooking booking) => new()
    {
        Id = booking.Id, ProjectId = booking.ProjectId, Title = booking.Title, ClientDisplayName = booking.ClientDisplayName,
        StartAt = booking.StartAtUtc, EndAt = booking.EndAtUtc, TimeZoneId = booking.TimeZoneId, IsAllDay = booking.IsAllDay,
        Status = booking.Status, Location = booking.Location, ShootingType = booking.ShootingType, ShootingRequirements = booking.ShootingRequirements,
        PreparationNotes = booking.PreparationNotes, TotalAmountMinor = booking.TotalAmountMinor, DepositAmountMinor = booking.DepositAmountMinor,
        PaidAmountMinor = booking.PaidAmountMinor, CurrencyCode = booking.CurrencyCode, CurrencyScale = booking.CurrencyScale,
        ContactName = booking.ContactName, ContactPhone = booking.ContactPhone, AllowOverlap = booking.AllowOverlap, Notes = booking.Notes
    };

    private static async Task<Setup> SetupAsync(DateTimeOffset? nowUtc = null, TimeSpan? bookingStartOffset = null, TimeSpan? bookingDuration = null)
    {
        var now = nowUtc ?? DateTimeOffset.Parse("2026-08-04T08:00:00Z");
        var time = new MutableTimeProvider(now);
        var temp = new TempDirectory();
        var database = new PixelTartDatabase(temp.Combine("data", "pixel-tart.db"));
        var migration = await new DatabaseMigrator(database, new DatabaseBackupService(database, temp.Combine("backups"))).MigrateAsync();
        Assert.IsTrue(migration.Success, migration.ErrorMessage);
        var repository = new SqliteShootBookingRepository(database);
        var bookings = new ShootBookingService(repository, new BookingConflictDetector(repository));
        var start = now + (bookingStartOffset ?? TimeSpan.FromHours(2));
        var saved = await bookings.SaveAsync(new ShootBookingDraft { Title = "阶段D测试排期", ClientDisplayName = "隐私客户", StartAt = start, EndAt = start + (bookingDuration ?? TimeSpan.FromHours(2)), TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait", Location = "完整地址" });
        var reminders = new SqliteReminderRepository(database, time);
        var audit = new RecordingAuditLog();
        var service = new BookingReminderService(reminders, bookings, audit, time);
        var documents = new SqliteBookingDocumentRepository(database);
        return new(temp, database, time, bookings, reminders, service, audit, documents, saved.Booking!.Id, start, saved.Booking.EndAtUtc);
    }

    private sealed class Setup : IDisposable
    {
        public Setup(TempDirectory temp, PixelTartDatabase database, MutableTimeProvider time, ShootBookingService bookings, SqliteReminderRepository reminders,
            BookingReminderService service, RecordingAuditLog audit, SqliteBookingDocumentRepository documents, Guid bookingId, DateTimeOffset bookingStart, DateTimeOffset bookingEnd)
        {
            Temp = temp; Database = database; Time = time; Bookings = bookings; Reminders = reminders; Service = service; Audit = audit; Documents = documents;
            BookingId = bookingId; BookingStart = bookingStart; BookingEnd = bookingEnd;
            Workbench = new WorkbenchScheduleService(bookings, documents, reminders, time, TimeZoneInfo.Utc);
        }
        public TempDirectory Temp { get; }
        public PixelTartDatabase Database { get; }
        public MutableTimeProvider Time { get; }
        public DateTimeOffset Now => Time.GetUtcNow();
        public ShootBookingService Bookings { get; }
        public SqliteReminderRepository Reminders { get; }
        public BookingReminderService Service { get; }
        public RecordingAuditLog Audit { get; }
        public SqliteBookingDocumentRepository Documents { get; }
        public WorkbenchScheduleService Workbench { get; }
        public Guid BookingId { get; }
        public DateTimeOffset BookingStart { get; }
        public DateTimeOffset BookingEnd { get; }

        public async Task<Guid> AddReminderAsync(DateTimeOffset at, TimeSpan? relativeOffset = null)
        {
            var id = Guid.NewGuid();
            var trigger = relativeOffset.HasValue ? new ReminderTrigger(ReminderTriggerKind.RelativeToBookingStart, null, relativeOffset) : new(ReminderTriggerKind.AbsoluteTime, at, null);
            await Reminders.SaveAsync(new(id, null, "", "", trigger, ReminderStatus.Scheduled, BookingId, true));
            return id;
        }

        public BookingReminderScheduler CreateScheduler(RecordingReminderNotification notifications) => new(Reminders, Bookings, notifications, Audit, Time, TimeSpan.FromSeconds(60));

        public async Task<Guid> CreateBookingAsync(string title, DateTimeOffset start, DateTimeOffset end, ShootBookingStatus status = ShootBookingStatus.Confirmed)
        {
            var result = await Bookings.SaveAsync(new ShootBookingDraft { Title = title, ClientDisplayName = "客户", StartAt = start, EndAt = end, TimeZoneId = TimeZoneInfo.Utc.Id, ShootingType = "Portrait", Status = status, AllowOverlap = true });
            return result.Booking!.Id;
        }

        public void Dispose() { SqliteTestIsolation.ClearPool(Database); Temp.Dispose(); }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void SetUtcNow(DateTimeOffset value) => _now = value;
    }

    private sealed class RecordingAuditLog : IAuditLogService
    {
        public List<string> Messages { get; } = [];
        public Task WriteAsync(string category, string eventType, string severity, string message, Guid? taskId = null, Guid? projectId = null, string? errorCode = null, string? correlationId = null, CancellationToken cancellationToken = default)
        { Messages.Add(message); return Task.CompletedTask; }
    }

    private sealed class RecordingReminderNotification : IBookingReminderNotificationService
    {
        public event EventHandler<ReminderPublishedEvent>? ReminderPublished { add { } remove { } }
        public List<ReminderDispatch> Dispatches { get; } = [];
        public bool ThrowOnCreate { get; init; }
        public Func<ReminderDispatch, Task>? BeforeRecord { get; init; }
        public NotificationMessage CreateNotification(ReminderDispatch dispatch)
        {
            if (ThrowOnCreate) throw new InvalidOperationException("simulated");
            return new(Guid.NewGuid(), NotificationType.Toast, NotificationSeverity.Information, "提醒", "已脱敏", null, dispatch.Booking.ProjectId, [], false, dispatch.TriggeredAtUtc);
        }
        public Task PublishAsync(ReminderDispatch dispatch, CancellationToken cancellationToken = default) { PublishPersisted(dispatch, CreateNotification(dispatch)); return Task.CompletedTask; }
        public void PublishPersisted(ReminderDispatch dispatch, NotificationMessage notification)
        {
            if (BeforeRecord is not null) BeforeRecord(dispatch).GetAwaiter().GetResult();
            Dispatches.Add(dispatch);
        }
    }

    private sealed class RecordingNotificationCenter : INotificationCenter
    {
        public event EventHandler<NotificationMessage>? Published;
        public List<NotificationMessage> Messages { get; } = [];
        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default) { Messages.Add(message); Published?.Invoke(this, message); return Task.CompletedTask; }
        public void NotifyPersisted(NotificationMessage message) { Messages.Add(message); Published?.Invoke(this, message); }
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>(Messages);
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
