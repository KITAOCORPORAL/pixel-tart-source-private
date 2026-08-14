using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

public sealed class SqliteVisualAssetQueryService(AssetLibraryDatabase database, IAssetVisualFeatureStore featureStore) : IVisualAssetQueryService
{
    private readonly AssetLibraryDatabase _database = database;
    private int _initialized;

    private const string AssetProjection = """
        a.AssetId,a.SourcePath,a.DisplayName,a.Extension,a.MediaType,a.FileSize,a.ContentHash,a.Width,a.Height,a.Orientation,
        a.CaptureTime,a.AddedAt,a.ModifiedAt,a.Rating,a.Comment,a.IsMissing,a.IsArchived,a.ImportMode,a.ManagedCopyPath
        """;

    private const string FeatureProjection = """
        CASE
            WHEN f.AssetId IS NULL THEN CASE WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures anyf WHERE anyf.AssetId=a.AssetId) THEN 'Stale' ELSE 'NotAnalyzed' END
            WHEN f.SourceContentHash IS NULL OR a.ContentHash IS NULL OR f.SourceContentHash<>a.ContentHash THEN 'Stale'
            WHEN f.Outcome='Succeeded' THEN 'Valid'
            WHEN f.Outcome='Failed' THEN 'Failed'
            ELSE 'Stale'
        END AS DerivedState,
        f.AnalysisVersion,f.ContentFingerprint,f.SourceContentHash,f.Outcome,f.FailureReason,f.AnalysisSource,f.SourceProfile,f.AnalysisProfile,
        f.Harmony,f.ToneKey,f.Contrast,f.LuminanceSpan,f.Saturation,f.WarmCool,f.DominantHue,f.SecondaryHue,f.AverageHue,
        f.AverageLuma,f.MedianLuma,f.ContrastMetric,f.LumaSpreadMetric,f.AverageSaturation,f.MedianSaturation,f.AverageLightness,
        f.WarmCoolMetric,f.DeepShadowRatio,f.ShadowRatio,f.MidtoneRatio,f.HighlightRatio,f.SpecularRatio,f.BlackClipRatio,
        f.WhiteClipRatio,f.HistogramLumaSignature,f.PaletteSignature,f.CreatedAt,f.UpdatedAt,f.ResultJson
        """;

    private const string CurrentFeatureJoin = "LEFT JOIN AssetVisualFeatures f ON f.AssetId=a.AssetId AND f.AnalysisVersion=$visualVersion";
    private const string ValidFeaturePredicate = "f.AssetId IS NOT NULL AND f.Outcome='Succeeded' AND f.SourceContentHash IS NOT NULL AND a.ContentHash IS NOT NULL AND f.SourceContentHash=a.ContentHash";

    public Task<AssetVisualFeatures> GetFeaturesAsync(Guid assetId, CancellationToken cancellationToken = default) => featureStore.GetFeaturesAsync(assetId, cancellationToken);

    public async Task<VisualAssetPage> QueryAsync(VisualAssetQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        ValidateScope(query.Scope);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        await using var count = connection.CreateCommand();
        count.Parameters.AddWithValue("$visualVersion", AssetVisualFeatureContract.AnalysisVersion);
        var countWhere = BuildWhere(query.Scope, query.Filter, count);
        count.CommandText = $"SELECT COUNT(*) FROM AssetItems a {CurrentFeatureJoin} WHERE {countWhere};";
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);

        await using var page = connection.CreateCommand();
        page.Parameters.AddWithValue("$visualVersion", AssetVisualFeatureContract.AnalysisVersion);
        var pageWhere = BuildWhere(query.Scope, query.Filter, page);
        if (TryParseCursor(query.Cursor, out var addedAt, out var assetId))
        {
            pageWhere += " AND (a.AddedAt<$cursorAdded OR (a.AddedAt=$cursorAdded AND a.AssetId>$cursorAsset))";
            page.Parameters.AddWithValue("$cursorAdded", addedAt.ToString("O")); page.Parameters.AddWithValue("$cursorAsset", assetId.ToString("D"));
        }
        page.CommandText = $"SELECT {AssetProjection},{FeatureProjection} FROM AssetItems a {CurrentFeatureJoin} WHERE {pageWhere} ORDER BY a.AddedAt DESC,a.AssetId LIMIT $limit;";
        page.Parameters.AddWithValue("$limit", query.EffectivePageSize + 1);
        var items = new List<VisualAssetMatch>(query.EffectivePageSize + 1);
        await using (var reader = await page.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadMatch(reader));
        var hasMore = items.Count > query.EffectivePageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        if (query.Filter.PaletteColor is not null && items.Count > 0)
            items = (await AttachColorDistancesAsync(connection, items, query.Filter.PaletteColor.Value, query.Filter.MinimumPaletteWeight, cancellationToken).ConfigureAwait(false)).ToList();
        return new(items, hasMore && items.Count > 0 ? CreateCursor(items[^1].Asset) : null, total);
    }

    public async Task<IReadOnlyList<VisualAssetMatch>> SearchByColorAsync(VisualAssetQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Filter.PaletteColor is null) throw new ArgumentException("PaletteColor is required for color search.", nameof(query));
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        ValidateScope(query.Scope);
        var target = query.Filter.PaletteColor.Value; var maximum = Math.Max(0, query.Filter.MaximumDeltaE);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var candidates = connection.CreateCommand(); candidates.Parameters.AddWithValue("$visualVersion", AssetVisualFeatureContract.AnalysisVersion);
        var scope = BuildScopeWhere(query.Scope, candidates);
        candidates.CommandText = $"""
            SELECT a.AssetId,MIN((pc.LabL-$targetL)*(pc.LabL-$targetL)+(pc.LabA-$targetA)*(pc.LabA-$targetA)+(pc.LabB-$targetB)*(pc.LabB-$targetB)) AS DistanceSquared
            FROM AssetItems a JOIN AssetVisualFeatures f ON f.AssetId=a.AssetId AND f.AnalysisVersion=$visualVersion
            JOIN AssetVisualPaletteColors pc ON pc.AssetId=f.AssetId AND pc.AnalysisVersion=f.AnalysisVersion
            WHERE {scope} AND {ValidFeaturePredicate} AND pc.Weight>=$weight
              AND ((pc.LabL-$targetL)*(pc.LabL-$targetL)+(pc.LabA-$targetA)*(pc.LabA-$targetA)+(pc.LabB-$targetB)*(pc.LabB-$targetB))<=$maximumSquared
            GROUP BY a.AssetId ORDER BY DistanceSquared,a.AssetId LIMIT $candidateLimit;
            """;
        candidates.Parameters.AddWithValue("$targetL", target.L); candidates.Parameters.AddWithValue("$targetA", target.A); candidates.Parameters.AddWithValue("$targetB", target.B); candidates.Parameters.AddWithValue("$weight", Math.Clamp(query.Filter.MinimumPaletteWeight, 0, 1)); candidates.Parameters.AddWithValue("$maximumSquared", maximum * maximum); candidates.Parameters.AddWithValue("$candidateLimit", AssetVisualFeatureContract.CandidatePoolLimit);
        var ids = new List<Guid>();
        await using (var reader = await candidates.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)) while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) ids.Add(Guid.Parse(reader.GetString(0)));
        if (ids.Count == 0) return [];
        await using var detail = connection.CreateCommand(); detail.Parameters.AddWithValue("$visualVersion", AssetVisualFeatureContract.AnalysisVersion);
        var names = new List<string>(ids.Count);
        for (var index = 0; index < ids.Count; index++) { var name = $"$candidate{index}"; names.Add(name); detail.Parameters.AddWithValue(name, ids[index].ToString("D")); }
        detail.CommandText = $"SELECT {AssetProjection},{FeatureProjection} FROM AssetItems a {CurrentFeatureJoin} WHERE a.AssetId IN ({string.Join(',', names)}) AND {ValidFeaturePredicate};";
        var matches = new List<VisualAssetMatch>(ids.Count);
        await using (var reader = await detail.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)) while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) matches.Add(ReadMatch(reader));
        var withDistances = await AttachColorDistancesAsync(connection, matches, target, query.Filter.MinimumPaletteWeight, cancellationToken).ConfigureAwait(false);
        return withDistances.OrderBy(item => item.ColorDeltaE).ThenBy(item => item.Asset.AssetId).Take(Math.Min(query.EffectivePageSize, AssetVisualFeatureContract.ResultLimit)).ToArray();
    }

    public async Task<IReadOnlyList<VisualSimilarityMatch>> FindSimilarAsync(VisualSimilarityQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        ValidateScope(query.Scope);
        var referenceFeatures = await featureStore.GetFeaturesAsync(query.ReferenceAssetId, cancellationToken).ConfigureAwait(false);
        if (referenceFeatures.Summary.State != AssetVisualFeatureState.Valid || referenceFeatures.Analysis is null)
            return [];

        var reference = referenceFeatures.Analysis;
        await using var connection = await _database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (query.Mode == VisualSimilarityMode.Palette)
        {
            var paletteCandidates = await LoadPaletteCandidatesAsync(connection, reference, query, cancellationToken).ConfigureAwait(false);
            return paletteCandidates
                .Select(candidate => new VisualSimilarityMatch(candidate.Match.Asset, candidate.Match.Features, VisualSimilarityScorer.Score(reference, candidate.Result)))
                .OrderByDescending(match => match.Scores.PaletteComponent)
                .ThenBy(match => match.Asset.AssetId)
                .Take(query.EffectiveLimit)
                .ToArray();
        }
        await using var command = connection.CreateCommand(); command.Parameters.AddWithValue("$visualVersion", AssetVisualFeatureContract.AnalysisVersion);
        var where = BuildScopeWhere(query.Scope, command);
        where += " AND " + ValidFeaturePredicate + " AND a.AssetId<>$reference";
        command.Parameters.AddWithValue("$reference", query.ReferenceAssetId.ToString("D"));
        command.Parameters.AddWithValue("$minLuma", Math.Max(0, reference.AverageLuma - 112)); command.Parameters.AddWithValue("$maxLuma", Math.Min(255, reference.AverageLuma + 112));
        command.Parameters.AddWithValue("$minContrast", Math.Max(0, reference.ContrastMetric - .70)); command.Parameters.AddWithValue("$maxContrast", Math.Min(1, reference.ContrastMetric + .70));
        command.Parameters.AddWithValue("$minSaturation", Math.Max(0, reference.AverageSaturation - .80)); command.Parameters.AddWithValue("$maxSaturation", Math.Min(1, reference.AverageSaturation + .80));
        command.Parameters.AddWithValue("$luma", reference.AverageLuma); command.Parameters.AddWithValue("$contrast", reference.ContrastMetric); command.Parameters.AddWithValue("$saturation", reference.AverageSaturation);
        command.Parameters.AddWithValue("$warmCoolMetric", reference.WarmCoolMetric);
        where += " AND f.AverageLuma BETWEEN $minLuma AND $maxLuma AND f.ContrastMetric BETWEEN $minContrast AND $maxContrast AND f.AverageSaturation BETWEEN $minSaturation AND $maxSaturation";
        command.Parameters.AddWithValue("$toneKey", reference.ToneKey.ToString()); command.Parameters.AddWithValue("$warmCool", reference.WarmCool.ToString()); command.Parameters.AddWithValue("$saturationClass", reference.Saturation.ToString());
        string fourthBranch;
        if (reference.HasDominantChromaticColor)
        {
            command.Parameters.AddWithValue("$hueStart", NormalizeHue(reference.DominantHue - 75)); command.Parameters.AddWithValue("$hueEnd", NormalizeHue(reference.DominantHue + 75)); command.Parameters.AddWithValue("$referenceHue", reference.DominantHue);
            var hueCondition = NormalizeHue(reference.DominantHue - 75) <= NormalizeHue(reference.DominantHue + 75) ? "f.DominantHue BETWEEN $hueStart AND $hueEnd" : "(f.DominantHue>=$hueStart OR f.DominantHue<=$hueEnd)";
            var hueDistance = "MIN(abs(f.DominantHue-$referenceHue),360-abs(f.DominantHue-$referenceHue))";
            fourthBranch = $"UNION SELECT AssetId FROM ({{0}} AND {hueCondition} ORDER BY {hueDistance},a.AssetId LIMIT $branchLimit)";
        }
        else fourthBranch = "UNION SELECT AssetId FROM ({0} AND f.Saturation=$saturationClass ORDER BY abs(f.AverageSaturation-$saturation),a.AssetId LIMIT $branchLimit)";
        var scopedProjection = $"SELECT a.AssetId FROM AssetItems a {CurrentFeatureJoin} WHERE {where}";
        command.CommandText = string.Format(CultureInfo.InvariantCulture, $"""
            WITH CandidateIds AS (
                SELECT AssetId FROM ({scopedProjection} AND f.AverageLuma BETWEEN $minLuma AND $maxLuma ORDER BY abs(f.AverageLuma-$luma),a.AssetId LIMIT $branchLimit)
                UNION SELECT AssetId FROM ({scopedProjection} AND f.ToneKey=$toneKey ORDER BY (abs(f.AverageLuma-$luma)/255.0 + abs(f.ContrastMetric-$contrast) + abs(f.AverageSaturation-$saturation)),a.AssetId LIMIT $branchLimit)
                UNION SELECT AssetId FROM ({scopedProjection} AND f.WarmCool=$warmCool ORDER BY abs(f.WarmCoolMetric-$warmCoolMetric),a.AssetId LIMIT $branchLimit)
                {fourthBranch}
            )
            SELECT {AssetProjection},{FeatureProjection}
            FROM CandidateIds c JOIN AssetItems a ON a.AssetId=c.AssetId {CurrentFeatureJoin}
            ORDER BY (abs(f.AverageLuma-$luma)/255.0 + abs(f.ContrastMetric-$contrast) + abs(f.AverageSaturation-$saturation)),a.AssetId
            LIMIT $candidateLimit;
            """, scopedProjection);
        command.Parameters.AddWithValue("$branchLimit", AssetVisualFeatureContract.CandidatePoolLimit / 4);
        command.Parameters.AddWithValue("$candidateLimit", AssetVisualFeatureContract.CandidatePoolLimit);
        var candidates = new List<(VisualAssetMatch Match, AssetVisualAnalysisResult Result)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.IsDBNull(56)) continue;
                var result = JsonSerializer.Deserialize<AssetVisualAnalysisResult>(reader.GetString(56));
                if (result is not null) candidates.Add((ReadMatch(reader), result));
            }
        }
        return candidates
            .Select(candidate => new VisualSimilarityMatch(candidate.Match.Asset, candidate.Match.Features, VisualSimilarityScorer.Score(reference, candidate.Result)))
            .OrderByDescending(match => query.Mode == VisualSimilarityMode.Palette ? match.Scores.PaletteComponent : match.Scores.Overall)
            .ThenBy(match => match.Asset.AssetId)
            .Take(query.EffectiveLimit)
            .ToArray();
    }

    private async Task<List<(VisualAssetMatch Match, AssetVisualAnalysisResult Result)>> LoadPaletteCandidatesAsync(
        SqliteConnection connection,
        AssetVisualAnalysisResult reference,
        VisualSimilarityQuery query,
        CancellationToken cancellationToken)
    {
        var palette = reference.Palette.OrderByDescending(color => color.Weight).Take(AssetVisualFeatureContract.PaletteSize).ToArray();
        if (palette.Length == 0) return [];
        await using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$visualVersion", AssetVisualFeatureContract.AnalysisVersion);
        command.Parameters.AddWithValue("$reference", query.ReferenceAssetId.ToString("D"));
        command.Parameters.AddWithValue("$candidateLimit", AssetVisualFeatureContract.CandidatePoolLimit);
        var where = BuildScopeWhere(query.Scope, command) + " AND " + ValidFeaturePredicate + " AND a.AssetId<>$reference";
        var referenceRows = new List<string>(palette.Length);
        for (var index = 0; index < palette.Length; index++)
        {
            var color = palette[index]; var prefix = "$paletteReference" + index;
            referenceRows.Add($"({index},{prefix}L,{prefix}A,{prefix}B,{prefix}Weight)");
            command.Parameters.AddWithValue(prefix + "L", color.Lab.L); command.Parameters.AddWithValue(prefix + "A", color.Lab.A); command.Parameters.AddWithValue(prefix + "B", color.Lab.B); command.Parameters.AddWithValue(prefix + "Weight", color.Weight);
        }
        command.CommandText = $"""
            WITH ReferenceColors(ColorIndex,LabL,LabA,LabB,Weight) AS (VALUES {string.Join(',', referenceRows)}),
            CandidateIds AS (
                SELECT a.AssetId,
                    MIN((pc.LabL-r.LabL)*(pc.LabL-r.LabL)+(pc.LabA-r.LabA)*(pc.LabA-r.LabA)+(pc.LabB-r.LabB)*(pc.LabB-r.LabB)+400*abs(pc.Weight-r.Weight)) AS ApproximateDistance
                FROM AssetItems a {CurrentFeatureJoin}
                JOIN AssetVisualPaletteColors pc ON pc.AssetId=f.AssetId AND pc.AnalysisVersion=f.AnalysisVersion
                CROSS JOIN ReferenceColors r
                WHERE {where}
                GROUP BY a.AssetId
                ORDER BY ApproximateDistance,a.AssetId
                LIMIT $candidateLimit
            )
            SELECT {AssetProjection},{FeatureProjection}
            FROM CandidateIds c JOIN AssetItems a ON a.AssetId=c.AssetId {CurrentFeatureJoin}
            ORDER BY c.ApproximateDistance,a.AssetId;
            """;
        var candidates = new List<(VisualAssetMatch Match, AssetVisualAnalysisResult Result)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.IsDBNull(56)) continue;
            var result = JsonSerializer.Deserialize<AssetVisualAnalysisResult>(reader.GetString(56));
            if (result is not null) candidates.Add((ReadMatch(reader), result));
        }
        return candidates;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _initialized) != 0) return;
        await using var connection = await _database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await AssetLibrarySchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _initialized, 1);
    }

    private static string BuildWhere(AssetLibraryQuery scope, VisualAssetFilter filter, SqliteCommand command)
    {
        var where = BuildScopeWhere(scope, command);
        var state = "CASE WHEN f.AssetId IS NULL THEN CASE WHEN EXISTS(SELECT 1 FROM AssetVisualFeatures anyf WHERE anyf.AssetId=a.AssetId) THEN 'Stale' ELSE 'NotAnalyzed' END WHEN f.SourceContentHash IS NULL OR a.ContentHash IS NULL OR f.SourceContentHash<>a.ContentHash THEN 'Stale' WHEN f.Outcome='Succeeded' THEN 'Valid' WHEN f.Outcome='Failed' THEN 'Failed' ELSE 'Stale' END";
        if (filter.State is not null) { where += " AND " + state + "=$featureState"; command.Parameters.AddWithValue("$featureState", filter.State.Value.ToString()); }
        var needsValid = filter.DominantHue is not null || filter.Harmony is not null || filter.ToneKey is not null || filter.Contrast is not null || filter.Saturation is not null || filter.WarmCool is not null ||
            filter.MinimumAverageLuma is not null || filter.MaximumAverageLuma is not null || filter.MinimumContrast is not null || filter.MaximumContrast is not null || filter.MinimumAverageSaturation is not null || filter.MaximumAverageSaturation is not null ||
            filter.MinimumMedianSaturation is not null || filter.MaximumMedianSaturation is not null || filter.MinimumLumaSpread is not null || filter.MaximumLumaSpread is not null || filter.MinimumShadowRatio is not null || filter.MinimumHighlightRatio is not null ||
            filter.MaximumBlackClipRatio is not null || filter.MaximumWhiteClipRatio is not null || filter.MinimumWarmCoolMetric is not null || filter.MaximumWarmCoolMetric is not null || filter.PaletteColor is not null;
        if (needsValid) where += " AND " + ValidFeaturePredicate;
        AddEnum(ref where, command, "f.Harmony", "$harmony", filter.Harmony);
        AddEnum(ref where, command, "f.ToneKey", "$toneKey", filter.ToneKey);
        AddEnum(ref where, command, "f.Contrast", "$visualContrast", filter.Contrast);
        AddEnum(ref where, command, "f.Saturation", "$visualSaturation", filter.Saturation);
        AddEnum(ref where, command, "f.WarmCool", "$warmCool", filter.WarmCool);
        AddMinimum(ref where, command, "f.AverageLuma", "$minLuma", filter.MinimumAverageLuma); AddMaximum(ref where, command, "f.AverageLuma", "$maxLuma", filter.MaximumAverageLuma);
        AddMinimum(ref where, command, "f.ContrastMetric", "$minContrast", filter.MinimumContrast); AddMaximum(ref where, command, "f.ContrastMetric", "$maxContrast", filter.MaximumContrast);
        AddMinimum(ref where, command, "f.AverageSaturation", "$minSaturation", filter.MinimumAverageSaturation); AddMaximum(ref where, command, "f.AverageSaturation", "$maxSaturation", filter.MaximumAverageSaturation);
        AddMinimum(ref where, command, "f.MedianSaturation", "$minMedianSaturation", filter.MinimumMedianSaturation); AddMaximum(ref where, command, "f.MedianSaturation", "$maxMedianSaturation", filter.MaximumMedianSaturation);
        AddMinimum(ref where, command, "f.LumaSpreadMetric", "$minSpread", filter.MinimumLumaSpread); AddMaximum(ref where, command, "f.LumaSpreadMetric", "$maxSpread", filter.MaximumLumaSpread);
        AddMinimum(ref where, command, "f.ShadowRatio", "$minShadow", filter.MinimumShadowRatio); AddMinimum(ref where, command, "f.HighlightRatio", "$minHighlight", filter.MinimumHighlightRatio);
        AddMaximum(ref where, command, "f.BlackClipRatio", "$maxBlackClip", filter.MaximumBlackClipRatio); AddMaximum(ref where, command, "f.WhiteClipRatio", "$maxWhiteClip", filter.MaximumWhiteClipRatio);
        AddMinimum(ref where, command, "f.WarmCoolMetric", "$minWarmCool", filter.MinimumWarmCoolMetric); AddMaximum(ref where, command, "f.WarmCoolMetric", "$maxWarmCool", filter.MaximumWarmCoolMetric);
        if (filter.DominantHue is { } hue)
        {
            command.Parameters.AddWithValue("$hueStart", hue.Start); command.Parameters.AddWithValue("$hueEnd", hue.End);
            var condition = hue.CrossesZero ? "(pc.Hue>=$hueStart OR pc.Hue<=$hueEnd)" : "pc.Hue BETWEEN $hueStart AND $hueEnd";
            where += " AND EXISTS(SELECT 1 FROM AssetVisualPaletteColors pc WHERE pc.AssetId=a.AssetId AND pc.AnalysisVersion=$visualVersion AND pc.Weight>=0.15 AND pc.Saturation>=0.08 AND pc.Chroma>=8 AND " + condition + ")";
        }
        if (filter.PaletteColor is { } target)
        {
            var max = Math.Max(0, filter.MaximumDeltaE); var minWeight = Math.Clamp(filter.MinimumPaletteWeight, 0, 1);
            command.Parameters.AddWithValue("$targetL", target.L); command.Parameters.AddWithValue("$targetA", target.A); command.Parameters.AddWithValue("$targetB", target.B); command.Parameters.AddWithValue("$maxDeltaSquared", max * max); command.Parameters.AddWithValue("$minimumPaletteWeight", minWeight);
            where += " AND EXISTS(SELECT 1 FROM AssetVisualPaletteColors pc WHERE pc.AssetId=a.AssetId AND pc.AnalysisVersion=$visualVersion AND pc.Weight>=$minimumPaletteWeight AND ((pc.LabL-$targetL)*(pc.LabL-$targetL)+(pc.LabA-$targetA)*(pc.LabA-$targetA)+(pc.LabB-$targetB)*(pc.LabB-$targetB))<=$maxDeltaSquared)";
        }
        return where;
    }

    private static string BuildScopeWhere(AssetLibraryQuery query, SqliteCommand command)
    {
        var where = new List<string> { query.IncludeArchived ? "1=1" : "a.IsArchived=0" };
        if (!string.IsNullOrWhiteSpace(query.SearchText)) { where.Add("(a.DisplayName LIKE $search OR a.Comment LIKE $search OR EXISTS(SELECT 1 FROM AssetTagMemberships sm JOIN AssetTags st ON st.TagId=sm.TagId WHERE sm.AssetId=a.AssetId AND st.Name LIKE $search) OR EXISTS(SELECT 1 FROM AssetFolderMemberships sfm JOIN AssetFolders sf ON sf.FolderId=sfm.FolderId WHERE sfm.AssetId=a.AssetId AND sf.Name LIKE $search))"); command.Parameters.AddWithValue("$search", "%" + query.SearchText.Trim() + "%"); }
        if (query.FolderId is not null) { where.Add("EXISTS(SELECT 1 FROM AssetFolderMemberships fm WHERE fm.AssetId=a.AssetId AND fm.FolderId=$folder)"); command.Parameters.AddWithValue("$folder", query.FolderId.Value.ToString("D")); }
        if (query.TagId is not null) { where.Add("EXISTS(SELECT 1 FROM AssetTagMemberships tm WHERE tm.AssetId=a.AssetId AND tm.TagId=$tag)"); command.Parameters.AddWithValue("$tag", query.TagId.Value.ToString("D")); }
        AddMemberships(where, command, query.FolderIds, "AssetFolderMemberships", "FolderId", "vf"); AddMemberships(where, command, query.TagIds, "AssetTagMemberships", "TagId", "vt");
        if (query.MinimumRating is not null) { where.Add("a.Rating>=$minRating"); command.Parameters.AddWithValue("$minRating", query.MinimumRating.Value); }
        if (query.MaximumRating is not null) { where.Add("a.Rating<=$maxRating"); command.Parameters.AddWithValue("$maxRating", query.MaximumRating.Value); }
        if (!string.IsNullOrWhiteSpace(query.MediaType)) { where.Add("a.MediaType=$mediaType"); command.Parameters.AddWithValue("$mediaType", query.MediaType); }
        if (!string.IsNullOrWhiteSpace(query.Extension)) { where.Add("a.Extension=$extension"); command.Parameters.AddWithValue("$extension", query.Extension.StartsWith('.') ? query.Extension : "." + query.Extension); }
        if (query.UncategorizedOnly) where.Add("NOT EXISTS(SELECT 1 FROM AssetFolderMemberships uf WHERE uf.AssetId=a.AssetId)");
        if (query.UntaggedOnly) where.Add("NOT EXISTS(SELECT 1 FROM AssetTagMemberships ut WHERE ut.AssetId=a.AssetId)");
        if (query.MissingOnly) where.Add("a.IsMissing=1");
        if (query.AddedFrom is not null) { where.Add("a.AddedAt>=$addedFrom"); command.Parameters.AddWithValue("$addedFrom", query.AddedFrom.Value.ToString("O")); }
        if (query.AddedTo is not null) { where.Add("a.AddedAt<=$addedTo"); command.Parameters.AddWithValue("$addedTo", query.AddedTo.Value.ToString("O")); }
        if (query.CaptureFrom is not null) { where.Add("a.CaptureTime>=$captureFrom"); command.Parameters.AddWithValue("$captureFrom", query.CaptureFrom.Value.ToString("O")); }
        if (query.CaptureTo is not null) { where.Add("a.CaptureTime<=$captureTo"); command.Parameters.AddWithValue("$captureTo", query.CaptureTo.Value.ToString("O")); }
        return string.Join(" AND ", where);
    }

    private static void AddMemberships(List<string> where, SqliteCommand command, IReadOnlyList<Guid>? ids, string table, string column, string prefix)
    {
        if (ids is null) return;
        foreach (var id in ids.Distinct()) { var name = $"${prefix}{command.Parameters.Count}"; where.Add($"EXISTS(SELECT 1 FROM {table} mx WHERE mx.AssetId=a.AssetId AND mx.{column}={name})"); command.Parameters.AddWithValue(name, id.ToString("D")); }
    }

    private static void ValidateScope(AssetLibraryQuery scope)
    {
        if (!string.IsNullOrWhiteSpace(scope.FileNameRegex)) throw new NotSupportedException("Visual query scope does not accept regex; resolve the regex before entering temporary visual results.");
        if (scope.SmartFolderId is not null) throw new NotSupportedException("Visual query scope does not nest a saved Smart Folder; visual Smart rules are compiled directly by the repository.");
    }

    private static void AddEnum<T>(ref string where, SqliteCommand command, string column, string parameter, T? value) where T : struct, Enum { if (value is null) return; where += $" AND {column}={parameter}"; command.Parameters.AddWithValue(parameter, value.Value.ToString()); }
    private static void AddMinimum(ref string where, SqliteCommand command, string column, string parameter, double? value) { if (value is null) return; where += $" AND {column}>={parameter}"; command.Parameters.AddWithValue(parameter, value.Value); }
    private static void AddMaximum(ref string where, SqliteCommand command, string column, string parameter, double? value) { if (value is null) return; where += $" AND {column}<={parameter}"; command.Parameters.AddWithValue(parameter, value.Value); }

    private static VisualAssetMatch ReadMatch(SqliteDataReader reader)
    {
        var asset = new AssetItem(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt64(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetInt32(7), reader.IsDBNull(8) ? null : reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : DateTimeOffset.Parse(reader.GetString(10)), DateTimeOffset.Parse(reader.GetString(11)), DateTimeOffset.Parse(reader.GetString(12)), reader.GetInt32(13), reader.GetString(14), reader.GetInt32(15) != 0, reader.GetInt32(16) != 0, Enum.TryParse<AssetImportMode>(reader.GetString(17), true, out var mode) ? mode : AssetImportMode.Reference, reader.IsDBNull(18) ? null : reader.GetString(18));
        var state = Enum.TryParse<AssetVisualFeatureState>(reader.GetString(19), true, out var parsed) ? parsed : AssetVisualFeatureState.Stale;
        if (reader.IsDBNull(20)) return new(asset, SqliteAssetVisualAnalysisCache.NotAnalyzed(asset.AssetId) with { State = state });
        double? Number(int index) => reader.IsDBNull(index) ? null : reader.GetDouble(index);
        T? EnumValue<T>(int index) where T : struct, Enum => !reader.IsDBNull(index) && Enum.TryParse<T>(reader.GetString(index), true, out var value) ? value : null;
        var summary = new AssetVisualFeatureSummary
        {
            AssetId = asset.AssetId, State = state, AnalysisVersion = reader.GetString(20), ContentFingerprint = reader.GetString(21), SourceContentHash = reader.IsDBNull(22) ? null : reader.GetString(22), FailureReason = reader.IsDBNull(24) ? null : reader.GetString(24),
            AnalysisSource = EnumValue<VisualAnalysisSourceKind>(25) ?? VisualAnalysisSourceKind.RasterOriginal, SourceProfile = reader.GetString(26), AnalysisProfile = reader.GetString(27), Harmony = EnumValue<ColorHarmonyTendency>(28), ToneKey = EnumValue<ToneKeyTendency>(29), Contrast = EnumValue<ContrastTendency>(30), LuminanceSpan = EnumValue<LuminanceSpanTendency>(31), Saturation = EnumValue<SaturationTendency>(32), WarmCool = EnumValue<WarmCoolTendency>(33),
            DominantHue = Number(34), SecondaryHue = Number(35), AverageHue = Number(36), AverageLuma = Number(37), MedianLuma = Number(38), ContrastMetric = Number(39), LumaSpreadMetric = Number(40), AverageSaturation = Number(41), MedianSaturation = Number(42), AverageLightness = Number(43), WarmCoolMetric = Number(44), DeepShadowRatio = Number(45), ShadowRatio = Number(46), MidtoneRatio = Number(47), HighlightRatio = Number(48), SpecularRatio = Number(49), BlackClipRatio = Number(50), WhiteClipRatio = Number(51), HistogramLumaSignature = reader.IsDBNull(52) ? null : reader.GetString(52), PaletteSignature = reader.IsDBNull(53) ? null : reader.GetString(53), CreatedAt = DateTimeOffset.Parse(reader.GetString(54)), UpdatedAt = DateTimeOffset.Parse(reader.GetString(55))
        };
        return new(asset, summary);
    }

    private static async Task<IReadOnlyList<VisualAssetMatch>> AttachColorDistancesAsync(SqliteConnection connection, IReadOnlyList<VisualAssetMatch> matches, VisualLab target, double minimumWeight, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var parameters = new List<string>(matches.Count);
        for (var index = 0; index < matches.Count; index++) { var name = $"$asset{index}"; parameters.Add(name); command.Parameters.AddWithValue(name, matches[index].Asset.AssetId.ToString("D")); }
        command.CommandText = $"SELECT AssetId,LabL,LabA,LabB FROM AssetVisualPaletteColors WHERE AnalysisVersion=$version AND Weight>=$weight AND AssetId IN ({string.Join(',', parameters)});";
        command.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion); command.Parameters.AddWithValue("$weight", Math.Clamp(minimumWeight, 0, 1));
        var distances = new Dictionary<Guid, double>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { var id = Guid.Parse(reader.GetString(0)); var distance = VisualAnalysisEngine.DeltaE(target, new(reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3))); if (!distances.TryGetValue(id, out var current) || distance < current) distances[id] = distance; }
        return matches.Select(match => match with { ColorDeltaE = distances.GetValueOrDefault(match.Asset.AssetId, double.MaxValue) }).ToArray();
    }

    private static string CreateCursor(AssetItem item) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{item.AddedAt:O}|{item.AssetId:D}"));
    private static double NormalizeHue(double value) => (value % 360 + 360) % 360;
    private static bool TryParseCursor(string? cursor, out DateTimeOffset addedAt, out Guid assetId)
    {
        addedAt = default; assetId = default; if (string.IsNullOrWhiteSpace(cursor)) return false;
        try { var value = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)); var separator = value.LastIndexOf('|'); return separator > 0 && DateTimeOffset.TryParse(value[..separator], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out addedAt) && Guid.TryParse(value[(separator + 1)..], out assetId); }
        catch (FormatException) { return false; }
    }
}

public static class VisualSimilarityScorer
{
    public static double ScorePalette(IReadOnlyList<DominantColor> left, IReadOnlyList<DominantColor> right) => Round(PaletteSimilarity(left, right));

    public static VisualSimilarityScores Score(AssetVisualAnalysisResult left, AssetVisualAnalysisResult right, VisualSimilarityProfile? profile = null)
    {
        var palette = PaletteSimilarity(left.Palette, right.Palette);
        var leftChromatic = left.HasDominantChromaticColor; var rightChromatic = right.HasDominantChromaticColor;
        var hue = !leftChromatic && !rightChromatic ? 100 : leftChromatic != rightChromatic ? 25 : 100 * (1 - AngularDistance(left.DominantHue, right.DominantHue) / 180);
        var warmCool = 100 * (1 - Math.Clamp(Math.Abs(left.WarmCoolMetric - right.WarmCoolMetric) / 2, 0, 1));
        var color = .65 * palette + .20 * hue + .15 * warmCool;
        var histogram = HistogramIntersection(left.HistogramLuma, right.HistogramLuma);
        var toneZoneDistance = Math.Abs(left.ToneZones.DeepShadow - right.ToneZones.DeepShadow) + Math.Abs(left.ToneZones.Shadow - right.ToneZones.Shadow) + Math.Abs(left.ToneZones.Midtone - right.ToneZones.Midtone) + Math.Abs(left.ToneZones.Highlight - right.ToneZones.Highlight) + Math.Abs(left.ToneZones.Specular - right.ToneZones.Specular);
        var zones = 100 * (1 - Math.Clamp(toneZoneDistance / 2, 0, 1));
        var key = 100 * (1 - Math.Abs((int)left.ToneKey - (int)right.ToneKey) / 2d);
        var tone = .55 * histogram + .30 * zones + .15 * key;
        var contrast = 100 * (1 - Math.Clamp(.7 * Math.Abs(left.ContrastMetric - right.ContrastMetric) + .3 * Math.Abs(left.LuminanceSpanMetric - right.LuminanceSpanMetric), 0, 1));
        var saturation = 100 * (1 - Math.Clamp(.7 * Math.Abs(left.AverageSaturation - right.AverageSaturation) + .3 * Math.Abs(left.MedianSaturation - right.MedianSaturation), 0, 1));
        var weights = (profile ?? VisualSimilarityProfile.Default).Normalize();
        var overall = weights.ColorWeight * color + weights.ToneWeight * tone + weights.ContrastWeight * contrast + weights.SaturationWeight * saturation;
        return new(Round(color), Round(tone), Round(contrast), Round(saturation), Round(overall), Round(palette), Round(histogram));
    }

    private static double PaletteSimilarity(IReadOnlyList<DominantColor> left, IReadOnlyList<DominantColor> right)
    {
        if (left.Count == 0 || right.Count == 0) return 0;
        static double Direction(IReadOnlyList<DominantColor> source, IReadOnlyList<DominantColor> target)
        {
            var total = source.Sum(color => color.Weight); if (total <= 0) return 0;
            var weightedDistance = source.Sum(color => color.Weight * target.Min(other => VisualAnalysisEngine.DeltaE(color.Lab, other.Lab)));
            var weightMismatch = source.Sum(color => Math.Abs(color.Weight - target.MinBy(other => VisualAnalysisEngine.DeltaE(color.Lab, other.Lab))!.Weight)) / total;
            var colorScore = 1 - Math.Clamp(weightedDistance / total / 100, 0, 1);
            return 100 * colorScore * (1 - .5 * Math.Clamp(weightMismatch, 0, 1));
        }
        return (Direction(left, right) + Direction(right, left)) / 2;
    }

    private static double HistogramIntersection(uint[] left, uint[] right)
    {
        var leftTotal = left.Sum(value => (double)value); var rightTotal = right.Sum(value => (double)value);
        if (leftTotal <= 0 || rightTotal <= 0) return 0;
        double intersection = 0;
        for (var index = 0; index < Math.Min(left.Length, right.Length); index++) intersection += Math.Min(left[index] / leftTotal, right[index] / rightTotal);
        return 100 * Math.Clamp(intersection, 0, 1);
    }

    private static double Round(double value) => Math.Round(Math.Clamp(value, 0, 100), 3, MidpointRounding.AwayFromZero);
    private static double AngularDistance(double left, double right) { var distance = Math.Abs(left - right) % 360; return distance > 180 ? 360 - distance : distance; }
}
