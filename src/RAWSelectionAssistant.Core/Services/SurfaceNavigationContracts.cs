namespace RAWSelectionAssistant.Core.Services;

/// <summary>
/// Shell-owned navigation for independent application surfaces. Closing a surface
/// is intentionally navigation-only and never cancels work owned by that surface.
/// </summary>
public interface ISurfaceNavigationHost
{
    string PreviousSurface { get; }
    string CurrentSurface { get; }
    string OriginSurface { get; }
    IReadOnlyList<string> NavigationHistory { get; }

    string Navigate(string surface);
    string CloseCurrentSurface();
    Task<string> CloseCurrentSurfaceAsync();
    string ReturnToOrigin();
    string ReturnToWorkbench();
}

/// <summary>
/// Shell-owned escape operations. These methods never depend on a hosted
/// module command, CanExecute state, task state or busy state.
/// </summary>
public interface IShellEscapeService
{
    void ForceCloseCurrentSurface();
    void ForceExitTutorial();
    void ForceReturnToWorkbench();
}

public sealed class SurfaceNavigationHost : ISurfaceNavigationHost
{
    public const string WorkbenchSurface = "Workbench";

    private readonly object _gate = new();
    private readonly List<string> _history = [];
    private readonly Func<string, bool> _isSurfaceValid;

    public SurfaceNavigationHost(
        string initialSurface = WorkbenchSurface,
        Func<string, bool>? isSurfaceValid = null)
    {
        _isSurfaceValid = isSurfaceValid ?? (surface => !string.IsNullOrWhiteSpace(surface));
        var normalizedInitial = Normalize(initialSurface);
        _history.Add(IsValid(normalizedInitial) ? normalizedInitial : WorkbenchSurface);
    }

    public string PreviousSurface
    {
        get
        {
            lock (_gate)
                return _history.Count > 1 ? _history[^2] : WorkbenchSurface;
        }
    }

    public string CurrentSurface
    {
        get
        {
            lock (_gate)
                return _history[^1];
        }
    }

    public string OriginSurface
    {
        get
        {
            lock (_gate)
                return ResolveReturnTargetLocked();
        }
    }

    public IReadOnlyList<string> NavigationHistory
    {
        get
        {
            lock (_gate)
                return _history.ToArray();
        }
    }

    public string Navigate(string surface)
    {
        var target = Normalize(surface);
        lock (_gate)
        {
            if (string.Equals(_history[^1], target, StringComparison.Ordinal))
                return _history[^1];

            if (!IsValid(target))
                return ReturnToWorkbenchLocked();

            _history.Add(target);
            return target;
        }
    }

    public string CloseCurrentSurface() => ReturnToOrigin();

    public Task<string> CloseCurrentSurfaceAsync() => Task.FromResult(CloseCurrentSurface());

    public string ReturnToOrigin()
    {
        lock (_gate)
        {
            if (_history.Count > 1)
                _history.RemoveAt(_history.Count - 1);

            while (_history.Count > 1 && !IsValid(_history[^1]))
                _history.RemoveAt(_history.Count - 1);

            if (!IsValid(_history[^1]))
            {
                _history.Clear();
                _history.Add(WorkbenchSurface);
            }

            return _history[^1];
        }
    }

    public string ReturnToWorkbench()
    {
        lock (_gate)
            return ReturnToWorkbenchLocked();
    }

    private string ReturnToWorkbenchLocked()
    {
        _history.Clear();
        _history.Add(WorkbenchSurface);
        return WorkbenchSurface;
    }

    private string ResolveReturnTargetLocked()
    {
        for (var index = _history.Count - 2; index >= 0; index--)
        {
            if (IsValid(_history[index]))
                return _history[index];
        }

        return WorkbenchSurface;
    }

    private bool IsValid(string surface) =>
        string.Equals(surface, WorkbenchSurface, StringComparison.Ordinal) || _isSurfaceValid(surface);

    private static string Normalize(string? surface) => surface?.Trim() ?? string.Empty;
}
