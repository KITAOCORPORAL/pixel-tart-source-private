# Color Studio Phase 1 closure — current development state

## Verified

- Windows-native .NET 10 SDK 10.0.401, Release x64 build: 0 warnings, 0 errors.
- Per-target stack isolation and null-stack reset, stack-only target snapshot, simple/professional reference and film parameter synchronization: targeted WPF tests pass.
- Color Range positive/negative samples, negative-only clear, node reset, continuous sampling, rename/reorder, slider edit transaction and undo/redo: targeted tests pass.
- Four-mode displayed-image coordinate mapping and letterbox rejection: targeted tests pass. Zoom/pan is not yet implemented.
- Scheme catalog uses atomic replacement; serializer, restart read, update and delete tests pass. Legacy `ReferenceLookStore` remains unchanged.
- Selection mask uses the selected node's captured input buffer; selection overlay remains editor presentation only. Export uses the shared `Render(...).Pixels` chain.
- Photography-critical targeted WPF regression (including inactive export) passed 38/38. Logical DPI 100/125/150/200% test passed, but the captured state had no loaded target photo and is **not** accepted as Phase 1 production screenshot evidence.
- 30 × 2400×1600 processed batch export passed in approximately 61 seconds; previous baseline 59.7 seconds / 771.5 MB. This run did not report a comparable peak-memory value.

## Open product gates (Phase 1)

- Production app scenarios and the requested 18 full-size screenshots have not been captured or visually reviewed. Empty-target DPI renders are not substituted.
- Professional node inspector does not yet expose every Reference and Film field in its selected-node section; related simple-mode pages are shared but the contextual inspector remains partial.
- Node list still needs a visually verified enable toggle/type icon/overflow menu; current command buttons, row name/strength and drag behavior are functional but do not satisfy the complete row UX contract.
- Batch sync popup and Scheme UI need production interaction QA; the model/commands and persistence tests do not prove their visual behavior.
- Shared zoom/pan comparison is not implemented; this also limits eyedropper testing under zoom/pan.
- Processing cancel and rapid target/reference/scheme switching need dedicated bounded behavioral regression beyond the existing targeted tests.

## Separate gates

- 3D Color Space: **DEFERRED_TO_PHASE_2**.
- Physical-display DPI: **RELEASE HARDWARE GATE PENDING**.
- Performance optimization: **P2**, not a Phase 1 speed target.

**COLOR STUDIO PHASE 1: BLOCKED** by the open product gates above, not by physical DPI, 3D space or historical RC12 evidence.
