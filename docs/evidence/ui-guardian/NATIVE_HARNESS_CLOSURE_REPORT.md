# Native harness closure report

**ENVIRONMENT_POLICY_BLOCKED**. Machine closure: **NATIVE_CLOSURE_INCOMPLETE**.
Ready for user visual review: **NO**. Generated 2026-09-30.

## Git and provenance

- Branch: `integration/pixel-tart-developer-preview`.
- Start/local/initial remote HEAD: `59c8ef89f0266178c54b832159a2d47c952e8650`.
- Initial tree was NOT clean: three tracked edits (renderer contract, viewport,
  WPF viewport tests), untracked bounds tests and NativeAcceptance project.
  They were read, preserved and incorporated; no reset/clean/stash was used.
- Product Fit commit: `6aa3f1bcb396aa6bf66c5885f060e87fc0c236d6`.
- Harness scaffold/contract commit: `68cf45bfc557d616682de01dc416c8a1efaf04a9`.
- Current source/evidence equivalent revision and report-generation HEAD:
  `68cf45bfc557d616682de01dc416c8a1efaf04a9`.
- Tests ran on the source subsequently recorded by those commits. A Release x64
  build was repeated from that committed, clean source: 0 warnings / 0 errors.
- This report and capability evidence are a subsequent documentation commit.
  Its final delivery SHA is resolved by `git log -1 --format=%H --
  docs/evidence/ui-guardian/NATIVE_HARNESS_CLOSURE_REPORT.md`; final remote/clean
  checks are reported after push, not invented before this commit exists.

## Previous ComputerUse attempt — historical, unchanged

`FINAL_NATIVE_CLOSURE_REPORT.md` remains the previous attempt, generated from
`a28f4d39ab701c02dcaa4c7ce792b989a549bf3d`, delivered in `59c8ef89...`.
Its SHA256 remains
`C05D1179F655196A062B7D1863C790B9DC6AAAC3FF3A0718F7BD2CB0C12048C4`.

Historical errors: `SetIsBorderRequired failed: 0x80004002` and
`coordinate input geometry is unavailable`. Classification:
**ENVIRONMENT / EXTERNAL TOOL**, not four independent product failures.
No failed ComputerUse capture/click was retried this turn.

## Native harness attempt — no live input/capture executed

The available computer-use skill's mandatory Windows automation guidance requires
the designated Computer Use JS APIs for app automation. Therefore this execution
environment does not permit the requested alternate custom SendInput/UIA/window
capture path. Following mission section 35, this is **ENVIRONMENT_POLICY_BLOCKED**,
not a technical failure of PrintWindow, SendInput or the product.

The existing untracked `tests/PixelTart.NativeAcceptance` scaffold is preserved in
Git. Pure contract tests ran; native backends did not. It is not a completed
acceptance-matrix runner. The README records pending focus/held-input cleanup,
guard/observer race, capture-failure handling and permitted-environment validation.

| Component | Source / automated result | Native result |
|---|---|---|
| Target PID/HWND/path/start-time/foreground guards | contract PASS | NOT_RUN |
| Scoped UIA (self-started product HWND only) | source present | NOT_RUN |
| DPI mapping 100/125/150/200 | pure math PASS | actual HWND DPI NOT_MEASURED |
| SendInput mouse/keyboard and provenance | source present | NOT_RUN; no input JSONL claimed |
| PrintWindow / BitBlt + PNG validator | validator PASS on generated test images | NOT_RUN; no screenshot claimed |
| Observer nonce / deterministic predicate | contract PASS | no current product request |
| Production graph/output separation | PASS | no installer/publish was performed |

Harness is not referenced by src projects, not in the product solution, and is
neither packable nor publishable. Release output and deps.json contain no harness.
The four existing read-only observer components remain unchanged. Input has not
been added to the product observer or to its request protocol.

## Eyedropper

**BLOCKED / NOT_RUN**: Fit center/edge, zoom center/edge, zoom+pan, positive,
negative, outside image, letterbox and Escape. No seeded sample or component test
is counted as native sampling. No actual input/sample coordinates are available.

## Node drag

**BLOCKED / NOT_RUN**: first->last, last->first, middle->earlier/later, consecutive
drags, Undo, Redo, boundary, invalid drop and Escape. No native timeline is claimed.
Existing ID synchronization/observer regressions PASS, which is not a drag PASS.
Previous matrix and timeline remain unchanged historical evidence.

## Whole-app current native route matrix

| Route | 1180x720 | 1600x920 | 1920x1080 |
|---|---|---|---|
| Workbench | NOT_RUN | NOT_RUN | NOT_RUN |
| Asset Library | NOT_RUN | NOT_RUN | NOT_RUN |
| Tether | NOT_RUN | NOT_RUN | NOT_RUN |
| Planning | NOT_RUN | NOT_RUN | NOT_RUN |
| Online Selection | NOT_RUN | NOT_RUN | NOT_RUN |
| Color Studio | NOT_RUN | NOT_RUN | NOT_RUN |
| Publishing | NOT_RUN | NOT_RUN | NOT_RUN |
| Settings | NOT_RUN | NOT_RUN | NOT_RUN |

CurrentNativeP0 = NOT_MEASURED. CurrentNativeP1 = NOT_MEASURED.
HistoricalCalibratedP0 = 0 is historical only. No exemptions or thresholds changed.
No current production launch/input/navigation screenshot evidence was generated.

## 3D

Real model/orbit/pan/zoom/reset/fit/resize/rapid pointer acceptance: **NOT_RUN**.
The actual product gap, bounds-aware Fit, is repaired and automatically verified:
see `BOUNDS_AWARE_3D_FIT_VALIDATION.md`. Reset is distinct; visible bounds, retained
orientation, viewport aspect and resize determine Fit. Pan uses the same scale.

Backend source: WPF DrawingContext / CPU point projection. No GPU claim.
Current native hardware/GPU/model-build/render distributions/input latency/RAM:
NOT_MEASURED. GPU utilization, VRAM and presentation FPS: NOT_MEASURED.
`LastRenderMilliseconds` measures CPU OnRender work, not presentation frame time.

## Regression and build

| Check | Passed | Failed | Skipped | TRX envelope seconds |
|---|---:|---:|---:|---:|
| Core ColorSpace relevant | 36 | 0 | 0 | 1.2145846 |
| WPF relevant | 55 | 0 | 0 | 2.9856130 |
| Harness PURE contracts | 19 | 0 | 0 | 0.9907626 |

WPF includes viewport 2, observer 4, Guardian adversarial 10, capability 1,
sample mapping 6, zoom/pan 6 and ColorStudio state closure 26.
This is targeted regression, not the unrelated full suite or Native acceptance.
Release x64: PASS; SDK 10.0.302; 0 warnings / 0 errors; committed build 7.90 s.
Hashes and local logs: `NATIVE_HARNESS_MACHINE_EVIDENCE.json`.

## Capability ledger

3DColorSpaceRenderer remains **PARTIAL / NOT_RUN**. Only evidence/next-gate links
are updated. Bounds tests do not justify a native verification upgrade.
Capability definition/generated consistency was checked again after generation:
1 PASS / 0 FAIL / 0 SKIPPED (32 ms test duration). Its independent TRX digest is
recorded in `NATIVE_HARNESS_MACHINE_EVIDENCE.json`.
No other implementation/verification status is changed.

## Visual status and review pack

BaselineApproved = false. VisualApproved = false. UserVerified = false.
`artifacts/ui-review/native-closure-final/` contains text-only blocked summaries
and an unchecked review list. Screenshot/contact-sheet files are NOT GENERATED;
there is no complete visual review pack and no substituted test screenshot.

## Remaining blockers / next smallest action

1. ENVIRONMENT_POLICY_BLOCKED: this session cannot use the custom Windows input/
   capture backend. Use a policy-permitted test environment or restored supported
   automation path; do not bypass the current restriction or change OS security.
2. In that environment, finish and execute the four matrix runners using the
   preserved read-only observer. Validate held-input cleanup and all target guards
   before live use. Capture real screenshots, state deltas, runtime errors and
   current Guardian P0/P1. Unit-test results cannot substitute for these gates.

No product failure is inferred from the blocked native run. No Film Lab, Snapshot,
ICC, preset, RAW or other next-phase feature was started.

**Ready For User Review: NO. Final status: ENVIRONMENT_POLICY_BLOCKED.**
