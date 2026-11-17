using System.Threading;

namespace RAWSelectionAssistant.Utilities;

public enum InstanceAcquisition { Acquired, ActivatedExisting, ExistingInstanceUnresponsive }

/// <summary>Owns only the named root mutex. An activation timeout never grants permission to kill a process.</summary>
public sealed class SingleInstanceManager(string instanceName) : IDisposable
{
    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent, _activationAcknowledgedEvent;
    private RegisteredWaitHandle? _registeredWait;
    private bool _ownsMutex;
    public event Action? ActivationRequested;
    public InstanceAcquisition LastAcquisition { get; private set; }

    public bool TryAcquire()
    {
        if (_ownsMutex) return true;
        DisposeHandles();
        // createdNew describes the kernel object, not whether this thread owns it.
        _mutex = new Mutex(false, $"Local\\{instanceName}-Mutex");
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{instanceName}-Activate");
        _activationAcknowledgedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{instanceName}-Activated");
        if (AcquireMutex()) return true;
        _activationAcknowledgedEvent.Reset();
        _activationEvent.Set();
        if (_activationAcknowledgedEvent.WaitOne(TimeSpan.FromSeconds(3)))
        {
            LastAcquisition = InstanceAcquisition.ActivatedExisting;
            return false;
        }
        // The owner may have exited while activation was pending. WaitOne also acquires
        // an abandoned mutex; no PID, process name, window state or age is ownership evidence.
        if (AcquireMutex()) return true;
        LastAcquisition = InstanceAcquisition.ExistingInstanceUnresponsive;
        return false;
    }

    private bool AcquireMutex()
    {
        try { _ownsMutex = _mutex!.WaitOne(0); }
        catch (AbandonedMutexException) { _ownsMutex = true; }
        if (!_ownsMutex) return false;
        LastAcquisition = InstanceAcquisition.Acquired;
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(_activationEvent!,
            (_, _) => ActivationRequested?.Invoke(), null, Timeout.Infinite, false);
        return true;
    }
    public void AcknowledgeActivation() => _activationAcknowledgedEvent?.Set();
    public void Dispose() => DisposeHandles();
    private void DisposeHandles()
    {
        _registeredWait?.Unregister(null); _registeredWait = null;
        _activationEvent?.Dispose(); _activationEvent = null;
        _activationAcknowledgedEvent?.Dispose(); _activationAcknowledgedEvent = null;
        if (_ownsMutex) { try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { } }
        _ownsMutex = false; _mutex?.Dispose(); _mutex = null;
    }
}
