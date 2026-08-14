using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

public interface IAssetVisualAnalysisCache
{
    Task<AssetVisualAnalysisResult?> TryGetAsync(Guid assetId, string contentHash, int paletteSize, PaletteSortMode paletteSort, string analysisVersion = AssetVisualAnalysisResult.CurrentVersion, CancellationToken cancellationToken = default);
    Task StoreAsync(AssetVisualAnalysisResult result, CancellationToken cancellationToken = default);
}

public interface IAssetVisualFeatureStore : IAssetVisualAnalysisCache
{
    Task<AssetVisualFeatures> GetFeaturesAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task RecordFailureAsync(Guid assetId, string? sourceContentHash, string failureReason, CancellationToken cancellationToken = default, string? previousSourceContentHash = null);
}

public sealed class SqliteAssetVisualAnalysisCache(AssetLibraryDatabase database) : IAssetVisualFeatureStore
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
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var cache = connection.CreateCommand())
        {
            cache.Transaction = transaction;
            cache.CommandText = """
                INSERT INTO AssetVisualAnalysis(AssetId,AnalysisVersion,ContentHash,PaletteSize,PaletteSort,AnalysisSource,SourceProfile,AnalysisProfile,ResultJson,CreatedAt)
                VALUES($asset,$version,$hash,$paletteSize,$paletteSort,$source,$sourceProfile,$analysisProfile,$json,$created)
                ON CONFLICT(AssetId,AnalysisVersion,PaletteSize,PaletteSort) DO UPDATE SET ContentHash=excluded.ContentHash,AnalysisSource=excluded.AnalysisSource,SourceProfile=excluded.SourceProfile,AnalysisProfile=excluded.AnalysisProfile,ResultJson=excluded.ResultJson,CreatedAt=excluded.CreatedAt;
                """;
            cache.Parameters.AddWithValue("$asset", result.AssetId.ToString("D")); cache.Parameters.AddWithValue("$version", result.AnalysisVersion); cache.Parameters.AddWithValue("$hash", result.ContentHash); cache.Parameters.AddWithValue("$paletteSize", result.PaletteSize); cache.Parameters.AddWithValue("$paletteSort", result.PaletteSort.ToString()); cache.Parameters.AddWithValue("$source", result.AnalysisSource.ToString()); cache.Parameters.AddWithValue("$sourceProfile", result.SourceProfile); cache.Parameters.AddWithValue("$analysisProfile", result.AnalysisProfile); cache.Parameters.AddWithValue("$json", json); cache.Parameters.AddWithValue("$created", result.CreatedAt.ToString("O"));
            await cache.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!AssetVisualFeatureContract.IsCanonical(result) || string.IsNullOrWhiteSpace(result.SourceContentHash))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await RefreshSourceFingerprintCasAsync(connection, transaction, result.AssetId, result.PreviousSourceContentHash, result.SourceContentHash, cancellationToken).ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO AssetVisualFeatures(
                    AssetId,AnalysisVersion,PaletteSize,PaletteSort,ContentFingerprint,SourceContentHash,Outcome,FailureReason,
                    AnalysisSource,SourceProfile,AnalysisProfile,Harmony,ToneKey,Contrast,LuminanceSpan,Saturation,WarmCool,
                    DominantHue,SecondaryHue,AverageHue,AverageLuma,MedianLuma,ContrastMetric,LumaSpreadMetric,
                    AverageSaturation,MedianSaturation,AverageLightness,WarmCoolMetric,DeepShadowRatio,ShadowRatio,MidtoneRatio,
                    HighlightRatio,SpecularRatio,BlackClipRatio,WhiteClipRatio,HistogramLumaSignature,PaletteSignature,ResultJson,CreatedAt,UpdatedAt)
                VALUES($asset,$version,$paletteSize,$paletteSort,$proxy,$sourceHash,'Succeeded',NULL,
                    $source,$sourceProfile,$analysisProfile,$harmony,$tone,$contrast,$span,$saturation,$warmCool,
                    $hue,$secondaryHue,$averageHue,$luma,$medianLuma,$contrastMetric,$lumaSpread,
                    $averageSaturation,$medianSaturation,$averageLightness,$warmCoolMetric,$deepShadow,$shadow,$midtone,
                    $highlight,$specular,$blackClip,$whiteClip,$histogramSignature,$paletteSignature,$json,$created,$updated)
                ON CONFLICT(AssetId,AnalysisVersion) DO UPDATE SET
                    ContentFingerprint=excluded.ContentFingerprint,SourceContentHash=excluded.SourceContentHash,Outcome='Succeeded',FailureReason=NULL,
                    AnalysisSource=excluded.AnalysisSource,SourceProfile=excluded.SourceProfile,AnalysisProfile=excluded.AnalysisProfile,
                    Harmony=excluded.Harmony,ToneKey=excluded.ToneKey,Contrast=excluded.Contrast,LuminanceSpan=excluded.LuminanceSpan,
                    Saturation=excluded.Saturation,WarmCool=excluded.WarmCool,DominantHue=excluded.DominantHue,SecondaryHue=excluded.SecondaryHue,
                    AverageHue=excluded.AverageHue,AverageLuma=excluded.AverageLuma,MedianLuma=excluded.MedianLuma,
                    ContrastMetric=excluded.ContrastMetric,LumaSpreadMetric=excluded.LumaSpreadMetric,AverageSaturation=excluded.AverageSaturation,
                    MedianSaturation=excluded.MedianSaturation,AverageLightness=excluded.AverageLightness,WarmCoolMetric=excluded.WarmCoolMetric,
                    DeepShadowRatio=excluded.DeepShadowRatio,ShadowRatio=excluded.ShadowRatio,MidtoneRatio=excluded.MidtoneRatio,
                    HighlightRatio=excluded.HighlightRatio,SpecularRatio=excluded.SpecularRatio,BlackClipRatio=excluded.BlackClipRatio,
                    WhiteClipRatio=excluded.WhiteClipRatio,HistogramLumaSignature=excluded.HistogramLumaSignature,
                    PaletteSignature=excluded.PaletteSignature,ResultJson=excluded.ResultJson,CreatedAt=excluded.CreatedAt,UpdatedAt=excluded.UpdatedAt;
                """;
            AddFeatureParameters(command, result, json);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM AssetVisualPaletteColors WHERE AssetId=$asset AND AnalysisVersion=$version;";
            clear.Parameters.AddWithValue("$asset", result.AssetId.ToString("D")); clear.Parameters.AddWithValue("$version", result.AnalysisVersion);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        for (var index = 0; index < result.Palette.Count; index++)
        {
            var color = result.Palette[index];
            await using var insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO AssetVisualPaletteColors(AssetId,AnalysisVersion,ColorIndex,Red,Green,Blue,LabL,LabA,LabB,Hue,Saturation,Chroma,Weight,Hex)
                VALUES($asset,$version,$index,$red,$green,$blue,$l,$a,$b,$hue,$saturation,$chroma,$weight,$hex);
                """;
            insert.Parameters.AddWithValue("$asset", result.AssetId.ToString("D")); insert.Parameters.AddWithValue("$version", result.AnalysisVersion); insert.Parameters.AddWithValue("$index", index);
            insert.Parameters.AddWithValue("$red", color.Rgb.R); insert.Parameters.AddWithValue("$green", color.Rgb.G); insert.Parameters.AddWithValue("$blue", color.Rgb.B); insert.Parameters.AddWithValue("$l", color.Lab.L); insert.Parameters.AddWithValue("$a", color.Lab.A); insert.Parameters.AddWithValue("$b", color.Lab.B); insert.Parameters.AddWithValue("$hue", color.Hue); insert.Parameters.AddWithValue("$saturation", color.Saturation); insert.Parameters.AddWithValue("$chroma", Math.Sqrt(color.Lab.A * color.Lab.A + color.Lab.B * color.Lab.B)); insert.Parameters.AddWithValue("$weight", color.Weight); insert.Parameters.AddWithValue("$hex", color.Hex);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssetVisualFeatures> GetFeaturesAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT f.AnalysisVersion,f.ContentFingerprint,f.SourceContentHash,f.Outcome,f.FailureReason,
                   f.AnalysisSource,f.SourceProfile,f.AnalysisProfile,f.Harmony,f.ToneKey,f.Contrast,f.LuminanceSpan,f.Saturation,
                   f.WarmCool,f.DominantHue,f.SecondaryHue,f.AverageHue,f.AverageLuma,f.MedianLuma,f.ContrastMetric,
                   f.LumaSpreadMetric,f.AverageSaturation,f.MedianSaturation,f.AverageLightness,f.WarmCoolMetric,
                   f.DeepShadowRatio,f.ShadowRatio,f.MidtoneRatio,f.HighlightRatio,f.SpecularRatio,f.BlackClipRatio,f.WhiteClipRatio,
                   f.HistogramLumaSignature,f.PaletteSignature,f.ResultJson,f.CreatedAt,f.UpdatedAt,a.ContentHash
            FROM AssetItems a LEFT JOIN AssetVisualFeatures f ON f.AssetId=a.AssetId
            WHERE a.AssetId=$asset
            ORDER BY CASE WHEN f.AnalysisVersion=$version THEN 0 ELSE 1 END,f.UpdatedAt DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$asset", assetId.ToString("D")); command.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new KeyNotFoundException($"Asset {assetId} was not found.");
        if (reader.IsDBNull(0)) return new(NotAnalyzed(assetId), null);
        var currentSourceHash = reader.IsDBNull(37) ? null : reader.GetString(37);
        var storedSourceHash = reader.IsDBNull(2) ? null : reader.GetString(2);
        var canonical = reader.GetString(0) == AssetVisualFeatureContract.AnalysisVersion;
        var sourceMatches = !string.IsNullOrWhiteSpace(currentSourceHash) && string.Equals(currentSourceHash, storedSourceHash, StringComparison.OrdinalIgnoreCase);
        var effectiveState = !canonical || !sourceMatches
            ? AssetVisualFeatureState.Stale
            : string.Equals(reader.GetString(3), "Succeeded", StringComparison.OrdinalIgnoreCase)
                ? AssetVisualFeatureState.Valid
                : string.Equals(reader.GetString(3), "Failed", StringComparison.OrdinalIgnoreCase)
                    ? AssetVisualFeatureState.Failed
                    : AssetVisualFeatureState.Stale;
        var summary = ReadSummary(reader, assetId, effectiveState);
        var result = effectiveState == AssetVisualFeatureState.Valid && !reader.IsDBNull(34) ? JsonSerializer.Deserialize<AssetVisualAnalysisResult>(reader.GetString(34)) : null;
        return new(summary, result);
    }

    public async Task RecordFailureAsync(Guid assetId, string? sourceContentHash, string failureReason, CancellationToken cancellationToken = default, string? previousSourceContentHash = null)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        await RefreshSourceFingerprintCasAsync(connection, transaction, assetId, previousSourceContentHash ?? sourceContentHash, sourceContentHash, cancellationToken).ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction; command.CommandText = """
                INSERT INTO AssetVisualFeatures(AssetId,AnalysisVersion,PaletteSize,PaletteSort,ContentFingerprint,SourceContentHash,Outcome,FailureReason,AnalysisSource,SourceProfile,AnalysisProfile,CreatedAt,UpdatedAt)
                VALUES($asset,$version,5,'Weight','',$sourceHash,'Failed',$reason,'RasterOriginal','Unknown','sRGB IEC61966-2.1',$created,$updated)
                ON CONFLICT(AssetId,AnalysisVersion) DO UPDATE SET SourceContentHash=excluded.SourceContentHash,Outcome='Failed',FailureReason=excluded.FailureReason,ResultJson=NULL,UpdatedAt=excluded.UpdatedAt;
                """;
            command.Parameters.AddWithValue("$asset", assetId.ToString("D")); command.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion); command.Parameters.AddWithValue("$sourceHash", (object?)sourceContentHash ?? DBNull.Value); command.Parameters.AddWithValue("$reason", failureReason); command.Parameters.AddWithValue("$created", now.ToString("O")); command.Parameters.AddWithValue("$updated", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (var clear = connection.CreateCommand()) { clear.Transaction = transaction; clear.CommandText = "DELETE FROM AssetVisualPaletteColors WHERE AssetId=$asset AND AnalysisVersion=$version;"; clear.Parameters.AddWithValue("$asset", assetId.ToString("D")); clear.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion); await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await using (var clearCache = connection.CreateCommand()) { clearCache.Transaction = transaction; clearCache.CommandText = "DELETE FROM AssetVisualAnalysis WHERE AssetId=$asset AND AnalysisVersion=$version AND PaletteSize=5 AND PaletteSort='Weight';"; clearCache.Parameters.AddWithValue("$asset", assetId.ToString("D")); clearCache.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion); await clearCache.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddFeatureParameters(SqliteCommand command, AssetVisualAnalysisResult result, string json)
    {
        command.Parameters.AddWithValue("$asset", result.AssetId.ToString("D")); command.Parameters.AddWithValue("$version", result.AnalysisVersion); command.Parameters.AddWithValue("$paletteSize", result.PaletteSize); command.Parameters.AddWithValue("$paletteSort", result.PaletteSort.ToString()); command.Parameters.AddWithValue("$proxy", result.ContentHash); command.Parameters.AddWithValue("$sourceHash", (object?)result.SourceContentHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", result.AnalysisSource.ToString()); command.Parameters.AddWithValue("$sourceProfile", result.SourceProfile); command.Parameters.AddWithValue("$analysisProfile", result.AnalysisProfile); command.Parameters.AddWithValue("$harmony", result.Harmony.ToString()); command.Parameters.AddWithValue("$tone", result.ToneKey.ToString()); command.Parameters.AddWithValue("$contrast", result.Contrast.ToString()); command.Parameters.AddWithValue("$span", result.LuminanceSpan.ToString()); command.Parameters.AddWithValue("$saturation", result.Saturation.ToString()); command.Parameters.AddWithValue("$warmCool", result.WarmCool.ToString());
        command.Parameters.AddWithValue("$hue", result.HasDominantChromaticColor ? result.DominantHue : DBNull.Value); command.Parameters.AddWithValue("$secondaryHue", (object?)result.SecondaryHue ?? DBNull.Value); command.Parameters.AddWithValue("$averageHue", (object?)result.AverageHue ?? DBNull.Value); command.Parameters.AddWithValue("$luma", result.AverageLuma); command.Parameters.AddWithValue("$medianLuma", result.MedianLuma); command.Parameters.AddWithValue("$contrastMetric", result.ContrastMetric); command.Parameters.AddWithValue("$lumaSpread", result.LuminanceSpanMetric); command.Parameters.AddWithValue("$averageSaturation", result.AverageSaturation); command.Parameters.AddWithValue("$medianSaturation", result.MedianSaturation); command.Parameters.AddWithValue("$averageLightness", result.AverageLightness); command.Parameters.AddWithValue("$warmCoolMetric", result.WarmCoolMetric); command.Parameters.AddWithValue("$deepShadow", result.ToneZones.DeepShadow); command.Parameters.AddWithValue("$shadow", result.ToneZones.Shadow); command.Parameters.AddWithValue("$midtone", result.ToneZones.Midtone); command.Parameters.AddWithValue("$highlight", result.ToneZones.Highlight); command.Parameters.AddWithValue("$specular", result.ToneZones.Specular); command.Parameters.AddWithValue("$blackClip", result.BlackClipRatio); command.Parameters.AddWithValue("$whiteClip", result.WhiteClipRatio); command.Parameters.AddWithValue("$histogramSignature", result.HistogramLumaSignature); command.Parameters.AddWithValue("$paletteSignature", result.PaletteSignature); command.Parameters.AddWithValue("$json", json); command.Parameters.AddWithValue("$created", result.CreatedAt.ToString("O")); command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
    }

    private static async Task RefreshSourceFingerprintCasAsync(SqliteConnection connection, SqliteTransaction transaction, Guid assetId, string? previousHash, string? currentHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentHash)) throw new InvalidOperationException("Visual features require a current source fingerprint.");
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE AssetItems SET ContentHash=$current WHERE AssetId=$asset AND ((ContentHash IS NULL AND $previous IS NULL) OR ContentHash=$previous);";
        command.Parameters.AddWithValue("$current", currentHash); command.Parameters.AddWithValue("$previous", (object?)previousHash ?? DBNull.Value); command.Parameters.AddWithValue("$asset", assetId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("The asset fingerprint changed concurrently after decode; stale visual features were not published.");
    }

    internal static AssetVisualFeatureSummary NotAnalyzed(Guid assetId) => new() { AssetId = assetId, State = AssetVisualFeatureState.NotAnalyzed, AnalysisVersion = AssetVisualFeatureContract.AnalysisVersion, ContentFingerprint = string.Empty, AnalysisSource = VisualAnalysisSourceKind.RasterOriginal, SourceProfile = "Unknown", AnalysisProfile = "sRGB IEC61966-2.1", CreatedAt = DateTimeOffset.MinValue, UpdatedAt = DateTimeOffset.MinValue };

    internal static AssetVisualFeatureSummary ReadSummary(SqliteDataReader reader, Guid assetId, AssetVisualFeatureState state)
    {
        static T? Parse<T>(SqliteDataReader r, int index) where T : struct, Enum => !r.IsDBNull(index) && Enum.TryParse<T>(r.GetString(index), true, out var value) ? value : null;
        double? Number(int index) => reader.IsDBNull(index) ? null : reader.GetDouble(index);
        return new()
        {
            AssetId = assetId, State = state, AnalysisVersion = reader.GetString(0), ContentFingerprint = reader.GetString(1), SourceContentHash = reader.IsDBNull(2) ? null : reader.GetString(2),
            AnalysisSource = Parse<VisualAnalysisSourceKind>(reader, 5) ?? VisualAnalysisSourceKind.RasterOriginal, SourceProfile = reader.GetString(6), AnalysisProfile = reader.GetString(7),
            Harmony = Parse<ColorHarmonyTendency>(reader, 8), ToneKey = Parse<ToneKeyTendency>(reader, 9), Contrast = Parse<ContrastTendency>(reader, 10), LuminanceSpan = Parse<LuminanceSpanTendency>(reader, 11), Saturation = Parse<SaturationTendency>(reader, 12), WarmCool = Parse<WarmCoolTendency>(reader, 13),
            DominantHue = Number(14), SecondaryHue = Number(15), AverageHue = Number(16), AverageLuma = Number(17), MedianLuma = Number(18), ContrastMetric = Number(19), LumaSpreadMetric = Number(20), AverageSaturation = Number(21), MedianSaturation = Number(22), AverageLightness = Number(23), WarmCoolMetric = Number(24),
            DeepShadowRatio = Number(25), ShadowRatio = Number(26), MidtoneRatio = Number(27), HighlightRatio = Number(28), SpecularRatio = Number(29), BlackClipRatio = Number(30), WhiteClipRatio = Number(31), HistogramLumaSignature = reader.IsDBNull(32) ? null : reader.GetString(32), PaletteSignature = reader.IsDBNull(33) ? null : reader.GetString(33),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(35)), UpdatedAt = DateTimeOffset.Parse(reader.GetString(36)), FailureReason = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }
}

public sealed class AssetVisualAnalysisService(IAssetVisualAnalysisCache cache)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, AssetLock> _assetLocks = new();

    public async Task<AssetVisualAnalysisResult> AnalyzeAsync(AssetVisualAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var assetLock = AcquireAssetLock(request.AssetId);
        try { await assetLock.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch { ReleaseAssetLock(request.AssetId, assetLock, acquiredSemaphore: false); throw; }
        try
        {
            if (await cache.TryGetAsync(request.AssetId, request.ContentHash, request.PaletteSize, request.PaletteSort, cancellationToken: cancellationToken).ConfigureAwait(false) is { } cached)
            {
                if (!string.Equals(cached.SourceContentHash, request.SourceContentHash, StringComparison.OrdinalIgnoreCase) ||
                    cached.AnalysisSource != request.AnalysisSource ||
                    !string.Equals(cached.SourceProfile, request.SourceProfile, StringComparison.Ordinal) ||
                    !string.Equals(cached.AnalysisProfile, request.AnalysisProfile, StringComparison.Ordinal))
                {
                    cached = cached with
                    {
                        SourceContentHash = request.SourceContentHash,
                        PreviousSourceContentHash = request.PreviousSourceContentHash,
                        AnalysisSource = request.AnalysisSource,
                        SourceProfile = request.SourceProfile,
                        AnalysisProfile = request.AnalysisProfile
                    };
                    await cache.StoreAsync(cached with { CacheHit = false }, cancellationToken).ConfigureAwait(false);
                }
                return cached.WithCacheHit();
            }
            var result = await Task.Run(() => VisualAnalysisEngine.Analyze(request, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await cache.StoreAsync(result, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            ReleaseAssetLock(request.AssetId, assetLock, acquiredSemaphore: true);
        }
    }

    private AssetLock AcquireAssetLock(Guid assetId)
    {
        while (true)
        {
            var entry = _assetLocks.GetOrAdd(assetId, static _ => new AssetLock());
            lock (entry.Gate)
            {
                if (entry.Removing) continue;
                entry.References++;
                return entry;
            }
        }
    }

    private void ReleaseAssetLock(Guid assetId, AssetLock entry, bool acquiredSemaphore)
    {
        if (acquiredSemaphore) entry.Semaphore.Release();
        var remove = false;
        lock (entry.Gate)
        {
            entry.References--;
            if (entry.References == 0) { entry.Removing = true; remove = true; }
        }
        if (!remove) return;
        _assetLocks.TryRemove(new(assetId, entry));
        entry.Semaphore.Dispose();
    }

    private sealed class AssetLock
    {
        public readonly object Gate = new();
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int References;
        public bool Removing;
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
