using PixelTart.SelectionApi.Contracts;

namespace PixelTart.SelectionApi.Skeleton;

public sealed record SelectionEndpointDefinition(string Method, string Route, string Purpose);

public static class SelectionApiSkeleton
{
    public static IReadOnlyList<SelectionEndpointDefinition> Endpoints { get; } =
    [
        new("POST", SelectionApiRouteNames.Projects, "创建选片项目"),
        new("PUT", SelectionApiRouteNames.Projects + "/{projectId}", "更新选片项目"),
        new("POST", SelectionApiRouteNames.Projects + "/{projectId}/assets", "创建代理图上传会话"),
        new("POST", SelectionApiRouteNames.Projects + "/{projectId}/publish", "发布项目"),
        new("GET", SelectionApiRouteNames.Projects + "/{projectId}/progress", "读取客户选片进度"),
        new("GET", SelectionApiRouteNames.Projects + "/{projectId}/final-selection", "读取客户最终结果"),
        new("GET", SelectionApiRouteNames.ClientProjects + "/{publicId}", "客户项目首页"),
        new("POST", SelectionApiRouteNames.ClientProjects + "/{publicId}/confirm", "客户确认选片")
    ];

    public const bool IsProductionConfigured = false;
    public const bool StartsLocalListener = false;
    public const string ProductionDependencyMessage = "需要生产域名、HTTPS、数据库、对象存储、微信 AppID 与凭证。";
}
