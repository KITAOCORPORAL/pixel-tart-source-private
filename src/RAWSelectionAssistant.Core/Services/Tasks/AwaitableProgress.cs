namespace RAWSelectionAssistant.Core.Services.Tasks;

/// <summary>
/// Preserves the normal <see cref="IProgress{T}"/> contract while exposing an
/// explicit completion boundary for persistence-sensitive task workflows.
/// </summary>
public sealed class AwaitableProgress<T>(Func<T, Task> report) : IProgress<T>
{
    private readonly object _sync = new();
    private readonly List<Task> _pending = [];

    public void Report(T value)
    {
        Task task;
        try { task = report(value); }
        catch (Exception ex) { task = Task.FromException(ex); }
        lock (_sync) _pending.Add(task);
    }

    public Task DrainAsync()
    {
        lock (_sync) return _pending.Count == 0 ? Task.CompletedTask : Task.WhenAll(_pending.ToArray());
    }
}
