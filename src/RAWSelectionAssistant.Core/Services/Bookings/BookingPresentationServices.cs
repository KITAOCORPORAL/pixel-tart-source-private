using System.Globalization;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public interface IBookingTimeDisplayService
{
    TimeZoneInfo ResolveTimeZone(string? timeZoneId);
    DateTimeOffset ToBookingTime(DateTimeOffset utcValue, string? timeZoneId);
    string FormatRange(DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, string? timeZoneId, bool isAllDay);
    string FriendlyTimeZoneName(string? timeZoneId);
}

public sealed class BookingTimeDisplayService : IBookingTimeDisplayService
{
    public static BookingTimeDisplayService Default { get; } = new();
    private readonly TimeZoneInfo _fallbackTimeZone;

    public BookingTimeDisplayService(TimeZoneInfo? fallbackTimeZone = null) => _fallbackTimeZone = fallbackTimeZone ?? TimeZoneInfo.Local;

    public TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return _fallbackTimeZone;
    }

    public DateTimeOffset ToBookingTime(DateTimeOffset utcValue, string? timeZoneId) =>
        TimeZoneInfo.ConvertTime(utcValue, ResolveTimeZone(timeZoneId));

    public string FormatRange(DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, string? timeZoneId, bool isAllDay)
    {
        var start = ToBookingTime(startAtUtc, timeZoneId);
        var end = ToBookingTime(endAtUtc, timeZoneId);
        return isAllDay
            ? $"{start:yyyy-MM-dd} 至 {end.AddDays(-1):yyyy-MM-dd}（全天）"
            : $"{start:yyyy-MM-dd HH:mm} — {end:yyyy-MM-dd HH:mm}";
    }

    public string FriendlyTimeZoneName(string? timeZoneId)
    {
        var zone = ResolveTimeZone(timeZoneId);
        var offset = zone.GetUtcOffset(DateTimeOffset.UtcNow);
        var offsetText = offset == TimeSpan.Zero
            ? "UTC"
            : string.Create(CultureInfo.InvariantCulture, $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours)}{(offset.Minutes == 0 ? string.Empty : $":{Math.Abs(offset.Minutes):00}")}");
        if (string.Equals(zone.Id, "China Standard Time", StringComparison.OrdinalIgnoreCase)) return $"中国标准时间 {offsetText}";
        var name = zone.DisplayName;
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, zone.Id, StringComparison.OrdinalIgnoreCase)) name = "拍摄地时间";
        return $"{name} {offsetText}";
    }
}

public static class CalendarWorkflowStatusMapper
{
    public static CalendarWorkflowStatus FromBookingStatus(ShootBookingStatus status) => status switch
    {
        ShootBookingStatus.Shooting or ShootBookingStatus.Completed => CalendarWorkflowStatus.Shot,
        ShootBookingStatus.AwaitingSelectionDelivery or ShootBookingStatus.AwaitingSelection or ShootBookingStatus.Selected or
            ShootBookingStatus.AwaitingRetouch or ShootBookingStatus.Retouched or ShootBookingStatus.AwaitingDelivery => CalendarWorkflowStatus.PendingDelivery,
        ShootBookingStatus.Delivered => CalendarWorkflowStatus.Delivered,
        _ => CalendarWorkflowStatus.Scheduled
    };

    public static ShootBookingStatus ToBookingStatus(CalendarWorkflowStatus status) => status switch
    {
        CalendarWorkflowStatus.Scheduled => ShootBookingStatus.Confirmed,
        CalendarWorkflowStatus.Shot => ShootBookingStatus.Completed,
        CalendarWorkflowStatus.PendingDelivery => ShootBookingStatus.AwaitingDelivery,
        CalendarWorkflowStatus.Delivered => ShootBookingStatus.Delivered,
        _ => ShootBookingStatus.Confirmed
    };

    public static string DisplayName(CalendarWorkflowStatus status) => status switch
    {
        CalendarWorkflowStatus.Scheduled => "有拍摄",
        CalendarWorkflowStatus.Shot => "已拍摄",
        CalendarWorkflowStatus.PendingDelivery => "待返图",
        CalendarWorkflowStatus.Delivered => "已返图",
        _ => "未知状态"
    };
}
