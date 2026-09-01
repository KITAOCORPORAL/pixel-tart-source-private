namespace RAWSelectionAssistant.Core.Models;

public enum AssetLibraryViewMode
{
    Grid,
    Masonry,
    Justified,
    List
}

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
    public Guid? SelectedAssetId { get; set; }
    public AssetLibraryViewMode ViewMode { get; set; } = AssetLibraryViewMode.Grid;
    public AssetLibrarySortField SortField { get; set; } = AssetLibrarySortField.AddedAt;
    public AssetLibrarySortDirection SortDirection { get; set; } = AssetLibrarySortDirection.Descending;
    public AssetLibrarySystemCollection ActiveCollection { get; set; } = AssetLibrarySystemCollection.AllAssets;
    public List<Guid> ExpandedFolderIds { get; set; } = [];
    public List<Guid> ExpandedTagGroupIds { get; set; } = [];
    public List<Guid> SelectedAssetIds { get; set; } = [];
    public Dictionary<string, Guid?> ScrollAnchors { get; set; } = [];

    public void Normalize()
    {
        OrganizationPaneWidth = NormalizeFinite(OrganizationPaneWidth, DefaultOrganizationPaneWidth, 180d, 420d);
        InspectorPaneWidth = NormalizeFinite(InspectorPaneWidth, DefaultInspectorPaneWidth, 260d, 520d);
        ThumbnailWidth = NormalizeFinite(ThumbnailWidth, DefaultThumbnailWidth, 120d, 280d);
        if (InspectorPinned) InspectorPaneCollapsed = false;
        SearchText = (SearchText ?? string.Empty).Trim();
        if (SearchText.Length > 500) SearchText = SearchText[..500];
        if (!Enum.IsDefined(ViewMode)) ViewMode = AssetLibraryViewMode.Grid;
        if (!Enum.IsDefined(SortField)) SortField = AssetLibrarySortField.AddedAt;
        if (!Enum.IsDefined(SortDirection)) SortDirection = AssetLibrarySortDirection.Descending;
        if (!Enum.IsDefined(ActiveCollection)) ActiveCollection = AssetLibrarySystemCollection.AllAssets;
        ExpandedFolderIds = NormalizeIds(ExpandedFolderIds, 10_000);
        ExpandedTagGroupIds = NormalizeIds(ExpandedTagGroupIds, 10_000);
        SelectedAssetIds = NormalizeIds(SelectedAssetIds, 10_000);
        if (SelectedAssetIds.Count == 0 && SelectedAssetId is not null) SelectedAssetIds.Add(SelectedAssetId.Value);
        SelectedAssetId = SelectedAssetIds.Count == 1 ? SelectedAssetIds[0] : null;
        ScrollAnchors = (ScrollAnchors ?? [])
            .Select(pair => (Valid: Enum.TryParse<AssetLibraryViewMode>(pair.Key, ignoreCase: true, out var view), View: view, pair.Value))
            .Where(item => item.Valid)
            .GroupBy(item => item.View)
            .ToDictionary(group => group.Key.ToString(), group => group.Last().Value, StringComparer.Ordinal);
    }

    private static double NormalizeFinite(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static List<Guid> NormalizeIds(IEnumerable<Guid>? values, int limit) =>
        (values ?? []).Where(value => value != Guid.Empty).Distinct().Take(limit).ToList();
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
