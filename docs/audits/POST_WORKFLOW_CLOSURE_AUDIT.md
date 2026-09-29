# Post-workflow closure audit

Generated for the correctness hotfix against the integration branch. Status values are deliberately separated from implementation claims.

| Area | Status | Evidence / finding |
|---|---|---|
| Actual Pixels formula | VERIFIED | `ActualPixelScaleCalculator` derives WPF natural DIP width from `PixelWidth * 96 / DpiX`; actual-pixel scale is `DpiX / (96 * devicePixelsPerDip)`. Matrix tests cover 100/125/150/175/200% display scales and 72/96/300 DPI. |
| A/B side-aware input | VERIFIED | Compare wheel and drag capture the hit side; VM exposes side-aware zoom/pan entry points and syncs only normalized-side state. |
| Normalized center persistence | VERIFIED | `CompareViewport` now carries bounded 0..1 center coordinates; legacy PanX/PanY remain for compatibility and side-aware WPF pan converts through image pixel/DPI geometry. |
| Swap semantics | VERIFIED | Core swap exchanges asset IDs and both viewport records; product command no longer performs a double swap. |
| Publishing progress order | VERIFIED | Publishing uses `OrderedAsyncProgress`; each report is awaited before export advances, removing fire-and-forget ordering risk. |
| Terminal progress guard | PARTIAL | TaskEngine publishes forced terminal snapshots; a dedicated late-progress regression is still required to prove no post-terminal mutation under adversarial scheduling. |
| Capability source of truth | VERIFIED | Explicit `capabilities.definition.json` is validated for evidence/test/artifact paths; generated files use `generated_from_head` and do not claim self-referential HEAD equality. |
| Creative Film | PARTIAL | Executable `PixelTartFilmPipeline` and tests exist. This is distinct from SpectralFilm and InstantFilm, both SPEC_ONLY. |
| Preset providers | PARTIAL | Adobe XMP and Cube LUT have executable parser/processor/test evidence; Capture One remains an adapter boundary without fixture verification. |
| 3D Color Space | PARTIAL | Core data/sampling evidence exists; Windows renderer is NOT_IMPLEMENTED. |

## Boundary

This audit does not claim native WPF physical-DPI walkthrough completion. It also does not upgrade GPU, RAW, ICC conversion, or 3D renderer status without corresponding runtime acceptance evidence.
