# Stage III — Reference Look / Tethered v1

Status: **PARTIAL, implementation in progress; not release acceptance**.

Baseline verified by `git fetch origin`: local and remote
`f3216f61bc8929a3ebabbe70f77744342a605857`, branch `integration/pixel-tart-developer-preview`.
Pre-existing RC12 report changes are excluded from this stage's commits.

## Phase A

- DONE (code + focused tests): prepared immutable Zone hover frames from the bounded analysis proxy/map; no decode on hover, source preserved. HEX/HSL copy with transient feedback. Project default palette/tone reference-only persistence with sources, version, timestamp, serialized atomic writes. Asset/board/canvas selection entry points. Exact duplicate import skip/view/import-anyway UI; skip is default, similarity does not block import. Watermark visual 3×3 control binds the existing position model; margin/offset remain.
- Focused core: 4 passed, 0 failed, 0 skipped (`artifacts/stage-iii-reference-tethered/tests/phase-a-core.trx`).
- Focused WPF: 2 passed, 0 failed, 0 skipped (`phase-a-wpf.trx`).
- Initial Release x64 build: 0 warnings, 0 errors. Final full gate and real-App screenshots are still pending.

## Remaining stage scope

NOT IMPLEMENTED in this checkpoint: Project Look, reference match/color-management integration, new tether accordion arrangement, split interaction upgrade, apply-to-following, Shot/lighting/pose bridge, detailed capability visibility, next-capture rules, reference second display.

## Acceptance boundaries

- Source Safety: focused Zone source-buffer and duplicate import before/after file-hash tests PASS; full Stage III source-safety gate not yet run.
- Performance/race stress: NOT IMPLEMENTED at this checkpoint.
- Visual Evidence: pending under `artifacts/stage-iii-reference-tethered/`.
- Physical Camera: **NO**. Physical DPI: **NO**. Synthetic app rendering is not physical acceptance.
- Publishing restart recovery: NOT IMPLEMENTED.
- Durable cross-store duplicate crash/power-loss journal: NOT IMPLEMENTED (ordinary failure compensation remains PARTIAL).
- New real 10K performance acceptance: NOT IMPLEMENTED.
- No RC13, main merge, history rewrite, AI matching, camera SDK integration, or complete Planning Center.
