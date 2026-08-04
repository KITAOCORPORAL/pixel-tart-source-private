using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public interface ILocalReminderScheduler
{
    bool IsEnabled { get; }
    Task ScheduleAsync(ReminderDefinition reminder, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid reminderId, CancellationToken cancellationToken = default);
}

public interface IReminderRepository
{
    Task SaveAsync(ReminderDefinition reminder, CancellationToken cancellationToken = default);
    Task<ReminderDefinition?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReminderDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReminderDefinition>> ListByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReminderDefinition>> ListDueAsync(DateTimeOffset fromUtc, DateTimeOffset untilUtc, int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReminderDefinition>> ListDueActiveAsync(DateTimeOffset fromUtc, DateTimeOffset untilUtc, DateTimeOffset activeAtUtc, int limit = 100, CancellationToken cancellationToken = default);
    Task DisableForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<bool> SetEnabledAsync(Guid id, bool enabled, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TryClaimTriggeredAsync(Guid id, DateTimeOffset triggeredAtUtc, CancellationToken cancellationToken = default);
    Task<bool> TryTriggerWithNotificationAsync(Guid id, DateTimeOffset triggeredAtUtc, NotificationMessage notification, CancellationToken cancellationToken = default);
    Task<bool> ReleaseTriggerClaimAsync(Guid id, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> MarkDismissedAsync(Guid id, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default);
}

public interface IBookingReminderService
{
    Task<IReadOnlyList<ReminderDefinition>> ListAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<ReminderDefinition> SaveAsync(ReminderDefinition reminder, CancellationToken cancellationToken = default);
    Task<bool> SetEnabledAsync(Guid reminderId, bool enabled, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid reminderId, CancellationToken cancellationToken = default);
    Task<bool> DismissAsync(Guid reminderId, CancellationToken cancellationToken = default);
}

public interface IBookingReminderNotificationService
{
    event EventHandler<ReminderPublishedEvent>? ReminderPublished;
    NotificationMessage CreateNotification(ReminderDispatch dispatch);
    Task PublishAsync(ReminderDispatch dispatch, CancellationToken cancellationToken = default);
    void PublishPersisted(ReminderDispatch dispatch, NotificationMessage notification);
}

public interface IBookingReminderScheduler : IAsyncDisposable
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task CheckDueAsync(CancellationToken cancellationToken = default);
    Task ProcessMissedAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IWorkbenchScheduleService
{
    Task<WorkbenchScheduleSnapshot> LoadAsync(CancellationToken cancellationToken = default);
}

public interface IProjectRelationshipService
{
    Task LinkAsync(Guid sourceProjectId, Guid relatedProjectId, string relationshipType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetRelatedProjectsAsync(Guid projectId, string? relationshipType = null, CancellationToken cancellationToken = default);
}

public sealed class DisabledLocalReminderScheduler : ILocalReminderScheduler
{
    public bool IsEnabled => false;
    public Task ScheduleAsync(ReminderDefinition reminder, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CancelAsync(Guid reminderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

