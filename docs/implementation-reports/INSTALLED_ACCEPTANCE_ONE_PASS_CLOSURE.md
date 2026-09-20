# Installed Acceptance one-pass closure — BLOCKED, NOT CLOSED

Updated 2026-09-20. This is an honest progress/blocker report, not Candidate acceptance. No Candidate ZIP, v4/v5 or standalone launcher has been delivered. **USER DEBUG RUNS REQUIRED BEFORE CANDIDATE: 0. USER VISUAL ACCEPTANCE: PENDING USER.**

## Identity

- START_REMOTE_HEAD: `6628d624dbee3d6a9170009a9d1b9e5e4353ec14` (verified with `git ls-remote`).
- START_LOCAL_HEAD: `1c054ab` (existing unpushed runner commit plus local v3 changes).
- FINAL_PRODUCT_SOURCE_SHA: `44009930e2fa40ef785b6bd85c16a31518f8acab`.
- FINAL_HEAD: report-containing Git commit; use branch HEAD, not the product SHA. Final remote confirmation is recorded in the response.
- Historical source `8cb95e6` is frozen; none of its installer results are relabelled as current-source evidence.

## History and corrections

`cb24083` preserved the already effective date inner-part selector, true sidebar navigation scope, strict uniqueness and v3 diagnostics, then pushed them to the requested integration branch. v2 (15 files) and v3 (20 files) were copied with SHA256 verification into `artifacts/installed-acceptance-history`; the v1 original folder is missing at its supplied location. See `INSTALLED_ACCEPTANCE_FAILURE_HISTORY.md`.

| Failure | Root cause | Current internal evidence |
| --- | --- | --- |
| v1 | DatePicker and PART_TextBox share 拍摄日期; untyped name ambiguous | Real production DatePicker Custom, inner Edit/Value, button Invoke; runner negative regression |
| v2 | SidebarRoot sibling treated as ancestor | Actual SidebarNavigationScroll Pane contains typed primary navigation Buttons |
| v3 | Reference StackPanel has no peer despite keyboard handlers | PlanningReferenceTile Button/Invoke; focus, Enter original image, Esc focus return, Shift+F10 and menu checks for references/moodboard/lighting/styling |

Product fixes are limited to accessibility and real workflow blockers:

- No default Button template or crop/ratio change; focus uses a restrained accent line. No random asset IDs.
- Real parent Pane scopes for content and dialogs, fixed module button IDs; no fake sibling ancestor.
- Shot rows expose real Invoke peers while retaining existing layout/model.
- New project passed to Tether is now added to its option collection before selection. Previously a newly created project could display 无项目 because it was absent from the ItemsSource.
- Pre-session Tether shows the loaded shot and a return-to-planning action; no camera/session is required to access the established project bridge.
- Plan close-confirm steps now wait for natural exit, matching MainWindow's automatic flush; no optional imaginary 保存 dialog.

## Contract audit

Both full matrices enumerate all formal steps and fields in `docs/acceptance/`. Shared definitions live in `selectors.json`; `LoadPlan` rejects inline overrides. Build executes lint; process-owned window roots, exact uniqueness and exact keyboard focus are enforced. Screenshot bounds cannot fall back to global name-only selection.

| Contract | VERIFIED (structural) | ACCESSIBILITY_GAP | PLAN_BUG / missing proof | NOT_APPLICABLE (no selector) | ASSUMED |
| --- | ---: | ---: | ---: | ---: | ---: |
| 124-step fresh | 91 | 0 recorded | 6 | 27 | 0 |
| 70-step upgrade | 53 | 0 recorded | 0 | 17 | 0 |

These totals **do not** mean all state transitions or the upgrade workflow passed. Six native Open/Save dialog contracts lack current internal evidence. Full per-step state replay, complete preset contract tests and old-to-new workflow proof are still incomplete. Missing evidence is visible in each row; nothing is marked ASSUMED or falsely PASS. Scope/name lint warnings are retained: fresh 108, upgrade 55. Lint errors: 0/0. Known reference Custom assumption is removed; remaining permitted Custom names are actual production UserControl/DatePicker peers.

## Tests and evidence

| Gate | Result / evidence |
| --- | --- |
| Current production accessibility | PASS, 39 checks, `artifacts/one-pass-contract/4400993/contract.trx`, `checks.json`, `peer-states.json` |
| Runner offline | PASS, 23 self-tests + 8 selector tests + 16 navigation/linter/catalog tests = 47 |
| Release solution build | PASS after other test processes released DLLs; 0 warnings/errors |
| Core | PASS 1376 / 0 failed / 0 skipped, `regression/core-final.trx` |
| DPI suite | PASS 90 / 0 failed / 0 skipped, `regression/dpi-final.trx`; not physical display DPI |
| Real production App/MainWindow | PASS, `regression/real-app.trx`; seven modules, save/reopen, project bridge, export and software scaling |
| WPF full | PASS 1255 / 0 failed / 0 skipped, 120 isolated fixtures; `wpf-final/rc12-wpf-process-isolation.json`, test HEAD `a6e03c4`, unchanged product source `4400993` |
| Portable staging | PASS 7/7: success, missing runner/dependency/installer/plan, tampered plan, restored success; repeated through final named entry at `中文 最终入口回归/portable-tests.json`; no Candidate ZIP |

The first single-process WPF run was stopped after stalled progress (65 partial passes); it is not a full PASS. A first 120-fixture isolation pass reported 1254 pass / 1 fail / 0 skip. The failing browser fixture blocked on asynchronous disposal without an STA synchronization context; fixed test pumps the dispatcher, targeted 4/4 passed. The fresh full isolation run completed at 16:28:29 with 1255/1255 PASS. Failed and partial artifacts remain intact; no replacement of old logs.

Production visual renders: `artifacts/one-pass-contract/4400993/` (reference focus, original preview, context menu and Tether), and `real-app-screenshots/` (seven modules, preview, software DPI, generated PDF). These are real WPF visual renders, not installed screen captures. Reference previews were checked at source width 1200 pixels. Synthetic one-pixel demo sources remain synthetic, not photography acceptance evidence.

Reference page and menu renders were inspected: no default white Button background or Windows blue focus rectangle appears. The 3:2 synthetic reference remains 3:2. Focus ownership is test-verified; these renders alone do not certify physical keyboard-focus visibility or photographic quality. Historical BAT/version metadata was corrected, and the final-named BAT's seven portable probes passed. A last documentation-only edit corrected the legacy README's entry name and isolated-directory description after those probes; executable/plan inputs did not change.

## New installer and fresh installation

Internal installer: `artifacts/stage-v2-installed-startup-failure/builds/2.3.0-dev.4400993/installer/PixelTart-DeveloperPreview-2.3.0-dev.4400993-x64-Setup.exe`.

SHA256: `C03F74A11AB5FA5BD6111C78EE71819FEB349296A6DFF3A4D078F530C5CC6C98`.

Release win-x64 self-contained publish, runtime dependency scan, forbidden test/source artifact scan and Inno compile succeeded. Installation exit 0; all installed publish files hash-match (0 mismatches). `STARTUP_OK` contains the exact source SHA. PID 51400 was launched only against isolated data:

`C:/Users/Administrator/AppData/Local/PixelTart-TestAcceptance/OnePass_4400993_ec1a9705ff5546a79bfd5fb21451daf9/`.

Installed main window and onboarding were read with the supported Computer Use API. Screenshot failed, then failed again after reselection: `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`. Clicking observed 退出教程 failed: `coordinate input geometry is unavailable`. No custom UIA/SendKeys workaround was executed. Supported Alt+F4 subsequently closed this isolated instance; its app log records normal application exit at 16:17:34. Exit code was not captured, so full runner close acceptance is not inferred.

## Candidate gate

**FAIL / DO NOT DISTRIBUTE.** Installer creation or internal tests alone do not authorize Candidate delivery. `Package-Kit.ps1` requires a complete source-bound gate, including native dialog contracts and both internal per-step rehearsals. `docs/acceptance/ACCEPTANCE_CANDIDATE_GATE.json` records the current FAIL. Six native dialog contracts, full internal per-step transitions and old-version compatibility proof remain incomplete. Final installed OS interaction may be deferred to the user's Candidate run as requested, but must not be confused with the unfinished internal work.

Known v1 ambiguity, v2 wrong scope and v3 Custom assumption are corrected and covered internally. This is not a blanket assertion of zero unknown ambiguities or wrong scopes across unvisited states. No known unresolved production accessibility defect was demonstrated by the 39 checks; complete coverage is still required before claiming zero gaps.

The environment issue is not a request for the user to debug selectors. No Stage VI, feature expansion, redesign or promotional work was started.

Packaging was explicitly invoked against the FAIL gate and rejected before staging or creating a ZIP. Remaining internal work is recorded rather than attributed entirely to the OS limitation: preset coverage, all formal state transitions, and old-version compatibility are unfinished, in addition to native dialog evidence.
