# Internal readiness — FAIL / not deliverable

This report is a checkpoint, **not one-pass closure**. No Candidate ZIP has been generated or delivered; no user debug run is requested.

Product source: `44009930e2fa40ef785b6bd85c16a31518f8acab`.

| Plan | Formal steps | Structural peer verified | Selector-free | Missing native contracts | Per-step transition replay |
| --- | ---: | ---: | ---: | ---: | --- |
| planning-full | 124 | 91 | 27 | 6 | INCOMPLETE |
| upgrade-full | 70 | 53 | 17 | 0 | INCOMPLETE; actual old-to-new workflow not run |

See the corresponding JSON matrices for every formal step, stage, action, scope, name, ID, type, pattern, focus requirement, expected count, source/evidence and risk. `VERIFIED` is explicitly structural evidence, not installed step PASS. `NOT_APPLICABLE` means no selector, not automatic action success. `PLAN_BUG` currently denotes six missing native-contract proofs, not a claim that all six native controls are broken. Assumed status is forbidden and absent.

Both plans lint with zero errors; scoped-name warnings remain (108/55, including repeated audit checkpoints). Shared selectors are single-source and inline overrides fail closed. The build now invokes plan lint. Required reference targets enforce visible/enabled/focusable/Invoke; keyboard dispatch verifies the exact focused element rather than any element in the process. Screenshot bounds no longer fall back to an untyped global name.

Remaining gate work:

- Verify native Open and Save dialog scope, Edit/Value and Button/Invoke contracts (six steps), overwrite handling and actual installed PDF export.
- Replay each formal state transition and bind all evidence to the current product SHA; snapshots of unrelated states cannot prove a transition.
- Complete all preset uniqueness tests and additional negative linter/runner mutation tests.
- Run full old-to-new installed upgrade workflow; old compatible selectors must remain valid.
- Completed gates: current-source WPF isolation 1255/1255, 120 fixtures, zero failed/skipped; internal Chinese-path portable staging checks 7/7. No final ZIP exists.
- Package only after a machine-readable gate with all required evidence passes. Packaging now binds new installer provenance and final Candidate naming, but remains blocked by `ACCEPTANCE_CANDIDATE_GATE.json`; never ship the historical v3 layout.

Supported Windows capture failed twice with `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`; observed button click failed with `coordinate input geometry is unavailable`. No direct UIA/SendKeys bypass was executed. In-process WPF tests and production visual renders remain separate from installed OS interaction. User visual acceptance remains PENDING USER.
