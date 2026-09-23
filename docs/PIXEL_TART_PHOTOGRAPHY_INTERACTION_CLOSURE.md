# Pixel Tart Photography Interaction Closure

## Current run

- RunId: `photo-closure-20260923-b37ab5a3`
- ProductSourceSha: `b37ab5a3224907fc352f8d6f0697c9082d922839`
- FixtureVersion: `synthetic-reference-v1`
- GeneratedAt: `2026-09-23T12:20:00+03:00`
- RC12: **HISTORICAL ONLY**
- This sealed-label record describes the earlier `b37ab5a3` source. The regression below was run against the subsequently checked-out `cad0b312` plus uncommitted gate fixes; its numbers are not silently relabeled as the older run.

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
- Color Studio processed export: 30 synthetic 2400x1600 fixtures, `60,955 ms` total, process peak `827.7 MB` (cold fixture creation excluded). This is a baseline, not a speed target or memory optimization claim.

## Evidence consistency

**ARTIFACT INTEGRITY PASS; SAME-RUN PROVENANCE PARTIAL.** The RunId, ProductSourceSha, input/output hashes, 11-event digest and artifact hashes are checked by automated tests. These fields were assembled retrospectively, not captured by a run-time sealed producer. They therefore establish file integrity and consistent labels, but cannot prove that every result was generated in one original execution. The old `organization-splitter` and related RC12 evidence remain a separate **HISTORICAL EVIDENCE ISSUE**.

## Remaining blockers

- Core full regression: **1,400 passed, 0 failed, 1 skipped** (`FilmCpuPerformance_WritesMeasuredEvidenceWhenRequested` requires an explicit evidence output path). The legacy literal assertions were updated to current product resources rather than reverting product text.
- WPF final isolated full regression: **143 classes, 1,308 passed, 0 failed, 4 justified opt-in visual/production-evidence skips, 0 timed out**. Each class ran in its own bounded test process; the two historical P1 validator classes need longer, still bounded budgets because they launch dozens of PowerShell validations. Earlier 45-second timeouts were class-budget exhaustion, not a demonstrated Dispatcher deadlock. Manifest: local ignored `artifacts/photo-wpf-final-20260923/rc12-wpf-process-isolation.json` (not sealed Photography evidence).
- Physical display DPI remains **NOT TESTED**. The in-process 100/125/150/200% renders passed and do not claim a physical monitor change.
- Photography evidence same-run sealing remains open; artifact integrity is not equivalent to provenance closure.

## Packaging

Installer: **NOT GENERATED**

## Gate separation

- Product Development Gate: **FAIL / PENDING SAME-RUN EVIDENCE PROVENANCE** (test behavior and performance baseline pass; the historical Photography RunId is not a sealed current-source run)
- Release Hardware Gate: **PENDING** (physical DPI is intentionally not tested here)
