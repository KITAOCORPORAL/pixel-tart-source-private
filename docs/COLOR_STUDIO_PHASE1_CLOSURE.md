# Color Studio Phase 1 — product UX closure

**Status: BLOCKED.** Implementation and targeted regression are substantially complete. A repeatable Win32 native pointer runner now verifies the production zoom, fit, pan, sampling, compare, cancellation, and rapid-interaction paths. Native node drag insertion/drop remains unverified, so the remaining product gate is still open.

## Source and build

- Start: `139ff521a79ee5e099b1d46f92e13b904a51e4de`.
- Latest compiled/tested product: `c175ebe` (`feat(color): add retry paths for failed targets and exports`).
- Screenshot/performance binary: `7bddb78db242345fc2b47c3cbbb14fa5bd135ee5`.
- Later delta: fix removal of an updated scheme by stable ID and extend scheme regression. No screenshot layout change, but captures are **not relabelled** as the newer binary.
- Windows-native .NET SDK **10.0.401**, Release x64 **PASS, 0 warnings, 0 errors**. Recovery-source build was rebuilt after commit `c175ebe`.
- Production project `RAWSelectionAssistant.csproj` configures executable `KitaoPhotoSelector.exe`; captures use its normal MainWindow/XAML/ViewModel/renderer, not a separate acceptance window.
- No WSL, Docker, VM, administrator installation or system DPI changes. The following evidence-only commit is not claimed as the source of earlier binaries.

## Implementation and regression

| Gate | Actual result |
|---|---|
| Shared zoom/pan | Implemented 25–400%, fit, 100% logical image pixels, cursor-centred wheel, space-left/middle drag, double-click fit, clamp. Four compare modes share one state. Six mapping/state tests PASS; production linked view capture 22. Win32 SendInput walkthrough verified wheel, fit, 100%, middle-pan, boundary, split, and side-by-side paths. |
| Eyedropper | Inverse displayed-rect mapping, zoom/pan, split, linked side-by-side, proxy resolution and letterbox rejection PASS in tests. |
| Node row | 36 DIP row, inline enable, disabled opacity, original nonemoji type symbols, name/strength, selection wash, overflow/rename implemented; bottom button wall removed. Production-view toggle/selection/menu tests PASS. |
| Drag feedback | Insertion border implemented, reorder/history commands tested. **PARTIAL:** 25 is a production routed `DragOver` hook (`DRAG_OVER_HOOK`); native pointer walkthrough is unavailable and is not claimed. |
| Scheme | Cards, select then apply, save/update/save-as, unsaved save/discard/cancel, delete confirmation, restart persistence PASS in command/store regression. Captures 13/19/20 show real view/popups. Updated-scheme deletion fixed by stable ID. Native click-through QA remains PARTIAL. |
| Batch sync | Choice popup/count, selected-node versus whole-adjustment actions, toast and isolation implemented; regression PASS. Captures 12/21/23 show popup/toast/logical DPI. |
| Rapid switching | Bounded reference A–E and scheme A–D last-wins tests PASS. Target activation reserves order before asynchronous thumbnail completion. |
| Render recovery | Injected post-processing exception preserves valid frame; render retry clears error/new revision; stale failed job cannot overwrite later success. 30/31 production fixture frames now show the failure and executed retry recovery. |
| Other failure recovery | Reference load/corrupt target/export tests preserve preview and recover after repair/reimport. 32 executes repair + `重试载入`; 33 executes one controlled failure + `重试失败导出` in the same output directory and records 1/1 success. |

Existing renderer, per-target snapshot model and scheme store remain. No second pipeline or Phase 2 feature.

## Production visual evidence

[Screenshot manifest](evidence/color-studio-phase1/final-ux/COLOR_STUDIO_PHASE1_SCREENSHOT_MANIFEST.json): **24 main images + 6 independent popup images**; required 01–18 all have loaded synthetic targets, real parameters/nodes.

Cursor-free **PrintWindow(PW_RENDERFULLCONTENT)** records PID/HWND, popup HWND, source, app version, fixture, DPI, UTC time and SHA256. No compositing/mock/cursor access. Fixture `color-studio-still-life-v1` generates safe repository-defined 1200×800 still-life PNGs inside an explicitly isolated runtime; no customer photos.

All 18 required frames were opened individually, not just a contact sheet; supplemental popups and changed frames were also inspected. Refreshed 25–38 evidence was reviewed at native image dimensions. The bounded record is in [`COLOR_STUDIO_PHASE1_NATIVE_PIXEL_QA.md`](COLOR_STUDIO_PHASE1_NATIVE_PIXEL_QA.md); 25/29 remain PARTIAL because native pointer input is unavailable, and logical-200% remains simulation only.

| Image | Review / limit |
|---|---|
| 01 Professional | Loaded target, four nodes and inspector. |
| 02 Stack | Reference selection and ordered rows. |
| 03 Reference | Production parameters. |
| 04 Color Range | Production inspector. |
| 05 Eyedropper | Active sampling state, not native click proof. |
| 06 Samples | Positive/negative samples after inspector scroll. |
| 07 Selection | Selection presentation visible. |
| 08 Luminance | Option in scrollable inspector. |
| 09 Reorder | Result only; 25 adds a routed DragOver insertion-border hook; native pointer input remains unverified. |
| 10 History | Controls/result; command regression covers history. |
| 11 Roundtrip | Historical frame had a processing/idle timing mismatch. Refreshed 34 records `SETTLED_ROUNDTRIP` and `Settled=true` from the same fixture state. |
| 12 Batch | Real independent popup, readable choices/count. |
| 13 Scheme | Current/my schemes and actions. |
| 14 Cancel | Post-cancel valid frame, not in-progress stop capture. |
| 15 Split | Shared source/matched geometry. |
| 16 Add | Real independently captured popup. |
| 17 1180×720 | Loaded target, usable canvas/filmstrip and scrollable compact inspector. 1770×1080 physical pixels at 150% host DPI. |
| 18 Logical 200% | Loaded-target layout simulation; not physical certification. Refreshed 35–37 cover scheme, error and node-overflow states at logical 200%. |
| 19–24 | Unsaved/delete popups, sync toast, linked zoom/pan, 200% batch popup, node overflow. |

Scoped Color Studio Chinese scan/review: **0 known unapproved English leaks**, not an exhaustive whole-app claim. Graphite/mineral/warm-silver/copper palette, themed popups and `TextValueBrush` values checked. Refreshed logical-200% scheme/error/node-overflow evidence is recorded in the native-pixel QA report.

## Tests and performance

[CLOSURE_RUN.json](evidence/color-studio-phase1/CLOSURE_RUN.json) commits portable test names/outcomes, timestamps, original run IDs and raw TRX digests; machine-specific raw results/deployment outputs remain local.

- Latest product source: **Core 23/23; WPF targeted 72/72; failures 0; skips 0**. These are targeted suites, not all solution tests.
- Coverage: Color Studio stack/film, mapping, history, real view geometry/controls, scheme persistence, switching, cancellation, processed batch pixels, snapshot isolation, parity, cache, filmstrip, metadata-related photography-critical regression and logical DPI. No separate fresh rating test result is claimed by this targeted selection.
- Model/static-XAML checks are distinguished from tests instantiating real WPF views/routed clicks; no full native gesture automation claim.
- Empty-target logical DPI core tests are not substituted for loaded production captures 17/18/23.
- 30 × 2400×1600 processed export: **1/1 PASS, 64.986 s, peak working set 857.0 MB**, initial 83.6 MB. Historical baseline 59.7 s / 771.5 MB: **+8.85% time, +11.08% peak**.
- Performance comparison **PARTIAL**: screenshot workload was concurrent; not a controlled same-host historical comparison or threshold verdict. Optimization remains P2/nonblocker.
- Test hang guards and screenshot startup/settling deadlines are bounded.

## Evidence consistency

**TRACEABLE_MULTI_REVISION / NOT SINGLE_BINARY_SIGNOFF.** Common closure RunId groups actual evidence, preserving original test-run IDs and source SHAs. Every PNG hash verified. This does not invent one historical run or conceal mixed binary revisions. Image 11 state/capture timing mismatch remains explicit. Historical RC12/organization-splitter evidence is not used for this signoff.

## Open product gates

1. Native pointer walkthrough is now available through `scripts/verify-color-studio-pointer.ps1`; wheel, fit, 100%, pan, sampling, compare, Esc, and rapid interaction passed on the production window. Native node drag threshold, insertion-line, drop, and final processing-order verification remain open. The PowerShell run produced state evidence; PrintWindow screenshot output remains unavailable in this host because `System.Drawing.Common` cannot load into PowerShell 7.
The refreshed 30–34 recovery/settled frames and 35–38 layout frames are production-fixture evidence. Physical display-DPI certification is a separate release-hardware gate, not a Phase 1 product blocker; logical 200% fixture/layout evidence is development coverage.

P0: none observed in targeted runs, not a global absence guarantee.

P1: native pointer interaction/final UX review above, not an SDK blocker. Dedicated failure retry is closed by `c175ebe`.

P2: controlled performance follow-up/optimization. No testing work assigned to the user.

## Separate gates

- 3D COLOR SPACE: **DEFERRED_TO_PHASE_2**.
- PHYSICAL DPI: **PENDING**, release hardware only, not a Phase 1 blocker.
- RC12: **HISTORICAL ONLY / NOT USED**.

**COLOR STUDIO PHASE 1: BLOCKED.** Do not advertise READY until the open product gates are closed.
