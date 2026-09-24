# Color Studio Phase 2 UX Layout

**Status: PLANNED — BLOCKED BY PHASE 1 REGRESSION.** Preserve Graphite, Mineral, Warm Silver, Oxidized Copper, and Photography Gold. Do not use an Adobe/Capture One clone or purple dashboard treatment.

| Option | Description | Minimum width | Image share | 1180×720 behavior |
|---|---|---:|---:|---|
| A. Right Context Panel | collapsible 3D point cloud beside existing context rail | 1320 DIP | ~68% | stays collapsed by default; explicit expand |
| B. Temporary Canvas Split | temporary overlay/split while inspecting a cluster | 1180 DIP | ~50% while open | image-first default; split closes on Escape |
| C. Floating/Expandable Inspector | transient panel anchored to selected node/sample | 1180 DIP | ~78% | opens on demand; never reserves permanent width |

Recommendation: **C**, with an optional A-like dock at wide widths. It preserves the existing `ReferenceColorWorkspaceView` canvas and uses the established `FocusView`/`ContextRailOpen` state instead of redesigning the workspace. B is useful for comparison but most disruptive at compact sizes.

The 3D view is a context surface: no permanent full-screen 3D mode, no rainbow chrome, and no replacement of the existing node inspector.

