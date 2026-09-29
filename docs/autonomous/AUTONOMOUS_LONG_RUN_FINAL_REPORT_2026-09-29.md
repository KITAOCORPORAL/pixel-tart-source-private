# Autonomous long-run safe checkpoint

## Git

START_HEAD: `b798dfaf571e300f4b2a07b66d58040fbf21ac83`.

All completed checkpoints have been pushed to `integration/pixel-tart-developer-preview`. The final report is itself a documentation checkpoint; no self-referential final SHA claim is made inside this file.

## Phase A

- Normalized center: implemented as bounded 0..1 image-coordinate semantics, default .5/.5.
- Legacy PanX/PanY: retained for compatibility.
- Different-resolution pan: routes through normalized center and each bitmap geometry.
- Actual Pixels mathematical matrix: automated PASS for 100/125/150/175/200% and 72/96/300 image DPI.
- Native physical-DPI QA: NOT TESTED; no Windows display settings changed.
- Task terminal progress: existing guard retained; a truly delayed callback adversarial test remains open.

## Phase B

- Existing Core inventory audited: OKLab model, cloud modes, sampling, migration and camera abstractions.
- Bounded projection and depth-prioritized hit testing: automated PASS.
- Camera defaults aligned to the photographic canonical defaults.
- WPF renderer surface / Color Studio integration: NOT IMPLEMENTED.
- Native interaction, screenshot QA, FPS and VRAM: NOT MEASURED.

## Phases C–G

No dependent production implementation started. Color snapshot, preset consolidation, ICC/TIFF closure, compute backend and Film Lab remain the existing documented boundaries. Creative Film executable code is preserved; Spectral/Instant Film remain research only.

## Tests

- Restore: PASS
- Release x64 build: PASS, 0 warnings, 0 errors
- Workflow/task targeted: 56 PASS, 0 FAIL, 0 SKIP
- 3D core targeted: 11 PASS, 0 FAIL, 0 SKIP
- Camera targeted: 7 PASS, 0 FAIL, 0 SKIP
- Existing full-suite failures remain historical/pre-existing and were not suppressed.

## Native QA

NOT TESTED. No native acceptance was inferred from automated tests.

## Known issues

- P0: dependent phases cannot claim completion until real WPF 3D integration and its gate are closed.
- P1: task delayed-progress adversarial test and native current-DPI verification remain open.
- P2: the renderer projection has no measured frame-time/FPS baseline yet.

## Deferred

Mac, iPad, Pocket, Online Selection deployment, Browser Extension production, Planning rewrite and Cloud RAW sync remain deferred.

## Next exact task

Implement the single-surface Windows Color Space renderer in Color Studio, with generation-safe asynchronous model building, native interaction QA and measured Standard/Dense performance.

## Status

AUTONOMOUS_RUN_PARTIAL_SAFE_CHECKPOINT
