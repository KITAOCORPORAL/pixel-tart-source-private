# AcceptanceKit v2 — selector audit and date-value repair

2026-09-20. Status: AWAITING FRESH LOCAL ACCEPTANCE RUN. Tool-only changes; no product rebuild.

## First actual run (user supplied)

Evidence retained unchanged at `D:/PixelTart-Installed-Acceptance-Kit-8cb95e6/acceptance-result/20260920-135509-417cc2/`.

The actual run passed preflight 6/6, isolated Setup installation, application startup, planning navigation/list, home screenshot, create dialog, project-name entry and location entry. It stopped at date-value because Name=拍摄日期 plus ValuePattern matched the outer DatePicker and inner TextBox. This is a runner defect, not a product crash. No new project was confirmed, no full modules/PDF/restart/upgrade PASS is inferred.

Recorded failure.tree.json SHA256: `6247A91C6840B2471625D285C47681CBE0D2C083BB6F3F9BE459AE2387DF0C92`.

## Exact date selector

ScopePath: Custom / 摄影策划案工作区 → Text / 新建策划案 (unique heading's raw parent container) → Custom / 拍摄日期.

Target: AutomationId=PART_TextBox, ControlType=Edit, Name=拍摄日期 (all three must match). Used for both date-value and date-check in both plans. Date button uses PART_Button + Button inside the same date scope.

Recorded-tree target match count: exactly 1 (PASS). The input dump is flat and lacks parent links, so full ancestor-path traversal has NOT been live-verified; no invented hierarchy evidence. Actual current-state live run remains pending. Zero fails, multiple fail, no first/index fallback.

## Complete selector audit

| Group | Correction / audit | Evidence and remaining uncertainty |
|---|---|---|
| Global nav | SidebarRoot + exact ID + Button | IDs/type/root observed in real tree |
| Planning list | DocumentList + List; project title descendant for list items | ID/type observed |
| Create fields/buttons | Unique modal-heading scope; date nested Custom area; name/location Edit | Observed peers; path tested offline only |
| Seven module buttons | Custom planning workspace + Name + Button | Real tree does not expose named UniformGrid navigation container; removed that invalid ancestor dependency |
| Body edit/finish/content | Workspace → DocumentScroll, explicit Button/Edit/Text | Scroll peer observed; later populated content not yet live-observed |
| Preview/toolbar | Workspace + exact semantic Name/type; hidden navigation checked through actual text button, not absent container | No global generic 编辑 search |
| Reference tiles | Workspace → DocumentScroll, named Custom tile; preview close uses CloseImageButton | Source-derived; tile peer type still needs live evidence |
| Context menu | Unique visible Menu within tested PID → MenuItem by exact label | No assumed parent relationship back to underlying image for detached popup |
| Calendar | Unique visible Calendar in tested PID, without guessing unobserved name or PART_Calendar | Live calendar not reached yet; fail with diagnostics if zero/multiple |
| Booking/export modal | Workspace → unique heading container, List/ComboBox/Button types | All sibling selectors audited when opened |
| PDF quality | Named ComboBox → specific ListItem | Popup peer placement remains live-dependent |
| Native file dialogs | Exact dialog Window → 1148 Edit / 1 Button | Scope retained; no process-global file-name searches |
| Export message/confirm | Named themed Window → MessageTextBlock Text / YesButton Button | Existing source IDs; avoids generic global 确定 |
| Optional close confirmation | Named themed Window + 保存 Button | Optional absent remains expected; ambiguity still fails |
| Tether/online | Explicit workspace type/name + content landmarks | Source-derived; not yet visited in actual run |
| Screenshot required elements | Reuses formal typed/scoped selectors when unambiguous | Avoids menu-label/text duplication in screenshot bounds lookup |

Every named selector has type and scope (except a dialog Window selector, already PID-root scoped). Names are auxiliary to IDs where real IDs exist; no fabricated IDs are added to product. `ScopePath` allows ordered nested typed scopes. Multiple UIA roots referencing the same runtime element are deduplicated by Automation.Compare, not by text. Retained legacy schema remains fail-closed.

## Diagnostics and proactive checkpoints

Target or ancestor ambiguity writes selector-diagnostics.json containing StepId, RequestedSelector, MatchCount, Phase and EVERY candidate. Each candidate contains Name, AutomationId, ControlType, ClassName, numeric BoundingRectangle, ParentName, ParentAutomationId and AncestorChain. Unavailable peers retain an error record rather than silently disappearing. Missing required targets also emit a zero-match diagnostic. Existing UIA tree and gap report remain.

Full plan remains 124 steps plus 15 non-mutating checkpoint groups; upgrade remains 70 steps plus 5 groups. At each state entry, all group selectors are checked before proceeding, collecting sibling failures rather than stopping after the first one. Groups include create modal (name/date/location/confirm), seven navigation buttons and toolbar, editing/finish editing, calendar, reference/Quick Preview/menu, booking, PDF quality/file dialog, tether menu and online content.

`--audit-selectors <kit-root>` is implemented as a LIVE isolated diagnostic: install/start, open create modal, inspect siblings, STOP without text entry or project creation. It reports SELECTOR_AUDIT_ONLY and leaves the test window for diagnosis. Later states are explicitly NOT_VISITED. Normal complete plans perform those later checks on state entry. No resume stitching is implemented; formal acceptance always starts a new fresh installation and must pass the entire workflow.

## Checks performed here

- Release build: 0 warnings / 0 errors.
- Fresh and upgrade schema/path/hash validation: PASS.
- Existing offline validation tests: 23 PASS.
- Selector tests: 8 PASS covering AmbiguousSelectorFailsClosedTests, ScopedAutomationIdResolvesUniqueElementTests, DateValueSelectorUsesInnerPartTextBoxTests (both plans), DuplicateNameDoesNotChooseFirstTests, SelectorDiagnosticContainsAllMatchesTests, plus both full-plan scope checks.
- Tests use recorded/fabricated metadata and the same exact-one guard and diagnostic serializer; they do not prove framework live UIA behavior.
- Portable test: 7/7 PASS in `C:\Users\Administrator\Downloads\PixelTartAcceptanceV2PortableTest-20260920`; actual extracted BAT preflight 6/6, direct self-contained Runner argument handling, missing runner/dependency/installer/plan and tampered plan, then restored success. Missing/tampered files were tested reversibly. No product is installed/launched by this pass.
- Computer-use restrictions remain respected: no custom live UIA, --run, --audit-selectors or live --kit executed. No claim that all future selectors are proven unique.

## Delivery

Version: AcceptanceKit v2, identified in BAT and KIT_VERSION.txt and per-run acceptance.json.

ZIP: `artifacts/acceptance-kit-v2/PixelTart-Installed-Acceptance-Kit-8cb95e6.zip`.

Bytes: 213,387,847. SHA256: `C47FA28EEBA222EA818E3A9DF4932D7A1F0B72332FFC3B292F3D81DDC9488FAE`.

PRODUCT_SOURCE_SHA unchanged: `8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc`.

Installer reused unchanged: `C2E5F26A1D00BFE2A466450C0BB9D015CDD35D6733E909FE8ADCB5BAEFA6247D`.

No src/, installer/, PlanningCenter or ProposalDatePicker edits. No old user result files overwritten or deleted. New kit is in a separate v2 directory; every live run uses new timestamp/random output and unique isolated installation/data root. Extract new ZIP separately and run its BAT for a fresh acceptance pass.
