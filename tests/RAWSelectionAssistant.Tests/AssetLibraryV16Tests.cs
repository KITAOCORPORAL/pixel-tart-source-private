using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryV16Tests
{
    [TestMethod]
    public async Task CanonicalFeaturesAreIndependentFromInspectorOptionsAndDeriveThreeStates()
    {
        await using var setup = await Setup.CreateAsync(3);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache);
        var first = setup.Assets[0];
        var inspector = await service.AnalyzeAsync(Request(first, Solid(8, 8, 20, 70, 160), 3, PaletteSortMode.Hue));
        Assert.AreEqual(3, inspector.PaletteSize);
        Assert.AreEqual(AssetVisualFeatureState.NotAnalyzed, (await cache.GetFeaturesAsync(first.AssetId)).Summary.State);

        var canonical = await service.AnalyzeAsync(Request(first, Solid(8, 8, 20, 70, 160)));
        var valid = await cache.GetFeaturesAsync(first.AssetId);
        Assert.AreEqual(AssetVisualFeatureState.Valid, valid.Summary.State);
        Assert.AreEqual(AssetVisualFeatureContract.PaletteSize, valid.Analysis!.PaletteSize);
        Assert.AreEqual(AssetVisualFeatureContract.PaletteSort, valid.Analysis.PaletteSort);
        Assert.AreEqual(canonical.ContentHash, valid.Summary.ContentFingerprint);

        await using (var connection = await setup.Database.OpenConnectionAsync(write: true))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE AssetItems SET ContentHash='changed-source' WHERE AssetId=$asset;";
            command.Parameters.AddWithValue("$asset", first.AssetId.ToString("D")); await command.ExecuteNonQueryAsync();
        }
        Assert.AreEqual(AssetVisualFeatureState.Stale, (await cache.GetFeaturesAsync(first.AssetId)).Summary.State);
        Assert.AreEqual(AssetVisualFeatureState.NotAnalyzed, (await cache.GetFeaturesAsync(setup.Assets[1].AssetId)).Summary.State);
    }

    [TestMethod]
    public async Task VisualQueryUsesCircularHueAndFifteenPercentPaletteWeight()
    {
        await using var setup = await Setup.CreateAsync(3);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache); var query = new SqliteVisualAssetQueryService(setup.Database, cache);
        await service.AnalyzeAsync(Request(setup.Assets[0], Stripes(100, 1, ((byte)240, (byte)20, (byte)40), ((byte)255, (byte)255, (byte)255), 20)));
        await service.AnalyzeAsync(Request(setup.Assets[1], Stripes(100, 1, ((byte)240, (byte)20, (byte)40), ((byte)128, (byte)128, (byte)128), 10)));
        await service.AnalyzeAsync(Request(setup.Assets[2], Solid(10, 10, 128, 128, 128)));

        var page = await query.QueryAsync(new(new(), new(DominantHue: new(330, 20)), PageSize: 20));
        CollectionAssert.AreEqual(new[] { setup.Assets[0].AssetId }, page.Items.Select(item => item.Asset.AssetId).ToArray());
        var neutral = await query.SearchByColorAsync(new(new(), new(PaletteColor: VisualAnalysisEngine.ToLab(new(128, 128, 128)), MaximumDeltaE: 5), PageSize: 20));
        Assert.IsTrue(neutral.Any(item => item.Asset.AssetId == setup.Assets[2].AssetId), "Neutral Lab search must not be blocked by hue/chroma guards.");
    }

    [TestMethod]
    public async Task ColorSearchRanksWholeCandidateSetInsteadOfCurrentAddedPage()
    {
        await using var setup = await Setup.CreateAsync(12);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache); var query = new SqliteVisualAssetQueryService(setup.Database, cache);
        for (var index = 0; index < setup.Assets.Count; index++)
        {
            var color = index == 0 ? new VisualRgb24(220, 30, 40) : new VisualRgb24((byte)(15 + index), 130, 210);
            await service.AnalyzeAsync(Request(setup.Assets[index], Solid(4, 4, color.R, color.G, color.B)));
        }
        var matches = await query.SearchByColorAsync(new(new(), new(PaletteColor: VisualAnalysisEngine.ToLab(new(220, 30, 40)), MaximumDeltaE: 200), PageSize: 3));
        Assert.IsGreaterThanOrEqualTo(1, matches.Count);
        Assert.AreEqual(setup.Assets[0].AssetId, matches[0].Asset.AssetId);
        Assert.AreEqual(0, matches[0].ColorDeltaE!.Value, .001);
    }

    [TestMethod]
    public async Task SimilarityBranchTruncationKeepsNearestReferenceBeyondGuidOrderedCap()
    {
        const int distractorCount = 1_260;
        await using var setup = await Setup.CreateAsync(1);
        var reference = setup.Assets[0];
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache); var query = new SqliteVisualAssetQueryService(setup.Database, cache);
        var referenceResult = await service.AnalyzeAsync(Request(reference, Solid(4, 4, 100, 100, 100)));
        var targetId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        await InsertSyntheticCandidatesAsync(setup.Database, referenceResult, distractorCount, targetId);

        var matches = await query.FindSimilarAsync(new(reference.AssetId, new(), 100));

        Assert.IsTrue(matches.Any(match => match.Asset.AssetId == targetId), "The true nearest candidate must survive each branch cap even when its GUID sorts last.");
        Assert.AreEqual(targetId, matches[0].Asset.AssetId);
        var diagnostics = query.LastSimilarityDiagnostics;
        Assert.IsNotNull(diagnostics);
        Assert.IsGreaterThan(0, diagnostics.CandidateRows);
        Assert.IsGreaterThanOrEqualTo(0, diagnostics.PruningMilliseconds);
        Assert.IsGreaterThanOrEqualTo(0, diagnostics.ExactScoringMilliseconds);
        Assert.IsGreaterThanOrEqualTo(diagnostics.PruningMilliseconds, diagnostics.TotalMilliseconds);
        Assert.IsGreaterThanOrEqualTo(diagnostics.ExactScoringMilliseconds, diagnostics.TotalMilliseconds);
        Assert.AreEqual(matches.Count, diagnostics.ReturnedRows);
    }

    [TestMethod]
    public async Task VisualFilterCombinesTagToneHueSaturationAndContrastInOneSqlQuery()
    {
        await using var setup = await Setup.CreateAsync(2);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database);
        var analysis = new AssetVisualAnalysisService(cache);
        var query = new SqliteVisualAssetQueryService(setup.Database, cache);
        var reference = await analysis.AnalyzeAsync(Request(setup.Assets[0], Solid(12, 12, 20, 150, 55)));
        await analysis.AnalyzeAsync(Request(setup.Assets[1], Solid(12, 12, 35, 65, 210)));
        var tag = (await setup.Repository.BatchCreateTagsAsync("人体")).Single();
        await setup.Repository.AddTagsAsync([setup.Assets[0].AssetId], [tag.TagId]);

        var hue = reference.DominantHue;
        var page = await query.QueryAsync(new(
            new(TagId: tag.TagId),
            new(
                DominantHue: new VisualHueRange((hue + 355) % 360, (hue + 5) % 360),
                ToneKey: reference.ToneKey,
                Contrast: reference.Contrast,
                Saturation: reference.Saturation),
            PageSize: 20));

        CollectionAssert.AreEqual(new[] { setup.Assets[0].AssetId }, page.Items.Select(item => item.Asset.AssetId).ToArray());
    }

    [TestMethod]
    public void SimilarityIsSymmetricBoundedAndSensitiveToPaletteWeights()
    {
        var redHeavy = VisualAnalysisEngine.Analyze(Request(Guid.NewGuid(), Stripes(100, 1, ((byte)220, (byte)30, (byte)40), ((byte)30, (byte)50, (byte)220), 80)));
        var blueHeavy = VisualAnalysisEngine.Analyze(Request(Guid.NewGuid(), Stripes(100, 1, ((byte)220, (byte)30, (byte)40), ((byte)30, (byte)50, (byte)220), 20)));
        var identical = VisualSimilarityScorer.Score(redHeavy, redHeavy);
        var changedWeights = VisualSimilarityScorer.Score(redHeavy, blueHeavy);
        var reverse = VisualSimilarityScorer.Score(blueHeavy, redHeavy);
        Assert.AreEqual(100, identical.Overall, .001);
        Assert.IsLessThan(identical.Color, changedWeights.Color);
        Assert.AreEqual(changedWeights.Overall, reverse.Overall, .001);
        Assert.IsTrue(new[] { changedWeights.Color, changedWeights.Tone, changedWeights.Contrast, changedWeights.Saturation, changedWeights.Overall }.All(score => score is >= 0 and <= 100));
        var colorProfile = VisualSimilarityScorer.Score(redHeavy, blueHeavy, new(1, 0, 0, 0));
        Assert.AreEqual(colorProfile.Color, colorProfile.Overall, .001);
    }

    [TestMethod]
    public async Task PaletteOnlyQueryRanksTopFiveColorsAndWeightsInsteadOfFirstColor()
    {
        await using var setup = await Setup.CreateAsync(3);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache); var query = new SqliteVisualAssetQueryService(setup.Database, cache);
        var reference = Stripes(100, 1, ((byte)20, (byte)20, (byte)20), ((byte)220, (byte)30, (byte)40), 80);
        var sameWeights = Stripes(100, 1, ((byte)20, (byte)20, (byte)20), ((byte)220, (byte)30, (byte)40), 80);
        var reversedWeights = Stripes(100, 1, ((byte)20, (byte)20, (byte)20), ((byte)220, (byte)30, (byte)40), 20);
        await service.AnalyzeAsync(Request(setup.Assets[0], reference));
        await service.AnalyzeAsync(Request(setup.Assets[1], sameWeights));
        await service.AnalyzeAsync(Request(setup.Assets[2], reversedWeights));

        var matches = await query.FindSimilarAsync(new(setup.Assets[0].AssetId, new(), 10, VisualSimilarityMode.Palette));

        Assert.AreEqual(setup.Assets[1].AssetId, matches[0].Asset.AssetId);
        Assert.IsGreaterThan(matches[1].Scores.PaletteComponent, matches[0].Scores.PaletteComponent);
    }

    [TestMethod]
    public async Task BatchUsesBoundedPriorityOrderAndIsolatesOneFailure()
    {
        await using var setup = await Setup.CreateAsync(4);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache); var processor = new AssetVisualAnalysisBatchProcessor(service, cache, 1);
        var started = new List<Guid>();
        VisualAnalysisBatchItem Item(AssetItem asset, VisualAnalysisPriority priority, bool fail = false) => new(asset.AssetId, asset.ContentHash, priority, token =>
        {
            started.Add(asset.AssetId);
            if (fail) throw new InvalidDataException("synthetic decode failure");
            return Task.FromResult(Request(asset, Solid(4, 4, 10, 40, 90)));
        });
        var result = await processor.ProcessAsync([Item(setup.Assets[0], VisualAnalysisPriority.Background), Item(setup.Assets[1], VisualAnalysisPriority.Interactive), Item(setup.Assets[2], VisualAnalysisPriority.Visible, fail: true), Item(setup.Assets[3], VisualAnalysisPriority.Background)]);
        CollectionAssert.AreEqual(new[] { setup.Assets[1].AssetId, setup.Assets[2].AssetId, setup.Assets[0].AssetId, setup.Assets[3].AssetId }, started);
        Assert.AreEqual(3, result.Succeeded); Assert.AreEqual(1, result.Failed);
        Assert.AreEqual(AssetVisualFeatureState.Failed, (await cache.GetFeaturesAsync(setup.Assets[2].AssetId)).Summary.State);
    }

    [TestMethod]
    public async Task BatchCancellationMarksEveryUnstartedItemWithoutUnboundedTasks()
    {
        await using var setup = await Setup.CreateAsync(20);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var processor = new AssetVisualAnalysisBatchProcessor(new(cache), cache, 2);
        using var cancellation = new CancellationTokenSource(); var entered = 0;
        var items = setup.Assets.Select(asset => new VisualAnalysisBatchItem(asset.AssetId, asset.ContentHash, VisualAnalysisPriority.Background, async token =>
        {
            if (Interlocked.Increment(ref entered) == 2) cancellation.Cancel();
            await Task.Delay(25, token); return Request(asset, Solid(4, 4, 1, 2, 3));
        }));
        var result = await processor.ProcessAsync(items, cancellationToken: cancellation.Token);
        Assert.IsTrue(result.Cancelled);
        Assert.HasCount(setup.Assets.Count, result.Items);
        Assert.IsGreaterThan(0, result.CancelledCount);
    }

    [TestMethod]
    public async Task SameAssetAnalysisLockSurvivesConcurrentAcquireAndCleansUpSafely()
    {
        await using var setup = await Setup.CreateAsync(1);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache); var asset = setup.Assets[0];
        var tasks = Enumerable.Range(0, 64).Select(index => Task.Run(async () =>
        {
            for (var iteration = 0; iteration < 8; iteration++)
            {
                var pixels = Solid(4, 4, (byte)(index + iteration), 50, 90);
                var result = await service.AnalyzeAsync(Request(asset, pixels, 3));
                Assert.AreEqual(asset.AssetId, result.AssetId);
            }
        }));
        await Task.WhenAll(tasks);
    }

    [TestMethod]
    public async Task InteractiveAnalysisCancelsSameAssetBackgroundWork()
    {
        await using var setup = await Setup.CreateAsync(1);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var processor = new AssetVisualAnalysisBatchProcessor(new(cache), cache, 1); var asset = setup.Assets[0];
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var background = processor.ProcessAsync([new(asset.AssetId, asset.ContentHash, VisualAnalysisPriority.Background, async token =>
        {
            entered.SetResult(); await Task.Delay(TimeSpan.FromSeconds(10), token); return Request(asset, Solid(4, 4, 1, 1, 1));
        })]);
        await entered.Task;
        var interactive = await processor.AnalyzeInteractiveAsync(asset.AssetId, _ => Task.FromResult(Request(asset, Solid(4, 4, 220, 20, 30))));
        var batch = await background;
        Assert.AreEqual(asset.AssetId, interactive.AssetId);
        Assert.AreEqual(VisualAnalysisBatchItemState.Cancelled, batch.Items.Single().State);
    }

    [TestMethod]
    public async Task VisualSmartFolderUsesSingleSqlHueWrapAndRejectsInvalidOperator()
    {
        await using var setup = await Setup.CreateAsync(2);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache);
        await service.AnalyzeAsync(Request(setup.Assets[0], Solid(8, 8, 235, 25, 40)));
        await service.AnalyzeAsync(Request(setup.Assets[1], Solid(8, 8, 20, 80, 220)));
        var smart = new SmartFolder(Guid.NewGuid(), "visual hue", SmartFolderLogic.And);
        await setup.Repository.SaveSmartFolderAsync(smart, [new(Guid.NewGuid(), smart.SmartFolderId, SmartFolderField.VisualDominantHue, SmartFolderOperator.InRange, "330..20")]);
        var page = await setup.Repository.QueryAsync(new(SmartFolderId: smart.SmartFolderId, PageSize: 10));
        CollectionAssert.AreEqual(new[] { setup.Assets[0].AssetId }, page.Items.Select(asset => asset.AssetId).ToArray());

        var invalid = new SmartFolder(Guid.NewGuid(), "invalid visual", SmartFolderLogic.And);
        await setup.Repository.SaveSmartFolderAsync(invalid, [new(Guid.NewGuid(), invalid.SmartFolderId, SmartFolderField.VisualAverageSaturation, SmartFolderOperator.Contains, "0.3")]);
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Repository.QueryAsync(new(SmartFolderId: invalid.SmartFolderId)));
    }

    [TestMethod]
    public async Task SameProxyCacheHitRefreshesProvenanceAcrossConsecutiveSourceVersions()
    {
        await using var setup = await Setup.CreateAsync(1);
        var cache = new SqliteAssetVisualAnalysisCache(setup.Database); var service = new AssetVisualAnalysisService(cache); var asset = setup.Assets[0]; var pixels = Solid(4, 4, 20, 60, 100);
        var first = await service.AnalyzeAsync(Request(asset, pixels));
        var secondSource = "source-version-two";
        await using (var connection = await setup.Database.OpenConnectionAsync(write: true))
        await using (var command = connection.CreateCommand()) { command.CommandText = "UPDATE AssetItems SET ContentHash=$hash WHERE AssetId=$asset;"; command.Parameters.AddWithValue("$hash", secondSource); command.Parameters.AddWithValue("$asset", asset.AssetId.ToString("D")); await command.ExecuteNonQueryAsync(); }
        var second = await service.AnalyzeAsync(Request(asset.AssetId, pixels) with { SourceContentHash = secondSource, PreviousSourceContentHash = secondSource, AnalysisSource = VisualAnalysisSourceKind.RenderedProxy, SourceProfile = "ProxyProfile" });
        Assert.IsTrue(second.CacheHit); Assert.AreEqual(VisualAnalysisSourceKind.RenderedProxy, second.AnalysisSource); Assert.AreEqual("ProxyProfile", second.SourceProfile);

        var thirdSource = "source-version-three";
        await using (var connection = await setup.Database.OpenConnectionAsync(write: true))
        await using (var command = connection.CreateCommand()) { command.CommandText = "UPDATE AssetItems SET ContentHash=$hash WHERE AssetId=$asset;"; command.Parameters.AddWithValue("$hash", thirdSource); command.Parameters.AddWithValue("$asset", asset.AssetId.ToString("D")); await command.ExecuteNonQueryAsync(); }
        var third = await service.AnalyzeAsync(Request(asset.AssetId, pixels) with { SourceContentHash = thirdSource, PreviousSourceContentHash = thirdSource, AnalysisSource = VisualAnalysisSourceKind.EmbeddedPreview, SourceProfile = "EmbeddedProfile" });
        Assert.IsTrue(third.CacheHit); Assert.AreEqual(VisualAnalysisSourceKind.EmbeddedPreview, third.AnalysisSource); Assert.AreEqual(thirdSource, (await cache.GetFeaturesAsync(asset.AssetId)).Summary.SourceContentHash);
        Assert.AreEqual(first.ContentHash, third.ContentHash);
    }

    [TestMethod]
    public void DominantChromaticPredicateMatchesHueQueryForExtremeDarkAndLightColors()
    {
        var dark = VisualAnalysisEngine.Analyze(Request(Guid.NewGuid(), Solid(4, 4, 1, 0, 0)));
        var light = VisualAnalysisEngine.Analyze(Request(Guid.NewGuid(), Solid(4, 4, 255, 254, 254)));
        Assert.IsFalse(dark.HasDominantChromaticColor);
        Assert.IsFalse(light.HasDominantChromaticColor);
        var red = VisualAnalysisEngine.Analyze(Request(Guid.NewGuid(), Solid(4, 4, 220, 20, 30)));
        Assert.IsTrue(red.HasDominantChromaticColor);
    }

    private static AssetVisualAnalysisRequest Request(AssetItem asset, VisualPixelBuffer pixels, int size = 5, PaletteSortMode sort = PaletteSortMode.Weight) => Request(asset.AssetId, pixels, size, sort) with { SourceContentHash = asset.ContentHash, PreviousSourceContentHash = asset.ContentHash };
    private static AssetVisualAnalysisRequest Request(Guid id, VisualPixelBuffer pixels, int size = 5, PaletteSortMode sort = PaletteSortMode.Weight) => new(id, VisualAnalysisFingerprint.Compute(pixels), pixels, size, sort, SourceContentHash: "source-" + id.ToString("N"));
    private static VisualPixelBuffer Solid(int width, int height, byte red, byte green, byte blue) { var bytes = new byte[width * height * 3]; for (var pixel = 0; pixel < width * height; pixel++) { bytes[pixel * 3] = red; bytes[pixel * 3 + 1] = green; bytes[pixel * 3 + 2] = blue; } return new(width, height, bytes); }
    private static VisualPixelBuffer Stripes(int count, int height, (byte R, byte G, byte B) first, (byte R, byte G, byte B) second, int firstPercent) { var bytes = new byte[count * height * 3]; for (var pixel = 0; pixel < count * height; pixel++) { var color = pixel % count < count * firstPercent / 100 ? first : second; bytes[pixel * 3] = color.R; bytes[pixel * 3 + 1] = color.G; bytes[pixel * 3 + 2] = color.B; } return new(count, height, bytes); }

    private static async Task InsertSyntheticCandidatesAsync(AssetLibraryDatabase database, AssetVisualAnalysisResult reference, int distractorCount, Guid targetId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var connection = await database.OpenConnectionAsync(write: true);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync();
        for (var index = 0; index <= distractorCount; index++)
        {
            var isTarget = index == distractorCount; var assetId = isTarget ? targetId : Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}");
            var sourceHash = "synthetic-" + assetId.ToString("N");
            var luma = isTarget ? reference.AverageLuma : Math.Min(255, reference.AverageLuma + 100 + index % 10);
            var representedPixels = reference.HistogramLuma.Aggregate(0u, (total, value) => total + value);
            var histogram = isTarget ? reference.HistogramLuma : Enumerable.Range(0, 256).Select(bin => bin == 255 ? representedPixels : 0u).ToArray();
            var result = reference with
            {
                AssetId = assetId,
                SourceContentHash = sourceHash,
                PreviousSourceContentHash = sourceHash,
                AverageLuma = luma,
                MedianLuma = luma,
                HistogramLuma = histogram,
                ToneZones = isTarget ? reference.ToneZones : new(0, 0, 0, 0, 1),
                ToneKey = isTarget ? reference.ToneKey : ToneKeyTendency.High,
                CacheHit = false
            };
            await using (var asset = connection.CreateCommand())
            {
                asset.Transaction = transaction; asset.CommandText = """
                    INSERT INTO AssetItems(AssetId,SourcePath,NormalizedSourcePath,DuplicateDiscriminator,DisplayName,Extension,MediaType,FileSize,ContentHash,AddedAt,ModifiedAt)
                    VALUES($id,$path,$path,'',$name,'.jpg','image/jpeg',1,$hash,$at,$at);
                    """;
                asset.Parameters.AddWithValue("$id", assetId.ToString("D")); asset.Parameters.AddWithValue("$path", $"synthetic://{assetId:D}"); asset.Parameters.AddWithValue("$name", $"candidate-{index:0000}.jpg"); asset.Parameters.AddWithValue("$hash", sourceHash); asset.Parameters.AddWithValue("$at", now.AddSeconds(-index).ToString("O"));
                await asset.ExecuteNonQueryAsync();
            }
            await using (var feature = connection.CreateCommand())
            {
                feature.Transaction = transaction; feature.CommandText = """
                    INSERT INTO AssetVisualFeatures(AssetId,AnalysisVersion,PaletteSize,PaletteSort,ContentFingerprint,SourceContentHash,Outcome,AnalysisSource,SourceProfile,AnalysisProfile,Harmony,ToneKey,Contrast,LuminanceSpan,Saturation,WarmCool,DominantHue,AverageLuma,MedianLuma,ContrastMetric,LumaSpreadMetric,AverageSaturation,MedianSaturation,AverageLightness,WarmCoolMetric,DeepShadowRatio,ShadowRatio,MidtoneRatio,HighlightRatio,SpecularRatio,BlackClipRatio,WhiteClipRatio,HistogramLumaSignature,PaletteSignature,ResultJson,CreatedAt,UpdatedAt)
                    VALUES($id,$version,5,'Weight',$fingerprint,$hash,'Succeeded',$source,$sourceProfile,$analysisProfile,$harmony,$tone,$contrast,$span,$saturation,$warmCool,$hue,$luma,$luma,$contrastMetric,$spread,$averageSaturation,$medianSaturation,$lightness,$warmMetric,$deep,$shadow,$mid,$highlight,$specular,$black,$white,$histogram,$palette,$json,$at,$at);
                    """;
                feature.Parameters.AddWithValue("$id", assetId.ToString("D")); feature.Parameters.AddWithValue("$version", reference.AnalysisVersion); feature.Parameters.AddWithValue("$fingerprint", reference.ContentHash); feature.Parameters.AddWithValue("$hash", sourceHash); feature.Parameters.AddWithValue("$source", reference.AnalysisSource.ToString()); feature.Parameters.AddWithValue("$sourceProfile", reference.SourceProfile); feature.Parameters.AddWithValue("$analysisProfile", reference.AnalysisProfile); feature.Parameters.AddWithValue("$harmony", reference.Harmony.ToString()); feature.Parameters.AddWithValue("$tone", reference.ToneKey.ToString()); feature.Parameters.AddWithValue("$contrast", reference.Contrast.ToString()); feature.Parameters.AddWithValue("$span", reference.LuminanceSpan.ToString()); feature.Parameters.AddWithValue("$saturation", reference.Saturation.ToString()); feature.Parameters.AddWithValue("$warmCool", reference.WarmCool.ToString()); feature.Parameters.AddWithValue("$hue", reference.HasDominantChromaticColor ? reference.DominantHue : DBNull.Value); feature.Parameters.AddWithValue("$luma", luma); feature.Parameters.AddWithValue("$contrastMetric", reference.ContrastMetric); feature.Parameters.AddWithValue("$spread", reference.LuminanceSpanMetric); feature.Parameters.AddWithValue("$averageSaturation", reference.AverageSaturation); feature.Parameters.AddWithValue("$medianSaturation", reference.MedianSaturation); feature.Parameters.AddWithValue("$lightness", reference.AverageLightness); feature.Parameters.AddWithValue("$warmMetric", reference.WarmCoolMetric); feature.Parameters.AddWithValue("$deep", reference.ToneZones.DeepShadow); feature.Parameters.AddWithValue("$shadow", reference.ToneZones.Shadow); feature.Parameters.AddWithValue("$mid", reference.ToneZones.Midtone); feature.Parameters.AddWithValue("$highlight", reference.ToneZones.Highlight); feature.Parameters.AddWithValue("$specular", reference.ToneZones.Specular); feature.Parameters.AddWithValue("$black", reference.BlackClipRatio); feature.Parameters.AddWithValue("$white", reference.WhiteClipRatio); feature.Parameters.AddWithValue("$histogram", reference.HistogramLumaSignature); feature.Parameters.AddWithValue("$palette", reference.PaletteSignature); feature.Parameters.AddWithValue("$json", System.Text.Json.JsonSerializer.Serialize(result)); feature.Parameters.AddWithValue("$at", now.ToString("O"));
                await feature.ExecuteNonQueryAsync();
            }
        }
        await transaction.CommitAsync();
    }

    private sealed class Setup : IAsyncDisposable
    {
        private readonly string _root;
        private Setup(string root, AssetLibraryDatabase database, SqliteAssetLibraryRepository repository, IReadOnlyList<AssetItem> assets) { _root = root; Database = database; Repository = repository; Assets = assets; }
        public AssetLibraryDatabase Database { get; }
        public SqliteAssetLibraryRepository Repository { get; }
        public IReadOnlyList<AssetItem> Assets { get; }
        public static async Task<Setup> CreateAsync(int count)
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-V16", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var database = new AssetLibraryDatabase(Path.Combine(root, "library.db")); var repository = new SqliteAssetLibraryRepository(database); await repository.InitializeAsync();
            var paths = new List<string>();
            for (var index = 0; index < count; index++) { var path = Path.Combine(root, $"asset-{index:000}.jpg"); var bytes = SHA256.HashData(BitConverter.GetBytes(index)); await File.WriteAllBytesAsync(path, bytes); paths.Add(path); }
            await repository.ImportAsync(paths.Select(path => new AssetImportRequest(path, ComputeContentHash: true)));
            var assets = (await repository.QueryAsync(new(PageSize: 500))).Items.OrderBy(asset => asset.DisplayName).ToArray();
            return new(root, database, repository, assets);
        }
        public async ValueTask DisposeAsync() { await Repository.DisposeAsync(); try { Directory.Delete(_root, true); } catch { } }
    }
}
