# Final Native Closure Report

Status: **NATIVE_CLOSURE_INCOMPLETE**. Ready For User Review: **NO**.
Generated at source HEAD `a28f4d39ab701c02dcaa4c7ce792b989a549bf3d`, 2026-09-30. No visual approval granted.

## Git

- Start HEAD after safe fast-forward: `5b61f44b3e6e848669ad775123d5fd63e2b0e09a`.
- Company checkout before sync: `d8b2ea68f6b377aa07618ceb0bb676dd0f041019`, clean.
- Branch: `integration/pixel-tart-developer-preview`.
- Final product/source HEAD: `a28f4d39ab701c02dcaa4c7ce792b989a549bf3d`.
- Created implementation commits: `d7ca23b` read-only native evidence; `a28f4d3` observer file-sharing race fix.
- Final delivery HEAD is the subsequent documentation-only commit containing this report; resolve using `git log -1 --format=%H -- docs/evidence/ui-guardian/FINAL_NATIVE_CLOSURE_REPORT.md`. The exact final delivery/remote SHA is reported after push; this file does not claim its own unknown future SHA.
- Working tree / remote synchronization at report generation: only this report/evidence and generated capability provenance pending; push not yet attempted. Final clean-tree/push results are verified separately in the task response.
- Historical source HEADs remain distinct: overnight native report uses `a1cde36d82bc65d211ae85e6a8fbf60baadd5d92`; calibrated Guardian reports `dc43ebe5079c5e7dfa8b2a546ae3b822955c17f8`. Neither is current-binary proof.

## Production

Release x64 PASS, SDK 10.0.302, 0 warnings / 0 errors; final source build 12.97 s.
Committed-source production KitaoPhotoSelector.exe launch PASS; STARTUP_OK at 2026-09-30 09:56:21.925 +08:00.
Exact binary hashes and ignored runtime paths: `NATIVE_CLOSURE_MACHINE_EVIDENCE.json`.

One intermediate attempt failed due to the task-owned running EXE locking apphost. Only that task-owned process was stopped; rebuild succeeded. An observer response-file race produced a real UI exception in the intermediate run; preserved BEFORE evidence and AFTER lock regression are recorded, not overwritten.

## Eyedropper

**BLOCKED**. No native sample was successfully injected.
`EYEDROPPER_NATIVE_ACCEPTANCE.json`: all requested sample actions NOT RUN.
Added opt-in read-only state: IsSampling, mode, real input timestamp, screen/viewport/image coordinates, image rect, zoom, pan, valid/invalid, sampled RGB, selected node, bounded sequence.
Sampling math/product behavior was not changed. The existing data-seeded samples are not counted as native samples.

## Node Drag

**BLOCKED**. Matrix: `NODE_DRAG_NATIVE_MATRIX.json`.
Timeline/causal distinctions: `NODE_DRAG_NATIVE_TIMELINE.md`.
Undo/Redo and all ten native cases NOT RUN.
Observer file contention is PROVEN AND FIXED; native drag product root cause remains NOT PROVEN.
20 read-only production requests passed deterministic initial model/visual/render ID checks. Not a native drag pass.

## Whole App

**BLOCKED**, all eight routes at 1180×720, 1600×920, 1920×1080 NOT RUN.
See per-route `WHOLE_APP_NATIVE_ROUTE_MATRIX.md` and JSON.
No screenshots claimed, no route exemptions added.

## 3D

**BLOCKED**, PARTIAL / NOT_RUN retained.
Current-run real model, orbit, pan, zoom, reset, bounds-aware fit, resize and rapid interaction NOT RUN.
See `3D_NATIVE_POINTER_ACCEPTANCE.md`, JSON and `3D_PERFORMANCE_MEASUREMENT.md`.
Source inspection also confirms Fit lacks bounds computation; it is not marked complete.
GPU usage / VRAM / FPS: NOT_MEASURED.

## Guardian / Regression

- Current native whole-app P0/P1: NOT MEASURED.
- Historical calibrated P0: 0, across 33 states; preserved, not reused as current acceptance.
- Guardian adversarial geometry: 10 PASS.
- Targeted WPF regression: 60 executed / 60 PASS / 0 FAIL / 0 SKIPPED, displayed duration 2 s (TRX execution envelope 3.573 s); final committed source a28f4d3.
- Suites: NativeClosureObservation 4; CapabilityConsistency 1; Guardian 10; sample mapping 6; zoom/pan 6; ColorStudio state 26; 3D viewport 1; workspace integration 6.
- No tests removed, thresholds relaxed, global route or ScrollViewer exemptions introduced.
- This was targeted WPF regression, NOT the unrelated full application test suite.

## Capability Ledger

No implementation or verification status was upgraded.
3DColorSpaceRenderer remains PARTIAL / NOT_RUN.
Generated JSON/MD provenance refreshed from `a28f4d39ab701c02dcaa4c7ce792b989a549bf3d`; the 3D definition now links the current blocked acceptance, observation tests and next gate. Status values are unchanged.
CapabilityDefinitionAndGeneratedArtifactsStayConsistent PASS, including a separate 1/1 check after the evidence-link update (32 ms).

## Visual Status

BaselineApproved = false
VisualApproved = false
UserVerified = false

## Remaining Blockers

1. Native capture/input tool: two captures fail with `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`; a text-only observed-element click fails with `coordinate input geometry is unavailable`. These prevent screenshot-backed pointer gates. No OS/security/driver/tool installation changes were made.
2. Eyedropper: exercise all real sample/mapping/outside/Escape cases after tool recovery.
3. Node Drag: execute ten native cases with synchronized model, visual and render IDs, selected node, observer events, Undo/Redo.
4. Whole-app: execute all 24 native route/size cases and run Guardian on those real states.
5. 3D: execute native interactions and reliable performance measurements; close the source-confirmed model-bounds Fit gap and verify aspect/resize behavior.

## Next smallest action

Restore a working supported screenshot/input session on this Windows host, then resume the production EXE gates using the read-only observer and bounded synchronization reader. Do not claim unit tests, fixture setup or direct commands as native input. No Film Lab, Snapshot or new phase was started.

## Ready For User Review

**NO — NATIVE_CLOSURE_INCOMPLETE**
