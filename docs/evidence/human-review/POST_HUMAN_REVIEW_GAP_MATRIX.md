> 本轮人工截图重新打开相关缺陷，最新状态见 [MANUAL_ACCEPTANCE_DEFECT_CLOSURE_2026-10-03.md](MANUAL_ACCEPTANCE_DEFECT_CLOSURE_2026-10-03.md)。不得从历史CODE/TEST推导Runtime通过。

> HISTORICAL / SUPERSEDED: user recording reopened this review. See [POST_VIDEO_RUNTIME_REVIEW_MATRIX.md](POST_VIDEO_RUNTIME_REVIEW_MATRIX.md). CLOSED_IN_CODE and old test counts below are not runtime or user approval. The former parity fixture contained unexecuted TIFF16/high-precision PASS entries; do not rely on those entries.

# Pixel Tart Human Review Gap Matrix — Round 3

Source branch: `integration/pixel-tart-developer-preview`
Source head audited: `b3ad8ad1ba73622ea005fa29a2f827043887f5d8`
The audited changes are committed at the source head. Visual evidence is current-run evidence generated from that head. `VisualApproved=false` and `UserVerified=false` remain until a photographer completes the retest.

Status values are limited to `CLOSED_IN_CODE`, `NEEDS_RUNTIME_VERIFY`, `PARTIAL`, `NOT_IMPLEMENTED`, `REGRESSION`, and `BLOCKED`.

| ID | Issue | Current implementation | Commit / source evidence | CODE | TEST | RUNTIME | Status |
|---|---|---|---|---|---|---|---|
| HR-001 | New asset dimensions | Import and metadata paths persist stored pixel width/height. | `7a87d3b`, current import tests | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-002 | Missing-dimension backfill | Background backfill schedules missing dimensions and updates the asset model. | `15b8f1c` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-003 | Rating click | Shared rating control updates the persisted asset model and query state. | `371dba0`, `fb8bda8` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-004 | Rating persistence | Rating survives refresh/restart through the shared store. | `371dba0` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-005 | Color/rating popup separation | Toolbar popup builders keep color and rating responsibilities separate. | `a8e801f` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-006 | Popup mutual exclusion | Shared toolbar popup manager closes the previous popup before opening another. | `a8e801f` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-007 | Popup overflow | Current-run visual harness covers logical DPI/resolution layout; native 150% run remains user work. | `fb8bda8`, current-run harness | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-008 | Context submenu collision | Existing monitor-aware placement is reused for nested context menus. | `a8e801f`, `fb8bda8` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-009 | Thumbnail max scale | Slider maps target size to viewport width and retains source aspect ratio. | `bb88db9`, `fb8bda8` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-010 | Smart Folder click | Smart-folder command updates the shared query and selected state. | `bb88db9` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-011 | Tag Group click | Tag-group filter command updates the shared query and gallery. | `bb88db9` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-012 | Folder text clipping | Folder rows use trimming/tooltip behavior in the organization pane. | `fb8bda8` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-013 | Duplicate dialog size | Duplicate finder has a larger comparison surface and resizable shell contract. | existing duplicate commits | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-014 | Similarity explanation | Visual embedding, palette/color, aspect ratio, and composition summaries are available; person detection is not fabricated. | existing visual analysis | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-015 | Quick Loupe | Loupe opens from its explicit affordance and double click remains viewer navigation. | `bb88db9` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-016 | Inspector export | Inspector export action routes through Publishing/export services. | existing publishing commits | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-017 | Palette runtime | Palette cards show HEX, H°, S/L%, percentage, and copy-HSL actions. | `fb8bda8` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-018 | RGB histogram | Histogram drawing consumes real visual analysis pixels and exposes RGB/luma rendering. | existing visual analysis | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-019 | Tone/zone distribution | Zone bars 0–X and hover map are present below histogram. | `fb8bda8` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-020 | Header New context | Route-aware header action mapping exists; no universal business action is assumed. | existing shell commits | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-021 | Header Import context | Import is exposed only on routes with an import contract. | existing shell commits | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-022 | Planning custom item | Planning has a real `自定义` document surface backed by existing document content fields. | existing planning commits | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-023 | Color Studio professional-only | UI now exposes one professional workspace; legacy mode values migrate in memory and the compatibility property remains for old automation. | current Round 3 source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-024 | Reference Navigator | Dedicated read-only navigator supports source preview, fit, 100%, zoom, and pan. | current Round 3 source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-025 | Target/reference label duplication | Target/reference identity bars are distinct; filename/source details remain in their relevant panel. | existing studio source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-026 | Match 0% identity | Zero-strength identity semantics exist for preview/export paths. | existing match commits | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-027 | Filmstrip | Filmstrip supports active item, rating/color control, processing status, and source aspect-preserving thumbnails. | existing studio source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-028 | Sync selected | Batch sync copies look/adjustment state to selected targets and leaves asset fields untouched. | existing studio source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-029 | Batch export partial failure | Export reports per-item status, cancellation, and a failure summary; retry failed export exists. | existing studio source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-030 | Preview/export parity | Shared production CPU processing services now have explicit V3 JPEG/TIFF16/high-precision/0% identity evidence; ICC scope is recorded honestly, V4 remains experimental and real RAW corpus is unavailable. | `artifacts/round3-final/preview-export-parity/`, `BatchExportProcessedPixelsTests`, `RawMatchTiff16ProductWpfTests` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-031 | Image → cloud highlight | Image sample maps through OKLab to a nearest cloud point and updates a preview selection. | current Round 3 source, `ColorSpaceLinking` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-032 | Cloud → image highlight | Cloud hit testing maps a selected point to source-pixel membership and renders a temporary overlay. | current Round 3 source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-033 | Free Canvas toolbar | Existing toolbar provides tools/edit/view/canvas/project actions; low-frequency canvas actions remain in More. | existing Free Canvas source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-034 | Free Canvas single zoom | Surface owns one zoom state and fit/percentage controls. | existing Free Canvas source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-035 | Free Canvas aspect ratio | Core image objects preserve geometry through move, resize, save, and reopen across eight landscape/portrait/tall/wide ratios. | `CanvasAspectRatioRegressionTests` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-036 | Professional context menu grouping | Asset menu has View/Open/Create/Visual/Organize/Workflow/Export/Manage groupings and only real actions. | current `AssetLibraryPage.xaml` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-037 | Context submenu hierarchy | Folder, tag, rating, export, workflow, and inspiration choices use child menu items. | current `AssetLibraryPage.xaml` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-038 | Context disabled states | File/open/copy/export/rating/trash commands now expose selection/file predicates and raise state changes. | current `AssetLibraryViewModel.P2Browser.cs` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-039 | Context keyboard hints | Rating choices expose 0–5 gesture hints matching the page rating contract. | current `AssetLibraryPage.xaml` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-040 | Multi-select right click | Shared policy keeps an existing selected set when the target is already selected. | `AssetLibraryContextSelectionPolicy` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-041 | Filmstrip rating/color display | Filmstrip hydrates Rating/ColorLabel from the Asset Library when AssetId is present and writes user changes back through the existing repository/presentation metadata pipeline; no-AssetId targets remain session-only. | `ReferenceColorWorkspaceViewModel`, `ColorStudioStateClosureTests.FilmstripHydratesAndPersistsAssetLibraryMetadata` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-042 | Copy Adjustments | Category picker copies only selected adjustment node types and protects rating, color label, tags, folders, filename, EXIF, and project relationship. | `SyncSelectedByTypeFrom`, `CopyApplyAdjustmentsUsesSelectedCategoriesAndProtectsAssetFields` | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-043 | Apply Adjustments | Selected-target sync applies adjustment stack snapshots with protected asset fields. | current studio VM | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-044 | Reference Navigator zoom/pan | Dedicated read-only navigator is implemented and bound to the first available reference source. | current Round 3 source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-045 | Color Studio layout | Left module rail, central target image, right reference/analysis/3D rail, and bottom filmstrip exist; responsive collapse is implemented. | existing studio source + current navigator | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-046 | Filmstrip selection contract | Single, Ctrl multi, Shift range, current target, and selected count are implemented. | existing studio VM/view | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-047 | Batch export partial failure | Per-item failed/succeeded/cancelled state and failure summary are implemented. | existing studio VM | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-048 | Image → 3D cloud highlight | OKLab nearest-point selection is connected to the image sampling path. | current Round 3 source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-049 | 3D cloud → image highlight | Projected hit test and source-pixel membership overlay are connected to the cloud click path. | current Round 3 source | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |
| HR-050 | Overlay no-layout-shift | Context/popup/rating surfaces update as overlays; logical visual harness passed without layout overflow. | `fb8bda8`, current-run harness | CLOSED_IN_CODE | CLOSED_IN_CODE | NEEDS_RUNTIME_VERIFY | NEEDS_RUNTIME_VERIFY |

## Evidence summary

- Focused Round 3 WPF set: **61 passed, 0 failed** after the navigator/3D/browser code; Copy/Apply adjustment protection adds **2 passed, 0 failed**.
- Full serial WPF gate: **1,393 passed, 0 failed, 11 skipped** in `artifacts/round3-gates/wpf-full-round3-final2/round3-wpf-full-final2.trx`.
- Full Core baseline: **1,528 passed, 0 failed, 4 skipped** in `artifacts/round3-gates/core-full-round3-final2/round3-core-full-final2.trx`.
- DPI suite: **91 passed, 0 failed, 0 skipped** in `tests/RAWSelectionAssistant.DpiTests/TestResults/round3-dpi-final.trx`.
- Current-run DPI evidence validator: **106 captures, 32 DPI states, passed**. This evidence is not a substitute for user visual approval at physical 150% scaling.
- Preview/export parity fixture evidence: `artifacts/round3-gates/preview-export-parity/README.md`; dedicated proxy/full-resolution corpus parity remains a user/corpus verification item.


