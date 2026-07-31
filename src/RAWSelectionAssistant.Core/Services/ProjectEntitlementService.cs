using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class ProjectEntitlementService(
    FileNameNormalizer normalizer,
    IFeatureGateService featureGateService)
{
    public const int FreeSelectionLimit = 30;
    public const int FreeSourceDirectoryLimit = 1;

    public SelectionImportLimitResult ApplySelectionLimit(
        IEnumerable<MediaSelectionItem> existingItems,
        IEnumerable<ParsedSelectionInput> incomingItems,
        bool tutorialBypass = false)
    {
        var unlimited = tutorialBypass || featureGateService.HasAccess(LicensedFeature.UnlimitedSelections);
        var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in existingItems)
        {
            var key = GetUniqueKey(item.NormalizedName, item.NumericId, item.OriginalInput);
            if (key.Length > 0) uniqueKeys.Add(key);
        }

        var accepted = new List<ParsedSelectionInput>();
        var rejected = new List<ParsedSelectionInput>();
        foreach (var item in incomingItems)
        {
            var normalized = normalizer.Normalize(item.OriginalInput);
            var key = GetUniqueKey(normalized.ComparisonName, normalized.NumericId, item.OriginalInput);
            if (key.Length == 0 || uniqueKeys.Contains(key))
            {
                accepted.Add(item);
                continue;
            }

            if (!unlimited && uniqueKeys.Count >= FreeSelectionLimit)
            {
                rejected.Add(item);
                continue;
            }

            uniqueKeys.Add(key);
            accepted.Add(item);
        }

        var limitReached = rejected.Count > 0;
        var message = limitReached
            ? $"免费版每个项目最多支持 {FreeSelectionLimit} 个唯一选片编号；重复编号不重复计数。"
            : string.Empty;
        return new SelectionImportLimitResult(accepted, rejected, uniqueKeys.Count, limitReached, message);
    }

    public SourceDirectoryLimitResult CanAddSourceDirectory(int existingDirectoryCount, bool tutorialBypass = false)
    {
        if (tutorialBypass || featureGateService.HasAccess(LicensedFeature.MultipleSourceDirectories))
        {
            return new SourceDirectoryLimitResult(true, int.MaxValue, string.Empty);
        }

        return existingDirectoryCount < FreeSourceDirectoryLimit
            ? new SourceDirectoryLimitResult(true, FreeSourceDirectoryLimit, string.Empty)
            : new SourceDirectoryLimitResult(false, FreeSourceDirectoryLimit, "免费版最多添加 1 个照片来源目录。");
    }

    private static string GetUniqueKey(string normalizedName, string numericId, string originalInput)
    {
        if (!string.IsNullOrWhiteSpace(numericId)) return $"ID:{numericId}";
        if (!string.IsNullOrWhiteSpace(normalizedName)) return $"NAME:{normalizedName}";
        return string.IsNullOrWhiteSpace(originalInput) ? string.Empty : $"RAW:{originalInput.Trim()}";
    }
}
