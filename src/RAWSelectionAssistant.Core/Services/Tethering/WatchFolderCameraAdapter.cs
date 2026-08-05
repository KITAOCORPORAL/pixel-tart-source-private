using System.Collections.Concurrent;
using System.Threading.Channels;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services.Tethering;

public sealed class WatchFolderCameraAdapter(
    ITetherSessionRepository sessionRepository,
    ITetherAssetRepository assetRepository,
    IFileStabilityProbe stabilityProbe,
    TetherPairingService pairingService,
    ITetherProxyCache proxyCache,
    ICameraTransferService transferService,
    IAuditLogService auditLog,
    INotificationCenter notifications,
    Func<string, IWatchFolderEventSource>? eventSourceFactory = null) : ICameraTetherProvider, ICameraConnectionMonitor
{
    private readonly Func<string, IWatchFolderEventSource> _eventSourceFactory = eventSourceFactory ?? (directory => new FileSystemWatcherEventSource(directory));
    private readonly object _sync = new();
    private WatchFolderCameraSession? _activeSession;

    public CameraProviderType ProviderType => CameraProviderType.WatchFolder;
    public string DisplayName => "看守文件夹";
    public CameraProviderType ActiveProvider { get { lock (_sync) return _activeSession is null ? CameraProviderType.None : CameraProviderType.WatchFolder; } }
    public bool IsConnected { get { lock (_sync) return _activeSession is not null && _activeSession.Session.State == TetherSessionState.Running; } }

    public async Task<ICameraSession> StartAsync(WatchFolderStartRequest request, CancellationToken cancellationToken = default)
    {
        var directory = ValidateRequest(request);
        lock (_sync)
        {
            if (_activeSession is not null) throw new InvalidOperationException("A watch-folder session is already active.");
        }

        var active = (await sessionRepository.ListActiveAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => string.Equals(item.NormalizedWatchDirectory, WatchFolderPathPolicy.NormalizeDirectory(directory), StringComparison.OrdinalIgnoreCase));
        if (active is not null)
            return await CreateAndStartAsync(active with { WatchDirectory = directory, State = TetherSessionState.Running, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow }, verifySha256: request.VerifySha256, recovering: true, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var session = new TetherSessionRecord(Guid.NewGuid(), request.ProjectId, CameraProviderType.WatchFolder, directory,
            WatchFolderPathPolicy.NormalizeDirectory(directory), TetherSessionState.Running, now, now, request.ImportExisting,
            request.CopyToProject, NormalizeOptional(request.ProjectDestination), request.CopyToBackup, NormalizeOptional(request.BackupDestination), now);
        await sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        return await CreateAndStartAsync(session, request.VerifySha256, recovering: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ICameraSession?> RecoverLatestAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync) { if (_activeSession is not null) return _activeSession; }
        var session = (await sessionRepository.ListActiveAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        if (session is null) return null;
        if (!Directory.Exists(session.WatchDirectory))
        {
            session = session with { State = TetherSessionState.NeedsAttention, LastErrorCode = ErrorCodeCatalog.SourceNotFound, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            return null;
        }
        return await CreateAndStartAsync(session with { State = TetherSessionState.Running, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow }, verifySha256: false, recovering: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ICameraSession> CreateAndStartAsync(TetherSessionRecord session, bool verifySha256, bool recovering, CancellationToken cancellationToken)
    {
        if (recovering) await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        var runtime = new WatchFolderCameraSession(session, verifySha256, sessionRepository, assetRepository, stabilityProbe, pairingService,
            proxyCache, transferService, auditLog, notifications, _eventSourceFactory(session.WatchDirectory), ClearActive);
        lock (_sync) _activeSession = runtime;
        try
        {
            await runtime.StartAsync(recovering, cancellationToken).ConfigureAwait(false);
            return runtime;
        }
        catch
        {
            ClearActive(runtime);
            await runtime.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void ClearActive(WatchFolderCameraSession session)
    {
        lock (_sync) { if (ReferenceEquals(_activeSession, session)) _activeSession = null; }
    }

    private static string ValidateRequest(WatchFolderStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Directory) || !Path.IsPathFullyQualified(request.Directory)) throw new ArgumentException("Watch folder must be an absolute path.");
        var directory = Path.GetFullPath(request.Directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Watch folder is unavailable.");
        if (request.CopyToProject && string.IsNullOrWhiteSpace(request.ProjectDestination)) throw new ArgumentException("Project destination is required when project copy is enabled.");
        if (request.CopyToBackup && string.IsNullOrWhiteSpace(request.BackupDestination)) throw new ArgumentException("Backup destination is required when backup copy is enabled.");
        return directory;
    }

    private static string? NormalizeOptional(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
}

internal sealed class WatchFolderCameraSession : ICameraSession
{
    private const int QueueCapacity = 256;
    private readonly bool _verifySha256;
    private readonly ITetherSessionRepository _sessionRepository;
    private readonly ITetherAssetRepository _assetRepository;
    private readonly IFileStabilityProbe _stabilityProbe;
    private readonly TetherPairingService _pairingService;
    private readonly ITetherProxyCache _proxyCache;
    private readonly ICameraTransferService _transferService;
    private readonly IAuditLogService _auditLog;
    private readonly INotificationCenter _notifications;
    private readonly IWatchFolderEventSource _eventSource;
    private readonly Action<WatchFolderCameraSession> _stopped;
    private readonly Channel<WatchFolderEvent> _queue = Channel.CreateBounded<WatchFolderEvent>(new BoundedChannelOptions(QueueCapacity)
    {
        SingleReader = false,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _pathGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (long Ticks, WatchFolderEventKind Kind)> _lastQueued = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _startupBaseline = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _reconcileGate = new(1, 1);
    private readonly SemaphoreSlim _snapshotGate = new(1, 1);
    private readonly object _snapshotScheduleSync = new();
    private Task[] _workers = [];
    private int _queueDepth;
    private int _reconciliationPending;
    private int _stopping;
    private DateTimeOffset _lastSnapshotAt = DateTimeOffset.MinValue;
    private Task _scheduledSnapshot = Task.CompletedTask;

    public WatchFolderCameraSession(TetherSessionRecord session, bool verifySha256, ITetherSessionRepository sessionRepository,
        ITetherAssetRepository assetRepository, IFileStabilityProbe stabilityProbe, TetherPairingService pairingService,
        ITetherProxyCache proxyCache, ICameraTransferService transferService, IAuditLogService auditLog, INotificationCenter notifications,
        IWatchFolderEventSource eventSource, Action<WatchFolderCameraSession> stopped)
    {
        Session = session;
        _verifySha256 = verifySha256;
        _sessionRepository = sessionRepository;
        _assetRepository = assetRepository;
        _stabilityProbe = stabilityProbe;
        _pairingService = pairingService;
        _proxyCache = proxyCache;
        _transferService = transferService;
        _auditLog = auditLog;
        _notifications = notifications;
        _eventSource = eventSource;
        _stopped = stopped;
    }

    public TetherSessionRecord Session { get; private set; }
    public event EventHandler<TetherSessionSnapshot>? SnapshotChanged;

    public Task StartAsync(bool recovering, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_eventSource.IncludeSubdirectories && !Session.ImportExisting && !recovering)
        {
            foreach (var path in Directory.EnumerateFiles(Session.WatchDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (new DateTimeOffset(File.GetCreationTimeUtc(path), TimeSpan.Zero) < Session.DiscoveryCutoffUtc)
                        _startupBaseline.Add(WatchFolderPathPolicy.NormalizePath(path));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        _eventSource.EventReceived += OnEventReceived;
        _workers = Enumerable.Range(0, 2).Select(_ => WorkerAsync(_lifetime.Token)).ToArray();
        _eventSource.Start();
        if (Session.ImportExisting || recovering) RequestReconciliation();
        return WriteAuditAsync(recovering ? "SessionRecovered" : "SessionStarted", "Succeeded", null, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0) return;
        _eventSource.Stop();
        _eventSource.EventReceived -= OnEventReceived;
        _queue.Writer.TryComplete();
        _lifetime.Cancel();
        try { await Task.WhenAll(_workers).WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
        try { await _scheduledSnapshot.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        var now = DateTimeOffset.UtcNow;
        Session = Session with { State = TetherSessionState.Stopped, StoppedAtUtc = now, UpdatedAtUtc = now, LastErrorCode = null };
        await _sessionRepository.UpdateAsync(Session, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync("SessionStopped", "Succeeded", null, cancellationToken).ConfigureAwait(false);
        await PublishSnapshotAsync(force: true, cancellationToken).ConfigureAwait(false);
        _stopped(this);
    }

    public Task ReconcileAsync(CancellationToken cancellationToken = default) => ReconcileCoreAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); }
        catch { _stopped(this); }
        await _eventSource.DisposeAsync().ConfigureAwait(false);
        _lifetime.Dispose();
        _reconcileGate.Dispose();
        _snapshotGate.Dispose();
        foreach (var gate in _pathGates.Values) gate.Dispose();
    }

    private void OnEventReceived(object? sender, WatchFolderEvent value)
    {
        if (value.Kind == WatchFolderEventKind.Error)
        {
            RequestReconciliation();
            return;
        }
        if (value.Path is null || !WatchFolderPathPolicy.IsCandidate(Session.WatchDirectory, value.Path)) return;
        var normalized = WatchFolderPathPolicy.NormalizePath(value.Path);
        var now = Environment.TickCount64;
        if (_lastQueued.TryGetValue(normalized, out var previous) && previous.Kind == value.Kind && now - previous.Ticks < 200) return;
        _lastQueued[normalized] = (now, value.Kind);
        if (_queue.Writer.TryWrite(value)) Interlocked.Increment(ref _queueDepth);
        else RequestReconciliation();
    }

    private void RequestReconciliation()
    {
        Interlocked.Exchange(ref _reconciliationPending, 1);
        if (_queue.Writer.TryWrite(new(WatchFolderEventKind.Reconcile, null, null, DateTimeOffset.UtcNow))) Interlocked.Increment(ref _queueDepth);
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var value in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _queueDepth);
                try
                {
                    if (value.Kind == WatchFolderEventKind.Reconcile) await ReconcileCoreAsync(cancellationToken).ConfigureAwait(false);
                    else if (value.Path is not null) await ProcessPathAsync(value.Path, value.Kind, cancellationToken).ConfigureAwait(false);
                    if (Interlocked.Exchange(ref _reconciliationPending, 0) != 0) await ReconcileCoreAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    await WriteAuditAsync("CandidateProcessing", "NeedsAttention", ErrorCodeCatalog.DestinationNotWritable, CancellationToken.None).ConfigureAwait(false);
                }
                await PublishSnapshotAsync(force: false, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task ReconcileCoreAsync(CancellationToken cancellationToken)
    {
        await _reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var path in Directory.EnumerateFiles(Session.WatchDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (WatchFolderPathPolicy.IsCandidate(Session.WatchDirectory, path))
                    await ProcessPathAsync(path, WatchFolderEventKind.Reconcile, cancellationToken).ConfigureAwait(false);
            }
            var reconciledAt = DateTimeOffset.UtcNow;
            Session = Session with
            {
                State = TetherSessionState.Running,
                LastErrorCode = null,
                LastReconciledAtUtc = reconciledAt,
                UpdatedAtUtc = reconciledAt
            };
            await _sessionRepository.UpdateAsync(Session, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync("TopLevelReconciliation", "Succeeded", null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            Session = Session with { State = TetherSessionState.NeedsAttention, LastErrorCode = ErrorCodeCatalog.SourceNotFound, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await _sessionRepository.UpdateAsync(Session, cancellationToken).ConfigureAwait(false);
            await NotifyAttentionAsync("看守文件夹暂时不可访问", cancellationToken).ConfigureAwait(false);
        }
        finally { _reconcileGate.Release(); }
    }

    private async Task ProcessPathAsync(string path, WatchFolderEventKind eventKind, CancellationToken cancellationToken)
    {
        var normalized = WatchFolderPathPolicy.NormalizePath(path);
        if (_startupBaseline.Contains(normalized)) return;
        var gate = _pathGates.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await _assetRepository.GetByPathAsync(Session.Id, normalized, cancellationToken).ConfigureAwait(false);
            if (existing is null && !Session.ImportExisting && eventKind == WatchFolderEventKind.Reconcile && IsOlderThanCutoff(path)) return;

            var now = DateTimeOffset.UtcNow;
            var asset = existing ?? new TetherAssetRecord(Guid.NewGuid(), Session.Id, Session.ProjectId, Path.GetFullPath(path), normalized, Path.GetFileName(path),
                Path.GetExtension(path).ToLowerInvariant(), WatchFolderPathPolicy.MediaKind(path), null, null, now, TetherStabilityState.Pending,
                TetherProcessingState.Pending, TetherPreviewState.None, now);
            asset = await _assetRepository.UpsertDiscoveredAsync(asset, cancellationToken).ConfigureAwait(false);

            if (asset.StabilityState == TetherStabilityState.Stable && IsUnchanged(asset))
            {
                await CompleteStableAssetAsync(asset, cancellationToken).ConfigureAwait(false);
                return;
            }

            asset = asset with { StabilityState = TetherStabilityState.Probing, ProcessingState = TetherProcessingState.Pending, LastErrorCode = null, UpdatedAtUtc = now };
            await _assetRepository.UpdateAsync(asset, cancellationToken).ConfigureAwait(false);
            var stability = await _stabilityProbe.WaitForStableAsync(path, cancellationToken).ConfigureAwait(false);
            if (stability.State != TetherStabilityState.Stable)
            {
                asset = asset with { FileSize = stability.Length, ModifiedAtUtc = stability.ModifiedAtUtc, StabilityState = stability.State,
                    ProcessingState = TetherProcessingState.NeedsAttention, LastErrorCode = stability.ErrorCode, UpdatedAtUtc = DateTimeOffset.UtcNow };
                await _assetRepository.UpdateAsync(asset, cancellationToken).ConfigureAwait(false);
                await NotifyAttentionAsync("文件仍在写入或暂时不可访问", cancellationToken).ConfigureAwait(false);
                return;
            }

            asset = asset with { FileSize = stability.Length, ModifiedAtUtc = stability.ModifiedAtUtc, ReadyAtUtc = DateTimeOffset.UtcNow,
                StabilityState = TetherStabilityState.Stable, ProcessingState = TetherProcessingState.Ready,
                PreviewState = asset.MediaKind == TetherMediaKind.Raw ? TetherPreviewState.Placeholder : TetherPreviewState.Pending,
                LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await _assetRepository.UpdateAsync(asset, cancellationToken).ConfigureAwait(false);
            await CompleteStableAssetAsync(asset, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private async Task CompleteStableAssetAsync(TetherAssetRecord asset, CancellationToken cancellationToken)
    {
        if (asset.MediaKind == TetherMediaKind.PreviewImage && asset.PreviewState != TetherPreviewState.Ready)
        {
            try
            {
                var cacheKey = await _proxyCache.GetOrCreateAsync(asset, cancellationToken).ConfigureAwait(false);
                asset = asset with { ProxyCacheKey = cacheKey, PreviewState = cacheKey is null ? TetherPreviewState.Failed : TetherPreviewState.Ready, UpdatedAtUtc = DateTimeOffset.UtcNow };
                await _assetRepository.UpdateAsync(asset, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                asset = asset with { PreviewState = TetherPreviewState.Failed, LastErrorCode = ErrorCodeCatalog.UnsupportedFormat, UpdatedAtUtc = DateTimeOffset.UtcNow };
                await _assetRepository.UpdateAsync(asset, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (asset.MediaKind == TetherMediaKind.Raw && asset.PreviewState != TetherPreviewState.Placeholder)
        {
            asset = asset with { PreviewState = TetherPreviewState.Placeholder, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await _assetRepository.UpdateAsync(asset, cancellationToken).ConfigureAwait(false);
        }

        await _pairingService.PairAsync(asset, cancellationToken).ConfigureAwait(false);
        var projectResult = default(TetherCopyResult);
        var backupResult = default(TetherCopyResult);
        if (Session.CopyToProject && asset.ProjectCopyPath is null && Session.ProjectDestination is not null)
            projectResult = await _transferService.CopyToProjectAsync(asset, Session.ProjectDestination, _verifySha256, cancellationToken).ConfigureAwait(false);
        if (Session.CopyToBackup && asset.BackupCopyPath is null && Session.BackupDestination is not null)
            backupResult = await _transferService.CopyToBackupAsync(asset, Session.BackupDestination, _verifySha256, cancellationToken).ConfigureAwait(false);

        if (projectResult is not null || backupResult is not null)
        {
            var latest = await _assetRepository.GetAsync(asset.Id, cancellationToken).ConfigureAwait(false) ?? asset;
            var results = new[] { projectResult, backupResult }.Where(result => result is not null).ToArray();
            var failed = results.Any(result => result!.State is TetherProcessingState.NeedsAttention or TetherProcessingState.PartiallyCompleted);
            var succeeded = results.Any(result => result!.State == TetherProcessingState.Copied);
            var state = failed && succeeded ? TetherProcessingState.PartiallyCompleted : failed ? TetherProcessingState.NeedsAttention : TetherProcessingState.Copied;
            latest = latest with { ProcessingState = state, LastErrorCode = failed ? results.First(result => result!.ErrorCode is not null)!.ErrorCode : null, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await _assetRepository.UpdateAsync(latest, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool IsOlderThanCutoff(string path)
    {
        try { return new DateTimeOffset(File.GetCreationTimeUtc(path), TimeSpan.Zero) < Session.DiscoveryCutoffUtc; }
        catch { return false; }
    }

    private static bool IsUnchanged(TetherAssetRecord asset)
    {
        try
        {
            var info = new FileInfo(asset.SourcePath);
            if (!info.Exists || asset.FileSize != info.Length || asset.ModifiedAtUtc is null) return false;
            return Math.Abs((info.LastWriteTimeUtc - asset.ModifiedAtUtc.Value.UtcDateTime).TotalSeconds) <= 1;
        }
        catch { return false; }
    }

    private async Task PublishSnapshotAsync(bool force, CancellationToken cancellationToken)
    {
        if (!force)
        {
            var remaining = TimeSpan.FromMilliseconds(100) - (DateTimeOffset.UtcNow - _lastSnapshotAt);
            if (remaining > TimeSpan.Zero)
            {
                lock (_snapshotScheduleSync)
                {
                    if (_scheduledSnapshot.IsCompleted) _scheduledSnapshot = PublishAfterDelayAsync(remaining);
                }
                return;
            }
        }
        await PublishSnapshotCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishAfterDelayAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _lifetime.Token).ConfigureAwait(false);
            await PublishSnapshotCoreAsync(_lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    private async Task PublishSnapshotCoreAsync(CancellationToken cancellationToken)
    {
        await _snapshotGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _lastSnapshotAt = DateTimeOffset.UtcNow;
            var assets = await _assetRepository.ListBySessionAsync(Session.Id, cancellationToken).ConfigureAwait(false);
            SnapshotChanged?.Invoke(this, new(Session, assets, Math.Max(0, Volatile.Read(ref _queueDepth)), Volatile.Read(ref _reconciliationPending) != 0));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
        finally { _snapshotGate.Release(); }
    }

    private Task WriteAuditAsync(string operation, string result, string? errorCode, CancellationToken cancellationToken) =>
        SafeAuditAsync(operation, result, errorCode, cancellationToken);

    private async Task SafeAuditAsync(string operation, string result, string? errorCode, CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog.WriteAsync("Tether", operation, result == "Succeeded" ? "Information" : "Warning", $"Operation={operation};Result={result}",
                projectId: Session.ProjectId, errorCode: errorCode, correlationId: Session.Id.ToString("N"), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
    }

    private async Task NotifyAttentionAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(new(Guid.NewGuid(), NotificationType.Toast, NotificationSeverity.Warning, "联机拍摄需要处理", message,
                null, Session.ProjectId, [], false, DateTimeOffset.UtcNow, DeduplicationKey: $"tether-attention-{Session.Id:N}"), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
    }
}
