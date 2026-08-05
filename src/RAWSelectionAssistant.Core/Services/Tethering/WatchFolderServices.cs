using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services.Tethering;

public static class WatchFolderPathPolicy
{
    private static readonly HashSet<string> PreviewExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase) { ".arw", ".cr2", ".cr3", ".dng", ".nef", ".nrw", ".orf", ".pef", ".raf", ".rw2", ".srw" };
    private static readonly HashSet<string> TemporaryExtensions = new(StringComparer.OrdinalIgnoreCase) { ".tmp", ".temp", ".part", ".crdownload", ".download" };

    public static string NormalizeDirectory(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
    public static string NormalizePath(string path) => Path.GetFullPath(path).ToUpperInvariant();

    public static bool IsTopLevelFile(string root, string path)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        return parent is not null && string.Equals(NormalizeDirectory(root), NormalizeDirectory(parent), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCandidate(string root, string path)
    {
        if (!IsTopLevelFile(root, path)) return false;
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.') || name.StartsWith('~')) return false;
        var extension = Path.GetExtension(path);
        if (TemporaryExtensions.Contains(extension) || MediaKind(path) == TetherMediaKind.Unsupported) return false;
        try
        {
            return !File.Exists(path) || (File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory)) == 0;
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    public static TetherMediaKind MediaKind(string path)
    {
        var extension = Path.GetExtension(path);
        if (PreviewExtensions.Contains(extension)) return TetherMediaKind.PreviewImage;
        if (RawExtensions.Contains(extension)) return TetherMediaKind.Raw;
        return TetherMediaKind.Unsupported;
    }

    public static string PairingKey(string path) => Path.GetFileNameWithoutExtension(path).Trim().ToUpperInvariant();
}

public interface IWatchFolderEventSource : IAsyncDisposable
{
    event EventHandler<WatchFolderEvent>? EventReceived;
    string Directory { get; }
    bool IncludeSubdirectories { get; }
    void Start();
    void Stop();
}

public sealed class FileSystemWatcherEventSource : IWatchFolderEventSource
{
    private readonly FileSystemWatcher _watcher;

    public FileSystemWatcherEventSource(string directory)
    {
        Directory = Path.GetFullPath(directory);
        _watcher = new FileSystemWatcher(Directory)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            InternalBufferSize = 32 * 1024,
            EnableRaisingEvents = false
        };
        _watcher.Created += (_, e) => Publish(WatchFolderEventKind.Created, e.FullPath, null);
        _watcher.Changed += (_, e) => Publish(WatchFolderEventKind.Changed, e.FullPath, null);
        _watcher.Renamed += (_, e) => Publish(WatchFolderEventKind.Renamed, e.FullPath, e.OldFullPath);
        _watcher.Error += (_, _) => Publish(WatchFolderEventKind.Error, null, null);
    }

    public event EventHandler<WatchFolderEvent>? EventReceived;
    public string Directory { get; }
    public bool IncludeSubdirectories => false;
    public void Start() => _watcher.EnableRaisingEvents = true;
    public void Stop() => _watcher.EnableRaisingEvents = false;
    public ValueTask DisposeAsync() { _watcher.Dispose(); return ValueTask.CompletedTask; }

    private void Publish(WatchFolderEventKind kind, string? path, string? oldPath)
    {
        try { EventReceived?.Invoke(this, new(kind, path, oldPath, DateTimeOffset.UtcNow)); }
        catch { /* File-system callbacks must never terminate the watcher thread. */ }
    }
}

public interface IFileStabilityProbe
{
    Task<FileStabilityResult> WaitForStableAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class FileStabilityProbe : IFileStabilityProbe
{
    private readonly FileStabilityOptions _options;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public FileStabilityProbe(FileStabilityOptions? options = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _options = options ?? FileStabilityOptions.Default;
        if (_options.RequiredUnchangedSamples < 2 || _options.ProbeInterval <= TimeSpan.Zero || _options.MaximumWait <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    public async Task<FileStabilityResult> WaitForStableAsync(string path, CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        long? previousLength = null;
        DateTimeOffset? previousModified = null;
        DateTimeOffset? unchangedSince = null;
        var unchangedSamples = 0;
        string? lastError = null;

        while (DateTimeOffset.UtcNow - started < _options.MaximumWait)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) throw new FileNotFoundException("Candidate not visible yet.");
                var length = info.Length;
                var modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
                await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var header = new byte[Math.Min(_options.HeaderBytes, (int)Math.Min(length, int.MaxValue))];
                    if (header.Length > 0) await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                }

                if (length > 0 && previousLength == length && previousModified == modified)
                {
                    unchangedSince ??= DateTimeOffset.UtcNow;
                    unchangedSamples++;
                    if (unchangedSamples >= _options.RequiredUnchangedSamples && DateTimeOffset.UtcNow - unchangedSince >= _options.MinimumStableWindow)
                        return new(TetherStabilityState.Stable, length, modified);
                }
                else
                {
                    previousLength = length;
                    previousModified = modified;
                    unchangedSamples = 1;
                    unchangedSince = DateTimeOffset.UtcNow;
                }
                lastError = null;
            }
            catch (FileNotFoundException) { lastError = ErrorCodeCatalog.SourceNotFound; }
            catch (DirectoryNotFoundException) { lastError = ErrorCodeCatalog.SourceNotFound; }
            catch (UnauthorizedAccessException) { lastError = ErrorCodeCatalog.PermissionDenied; }
            catch (IOException) { lastError = ErrorCodeCatalog.FileLocked; }

            await _delay(_options.ProbeInterval, cancellationToken).ConfigureAwait(false);
        }

        return new(TetherStabilityState.TimedOut, previousLength, previousModified, lastError ?? ErrorCodeCatalog.FileLocked);
    }
}

public sealed class TetherPairingService(ITetherAssetRepository repository, TimeSpan? pairingWindow = null)
{
    private readonly TimeSpan _pairingWindow = pairingWindow ?? TimeSpan.FromMinutes(5);

    public async Task PairAsync(TetherAssetRecord asset, CancellationToken cancellationToken = default)
    {
        if (asset.StabilityState != TetherStabilityState.Stable || asset.MediaKind == TetherMediaKind.Unsupported || asset.PairedAssetId is not null) return;
        var key = WatchFolderPathPolicy.PairingKey(asset.SourcePath);
        var assets = await repository.ListBySessionAsync(asset.SessionId, cancellationToken).ConfigureAwait(false);
        var candidates = assets.Where(candidate => candidate.Id != asset.Id && candidate.PairedAssetId is null && candidate.StabilityState == TetherStabilityState.Stable &&
            candidate.MediaKind != asset.MediaKind && candidate.MediaKind != TetherMediaKind.Unsupported &&
            string.Equals(Path.GetDirectoryName(candidate.SourcePath), Path.GetDirectoryName(asset.SourcePath), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(WatchFolderPathPolicy.PairingKey(candidate.SourcePath), key, StringComparison.OrdinalIgnoreCase) &&
            WithinWindow(asset, candidate)).ToArray();

        if (candidates.Length == 0)
        {
            await repository.UpdateAsync(asset with { PairingKey = key, UpdatedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (candidates.Length > 1)
        {
            await repository.UpdateAsync(asset with { PairingKey = key, ProcessingState = TetherProcessingState.NeedsAttention, LastErrorCode = ErrorCodeCatalog.DuplicateConflict, UpdatedAtUtc = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var pair = candidates[0];
        await repository.PairAsync(asset.SessionId, asset.Id, pair.Id, key, cancellationToken).ConfigureAwait(false);
    }

    private bool WithinWindow(TetherAssetRecord left, TetherAssetRecord right)
    {
        var leftTime = left.ModifiedAtUtc ?? left.FirstSeenAtUtc;
        var rightTime = right.ModifiedAtUtc ?? right.FirstSeenAtUtc;
        return (leftTime - rightTime).Duration() <= _pairingWindow;
    }
}
