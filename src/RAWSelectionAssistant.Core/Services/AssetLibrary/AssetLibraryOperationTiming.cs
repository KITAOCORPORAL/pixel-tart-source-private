using System.Diagnostics;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

/// <summary>Opt-in timings of the real operation; never changes its execution or result.</summary>
public static class AssetLibraryOperationTiming
{
    private static readonly AsyncLocal<Action<Sample>?> Sink = new();

    public sealed record Sample(string Phase, double ElapsedMs, int StartThread, int EndThread);

    public static IDisposable Capture(Action<Sample> sink)
    {
        var previous = Sink.Value;
        Sink.Value = sink;
        return new OnDispose(() => Sink.Value = previous);
    }

    public static IDisposable? Measure(string phase)
    {
        if (Sink.Value is not { } sink) return null;
        var start = Stopwatch.GetTimestamp();
        var thread = Environment.CurrentManagedThreadId;
        return new OnDispose(() => sink(new(phase, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            thread, Environment.CurrentManagedThreadId)));
    }

    private sealed class OnDispose(Action action) : IDisposable
    {
        private Action? _action = action;
        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
