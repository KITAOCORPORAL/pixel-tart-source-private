using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public static class SelectionProjectValidator
{
    public static SelectionValidationResult ValidateDraft(SelectionProject project)
    {
        if (project.Id == Guid.Empty || string.IsNullOrWhiteSpace(project.PublicId) || project.PublicId.Length < 24)
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidProject, "项目标识无效。");
        if (string.IsNullOrWhiteSpace(project.Name))
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidProject, "请填写项目名。");
        if (string.IsNullOrWhiteSpace(project.ClientDisplayName))
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidProject, "请填写客户称呼。");
        if (project.TargetCount <= 0)
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidProject, "目标数量必须大于零。");
        return SelectionValidationResult.Valid();
    }

    public static SelectionValidationResult ValidateRule(SelectionRule rule)
    {
        if (rule.ProjectId == Guid.Empty || rule.TargetCount <= 0)
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidRule, "目标数量必须大于零。");
        if (rule.MinimumCount < 0 || rule.MaximumCount < rule.MinimumCount || rule.TargetCount < rule.MinimumCount || rule.TargetCount > rule.MaximumCount)
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidRule, "最低、目标和最高数量的关系无效。");
        if (!rule.AllowExtraSelections && rule.ExtraSelectionPriceMinor != 0)
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidRule, "不允许加选时不能设置加选价格。");
        if (rule.ExtraSelectionPriceMinor < 0)
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidRule, "加选价格不能为负数。");
        if (rule.AccessExpiresAtUtc is not null && rule.DeadlineUtc is not null && rule.AccessExpiresAtUtc < rule.DeadlineUtc)
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidRule, "访问有效期不能早于截止日期。");
        return SelectionValidationResult.Valid();
    }

    public static SelectionValidationResult ValidateForPublish(
        SelectionProject project,
        SelectionRule rule,
        IEnumerable<SelectionAsset> assets)
    {
        var projectValidation = ValidateDraft(project);
        if (!projectValidation.IsValid) return projectValidation;
        var ruleValidation = ValidateRule(rule);
        if (!ruleValidation.IsValid) return ruleValidation;
        if (rule.ProjectId != project.Id)
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.InvalidRule, "选片规则不属于当前项目。");
        if (!assets.Any(asset => asset.ProjectId == project.Id && asset.Status == SelectionAssetStatus.Ready))
            return SelectionValidationResult.Invalid(OnlineSelectionErrorCodes.NoReadyAssets, "至少需要一张已就绪照片才能发布。");
        return SelectionValidationResult.Valid("项目、照片和规则已通过发布检查。");
    }
}

public sealed record SelectionClientAsset(
    Guid ImageId,
    string FileName,
    int SortOrder,
    bool IsCover,
    string? SignedPreviewUrl);

public static class SelectionPrivacyPolicy
{
    public static SelectionClientAsset ToClientAsset(SelectionAsset asset, string? signedPreviewUrl = null) => new(
        asset.Id,
        SafeFileName(asset.OriginalFileName),
        asset.SortOrder,
        asset.IsCover,
        signedPreviewUrl);

    public static string SafeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try { return Path.GetFileName(value); }
        catch { return string.Empty; }
    }

    public static string SafeOperationalMessage(string code) => code switch
    {
        OnlineSelectionErrorCodes.ProviderNotConfigured => "在线选片服务尚未配置。",
        OnlineSelectionErrorCodes.NoReadyAssets => "至少需要一张已就绪照片才能发布。",
        OnlineSelectionErrorCodes.InvalidProject => "项目资料不完整。",
        OnlineSelectionErrorCodes.InvalidRule => "选片规则需要检查。",
        OnlineSelectionErrorCodes.UploadFailed => "照片上传未完成，可稍后重试。",
        OnlineSelectionErrorCodes.ProxyGenerationFailed => "代理图生成未完成，源文件保持不变。",
        _ => "操作未完成，请稍后重试。"
    };
}
