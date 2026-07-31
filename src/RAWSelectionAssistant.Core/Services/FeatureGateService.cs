using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services;

public sealed class FeatureGateService(ILicenseService licenseService) : IFeatureGateService
{
    private static readonly IReadOnlyDictionary<LicensedFeature, string> Messages =
        new Dictionary<LicensedFeature, string>
        {
            [LicensedFeature.UnlimitedSelections] = "免费版每个项目最多支持 30 个唯一选片编号。",
            [LicensedFeature.MultipleSourceDirectories] = "免费版最多添加 1 个照片来源目录。",
            [LicensedFeature.PersistentFileIndex] = "持久化高速索引是专业版功能；免费版仍可正常扫描和匹配。",
            [LicensedFeature.CustomFileFormats] = "自定义文件格式是专业版功能。",
            [LicensedFeature.AdvancedJpegQualityAssessment] = "完整 JPG 尺寸、EXIF 和质量对比是专业版功能。",
            [LicensedFeature.AdvancedConflictResolution] = "高级冲突对比是专业版功能；免费版仍可进行基础候选选择。",
            [LicensedFeature.UnlimitedProjectHistory] = "免费版只显示最近 1 个项目。",
            [LicensedFeature.AdvancedReports] = "JSON 和完整技术日志报告是专业版功能；免费版可导出基础 CSV。",
            [LicensedFeature.OutputPresets] = "输出预设是专业版功能。",
            [LicensedFeature.BatchProjects] = "批量项目处理是专业版功能。"
        };

    public LicenseState CurrentLicense => licenseService.Current;
    public bool HasAccess(LicensedFeature feature) => licenseService.Current.IsPro;
    public FeatureAccessResult Check(LicensedFeature feature) => HasAccess(feature)
        ? FeatureAccessResult.Permit(feature)
        : FeatureAccessResult.Deny(feature, Messages[feature]);
}
