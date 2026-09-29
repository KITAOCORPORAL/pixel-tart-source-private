# Color Studio current status

> Current capability source of truth: `docs/current/PIXEL_TART_CURRENT_CAPABILITIES.json` and `docs/current/PIXEL_TART_CURRENT_STATE.md`. This document remains the Color Studio-specific historical/current-status note.

## Implemented and tested

- Existing Match v3 and guarded Match v4 Core contracts remain in place.
- Existing `ColorSpaceVisualizationModel` provides bounded color sampling, Lab/OKLab conversion, point-cloud data, camera state, and migration vectors at Core level.
- Publishing now has a verified multi-recipe file path and a guarded high-precision TIFF16 path.

## Partial / not yet production-verified

- A Windows/WPF 3D renderer and native screenshot evidence are not closed by this change.
- GPU rendering, device-loss behavior, and 3D performance benchmarks require a separate native acceptance phase.
- The current worktree's pre-existing unrelated visual test failures remain historical baseline failures and were not rewritten.
