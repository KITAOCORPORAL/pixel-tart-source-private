using PixelTart.SelectionApi.Contracts;

namespace PixelTart.SelectionApi.Skeleton;

public sealed record SelectionEndpointDefinition(string Method, string Route, string Purpose);

public static class SelectionApiSkeleton
{
    public static IReadOnlyList<SelectionEndpointDefinition> Endpoints { get; } =
    [
        new("POST", SelectionApiRouteNames.Projects, "Create selection project"),
        new("PUT", SelectionApiRouteNames.Projects + "/{projectId}", "Update selection project"),
        new("GET", SelectionApiRouteNames.Projects + "/{projectId}", "Read photographer project"),
        new("POST", SelectionApiRouteNames.ProjectAssets, "Create proxy upload session"),
        new("POST", SelectionApiRouteNames.ProjectAssetComplete, "Complete proxy upload"),
        new("DELETE", SelectionApiRouteNames.ProjectAssetCloudCopy, "Delete cloud copy only"),
        new("POST", SelectionApiRouteNames.ProjectPublish, "Publish project"),
        new("POST", SelectionApiRouteNames.ProjectUnpublish, "Unpublish project"),
        new("GET", SelectionApiRouteNames.ProjectProgress, "Read client selection progress"),
        new("GET", SelectionApiRouteNames.ProjectFinalSelection, "Read final selection"),
        new("GET", SelectionApiRouteNames.ClientProjects + "/{publicId}", "Read client project"),
        new("GET", SelectionApiRouteNames.ClientAssets, "Read paginated client gallery"),
        new("PUT", SelectionApiRouteNames.ClientChoices, "Set client choice"),
        new("PUT", SelectionApiRouteNames.ClientFavorites, "Set client favorite"),
        new("PUT", SelectionApiRouteNames.ClientComments, "Set client comment"),
        new("POST", SelectionApiRouteNames.ClientConfirm, "Confirm client selection")
    ];

    public const bool IsProductionConfigured = false;
    public const bool StartsLocalListener = false;
    public const string ProductionDependencyMessage = "Production requires a domain, HTTPS, database, object storage, and WeChat credentials.";
}
