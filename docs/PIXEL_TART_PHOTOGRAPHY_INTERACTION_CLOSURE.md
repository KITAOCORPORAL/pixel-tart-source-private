# Pixel Tart Photography Interaction Closure

## Current run

- RunId: `photo-closure-20260923-b37ab5a3`
- ProductSourceSha: `b37ab5a3224907fc352f8d6f0697c9082d922839`
- FixtureVersion: `synthetic-reference-v1`
- GeneratedAt: `2026-09-23T12:20:00+03:00`
- RC12: **HISTORICAL ONLY**

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
| Performance | **PARTIAL** | Current-source measurements recorded for import, film, batch export, cancel, filmstrip virtualization and rapid switching; production-size acceptance remains open |

## Measured evidence

- 24MP film processing: `9387 ms`; cancellation latency: `0.85 ms`.
- Import 100: `73.4 ms`; import 500: `104.3 ms`.
- Batch export 100 synthetic targets: `197.6 ms`; process peak `81.15 MB`.
- Filmstrip 500 targets: `148.0 ms`; `21` realized containers; process peak `110.68 MB`.
- Rapid 30 target switches: `46.4 ms`.
- Reference cache analysis time across two misses: `1751.67 ms`.

## Evidence consistency

**PARTIAL / NOT CLOSED.** This run's automation, reports and provenance use the RunId and ProductSourceSha above. The old `organization-splitter` and related RC12 evidence were not regenerated; they are a **HISTORICAL EVIDENCE ISSUE** and are intentionally separated from current Photography evidence. No unified event digest or Input/Output Manifest hash is claimed.

## Remaining blockers

- Full Core suite has 1,381 passed, 10 legacy UI literal-assertion failures, and 1 skipped test.
- Full WPF suite was bounded and stopped after a no-output hang; it is not reported as PASS.
- Physical display DPI and a unified event-digest evidence run remain open.

## Packaging

Installer: **NOT GENERATED**
