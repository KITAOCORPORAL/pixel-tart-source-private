using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public sealed class BookingReminderService(
    IReminderRepository repository,
    IShootBookingService bookingService,
    IAuditLogService auditLog,
    TimeProvider? timeProvider = null) : IBookingReminderService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task<IReadOnlyList<ReminderDefinition>> ListAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        repository.ListByBookingAsync(bookingId, cancellationToken);

    public async Task<ReminderDefinition> SaveAsync(ReminderDefinition reminder, CancellationToken cancellationToken = default)
    {
        if (reminder.BookingId is null) throw new ArgumentException("排期提醒必须关联排期。", nameof(reminder));
        var booking = await bookingService.GetAsync(reminder.BookingId.Value, includeArchived: true, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("排期不存在。");
        if (booking.IsArchived) throw new InvalidOperationException("已归档排期只能查看提醒。");
        if (booking.Status == ShootBookingStatus.Cancelled && reminder.IsEnabled) throw new InvalidOperationException("已取消排期不能启用提醒。");
        var planned = ResolvePlannedAt(reminder, booking);
        if (planned >= booking.EndAtUtc) throw new InvalidOperationException("提醒时间必须早于拍摄结束时间。");
        var now = _timeProvider.GetUtcNow();
        var normalized = reminder with
        {
            ProjectId = booking.ProjectId,
            Title = booking.Title,
            Trigger = reminder.Trigger with { At = planned },
            Status = reminder.IsEnabled ? ReminderStatus.Scheduled : ReminderStatus.Disabled,
            LastTriggeredAt = null,
            CreatedAt = reminder.CreatedAt ?? now,
            UpdatedAt = now
        };
        await repository.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
        await auditLog.WriteAsync("BookingReminder", "Saved", "Information", $"ReminderId={normalized.Id:D};BookingId={booking.Id:D};Result=Succeeded", cancellationToken: cancellationToken).ConfigureAwait(false);
        return normalized;
    }

    public async Task<bool> SetEnabledAsync(Guid reminderId, bool enabled, CancellationToken cancellationToken = default)
    {
        var reminder = await repository.GetAsync(reminderId, cancellationToken).ConfigureAwait(false);
        if (reminder?.BookingId is null) return false;
        var booking = await bookingService.GetAsync(reminder.BookingId.Value, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (booking is null || booking.IsArchived || booking.Status == ShootBookingStatus.Cancelled) return false;
        if (enabled && (reminder.Trigger.At is null || reminder.Trigger.At >= booking.EndAtUtc)) return false;
        var changed = await repository.SetEnabledAsync(reminderId, enabled, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (changed) await auditLog.WriteAsync("BookingReminder", enabled ? "Enabled" : "Disabled", "Information", $"ReminderId={reminderId:D};BookingId={booking.Id:D};Result=Succeeded", cancellationToken: cancellationToken).ConfigureAwait(false);
        return changed;
    }

    public async Task<bool> DeleteAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        var reminder = await repository.GetAsync(reminderId, cancellationToken).ConfigureAwait(false);
        var deleted = await repository.DeleteAsync(reminderId, cancellationToken).ConfigureAwait(false);
        if (deleted) await auditLog.WriteAsync("BookingReminder", "Removed", "Information", $"ReminderId={reminderId:D};BookingId={reminder?.BookingId:D};Result=Succeeded", cancellationToken: cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    public async Task<bool> DismissAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        var reminder = await repository.GetAsync(reminderId, cancellationToken).ConfigureAwait(false);
        var dismissed = await repository.MarkDismissedAsync(reminderId, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (dismissed) await auditLog.WriteAsync("BookingReminder", "Dismissed", "Information", $"ReminderId={reminderId:D};BookingId={reminder?.BookingId:D};Result=Succeeded", cancellationToken: cancellationToken).ConfigureAwait(false);
        return dismissed;
    }

    private static DateTimeOffset ResolvePlannedAt(ReminderDefinition reminder, ShootBooking booking) => reminder.Trigger.Kind switch
    {
        ReminderTriggerKind.AbsoluteTime => reminder.Trigger.At?.ToUniversalTime() ?? throw new ArgumentException("自定义提醒需要明确时间。"),
        ReminderTriggerKind.RelativeToBookingStart => booking.StartAtUtc - (reminder.Trigger.Offset ?? throw new ArgumentException("相对提醒需要提前量。")),
        _ => throw new NotSupportedException("2.3.0 不支持项目日期提醒。")
    };
}

public sealed class BookingReminderNotificationService(
    INotificationCenter notificationCenter,
    TimeZoneInfo? localTimeZone = null) : IBookingReminderNotificationService
{
    private readonly TimeZoneInfo _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
    public event EventHandler<ReminderPublishedEvent>? ReminderPublished;

    public NotificationMessage CreateNotification(ReminderDispatch dispatch)
    {
        var plannedLocal = TimeZoneInfo.ConvertTime(dispatch.PlannedAtUtc, _localTimeZone);
        var startLocal = TimeZoneInfo.ConvertTime(dispatch.Booking.StartAtUtc, _localTimeZone);
        var endLocal = TimeZoneInfo.ConvertTime(dispatch.Booking.EndAtUtc, _localTimeZone);
        var location = string.IsNullOrWhiteSpace(dispatch.Booking.Location) ? "未记录地点" : "地点已记录";
        var type = dispatch.Reminder.Trigger.Kind == ReminderTriggerKind.AbsoluteTime ? "自定义时间" : RelativeLabel(dispatch.Reminder.Trigger.Offset);
        var message = $"{type}；计划提醒 {plannedLocal:MM-dd HH:mm}；拍摄 {startLocal:MM-dd HH:mm}–{endLocal:HH:mm}；{location}";
        return new NotificationMessage(
            Guid.NewGuid(), NotificationType.Toast, dispatch.IsMissed ? NotificationSeverity.Warning : NotificationSeverity.Information,
            dispatch.IsMissed ? "错过的拍摄提醒" : "拍摄排期提醒", message, dispatch.Booking.Id, dispatch.Booking.ProjectId,
            [new("open", "打开排期"), new("later", "稍后查看"), new("ack", "知道了")], false, dispatch.TriggeredAtUtc,
            dispatch.TriggeredAtUtc.AddDays(7), $"booking-reminder:{dispatch.Reminder.Id:D}");
    }

    public async Task PublishAsync(ReminderDispatch dispatch, CancellationToken cancellationToken = default)
    {
        var notification = CreateNotification(dispatch);
        await notificationCenter.PublishAsync(notification, cancellationToken).ConfigureAwait(false);
        RaisePublished(dispatch, notification);
    }

    public void PublishPersisted(ReminderDispatch dispatch, NotificationMessage notification)
    {
        notificationCenter.NotifyPersisted(notification);
        RaisePublished(dispatch, notification);
    }

    private void RaisePublished(ReminderDispatch dispatch, NotificationMessage notification)
    {
        var handlers = ReminderPublished?.GetInvocationList();
        if (handlers is null) return;
        var published = new ReminderPublishedEvent(dispatch, notification);
        foreach (EventHandler<ReminderPublishedEvent> handler in handlers)
        {
            try { handler(this, published); }
            catch { /* UI notification handling must not affect persisted reminder state. */ }
        }
    }

    private static string RelativeLabel(TimeSpan? offset) => offset?.TotalMinutes switch
    {
        0 => "开始时",
        10 => "提前10分钟",
        30 => "提前30分钟",
        60 => "提前1小时",
        120 => "提前2小时",
        180 => "提前3小时",
        1440 => "提前1天",
        { } minutes => $"提前{minutes:0}分钟",
        _ => "排期提醒"
    };
}

public sealed class BookingReminderScheduler(
    IReminderRepository repository,
    IShootBookingService bookingService,
    IBookingReminderNotificationService notificationService,
    IAuditLogService auditLog,
    TimeProvider? timeProvider = null,
    TimeSpan? interval = null) : IBookingReminderScheduler
{
    public static readonly TimeSpan MissedWindow = TimeSpan.FromHours(24);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ITimer? _timer;
    private DateTimeOffset _lastCheckUtc;
    private bool _disposed;
    private bool _subscribed;

    public bool IsRunning => _timer is not null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BookingReminderScheduler));
        if (IsRunning) return;
        if (!_subscribed && bookingService is IBookingChangeNotifier notifier)
        {
            notifier.BookingChanged += BookingChanged;
            _subscribed = true;
        }
        await ProcessMissedAsync(cancellationToken).ConfigureAwait(false);
        _lastCheckUtc = _timeProvider.GetUtcNow();
        _timer = _timeProvider.CreateTimer(_ => _ = CheckDueSafelyAsync(), null, _interval, _interval);
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => CheckDueAsync(cancellationToken);

    public async Task ProcessMissedAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        await ProcessRangeAsync(now - MissedWindow, now.AddTicks(1), now, missed: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task CheckDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var from = _lastCheckUtc == default || now < _lastCheckUtc ? now - _interval : _lastCheckUtc;
        if (now - from > MissedWindow) from = now - MissedWindow;
        await ProcessRangeAsync(from, now.AddTicks(1), now, missed: false, cancellationToken).ConfigureAwait(false);
        _lastCheckUtc = now;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _timer?.Dispose();
        _timer = null;
        if (_subscribed && bookingService is IBookingChangeNotifier notifier)
        {
            notifier.BookingChanged -= BookingChanged;
            _subscribed = false;
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task ProcessRangeAsync(DateTimeOffset from, DateTimeOffset until, DateTimeOffset activeAt, bool missed, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return;
        try
        {
            while (true)
            {
                var due = await repository.ListDueActiveAsync(from, until, activeAt, 100, cancellationToken).ConfigureAwait(false);
                if (due.Count == 0) break;
                foreach (var reminder in due)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reminder.BookingId is null || reminder.Trigger.At is null) continue;
                    var booking = await bookingService.GetAsync(reminder.BookingId.Value, includeArchived: true, cancellationToken).ConfigureAwait(false);
                    if (booking is null || booking.IsArchived || booking.Status == ShootBookingStatus.Cancelled || booking.EndAtUtc <= activeAt) continue;
                    var isMissed = missed && reminder.Trigger.At < activeAt - TimeSpan.FromSeconds(1);
                    var dispatch = new ReminderDispatch(reminder, booking, reminder.Trigger.At.Value, activeAt, isMissed);
                    try
                    {
                        var notification = notificationService.CreateNotification(dispatch);
                        if (!await repository.TryTriggerWithNotificationAsync(reminder.Id, activeAt, notification, cancellationToken).ConfigureAwait(false)) continue;
                        notificationService.PublishPersisted(dispatch, notification);
                        await auditLog.WriteAsync("BookingReminder", isMissed ? "MissedTriggered" : "Triggered", "Information", $"ReminderId={reminder.Id:D};BookingId={booking.Id:D};Result=Succeeded", cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        await auditLog.WriteAsync("BookingReminder", "PublishFailed", "Warning", $"ReminderId={reminder.Id:D};BookingId={booking.Id:D};Result=Failed", errorCode: "REMINDER_NOTIFICATION_FAILED", cancellationToken: CancellationToken.None).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
                if (due.Count < 100) break;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task CheckDueSafelyAsync()
    {
        try { await CheckDueAsync().ConfigureAwait(false); }
        catch { await auditLog.WriteAsync("BookingReminder", "SchedulerFailed", "Warning", "Operation=SchedulerCheck;Result=Failed", errorCode: "REMINDER_SCHEDULER_FAILED").ConfigureAwait(false); }
    }

    private void BookingChanged(object? sender, Guid bookingId) => _ = CheckDueSafelyAsync();
}

public sealed class WorkbenchScheduleService(
    IShootBookingService bookingService,
    IBookingDocumentRepository documentRepository,
    IReminderRepository reminderRepository,
    TimeProvider? timeProvider = null,
    TimeZoneInfo? localTimeZone = null,
    IProjectRepository? projectRepository = null) : IWorkbenchScheduleService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly TimeZoneInfo _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;

    public async Task<WorkbenchScheduleSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, _localTimeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var startUtc = ToUtc(today);
        var endUtc = ToUtc(today.AddDays(8));
        var bookings = await bookingService.QueryCurrentViewAsync(new(startUtc, endUtc), cancellationToken).ConfigureAwait(false);
        var active = bookings.Where(x => x.Status != ShootBookingStatus.Cancelled).ToArray();
        var projectNames = projectRepository is null
            ? new Dictionary<Guid, string>()
            : (await projectRepository.ListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(item => item.Id, item => item.Name);
        var items = await Task.WhenAll(active.Select(item => BuildAsync(item, now, projectNames, cancellationToken))).ConfigureAwait(false);
        var todayItems = items.Where(item => item.StartAtUtc < ToUtc(today.AddDays(1)) && item.EndAtUtc > startUtc)
            .OrderByDescending(item => item.IsOngoing).ThenBy(item => item.EndAtUtc <= now).ThenBy(item => item.StartAtUtc).ToArray();
        var futureItems = items.Where(item =>
            {
                var localStart = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.StartAtUtc, _localTimeZone).DateTime);
                return localStart > today && localStart <= today.AddDays(7);
            })
            .OrderBy(item => item.StartAtUtc)
            .ThenBy(item => item.BookingId)
            .ToArray();
        var visibleFutureItems = futureItems.Take(5).ToArray();
        var future = visibleFutureItems
            .GroupBy(item => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.StartAtUtc, _localTimeZone).DateTime))
            .OrderBy(group => group.Key)
            .Select(group => new WorkbenchScheduleDay(group.Key, group.OrderBy(item => item.StartAtUtc).ToArray())).ToArray();
        return new(todayItems, future, today, futureItems.Length);
    }

    private async Task<WorkbenchScheduleItem> BuildAsync(ShootBookingSummary summary, DateTimeOffset now, IReadOnlyDictionary<Guid, string> projectNames, CancellationToken cancellationToken)
    {
        var documentsTask = documentRepository.ListByBookingAsync(summary.Id, cancellationToken);
        var remindersTask = reminderRepository.ListByBookingAsync(summary.Id, cancellationToken);
        var requirementsTask = bookingService.GetRequirementsAsync(summary.Id, cancellationToken);
        await Task.WhenAll(documentsTask, remindersTask, requirementsTask).ConfigureAwait(false);
        var requirements = requirementsTask.Result;
        var projectName = summary.ProjectId is { } projectId && projectNames.TryGetValue(projectId, out var name) && !string.IsNullOrWhiteSpace(name) ? name : summary.Title;
        return new(summary.Id, summary.ProjectId, summary.Title, summary.StartAtUtc, summary.EndAtUtc, summary.TimeZoneId, summary.Status,
            summary.IsAllDay, summary.StartAtUtc <= now && summary.EndAtUtc > now, !string.IsNullOrWhiteSpace(summary.Location),
            remindersTask.Result.Any(x => x.IsEnabled && x.Status == ReminderStatus.Scheduled), documentsTask.Result.Count,
            projectName, string.IsNullOrWhiteSpace(summary.Location) ? "未记录地点" : "地点已记录",
            requirements.Count(item => item.IsCompleted), requirements.Count);
    }

    private DateTimeOffset ToUtc(DateOnly date)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        while (_localTimeZone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = _localTimeZone.IsAmbiguousTime(local) ? _localTimeZone.GetAmbiguousTimeOffsets(local).Max() : _localTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
