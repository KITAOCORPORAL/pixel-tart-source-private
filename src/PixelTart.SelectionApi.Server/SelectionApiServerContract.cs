using PixelTart.SelectionApi.Contracts;

namespace PixelTart.SelectionApi.Server;

public sealed record SelectionApiServerOptions(
    bool IsProductionConfigured = false,
    bool StartsListener = false,
    string? PublicBaseUrl = null)
{
    public static SelectionApiServerOptions LocalDevelopment { get; } = new();
}
/// <summary>
/// Contract-only server façade for local development and deterministic API
/// tests. No listener, credentials, database, or production cloud is started.
/// </summary>
public sealed class SelectionApiServerContract(SelectionApiServerOptions? options = null)
{
    public SelectionApiServerOptions Options { get; } = options ?? SelectionApiServerOptions.LocalDevelopment;
    public bool IsProductionConfigured => Options.IsProductionConfigured;
    public bool StartsListener => Options.StartsListener;
    public IReadOnlyList<string> Routes { get; } = SelectionApiSkeletonRoutes.All;
}

internal static class SelectionApiSkeletonRoutes
{
    public static IReadOnlyList<string> All { get; } =
    [
        $"POST {SelectionApiRouteNames.Projects}",
        $"GET {SelectionApiRouteNames.Projects}/{{projectId}}",
        $"POST {SelectionApiRouteNames.ProjectAssets}",
        $"POST {SelectionApiRouteNames.ProjectAssetComplete}",
        $"POST {SelectionApiRouteNames.ProjectPublish}",
        $"POST {SelectionApiRouteNames.ProjectUnpublish}",
        $"GET {SelectionApiRouteNames.ProjectProgress}",
        $"GET {SelectionApiRouteNames.ProjectFinalSelection}",
        $"GET {SelectionApiRouteNames.ClientProjects}/{{publicId}}",
        $"GET {SelectionApiRouteNames.ClientAssets}",
        $"PUT {SelectionApiRouteNames.ClientChoices}",
        $"PUT {SelectionApiRouteNames.ClientFavorites}",
        $"PUT {SelectionApiRouteNames.ClientComments}",
        $"POST {SelectionApiRouteNames.ClientConfirm}"
    ];
}
