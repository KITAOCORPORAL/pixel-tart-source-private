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
    Task DisableForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
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

