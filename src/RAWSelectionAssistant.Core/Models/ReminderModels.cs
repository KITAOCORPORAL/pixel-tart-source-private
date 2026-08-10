namespace RAWSelectionAssistant.Core.Models;

public enum ReminderStatus { Disabled, Scheduled, Triggered, Dismissed, Cancelled }
public enum ReminderTriggerKind { AbsoluteTime, RelativeToProjectDate, RelativeToBookingStart }

public sealed record ReminderTrigger(ReminderTriggerKind Kind, DateTimeOffset? At, TimeSpan? Offset);
public sealed record ReminderDefinition(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string Description,
    ReminderTrigger Trigger,
    ReminderStatus Status = ReminderStatus.Disabled,
    Guid? BookingId = null,
    bool IsEnabled = false,
    DateTimeOffset? LastTriggeredAt = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null);

public sealed record ReminderDispatch(
    ReminderDefinition Reminder,
    ShootBooking Booking,
    DateTimeOffset PlannedAtUtc,
    DateTimeOffset TriggeredAtUtc,
    bool IsMissed);

public sealed record ReminderPublishedEvent(ReminderDispatch Dispatch, NotificationMessage Notification);

public sealed record WorkbenchScheduleItem(
    Guid BookingId,
    Guid? ProjectId,
    string Title,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string TimeZoneId,
    ShootBookingStatus Status,
    bool IsAllDay,
    bool IsOngoing,
    bool HasLocation,
    bool HasEnabledReminder,
    int DocumentCount,
    string ProjectName,
    string LocationDisplay,
    int RequirementCompleted,
    int RequirementTotal,
    string ClientDisplayName = "");

public sealed record WorkbenchScheduleDay(DateOnly Date, IReadOnlyList<WorkbenchScheduleItem> Items);

public sealed record WorkbenchScheduleSnapshot(
    IReadOnlyList<WorkbenchScheduleItem> Today,
    IReadOnlyList<WorkbenchScheduleDay> FutureSevenDays,
    DateOnly LocalDate,
    int FutureTotalCount);

