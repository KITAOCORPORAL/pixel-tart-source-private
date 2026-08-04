using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tasks;

public static class TaskStateMachine
{
    private static readonly IReadOnlyDictionary<TaskLifecycleState, HashSet<TaskLifecycleState>> Allowed =
        new Dictionary<TaskLifecycleState, HashSet<TaskLifecycleState>>
        {
            [TaskLifecycleState.Pending] = [TaskLifecycleState.Preparing, TaskLifecycleState.Cancelling, TaskLifecycleState.Cancelled, TaskLifecycleState.Failed],
            [TaskLifecycleState.Preparing] = [TaskLifecycleState.Scanning, TaskLifecycleState.Validating, TaskLifecycleState.Running, TaskLifecycleState.Cancelling, TaskLifecycleState.Failed, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.Scanning] = [TaskLifecycleState.Validating, TaskLifecycleState.Running, TaskLifecycleState.Pausing, TaskLifecycleState.Cancelling, TaskLifecycleState.Failed, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.Validating] = [TaskLifecycleState.WaitingForConfirmation, TaskLifecycleState.Running, TaskLifecycleState.NeedsAttention, TaskLifecycleState.Cancelling, TaskLifecycleState.Failed, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.WaitingForConfirmation] = [TaskLifecycleState.Running, TaskLifecycleState.NeedsAttention, TaskLifecycleState.Cancelling, TaskLifecycleState.Cancelled, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.Running] = [TaskLifecycleState.Pausing, TaskLifecycleState.NeedsAttention, TaskLifecycleState.Retrying, TaskLifecycleState.Cancelling, TaskLifecycleState.PartiallyCompleted, TaskLifecycleState.Failed, TaskLifecycleState.Completed, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.Pausing] = [TaskLifecycleState.Paused, TaskLifecycleState.Cancelling, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.Paused] = [TaskLifecycleState.Running, TaskLifecycleState.Cancelling, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.NeedsAttention] = [TaskLifecycleState.Running, TaskLifecycleState.Retrying, TaskLifecycleState.Cancelling, TaskLifecycleState.Cancelled, TaskLifecycleState.Failed, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.Retrying] = [TaskLifecycleState.Running, TaskLifecycleState.NeedsAttention, TaskLifecycleState.Cancelling, TaskLifecycleState.Failed, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.Cancelling] = [TaskLifecycleState.Cancelled, TaskLifecycleState.PartiallyCompleted, TaskLifecycleState.Failed, TaskLifecycleState.Interrupted],
            [TaskLifecycleState.Interrupted] = [TaskLifecycleState.Retrying, TaskLifecycleState.NeedsAttention, TaskLifecycleState.Cancelled],
            [TaskLifecycleState.Failed] = [TaskLifecycleState.Retrying],
            [TaskLifecycleState.PartiallyCompleted] = [TaskLifecycleState.Retrying],
            [TaskLifecycleState.Cancelled] = [TaskLifecycleState.Retrying],
            [TaskLifecycleState.Completed] = []
        };

    public static bool CanTransition(TaskLifecycleState from, TaskLifecycleState to) => from == to || Allowed[from].Contains(to);

    public static void EnsureTransition(TaskLifecycleState from, TaskLifecycleState to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"{ErrorCodeCatalog.InvalidStateTransition}: {from} -> {to}");
    }

    public static bool IsTerminal(TaskLifecycleState state) => state is TaskLifecycleState.Completed or TaskLifecycleState.Cancelled or TaskLifecycleState.PartiallyCompleted or TaskLifecycleState.Failed;
    public static bool IsUnexpectedActive(TaskLifecycleState state) => state is TaskLifecycleState.Preparing or TaskLifecycleState.Scanning or TaskLifecycleState.Validating or TaskLifecycleState.WaitingForConfirmation or TaskLifecycleState.Running or TaskLifecycleState.Pausing or TaskLifecycleState.Paused or TaskLifecycleState.NeedsAttention or TaskLifecycleState.Retrying or TaskLifecycleState.Cancelling;
}

