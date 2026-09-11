# Pixel Tart Weekend Sprint RC10 Report

This report is deliberately evidence-led. A capability is not presented as complete when it is still only a visual entry point.

## Git

- Branch: `integration/pixel-tart-developer-preview`
- Start HEAD: `e496b6b557adf9e378e0d11de3a3d50a70950f20`
- Final implementation HEAD before this report: `e344b30853f9864e24a585cfa676c2a083b08e5d`
- Phase commits:
  - `060d8e5` — audit RC9 parity evidence
  - `99fab8a` — anchored Filter popover
  - `02334f8`, `8c2925b` — source-safe Viewer and pan contract
  - `7e5fa40` — visual Inspiration Tray foundation
  - `1155a07` — recoverable Trash
  - `810ae41` — Project/Booking/Asset relationship persistence
  - `78cf15d`, `619b809` — selection export service and live contextual actions
  - `d5a7d88` — deterministic missing-thumbnail state
  - `e344b30` — RC10 performance and packaging gates

## Phase A

The audit found that e496b6b already had durable Folder, Tag, Smart Folder, Search, density, layout, import, multi-select and Gallery-to-Folder/Tag drag/drop behavior. Those rows stayed `DONE` only where persistence/reload tests existed. Filter advanced from a layout-consuming panel to an overlay popover with an active-condition badge. Workflow Inspector placeholders were not counted as done.

See `docs/design/ASSET_LIBRARY_EAGLE_PARITY_MATRIX.md` for the row-by-row before/after evidence.

## Phase B

- Viewer: dark, source-safe window opened by double click or context menu; Fit, 100%, zoom buttons, wheel zoom, pan, previous/next and Esc are implemented through the shared thumbnail provider. RAW remains embedded-preview/proxy behavior, never a claim of full RAW decode.
- Archive: existing database lifecycle, Archived collection, restore and undo remain source-safe.
- Trash: persistent recoverable lifecycle with restore, undo/redo and preservation of the prior archive state. No permanent-delete command exists and source files are never deleted.
- Export: multi-selection original-reference copy, managed-copy export and UTF-8 CSV are live. Existing output files are not overwritten; file conflicts are auto-numbered.

## Phase C

Dedicated many-to-many `ProjectAssetLink` and `BookingAssetLink` tables and workflow metadata are implemented. Relationships survive repository restart, and Inspector reads AssetOrigin, Project IDs, Booking IDs and WorkflowStatus from persisted records rather than fixed placeholder text.

Calendar detail thumbnail strips, Project/Booking pickers, Client name resolution and bidirectional Calendar deep links remain incomplete; therefore Calendar integration and the whole Inspector workflow row remain `PARTIAL`.

## Phase D

The existing InspirationTrayService remains the authority. The tray now presents thumbnail cards with source/resolution badges and individual removal. It stores stable references and does not copy or alter source images. Offline/missing sources show a visual placeholder instead of collapsing to a text-only list.

Named Inspiration Collection CRUD, tray drag/reorder and Project relation are not wired to this UI and remain `PARTIAL`.

## Performance

Release-mode synthetic metadata search (`SearchText=asset`, `MinimumRating=2`, first 100 results), each corpus freshly created and measured three times:

| Rows | Min | Median | Max |
|---:|---:|---:|---:|
| 10,000 | 17.37 ms | 17.53 ms | 167.14 ms |
| 50,000 | 83.20 ms | 83.57 ms | 84.46 ms |
| 100,000 | 166.65 ms | 166.96 ms | 167.13 ms |

The 100K median is below the unchanged 27-second ceiling. The test is `AssetLibraryRc10PerformanceTests`; raw local evidence is generated under ignored `artifacts/rc10/performance.json`. These numbers are repository search/first-page measurements, not end-to-end cold Gallery startup. Separate existing 100K visual-query and similarity acceptance remains in `ModularHarnessVisualScaleAcceptanceTests`.

## Tests

- Full Release solution build: PASS, 0 warnings / 0 errors.
- Core full suite before final performance test addition: PASS, 1,302 / 1,302.
- RC10 Folder/Tag/Smart/Search/Archive/Trash/Export/Workflow persistence tests are included in the core suite.
- Targeted Viewer, context, Filter/P3 and missing-thumbnail WPF suite: PASS, 34 / 34.
- RC10 10K/50K/100K performance gate: PASS, 1 / 1.
- Focus/DPI matrix renderer: PASS, 1 / 1 and 16 generated synthetic screenshots across 100/125/150/200% and four layouts.
- Legacy DPI evidence suite: 75 passed / 26 failed because this worktree does not contain the historical `artifacts/automated-dpi-review/2.0.4/AutomatedDpiScreenshotHashes.json` evidence bundle.
- Full WPF run: 1,066 passed / 85 failed / 2 skipped. Failures include a branch allow-list pinned to an older feature branch, repository-root contract tests resolving the sibling checkout, process-wide WPF Application singleton conflicts, and async-suite contamination. This is recorded as an acceptance-infrastructure gap, not reported as green.

## Screenshots

Sixteen new programmatic screenshots were generated from synthetic colored PNGs only under ignored `artifacts/rc10/wpf-evidence/screenshots/`, covering Grid/Masonry/Justified/List at 100/125/150/200%. Visual inspection found that these bare-control renders do not merge the app-level theme resources, so they are layout evidence but are not suitable as polished RC10 product screenshots.

The specifically requested named product captures (`01_eagle_shell.png` through `10_inspiration_tray.png`) are not claimed: a normal app window was not targetable through the available UI automation surface, and the legacy acceptance runner rejects this integration branch. No old screenshot was relabeled and no synthetic render was misrepresented as a live product capture.

## Installer

- File: `像素蛋挞_Setup_2.3.0_RC10_x64.exe`
- Local ignored path: `artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC10_x64.exe`
- Size: 51,167,098 bytes
- SHA-256: `3A207C22EE30D7D1B6A24370B93507076A5BB4FF7636A99F12C74A2EAC0F388D`
- RC8 and RC9 outputs were not overwritten. The installer and publish directory are ignored and are not committed.

## Remaining Gaps

1. Calendar booking details do not yet resolve and display Asset Library links, counts or a thumbnail strip; Calendar ↔ Asset Library deep-link navigation is absent.
2. Project/Booking relationship persistence exists, but human-readable picker/resolver UI and Client lookup are absent.
3. Named Inspiration Collection Create/Rename/Delete/Add/Remove UI and Project relation are absent.
4. The shared provider has the existing bounded 64 MiB memory cache. Portable-library preview directories exist and packages preserve them, but the WPF provider does not yet read/write that disk cache; cached offline thumbnails are therefore not guaranteed after process restart.
5. Library tree recent-library presentation remains incomplete.
6. Full WPF and historical DPI evidence infrastructure needs branch/root isolation fixes before it can be called green.
7. The required ten named live screenshots still need a targetable themed application run; generated synthetic DPI/layout captures are retained only as local evidence.
8. Permanent Delete remains `NOT PLANNED / DEFERRED`.
