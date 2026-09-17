# Visual Intelligence & Publishing Pipeline v1 — implementation report

Status: **PARTIAL — not a release acceptance**. Branch: `integration/pixel-tart-developer-preview`. Baseline source: `1db32244dc3ab1d5770ba27b3b978bb06083c9c2`; staged implementation commits `9114231`, `9810049`, `9c2f116`, `c5d563e`. This report distinguishes implemented code, executable tests, automated real-App captures, and unperformed human/physical-machine checks.

## Visual Analysis

| Status | Scope and evidence |
| --- | --- |
| DONE | Unified RGB proxy analysis for 3/5/7 swatches, group aggregation, luminance/RGB histograms and clipping, 11 zones, non-destructive monochrome/zone previews, similarity signature, fingerprint/version-keyed cache and background decode. Contextual library, inspiration-board and Free Canvas palette/monochrome references use it. Seven real `App.xaml`/`MainWindow` dark-theme captures are in `artifacts/creative-intelligence-v1/visual-analysis/`. |
| PARTIAL | Canvas palette object stores structured data and sources, but complete UI manipulation acceptance on an ordinary photographer's machine has not happened. The zone-map screenshot demonstrates the visual mapping, not hover highlighting. |
| NOT IMPLEMENTED | Zone hover → matching-region highlight, one-click HSL copy, project palette/tone persistence and Planning data consumer/UI. No claim of 10,000-image responsiveness or physical DPI acceptance. |

## Duplicate Finder

| Status | Scope and evidence |
| --- | --- |
| DONE | Exact content-fingerprint groups, cached perceptual similar groups, adjustable strictness, horizontal comparison and advisory keep suggestion; recoverable library trash never deletes disk source. Inspiration-board, canvas and project references are counted. Multi-source replacement takes snapshots before writes and compensates on an ordinary failure; tests include a failure on the second source restoring the first. Five real-App captures are in `artifacts/creative-intelligence-v1/duplicate-finder/`. |
| PARTIAL | Import policy defaults to skip exact duplicates and a three-decision guard exists, but import UI does not offer the photographer all `跳过 / 查看 / 仍然导入` options. Scan/usage lookup is asynchronous but sequential; large-library performance remains unmeasured. The replacement screenshot only shows resulting UI state, not transactional proof. |
| NOT IMPLEMENTED | Crash/power-loss-atomic journal across SQLite, board and canvas stores; persisted Planning reference participant. Consequently the requested unconditional atomic reference-safety guarantee is **not met**. No permanent-delete path was added. |

## Publishing Export

| Status | Scope and evidence |
| --- | --- |
| DONE | Independent dimension/compression and watermark switches; original/long-edge/exact-box aspect-preserving sizing; JPEG quality and optional metadata; multiple image/text layers; source-safe collision-free temporary output; folder input for JPG/JPEG/PNG; previous/next background preview; local presets; Task Center export/progress/cancel/failure state. A renderer-through-export source-safety test hashes the source and logo before/after compression-only, watermark-only, combined, image HSL and text variants. Ten real-App captures are in `artifacts/creative-intelligence-v1/publishing/`, including a completed Task Center entry. |
| PARTIAL | HSL, opacity, proportional logo size, named nine-position selection and text fields are modeled/rendered, but typography, offsets/margins and restore-original-color controls are not all exposed in the page. Preset names are generated rather than entered. Task requests are in-memory and do not resume across restart. Task Center handles cancellation; the page has no local cancel button. |
| NOT IMPLEMENTED | A visual 3×3 position picker, rendered text letter-spacing, automatic project-default-preset selection and guaranteed recovery of queued exports after a process restart. TIFF is explicitly unsupported. |

## Evidence and gate

- App captures: **22 PNG + 22 adjacent layout metadata files** (7 + 5 + 10), generated from the real application shell/theme with synthetic source photographs. Metadata marks logical layout pass; it explicitly says `physicalDpiManuallyTested=false`. These are not mock page screenshots, but also not photographer or multi-machine acceptance. Individual picture titles are not a substitute for interaction evidence.
- Core suite passed **1,326/1,326, 0 failed, 0 skipped**, including the new group rollback test. Focused WPF publishing renderer/source-safety tests passed **7/7**, and isolated collage export tests passed **3/3**. The full WPF run recorded in `tests/RAWSelectionAssistant.WpfTests/TestResults/creative-v1-full.trx` had **1,216 passed / 16 failed / 1 skipped / 1,233 total**, before the isolated collage-rendering fix. The 16 were 3 collage exports cancelled by a shutting-down global dispatcher (now rendered on a dedicated STA; isolated retest passed) and 13 shared-`Application` lifetime/STA contamination failures in calendar, booking and layout test classes. A full post-fix rerun has **not** passed. The skipped opt-in 10,128-record diagnostic requires a fresh fixture and output directory; it was not falsely converted into a pass. Required **0 failed / 0 skipped gate: NOT MET**.
- No ordinary user's computer, 100/125/150/200% physical DPI matrix, photographer workflow interview, installer/reinstall cycle or real 10,000-photo benchmark is evidenced by this v1 work. The separate RC12 physical acceptance remains open. Do **not** make a formal Release Candidate on the strength of these screenshots.

See `docs/design/VISUAL_ANALYSIS_SPEC.md`, `DUPLICATE_FINDER_SPEC.md`, `PUBLISHING_EXPORT_SPEC.md` and `docs/architecture/VISUAL_INTELLIGENCE_PIPELINE.md`, `PUBLISHING_PIPELINE.md` for boundaries and contracts.
