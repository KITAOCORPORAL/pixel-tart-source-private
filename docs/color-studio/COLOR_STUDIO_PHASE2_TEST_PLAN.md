# Color Studio Phase 2 Test Plan

**Status: CORE PARTIAL — WPF/product gates remain blocked by Phase 1 regression.**

| Test | Purpose / current result |
|---|---|
| `ColorSpaceProjectionTests` | deterministic L*/a*/b* projection and orbit math — covered by `ColorSpaceProxyTests` |
| `OKLabPointMappingTests` | sRGB ↔ OKLab bounds and gamut behavior — existing `ColorSpaceRoundTripTests` |
| `ColorSpaceSamplingDeterminismTests` | same proxy/input produces same point reduction — PASS |
| `CanvasToColorSpaceLinkTests` | displayed pixel maps through `ColorStudioSampleMapping` |
| `ColorSpaceToCanvasSelectionTests` | cluster predicate requests preview-only mask |
| `PositiveSampleHighlightTests` | positive sample visual state |
| `NegativeSampleHighlightTests` | negative sample visual state |
| `ZoomPanSampleMappingRegressionTests` | Fit/100%/zoom/pan coordinate invariance |
| `SplitCompareColorSpaceMappingTests` | split and side-by-side source selection |
| `ColorSpaceCancellationTests` | cancellation during reduction/projection — PASS |
| `ColorSpaceProxyPerformanceTests` | bounded memory/time on 24/45/60 MP synthetic buffers — PASS, 7 proxy tests / 218 ms test time |
| `ColorSpaceLogicalDpiTests` | compact/wide panel layout and hit targets |

Evidence must record source SHA, proxy dimensions, point count, cancellation result, and whether the run is logical or physical DPI. A test passing on a fake point list is not product evidence.

Current Core evidence: `ColorSpaceProxyTests` 7/7 PASS at source checkpoint `822093f` (large synthetic dimensions 6000×4000, 7680×5760, 9500×6316; max 2048 points). WPF visual tests, physical DPI, and native 3D gesture evidence are not claimed.
