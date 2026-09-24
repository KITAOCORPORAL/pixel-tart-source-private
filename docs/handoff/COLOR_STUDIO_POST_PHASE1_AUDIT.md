# Color Studio Post-Phase-1 Audit / Phase 2 Entry Gate

Audit basis: repository state at `9345d22211b338ae370a8ded1059fa2e9b920401` on `integration/pixel-tart-developer-preview`.

## Source safety

| Check | Result |
|---|---|
| Local HEAD | `9345d22211b338ae370a8ded1059fa2e9b920401` |
| Remote HEAD | `9345d22211b338ae370a8ded1059fa2e9b920401` |
| Working tree | Clean at audit start |
| Product branch | `integration/pixel-tart-developer-preview` |
| Build | Release x64 PASS, 0 warnings / 0 errors (closure record) |

## Phase 1 gate

**COLOR STUDIO PHASE 1: BLOCKED.** The production stack and targeted regression are substantially complete, but the final product gate is not closed. `docs/COLOR_STUDIO_PHASE1_CLOSURE.md` explicitly leaves native node drag threshold, insertion-line, drop, and final processing-order verification open. The presence of `DragOver`, `Drop`, and `InsertAdjustmentNode` code is implementation evidence, not proof of a real native gesture.

The native pointer artifact at `artifacts/color-studio-pointer/POINTER_WALKTHROUGH_MANIFEST.json` records PASS for wheel zoom, Fit, 100%, middle pan, pan boundary, split/side-by-side compare, sampling, Esc exit, conflict handling, and rapid interaction. Its `ProductSourceSha` is `327efd93d15790c717675aa92102d4fa78ec88f8`, which does not match this audit HEAD; it is therefore supporting evidence only, not a same-source sign-off. The runner also did not complete real node drag/drop and produced no PrintWindow screenshots because `System.Drawing.Common` could not be loaded in PowerShell 7.

## Capability matrix

| Capability | Current evidence / real implementation | Status |
|---|---|---|
| Professional mode | `TetherReferenceModeViewModel.WorkspaceMode`; `ReferenceColorWorkspaceView.xaml` | PASS (targeted WPF) |
| Adjustment stack/order | `ColorAdjustmentStack` / `ColorAdjustmentStackNode` in `ColorStudioModels.cs`; `ColorStudioRenderPipeline.Render` preserves enabled node order | PASS in core/WPF tests; native reorder unverified |
| Reference Match | `ReferenceLookMatcher`, `ColorStudioRenderPipeline.ApplyReference` | PASS |
| Color Range | `ApplyColorRange`, `SelectionWeight`, OKLab sampling | PASS |
| Positive/negative/clear samples | `TetherReferenceModeViewModel` sample commands and `ColorAdjustmentStackNode.Samples`/`NegativeSamples` | PASS |
| Show Selection | `ColorStudioRenderPipeline.ShowSelection` and `ColorStudioBitmapRenderer.RenderSelection` | PASS; preview-only |
| Keep Original Luminance | `keep_original_luminance` parameter in `ApplyColorRange` | PASS |
| Transition | `ApplyTransition` | PASS |
| Film | `PixelTartFilmPipeline.Apply`; `PixelTartFilmSettings` | PASS; cancellation covered |
| Undo/Redo | `_undoStacks`/`_redoStacks`, edit transactions in `TetherReferenceModeViewModel` | PASS |
| Simple/Professional parity | shared `AdjustmentStack`; WPF round-trip regression | PASS |
| Selected-node batch sync | `CopyCurrentLookTo` / target snapshots in `ReferenceColorWorkspaceViewModel` | PASS |
| Scheme v2 | `ColorStudioSchemeV2`, `ColorStudioSchemeStore` atomic persistence | PASS |
| Legacy migration | `ColorStudioSchemeV2.Migrate(ReferenceLook)` | PASS |
| Stop/cancel | revision + linked CTS in `TetherReferenceModeViewModel`; export CTS in workspace VM | PASS |
| Preview/export parity | `ColorStudioBitmapRenderer` and headless `ColorStudioRenderPipeline` shared by preview/export | PASS |
| Native wheel / cursor zoom | `OnPreviewCanvasMouseWheel` + `ColorStudioZoomPanState.ZoomAbout`; native runner PASS | PASS, evidence SHA caveat |
| Native pan / Fit / 100% | `PanBy`, `Fit`, `SetZoom(1)`; native runner PASS | PASS, evidence SHA caveat |
| Split / side-by-side compare | `ColorStudioSampleMapping` and shared zoom state | PASS |
| Eyedropper after zoom/pan | `ColorStudioSampleMapping.Map`; letterbox rejection tests | PASS |
| Sampling exit | Escape handler in `ReferenceColorWorkspaceView.xaml.cs` | PASS |
| Rapid interaction | revision/last-wins paths and native rapid runner | PASS, evidence SHA caveat |
| Native node drag/drop | `OnNodeDragMove`, `OnNodeDragOver`, `OnNodeDrop`, `InsertAdjustmentNode` exist | **NOT VERIFIED; OPEN P1 gate** |
| Logical DPI | fixture/evidence 35–38 and targeted WPF layout tests | PASS as logical/development evidence |
| Physical DPI | no physical 125/150/200% release-hardware run | RELEASE HARDWARE GATE PENDING |
| Photography product gate | `COLOR_STUDIO_PHOTOGRAPHY_INTERACTION_CLOSURE.md`: Product Development Gate PASS | PASS; regression frozen |
| Performance | historical comparison about +8.85% time / +11.08% peak working set | PARTIAL; P2 follow-up |
| P0 | none observed in targeted runs | 0 observed, not global absence claim |
| P1 | native node drag/drop and final UX review | OPEN |
| P2 | controlled performance comparison/optimization | NON-BLOCKING |

## Phase 2 entry decision

Phase 2 may be designed and audited, but production implementation is **BLOCKED BY PHASE 1 REGRESSION** until a same-source native pointer run proves threshold, insertion line, drop, and final processing order. Phase 2 planning below is intentionally architecture/specification only; no production UI or renderer is introduced in this audit.

## Required closure evidence

1. Run `scripts/verify-color-studio-pointer.ps1` against the current product binary.
2. Exercise real mouse-down → threshold → drag-over → insertion-line → drop for at least two destination positions.
3. Record final stack order and undo/redo result from the same source SHA.
4. Update Closure and the native QA record only if all steps pass.

