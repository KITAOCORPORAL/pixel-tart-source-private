using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

public interface IAssetVisualAnalysisCache
{
    Task<AssetVisualAnalysisResult?> TryGetAsync(Guid assetId, string contentHash, int paletteSize, PaletteSortMode paletteSort, string analysisVersion = AssetVisualAnalysisResult.CurrentVersion, CancellationToken cancellationToken = default);
    Task StoreAsync(AssetVisualAnalysisResult result, CancellationToken cancellationToken = default);
}

public sealed class SqliteAssetVisualAnalysisCache(AssetLibraryDatabase database) : IAssetVisualAnalysisCache
{
    private readonly AssetLibraryDatabase _database = database;
    private int _initialized;

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _initialized) != 0) return;
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await AssetLibrarySchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _initialized, 1);
    }

    public async Task<AssetVisualAnalysisResult?> TryGetAsync(Guid assetId, string contentHash, int paletteSize, PaletteSortMode paletteSort, string analysisVersion = AssetVisualAnalysisResult.CurrentVersion, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT ResultJson FROM AssetVisualAnalysis WHERE AssetId=$asset AND AnalysisVersion=$version AND ContentHash=$hash AND PaletteSize=$paletteSize AND PaletteSort=$paletteSort;"; command.Parameters.AddWithValue("$asset", assetId.ToString("D")); command.Parameters.AddWithValue("$version", analysisVersion); command.Parameters.AddWithValue("$hash", contentHash); command.Parameters.AddWithValue("$paletteSize", paletteSize); command.Parameters.AddWithValue("$paletteSort", paletteSort.ToString());
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return value is null ? null : JsonSerializer.Deserialize<AssetVisualAnalysisResult>(value)?.WithCacheHit();
    }

    public async Task StoreAsync(AssetVisualAnalysisResult result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(result with { CacheHit = false });
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = """
            INSERT INTO AssetVisualAnalysis(AssetId,AnalysisVersion,ContentHash,PaletteSize,PaletteSort,AnalysisSource,SourceProfile,AnalysisProfile,ResultJson,CreatedAt)
            VALUES($asset,$version,$hash,$paletteSize,$paletteSort,$source,$sourceProfile,$analysisProfile,$json,$created)
            ON CONFLICT(AssetId,AnalysisVersion,PaletteSize,PaletteSort) DO UPDATE SET ContentHash=excluded.ContentHash,AnalysisSource=excluded.AnalysisSource,SourceProfile=excluded.SourceProfile,AnalysisProfile=excluded.AnalysisProfile,ResultJson=excluded.ResultJson,CreatedAt=excluded.CreatedAt;
            """;
        command.Parameters.AddWithValue("$asset", result.AssetId.ToString("D")); command.Parameters.AddWithValue("$version", result.AnalysisVersion); command.Parameters.AddWithValue("$hash", result.ContentHash); command.Parameters.AddWithValue("$paletteSize", result.PaletteSize); command.Parameters.AddWithValue("$paletteSort", result.PaletteSort.ToString()); command.Parameters.AddWithValue("$source", result.AnalysisSource.ToString()); command.Parameters.AddWithValue("$sourceProfile", result.SourceProfile); command.Parameters.AddWithValue("$analysisProfile", result.AnalysisProfile); command.Parameters.AddWithValue("$json", json); command.Parameters.AddWithValue("$created", result.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class AssetVisualAnalysisService(IAssetVisualAnalysisCache cache)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> _assetLocks = new();

    public async Task<AssetVisualAnalysisResult> AnalyzeAsync(AssetVisualAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var assetLock = _assetLocks.GetOrAdd(request.AssetId, static _ => new SemaphoreSlim(1, 1));
        await assetLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await cache.TryGetAsync(request.AssetId, request.ContentHash, request.PaletteSize, request.PaletteSort, cancellationToken: cancellationToken).ConfigureAwait(false) is { } cached) return cached;
            var result = await Task.Run(() => VisualAnalysisEngine.Analyze(request, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await cache.StoreAsync(result, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally { assetLock.Release(); }
    }
}

public sealed class AssetVisualAnalysisSelectionCoordinator
{
    private readonly object _gate = new();
    private CancellationTokenSource? _active;
    private long _generation;
    private Guid? _selectedAssetId;

    public async Task<bool> AnalyzeSelectionAsync(Guid assetId, Func<CancellationToken, Task<AssetVisualAnalysisResult>> analyze, Action<AssetVisualAnalysisResult> publish, CancellationToken cancellationToken = default)
    {
        var synchronizationContext = SynchronizationContext.Current;
        CancellationTokenSource linked;
        long generation;
        lock (_gate)
        {
            _active?.Cancel(); _active?.Dispose();
            _selectedAssetId = assetId; generation = ++_generation;
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); _active = linked;
        }
        try
        {
            var result = await analyze(linked.Token).ConfigureAwait(false);
            if (!IsCurrent(assetId, generation)) return false;
            if (synchronizationContext is null)
            {
                if (!IsCurrent(assetId, generation)) return false;
                publish(result);
                return true;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            synchronizationContext.Post(_ =>
            {
                try
                {
                    if (!IsCurrent(assetId, generation)) { completion.TrySetResult(false); return; }
                    publish(result);
                    completion.TrySetResult(true);
                }
                catch (Exception exception) { completion.TrySetException(exception); }
            }, null);
            return await completion.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return false; }
        finally
        {
            lock (_gate) { if (ReferenceEquals(_active, linked)) _active = null; }
            linked.Dispose();
        }
    }

    public void ClearSelection()
    {
        lock (_gate) { _selectedAssetId = null; _generation++; _active?.Cancel(); _active?.Dispose(); _active = null; }
    }

    public bool IsCurrent(Guid assetId, long generation)
    {
        lock (_gate) return _selectedAssetId == assetId && _generation == generation && _active is { IsCancellationRequested: false };
    }
}

public interface IAssetVisualAnalysisSourceResolver
{
    Task<AssetVisualAnalysisSource?> ResolveAsync(Guid assetId, CancellationToken cancellationToken = default);
}

public sealed class AssetVisualAnalysisSource : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<Stream>> _openReadAsync;
    private Stream? _stream;

    public AssetVisualAnalysisSource(Guid assetId, VisualAnalysisSourceKind sourceKind, string contentFingerprint, string sourceProfile, Func<CancellationToken, Task<Stream>> openReadAsync)
    {
        AssetId = assetId; SourceKind = sourceKind; ContentFingerprint = contentFingerprint; SourceProfile = sourceProfile; _openReadAsync = openReadAsync;
    }

    public Guid AssetId { get; }
    public VisualAnalysisSourceKind SourceKind { get; }
    public string ContentFingerprint { get; }
    public string SourceProfile { get; }
    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is not null) throw new InvalidOperationException("Analysis stream is already open.");
        _stream = await _openReadAsync(cancellationToken).ConfigureAwait(false);
        if (_stream.CanWrite) throw new InvalidOperationException("Analysis source must be read-only.");
        return _stream;
    }
    public async ValueTask DisposeAsync() { if (_stream is not null) await _stream.DisposeAsync().ConfigureAwait(false); _stream = null; }
}

internal static class AssetVisualAnalysisResultExtensions
{
    public static AssetVisualAnalysisResult WithCacheHit(this AssetVisualAnalysisResult result) => result with { CacheHit = true };
}
