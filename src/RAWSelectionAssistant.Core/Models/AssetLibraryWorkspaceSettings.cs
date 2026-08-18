namespace RAWSelectionAssistant.Core.Models;

public sealed class AssetLibraryWorkspaceSettings
{
    public const double DefaultOrganizationPaneWidth = 220d;
    public const double DefaultInspectorPaneWidth = 320d;
    public const double DefaultThumbnailWidth = 180d;

    public double OrganizationPaneWidth { get; set; } = DefaultOrganizationPaneWidth;
    public double InspectorPaneWidth { get; set; } = DefaultInspectorPaneWidth;
    public bool OrganizationPaneCollapsed { get; set; }
    public bool InspectorPaneCollapsed { get; set; }
    public bool InspectorPinned { get; set; }
    public double ThumbnailWidth { get; set; } = DefaultThumbnailWidth;
    public string SearchText { get; set; } = string.Empty;
    public Guid? SelectedFolderId { get; set; }
    public Guid? SelectedTagId { get; set; }
    public Guid? SelectedSmartFolderId { get; set; }

    public void Normalize()
    {
        OrganizationPaneWidth = NormalizeFinite(OrganizationPaneWidth, DefaultOrganizationPaneWidth, 180d, 420d);
        InspectorPaneWidth = NormalizeFinite(InspectorPaneWidth, DefaultInspectorPaneWidth, 260d, 520d);
        ThumbnailWidth = NormalizeFinite(ThumbnailWidth, DefaultThumbnailWidth, 120d, 280d);
        if (InspectorPinned) InspectorPaneCollapsed = false;
        SearchText = (SearchText ?? string.Empty).Trim();
        if (SearchText.Length > 500) SearchText = SearchText[..500];
    }

    private static double NormalizeFinite(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}

public static class PrimaryNavigationPolicy
{
    public const string Workbench = "Workbench";
    public const string AssetLibrary = "AssetLibrary";
    public const string Workflow = "Workflow";
    public const string WorkCalendar = "WorkCalendar";
    public const string Tether = "Tether";
    public const string Finance = "Finance";
    public const string History = "History";

    public static IReadOnlyList<string> OrderedPages { get; } =
    [
        Workbench,
        AssetLibrary,
        Workflow,
        WorkCalendar,
        Tether,
        Finance,
        History
    ];

    public static bool IsPrimaryPage(string? value) =>
        !string.IsNullOrWhiteSpace(value) && OrderedPages.Contains(value, StringComparer.Ordinal);

    public static string Normalize(string? value) => value switch
    {
        "asset-library" => AssetLibrary,
        "ProjectCenter" => Workbench,
        _ when IsPrimaryPage(value) => value!,
        _ => Workbench
    };
}
