# Tethered Non-Blocking Pipeline

```text
watch-folder event -> stability probe -> persist discovery -> fast preview -> UI selection
                                                     \-> reference render worker
source saved/visible ----------------------------------------------------^ 
```

Capture ingestion is owned by the existing watch-folder session and never awaits reference matching. `TetherReferenceModeViewModel` holds one cancellable render request and a monotonic revision. Slider changes debounce for 80ms; a newer revision or asset identity invalidates older output before publication.

The render worker performs analysis, matching, optional LUT and display conversion off the UI thread. Only the final frozen bitmap is assigned to the view model. Cancellation, timeout-like cancellation, decode/match error, or page shutdown leaves the original preview usable.

Split drag is independent from this pipeline: `TetherReferenceSplitView` only mutates a `RectangleGeometry` and visual positions. Its two bitmap layers inherit the same parent Scale/Translate so zoom and pan cannot drift.

Watch Folder capability values are false for remote capture, focus, exposure writes, battery and storage reporting. Corresponding control groups are absent rather than disabled placeholders.
