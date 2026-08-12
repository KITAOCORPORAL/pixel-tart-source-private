using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Core.Services.Bookings;

public sealed class BookingWorkflowService(
    IShootBookingRepository repository,
    IAuditLogService? auditLog = null) : IBookingWorkflowService
{
    public Task<BookingWorkflowResult> SetDayAvailabilityAsync(DateTime date, bool isClosed, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new BookingWorkflowResult(Guid.Empty, BookingWorkflowOperationStatus.Succeeded, CalendarWorkflowState.Free, CalendarWorkflowState.Free));
    }

    public async Task<BookingWorkflowResult> MarkShootCompletedAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetAsync(bookingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (booking is null) return Missing(bookingId);
        var previous = CalendarWorkflowStateMapper.FromBookingStatus(booking.Status);
        if (booking.IsArchived || booking.Status == ShootBookingStatus.Cancelled)
            return Rejected(bookingId, previous, "BookingInactive", "已取消或已归档的拍摄不能标记完成。");
        if (previous == CalendarWorkflowState.Delivered)
            return Rejected(bookingId, previous, "AlreadyDelivered", "已交付项目不能直接撤销或重复标记拍摄完成。");
        if (previous == CalendarWorkflowState.PostProduction)
            return new(bookingId, BookingWorkflowOperationStatus.AlreadyApplied, previous, previous, booking.UpdatedAtUtc);

        var completedAt = DateTimeOffset.UtcNow;
        var changed = await repository.SetStatusAsync(bookingId, ShootBookingStatus.Completed, completedAt, cancellationToken).ConfigureAwait(false);
        if (!changed)
        {
            var current = await repository.GetAsync(bookingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
            if (current is not null && CalendarWorkflowStateMapper.FromBookingStatus(current.Status) == CalendarWorkflowState.PostProduction)
                return new(bookingId, BookingWorkflowOperationStatus.AlreadyApplied, previous, CalendarWorkflowState.PostProduction, current.UpdatedAtUtc);
            return Failed(bookingId, previous, "WorkflowWriteFailed", "拍摄完成状态未能保存。");
        }
        await WriteAuditAsync("ShootCompleted", bookingId, cancellationToken).ConfigureAwait(false);
        return new(bookingId, BookingWorkflowOperationStatus.Succeeded, previous, CalendarWorkflowState.PostProduction, completedAt);
    }

    public async Task<BookingWorkflowResult> UndoShootCompletedAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetAsync(bookingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (booking is null) return Missing(bookingId);
        var previous = CalendarWorkflowStateMapper.FromBookingStatus(booking.Status);
        if (booking.IsArchived || booking.Status == ShootBookingStatus.Cancelled)
            return Rejected(bookingId, previous, "BookingInactive", "已取消或已归档的拍摄不能撤销完成标记。");
        if (previous == CalendarWorkflowState.Delivered)
            return Rejected(bookingId, previous, "DeliveredCannotUndoShoot", "已交付项目不能直接撤销拍摄完成。");
        if (previous == CalendarWorkflowState.Scheduled)
            return new(bookingId, BookingWorkflowOperationStatus.AlreadyApplied, previous, previous);

        var changed = await repository.SetStatusAsync(bookingId, ShootBookingStatus.Confirmed, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (!changed)
        {
            var current = await repository.GetAsync(bookingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
            if (current is not null && CalendarWorkflowStateMapper.FromBookingStatus(current.Status) == CalendarWorkflowState.Scheduled)
                return new(bookingId, BookingWorkflowOperationStatus.AlreadyApplied, previous, CalendarWorkflowState.Scheduled);
            return Failed(bookingId, previous, "WorkflowWriteFailed", "撤销拍摄完成状态未能保存。");
        }
        await WriteAuditAsync("ShootCompletedUndone", bookingId, cancellationToken).ConfigureAwait(false);
        return new(bookingId, BookingWorkflowOperationStatus.Succeeded, previous, CalendarWorkflowState.Scheduled);
    }

    public async Task<BookingWorkflowResult> SetPostProductionStageAsync(Guid bookingId, CalendarPostProductionStage stage, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetAsync(bookingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (booking is null) return Missing(bookingId);
        var previous = CalendarWorkflowStateMapper.FromBookingStatus(booking.Status);
        if (booking.IsArchived || booking.Status == ShootBookingStatus.Cancelled)
            return Rejected(bookingId, previous, "BookingInactive", "已取消或已归档的拍摄不能更新后期阶段。");
        if (previous == CalendarWorkflowState.Delivered)
            return Rejected(bookingId, previous, "DeliveredCannotEditStage", "已交付项目不能直接修改后期阶段。");
        if (previous != CalendarWorkflowState.PostProduction)
            return Rejected(bookingId, previous, "InvalidWorkflowTransition", "拍摄完成后才能设置后期阶段。");

        var target = CalendarPostProductionStageMapper.ToBookingStatus(stage);
        if (booking.Status == target)
            return new(bookingId, BookingWorkflowOperationStatus.AlreadyApplied, previous, CalendarWorkflowState.PostProduction);
        var changed = await repository.SetStatusAsync(bookingId, target, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (!changed) return Failed(bookingId, previous, "WorkflowWriteFailed", "后期阶段未能保存。");
        await WriteAuditAsync("PostProductionStageChanged", bookingId, cancellationToken).ConfigureAwait(false);
        return new(bookingId, BookingWorkflowOperationStatus.Succeeded, previous, CalendarWorkflowState.PostProduction);
    }

    public async Task<BookingWorkflowResult> MarkDeliveredAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetAsync(bookingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (booking is null) return Missing(bookingId);
        var previous = CalendarWorkflowStateMapper.FromBookingStatus(booking.Status);
        if (booking.IsArchived || booking.Status == ShootBookingStatus.Cancelled)
            return Rejected(bookingId, previous, "BookingInactive", "已取消或已归档的拍摄不能标记交付。");
        if (previous == CalendarWorkflowState.Delivered)
            return new(bookingId, BookingWorkflowOperationStatus.AlreadyApplied, previous, previous);
        if (previous != CalendarWorkflowState.PostProduction)
            return Rejected(bookingId, previous, "InvalidWorkflowTransition", "拍摄完成后才能标记最终交付。");
        var changed = await repository.SetStatusAsync(bookingId, ShootBookingStatus.Delivered, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (!changed) return Failed(bookingId, previous, "WorkflowWriteFailed", "交付状态未能保存。");
        await WriteAuditAsync("Delivered", bookingId, cancellationToken).ConfigureAwait(false);
        return new(bookingId, BookingWorkflowOperationStatus.Succeeded, previous, CalendarWorkflowState.Delivered);
    }

    public async Task<BookingWorkflowResult> ReopenDeliveryAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetAsync(bookingId, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (booking is null) return Missing(bookingId);
        var previous = CalendarWorkflowStateMapper.FromBookingStatus(booking.Status);
        if (previous != CalendarWorkflowState.Delivered)
            return new(bookingId, BookingWorkflowOperationStatus.AlreadyApplied, previous, previous);
        if (booking.IsArchived || booking.Status == ShootBookingStatus.Cancelled)
            return Rejected(bookingId, previous, "BookingInactive", "已取消或已归档的拍摄不能重新打开。");
        var changed = await repository.SetStatusAsync(bookingId, ShootBookingStatus.AwaitingDelivery, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (!changed) return Failed(bookingId, previous, "WorkflowWriteFailed", "重新打开交付状态未能保存。");
        await WriteAuditAsync("DeliveryReopened", bookingId, cancellationToken).ConfigureAwait(false);
        return new(bookingId, BookingWorkflowOperationStatus.Succeeded, previous, CalendarWorkflowState.PostProduction);
    }

    private async Task WriteAuditAsync(string eventType, Guid bookingId, CancellationToken cancellationToken)
    {
        if (auditLog is not null) await auditLog.WriteAsync("BookingWorkflow", eventType, "Information", "拍摄工作流状态已更新。", correlationId: bookingId.ToString("N"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static BookingWorkflowResult Missing(Guid id) => new(id, BookingWorkflowOperationStatus.NotFound, CalendarWorkflowState.Free, CalendarWorkflowState.Free, ErrorCode: "BookingNotFound", ErrorMessage: "拍摄排期不存在。");
    private static BookingWorkflowResult Rejected(Guid id, CalendarWorkflowState state, string code, string message) => new(id, BookingWorkflowOperationStatus.Rejected, state, state, ErrorCode: code, ErrorMessage: message);
    private static BookingWorkflowResult Failed(Guid id, CalendarWorkflowState state, string code, string message) => new(id, BookingWorkflowOperationStatus.Failed, state, state, ErrorCode: code, ErrorMessage: message);
}

public static class CalendarWorkflowStateMapper
{
    public static CalendarWorkflowState FromBookingStatus(ShootBookingStatus status) => status switch
    {
        ShootBookingStatus.Cancelled or ShootBookingStatus.Draft => CalendarWorkflowState.Free,
        ShootBookingStatus.Completed or ShootBookingStatus.Shooting => CalendarWorkflowState.PostProduction,
        ShootBookingStatus.AwaitingSelectionDelivery or ShootBookingStatus.AwaitingSelection or ShootBookingStatus.Selected or
            ShootBookingStatus.AwaitingRetouch or ShootBookingStatus.Retouched or ShootBookingStatus.AwaitingDelivery => CalendarWorkflowState.PostProduction,
        ShootBookingStatus.Delivered => CalendarWorkflowState.Delivered,
        _ => CalendarWorkflowState.Scheduled
    };

    public static string DisplayName(CalendarWorkflowState state) => state switch
    {
        CalendarWorkflowState.Free => "空闲",
        CalendarWorkflowState.Scheduled => "待拍摄",
        CalendarWorkflowState.PostProduction => "后期待处理",
        CalendarWorkflowState.Delivered => "已交付",
        _ => "未知状态"
    };
}

public static class CalendarPostProductionStageMapper
{
    public static ShootBookingStatus ToBookingStatus(CalendarPostProductionStage stage) => stage switch
    {
        CalendarPostProductionStage.AwaitingSelection or CalendarPostProductionStage.ClientSelecting => ShootBookingStatus.AwaitingSelection,
        CalendarPostProductionStage.Selected => ShootBookingStatus.Selected,
        CalendarPostProductionStage.Retouching => ShootBookingStatus.AwaitingRetouch,
        CalendarPostProductionStage.PendingDelivery or CalendarPostProductionStage.WaitingClientConfirm => ShootBookingStatus.AwaitingDelivery,
        _ => ShootBookingStatus.AwaitingDelivery
    };

    public static CalendarPostProductionStage FromBookingStatus(ShootBookingStatus status) => status switch
    {
        ShootBookingStatus.Selected => CalendarPostProductionStage.Selected,
        ShootBookingStatus.AwaitingRetouch or ShootBookingStatus.Retouched => CalendarPostProductionStage.Retouching,
        ShootBookingStatus.AwaitingDelivery => CalendarPostProductionStage.PendingDelivery,
        _ => CalendarPostProductionStage.AwaitingSelection
    };
}

public sealed record CalendarDayVisualState(
    DateTime Date,
    CalendarWorkflowState WorkflowState,
    int BookingCount,
    bool IsClosed,
    bool IsToday,
    bool IsSelected,
    string BadgeBrushKey,
    string BadgeForegroundBrushKey,
    bool LockVisible,
    bool TodayRingVisible,
    bool SelectedBorderVisible,
    string SummaryState)
{
    public bool HasBookings => BookingCount > 0;
    public CalendarWorkflowStatus PrimaryWorkflowStatus { get; init; } = WorkflowState switch
    {
        CalendarWorkflowState.Scheduled => CalendarWorkflowStatus.Scheduled,
        CalendarWorkflowState.PostProduction => CalendarWorkflowStatus.PendingDelivery,
        CalendarWorkflowState.Delivered => CalendarWorkflowStatus.Delivered,
        _ => CalendarWorkflowStatus.Scheduled
    };
}

public static class CalendarDayVisualStateResolver
{
    public const string FreeBrush = "CalendarStatusFreeBrush";
    public const string ScheduledBrush = "CalendarStatusScheduledBrush";
    public const string PostProductionBrush = "CalendarStatusPendingDeliveryBrush";
    public const string DeliveredBrush = "CalendarStatusDeliveredBrush";

    public static CalendarDayVisualState Resolve(DateTime date, IReadOnlyList<ShootBookingSummary> bookings, bool isClosed, bool isToday, bool isSelected)
    {
        var active = bookings.Where(item => !item.IsArchived && item.Status != ShootBookingStatus.Cancelled && item.Status != ShootBookingStatus.Draft).ToArray();
        var state = ResolveWorkflowState(active.Select(item => item.Status));
        var brush = state switch
        {
            CalendarWorkflowState.Scheduled => ScheduledBrush,
            CalendarWorkflowState.PostProduction => PostProductionBrush,
            CalendarWorkflowState.Delivered => DeliveredBrush,
            _ => FreeBrush
        };
        var foreground = state == CalendarWorkflowState.PostProduction ? "CalendarStatusPendingDeliveryForegroundBrush" : state == CalendarWorkflowState.Delivered ? "CalendarStatusDeliveredForegroundBrush" : state == CalendarWorkflowState.Scheduled ? "CalendarStatusScheduledForegroundBrush" : "CalendarStatusFreeForegroundBrush";
        var result = new CalendarDayVisualState(date.Date, state, active.Length, isClosed, isToday, isSelected, brush, foreground, isClosed, isToday, isSelected, CalendarWorkflowStateMapper.DisplayName(state));
        return result with { PrimaryWorkflowStatus = ResolveLegacyStatus(active.Select(item => item.Status)) };
    }

    public static CalendarDayVisualState Resolve<TBooking>(DateTime date, IReadOnlyList<TBooking> bookings, bool isClosed, bool isToday, bool isSelected)
        where TBooking : ICalendarWorkflowBooking
    {
        var active = bookings.Where(item => !item.IsArchived && item.BookingStatus != ShootBookingStatus.Cancelled && item.BookingStatus != ShootBookingStatus.Draft).ToArray();
        var states = active.Select(item => item.BookingStatus).ToArray();
        var state = ResolveWorkflowState(states);
        var brush = state switch
        {
            CalendarWorkflowState.Scheduled => ScheduledBrush,
            CalendarWorkflowState.PostProduction => PostProductionBrush,
            CalendarWorkflowState.Delivered => DeliveredBrush,
            _ => FreeBrush
        };
        var foreground = state == CalendarWorkflowState.PostProduction ? "CalendarStatusPendingDeliveryForegroundBrush" : state == CalendarWorkflowState.Delivered ? "CalendarStatusDeliveredForegroundBrush" : state == CalendarWorkflowState.Scheduled ? "CalendarStatusScheduledForegroundBrush" : "CalendarStatusFreeForegroundBrush";
        var result = new CalendarDayVisualState(date.Date, state, active.Length, isClosed, isToday, isSelected, brush, foreground, isClosed, isToday, isSelected, CalendarWorkflowStateMapper.DisplayName(state));
        return result with { PrimaryWorkflowStatus = ResolveLegacyStatus(active.OrderBy(item => item.SortStart).Select(item => item.BookingStatus)) };
    }

    public static CalendarWorkflowState ResolveWorkflowState(IEnumerable<ShootBookingStatus> statuses)
    {
        var values = statuses.ToArray();
        if (values.Any(status => CalendarWorkflowStateMapper.FromBookingStatus(status) == CalendarWorkflowState.Scheduled)) return CalendarWorkflowState.Scheduled;
        if (values.Any(status => CalendarWorkflowStateMapper.FromBookingStatus(status) == CalendarWorkflowState.PostProduction)) return CalendarWorkflowState.PostProduction;
        if (values.Any(status => CalendarWorkflowStateMapper.FromBookingStatus(status) == CalendarWorkflowState.Delivered)) return CalendarWorkflowState.Delivered;
        return CalendarWorkflowState.Free;
    }

    private static CalendarWorkflowStatus ResolveLegacyStatus(IEnumerable<ShootBookingStatus> statuses)
    {
        var values = statuses.Select(CalendarWorkflowStatusMapper.FromBookingStatus).ToArray();
        if (values.Length == 0) return CalendarWorkflowStatus.Scheduled;
        return values.OrderBy(value => value switch
        {
            CalendarWorkflowStatus.Scheduled => 0,
            CalendarWorkflowStatus.PendingDelivery => 1,
            CalendarWorkflowStatus.Delivered => 2,
            CalendarWorkflowStatus.Shot => 1,
            _ => int.MaxValue
        }).First();
    }
}

public static class CalendarStatusBrushResolver
{
    public const string Free = CalendarDayVisualStateResolver.FreeBrush;
    public const string Scheduled = CalendarDayVisualStateResolver.ScheduledBrush;
    public const string Shot = "CalendarStatusShotBrush";
    public const string PendingReturn = CalendarDayVisualStateResolver.PostProductionBrush;
    public const string Returned = CalendarDayVisualStateResolver.DeliveredBrush;

    public static string Resolve(CalendarDayVisualState state) => state.BadgeBrushKey;
    public static string Resolve(CalendarWorkflowStatus status) => status switch
    {
        CalendarWorkflowStatus.Shot => Shot,
        CalendarWorkflowStatus.PendingDelivery => PendingReturn,
        CalendarWorkflowStatus.Delivered => Returned,
        _ => Scheduled
    };
}
