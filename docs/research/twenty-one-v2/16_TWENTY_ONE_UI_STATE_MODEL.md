# Twenty One UI State Model

Status: **INFERRED WORKING VOCABULARY ONLY**

This is a neutral observation model, not an internal-code claim:

```text
AssetSet: ActiveAsset, SelectedAssets, HoveredAsset, FocusedPanel
Viewer: ZoomMode/ZoomFactor, PanOffset
Rating: RatingPreview, CommittedRating
Task: Idle, Loading, Running, Cancelling, Cancelled, Completed,
      PartiallyCompleted, Failed, Recoverable
```

All transitions and active-vs-selected separation remain **UNVERIFIED**.

