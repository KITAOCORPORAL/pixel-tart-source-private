namespace RAWSelectionAssistant.Core.Models;

public enum ReminderStatus { Disabled, Scheduled, Triggered, Dismissed, Cancelled }
public enum ReminderTriggerKind { AbsoluteTime, RelativeToProjectDate }

public sealed record ReminderTrigger(ReminderTriggerKind Kind, DateTimeOffset? At, TimeSpan? Offset);
public sealed record ReminderDefinition(Guid Id, Guid? ProjectId, string Title, string Description, ReminderTrigger Trigger, ReminderStatus Status = ReminderStatus.Disabled);

