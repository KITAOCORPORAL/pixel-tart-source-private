using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Tests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModularHarnessScale")]
public sealed class ModularHarnessVisualScaleAcceptanceTests
{
    private const int CorpusCount = 100_000;
    private static readonly Guid ReferenceAssetId = Guid.Parse("00000000-0000-0000-0001-000000000000");
    private static readonly Guid TaggedVisualTagId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid VisualSmartFolderId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static string _temporaryRoot = null!;
    private static AssetLibraryDatabase _database = null!;
    private static SqliteAssetLibraryRepository _repository = null!;
    private static CountingFeatureStore _featureStore = null!;
    private static SqliteVisualAssetQueryService _queryService = null!;
    private static AssetVisualAnalysisResult _template = null!;
    private static double _seedMilliseconds;
    private static readonly Dictionary<string, VisualQueryMeasurement> VisualQueries = new(StringComparer.Ordinal);
    private static double _similarityMilliseconds;
    private static double _similarityWarmMilliseconds;
    private static int _similarityResultCount;
    private static VisualSimilarityDiagnostics? _similarityColdDiagnostics;
    private static VisualSimilarityDiagnostics? _similarityWarmDiagnostics;
    private static int _featureStoreCalls;
    private static int _assetRows;
    private static int _featureRows;
    private static int _paletteRows;
    private static int _taggedRows;
    private static int _pairwiseCacheTableCount;

    [ClassInitialize]
    public static async Task Initialize(TestContext _)
    {
        _temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "PixelTart_ModularHarness_V1_Acceptance",
            "VisualScale100K",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryRoot);
        _database = new AssetLibraryDatabase(Path.Combine(_temporaryRoot, "asset-library-100k.db"));
        _repository = new SqliteAssetLibraryRepository(_database);
        await _repository.InitializeAsync();

        _template = CreateLightweightCanonicalResult();
        var seedTimer = Stopwatch.StartNew();
        await SeedCorpusAsync();
        await SaveScaleSmartFolderAsync();
        seedTimer.Stop();
        _seedMilliseconds = seedTimer.Elapsed.TotalMilliseconds;

        var productionStore = new SqliteAssetVisualAnalysisCache(_database);
        _featureStore = new CountingFeatureStore(productionStore);
        _queryService = new SqliteVisualAssetQueryService(_database, _featureStore);
        _assetRows = await CountRowsAsync("AssetItems");
        _featureRows = await CountRowsAsync("AssetVisualFeatures");
        _paletteRows = await CountRowsAsync("AssetVisualPaletteColors");
        _taggedRows = await CountRowsAsync("AssetTagMemberships");
    }

    [TestMethod]
    public async Task VisualQuery_On100KDistributedFeaturesCoversToneHueSaturationContrastAndTagScope()
    {
        Assert.AreEqual(CorpusCount, _assetRows);
        Assert.AreEqual(CorpusCount, _featureRows);
        Assert.AreEqual(CorpusCount, _paletteRows);
        Assert.AreEqual(10_000, _taggedRows);

        await MeasureVisualQueryAsync(
            "tone",
            new(),
            new(State: AssetVisualFeatureState.Valid, ToneKey: ToneKeyTendency.Low),
            33_334,
            match => match.Features.ToneKey == ToneKeyTendency.Low);

        await MeasureVisualQueryAsync(
            "hue",
            new(),
            new(State: AssetVisualFeatureState.Valid, DominantHue: new(30, 60)),
            8_618,
            match => match.Features.DominantHue is >= 30 and <= 60);

        await MeasureVisualQueryAsync(
            "saturation",
            new(),
            new(State: AssetVisualFeatureState.Valid, Saturation: SaturationTendency.High),
            33_333,
            match => match.Features.Saturation == SaturationTendency.High);

        await MeasureVisualQueryAsync(
            "contrast",
            new(),
            new(State: AssetVisualFeatureState.Valid, Contrast: ContrastTendency.Medium),
            33_333,
            match => match.Features.Contrast == ContrastTendency.Medium);

        await MeasureVisualQueryAsync(
            "tag_visual",
            new(TagId: TaggedVisualTagId),
            new(State: AssetVisualFeatureState.Valid, ToneKey: ToneKeyTendency.Low),
            3_334,
            match => match.Features.ToneKey == ToneKeyTendency.Low);

        await MeasureSavedSmartFolderQueryAsync();
    }

    [TestMethod]
    public async Task Similarity_On100KCandidatesScoresOnlyBoundedPoolAndBuildsNoPairwiseCache()
    {
        var callsBefore = _featureStore.GetFeaturesCalls;
        var query = new VisualSimilarityQuery(ReferenceAssetId, new AssetLibraryQuery(PageSize: 200), 100, VisualSimilarityMode.Full);

        var timer = Stopwatch.StartNew();
        var cold = await _queryService.FindSimilarAsync(query);
        timer.Stop();
        _similarityMilliseconds = timer.Elapsed.TotalMilliseconds;
        var coldDiagnostics = _queryService.LastSimilarityDiagnostics ??
            throw new AssertFailedException("Production similarity query did not publish cold-run diagnostics.");
        _similarityColdDiagnostics = coldDiagnostics;

        timer.Restart();
        var warm = await _queryService.FindSimilarAsync(query);
        timer.Stop();
        _similarityWarmMilliseconds = timer.Elapsed.TotalMilliseconds;
        var warmDiagnostics = _queryService.LastSimilarityDiagnostics ??
            throw new AssertFailedException("Production similarity query did not publish warm-run diagnostics.");
        _similarityWarmDiagnostics = warmDiagnostics;

        _featureStoreCalls = _featureStore.GetFeaturesCalls - callsBefore;
        _pairwiseCacheTableCount = await CountPairwiseCacheTablesAsync();
        var rowsAfter = await CountRowsAsync("AssetVisualFeatures");

        Assert.HasCount(AssetVisualFeatureContract.ResultLimit, cold);
        Assert.HasCount(AssetVisualFeatureContract.ResultLimit, warm);
        Assert.IsTrue(cold.All(match => match.Asset.AssetId != ReferenceAssetId));
        Assert.IsTrue(cold.All(match => match.Scores.Overall is >= 0 and <= 100));
        Assert.AreEqual(2, _featureStoreCalls, "Each similarity query may load only its one reference feature through the feature store.");
        AssertSimilarityDiagnostics(coldDiagnostics, cold.Count, query.EffectiveLimit, "cold");
        AssertSimilarityDiagnostics(warmDiagnostics, warm.Count, query.EffectiveLimit, "warm");
        Assert.AreEqual(0, _pairwiseCacheTableCount);
        Assert.AreEqual(CorpusCount, rowsAfter);
        Assert.IsGreaterThan(0, _similarityMilliseconds);
        Assert.IsGreaterThan(0, _similarityWarmMilliseconds);

        _similarityResultCount = cold.Count;
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        try
        {
            var metricsPath = Environment.GetEnvironmentVariable("PIXEL_TART_MODULAR_HARNESS_METRICS_PATH");
            if (!string.IsNullOrWhiteSpace(metricsPath) && Path.IsPathFullyQualified(metricsPath))
            {
                var metrics = new
                {
                    schema = "pixel-tart-modular-harness-v1-visual-scale/v1",
                    corpus_count = CorpusCount,
                    seed_milliseconds = _seedMilliseconds,
                    database = new
                    {
                        asset_rows = _assetRows,
                        visual_feature_rows = _featureRows,
                        candidate_pool_limit = AssetVisualFeatureContract.CandidatePoolLimit,
                        result_limit = AssetVisualFeatureContract.ResultLimit
                    },
                    distribution = new
                    {
                        tone_buckets = 3,
                        hue_buckets = 360,
                        saturation_buckets = 3,
                        contrast_buckets = 3,
                        palette_rows = _paletteRows,
                        tagged_rows = _taggedRows
                    },
                    visual_queries = VisualQueries.ToDictionary(
                        item => item.Key,
                        item => new
                        {
                            milliseconds = item.Value.Milliseconds,
                            total_count = item.Value.TotalCount,
                            result_count = item.Value.ResultCount,
                            saved_rule_count = item.Value.SavedRuleCount
                        }),
                    similarity = new
                    {
                        top_k = 100,
                        result_count = _similarityResultCount,
                        reference_feature_store_calls = _featureStoreCalls,
                        cold = SimilarityMeasurement(_similarityColdDiagnostics, _similarityMilliseconds),
                        warm = SimilarityMeasurement(_similarityWarmDiagnostics, _similarityWarmMilliseconds)
                    },
                    pairwise_cache_table_count = _pairwiseCacheTableCount,
                    pairwise_cache_built = false,
                    color_management_reference_verified = false,
                    raw_visual_proxy_verified = false
                };
                var parent = Path.GetDirectoryName(Path.GetFullPath(metricsPath));
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                await File.WriteAllTextAsync(
                    metricsPath,
                    JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
            }
        }
        finally
        {
            if (_repository is not null) await _repository.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (!string.IsNullOrWhiteSpace(_temporaryRoot) && Directory.Exists(_temporaryRoot))
                Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private static AssetVisualAnalysisResult CreateLightweightCanonicalResult()
    {
        var pixels = new byte[8 * 8 * 3];
        for (var index = 0; index < pixels.Length; index += 3)
        {
            pixels[index] = 210;
            pixels[index + 1] = 52;
            pixels[index + 2] = 64;
        }
        const string templateSourceHash = "source-template-100k";
        var result = VisualAnalysisEngine.Analyze(new(
            ReferenceAssetId,
            VisualAnalysisFingerprint.Compute(new(8, 8, pixels)),
            new(8, 8, pixels),
            AssetVisualFeatureContract.PaletteSize,
            AssetVisualFeatureContract.PaletteSort,
            SourceContentHash: templateSourceHash,
            PreviousSourceContentHash: templateSourceHash));
        return result with
        {
            HistogramR = [1],
            HistogramG = [1],
            HistogramB = [1],
            HistogramLuma = [1],
            Palette = [result.Palette[0] with { Weight = 1 }],
            HistogramLumaSignature = "1",
            PaletteSignature = result.Palette[0].Hex + ":1"
        };
    }

    private static async Task SeedCorpusAsync()
    {
        await using var connection = await _database.OpenConnectionAsync(write: true);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        await using (var assets = connection.CreateCommand())
        {
            assets.Transaction = transaction;
            assets.CommandText = """
                WITH RECURSIVE sequence(value) AS (
                    SELECT 0
                    UNION ALL
                    SELECT value + 1 FROM sequence WHERE value + 1 < $count
                )
                INSERT INTO AssetItems(
                    AssetId,SourcePath,NormalizedSourcePath,DuplicateDiscriminator,DisplayName,Extension,MediaType,
                    FileSize,ContentHash,AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode)
                SELECT
                    printf('00000000-0000-0000-0001-%012d', value),
                    'synthetic://modular-harness/asset-' || printf('%06d', value) || '.jpg',
                    'synthetic://modular-harness/asset-' || printf('%06d', value) || '.jpg',
                    '',
                    'asset-' || printf('%06d', value) || '.jpg',
                    '.jpg','image/jpeg',1,
                    'source-' || printf('%012d', value),
                    $at,$at,value % 6,'',0,0,'Reference'
                FROM sequence;
                """;
            assets.Parameters.AddWithValue("$count", CorpusCount);
            assets.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            await assets.ExecuteNonQueryAsync();
        }

        var resultJson = JsonSerializer.Serialize(_template);
        const string templateSourceHash = "source-template-100k";
        await using (var features = connection.CreateCommand())
        {
            features.Transaction = transaction;
            features.CommandText = """
                WITH distributed AS (
                    SELECT a.*, CAST(substr(a.ContentHash,8) AS INTEGER) AS n FROM AssetItems a
                )
                INSERT INTO AssetVisualFeatures(
                    AssetId,AnalysisVersion,PaletteSize,PaletteSort,ContentFingerprint,SourceContentHash,Outcome,FailureReason,
                    AnalysisSource,SourceProfile,AnalysisProfile,Harmony,ToneKey,Contrast,LuminanceSpan,Saturation,WarmCool,
                    DominantHue,SecondaryHue,AverageHue,AverageLuma,MedianLuma,ContrastMetric,LumaSpreadMetric,
                    AverageSaturation,MedianSaturation,AverageLightness,WarmCoolMetric,DeepShadowRatio,ShadowRatio,MidtoneRatio,
                    HighlightRatio,SpecularRatio,BlackClipRatio,WhiteClipRatio,HistogramLumaSignature,PaletteSignature,
                    ResultJson,CreatedAt,UpdatedAt)
                SELECT
                    d.AssetId,$version,5,'Weight',$fingerprint,d.ContentHash,'Succeeded',NULL,
                    $analysisSource,$sourceProfile,$analysisProfile,$harmony,
                    CASE d.n % 3 WHEN 0 THEN 'Low' WHEN 1 THEN 'Mid' ELSE 'High' END,
                    CASE d.n % 3 WHEN 0 THEN 'Low' WHEN 1 THEN 'Medium' ELSE 'High' END,
                    CASE d.n % 3 WHEN 0 THEN 'Narrow' WHEN 1 THEN 'Medium' ELSE 'Wide' END,
                    CASE d.n % 3 WHEN 0 THEN 'Low' WHEN 1 THEN 'Medium' ELSE 'High' END,
                    CASE d.n % 3 WHEN 0 THEN 'Cool' WHEN 1 THEN 'Neutral' ELSE 'Warm' END,
                    CAST(d.n % 360 AS REAL),CAST((d.n + 120) % 360 AS REAL),CAST((d.n + 240) % 360 AS REAL),
                    CAST(d.n % 256 AS REAL),CAST((d.n * 5) % 256 AS REAL),
                    CAST(d.n % 101 AS REAL) / 100.0,CAST((d.n * 3) % 101 AS REAL) / 100.0,
                    CAST((d.n * 7) % 101 AS REAL) / 100.0,CAST((d.n * 9) % 101 AS REAL) / 100.0,
                    CAST((d.n * 11) % 101 AS REAL),CAST((d.n % 201) - 100 AS REAL) / 100.0,
                    0.1,0.2,0.4,0.2,0.1,0.0,0.0,$histogramSignature,$paletteSignature,
                    replace(replace($resultJson,$templateAssetId,d.AssetId),$templateSourceHash,d.ContentHash),$created,$created
                FROM distributed d;
                """;
            features.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion);
            features.Parameters.AddWithValue("$fingerprint", _template.ContentHash);
            features.Parameters.AddWithValue("$analysisSource", _template.AnalysisSource.ToString());
            features.Parameters.AddWithValue("$sourceProfile", _template.SourceProfile);
            features.Parameters.AddWithValue("$analysisProfile", _template.AnalysisProfile);
            features.Parameters.AddWithValue("$harmony", _template.Harmony.ToString());
            features.Parameters.AddWithValue("$histogramSignature", _template.HistogramLumaSignature);
            features.Parameters.AddWithValue("$paletteSignature", _template.PaletteSignature);
            features.Parameters.AddWithValue("$resultJson", resultJson);
            features.Parameters.AddWithValue("$templateAssetId", ReferenceAssetId.ToString("D"));
            features.Parameters.AddWithValue("$templateSourceHash", templateSourceHash);
            features.Parameters.AddWithValue("$created", _template.CreatedAt.ToString("O"));
            await features.ExecuteNonQueryAsync();
        }

        await using (var palette = connection.CreateCommand())
        {
            palette.Transaction = transaction;
            palette.CommandText = """
                WITH distributed AS (
                    SELECT a.AssetId, CAST(substr(a.ContentHash,8) AS INTEGER) AS n FROM AssetItems a
                )
                INSERT INTO AssetVisualPaletteColors(
                    AssetId,AnalysisVersion,ColorIndex,Red,Green,Blue,LabL,LabA,LabB,Hue,Saturation,Chroma,Weight,Hex)
                SELECT AssetId,$version,0,n % 256,(n * 3) % 256,(n * 7) % 256,
                       50.0,30.0,20.0,CAST(n % 360 AS REAL),0.8,36.0555,1.0,'#D23440'
                FROM distributed;
                """;
            palette.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion);
            await palette.ExecuteNonQueryAsync();
        }

        await using (var tag = connection.CreateCommand())
        {
            tag.Transaction = transaction;
            tag.CommandText = """
                INSERT INTO AssetTags(TagId,Name,TagGroupId,SortOrder,UsageCount,CreatedAt,IsArchived)
                VALUES($tag,'100K distributed tag',NULL,0,10000,$created,0);
                INSERT INTO AssetTagMemberships(AssetId,TagId,AddedAt)
                SELECT a.AssetId,$tag,$created FROM AssetItems a
                WHERE CAST(substr(a.ContentHash,8) AS INTEGER) % 10 = 0;
                """;
            tag.Parameters.AddWithValue("$tag", TaggedVisualTagId.ToString("D"));
            tag.Parameters.AddWithValue("$created", _template.CreatedAt.ToString("O"));
            await tag.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static async Task<int> CountRowsAsync(string table)
    {
        if (table is not ("AssetItems" or "AssetVisualFeatures" or "AssetVisualPaletteColors" or "AssetTagMemberships")) throw new ArgumentOutOfRangeException(nameof(table));
        await using var connection = await _database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SaveScaleSmartFolderAsync()
    {
        var folder = new SmartFolder(
            VisualSmartFolderId,
            "100K Tag + Tone + Saturation + Hue + Rating",
            SmartFolderLogic.And,
            "Synthetic acceptance folder with persisted mixed metadata and visual rules.");
        await _repository.SaveSmartFolderAsync(folder,
        [
            new(Guid.NewGuid(), VisualSmartFolderId, SmartFolderField.Tag, SmartFolderOperator.Equals, "100K distributed tag", SortOrder: 0),
            new(Guid.NewGuid(), VisualSmartFolderId, SmartFolderField.VisualToneKey, SmartFolderOperator.Equals, ToneKeyTendency.Low.ToString(), SortOrder: 1),
            new(Guid.NewGuid(), VisualSmartFolderId, SmartFolderField.VisualAverageSaturation, SmartFolderOperator.GreaterThanOrEqual, "0.5", SortOrder: 2),
            new(Guid.NewGuid(), VisualSmartFolderId, SmartFolderField.VisualDominantHue, SmartFolderOperator.InRange, "30..60", SortOrder: 3),
            new(Guid.NewGuid(), VisualSmartFolderId, SmartFolderField.Rating, SmartFolderOperator.Equals, "0", SortOrder: 4)
        ]);
    }

    private static async Task<int> CountPairwiseCacheTablesAsync()
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type='table' AND (
                lower(name) LIKE '%pairwise%' OR
                lower(name) LIKE '%allpairs%' OR
                lower(name) LIKE '%all_pairs%');
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task MeasureVisualQueryAsync(
        string name,
        AssetLibraryQuery scope,
        VisualAssetFilter filter,
        int expectedTotal,
        Func<VisualAssetMatch, bool> itemPredicate)
    {
        var timer = Stopwatch.StartNew();
        var page = await _queryService.QueryAsync(new(scope, filter, PageSize: 100));
        timer.Stop();

        Assert.AreEqual(expectedTotal, page.TotalCount, $"Unexpected distributed count for {name}.");
        Assert.HasCount(100, page.Items, $"Expected a bounded full page for {name}.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(page.NextCursor), $"Expected more distributed results for {name}.");
        Assert.IsTrue(page.Items.All(itemPredicate), $"Returned item violates the {name} filter.");
        Assert.IsGreaterThan(0, timer.Elapsed.TotalMilliseconds);
        VisualQueries[name] = new(timer.Elapsed.TotalMilliseconds, page.TotalCount, page.Items.Count);
    }

    private static async Task MeasureSavedSmartFolderQueryAsync()
    {
        var folders = await _repository.ListSmartFoldersAsync();
        var folder = folders.Single(item => item.SmartFolderId == VisualSmartFolderId);
        var rules = await _repository.ListSmartFolderRulesAsync(folder.SmartFolderId);
        Assert.AreEqual(SmartFolderLogic.And, folder.Logic);
        Assert.HasCount(5, rules);
        CollectionAssert.AreEquivalent(
            new[]
            {
                SmartFolderField.Tag,
                SmartFolderField.VisualToneKey,
                SmartFolderField.VisualAverageSaturation,
                SmartFolderField.VisualDominantHue,
                SmartFolderField.Rating
            },
            rules.Select(rule => rule.Field).ToArray());

        var timer = Stopwatch.StartNew();
        var page = await _repository.QueryAsync(new(SmartFolderId: folder.SmartFolderId, PageSize: 100));
        timer.Stop();

        Assert.IsNull(page.RegexError);
        Assert.AreEqual(286, page.TotalCount);
        Assert.HasCount(100, page.Items);
        Assert.IsFalse(string.IsNullOrWhiteSpace(page.NextCursor));
        Assert.IsTrue(page.Items.All(item => item.Rating == 0));
        Assert.IsGreaterThan(0, timer.Elapsed.TotalMilliseconds);
        VisualQueries["smart_folder"] = new(timer.Elapsed.TotalMilliseconds, page.TotalCount, page.Items.Count, rules.Count);
    }

    private static void AssertSimilarityDiagnostics(
        VisualSimilarityDiagnostics diagnostics,
        int expectedReturnedRows,
        int requestedTopK,
        string phase)
    {
        Assert.IsGreaterThan(0, diagnostics.CandidateRows, $"{phase}: production pruning returned no real candidate rows.");
        Assert.IsLessThanOrEqualTo(AssetVisualFeatureContract.CandidatePoolLimit, diagnostics.CandidateRows, $"{phase}: candidate rows exceeded the production pool bound.");
        Assert.IsLessThan(CorpusCount, diagnostics.CandidateRows, $"{phase}: similarity exact-scored the full corpus.");
        Assert.AreEqual(expectedReturnedRows, diagnostics.ReturnedRows, $"{phase}: diagnostics returned rows disagree with the real result.");
        Assert.IsLessThanOrEqualTo(requestedTopK, diagnostics.ReturnedRows, $"{phase}: returned rows exceeded TopK.");
        Assert.IsGreaterThan(0, diagnostics.PruningMilliseconds, $"{phase}: pruning duration was not measured.");
        Assert.IsGreaterThan(0, diagnostics.ExactScoringMilliseconds, $"{phase}: exact scoring duration was not measured.");
        Assert.IsGreaterThan(0, diagnostics.TotalMilliseconds, $"{phase}: total duration was not measured.");
    }

    private static object? SimilarityMeasurement(VisualSimilarityDiagnostics? diagnostics, double wallMilliseconds) =>
        diagnostics is null
            ? null
            : new
            {
                candidate_rows = diagnostics.CandidateRows,
                pruning_milliseconds = diagnostics.PruningMilliseconds,
                exact_scoring_milliseconds = diagnostics.ExactScoringMilliseconds,
                total_milliseconds = diagnostics.TotalMilliseconds,
                wall_milliseconds = wallMilliseconds,
                returned_rows = diagnostics.ReturnedRows
            };

    private sealed class CountingFeatureStore(IAssetVisualFeatureStore inner) : IAssetVisualFeatureStore
    {
        private int _getFeaturesCalls;
        public int GetFeaturesCalls => Volatile.Read(ref _getFeaturesCalls);

        public Task<AssetVisualAnalysisResult?> TryGetAsync(Guid assetId, string contentHash, int paletteSize, PaletteSortMode paletteSort, string analysisVersion = AssetVisualAnalysisResult.CurrentVersion, CancellationToken cancellationToken = default) =>
            inner.TryGetAsync(assetId, contentHash, paletteSize, paletteSort, analysisVersion, cancellationToken);

        public Task StoreAsync(AssetVisualAnalysisResult result, CancellationToken cancellationToken = default) =>
            inner.StoreAsync(result, cancellationToken);

        public Task<AssetVisualFeatures> GetFeaturesAsync(Guid assetId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getFeaturesCalls);
            return inner.GetFeaturesAsync(assetId, cancellationToken);
        }

        public Task RecordFailureAsync(Guid assetId, string? sourceContentHash, string failureReason, CancellationToken cancellationToken = default, string? previousSourceContentHash = null) =>
            inner.RecordFailureAsync(assetId, sourceContentHash, failureReason, cancellationToken, previousSourceContentHash);
    }

    private sealed record VisualQueryMeasurement(double Milliseconds, int TotalCount, int ResultCount, int SavedRuleCount = 0);
}
