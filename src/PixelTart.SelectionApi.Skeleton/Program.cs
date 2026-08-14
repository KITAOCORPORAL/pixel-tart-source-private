using PixelTart.SelectionApi.Contracts;

namespace PixelTart.SelectionApi.Skeleton;

public sealed record SelectionEndpointDefinition(string Method, string Route, string Purpose);

public static class SelectionApiSkeleton
{
    public static IReadOnlyList<SelectionEndpointDefinition> Endpoints { get; } =
    [
        new("POST", SelectionApiRouteNames.Projects, "创建选片项目"),
        new("PUT", SelectionApiRouteNames.Projects + "/{projectId}", "更新选片项目"),
        new("GET", SelectionApiRouteNames.Projects + "/{projectId}", "读取摄影师端项目"),
        new("POST", SelectionApiRouteNames.ProjectAssets, "创建代理图上传会话"),
        new("POST", SelectionApiRouteNames.ProjectAssetComplete, "完成代理图上传"),
        new("DELETE", SelectionApiRouteNames.ProjectAssetCloudCopy, "只删除云端副本"),
        new("POST", SelectionApiRouteNames.ProjectPublish, "发布项目"),
        new("POST", SelectionApiRouteNames.ProjectUnpublish, "撤销发布"),
        new("GET", SelectionApiRouteNames.ProjectProgress, "读取客户选片进度"),
        new("GET", SelectionApiRouteNames.ProjectFinalSelection, "读取客户最终结果"),
        new("GET", SelectionApiRouteNames.ClientProjects + "/{publicId}", "客户项目首页"),
        new("GET", SelectionApiRouteNames.ClientAssets, "分页读取客户图库"),
        new("PUT", SelectionApiRouteNames.ClientChoices, "客户选择照片"),
        new("PUT", SelectionApiRouteNames.ClientFavorites, "客户收藏照片"),
        new("PUT", SelectionApiRouteNames.ClientComments, "客户备注"),
        new("POST", SelectionApiRouteNames.ClientConfirm, "客户确认选片")
    ];

    public const bool IsProductionConfigured = false;
    public const bool StartsLocalListener = false;
    public const string ProductionDependencyMessage = "需要生产域名、HTTPS、数据库、对象存储、微信 AppID 与凭证。";
}
