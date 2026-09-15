# RC12 UX Simplification Report

Date: 2026-09-15  
Branch: `integration/pixel-tart-developer-preview`

## Outcome

Pixel Tart's primary photo workflows now describe what a photographer is doing. Safety verification, collision handling, source preservation, cancellation, recovery, library isolation and diagnostic IDs remain intact underneath the simplified interface.

## Before / after

| Surface | Before | After |
|---|---|---|
| Organize | grouping rules, operation mode, manifest language | four natural questions ending in `预览整理结果` and `开始整理` |
| RAW to JPG | decoder capability and technical options competed with the task | output, size and quality are primary; capture information and color settings are collapsed |
| Smart Folder | a technical rule tree with developer vocabulary | `满足` + condition rows, optional condition groups, live `找到 N 张照片` feedback |
| Gallery | permanent `★ 0`, dimensions/format noise and multiple similarity metrics | filename first, rating only when set, one similarity percentage |
| Inspector | raw media type, bytes, storage enum and Boolean missing state | actual format, KB/MB/GB, human storage location, missing warning only when abnormal |
| Task Center | internal task ID and diagnostics on every card | progress and actionable failure summary; diagnostics live in collapsed technical details |
| Library dialogs | library ID, content mode and transaction wording | library name, location and human file-location description |

## Click-cost audit

| Core task | Before | After | Change |
|---|---:|---:|---:|
| Import photos | 2 | 2 | direct import remains stable |
| Organize photos | 6 | 5 | removed technical confirmation step |
| RAW to JPG | 5 | 4 | advanced options no longer block the primary path |
| New Smart Folder | 7 | 5 | default condition editor opens directly |
| Batch tag | 4 | 4 | unchanged; competing auxiliary surfaces now close |
| Associate project | 4 | 4 | unchanged, with human relation labels |
| Open viewer | 2 | 2 | unchanged |

Counts start from the relevant primary product page and include the final action. They are an interaction audit, not a target to remove necessary confirmation.

## Hidden technical details

- Task ID is available in copied diagnostics only.
- Full paths and technical failures are available from `查看详细信息`.
- Hash algorithms, decoder/library names, database implementation and internal record IDs are absent from normal UI.
- Smart Folder still uses the same query document and nested-condition engine internally.

## Screenshots

The real themed application harness produces ten UX acceptance images in `artifacts/rc12-ux-simplification/ux-simplification`: toolbox, simple/preview organize, simple/advanced RAW conversion, clean library, human Smart Folder, clean Inspector, clean Task Center and collapsed error details. The sealed visual manifest contains 60 real-application captures: 12 product scenes, 10 UX scenes, 32 current-DPI scenes and 6 Asset Library resolution scenes. The UX contact sheet is `artifacts/rc12-ux-simplification/ux-simplification-contact-sheet.png`.

## Tests

- `ProductLanguageLeakTests`: visible XAML and audited runtime-message leak prevention.
- `UXSimplificationRegressionTests`: required language, gallery/Inspector/Task Center cleanup and auxiliary-surface exclusivity.
- `FirstTimePhotographerUXTests`: direct actions for import, organize, conversion, four-star filtering, Smart Folder and project association.
- Existing organize, RAW conversion and task-card contracts were updated to the photographer-facing language.
- The final process-isolated WPF gate runs 86 fixtures in separate processes and covers 1,182 tests, including the opt-in 10,128-row performance diagnostic with no skipped fixture.

## Acceptance status

| Goal | Result |
|---|---:|
| Internal terminology leaks in tested primary UI | 0 |
| Primary flows requiring programming knowledge | 0 |
| Raw True / False exposed | 0 |
| TaskId on primary UI | 0 |
| P1 / P2 / P3 as visible literal product text | 0 |
| LibRaw on RAW surface | 0 |
| SHA-256 on primary UI | 0 |

No new product feature, navigation level, database model or advanced setting was added.
