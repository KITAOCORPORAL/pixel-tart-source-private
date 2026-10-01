# Post-human-review gap matrix

Baseline: `4e4f22c15bba7d13a72fb27f0960c2699605e598` (`integration/pixel-tart-developer-preview`)

This matrix records code evidence separately from runtime evidence. `CLOSED_IN_CODE` means the contract and implementation are present in the current tree; it does not claim a fresh human runtime pass.

| Issue | Current implementation | Commit that addressed it | Still reproducible? | Status |
|---|---|---|---|---|
| HR-001 Dimension import | Import decodes source dimensions into `AssetItems.Width/Height`; import acceptance covers JPEG/PNG/TIFF. | `7a87d3b`, `bb88db9` | Fresh 6000x4000 and EXIF 6/8 runtime check is pending. | NEEDS_RUNTIME_VERIFY |
| HR-002 Old dimension backfill | Backfill repository and lifecycle scheduling update missing dimensions asynchronously. | `15b8f1c` | Existing home smoke covered missing dimensions; this round's fresh run is pending. | NEEDS_RUNTIME_VERIFY |
| HR-003 Rating click | `PixelTartRatingControl` is bound to the shared rating command and supports hover/click semantics. | `371dba0`, `bb88db9` | Full 0→1→3→5→clear sequence needs runtime verification. | NEEDS_RUNTIME_VERIFY |
| HR-004 Rating persistence | Rating is persisted in repository and shared by inspector, grid, popup, and query state. | `371dba0` | Restart sequence needs fresh runtime evidence. | NEEDS_RUNTIME_VERIFY |
| HR-005 Color/rating popup separation | Filter popup commands and views keep color, rating, tag, and date responsibilities separate. | `a8e801f` | No new regression observed in code; fresh UI run pending. | NEEDS_RUNTIME_VERIFY |
| HR-006 Popup mutual exclusion | Shared popup coordination closes the active popup before opening another. | `a8e801f`, `bb88db9` | Fresh click-through at multiple DPIs pending. | NEEDS_RUNTIME_VERIFY |
| HR-007 Popup overflow | Smart Folder group headers now use a stacked Grid, buttons wrap on a second row, and the rule tree has vertical-only scrolling. | This round | Fresh 100/125/150/200% runtime matrix is still pending. | NEEDS_RUNTIME_VERIFY |
| HR-008 Context submenu collision | `ContextMenuPlacement` and submenu monitor-aware placement are implemented and covered by placement tests. | `15b8f1c`, `a8e801f` | Native two-level menu run is pending. | NEEDS_RUNTIME_VERIFY |
| HR-009 Thumbnail max scale | Thumbnail sizing derives target item width from viewport and preserves `Stretch=Uniform`. | `bb88db9` | Fresh min/middle/max UI run pending. | NEEDS_RUNTIME_VERIFY |
| HR-010 Smart Folder click | Smart Folder selection updates query state and gallery; unsaved guard is present. | `bb88db9` | Fresh runtime click-through pending. | NEEDS_RUNTIME_VERIFY |
| HR-011 Tag Group click | Tag Group command updates query state and selected organization node. | `bb88db9` | Fresh runtime click-through pending. | NEEDS_RUNTIME_VERIFY |
| HR-012 Folder text clipping | Organization rows have trimming in several templates, but a complete folder-row geometry contract is not present. | `bb88db9` | Long-name clipping at scaled layouts is not closed. | PARTIAL |
| HR-013 Duplicate dialog size | Duplicate Finder remains 1080px wide; duplicate import comparison is now 1000px wide with 320px image panes and uniform aspect rendering. | This round | Fresh native comparison run is pending. | NEEDS_RUNTIME_VERIFY |
| HR-014 Visual similarity explanation | Similarity query and visual fingerprint expose real color/aspect/composition data; no person detector score is fabricated. | Existing visual-analysis commits | UI explanation dimensions are not fully surfaced. | PARTIAL |
| HR-015 Quick Loupe | Magnifier-only hover/click handlers open a centered popup; leave closes it; ordinary card hover does not call the opener. | `bb88db9` | Fresh native interaction run pending. | NEEDS_RUNTIME_VERIFY |
| HR-016 Inspector Export | Inspector has a real `ExportSelectedOriginalCommand` button and shares publishing/export commands. | `bb88db9` | Fresh file export run pending. | NEEDS_RUNTIME_VERIFY |
| HR-017 HSL card layout | Inspector palette cards now show HEX, H (degrees), S/L (percent), percentage, and a real Copy HSL button sourced from `DominantColor`. | This round | Fresh visual runtime check is pending. | NEEDS_RUNTIME_VERIFY |
| HR-018 RGB histogram | `HistogramR/G/B/Luma` are computed from real pixel buffers and drawn by `HistogramDrawing`. | Existing visual-analysis commits | Inspector default/composite presentation needs runtime verification. | NEEDS_RUNTIME_VERIFY |
| HR-019 Tone zone distribution | Inspector histogram tab now renders RGB histogram followed by 0–X zone distribution bars bound to `Analysis.ZoneDistribution.Ratios`; tone/contrast remain supporting text. | This round | Fresh visual runtime check is pending. | NEEDS_RUNTIME_VERIFY |
| HR-020 Header New context | Header actions are route-aware for Asset Library/Planning/Canvas; no universal New action remains in Asset Library. | `bb88db9` | Full route matrix runtime run pending. | NEEDS_RUNTIME_VERIFY |
| HR-021 Header Import context | Import action is owned by Asset Library context; other routes are not supposed to expose a generic import. | `bb88db9` | Full route matrix runtime run pending. | NEEDS_RUNTIME_VERIFY |
| HR-022 Planning custom item | Planning uses existing document/block persistence and supports custom planning documents. | `bb88db9` | Fresh create/edit/save/reopen run pending. | NEEDS_RUNTIME_VERIFY |
| HR-023 Color Studio professional-only | No active Simple/Professional branch was found in the current source; legacy preference migration is not documented. | Existing Color Studio commits | Product layout still needs audit. | PARTIAL |
| HR-024 Reference Navigator | Color Studio has image viewport and zoom/pan state, but a dedicated larger navigator contract was not found. | Existing Color Studio commits | Navigator remains unverified/not closed. | NOT_IMPLEMENTED |
| HR-025 Duplicate Target/Reference label | Match workflows expose target/reference metadata in multiple surfaces; duplicate-label cleanup has no explicit contract. | Existing Color Studio commits | Duplicate label risk remains. | PARTIAL |
| HR-026 Match 0% identity | Strength zero path returns identity semantics and is covered by core tests. | `7a87d3b` | JPEG/TIFF16/high-precision preview/export parity run pending. | NEEDS_RUNTIME_VERIFY |
| HR-027 Filmstrip | Color Studio/selection foundations exist, but multi-select filmstrip behavior is not covered by a current end-to-end contract. | Existing Color Studio commits | Not closed. | PARTIAL |
| HR-028 Sync selected | Batch sync services exist, but selected-target UI flow and field protection need verification. | Existing batch workflow commits | Runtime flow not proven this round. | NEEDS_RUNTIME_VERIFY |
| HR-029 Batch export | Publishing pipeline exists; cancellation and per-item failure reporting are not covered by this round's evidence. | Existing publishing commits | Runtime flow not proven. | NEEDS_RUNTIME_VERIFY |
| HR-030 Preview/export parity | Shared render pipeline foundations exist, but parity tolerances across proxy/full resolution are not currently evidenced. | Existing Reference Match commits | Not closed. | PARTIAL |
| HR-031 3D image→cloud link | 3D viewport and sample mapping foundation exist; preview highlight link is not wired as a verified product interaction. | Existing Color Studio commits | Not proven. | NOT_IMPLEMENTED |
| HR-032 3D cloud→image link | Sample mapping foundation exists; reverse image highlight is not wired as a verified product interaction. | Existing Color Studio commits | Not proven. | NOT_IMPLEMENTED |
| HR-033 Free Canvas toolbar | Free Canvas v1 tools, edit, view, canvas, and project actions are present in `FreeCanvasView`. | Existing Free Canvas commits | Fresh toolbar runtime run pending. | NEEDS_RUNTIME_VERIFY |
| HR-034 Free Canvas single zoom | `FreeCanvasSurface.Zoom` is a single state and toolbar renders it; duplicate display audit is pending. | Existing Free Canvas commits | Fresh run needed to verify no duplicate zoom labels. | NEEDS_RUNTIME_VERIFY |
| HR-035 Free Canvas aspect ratio | Canvas document/editor preserve image object geometry in core; explicit multi-ratio regression coverage is not present. | Existing Free Canvas commits | 3:2/4:3/1:1/16:9/portrait/tall runtime suite pending. | PARTIAL |

