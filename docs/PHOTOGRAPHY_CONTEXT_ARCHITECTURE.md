# Photography Context architecture

Pixel Tart now has an explicit core context model for the workflow boundary:

- `ActiveAssetId` / viewer path: the photo currently being viewed or edited.
- `SelectedAssetIds`: the persistent multi-selection used by batch operations.
- Rating: metadata on each asset, independent of both active and selected state.
- `PreviewPresetId` vs `AppliedPresetId`: hover preview never enters committed history.
- `EffectiveBatchTargetIds`: explicit target scope, defaulting to selected assets.

The existing Asset Library ViewModel remains backward compatible and exposes `ActiveAsset`,
`SelectedAssetSet`, and a visible Chinese batch-target summary. Viewer window wiring still needs
to be migrated to this context; that remains PARTIAL.
