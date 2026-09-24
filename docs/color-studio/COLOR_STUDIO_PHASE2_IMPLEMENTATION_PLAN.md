# Color Studio Phase 2 Implementation Plan

**Status: CORE PARTIAL — PRODUCTION UI IMPLEMENTATION BLOCKED BY PHASE 1 REGRESSION.**

## P2.1 — bounded color-space projection

- Goal: define a deterministic proxy/reduction contract over `VisualPixelBuffer` and `OklabColorSpace`.
- Files implemented: `ColorStudioColorSpace.cs`, `ColorSpaceCamera.cs`; existing `ReferenceColorCoreV2.cs` and `VisualPixelBuffer` remain the inputs.
- Tests: `ColorSpaceProjectionTests`, `OKLabPointMappingTests`, `ColorSpaceSamplingDeterminismTests`.
- Evidence: source SHA, proxy size, point count, deterministic digest.
- Exit gate: no UI integration; cancellation, determinism, cache bounds, and large synthetic proxy bounds pass.

## P2.2 — canvas/sample linking

- Goal: reuse `ColorStudioSampleMapping.Map` and `ColorStudioZoomPanState` for Canvas → point selection.
- Files: `ReferenceColorWorkspaceView.xaml(.cs)`, `TetherReferenceModeViewModel`, new link state only if required.
- Tests: canvas/link, zoom-pan, split/side-by-side, positive/negative sample tests.
- Evidence: native or bounded interaction trace at current source SHA.
- Exit gate: core linking has no duplicate coordinate transform and no stack mutation; WPF integration remains blocked.

## P2.3 — selection visualization

- Goal: cluster hit → preview-only selection mask using `ColorStudioRenderPipeline.ShowSelection` semantics.
- Files: existing renderer/pipeline plus a small adapter; no second renderer.
- Tests: reverse-link, cancellation, preview/export non-mutation tests.
- Evidence: before/after frame and stack hash unchanged.
- Exit gate: selected cluster never silently creates or reorders a node.

## P2.4 — UX/performance hardening

- Goal: recommended expandable inspector, DPI behavior, and bounded performance evidence.
- Files: existing workspace XAML/resources; rendering option chosen by prototype evidence.
- Tests: logical DPI, proxy performance, cancellation, accessibility/hit-target checks.
- Evidence: 1180×720 plus wide layout, source SHA, timings and peak working set.
- Exit gate: image remains primary, no meaningful preview stall, and release review approves scope.
