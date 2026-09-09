namespace PixelTart.Modules.AssetLibrary;

/// <summary>Selection rules shared by pointer and automation context-menu entry points.</summary>
public static class AssetLibraryContextSelectionPolicy
{
    /// <summary>
    /// A context click promotes an unselected card to the sole selection. Clicking a card
    /// already in an extended selection keeps the complete selection set for batch commands.
    /// </summary>
    public static IReadOnlySet<Guid> ResolveSelection(IEnumerable<Guid> currentSelection, Guid rightClickedAssetId)
    {
        if (rightClickedAssetId == Guid.Empty) return currentSelection.ToHashSet();
        var selected = currentSelection.Where(id => id != Guid.Empty).ToHashSet();
        return selected.Contains(rightClickedAssetId) ? selected : [rightClickedAssetId];
    }
}
