# Whole-app product logic audit

Product source: `f505a177106a38fc23550edd87949f235238fb79`.
Overall: **PARTIAL / final acceptance FAIL**. This is not a claim that every action was exercised.

## Changes and evidence boundaries

No business engine, planning model, primary navigation or native file dialog was replaced.
Existing uncommitted UI work was continued. The unrelated RC12 physical-acceptance reports
and historical publish directories were left untouched and are not part of this delivery.

| Area | Evidence exercised or inspected | Result / remaining work |
| --- | --- | --- |
| Navigation | Production App/MainWindow visits all 17 requested routes; Settings remains an overlay | PASS for route entry, not every return path |
| Planning | Seven content modules, document reading, 280 DIP list, preview, create modal, actual date popup, image menus | PARTIAL; full 124-step installed transitions not replayed |
| Planning data | Original Shot model/commands retained; brief shot summary uses existing shots | No model migration; full persistence covered only by existing tests |
| Reference selection | ObservableCollection reset could send SelectedItem=null and erase the current scheme | FIXED: suppress transient resets, preserve explicit deselection and project switch |
| Reference rendering | Source change/disabled state could leave an old render in flight | FIXED: cancellation + revision checks + captured token + lifetime cancellation |
| Reference roles | Empty state has separate 待调色照片/参考图片; loaded main image is target/result; reference thumbnails separate | PASS in recorded single-reference fixture |
| Reference feedback | Target decode off UI thread; importing/render/export update IsBusy/status; standalone parameters disabled while rendering | PARTIAL: busy render recorded; native import/export/error workflows not all exercised |
| Reference save | Explicit save/restore behavior and existing project-default resolver retained | Existing regression tests, not a fresh end-to-end save dialog replay |
| Multi-reference | Source collection/weights retained | PARTIAL: screenshot fixture has one reference; remove/reorder controls not fully surfaced in standalone page |
| Tether parity | Shared reference view model and busy indicator; existing return-context peer contracts retained | PARTIAL: no connected-camera capture or second-display acceptance |
| Asset library | Isolated library import of 12 distinct synthetic images; selection/multiselect; viewer/loupe; filter overlay; grouped menus | PASS for recorded states; no 10K physical performance claim |
| Asset toolbar | Search gets remaining width with 300 DIP minimum; horizontal overflow stays scrollable at narrow width | Regression preserves access to trailing redo action |
| Asset menu | Low-frequency workflow/export/manage actions moved to submenus, action IDs/commands kept | Menu hierarchy/action/icon and recent-library tests cover persistence/non-deletion |
| Recent libraries | Header clipped with long date/path/status | FIXED: concise ellipsized name, complete tooltip; offline state retained in tooltip |
| Publishing | Three synthetic inputs, real preview generation and preset dropdown | PARTIAL: output destination/native save and full publication not executed |
| Settings | Six tabs and available dropdowns; unbound “remember window” checkbox found | FIXED: honest read-only description of existing automatic behavior |
| Workbench/calendar/history/finance | Actual default pages and source inspection | PARTIAL: populated/error/loading actions missing from visual matrix |
| Ingest/RAW/organize/collage | Actual empty/default surfaces; existing unit/WPF coverage | PARTIAL: end-to-end files/output/recovery not exercised this pass |
| Online selection/license | Actual local default UI | PARTIAL: service/account/payment/activation side effects not attempted |
| Destructive actions | No personal files deleted, no customer photos uploaded; demo writes isolated | PASS for this execution scope |

## Loading audit

Source inspection found existing status/progress bindings in library import/analysis,
calendar, RAW conversion, organizing, collage, publishing and tether preview. Reference
decode/render/import/LUT export had the material gap addressed here. Publishing now shows
preview/export activity. A binding's existence is **not** timing, duplicate-action or
recovery evidence. The requested 300 ms / 1.5 s timing matrix remains unmeasured.

## Accessibility and contracts

Production peer checks and offline selector/navigation tests are separate from installed
UIA transitions. Existing `PlanningReferenceTile`, scope IDs, Enter/Esc/Shift+F10 and
tether return context were retained. Native Windows Open/Save dialogs remain unchanged.
Six native contracts and the full 124/70 transition sequences remain unverified.
Plan lint has zero errors but 108/55 warnings requiring live scoped uniqueness checks.
Offline selector/navigation tests passed 8 + 16. The broader Runner `--self-test`
could not run from its build directory because it requires the historical 4400993
installer payload. It is NOT recorded as PASS and no installer was staged to conceal
the missing current-source installed acceptance evidence.

## Open issues

- P1: Asset inspector remains dense when expanded; standalone reference controls still
  occupy a permanent narrow panel. No claim of complete density closure.
- P1: multi-reference ordering/removal and busy/error recovery lack full visible/action coverage.
- Acceptance blockers: native capture API returned unsupported interface twice; physical
  DPI/2K/4K, complete state matrix, installed fresh/upgrade replay are not available as proof.
- P2: mixed icon semantics in planning reference menu and residual heavy dividers need review.

Do not promote the Candidate gate based on the screenshot producer or passing unit tests.
