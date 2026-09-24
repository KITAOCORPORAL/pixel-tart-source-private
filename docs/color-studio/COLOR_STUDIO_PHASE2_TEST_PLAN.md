# Color Studio Phase 2 Test Plan

**Status: PLANNED — BLOCKED BY PHASE 1 REGRESSION.** These are planned names and gates, not claimed test results.

| Planned test | Purpose |
|---|---|
| `ColorSpaceProjectionTests` | deterministic L*/a*/b* projection and orbit math |
| `OKLabPointMappingTests` | sRGB ↔ OKLab bounds and gamut behavior |
| `ColorSpaceSamplingDeterminismTests` | same proxy/input produces same point reduction |
| `CanvasToColorSpaceLinkTests` | displayed pixel maps through `ColorStudioSampleMapping` |
| `ColorSpaceToCanvasSelectionTests` | cluster predicate requests preview-only mask |
| `PositiveSampleHighlightTests` | positive sample visual state |
| `NegativeSampleHighlightTests` | negative sample visual state |
| `ZoomPanSampleMappingRegressionTests` | Fit/100%/zoom/pan coordinate invariance |
| `SplitCompareColorSpaceMappingTests` | split and side-by-side source selection |
| `ColorSpaceCancellationTests` | cancellation during reduction/projection |
| `ColorSpaceProxyPerformanceTests` | bounded memory/time on large source proxies |
| `ColorSpaceLogicalDpiTests` | compact/wide panel layout and hit targets |

Evidence must record source SHA, proxy dimensions, point count, cancellation result, and whether the run is logical or physical DPI. A test passing on a fake point list is not product evidence.

