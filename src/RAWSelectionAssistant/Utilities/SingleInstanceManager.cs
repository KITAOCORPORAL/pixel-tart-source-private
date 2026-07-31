using System.Diagnostics;
using System.Threading;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Utilities;

public sealed class SingleInstanceManager(string instanceName) : IDisposable
{
    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent;
    private EventWaitHandle? _activationAcknowledgedEvent;
    private RegisteredWaitHandle? _registeredWait;
    private bool _ownsMutex;

    public event Action? ActivationRequested;

    public bool TryAcquire()
    {
        return TryAcquireCore(allowStaleRecovery: true);
    }

    private bool TryAcquireCore(bool allowStaleRecovery)
    {
        _mutex = new Mutex(true, $"Local\\{instanceName}-Mutex", out var createdNew);
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{instanceName}-Activate");
        _activationAcknowledgedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{instanceName}-Activated");
        if (!createdNew)
        {
            _activationEvent.Set();
            if (_activationAcknowledgedEvent.WaitOne(TimeSpan.FromSeconds(3)))
            {
                return false;
            }

            if (allowStaleRecovery && TryTerminateStaleInstance())
            {
                DisposeHandles();
                return TryAcquireCore(allowStaleRecovery: false);
            }

            return false;
        }

        _ownsMutex = true;
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, _) => ActivationRequested?.Invoke(),
            null,
            Timeout.Infinite,
            false);
        return true;
    }

    public void AcknowledgeActivation() => _activationAcknowledgedEvent?.Set();

    private static bool TryTerminateStaleInstance()
    {
        using var current = Process.GetCurrentProcess();
        var staleProcesses = new List<Process>();
        try
        {
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id == current.Id)
                {
                    process.Dispose();
                    continue;
                }

                try
                {
                    if (process.SessionId != current.SessionId ||
                        process.MainWindowHandle != IntPtr.Zero ||
                        DateTime.Now - process.StartTime < TimeSpan.FromSeconds(15))
                    {
                        process.Dispose();
                        return false;
                    }

                    var executablePath = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(executablePath))
                    {
                        process.Dispose();
                        return false;
                    }

                    var version = FileVersionInfo.GetVersionInfo(executablePath);
                    if (!string.Equals(version.ProductName, Branding.ProductName, StringComparison.Ordinal))
                    {
                        process.Dispose();
                        return false;
                    }

                    staleProcesses.Add(process);
                }
                catch
                {
                    process.Dispose();
                    return false;
                }
            }

            if (staleProcesses.Count == 0)
            {
                return false;
            }

            foreach (var process in staleProcesses)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(5000))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            foreach (var process in staleProcesses)
            {
                process.Dispose();
            }
        }
    }

    public void Dispose()
    {
        DisposeHandles();
    }

    private void DisposeHandles()
    {
        _registeredWait?.Unregister(null);
        _registeredWait = null;
        _activationEvent?.Dispose();
        _activationEvent = null;
        _activationAcknowledgedEvent?.Dispose();
        _activationAcknowledgedEvent = null;
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _ownsMutex = false;
        _mutex?.Dispose();
        _mutex = null;
    }
}
