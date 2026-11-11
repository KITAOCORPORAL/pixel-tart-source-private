using RAWSelectionAssistant.Core.Models;
namespace Guangjing.Schedule.Server;
public sealed record ScheduleUpdate(long ExpectedRevision, string OperationId, DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc, ShootBookingStatus Status, BookingConflictResolution ConflictResolution=BookingConflictResolution.None);
public sealed record DeviceRequest(string Name,int ValidHours=48);
public sealed record ScheduleDevice(string Id,string Name,DateTimeOffset ExpiresAtUtc);
public sealed record ScheduleReceipt(Guid BookingId,long Revision,DateTimeOffset SavedAtUtc,string OperationId);
public sealed class ScheduleProblem(int status,string code,string message) : Exception(message)
{
    public int Status { get; }=status; public string Code { get; }=code;
}
