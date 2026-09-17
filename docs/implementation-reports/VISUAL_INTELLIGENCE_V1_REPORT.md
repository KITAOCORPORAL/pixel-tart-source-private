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
| DONE | Independent dimension/compression and watermark switches; original/long-edge/exact-box aspect-preserving sizing; JPEG quality and optional metadata; multiple image/text layers; editable typography, rendered letter spacing, HSL/opacity, image-relative size, margin/offset and restore-color controls; source-safe collision-free temporary output; folder input for JPG/JPEG/PNG; previous/next background preview; named local presets and active-project default selection; Task Center export/progress/cancel/failure state. A renderer-through-export source-safety test hashes the source and logo before/after compression-only, watermark-only, combined, image HSL and text variants; another renderer test verifies letter-spacing changes output. Ten real-App captures are in `artifacts/creative-intelligence-v1/publishing/`, including a completed Task Center entry. |
| PARTIAL | Nine-position selection is a named list rather than a visual 3×3 picker. Task requests are in-memory and do not resume across restart. Task Center handles cancellation; the page has no local cancel button. |
| NOT IMPLEMENTED | A visual 3×3 position picker and guaranteed recovery of queued exports after a process restart. TIFF is explicitly unsupported; folder drop reports skipped TIFF files. |

## Evidence and gate

- App captures: **22 PNG + 22 adjacent layout metadata files** (7 + 5 + 10), generated from the real application shell/theme with synthetic source photographs. Metadata marks logical layout pass; it explicitly says `physicalDpiManuallyTested=false`. These are not mock page screenshots, but also not photographer or multi-machine acceptance. Individual picture titles are not a substitute for interaction evidence.
- Core suite passed **1,327/1,327, 0 failed, 0 skipped**, including group rollback and project-default-preset persistence. The formal WPF gate passed **1,234/1,234, 0 failed, 0 skipped** in `tests/RAWSelectionAssistant.WpfTests/TestResults/creative-v1-gate-final.trx`; focused publishing renderer/source-safety/letter-spacing tests passed **8/8**, and isolated collage export tests passed **3/3**. An unfiltered historical WPF run in `creative-v1-full.trx` exposed global WPF `Application` lifetime contamination and one opt-in diagnostic skip; these were not presented as passes. The collage renderer now owns a dedicated STA, and test-only `Application` lifetime was corrected. The formal WPF gate excludes only the repository's `[TestCategory("P3Diagnostic")]` 10,128-record benchmark because it requires a separately supplied fresh fixture/output pair. That diagnostic remains unexecuted and is not a performance pass.
- No ordinary user's computer, 100/125/150/200% physical DPI matrix, photographer workflow interview, installer/reinstall cycle or real 10,000-photo benchmark is evidenced by this v1 work. The separate RC12 physical acceptance remains open. Do **not** make a formal Release Candidate on the strength of these screenshots.

See `docs/design/VISUAL_ANALYSIS_SPEC.md`, `DUPLICATE_FINDER_SPEC.md`, `PUBLISHING_EXPORT_SPEC.md` and `docs/architecture/VISUAL_INTELLIGENCE_PIPELINE.md`, `PUBLISHING_PIPELINE.md` for boundaries and contracts.
