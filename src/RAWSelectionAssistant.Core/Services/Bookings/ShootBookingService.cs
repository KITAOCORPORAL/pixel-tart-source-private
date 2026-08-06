using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public sealed class ShootBookingService(
    IShootBookingRepository repository,
    IBookingConflictDetector conflictDetector,
    IAuditLogService? auditLog = null) : IShootBookingService, IBookingChangeNotifier
{
    public event EventHandler<Guid>? BookingChanged;

    public async Task<BookingSaveResult> SaveAsync(ShootBookingDraft draft, BookingConflictResolution conflictResolution = BookingConflictResolution.None, CancellationToken cancellationToken = default)
    {
        var money = BookingMoneyCalculator.Calculate(draft.TotalAmountMinor, draft.DepositAmountMinor, draft.PaidAmountMinor);
        var validation = Validate(draft).ToList();
        if (draft.Id is { } existingId)
        {
            var existing = await repository.GetAsync(existingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
            if (existing is null) return new(BookingSaveStatus.NotFound, null, money, [], ["拍摄排期不存在。"]);
            if (existing.IsArchived) validation.Add("已归档排期必须先恢复后才能编辑。");
        }
        if (validation.Count > 0) return BookingSaveResult.ValidationFailed(money, validation);

        var startUtc = draft.StartAt.ToUniversalTime();
        var endUtc = draft.EndAt.ToUniversalTime();
        var effectiveAllowOverlap = draft.AllowOverlap || conflictResolution == BookingConflictResolution.MarkAllowOverlap;
        var conflicts = await conflictDetector.DetectAsync(draft.Id, startUtc, endUtc, effectiveAllowOverlap, cancellationToken).ConfigureAwait(false);
        if (conflicts.Any(x => x.IsBlocking) && conflictResolution == BookingConflictResolution.None)
            return new(BookingSaveStatus.NeedsAttention, null, money, conflicts, []);

        var now = DateTimeOffset.UtcNow;
        var previous = draft.Id is { } id ? await repository.GetAsync(id, includeArchived: true, cancellationToken).ConfigureAwait(false) : null;
        var bookingId = draft.Id ?? Guid.NewGuid();
        var booking = new ShootBooking
        {
            Id = bookingId,
            ProjectId = draft.ProjectId,
            Title = draft.Title.Trim(),
            ClientDisplayName = draft.ClientDisplayName.Trim(),
            StartAtUtc = startUtc,
            EndAtUtc = endUtc,
            TimeZoneId = draft.TimeZoneId,
            IsAllDay = draft.IsAllDay,
            Status = draft.Status,
            Location = Clean(draft.Location),
            ShootingType = draft.ShootingType.Trim(),
            ShootingRequirements = Clean(draft.ShootingRequirements),
            PreparationNotes = Clean(draft.PreparationNotes),
            TotalAmountMinor = draft.TotalAmountMinor,
            DepositAmountMinor = draft.DepositAmountMinor,
            PaidAmountMinor = draft.PaidAmountMinor,
            CurrencyCode = draft.CurrencyCode.Trim().ToUpperInvariant(),
            CurrencyScale = draft.CurrencyScale,
            ContactName = Clean(draft.ContactName),
            ContactPhone = Clean(draft.ContactPhone),
            AllowOverlap = effectiveAllowOverlap,
            ConflictOverride = conflicts.Count > 0 && conflictResolution == BookingConflictResolution.SaveAnyway,
            Notes = Clean(draft.Notes),
            CreatedAtUtc = previous?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now,
            IsArchived = false,
            ArchivedAtUtc = null
        };
        var requirements = draft.Requirements.Select((item, index) => item with
        {
            BookingId = bookingId,
            SortOrder = item.SortOrder < 0 ? index : item.SortOrder,
            ItemText = item.ItemText.Trim(),
            UpdatedAtUtc = now,
            CreatedAtUtc = item.CreatedAtUtc == default ? now : item.CreatedAtUtc,
            CompletedAtUtc = item.IsCompleted ? item.CompletedAtUtc ?? now : null
        }).ToArray();

        await repository.SaveAsync(booking, requirements, cancellationToken).ConfigureAwait(false);
        if (auditLog is not null)
            await auditLog.WriteAsync("Booking", previous is null ? "Created" : "Updated", "Information", "拍摄排期记录已保存。", projectId: booking.ProjectId, cancellationToken: cancellationToken).ConfigureAwait(false);
        BookingChanged?.Invoke(this, booking.Id);
        return new(BookingSaveStatus.Saved, booking, money, conflicts, []);
    }

    public Task<ShootBooking?> GetAsync(Guid id, bool includeArchived = false, CancellationToken cancellationToken = default) =>
        repository.GetAsync(id, includeArchived, cancellationToken);

    public Task<IReadOnlyList<ShootRequirementItem>> GetRequirementsAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        repository.GetRequirementsAsync(bookingId, cancellationToken);

    public Task<IReadOnlyList<ShootBookingSummary>> QueryCurrentViewAsync(ShootBookingQuery query, CancellationToken cancellationToken = default) =>
        repository.QueryCurrentViewAsync(query, cancellationToken);

    public Task<ShootBookingPage> SearchAllUnarchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) =>
        repository.SearchAllUnarchivedAsync(request with { PageSize = Math.Clamp(request.PageSize, 1, 100) }, cancellationToken);

    public Task<ShootBookingPage> SearchArchivedAsync(ShootBookingSearchRequest request, CancellationToken cancellationToken = default) =>
        repository.SearchArchivedAsync(request with { PageSize = Math.Clamp(request.PageSize, 1, 100) }, cancellationToken);

    public async Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var completed = await repository.CompleteAsync(id, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (completed && auditLog is not null)
            await auditLog.WriteAsync("Booking", "Completed", "Information", "拍摄排期已标记完成，未触发提醒已关闭。", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (completed) BookingChanged?.Invoke(this, id);
        return completed;
    }

    public async Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var archived = await repository.ArchiveAsync(id, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (archived && auditLog is not null)
            await auditLog.WriteAsync("Booking", "Archived", "Information", "拍摄排期记录已归档，关联提醒已禁用。", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (archived) BookingChanged?.Invoke(this, id);
        return archived;
    }

    public async Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var restored = await repository.RestoreAsync(id, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (restored && auditLog is not null)
            await auditLog.WriteAsync("Booking", "Restored", "Information", "拍摄排期记录已恢复，提醒保持关闭。", cancellationToken: cancellationToken).ConfigureAwait(false);
        if (restored) BookingChanged?.Invoke(this, id);
        return restored;
    }

    private static IEnumerable<string> Validate(ShootBookingDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Title)) yield return "项目名称不能为空。";
        if (string.IsNullOrWhiteSpace(draft.ShootingType)) yield return "拍摄类型不能为空。";
        if (draft.CurrencyCode.Trim().Length != 3) yield return "货币代码必须为三位字符。";
        foreach (var error in ShootBookingTimeRules.Validate(draft.StartAt, draft.EndAt, draft.TimeZoneId, draft.IsAllDay)) yield return error;
        foreach (var error in BookingMoneyCalculator.Validate(draft.TotalAmountMinor, draft.DepositAmountMinor, draft.PaidAmountMinor, draft.CurrencyScale)) yield return error;
        foreach (var item in draft.Requirements)
            if (string.IsNullOrWhiteSpace(item.ItemText)) yield return "准备清单项目不能为空。";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
