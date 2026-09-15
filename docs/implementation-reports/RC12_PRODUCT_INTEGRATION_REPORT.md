# Pixel Tart RC12 Product Integration Report

Date: 2026-09-15
Branch: `integration/pixel-tart-developer-preview`  
Evidence working-tree base HEAD: `5bc2602fa2ac4f0a8560e16b31346e18b3d626c5` (the completion pass is not yet a final release commit).

## Git and test infrastructure

RC12 began at RC11 `15dbe966`. `RC12_START_TRUTH_AUDIT.md` classifies every requested area as DONE, SERVICE ONLY, UI ONLY, PARTIAL or MISSING. `WPF_FULL_SUITE_RC12.md` separates current product-gate suites from legacy archival WPF evidence. The Release solution build is green with 0 warnings and 0 errors.

## Product integration status — Completion Pass II checkpoint

- Project / Booking links — DONE: Asset Inspector exposes dark picker panels backed by existing Projects/ShootBookings, with search, recency/context grouping and multi-select add/remove persistence. A current isolated real-database WPF test covers add, remove, re-add and restart recovery for both relation types.
- Client — DONE: `ClientDisplayResolver` is wired to real Booking client fields and online-selection Project client fields; multiple clients are explicit. The current restart fixture asserts the rendered Booking client name rather than a service-only value.
- Calendar ↔ assets — DONE: Booking Detail loads BookingAssetLink records into a shared-thumbnail strip with real workflow counts. “查看全部素材 / 查看项目素材” applies logical BookingId/ProjectId filters, and Asset Inspector “查看拍摄” returns the persisted Booking ID to Calendar. The current round-trip fixture proves two linked assets remain visible while an unrelated asset is excluded for both filters.
- Persistent Preview: RC11 implementation remains active and restart/offline-tested through the shared provider.
- Inspiration Collections — DONE: visual panel/grid, create/open/archive/add/rename, project header, drag/drop and manual reorder are connected to the SQLite service. Project association uses a real picker, renders the project name and supports removal; the current WPF test proves select/restart/remove/restart while the UI contract verifies every drag target.
- Recent Libraries — DONE: current/recent UI shows path/time/Online-Offline, open/switch, Explorer locate and remove-entry-only. A current two-container WPF test proves switch persistence, settings reload/restart and no `.ptlibrary` deletion; safe task-drain separately proves the old request is drained without writeback while the new library completes.
- EXIF: Inspector loads real JPEG/TIFF/DNG-compatible metadata through `JpegMetadataService` for camera, lens, ISO, exposure, aperture, focal length, capture time, dimensions and orientation. Standard EXIF dimensions are used when no JPEG directory exists; corrupt/unsupported RAW returns a friendly error and missing tags remain `未记录` without fabrication.
- Context Menu — DONE: exactly six product groups are wired to live viewer/default app/Explorer/copy path, organize/rating/color, Project/Booking/Workflow, Inspiration, export and recoverable lifecycle actions; permanent delete is absent. Current multi-select runtime coverage proves rating/workflow/archive/restore/trash/recover persistence and source safety after restart.
- Visual Harness / DPI: RC12 Product Visual Harness is current and independently validated: 12 required product screenshots, 32 current-DPI captures (100/125/150/200%), six Asset Library resolution captures, real App.xaml/MainWindow/dark theme, synthetic assets only, and process-per-capture lifecycle isolation. The corrected WPF isolation producer ran 79 fixtures in 79 processes (1153 discovered; 1151 passed; 0 failed; 2 not executed). The performance opt-in subsequently passed separately; the P2 sealed-run probe still lacks an eligible historical root.
- Performance — DONE: Current WPF diagnostics run three samples at 10K/50K/100K for library open, first visible real thumbnail, scroll, search, rating filter, Project/Booking logical filters, restart cached preview, working set, thumbnail queue and virtualized realization. At 100K, P95 library open is 328.22ms, scroll 37.50ms, search 1676.45ms and rating filter 276.67ms; only 500 rows are loaded and 20 containers realized. The complementary 10,128-row public batch-command evidence also passes.

## Tests

- Release solution build with warnings treated as errors: PASS, 0 warnings / 0 errors.
- Current RC12 focused completion regression: PASS, 15/15, 0 skipped (relations, Calendar round-trip, Inspiration Project picker, Recent Libraries, Context Menu, EXIF and safe thumbnail switching).
- Current WPF process isolation attempt: FAIL-CLOSED, 83 fixtures / 1160 tests / 1157 passed / 1 failed / 2 not executed. The one inventory failure is fixed and independently re-run 6/6; the full producer is still not zero-skip because the P2 sealed run is unavailable and performance is opt-in.

## Installer — NOT ACCEPTED / NOT REBUILT

The following installer predates Completion Pass II and is retained only as an unaccepted artifact:

`artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC12_x64.exe`

File size: 51,168,959 bytes  
SHA-256: `4664E930E70C3A873EA05BD7032528A29806CC92CE980E58CD301D5AC122D1EE`

No installer was generated or overwritten in this pass because the product gate is not green.

## Current evidence artifacts

- Product visual evidence: `artifacts/rc12-product-visual-pass2-final/rc12-product-visual-evidence.json` — validator PASS (50 captures).
- WPF process isolation evidence: `artifacts/rc12-wpf-process-isolation-after-completion-pass2/rc12-wpf-process-isolation.json` — 83 isolated fixtures, 1160 discovered tests, 1157 passed, 1 failed, 2 not executed. The inventory failure was fixed and its isolated fixture re-run is 6/6 in `artifacts/rc12-wpf-process-isolation-button-fixed`; full zero-skip proof remains open.
- Visual performance evidence: `artifacts/rc12-p3-visual-performance-corrected-final/rc12-visual-performance-evidence.json` — three fresh 10,128-row samples with nearest-rank P50/P95/max timings.
- Scale performance evidence: `artifacts/rc12-visual-performance-scale-completion-pass2-final/rc12-visual-performance-scale-evidence.json` — 10K/50K/100K, three samples each, real first-visible thumbnails, all requested product operations, working set/queue and virtualization metrics PASS.
- Project/Booking/Client/Calendar round-trip evidence: `AssetLibraryProductRelationEndToEndTests.ProjectBookingAndClientPersistAcrossRestartAndBookingOpensInCalendar` — real product and asset SQLite databases, multi-select add/remove/re-add, dispose/recreate, Inspector render assertions, exact Asset → Calendar Booking callback, Booking Detail thumbnail count, and Booking/Project logical filter assertions PASS.
- Inspiration Project evidence: `AssetLibraryProductRelationEndToEndTests.InspirationCollectionProjectUsesPickerAndPersistsSelectionAndRemoval` — multiple real Projects, explicit picker selection, rendered Project name, restart persistence, removal and second restart PASS.
- Recent-library evidence: `AssetLibraryRecentLibrarySwitcherEndToEndTests.SwitchPersistsAcrossReloadAndRemoveRecentNeverDeletesLibrary` — two real portable containers, switch/settings reload/restart, Online/Offline presentation, remove-entry-only and retained container PASS.
- Context-menu evidence: `AssetLibraryContextMenuEndToEndTests.MultiSelectMetadataLifecycleAndSourceSafetyPersistAcrossRestart` — real multi-select rating/workflow/archive/restore/trash/recover, restart persistence and source-file retention PASS.
- Current targeted WPF product flow: 58/58 relation, client, visual, accessibility and embedded tests PASS.
- Visual audit: `docs/implementation-reports/RC12_VISUAL_AUDIT.md` records the screenshot-by-screenshot result and remaining tuning notes.

## Remaining gaps

The remaining RC12 release blocker is the fail-closed P2 sealed-run probe/full zero-skip isolation seal detailed in `RC12_COMPLETION_GAP_AUDIT.md`. Project/Booking/Client, both Calendar directions, Inspiration Project relation, Recent Libraries, Context Menu and scale performance are promoted only where current WPF product evidence passes.
