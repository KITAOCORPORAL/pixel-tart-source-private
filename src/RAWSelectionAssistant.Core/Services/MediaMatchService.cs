using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class MediaMatchService
{
    private readonly FileNameNormalizer _normalizer;
    private readonly IJpegMetadataService _jpegMetadataService;
    private readonly JpegQualityAssessmentService _jpegAssessmentService;
    private readonly IFeatureGateService? _featureGateService;

    public MediaMatchService(
        FileNameNormalizer normalizer,
        IJpegMetadataService? jpegMetadataService = null,
        JpegQualityAssessmentService? jpegAssessmentService = null,
        IFeatureGateService? featureGateService = null)
    {
        _normalizer = normalizer;
        _jpegMetadataService = jpegMetadataService ?? new JpegMetadataService();
        _jpegAssessmentService = jpegAssessmentService ?? new JpegQualityAssessmentService();
        _featureGateService = featureGateService;
    }

    public Task<IReadOnlyList<MediaMatchDecision>> MatchAsync(
        IEnumerable<MediaSelectionItem> items,
        MediaIndexSnapshot index,
        MediaMatchOptions options,
        CancellationToken cancellationToken) => Task.Run<IReadOnlyList<MediaMatchDecision>>(() =>
    {
        if (_featureGateService is not null && options.Category == CollectionCategory.Custom &&
            !_featureGateService.HasAccess(LicensedFeature.CustomFileFormats))
        {
            throw new InvalidOperationException("自定义文件格式是专业版功能。 ");
        }
        if (_featureGateService is not null && options.EffectiveCustomerJpegMode != CustomerJpegHandlingMode.Strict &&
            !_featureGateService.HasAccess(LicensedFeature.AdvancedJpegQualityAssessment))
        {
            throw new InvalidOperationException("高级 JPG 质量对比和客户文件备用模式是专业版功能。 ");
        }
        var results = new List<MediaMatchDecision>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = _normalizer.Normalize(item.OriginalInput);
            var duplicateKey = normalized.NumericId.Length > 0 ? $"N:{normalized.NumericId}" : $"F:{normalized.ComparisonName}";
            var isDuplicate = !seen.Add(duplicateKey);
            var formatResults = BuildTargets(options)
                .Select(target => target.Category == FileCategory.Jpeg
                    ? MatchJpegTarget(item, normalized, target, index, options)
                    : MatchStandardTarget(normalized, target, index))
                .ToList();
            var overallStatus = DetermineOverallStatus(formatResults);
            var note = BuildNote(formatResults, isDuplicate);
            results.Add(new MediaMatchDecision(item.Id, normalized.ComparisonName, normalized.NumericId, overallStatus, formatResults, isDuplicate, note));
        }
        return results;
    }, cancellationToken);

    private MediaFormatMatchResult MatchJpegTarget(
        MediaSelectionItem item,
        NormalizedFileName normalized,
        TargetDefinition target,
        MediaIndexSnapshot index,
        MediaMatchOptions options)
    {
        var sourceCandidates = FindCandidates(normalized, target, index)
            .Where(file => file.JpegSourceType != JpegFileSourceType.CustomerReturnedFile && !file.IsCustomerProvided)
            .ToList();
        foreach (var source in sourceCandidates) EnsureQuality(source);

        var customerFile = CreateCustomerFile(item, target, options.CustomExtensions, out var customerError);
        var rankedSources = _jpegAssessmentService.RankCandidates(sourceCandidates, normalized, customerFile);
        var allCandidates = _jpegAssessmentService.RankCandidates(
                customerFile is null ? rankedSources : rankedSources.Append(customerFile),
                normalized,
                customerFile)
            .DistinctBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = NewResult(target, allCandidates);
        result.ErrorMessage = customerError;
        result.RecommendedFile = rankedSources.FirstOrDefault() ?? customerFile;
        if (result.RecommendedFile is not null)
        {
            result.RecommendedCandidateReason = result.RecommendedFile.JpegSourceType == JpegFileSourceType.SourceDirectory
                ? _jpegAssessmentService.BuildRecommendedReason(result.RecommendedFile, customerFile, normalized)
                : "仅找到客户返回 JPG；质量信息只用于风险提示，不能自动证明其为原图";
        }

        if (rankedSources.Count == 1)
        {
            result.SelectedFile = rankedSources[0];
            result.Status = MatchStatus.Matched;
            result.FinalJpegSourceType = JpegFileSourceType.SourceDirectory;
            if (customerFile is not null)
            {
                _jpegAssessmentService.ApplyComparisonWarnings(rankedSources[0], customerFile);
                result.JpegComparisonSummary = _jpegAssessmentService.BuildComparison(rankedSources[0], customerFile);
            }
            return result;
        }

        if (rankedSources.Count > 1)
        {
            result.Status = MatchStatus.Conflict;
            result.SelectedFile = null;
            if (customerFile is not null && result.RecommendedFile is not null)
            {
                _jpegAssessmentService.ApplyComparisonWarnings(result.RecommendedFile, customerFile);
                result.JpegComparisonSummary = _jpegAssessmentService.BuildComparison(result.RecommendedFile, customerFile);
            }
            return result;
        }

        if (customerFile is null)
        {
            result.Status = MatchStatus.NotFound;
            return result;
        }

        switch (options.EffectiveCustomerJpegMode)
        {
            case CustomerJpegHandlingMode.SmartBackup:
                result.Status = MatchStatus.WaitingManualConfirmation;
                result.RequiresManualConfirmation = true;
                break;
            case CustomerJpegHandlingMode.AllowCustomerFile:
                result.Status = MatchStatus.Matched;
                result.SelectedFile = customerFile;
                result.UsedCustomerFile = true;
                result.FinalJpegSourceType = JpegFileSourceType.CustomerReturnedFile;
                break;
            default:
                result.Status = MatchStatus.NotFound;
                break;
        }
        return result;
    }

    private MediaFormatMatchResult MatchStandardTarget(
        NormalizedFileName normalized,
        TargetDefinition target,
        MediaIndexSnapshot index)
    {
        var candidates = FindCandidates(normalized, target, index);
        return new MediaFormatMatchResult
        {
            Key = target.Key,
            DisplayName = target.DisplayName,
            Category = target.Category,
            TargetExtensions = target.Extensions,
            Candidates = candidates,
            Status = candidates.Count switch { 0 => MatchStatus.NotFound, 1 => MatchStatus.Matched, _ => MatchStatus.Conflict },
            SelectedFile = candidates.Count == 1 ? candidates[0] : null
        };
    }

    private static IReadOnlyList<MediaFileRecord> FindCandidates(
        NormalizedFileName normalized,
        TargetDefinition target,
        MediaIndexSnapshot index)
    {
        IReadOnlyList<MediaFileRecord> candidates = [];
        if (normalized.ComparisonName.Length > 0)
        {
            candidates = index.FindByNameAndExtensions(normalized.ComparisonName, target.Extensions);
        }
        if (candidates.Count == 0 && normalized.NumericId.Length > 0)
        {
            candidates = index.FindByNumberAndExtensions(normalized.NumericId, target.Extensions);
        }
        return candidates;
    }

    private MediaFileRecord? CreateCustomerFile(
        MediaSelectionItem item,
        TargetDefinition target,
        IReadOnlyList<string> customExtensions,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(item.CustomerInputFilePath)) return null;
        var extension = MediaExtensionPolicy.NormalizeExtension(Path.GetExtension(item.CustomerInputFilePath));
        if (!target.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return null;
        if (!File.Exists(item.CustomerInputFilePath))
        {
            errorMessage = "客户返回 JPG 不存在或存储设备不可用。";
            return null;
        }

        try
        {
            var customerFile = MediaFileRecord.FromFile(
                item.CustomerInputFilePath,
                Path.GetDirectoryName(item.CustomerInputFilePath)!,
                _normalizer,
                customExtensions,
                isCustomerProvided: true);
            customerFile.JpegSourceType = JpegFileSourceType.CustomerReturnedFile;
            customerFile.SourcePriority = int.MaxValue;
            customerFile.JpegQuality = _jpegAssessmentService.Assess(_jpegMetadataService.Read(customerFile.FullPath), customerFile.FileName);
            return customerFile;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            errorMessage = $"客户返回 JPG 无法读取：{ex.Message}";
            return null;
        }
    }

    private void EnsureQuality(MediaFileRecord file)
    {
        if (file.JpegQuality is null)
        {
            file.JpegQuality = _jpegMetadataService.Read(file.FullPath);
        }
        _jpegAssessmentService.Assess(file.JpegQuality, file.FileName);
    }

    private static MediaFormatMatchResult NewResult(TargetDefinition target, IReadOnlyList<MediaFileRecord> candidates) => new()
    {
        Key = target.Key,
        DisplayName = target.DisplayName,
        Category = target.Category,
        TargetExtensions = target.Extensions,
        Candidates = candidates
    };

    private static IReadOnlyList<TargetDefinition> BuildTargets(MediaMatchOptions options)
    {
        var jpeg = options.JpegExtensions.Select(MediaExtensionPolicy.NormalizeExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var raw = options.RawExtensions.Select(MediaExtensionPolicy.NormalizeExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return options.Category switch
        {
            CollectionCategory.JpegOnly => [new("JPG", "JPG", FileCategory.Jpeg, jpeg)],
            CollectionCategory.RawOnly => [new("RAW", "RAW", FileCategory.Raw, raw)],
            CollectionCategory.JpegAndRaw =>
            [
                new("JPG", "JPG", FileCategory.Jpeg, jpeg),
                new("RAW", "RAW", FileCategory.Raw, raw)
            ],
            CollectionCategory.Custom => options.CustomExtensions
                .Select(MediaExtensionPolicy.NormalizeExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(extension => new TargetDefinition(extension, extension.TrimStart('.'), MediaExtensionPolicy.Classify(extension, options.CustomExtensions), [extension]))
                .ToList(),
            _ => []
        };
    }

    private static MediaOverallStatus DetermineOverallStatus(IReadOnlyList<MediaFormatMatchResult> results)
    {
        if (results.Any(x => x.Status == MatchStatus.CopyFailed)) return MediaOverallStatus.CopyFailed;
        if (results.Any(x => x.Status == MatchStatus.WaitingManualConfirmation)) return MediaOverallStatus.WaitingConfirmation;
        if (results.Count == 0 || results.All(x => x.Status == MatchStatus.NotFound)) return MediaOverallStatus.NotFound;
        if (results.Any(x => x.Status == MatchStatus.Conflict)) return MediaOverallStatus.Conflict;
        if (results.All(x => x.Status is MatchStatus.Matched or MatchStatus.ManuallyConfirmed)) return MediaOverallStatus.CompleteMatched;
        return MediaOverallStatus.PartialMatched;
    }

    private static string BuildNote(IReadOnlyList<MediaFormatMatchResult> results, bool duplicate)
    {
        var notes = new List<string>();
        if (duplicate) notes.Add("重复输入，实际源文件只复制一次");
        foreach (var result in results)
        {
            if (result.Category != FileCategory.Jpeg)
            {
                if (result.Status == MatchStatus.NotFound) notes.Add($"{result.DisplayName} 未找到");
                else if (result.Status == MatchStatus.Conflict) notes.Add($"{result.DisplayName} 存在冲突");
                continue;
            }

            if (result.Status == MatchStatus.Conflict)
            {
                notes.Add("来源 JPG 存在冲突，必须手动选择");
            }
            else if (result.Status == MatchStatus.WaitingManualConfirmation)
            {
                notes.Add("未找到来源 JPG；客户 JPG 等待手动确认");
            }
            else if (result.Status == MatchStatus.NotFound && result.CandidateCount > 0)
            {
                notes.Add("未找到来源 JPG；客户返回文件未自动采用");
            }
            else if (result.Status == MatchStatus.NotFound)
            {
                notes.Add("JPG 未找到（未找到来源 JPG）");
            }
            else if (result.UsedCustomerFile)
            {
                notes.Add("使用客户返回 JPG；原始质量未经确认");
            }
            else if (result.SelectedFile?.JpegSourceType == JpegFileSourceType.SourceDirectory &&
                     result.Candidates.Any(file => file.JpegSourceType == JpegFileSourceType.CustomerReturnedFile))
            {
                notes.Add("已优先使用来源目录 JPG；客户返回 JPG 仅用于选片和质量对比");
            }
        }
        return string.Join("；", notes);
    }

    private sealed record TargetDefinition(string Key, string DisplayName, FileCategory Category, IReadOnlyList<string> Extensions);
}
