# Color Studio Phase 2 Rendering Options

**Status: DECISION RECORDED — PROTOTYPE NOT YET INTEGRATED; BLOCKED BY PHASE 1 REGRESSION.**

| Option | Complexity | Performance | Interaction | DPI / packaging | Maintenance / dependency cost |
|---|---|---|---|---|---|
| Existing WPF 3D (`Viewport3D`) | medium | adequate for bounded points; CPU/scene limits | native WPF input | existing DPI/package | lowest new dependency; visual styling work required |
| DirectX-compatible route | high | strongest ceiling | custom input/render loop | higher DPI/device surface | high implementation and packaging cost; no current route in repo |
| Skia/existing bitmap renderer reuse | medium | good for projected 2D points, not true 3D | custom orbit math | existing bitmap path | no current Skia 3D capability; would be a new dependency/path |

Decision for planning: start with **existing WPF 3D only if a bounded prototype meets the proxy/performance gate**; otherwise use a projected 2D diagnostic fallback rather than adding a large framework. DirectX and Skia routes are not implemented in the repository today.

No rendering option is claimed as production-ready in this run because no WPF scene or visual evidence was implemented.
