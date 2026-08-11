using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed class SelectionUploadQueueItem
{
    private readonly Func<CancellationToken, ValueTask<Stream>> _openReadAsync;

    internal SelectionUploadQueueItem(SelectionAsset asset, Func<CancellationToken, ValueTask<Stream>> openReadAsync)
    {
        Asset = asset with { Status = SelectionAssetStatus.Queued, UpdatedAtUtc = DateTimeOffset.UtcNow, LastErrorCode = null };
        _openReadAsync = openReadAsync;
    }

    public Guid AssetId => Asset.Id;
    public SelectionAsset Asset { get; internal set; }
    public SelectionAssetStatus State => Asset.Status;
    public double ProgressPercent { get; internal set; }
    public string? ErrorCode => Asset.LastErrorCode;

    internal ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken) => _openReadAsync(cancellationToken);
}

public sealed class SelectionUploadQueue(IOnlineSelectionProvider provider)
{
    private readonly object _sync = new();
    private readonly List<SelectionUploadQueueItem> _items = [];
    private readonly SemaphoreSlim _runnerGate = new(1, 1);
    private bool _pauseRequested;
    private SelectionUploadQueueState _state = SelectionUploadQueueState.Idle;

    public event EventHandler<SelectionUploadQueueItem>? ItemChanged;
    public event EventHandler<SelectionUploadQueueState>? StateChanged;

    public SelectionUploadQueueState State
    {
        get { lock (_sync) return _state; }
    }

    public IReadOnlyList<SelectionUploadQueueItem> Items
    {
        get { lock (_sync) return _items.ToArray(); }
    }

    public SelectionUploadQueueItem Enqueue(
        SelectionAsset asset,
        Func<CancellationToken, ValueTask<Stream>>? openReadAsync = null)
    {
        if (asset.Id == Guid.Empty || asset.ProjectId == Guid.Empty) throw new ArgumentException("上传项目与照片标识不能为空。", nameof(asset));
        if (string.IsNullOrWhiteSpace(asset.ProxyJpegPath)) throw new ArgumentException("上传队列只接受已生成的代理 JPG。", nameof(asset));

        lock (_sync)
        {
            var existing = _items.FirstOrDefault(item => item.AssetId == asset.Id);
            if (existing is not null) return existing;
            var path = Path.GetFullPath(asset.ProxyJpegPath);
            openReadAsync ??= token =>
            {
                token.ThrowIfCancellationRequested();
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                return ValueTask.FromResult(stream);
            };
            var item = new SelectionUploadQueueItem(asset, openReadAsync);
            _items.Add(item);
            return item;
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            _pauseRequested = true;
            SetStateLocked(SelectionUploadQueueState.Paused);
        }
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync) _pauseRequested = false;
        return RunAsync(cancellationToken);
    }

    public async Task RetryFailedAsync(Guid? assetId = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            foreach (var item in _items.Where(item => item.State == SelectionAssetStatus.Failed && (assetId is null || item.AssetId == assetId)))
            {
                item.Asset = item.Asset with { Status = SelectionAssetStatus.Queued, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow };
                item.ProgressPercent = 0;
                RaiseItemChanged(item);
            }
            _pauseRequested = false;
        }
        await RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await _runnerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_sync)
            {
                if (_pauseRequested)
                {
                    SetStateLocked(SelectionUploadQueueState.Paused);
                    return;
                }
                SetStateLocked(SelectionUploadQueueState.Running);
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SelectionUploadQueueItem? next;
                lock (_sync)
                {
                    if (_pauseRequested)
                    {
                        SetStateLocked(SelectionUploadQueueState.Paused);
                        return;
                    }
                    next = _items.FirstOrDefault(item => item.State == SelectionAssetStatus.Queued);
                    if (next is null)
                    {
                        SetStateLocked(SelectionUploadQueueState.Idle);
                        return;
                    }
                    next.Asset = next.Asset with { Status = SelectionAssetStatus.Uploading, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow };
                    RaiseItemChanged(next);
                }

                await UploadOneAsync(next, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_sync)
            {
                if (_state == SelectionUploadQueueState.Running)
                    SetStateLocked(_pauseRequested ? SelectionUploadQueueState.Paused : SelectionUploadQueueState.Idle);
            }
            _runnerGate.Release();
        }
    }

    private async Task UploadOneAsync(SelectionUploadQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await item.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            if (!stream.CanRead) throw new IOException("代理图不可读。");
            var totalBytes = stream.CanSeek ? stream.Length : item.Asset.ProxyBytes ?? 0;
            var progress = new InlineProgress<SelectionUploadProgress>(value =>
            {
                lock (_sync)
                {
                    item.ProgressPercent = value.Percent;
                    RaiseItemChanged(item);
                }
            });
            var result = await provider.UploadAssetAsync(item.Asset.ProjectId, item.Asset, stream, progress, cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                if (result.Success && result.Value is not null)
                {
                    item.Asset = result.Value with { Status = SelectionAssetStatus.Ready, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow };
                    item.ProgressPercent = 100;
                }
                else
                {
                    item.Asset = item.Asset with
                    {
                        Status = SelectionAssetStatus.Failed,
                        LastErrorCode = result.ErrorCode ?? OnlineSelectionErrorCodes.UploadFailed,
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    };
                    if (totalBytes == 0) item.ProgressPercent = 0;
                }
                RaiseItemChanged(item);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_sync)
            {
                item.Asset = item.Asset with { Status = SelectionAssetStatus.Queued, LastErrorCode = null, UpdatedAtUtc = DateTimeOffset.UtcNow };
                RaiseItemChanged(item);
            }
            throw;
        }
        catch
        {
            lock (_sync)
            {
                item.Asset = item.Asset with { Status = SelectionAssetStatus.Failed, LastErrorCode = OnlineSelectionErrorCodes.UploadFailed, UpdatedAtUtc = DateTimeOffset.UtcNow };
                RaiseItemChanged(item);
            }
        }
    }

    private void SetStateLocked(SelectionUploadQueueState value)
    {
        if (_state == value) return;
        _state = value;
        StateChanged?.Invoke(this, value);
    }

    private void RaiseItemChanged(SelectionUploadQueueItem item) => ItemChanged?.Invoke(this, item);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
