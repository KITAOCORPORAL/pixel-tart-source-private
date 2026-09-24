# Color Studio Phase 2 Architecture Map

**Status: CORE PARTIAL — UI INTEGRATION BLOCKED BY PHASE 1 REGRESSION.** The implemented core reuses the existing Color Studio pipeline; it does not introduce a second color system.

| Concern | Existing class/file | Phase 2 responsibility | Input / output | Threading / ownership |
|---|---|---|---|---|
| Pixel storage | `VisualPixelBuffer` in `src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisModels.cs` | bounded proxy for point-cloud extraction | RGB24 buffer → sampled colors | worker-owned immutable snapshot |
| Color-space proxy | `ColorSpaceProxyBuilder`, `ColorSpaceCloud`, `ColorSpacePoint` in `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioColorSpace.cs` | deterministic adaptive grid proxy with source relationship | RGB24 → bounded OKLab points | caller worker; cancellation-aware |
| Proxy cache | `ColorSpaceProxyCache` in `ColorStudioColorSpace.cs` | bounded LRU by asset/version/settings/color state | cache key → immutable cloud | thread-safe core ownership |
| Camera/projection state | `ColorSpaceCameraState`, `ColorSpaceCoordinate` in `src/RAWSelectionAssistant.Core/Services/Projects/ColorSpaceCamera.cs` | stable orbit/zoom/reset and normalized L*/a*/b* coordinates | gestures/OKLab → camera or coordinate | UI-framework-neutral state |
| Sample/cluster linking | `ColorSpaceLinking` in `ColorStudioColorSpace.cs` | nearest point, positive/negative markers, preview cluster indices | working OKLab → point/indices | pure deterministic functions |
| Working color | `OklabColorSpace` in `ReferenceColorCoreV2.cs` | convert sampled sRGB to OKLab D65 | `VisualRgb24` → `OklabColor` | CPU worker; deterministic |
| Reference match | `ReferenceLookMatcher` / `ReferenceLook.cs` | source/target distribution context | pixels + analysis + look → preview | existing cancellation token |
| Color range | `ColorStudioRenderPipeline.ApplyColorRange` | reuse positive/negative sample weighting | OKLab sample distances → selection weights | worker; cancellation every bounded batch |
| Selection preview | `ColorStudioRenderPipeline.ShowSelection` and `ColorStudioBitmapRenderer.RenderSelection` | show cluster/selection mask without mutating stack | node input + mask → diagnostic frame | worker; preview-only |
| Ordered render | `ColorStudioRenderPipeline.Render` | provide node input/output context to visualization | stack → `NodeInputs`/`NodeOutputs` | worker; no UI-thread heavy work |
| WPF preview adapter | `ColorStudioBitmapRenderer` | convert proxy/selection frames to `BitmapSource` | buffer → frozen bitmap | worker then UI property assignment |
| View state | `TetherReferenceModeViewModel` | own panel open/selection/cancellation and link state | canvas sample + selected node → observable state | UI-owned state; render jobs cancellable |
| Canvas mapping | `ColorStudioSampleMapping` + `ColorStudioZoomPanState` | canonical Canvas ↔ source coordinate mapping | displayed point + mode/split/state → source pixel | UI event path; no duplicate mapping |
| Batch isolation | `ReferenceColorWorkspaceViewModel` | freeze stack/look/film snapshots before export | target snapshot → export | sequential export with CTS |
| Scheme persistence | `ColorStudioSchemeStore` | persist only existing stack model | scheme v2 → atomic JSON | store-owned file gate |
| Film/transition | `PixelTartFilmPipeline`, `ColorStudioRenderPipeline.ApplyTransition` | remain downstream nodes; not part of point-cloud UI | buffer → buffer | worker + cancellation |

## Proposed Phase 2 data flow

`Displayed Pixel → ColorStudioSampleMapping.Map → source coordinate → proxy pixel → OklabColorSpace.FromSrgb → bounded OKLab point sample → 3D projection (L*, a*, b*)`.

The reverse path is `cluster hit → selected OKLab predicate → existing ColorRange-style mask/ShowSelection preview`. It must not automatically write a `ColorAdjustmentStackNode`.

## Explicit boundaries

- `ReferenceCubeLut` remains the existing reference LUT path; no 3D LUT editor is implied.
- `PixelTartFilmPipeline` remains a pixel effect and is not represented as a cloud effect.
- No GPU path, WPF 3D scene, production panel, or native visual hit-testing currently exists: **NOT IMPLEMENTED**.
- Canvas/WPF integration must be introduced only after Phase 1 native drag/drop closure; the current linking functions deliberately do not own viewport coordinates or mutate the adjustment stack.
