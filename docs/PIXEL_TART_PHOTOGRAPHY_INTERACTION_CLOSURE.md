# Pixel Tart Photography Interaction Closure

## Current run

- RunId: `photo-20260923T075548Z-52be8b76`
- ProductSourceSha: `52be8b76e738c1defae504e0941cb843e0caa695`
- FixtureVersion: `synthetic-reference-v2`
- GeneratedAt: `2026-09-23T08:30:21Z`
- RC12: **HISTORICAL ONLY**
- The formal manifest is stored under `docs/evidence/photography/photo-20260923T075548Z-52be8b76/` and records the initial post-processing failure plus recovery without rerunning suites.

## Toolchain and source build

- .NET Target: `net10.0-windows10.0.19041.0`
- .NET SDK: `10.0.401`
- SDK Path: user-local `.dotnet10` (resolved from the current Windows user profile)
- Install Method: existing user-level SDK; no WSL, Ubuntu, Docker, VM, or admin install
- Current Source Recompiled: **YES**
- Clean/Restore: **PASS**
- Release x64: **PASS**
- Warnings: `0`
- Errors: `0`

## Photography results

| Gate | Result | Evidence |
|---|---|---|
| Inactive Target Export | **PASS** | Real CPU A/B/C file export; B/C were never activated and all output pixels differed from source |
| Per-target Snapshot | **PASS** | Active edits freeze at export start; later UI changes do not alter queued targets |
| Preview / Export | **PASS** | Full-resolution pixel comparison; maximum channel delta `0` |
| Metadata | **PASS** | 30 rapid target switches; only final filename/dimensions/camera/lens/ISO/shutter survived |
| Filmstrip | **PASS** | Click, Ctrl, Shift, Ctrl+Shift, Ctrl+A, Esc, keyboard; active and selected remain independent |
| Rating | **PASS** | Shared Asset/Tether/Reference control; hover is preview-only and MouseLeave restores committed rating |
| Match v3 | **PASS** | P05/P50/P95, neutral, skin-like, highlight, shadow, warm, cool, cast and saturation fixtures |
| Reference Cache | **PASS** | Same reference: 2 hits, 2 misses, 2 executions; timestamp change invalidates cache |
| DPI | **PARTIAL** | In-process 100/125/150/200% renders at 1180x720; physical-display validation is not claimed |
| Performance | **PASS WITH BASELINE** | Current-source measurements recorded for import, film, batch export, cancel, filmstrip virtualization and rapid switching; 30 higher-resolution processed exports extend the baseline |

## Measured evidence

- 24MP film processing: `9387 ms`; cancellation latency: `0.85 ms`.
- Import 100: `73.4 ms`; import 500: `104.3 ms`.
- Batch export 100 synthetic targets: `197.6 ms`; process peak `81.15 MB`.
- Filmstrip 500 targets: `148.0 ms`; `21` realized containers; process peak `110.68 MB`.
- Rapid 30 target switches: `46.4 ms`.
- Reference cache analysis time across two misses: `1751.67 ms`.
- Color Studio processed export: 30 synthetic 2400x1600 fixtures, `59,718 ms` total, process peak `771.5 MB` (cold fixture creation excluded). This is a baseline, not a speed target or memory optimization claim.

## Evidence consistency

**PASS WITH DOCUMENTED SEAL RECOVERY.** The RunId, ProductSourceSha, input/output hashes, event digest and portable artifact hashes are checked by six automated evidence tests. The original run recorded a post-processing failure after all five suites had passed; the sealing script appended `seal_recovery` and `run_sealed` without rerunning any suite. Raw TRX/PNG files remain local-only; their SHA256 values are recorded in `artifact-manifest.json`. The old `organization-splitter` and related RC12 evidence remain a separate **HISTORICAL EVIDENCE ISSUE**.

## Remaining blockers

- Core full regression: **1,400 passed, 0 failed, 1 skipped** (`FilmCpuPerformance_WritesMeasuredEvidenceWhenRequested` requires an explicit evidence output path). The legacy literal assertions were updated to current product resources rather than reverting product text.
- WPF final isolated full regression: **143 classes, 1,308 passed, 0 failed, 4 justified opt-in visual/production-evidence skips, 0 timed out**. Each class ran in its own bounded test process; the two historical P1 validator classes need longer, still bounded budgets because they launch dozens of PowerShell validations. Earlier 45-second timeouts were class-budget exhaustion, not a demonstrated Dispatcher deadlock. Manifest: local ignored `artifacts/photo-wpf-final-20260923/rc12-wpf-process-isolation.json` (not sealed Photography evidence).
- Physical display DPI remains **NOT TESTED**. The in-process 100/125/150/200% renders passed and do not claim a physical monitor change.
- The seal recovery is explicit; it is not represented as a clean post-processing run.

## Packaging

Installer: **NOT GENERATED**

## Gate separation

- Product Development Gate: **PASS** (current-source automated suites pass; seal recovery is documented)
- Release Hardware Gate: **PENDING** (physical DPI is intentionally not tested here)
