# WPS Research Evidence Manifest

Status: **PARTIAL**. This is an audit of the material already present in `docs/research/wps/`, not a new WPS test run.

## Evidence rule

`VERIFIED` applies only to the exact fact recorded by an existing evidence item. A proposed probe, expected control, shortcut candidate, OOXML design note, or Pixel Tart recommendation does not verify WPS behavior. Existing written metadata is identified as a **recorded result**; its original command output was not preserved here, so it cannot substantiate UI or licensing claims. `NOT VERIFIED` does not mean a feature is absent.

## Inventory checked

| ID | Existing item | What it supports | Limit |
|---|---|---|---|
| E-001 | [Version baseline](00_WPS_VERSION_BASELINE.md), Observed baseline and Reproduction commands | Recorded Windows uninstall/version and executable file metadata | No raw command transcript, About dialog, or file hash attached |
| E-002 | [Information architecture](01_WPS_INFORMATION_ARCHITECTURE.md), [Writer context menus](02_WPS_WRITER_CONTEXT_MENU_MATRIX.md), [Writer shortcuts](03_WPS_WRITER_SHORTCUT_MATRIX.md), [Presentation context menus](04_WPS_PRESENTATION_CONTEXT_MENU_MATRIX.md), [Presentation shortcuts](05_WPS_PRESENTATION_SHORTCUT_MATRIX.md) | Probe lists and candidate actions only | No observed action log or before/after result |
| E-003 | [DOCX](06_DOCX_INTEROPERABILITY_RESEARCH.md) and [PPTX](07_PPTX_INTEROPERABILITY_RESEARCH.md) research | Package maps and proposed Pixel Tart interchange contracts | No WPS-authored file, import/export log, or render comparison |
| E-004 | [Writer](09_WPS_WRITER_COMPLETE_RESEARCH.md) and [Presentation](10_WPS_PRESENTATION_COMPLETE_RESEARCH.md) summaries | Explicit statement that UI and round trips were not performed | Summaries are not UI evidence |
| E-005 | `screenshots/writer/.gitkeep`, `screenshots/presentation/.gitkeep` | Screenshot directories exist | Zero WPS screenshots; placeholders are not images |
| E-006 | [Stage V Planning Center v1 report](../../implementation-reports/STAGE_V_PLANNING_CENTER_V1_REPORT.md) and [Planning proposal closure](../../implementation-reports/PLANNING_PROPOSAL_FINAL_PRODUCT_CLOSURE.md) | Historical Pixel Tart implementation and acceptance status | These are Pixel Tart records, not WPS evidence; installed acceptance remains pending |

## VERIFIED facts, with boundaries

| ID | Exact fact supported by existing material | Evidence | Boundary |
|---|---|---|---|
| V-001 | The baseline records a WPS Office uninstall `DisplayVersion` of `12.1.0.28505`. | E-001, Observed baseline | Recorded installation metadata only; edition/channel and live UI build NOT VERIFIED |
| V-002 | The baseline records `wps.exe` and `wpp.exe` file versions as `12,1,0,28505`. | E-001, Observed baseline | Recorded file metadata only; no feature or UI behavior follows from it |
| V-003 | The baseline records Windows 11 Professional `10.0.26200` on a 64-bit host. | E-001, Observed baseline | Host architecture does not establish executable architecture |
| V-004 | The WPS screenshot folders contain only `.gitkeep` placeholders. | E-005, directory inventory | Verifies absence of a screenshot in this package, not absence of a WPS feature |

The earlier manifest entry about a repository start HEAD and dirty RAW/TIFF/GPU changes is removed: it was not WPS product evidence, and no matching task log is present in this package.

## NOT VERIFIED

| ID | Claim or test area | Missing evidence |
|---|---|---|
| N-001 | Writer/Presentation About version, edition, license, language, update channel | About dialog capture or transcription |
| N-002 | Ribbon, tabs, sidebars, navigation, dialogs, status bar, context menus | Actual WPS screenshots and action notes |
| N-003 | Shortcut operation and collision behavior | Keypress log with before/after state |
| N-004 | WPS-created DOCX/PPTX import and round trip | Source and output files, hashes, comparison results |
| N-005 | WPS PDF/image export appearance, warnings, and fidelity | Export artifacts and render comparison |
| N-006 | Any WPS screenshot-based product experience conclusion | WPS screenshots; current count is zero |

The [Feature Matrix](08_WPS_COMPLETE_FEATURE_MATRIX.md) keeps every UI-dependent WPS row `NOT VERIFIED`. The [Planning Center borrowing plan](12_PIXEL_TART_PLANNING_CENTER_BORROWING_PLAN.md) is a product proposal grounded in existing Pixel Tart records, not a WPS parity claim.
