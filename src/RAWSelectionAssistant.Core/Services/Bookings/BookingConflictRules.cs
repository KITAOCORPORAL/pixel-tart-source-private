using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public enum BookingConflictKind { HardOverlap, BufferOverlap, HoldOverlap, LockedOverlap }
public sealed record BookingBufferPolicy(int PreBufferMinutes = 0, int PostBufferMinutes = 0)
{
    public BookingBufferPolicy Validate() => this with { PreBufferMinutes = Math.Clamp(PreBufferMinutes, 0, 24 * 60), PostBufferMinutes = Math.Clamp(PostBufferMinutes, 0, 24 * 60) };
}
public sealed record BufferedBookingInterval(Guid BookingId, DateTimeOffset OccupiedStartUtc, DateTimeOffset OccupiedEndUtc, ShootBookingStatus Status, bool IsHoldExpired = false);
public sealed record StructuredBookingConflict(Guid ExistingBookingId, Guid CandidateBookingId, BookingConflictKind Kind, DateTimeOffset OverlapStartUtc, DateTimeOffset OverlapEndUtc, bool IsBlocking, string Reason);

public static class BookingConflictRules
{
    public static BufferedBookingInterval ToOccupiedInterval(ShootBooking booking, BookingBufferPolicy policy, DateTimeOffset nowUtc)
    {
        var normalized = policy.Validate();
        return new(booking.Id, booking.StartAtUtc.ToUniversalTime().Subtract(TimeSpan.FromMinutes(normalized.PreBufferMinutes)), booking.EndAtUtc.ToUniversalTime().Add(TimeSpan.FromMinutes(normalized.PostBufferMinutes)), booking.Status);
    }

    public static IReadOnlyList<StructuredBookingConflict> Detect(BufferedBookingInterval candidate, IEnumerable<BufferedBookingInterval> existing)
    {
        var result = new List<StructuredBookingConflict>();
        foreach (var item in existing.Where(item => item.BookingId != candidate.BookingId))
        {
            var start = candidate.OccupiedStartUtc > item.OccupiedStartUtc ? candidate.OccupiedStartUtc : item.OccupiedStartUtc;
            var end = candidate.OccupiedEndUtc < item.OccupiedEndUtc ? candidate.OccupiedEndUtc : item.OccupiedEndUtc;
            if (end <= start) continue;
            var kind = candidate.OccupiedStartUtc >= item.OccupiedStartUtc && candidate.OccupiedEndUtc <= item.OccupiedEndUtc || item.OccupiedStartUtc >= candidate.OccupiedStartUtc && item.OccupiedEndUtc <= candidate.OccupiedEndUtc ? BookingConflictKind.HardOverlap : BookingConflictKind.BufferOverlap;
            if (item.Status == ShootBookingStatus.Tentative) kind = BookingConflictKind.HoldOverlap;
            if (item.Status == ShootBookingStatus.Confirmed) kind = BookingConflictKind.LockedOverlap;
            result.Add(new(item.BookingId, candidate.BookingId, kind, start, end, kind is BookingConflictKind.HardOverlap or BookingConflictKind.LockedOverlap, $"{kind} between occupied booking intervals"));
        }
        return result;
    }
}
