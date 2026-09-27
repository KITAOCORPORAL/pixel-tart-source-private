# Twenty One Rating and Selection Model

Status: **UNVERIFIED**

The requested 0–5 hover/commit tests, pointer sweep 1→5, numeric shortcuts, pick/reject/flag/color labels, Ctrl/Shift range, select-all, deselect, invert, and active-vs-selected separation were not executable.

## Observation vocabulary (not internal identifiers)

```text
HoveredAsset / FocusedAsset / ActiveAsset / SelectedAssets
RatingPreview / CommittedRating
```

No conclusion is made about whether Twenty One keeps these states separate. Pixel Tart contracts model persistent 0–5 rating and `SelectedAssetId` plus `SelectedAssetIds`; see current source models.

