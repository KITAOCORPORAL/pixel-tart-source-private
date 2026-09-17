# Free Canvas v1 — implementation and acceptance

2026-09-17 · 2.3.0-RC12 · `integration/pixel-tart-developer-preview`

## Final disposition

**Implementation and automated acceptance complete; stop at Free Canvas v1.** The final product source is `1db32244dc3ab1d5770ba27b3b978bb06083c9c2`, verified on the remote integration branch. Report-only commits after it do not change the tested product. User-machine acceptance, photographer first-use feedback and release certification remain pending; no RC13 or main merge is authorized by this closure.

## Implementation scope

- Eagle visual closure: compact color overlay, actual parent + submenu capture, actual centered high-quality quick preview and leave-close evidence.
- Contextual Inspector: collection summary when empty; one-photo detail/annotations; multi-photo thumbnail strip, combined size and batch actions. No first-photo metadata in multi-selection.
- Quick tools receive the current ordered selection directly: full image, RAW conversion, compression, export, collage and canvas. No re-selection dialog for existing tool inputs.
- Free Canvas: reference-only initial arrangement; proportional corner resizing; move, crop, rotation/snap, mirror, duplicate, remove, order, lock, multi-selection, grouping, alignment, text, arrange, pan/zoom and gesture-aware undo/redo.
- Integration: library/context-menu/board entry; board round-trip with stable-reference deduplication; collapsible source drawer; optional project relation; saved-document reopen; debounced autosave and failed-close protection.

No RC13, main merge, force push, Planning Center product, separate Moodboard UI, clipper, AI, MCP, brushes, masks or image effects.

## Implementation commits

| Stage | Commit |
| --- | --- |
| Eagle closure | `d41ea19` |
| Contextual Inspector | `966c5c7` |
| Selection → quick tools | `93884e4` |
| Document/history core | `2c94329` |
| Crop/rotate/flip | `0862c0c` |
| Group/order/lock | `48121ef` |
| Canvas UI, board and project integration | `d9e038c` |
| Real-app evidence producer and validator | `9a4a614` |
| Batch relation readiness and thumbnail cleanup closure | `1db3224` |

All stages were pushed to the existing integration branch. Product/evidence source under test: `1db32244dc3ab1d5770ba27b3b978bb06083c9c2`. A later report-only commit does not change the verified product/test/tool source.

## Data and source safety

`CanvasDocument` owns IDs, name, optional ProjectId/PlanningSectionId and objects. An image object retains the library/asset/hash/path reference plus geometry, normalized crop, rotation, flips, order, group and lock state. Text adds plain content, font size and color. Board round-trips preserve references, not canvas geometry.

All editing/history/storage tests leave the source SHA-256 unchanged. A save target equal to a referenced source is rejected and its sentinel bytes remain unchanged. Persistent high-quality Canvas previews are readable after a source path becomes unavailable. The UI does not substitute enlarged gallery thumbnails.

Design: [PIXEL_TART_FREE_CANVAS_SPEC](../design/PIXEL_TART_FREE_CANVAS_SPEC.md). Architecture: [CREATIVE_WORKFLOW_PIPELINE](../architecture/CREATIVE_WORKFLOW_PIPELINE.md).

## Tests

- Integration checkpoint: **52/52 passed**, zero failed/skipped (`artifacts/creative-workflow-canvas/tests/integration-complete.trx`).
- Later targeted language/icon/canvas checkpoint: **23/23 passed**, zero failed/skipped (`precommit.trx`).
- **20 independent processes, 40/40 passed on final product source** for the newly explicit icon-composed toolbar readiness/menu test and the historic splitter/keyboard regression. Evidence: `artifacts/creative-workflow-1db3224/icon-repeat/`.
- Release x64 product/test build and UI-review build: **0 warnings / 0 errors**.
- Final fixes: **28/28** isolated embedded-page tests and **26/26** relation/menu/canvas/language tests passed, zero failed/skipped.
- Full current WPF process-isolated gate: **1220/1220 passed across 111 fixtures**, **0 failed / 0 skipped / 0 failed fixtures**, completed 2026-09-17 12:23 +08:00. Final manifest: `artifacts/creative-workflow-1db3224/wpf/rc12-wpf-process-isolation.json`. The previous `9a4a614` gate was 1219/1220: its one failure was the pre-canvas exact menu inventory, now updated to include the required 用于创作 entry.
- Gate scope: the existing runner excludes the opt-in `P3Diagnostic` fixture, which needs a separately provisioned 10,128-item database and diagnostic output directory. It is not counted as a pass or skip above; this gate is not a fresh large-library performance run.
- Final Release x64 product/test build was rechecked after the gate: **0 warnings / 0 errors**. No product source changed between the tested commit and this report.

Required fixture families exist: CanvasObjectTransform, Crop, Rotation, Flip, Group, ZOrder, Lock, MultiSelection, UndoRedo, SourceSafety, OfflinePreview, InspirationBoardLink and ProjectLink. Additional real-surface tests route Delete, persist/reload transformed objects, cancel crop, reject crop on locked objects and retain documents after save failure.

### Timeout investigation

The retained failing historic case is `RealSplitterDragKeepsSideWidthBindingsAndCollapseRestoresTheDraggedWidths`: attachment before initialization let Loaded disable the slider while the key assertion ran. Initialization/readiness synchronization fixes the test without increasing its one-second deadline. No retained failure identifies a separate icon-composition timeout. That specific historic root cause therefore remains unconfirmed; the explicit icon-render/menu test now checks the relevant live controls and passes all 20 repetitions. Do not conflate these two findings.

### Issues fixed during integration

- Canvas previously hid independently bound children; separate library/canvas hosts now preserve their bindings.
- Reopening saved canvases no longer requires creating a new canvas first.
- Global library shortcuts cannot intercept focused canvas commands, including toolbar focus.
- Four proportional corner handles share an anchor; continuous rotation accumulates before 15° snapping.
- Crop selection correctly maps inverse rotation and mirror; reopening crop displays the source aspect.
- Preview request width accounts for crop and zoom; surface decoded-cache budget is bounded.
- Drawer shows proportional thumbnails, retains references, and supports drag/add.
- Optional visual-analysis details remain collapsed; the regression expands the actual control before checking its swatches.
- The button inventory covers all 80 current XAML buttons, with explicit canonical styles. The language scan includes new runtime UI files but excludes binding expressions from visible-literal checks.
- Screenshot review caught batch project/booking buttons remaining disabled after multi-selection. Selection and workspace readiness now publish command availability, verified on actual bound buttons. Relation writes capture the selected assets before awaiting and retain the UI synchronization context.
- No-selection Inspector no longer shows a misleading extra `0 项` beside the actual collection count.
- Multi-dispatcher thumbnail cleanup checks dispatcher ownership before walking a visual tree; the new inspector test drains its thumbnail work before ending its STA dispatcher.

## Real application screenshots

Final producer and validator: **84/84 captures PASS**, completed 2026-09-17 12:20 +08:00, under `artifacts/creative-workflow-1db3224/visual/`. Every capture owns a real App.xaml/MainWindow process, dark theme and synthetic library; no mock canvas. Per-image JSON records source identity and layout results. Canvas metadata additionally requires loaded previews, persisted document and the relevant selected/cropped/grouped/locked/text/project state. Source files remained unchanged. Captures run with isolated lifecycles and each process exits before the next; Windows reused a numeric process ID, so globally unique process IDs are not claimed. The previous 84-capture set at `9a4a614` is retained as pre-readiness-fix evidence, not substituted for this final run.

The final manifest is `artifacts/creative-workflow-1db3224/visual/rc12-product-visual-evidence.json`: 12 product, 10 UX, 32 DPI, 6 resolution, 11 Eagle closure, 1 aspect-ratio comparison, 10 canvas and 2 contextual-inspector captures. DPI/resolution captures are logical rendering evidence, not tests on distinct physical monitors.

Required files under `free-canvas/`:

1. `01_canvas_initial.png`
2. `02_canvas_free_layout.png`
3. `03_canvas_multi_select.png`
4. `04_canvas_crop.png`
5. `05_canvas_rotate_flip.png`
6. `06_canvas_group.png`
7. `07_canvas_locked.png`
8. `08_canvas_text.png`
9. `09_canvas_inspiration_drawer.png`
10. `10_canvas_project_link.png`

`AssetInspectorNone.png` and `AssetInspectorMulti.png` supplement the single-selection product capture. The 11 Eagle closure images include actual submenu, loupe idle/active/closed, color filter and board states.

Visual review confirmed the real parent and child menu panels, compact filter without gallery-row expansion, centered non-fullscreen preview with surrounding gallery, crop toolbar, selected/grouped/locked states, text, drawer and project choice. Final multi-selection imagery confirms enabled project/booking actions; empty selection shows only collection information. The existing bright current-library tab and tall legacy context menu were not redesigned in this scope. Long project names are truncated by the bounded picker; they remain selectable in its drop-down.

Visible XAML literals were additionally scanned for `TaskId`, `P3Query`, `SHA`, `LibRaw`, `False`, `True`, `AssetId`, `Viewer`, and `Query`: **0 literal leaks**. Internal bindings, IDs and report diagnostics are not user-facing text.

## Performance and known limits

- Source images are never re-encoded for canvas transforms.
- Core-only synthetic benchmark (three runs each; source linked directly from this commit): mean Move update was 0.0067–0.0131 ms for 6 objects, 0.0567–0.0923 ms for 100 and 0.4234–0.6555 ms for 1000. The 1000-object combined scale/rotate/flip/group/order/history sequence took 63.45–99.59 ms. Evidence: `artifacts/creative-workflow-1db3224/performance/results.json`. This excludes decoding, WPF drawing and user input; it is not FPS or physical-machine acceptance. Group/selection work has higher cost than a simple move and warrants profiling before large-canvas claims.
- Shared high-quality previews are requested for the viewport plus margin; at most 128 surface images and approximately 192 MiB decoded pixels are retained.
- Document cap: 10,000 objects. This is validation protection, **not** a claim that 10K photo canvases meet an interactive performance target.
- Current automated visual scenes contain six images (plus text in the text scene). Large real-photo canvas FPS, memory and photographer first-use behavior still require user-machine measurements.
- An unavailable external library without a reusable high-quality preview shows its persistent object placeholder and offline badge. Automatic cross-library reconnection is not implemented.
- Board drawer uses the active board if populated, otherwise a combined/deduplicated list. No new board-management product is introduced.
- Historical installer and physical-acceptance report edits present before this work were preserved separately. Their older binary does not contain this creative-workflow source and is not re-certified here.

## Next stage

Automated gates and final screenshot review are complete. Stop development and ask the user to experience `素材库 → 灵感板 → 自由画布`, including first use, save/reopen and offline media. Record every place requiring guesswork. Planning Center remains interface-only. Do not label a formal release/physical-machine acceptance complete based on these logical screenshots.

Suggested user acceptance sequence:

1. Select photos in the library, check the three Inspector states and invoke a quick tool without reselecting files.
2. Put photos on an inspiration board, open them in a canvas, then send canvas references back to a board.
3. Move, resize, crop, rotate, mirror, group and lock objects; verify undo/redo and source-file preservation.
4. Add a note, use the material drawer, associate a project, close the canvas and reopen it.
5. With a safely disconnected test source, check persistent previews/offline badges; record actual latency, memory, display/DPI and any unclear interaction.

The older installed RC12 package predates these changes. It must not be used to sign off this canvas implementation; build/package provenance must be checked before user-machine testing.
