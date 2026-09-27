namespace RAWSelectionAssistant.Core.Models;

/// <summary>Canonical photography workflow state. Active, selected and rated are independent concepts.</summary>
public sealed record PhotographyContextSnapshot(
    Guid? ActiveAssetId,
    IReadOnlySet<Guid> SelectedAssetIds,
    int? ActiveRating,
    string? CurrentViewerAssetPath = null,
    string? PreviewPresetId = null,
    string? AppliedPresetId = null,
    double PresetStrength = 1,
    string? ReferenceLookId = null,
    string? FilmProfileId = null,
    IReadOnlySet<Guid>? BatchTargetIds = null,
    bool HasPendingPreview = false,
    bool HasCommittedState = true,
    string? ExportSnapshotId = null)
{
    public IReadOnlySet<Guid> EffectiveBatchTargetIds => BatchTargetIds ?? SelectedAssetIds;
    public bool IsSelected(Guid id) => SelectedAssetIds.Contains(id);
    public PhotographyContextSnapshot WithActive(Guid? id, string? path = null, int? rating = null) =>
        this with { ActiveAssetId = id, CurrentViewerAssetPath = path, ActiveRating = rating };
}

public static class PhotographyContextReducer
{
    public static PhotographyContextSnapshot Activate(PhotographyContextSnapshot state, Guid assetId, string? path, int? rating) =>
        state.WithActive(assetId, path, rating);

    public static PhotographyContextSnapshot ToggleSelection(PhotographyContextSnapshot state, Guid assetId)
    {
        var selected = state.SelectedAssetIds.ToHashSet();
        if (!selected.Add(assetId)) selected.Remove(assetId);
        return state with { SelectedAssetIds = selected };
    }

    public static PhotographyContextSnapshot SetSelection(PhotographyContextSnapshot state, IEnumerable<Guid> assetIds) =>
        state with { SelectedAssetIds = assetIds.Where(id => id != Guid.Empty).ToHashSet() };

    public static PhotographyContextSnapshot BeginPresetPreview(PhotographyContextSnapshot state, string presetId, double strength) =>
        state with { PreviewPresetId = presetId, PresetStrength = Math.Clamp(strength, 0, 1), HasPendingPreview = true };

    public static PhotographyContextSnapshot CommitPreset(PhotographyContextSnapshot state, string presetId, double strength) =>
        state with { PreviewPresetId = null, AppliedPresetId = presetId, PresetStrength = Math.Clamp(strength, 0, 1), HasPendingPreview = false, HasCommittedState = true };

    public static PhotographyContextSnapshot CancelPreview(PhotographyContextSnapshot state) =>
        state with { PreviewPresetId = null, HasPendingPreview = false };
}
