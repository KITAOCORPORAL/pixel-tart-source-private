# Overnight Native Closure Report

## Git

- Start HEAD: `a1cde36d82bc65d211ae85e6a8fbf60baadd5d92`
- Current commit: `e6831c6d9a3b4b3bfc657fbe2ec498ffa2fe2bb7`
- Product source used by native run: `a1cde36d82bc65d211ae85e6a8fbf60baadd5d92`
- Working tree changes in this closure: startup binding fixes, capability consistency test, source-of-truth regeneration.

## Guardian

- Historical P0: 1846 observations from the pre-calibration evidence.
- Latest calibrated P0: 0 across 33 states.
- Adversarial geometry tests: PASS.
- No global route or ScrollViewer exemption.

## Desktop / Color Studio native

- Backend: Win32 SendInput against committed Release x64 production EXE.
- Launch: PASS after fixing two real startup binding defects.
- Zoom, Fit, 100%, pan, split, side-by-side, boundary, rapid interaction and Escape: PASS.
- Eyedropper fit, zoom/pan and negative sampling: BLOCKED by the existing runner's sampling-state discovery; not claimed as pass.
- Node drag: historical diagnostic evidence remains blocked by observer/order synchronization; not closed.
- Whole-app native routes at three sizes: NOT_RUN.

## 3D

- Real model capture: existing evidence PASS.
- Orbit/pan/zoom/reset/fit/resize native pointer: NOT_RUN.
- Renderer remains PARTIAL / NOT_RUN in the capability ledger until those native gates are closed.
- No GPU/VRAM performance claim.

## Capability source of truth

`capabilities.definition.json`, generated JSON and generated Markdown are synchronized and guarded by `CapabilityDefinitionAndGeneratedArtifactsStayConsistent`.

## Visual status

BaselineApproved: false. VisualApproved: false. UserVerified: false. The review pack is current-state evidence only.

## Remaining blockers

1. Close eyedropper native state discovery and record actual samples.
2. Re-run synchronized native node-drag matrix with timeline evidence.
3. Execute whole-app native route matrix at 1180/1600/1920.
4. Execute real 3D pointer acceptance and performance measurement.
5. User visual review.
