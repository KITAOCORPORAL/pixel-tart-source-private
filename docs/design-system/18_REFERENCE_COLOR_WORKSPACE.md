# Reference Color Workspace

Purpose: a professional, image-first workspace for evaluating and saving a weighted reference-color treatment. It is not a dashboard and does not mutate source files.

Anatomy: top source/context strip; left tool rail with five fixed sections; center canvas with original/result/split/side-by-side modes; optional right reference context rail. Wide columns are 21/61/18. Below 1440 effective DIP the context rail becomes a 280–340 DIP overlay and starts closed; below 980 DIP the left rail collapses. Focus mode gives the full workspace to the canvas.

States: empty target, loading, ready, session-adjusted, reference-offline, error/retry and cancelled/stale result. Simple and Pro share state; only disclosure changes.

Do: keep the image dominant, use Chinese photographic language, keep 33³ preview and 65³ export semantics visible. Don't: show empty future controls, let a compact drawer cover the photo on entry, or persist UI mode in a color scheme.

Accessibility: keyboard focus uses `PixelTart.Focus`; controls retain automation names; value labels are not color-only indicators. Implementation: `ReferenceColorWorkspaceView`, `TetherReferenceModeViewModel`, `IReferenceRenderBackend`.
