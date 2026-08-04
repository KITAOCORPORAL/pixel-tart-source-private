using System.Collections.Concurrent;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Tasks;

public sealed class ConservativeTaskScheduler(int maximumParallelReadTasks = 2) : ITaskScheduler
{
    private readonly SemaphoreSlim _readSemaphore = new(Math.Max(1, maximumParallelReadTasks));
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writeLocks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IDisposable> AcquireAsync(TaskDefinition definition, CancellationToken cancellationToken)
    {
        var keys = ExtractWriteKeys(definition.InputSnapshot);
        if (keys.Count == 0)
        {
            await _readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(_readSemaphore);
        }
        var acquired = new List<SemaphoreSlim>();
        try
        {
            foreach (var key in keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var gate = _writeLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired.Add(gate);
            }
            return new CompositeReleaser(acquired);
        }
        catch
        {
            foreach (var gate in acquired.AsEnumerable().Reverse()) gate.Release();
            throw;
        }
    }

    private static IReadOnlyList<string> ExtractWriteKeys(string inputSnapshot)
    {
        if (string.IsNullOrWhiteSpace(inputSnapshot)) return [];
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in inputSnapshot.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            var marker = line[..separator];
            if (!marker.Equals("write-root", StringComparison.OrdinalIgnoreCase) && !marker.Equals("write-source", StringComparison.OrdinalIgnoreCase)) continue;
            var value = line[(separator + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;
            try { result.Add(Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar)); } catch { }
        }
        return result.ToArray();
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable { public void Dispose() => gate.Release(); }
    private sealed class CompositeReleaser(IReadOnlyList<SemaphoreSlim> gates) : IDisposable { public void Dispose() { foreach (var gate in gates.Reverse()) gate.Release(); } }
}
