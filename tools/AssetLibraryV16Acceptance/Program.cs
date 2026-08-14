using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

return await AcceptanceRunner.RunAsync(args);

internal static class AcceptanceRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The V1.6 acceptance runner is Windows-only.");
            var options = RunnerOptions.Parse(args);
            EnsureSafePath(options.FixtureRoot);
            EnsureSafePath(options.DatabasePath);
            EnsureSafePath(options.ResultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(options.ResultPath)!);

            object result = options.Mode switch
            {
                "pipeline" => await RunPipelineAsync(options),
                "cancellation" => await RunCancellationAsync(options),
                _ => throw new ArgumentException($"Unknown mode: {options.Mode}")
            };
            await File.WriteAllTextAsync(options.ResultPath, JsonSerializer.Serialize(result, JsonOptions), new System.Text.UTF8Encoding(false));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task<PipelineAcceptanceResult> RunPipelineAsync(RunnerOptions options)
    {
        var paths = Directory.EnumerateFiles(Path.Combine(options.FixtureRoot, "performance"), "*.jpg", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Take(options.Count)
            .ToArray();
        if (paths.Length != options.Count) throw new InvalidDataException($"Expected {options.Count} performance fixtures, found {paths.Length}.");

        var database = new AssetLibraryDatabase(options.DatabasePath);
        await using var repository = new SqliteAssetLibraryRepository(database);
        var import = await repository.ImportAsync(paths.Select(path => new AssetImportRequest(path, ComputeContentHash: true)));
        if (import.ImportedCount != options.Count || import.Cancelled || import.MissingCount != 0)
            throw new InvalidDataException("The generated corpus was not imported completely.");
        var assets = await LoadAllAssetsAsync(repository, options.Count);
        var cache = new SqliteAssetVisualAnalysisCache(database);
        var service = new AssetVisualAnalysisService(cache);

        var cold = await RunPassAsync(assets, service, cache);
        var warm = await RunPassAsync(assets, service, cache);
        var rows = await CountRowsAsync(database);

        SqliteConnection.ClearAllPools();
        var reopenedDatabase = new AssetLibraryDatabase(options.DatabasePath);
        var reopenedStore = new SqliteAssetVisualAnalysisCache(reopenedDatabase);
        var reloadWatch = Stopwatch.StartNew();
        var reloadValid = 0;
        foreach (var asset in assets)
        {
            var features = await reopenedStore.GetFeaturesAsync(asset.AssetId);
            if (features.Summary.State == AssetVisualFeatureState.Valid && features.Analysis is not null) reloadValid++;
        }
        reloadWatch.Stop();

        var coordinator = new AssetVisualAnalysisSelectionCoordinator();
        Guid? publishedAssetId = null;
        var finalAsset = assets[^1];
        var finalPublished = await coordinator.AnalyzeSelectionAsync(
            finalAsset.AssetId,
            async token => await service.AnalyzeAsync(await DecodeAsync(finalAsset, token), token),
            result => publishedAssetId = result.AssetId);

        return new(
            Schema: "pixel-tart-asset-library-v16-pipeline/v1",
            RequestedCount: options.Count,
            ImportedCount: import.ImportedCount,
            Cold: cold,
            Warm: warm,
            Database: rows,
            ReopenedInspectorValidCount: reloadValid,
            ReopenedInspectorMilliseconds: reloadWatch.Elapsed.TotalMilliseconds,
            FinalInspectorPublished: finalPublished,
            FinalInspectorAssetId: publishedAssetId,
            ExpectedFinalAssetId: finalAsset.AssetId,
            ColorManagementReferenceVerified: false,
            RawVisualProxyVerified: false);
    }

    private static async Task<PipelinePassResult> RunPassAsync(
        IReadOnlyList<AssetItem> assets,
        AssetVisualAnalysisService service,
        IAssetVisualFeatureStore featureStore)
    {
        var totalWatch = Stopwatch.StartNew();
        var decodeMilliseconds = 0d;
        var analysisMilliseconds = 0d;
        var inspectorMilliseconds = 0d;
        var cacheHits = 0;
        var cacheMisses = 0;
        var inspectorValid = 0;

        foreach (var asset in assets)
        {
            var watch = Stopwatch.StartNew();
            var request = await DecodeAsync(asset, CancellationToken.None);
            watch.Stop();
            decodeMilliseconds += watch.Elapsed.TotalMilliseconds;

            watch.Restart();
            var analysis = await service.AnalyzeAsync(request);
            watch.Stop();
            analysisMilliseconds += watch.Elapsed.TotalMilliseconds;
            if (analysis.CacheHit) cacheHits++; else cacheMisses++;

            watch.Restart();
            var features = await featureStore.GetFeaturesAsync(asset.AssetId);
            watch.Stop();
            inspectorMilliseconds += watch.Elapsed.TotalMilliseconds;
            if (features.Summary.State == AssetVisualFeatureState.Valid && features.Analysis is not null) inspectorValid++;
        }
        totalWatch.Stop();
        return new(
            TotalMilliseconds: totalWatch.Elapsed.TotalMilliseconds,
            DecodeMilliseconds: decodeMilliseconds,
            AnalysisCacheSqliteMilliseconds: analysisMilliseconds,
            InspectorMilliseconds: inspectorMilliseconds,
            CacheHits: cacheHits,
            CacheMisses: cacheMisses,
            InspectorValidCount: inspectorValid);
    }

    private static async Task<CancellationAcceptanceResult> RunCancellationAsync(RunnerOptions options)
    {
        var paths = Directory.EnumerateFiles(Path.Combine(options.FixtureRoot, "performance"), "*.jpg", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        if (paths.Length != 3) throw new InvalidDataException("Cancellation acceptance requires three generated JPEG fixtures.");
        var database = new AssetLibraryDatabase(options.DatabasePath);
        await using var repository = new SqliteAssetLibraryRepository(database);
        var import = await repository.ImportAsync(paths.Select(path => new AssetImportRequest(path, ComputeContentHash: true)));
        if (import.ImportedCount != 3) throw new InvalidDataException("Cancellation fixtures were not imported completely.");
        var assets = await LoadAllAssetsAsync(repository, 3);
        var cache = new SqliteAssetVisualAnalysisCache(database);
        var service = new AssetVisualAnalysisService(cache);
        var coordinator = new AssetVisualAnalysisSelectionCoordinator();
        var published = new List<Guid>();
        var enteredA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decoderCallsA = 0;
        var decoderCallsB = 0;
        var decoderStartedUncancelledA = false;
        var decoderStartedUncancelledB = false;
        var decoderCancelledA = false;
        var decoderCancelledB = false;

        async Task<AssetVisualAnalysisResult> DecodeUntilCancelledAsync(
            AssetItem asset,
            TaskCompletionSource entered,
            Action called,
            Action startedUncancelled,
            Action cancelled,
            CancellationToken token)
        {
            entered.TrySetResult();
            try
            {
                while (true)
                {
                    if (!token.IsCancellationRequested) startedUncancelled();
                    called();
                    _ = await DecodeAsync(asset, token);
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancelled();
                throw;
            }
        }

        var first = coordinator.AnalyzeSelectionAsync(
            assets[0].AssetId,
            token => DecodeUntilCancelledAsync(assets[0], enteredA, () => decoderCallsA++, () => decoderStartedUncancelledA = true, () => decoderCancelledA = true, token),
            result => published.Add(result.AssetId));
        await enteredA.Task;
        var second = coordinator.AnalyzeSelectionAsync(
            assets[1].AssetId,
            token => DecodeUntilCancelledAsync(assets[1], enteredB, () => decoderCallsB++, () => decoderStartedUncancelledB = true, () => decoderCancelledB = true, token),
            result => published.Add(result.AssetId));
        await enteredB.Task;
        var third = coordinator.AnalyzeSelectionAsync(
            assets[2].AssetId,
            async token => await service.AnalyzeAsync(await DecodeAsync(assets[2], token), token),
            result => published.Add(result.AssetId));
        var completion = await Task.WhenAll(first, second, third);

        return new(
            Schema: "pixel-tart-asset-library-v16-cancellation/v1",
            ADecoderCalls: decoderCallsA,
            ADecoderStartedUncancelled: decoderStartedUncancelledA,
            ADecoderCancelled: decoderCancelledA,
            BDecoderCalls: decoderCallsB,
            BDecoderStartedUncancelled: decoderStartedUncancelledB,
            BDecoderCancelled: decoderCancelledB,
            APublished: completion[0],
            BPublished: completion[1],
            CPublished: completion[2],
            PublishedAssetIds: published,
            ExpectedPublishedAssetId: assets[2].AssetId,
            CInspectorState: (await cache.GetFeaturesAsync(assets[2].AssetId)).Summary.State.ToString());
    }

    private static async Task<IReadOnlyList<AssetItem>> LoadAllAssetsAsync(SqliteAssetLibraryRepository repository, int expectedCount)
    {
        var result = new List<AssetItem>(expectedCount);
        string? cursor = null;
        do
        {
            var page = await repository.QueryAsync(new(PageSize: 500, Cursor: cursor));
            result.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        if (result.Count != expectedCount) throw new InvalidDataException($"Expected {expectedCount} imported assets, found {result.Count}.");
        return result.OrderBy(asset => asset.SourcePath, StringComparer.Ordinal).ToArray();
    }

    private static async Task<DatabaseRowCounts> CountRowsAsync(AssetLibraryDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        async Task<int> CountAsync(string table)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }
        return new(
            AssetItems: await CountAsync("AssetItems"),
            AnalysisCacheRows: await CountAsync("AssetVisualAnalysis"),
            VisualFeatureRows: await CountAsync("AssetVisualFeatures"),
            PaletteRows: await CountAsync("AssetVisualPaletteColors"));
    }

    private static async Task<AssetVisualAnalysisRequest> DecodeAsync(AssetItem asset, CancellationToken cancellationToken)
    {
        var previewAssembly = Assembly.Load("PixelTart_AssetLibrary_V1_6_Preview");
        var decoder = previewAssembly.GetType("PixelTart.AssetLibrary.Preview.WpfVisualAnalysisDecoder", throwOnError: true)!;
        var method = decoder.GetMethod("DecodeAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(decoder.FullName, "DecodeAsync");
        var task = (Task?)method.Invoke(null, [asset, AssetVisualFeatureContract.PaletteSize, cancellationToken, AssetVisualFeatureContract.PaletteSort])
            ?? throw new InvalidOperationException("WPF decoder returned no task.");
        await task.ConfigureAwait(false);
        return (AssetVisualAnalysisRequest)(task.GetType().GetProperty("Result")?.GetValue(task)
            ?? throw new InvalidOperationException("WPF decoder returned no request."));
    }

    private static void EnsureSafePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Every V1.6 acceptance path must be below the system temporary directory.");
    }
}

internal sealed record RunnerOptions(string Mode, string FixtureRoot, string DatabasePath, string ResultPath, int Count)
{
    public static RunnerOptions Parse(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Mode is required.");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Arguments must use --name value pairs.");
            values[args[index][2..]] = args[index + 1];
        }
        string Required(string name) => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? Path.GetFullPath(value)
            : throw new ArgumentException($"--{name} is required.");
        var count = values.TryGetValue("count", out var rawCount) ? int.Parse(rawCount, System.Globalization.CultureInfo.InvariantCulture) : 3;
        if (count is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(count));
        return new(args[0].ToLowerInvariant(), Required("fixture-root"), Required("database"), Required("result"), count);
    }
}

internal sealed record PipelineAcceptanceResult(
    string Schema,
    int RequestedCount,
    int ImportedCount,
    PipelinePassResult Cold,
    PipelinePassResult Warm,
    DatabaseRowCounts Database,
    int ReopenedInspectorValidCount,
    double ReopenedInspectorMilliseconds,
    bool FinalInspectorPublished,
    Guid? FinalInspectorAssetId,
    Guid ExpectedFinalAssetId,
    bool ColorManagementReferenceVerified,
    bool RawVisualProxyVerified);

internal sealed record PipelinePassResult(
    double TotalMilliseconds,
    double DecodeMilliseconds,
    double AnalysisCacheSqliteMilliseconds,
    double InspectorMilliseconds,
    int CacheHits,
    int CacheMisses,
    int InspectorValidCount);

internal sealed record DatabaseRowCounts(int AssetItems, int AnalysisCacheRows, int VisualFeatureRows, int PaletteRows);

internal sealed record CancellationAcceptanceResult(
    string Schema,
    int ADecoderCalls,
    bool ADecoderStartedUncancelled,
    bool ADecoderCancelled,
    int BDecoderCalls,
    bool BDecoderStartedUncancelled,
    bool BDecoderCancelled,
    bool APublished,
    bool BPublished,
    bool CPublished,
    IReadOnlyList<Guid> PublishedAssetIds,
    Guid ExpectedPublishedAssetId,
    string CInspectorState);
