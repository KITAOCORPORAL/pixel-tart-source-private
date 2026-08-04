namespace RAWSelectionAssistant.Core.Models;

public enum NotificationType { Toast, InlineError, Modal, TaskNotification, EmptyState, SystemBanner }
public enum NotificationSeverity { Information, Success, Warning, Error }

public sealed record NotificationAction(string Id, string Label, bool IsDestructive = false);

public sealed record NotificationMessage(
    Guid Id,
    NotificationType Type,
    NotificationSeverity Severity,
    string Title,
    string Message,
    Guid? TaskId,
    Guid? ProjectId,
    IReadOnlyList<NotificationAction> Actions,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt = null,
    string? DeduplicationKey = null);

public sealed record NotificationHistory(IReadOnlyList<NotificationMessage> Items);

