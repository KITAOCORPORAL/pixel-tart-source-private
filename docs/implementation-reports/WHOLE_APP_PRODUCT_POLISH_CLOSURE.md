# Whole-app product polish closure — BLOCKED

This is a **partial implementation and evidence handoff**, not the requested complete
whole-app acceptance. No Candidate, installer or user-run Runner was produced.

## Git baseline

- START_HEAD: `daaad6e52cf820258e0e91d32aab20662f968cbc`
- START_PRODUCT_SHA: `44009930e2fa40ef785b6bd85c16a31518f8acab`
- NEW_PRODUCT_SOURCE_SHA: `f505a177106a38fc23550edd87949f235238fb79`
- Product/test commits: `e99b231`, `22d17d8`, `9733f65`, `f505a17`.
- FINAL_HEAD: the report/evidence-only commit containing this file (resolve with
  `git log -1 --format=%H -- docs/implementation-reports/WHOLE_APP_PRODUCT_POLISH_CLOSURE.md`).
  A commit cannot embed its own hash. Exact final hash is returned in the handoff.
- Reference UI: **USED AS DESIGN DENSITY / LAYOUT REFERENCE**.

No force push. Existing RC12 reports/publish directories outside this change remain
uncommitted and were not reverted or included.

## Implemented

- Shared dark context menus; explicit dark combo item, calendar/day/month, check/radio,
  toggle and progress templates. Calendar native part names and weekday labels retained.
- Asset header bright block removed; search expanded; narrow toolbar remains scrollable;
  filter overlays gallery; low-frequency analysis collapses; menus grouped into submenus.
- Reference target/reference role language and empty state, target-dominant preview,
  source thumbnails, original/result labels, async loading and busy feedback.
- Fixed actual reference-choice reset and stale-render logic bugs without changing engine.
- Planning keeps 280 DIP list and seven modules; two heroes now equal width; reading page
  gains a brief existing-shot summary, not a second shot editor.
- Publishing header/footer separated; preview/export feedback; unbound Settings checkbox
  replaced with a read-only description of automatic behavior.
- Production whole-app render inventory and isolated per-fixture regression execution.

## Status by gate

| Gate | Status |
| --- | --- |
| Planning reference/rail/header/seven modules/reading/hero/summary | Implemented; recorded-state review only |
| Asset header/search/toolbar/filter/menu/gallery | Implemented; recorded states + targeted regressions |
| Asset inspector density | PARTIAL |
| Reference roles/comparison/loading | Implemented; multi-reference and all recovery states PARTIAL |
| Global menus / light leaks | Captured popup inventory dark; not all possible popups certified |
| Text overflow | PARTIAL, no full 17-page × all-state × all-DPI geometry audit |
| Border density / Loading | PARTIAL |
| Accessibility | Production peer and offline contracts separate from installed UIA |
| Planning 124 / Upgrade 70 | INCOMPLETE; six native file-dialog contracts unverified |
| Physical DPI / 2K / 4K | NOT RUN |
| Candidate | NOT GENERATED |
| User Visual Acceptance | PENDING GPT REVIEW → PENDING USER, not accepted |

## Evidence policy

Final evidence must be from NEW_PRODUCT_SOURCE_SHA. Working-tree first–ninth iterations
and `wpf-worktree` are diagnostic only. The first full diagnostic run's manifest stamped
the old HEAD despite dirty sources; it is expressly **invalid as source-bound evidence**.
The isolation script now reports UNFROZEN_WORKTREE when src/tests/tools are dirty.
The initial isolation discovery regex also omitted classes with an intervening
`[DoNotParallelize]` attribute. `9733f65` corrects this: 125 non-diagnostic fixtures,
including real production-App/peer/visual classes, must run. The prior 121-fixture
1257-pass run is diagnostic only, not the final full-suite claim.

The first corrected-discovery run at 9733f65 had 1263 passes and one failure:
RealAppStartupIntegrationTests depended on an unseeded Release fixture. f505a17
explicitly migrates/seeds its required isolated acceptance root before starting the
real App. Production startup/seeding behavior was not changed. The final full run
includes that fixture; earlier failed runs are not relabelled as final-source proof.

Final production images are IN_PROCESS_WPF_RENDER, not desktop capture. The production
window is requested at 1920 × 1080 DIP; content is 1906 × 1043 px, with separate popup
dimensions. All demo content is synthetic. No source photo/user workspace is modified.

See [visual review](WHOLE_APP_VISUAL_REVIEW.md) and
[logic audit](WHOLE_APP_PRODUCT_LOGIC_AUDIT.md). Neither is a whole-product PASS.

## Source-bound evidence inventory

Evidence root: `artifacts/whole-app-product-polish/`.

| Evidence | Result / path |
| --- | --- |
| Release x64 solution | 0 warnings, 0 errors; local `release-final.binlog` |
| Core | 1376 passed, 0 failed/skipped; `tests-final-source/core.trx` |
| Full WPF | 125 isolated fixtures, 1264 passed, 0 failed/skipped; `wpf-final-source/rc12-wpf-process-isolation.json` and 125 TRX files; product_dirty=false |
| Real production App startup | Included in full WPF, 1 passed; `wpf-final-source/092-RealAppStartupIntegrationTests.trx` |
| Logical DPI suite | 90 passed, 0 failed/skipped; `tests-final-source/dpi.trx`; source/math tests, not full rendered scaling matrix |
| Modular suite | 14 passed, 0 failed/skipped; `tests-final-source/modular.trx` |
| Whole-app image producer | 1 passed; `tests-final-source/visual.trx` |
| Screenshots | 70; `review-final/WHOLE_APP_SCREENSHOT_MANIFEST.json`; SHA256 verified, zero mismatches |
| Contact sheets | `review-final/WHOLE_APP_CONTACT_SHEET_01.png` through `_08.png` (9,9,9,9,9,9,9,7 states) |
| Popup contact sheet | `review-final/GLOBAL_POPUP_CONTACT_SHEET.png`, 23 popups |
| Production accessibility | 39 checks PASS; `peers-final/result.json`, `checks.json`, `peer-states.json`; `tests-final-source/contract.trx` |
| Plan lint | 124/70 steps, 0/0 errors, 108/55 warnings |
| Offline selector/navigation | 8 + 16 PASS; does not use native UI |
| Runner comprehensive self-test | BLOCKED: requires historical installer beside runner; not staged, not counted PASS |

Re-generated contract inventory: fresh 91 structural matches / 6 missing / 27 selector-free;
upgrade 53 structural matches / 0 missing structural / 17 selector-free. All actual installed
state transitions remain separate and incomplete. Reports preserve that distinction.

## Blocking gaps and known issues

- Native screenshot API failed twice with unsupported interface (E_NOINTERFACE).
  Supported UI access was not replaced by an unapproved desktop capture/automation path.
- Native Open/Save, fresh install and old-to-new upgrade transitions remain unproved;
  current source is not an installed acceptance build and no new installer is authorized.
- Only a subset of applicable content/selected/loading/error states is captured for many
  routes. 17 route entries is not a claim of full functional traversal.
- P0: initial asset-toolbar overflow clipping fixed and full-size re-capture checked.
  No white blocks observed in captured popup inventory; **global P0=0 not certified**.
- P1: inspector density; multi-reference control/interaction coverage; missing full-state
  and physical-DPI validation. Loading latency thresholds not measured.
- P2: planning menu repeated icons and residual dividers/micro-spacing.

Do not issue READY FOR GPT REVIEW or open Candidate gate on this evidence alone.
