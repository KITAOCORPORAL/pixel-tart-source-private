# Color Studio current implementation map

Audited at `b6a21d692a8745b58d827e2da7910335706456cd`. This maps actual production types, not planned node names.

| Concern | Actual implementation | Current boundary |
|---|---|---|
| Production page | `ReferenceColorWorkspaceView` in `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml` and code-behind | MainWindow hosts this view as the reference-color toolbox page. The existing 简洁/专业 selector changes `TetherReferenceModeViewModel.WorkspaceMode`, but currently only hides/reveals some controls. |
| Shared editor state | `TetherReferenceModeViewModel` in `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs` | Holds reference look, film, source/matched frame, preview state and cancellation. This is the existing simple editor, not a second professional editor. |
| Batch/target snapshot | `ReferenceColorWorkspaceViewModel`, `ReferenceTargetItem` in `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs` | `ColorAdjustmentStackSnapshot` is captured at export start. `SyncSelectedColorNodes` currently merges selected IDs; UI is not wired. |
| Stack/scheme model | `ColorAdjustmentStack`, `ColorAdjustmentStackNode`, `ColorStudioSchemeV2`, `ColorStudioLegacyMigration`, `ColorStudioSchemeSerializer` in `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioModels.cs` | Four `ColorStudioNodeType` values: `ReferenceMatch`, `ColorRange`, `Film`, `TransitionBlend`. No separate `ReferenceMatchNode` class. Scheme v2 serialization exists but is not a product store or UI action. |
| Headless renderer | `ColorStudioRenderPipeline` in `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioRenderPipeline.cs` | Linear OKLab color-range selection and separate `ShowSelection`; no negative samples or editor undo stack yet. |
| WPF pixel adapter | `ColorStudioBitmapRenderer` in `src/RAWSelectionAssistant/Services/ColorStudioBitmapRenderer.cs` | Converts BitmapSource to the same core buffer and back. |
| Preview entry | `TetherReferenceModeViewModel.PreviewColorStudioAsync` | Explicit API only. The production `RenderAsync` still uses `ReferenceLookPreviewService` for the existing simple look; stack editing is not yet wired to automatic preview. |
| Export entry | `ReferenceColorWorkspaceViewModel.ExportAsync` → `TetherReferenceModeViewModel.ProcessForExportAsync` → `ColorStudioBitmapRenderer` when a stack snapshot exists | Existing encoder and target isolation boundary; otherwise legacy reference/film path. |
| Existing look store | `ReferenceLookStore` in `src/RAWSelectionAssistant.Core/Services/Projects/ReferenceLookStore.cs` | Version 1 `reference-looks.json`; does not persist `ColorStudioSchemeV2`. |
| Existing reference/film | `ReferenceLookPreviewService`, `ReferenceLookMatcher`, `PixelTartFilmPipeline` | Existing actual CPU processing. The professional stack should reuse these components and must not create a second export renderer. |

Phase 1 gaps at audit: professional stack UI, editable parameters and sampling, negative samples, presentation-only selection binding, undo/redo, per-target isolation tests, v2 persistence/actions, shared simple/pro state, processing cancellation preserving the prior frame, production screenshots. 3D color-space visualization is Phase 2. Existing 3D LUT export is a separate legacy action, not a 3D color-space UI.
