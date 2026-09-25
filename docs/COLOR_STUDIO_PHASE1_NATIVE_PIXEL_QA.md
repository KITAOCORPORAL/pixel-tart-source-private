# Color Studio Phase 1 — Native-pixel QA record

Run: `color-studio-phase1-ux-gates-20260924`; native drag evidence source SHA: `8e8f16cab28930cc2ba5456ceac08445c32835f5`.

The source PNGs were inspected at their native dimensions using the repository image viewer with `original` detail. No resampling or derived contact sheet was used for the checks below. This is a bounded evidence review, not a physical-DPI certification.

| Evidence | Native size | Regions reviewed | Result | Limits |
|---|---:|---|---|---|
| 25 drag insertion | 2400×1500 | node rail, selected row, insertion border | PARTIAL | visual frame remains unavailable; same-source Win32 matrix is recorded separately in `artifacts/color-studio-pointer/NODE_DRAG_WALKTHROUGH_MANIFEST.json` |
| 26 cursor zoom | 2400×1500 | canvas, zoom controls, rails, filmstrip | PASS (fixture) | state-driven zoom, not native wheel input |
| 27 linked compare pan | 2400×1500 | split canvas alignment, rails | PASS (fixture) | state-driven pan, not native middle-drag input |
| 28 fit after pan | 2400×1500 | full canvas restoration, controls | PASS (fixture) | state-driven Fit |
| 29 eyedropper after zoom/pan | 2400×1500 | sampling state, canvas, inspector | PARTIAL | mapping hook; native pointer sample unavailable |
| 30 render error | 2400×1500 | retained preview, status, retry button | PASS | production render path with safe injected exception |
| 31 render retry | 2400×1500 | recovered preview, cleared error | PASS | production retry command executed |
| 32 corrupt target retry | 2400×1500 | filmstrip failure/recovery, preview | PASS | corrupt fixture repaired, production retry-load command executed |
| 33 export retry | 2400×1500 | export summary, filmstrip statuses | PASS | one controlled failure then production retry command; 1/1 success |
| 34 settled roundtrip | 2400×1500 | Simple/Professional state, canvas, filmstrip | PASS | `IsSettled=true` recorded in same fixture state |
| 35 scheme at logical 200% | 2700×1800 | scheme rail, text, buttons, canvas | PASS (logical) | simulated logical DPI, not physical display certification |
| 36 error at logical 200% | 2700×1800 | retained preview, error/retry controls | PASS (logical) | simulated logical DPI |
| 37 node overflow at logical 200% | 2700×1800 + popup 300×348 | node menu, popup bounds, labels | PASS (logical) | simulated logical DPI |
| 38 compact popup | 1770×1080 + popup 420×403 | compact popup, close-safe area, labels | PASS (fixture) | host capture is 1180×720 at 150% physical DPI |

Global visual notes: graphite/mineral surfaces, warm-silver values, oxidized-copper accent, Chinese labels, and themed popups are present in the reviewed frames. Physical DPI remains a release-hardware gate. Native node drag behavior is verified by same-source Win32 state/ordering evidence, but native visual confirmation is explicitly unavailable in this host and is not inferred from state evidence.

## Native production capture diagnostic

Run `native-capture-20260925-052506`, source SHA `d00df6ab75437e05cd0f696db8d53975a65275ed`. The validator accepted 100/100 stability frames; the run contains 195 real Production WPF PNGs. `PRINTWINDOW_0` was selected after testing three PrintWindow flags, window BitBlt, and desktop-region BitBlt. The drag sequence is diagnostic only: 18 cases contain intermittent observer timeout or ordering mismatch, so this run does not close Phase 1.
