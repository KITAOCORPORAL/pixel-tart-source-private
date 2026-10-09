# Confirmed A/B/C trace

Status: NOT_READY_FOR_USER_RETEST. Implementation/automatic coverage and production native verification are distinct. `PARTIAL` below is the complete requirement, not a claim of native pass. Existing parent repair is `1a9a539`; current changes are listed in FILES_AND_TYPES.md.

| ID | Requirement / source | Implementation / test evidence | Overall / missing closure |
|---|---|---|---|
| A01 | Global Help > Language | MainWindow, StudioLanguageMenu, StudioLocalizationTests | PARTIAL: shell-wide and native restart sweep |
| A02 | Move viewing tools below photo | PhotoToolsBand dedicated bottom row | PARTIAL: pointer reachability |
| A03 | Icons, zoom and history | Existing zoom/undo commands, shared icon style | PARTIAL: shortcut and input sequence |
| A04 | Export parent | StudioExportMenu wraps existing selected/all/publishing commands | PARTIAL: native recipe and files |
| A05 | WB gradients / relative units | StudioToolPanel temperature/tint tracks | PARTIAL: native visual response |
| A06 | Dark numeric fields | Shared deep TextBox style, existing draft validation | PARTIAL: all native states |
| A07 | Group reset arrow | Vector return arrow, existing group reset and history | PARTIAL: native group sweep |
| A08 | Color swatches/eyedropper | Existing ColorRange samples, basic swatch node selection | PARTIAL: samples/exclusion/Esc |
| A09 | Four balance wheels | StudioColorBalanceWheel uses existing hue/amount keys and transaction | PARTIAL: pointer and per-zone pixels |
| A10 | Levels graph | StudioLevelsGraph existing five parameters, true histogram | PARTIAL: native handles/channel sweep |
| A11 | Reference thumbnails | Shared ICC/orientation detached loader, async revision, bounded384 thumbnail | PARTIAL: native remove/reopen |
| A12 | Reference list hierarchy | Thumbnail/name/weight/actions, advanced engine expander | PARTIAL: long names and actual files |
| A13 | Wheel zoom stuck | Fit-relative minimum, finite checks, zoom regression failed-before/pass-after | PARTIAL: continuous physical wheel/pan |
| A14 | Curve starvation | Serialized latest-input proxy pump and settled final render | PARTIAL: native baseline/final median/P95 |
| A15 | Workflow copy | MainWindow concise title/warnings and tooltip help | PARTIAL: actual workflow tasks |
| B01 | Default layout migration | Left auxiliary / center photo / right tools | PARTIAL: exact missing original reference |
| B02 | Right RGB/Y/tone | Tone entry outside histogram internal scroll | PARTIAL: native smallest window |
| B03 | Truthful histograms | No fake drawing floor; alpha-aware statistics and labeled domains | PARTIAL: natural photo audit |
| B04 | Sphere/sample semantics | Existing smooth sRGB-normalized OKLab surface and true samples retained | PARTIAL: native mapping and limits |
| B05 | Expanded3D (CU-02) | Same viewport/camera/settings in expanded auxiliary dock | PARTIAL: orbit/pan/zoom/Esc |
| B06 | Filmstrip dock/view | Bottom/right, thumbnails/list/slideshow timer; existing selection/filter | PARTIAL: native playback/save/reopen; exact C1 missing |
| B07 | Five categories | Seven old pages mapped without changing stack execution order | PARTIAL: all native controls |
| B08 | DPI/reachability (CU-01) | Automated DIP bounds; no font-size workaround | PARTIAL: actual3 windows ×4 OS scales |
| B09 | Draft/Esc (CU-05) | Existing numeric validation, target identity, rollback before shell | PARTIAL: native Enter/Tab/blur/Esc |
| B10 | Frozen sync (CU-03) | Existing frozen target objects/source/adjustments drive summary and execution | PARTIAL: native boundary workflow |
| B11 | Locale (CU-04) | Three resources, live import summary fix, stage source labels | PARTIAL: legacy composed errors/full shell audit |
| B12 | Tone inspection (CU-06) | First-screen entry/clear on collapse; existing pixel mapping | PARTIAL: hover/leave/Esc/export isolation |
| B13 | Rename folders/tags | Existing OrganizationRename and repository integrity tests retained | PARTIAL: native conflict/cancel/restart |
| B14 | Canvas menus | Copy fixed to clipboard; duplicate explicit; transform/arrange submenus; absent callbacks omitted | PARTIAL: topbar/menu full native sweep |
| B15 | Release identity | Exact source revision embedded, payload hashes, isolated real process | PARTIAL: desktop shortcut click not validated |
| B16 | Whole workflow | Full Core/WPF/module regression; separate95 native ledger | BLOCKED: current input/capture connection |
| C01 | Five parent categories | Same as B07 | PARTIAL |
| C02 | Resize/persist layout | ColorStudioLayout optional session field, safe normalization | PARTIAL: cross-process native |
| C03 | Controls/history | Existing stack transactions, graphical wheel/levels tests | PARTIAL: complete input sweep |
| C04 | Reference compare/link | Read-only inspection window; optional one-way target zoom/pan follow | PARTIAL: native compare and independently navigating |
| C05 | Color/skin/sampling | Existing range and skin nodes, new continuous reference color protection | PARTIAL: actual skin/background suite |
| C06 | Left presets/right controls | Existing preset flow preserved | PARTIAL: native XMP/hover/reload |
| C07 | Node-input/domain correctness | Version2 current pixels / OKLab L statistics+curve+apply; v1 retained | PARTIAL: candidate covariance/Bures comparison not implemented or measured |
| C08 | Weighted references | Deterministic alpha-aware stratified65536 proxy statistics and weighted references | PARTIAL: real-photo distribution/quality comparisons |
| C09 | Safety/compatibility | Strict zero identity; bounded shifts kept; V4 explicit unsupported feedback existing | PARTIAL: native fallback/whole LUT matrix |
| C10 | Precision limitations | Float encoded sRGB documented, not scene-linear RAW; old looks remain v1 | PARTIAL: visual approval and comparative output suite |

Exact historical `image(20261005-022726).png` and C1 reference unavailable. Five available 3/4/5/8/9.png candidates cannot be asserted identical to requested `(2)` originals. Private source images are not published.
