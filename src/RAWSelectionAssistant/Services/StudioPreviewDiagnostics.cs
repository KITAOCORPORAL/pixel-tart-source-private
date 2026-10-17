using System.Diagnostics;
using System.Threading;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Services;

/// <summary>Opt-in measurements only: no timers, captures or allocations per pixel.</summary>
public static class StudioPreviewDiagnostics
{
    private static readonly AsyncLocal<Capture?> Current = new();
    public sealed record Observation(string Stage, double ElapsedMs, long PrivateBytes, long WorkingSet, long AccountedBytes);
    public sealed class Capture : IDisposable
    {
        private readonly Capture? previous;
        private readonly List<Observation> observations = [];
        internal Capture() { previous = Current.Value; }
        public Observation[] Observations { get { lock(observations) return observations.ToArray(); } }
        public void Mark(string name) => Record(name, 0);
        internal void Record(string name, double elapsed)
        {
            using var process=Process.GetCurrentProcess(); process.Refresh();
            lock(observations) observations.Add(new(name,elapsed,process.PrivateMemorySize64,process.WorkingSet64,StudioGlobalResources.AccountedBytes));
        }
        public void Dispose() { Current.Value=previous; }
    }
    public static Capture Begin() { var capture=new Capture(); Current.Value=capture; return capture; }
    public static IDisposable Measure(string stage) => Current.Value is {} capture ? new Measurement(capture,stage) : Empty.Instance;
    private sealed class Measurement(Capture capture,string stage) : IDisposable
    {
        private readonly Stopwatch watch=Stopwatch.StartNew();
        public void Dispose() { watch.Stop(); capture.Record(stage,watch.Elapsed.TotalMilliseconds); }
    }
    private sealed class Empty : IDisposable { public static readonly Empty Instance=new(); public void Dispose() {} }
}
