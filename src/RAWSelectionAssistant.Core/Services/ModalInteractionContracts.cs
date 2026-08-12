namespace RAWSelectionAssistant.Core.Services;

public interface IModalSession
{
    bool CanClose { get; }
    bool CanCancel { get; }
    CancellationToken CancellationToken { get; }
    Task RequestCloseAsync();
    Task RequestCancelAsync();
}

public interface IModalHost : IDisposable
{
    IModalSession? CurrentSession { get; }
    bool IsOpen { get; }
    IModalSession Show(IModalSession session);
    IModalSession Open(IModalSession session);
    Task<bool> RequestCloseAsync();
    Task<bool> RequestCancelAsync();
    void Dismiss(IModalSession session);
}

public sealed class ModalSession : IModalSession, IDisposable
{
    private readonly object _gate = new();
    private readonly Func<Task>? _closeAsync;
    private readonly Func<Task>? _cancelAsync;
    private readonly CancellationTokenSource _cancellation;
    private readonly CancellationToken _token;
    private Task? _request;
    private bool _requestIsCancel;
    private bool _isClosed;
    private bool _disposed;

    public ModalSession(
        bool canClose = true,
        bool canCancel = true,
        Func<Task>? closeAsync = null,
        Func<Task>? cancelAsync = null,
        CancellationToken cancellationToken = default)
    {
        CanClose = canClose;
        CanCancel = canCancel;
        _closeAsync = closeAsync;
        _cancelAsync = cancelAsync;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _token = _cancellation.Token;
    }

    public bool CanClose { get; private set; }
    public bool CanCancel { get; private set; }
    public CancellationToken CancellationToken => _token;
    public bool IsClosed
    {
        get
        {
            lock (_gate)
                return _isClosed;
        }
    }

    public Task RequestCloseAsync() => RequestAsync(isCancel: false);

    public Task RequestCancelAsync() => RequestAsync(isCancel: true);

    private Task RequestAsync(bool isCancel)
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_disposed || _isClosed)
                return Task.CompletedTask;

            if (isCancel && !CanCancel || !isCancel && !CanClose)
                return Task.CompletedTask;

            if (_request is not null)
                return _request;

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _request = completion.Task;
            _requestIsCancel = isCancel;
        }

        _ = ExecuteRequestAsync(isCancel, completion);
        return completion.Task;
    }

    private async Task ExecuteRequestAsync(bool isCancel, TaskCompletionSource completion)
    {
        try
        {
            _cancellation.Cancel();
            if (isCancel)
            {
                if (_cancelAsync is not null)
                    await _cancelAsync().ConfigureAwait(false);
            }
            else if (_closeAsync is not null)
            {
                await _closeAsync().ConfigureAwait(false);
            }

            lock (_gate)
            {
                _isClosed = true;
                CanClose = false;
                CanCancel = false;
            }
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                if (_requestIsCancel == isCancel)
                    _request = null;
            }
            completion.TrySetException(exception);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _isClosed = true;
            CanClose = false;
            CanCancel = false;
        }
        _cancellation.Dispose();
    }
}

public sealed class ModalHost : IModalHost
{
    private readonly object _gate = new();
    private IModalSession? _currentSession;
    private bool _disposed;

    public IModalSession? CurrentSession
    {
        get
        {
            lock (_gate)
                return _currentSession;
        }
    }

    public bool IsOpen => CurrentSession is not null;

    public IModalSession Show(IModalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_currentSession is not null)
                throw new InvalidOperationException("A modal session is already open.");
            _currentSession = session;
            return session;
        }
    }

    public IModalSession Open(IModalSession session) => Show(session);

    public async Task<bool> RequestCloseAsync()
    {
        var session = CurrentSession;
        if (session is null || !session.CanClose)
            return false;

        await session.RequestCloseAsync().ConfigureAwait(false);
        ReleaseIfClosed(session);
        return true;
    }

    public async Task<bool> RequestCancelAsync()
    {
        var session = CurrentSession;
        if (session is null || !session.CanCancel)
            return false;

        await session.RequestCancelAsync().ConfigureAwait(false);
        ReleaseIfClosed(session);
        return true;
    }

    public void Dismiss(IModalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_gate)
        {
            if (ReferenceEquals(_currentSession, session))
            {
                _currentSession = null;
                if (session is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }

    private void ReleaseIfClosed(IModalSession session)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_currentSession, session))
            {
                _currentSession = null;
                if (session is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }

    public void Dispose()
    {
        IModalSession? session;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            session = _currentSession;
            _currentSession = null;
        }
        if (session is IDisposable disposable)
            disposable.Dispose();
    }
}
