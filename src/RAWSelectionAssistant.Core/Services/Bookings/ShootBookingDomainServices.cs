using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public static class BookingMoneyCalculator
{
    public const string PaidExceedsTotalCode = "PaidAmountExceedsTotal";

    public static BookingMoneySummary Calculate(long? totalAmountMinor, long? depositAmountMinor, long? paidAmountMinor)
    {
        var warnings = new List<BookingMoneyWarning>();
        long? balance = totalAmountMinor.HasValue ? totalAmountMinor.Value - (paidAmountMinor ?? 0) : null;
        if (totalAmountMinor.HasValue && paidAmountMinor > totalAmountMinor)
            warnings.Add(new(PaidExceedsTotalCode, "已收金额高于当前拍摄总金额"));

        var displayKind = balance switch
        {
            null => BookingMoneyDisplayKind.Unknown,
            > 0 => BookingMoneyDisplayKind.Receivable,
            0 => BookingMoneyDisplayKind.Settled,
            _ => BookingMoneyDisplayKind.Overpaid
        };

        return new(totalAmountMinor, depositAmountMinor, paidAmountMinor, balance,
            balance.HasValue ? Math.Abs(balance.Value) : null, displayKind, warnings);
    }

    public static IReadOnlyList<string> Validate(long? totalAmountMinor, long? depositAmountMinor, long? paidAmountMinor, int currencyScale)
    {
        var errors = new List<string>();
        if (totalAmountMinor < 0) errors.Add("拍摄总金额不得为负数。");
        if (depositAmountMinor < 0) errors.Add("定金不得为负数。");
        if (paidAmountMinor < 0) errors.Add("已收金额不得为负数。");
        if (currencyScale is < 0 or > 4) errors.Add("货币小数位必须在0到4之间。");
        return errors;
    }
}

public static class ShootBookingTimeRules
{
    public static IReadOnlyList<string> Validate(DateTimeOffset startAt, DateTimeOffset endAt, string timeZoneId, bool isAllDay)
    {
        var errors = new List<string>();
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { errors.Add("拍摄时区不存在。"); return errors; }
        catch (InvalidTimeZoneException) { errors.Add("拍摄时区配置无效。"); return errors; }

        var startUtc = startAt.ToUniversalTime();
        var endUtc = endAt.ToUniversalTime();
        if (endUtc <= startUtc) errors.Add("结束时间必须晚于开始时间。");

        if (isAllDay)
        {
            var localStart = TimeZoneInfo.ConvertTime(startUtc, timeZone);
            var localEnd = TimeZoneInfo.ConvertTime(endUtc, timeZone);
            if (localStart.TimeOfDay != TimeSpan.Zero || localEnd.TimeOfDay != TimeSpan.Zero)
                errors.Add("全天排期必须从当地日期零点开始，并在结束日期零点结束。");
            if (localEnd.Date <= localStart.Date)
                errors.Add("全天排期至少覆盖一个完整日期，结束日期采用不包含边界。");
        }

        return errors;
    }

    public static (DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc) CreateAllDayRange(DateOnly startDate, DateOnly endDateExclusive, string timeZoneId)
    {
        if (endDateExclusive <= startDate) throw new ArgumentOutOfRangeException(nameof(endDateExclusive), "All-day end date must be after start date.");
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return (ResolveLocalMidnight(startDate, timeZone), ResolveLocalMidnight(endDateExclusive, timeZone));
    }

    private static DateTimeOffset ResolveLocalMidnight(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
            throw new InvalidOperationException("The selected local midnight does not exist in the requested time zone.");
        var offset = timeZone.IsAmbiguousTime(local)
            ? timeZone.GetAmbiguousTimeOffsets(local).Max()
            : timeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}

public sealed class BookingConflictDetector(IShootBookingRepository repository) : IBookingConflictDetector
{
    public async Task<IReadOnlyList<BookingConflict>> DetectAsync(Guid? bookingId, DateTimeOffset startAtUtc, DateTimeOffset endAtUtc, bool allowOverlap, CancellationToken cancellationToken = default)
    {
        var existing = await repository.FindOverlappingAsync(startAtUtc.ToUniversalTime(), endAtUtc.ToUniversalTime(), bookingId, cancellationToken).ConfigureAwait(false);
        return existing.Select(item =>
        {
            var overlapStart = item.StartAtUtc > startAtUtc ? item.StartAtUtc : startAtUtc;
            var overlapEnd = item.EndAtUtc < endAtUtc ? item.EndAtUtc : endAtUtc;
            return new BookingConflict(item.Id, item.Title, item.ClientDisplayName, item.StartAtUtc, item.EndAtUtc,
                item.Location, overlapEnd - overlapStart, item.AllowOverlap, !allowOverlap && !item.AllowOverlap);
        }).ToArray();
    }
}
