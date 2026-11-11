using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Core.Services.Bookings;
public sealed class BookingVersionConflictException() : IOException("预约已由另一端更新，请刷新后重新编辑；本次输入未覆盖新版本。");
public sealed class BookingOperationConflictException() : IOException("重试标识已用于其他修改，请刷新后重试。");

public static class BookingDraftFactory
{
    public static ShootBookingDraft From(ShootBooking b, IReadOnlyList<ShootRequirementItem> requirements) => new()
    {
        Id=b.Id, ExpectedRevision=b.Revision, ProjectId=b.ProjectId, Title=b.Title,ClientDisplayName=b.ClientDisplayName,
        StartAt=b.StartAtUtc,EndAt=b.EndAtUtc,TimeZoneId=b.TimeZoneId,IsAllDay=b.IsAllDay,Status=b.Status,
        Location=b.Location,ShootingType=b.ShootingType,ShootingRequirements=b.ShootingRequirements,PreparationNotes=b.PreparationNotes,
        TotalAmountMinor=b.TotalAmountMinor,DepositAmountMinor=b.DepositAmountMinor,PaidAmountMinor=b.PaidAmountMinor,
        CurrencyCode=b.CurrencyCode,CurrencyScale=b.CurrencyScale,ContactName=b.ContactName,ContactPhone=b.ContactPhone,
        AllowOverlap=b.AllowOverlap,Notes=b.Notes,PreBufferMinutes=b.PreBufferMinutes,PostBufferMinutes=b.PostBufferMinutes,
        HoldExpiresAtUtc=b.HoldExpiresAtUtc,PaymentState=b.PaymentState,Requirements=requirements,ReplacePeople=false
    };
}
