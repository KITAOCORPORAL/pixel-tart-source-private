# Pixel Tart RC12 Product Integration Report

Date: 2026-09-15
Branch: `integration/pixel-tart-developer-preview`  
Product-gate source HEAD: `4364b458ee6d9ee37025056d7f2ab7253a996935`.

## Git and test infrastructure

RC12 began at RC11 `15dbe966`. `RC12_START_TRUTH_AUDIT.md` classifies every requested area as DONE, SERVICE ONLY, UI ONLY, PARTIAL or MISSING. `WPF_FULL_SUITE_RC12.md` separates current product-gate suites from legacy archival WPF evidence. The Release solution build is green with 0 warnings and 0 errors.

## Product integration status — Completion Pass II final gate

- Project / Booking links — DONE: Asset Inspector exposes dark picker panels backed by existing Projects/ShootBookings, with search, recency/context grouping and multi-select add/remove persistence. A current isolated real-database WPF test covers add, remove, re-add and restart recovery for both relation types.
- Client — DONE: `ClientDisplayResolver` is wired to real Booking client fields and online-selection Project client fields; multiple clients are explicit. The current restart fixture asserts the rendered Booking client name rather than a service-only value.
- Calendar ↔ assets — DONE: Booking Detail loads BookingAssetLink records into a shared-thumbnail strip with real workflow counts. “查看全部素材 / 查看项目素材” applies logical BookingId/ProjectId filters, and Asset Inspector “查看拍摄” returns the persisted Booking ID to Calendar. The current round-trip fixture proves two linked assets remain visible while an unrelated asset is excluded for both filters.
- Persistent Preview: RC11 implementation remains active and restart/offline-tested through the shared provider.
- Inspiration Collections — DONE: visual panel/grid, create/open/archive/add/rename, project header, drag/drop and manual reorder are connected to the SQLite service. Project association uses a real picker, renders the project name and supports removal; the current WPF test proves select/restart/remove/restart while the UI contract verifies every drag target.
- Recent Libraries — DONE: current/recent UI shows path/time/Online-Offline, open/switch, Explorer locate and remove-entry-only. A current two-container WPF test proves switch persistence, settings reload/restart and no `.ptlibrary` deletion; safe task-drain separately proves the old request is drained without writeback while the new library completes.
- EXIF: Inspector loads real JPEG/TIFF/DNG-compatible metadata through `JpegMetadataService` for camera, lens, ISO, exposure, aperture, focal length, capture time, dimensions and orientation. Standard EXIF dimensions are used when no JPEG directory exists; corrupt/unsupported RAW returns a friendly error and missing tags remain `未记录` without fabrication.
- Context Menu — DONE: exactly six product groups are wired to live viewer/default app/Explorer/copy path, organize/rating/color, Project/Booking/Workflow, Inspiration, export and recoverable lifecycle actions; permanent delete is absent. Current multi-select runtime coverage proves rating/workflow/archive/restore/trash/recover persistence and source safety after restart.
- Visual Harness / DPI — DONE: current-HEAD RC12 Product Visual Harness is independently validated: 12 required product screenshots, 32 current-DPI captures (100/125/150/200%), six Asset Library resolution captures, real App.xaml/MainWindow/dark theme, synthetic assets only, process-per-capture lifecycle isolation and unchanged source assets. The visual audit found no clipping, overflow or P0 visual defect.
- Performance — DONE: Current WPF diagnostics run three samples at 10K/50K/100K for library open, first visible real thumbnail, scroll, search, rating filter, Project/Booking logical filters, restart cached preview, working set, thumbnail queue and virtualized realization. At 100K, P95 library open is 328.22ms, scroll 37.50ms, search 1676.45ms and rating filter 276.67ms; only 500 rows are loaded and 20 containers realized. The complementary 10,128-row public batch-command evidence also passes.

## Tests

- Release solution build with warnings treated as errors: PASS, 0 warnings / 0 errors.
- Current RC12 focused completion regression: PASS, 15/15, 0 skipped (relations, Calendar round-trip, Inspiration Project picker, Recent Libraries, Context Menu, EXIF and safe thumbnail switching).
- P2 automated acceptance: PASS. The sealed current-HEAD run completed all ten scenarios plus restart, schema-v7 database audit, process cleanup and source-safety checks; `ValidateExistingRun` then passed without mutating the sealed tree.
- Current WPF process isolation: PASS, 83 fixtures / 1162 tests / 1162 passed / 0 failed / 0 skipped. This includes the sealed P2 validation probe and opt-in scale-performance fixture.

## Final RC12 installer — BUILT

All P0 product gates were green before packaging. The earlier file at the final path was overwritten by a fresh self-contained win-x64 publish and Inno Setup 7 build from clean source HEAD `3639f7e03f47522ef84391064ba88d140c5e8f0b`:

`artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC12_x64.exe`

- File size: 51,209,065 bytes
- SHA-256: `4FDD855257A4F854CF0C347B40556E3A8BF832ACAC6FA810D14BF34D2FB51D97` (independently calculated twice; hashes match)
- Build window: 2026-09-15 11:36:19–11:37:09 +08:00
- Publish + packaging duration: 49.867 seconds
- Published files: 284
- Published product/file version: 2.3.0 / 2.3.0.0
- Authenticode: `NotSigned` (explicitly reported; no signing claim is made)
- Git: installer and publish output are untracked release artifacts and were not committed

## Current evidence artifacts

- Product visual evidence: `artifacts/rc12-product-visual-final-4364b45/rc12-product-visual-evidence.json` — current source HEAD, validator PASS (50/50 captures).
- P2 sealed acceptance evidence: `.validation/P2-Automated-Acceptance-20260915-111028-696f8b25651b` — completed current-HEAD run, ten scenarios plus restart, and immutable existing-run validation PASS.
- WPF process isolation evidence: `artifacts/rc12-wpf-process-isolation-final-zero-skip/rc12-wpf-process-isolation.json` — 83 isolated fixtures, 1162 discovered, 1162 passed, 0 failed, 0 skipped.
- Visual performance evidence: the opt-in fixture is included in the final zero-skip WPF isolation run; its fresh 10,128-row input passed.
- Scale performance evidence: `artifacts/rc12-visual-performance-scale-completion-pass2-final/rc12-visual-performance-scale-evidence.json` — 10K/50K/100K, three samples each, real first-visible thumbnails, all requested product operations, working set/queue and virtualization metrics PASS.
- Project/Booking/Client/Calendar round-trip evidence: `AssetLibraryProductRelationEndToEndTests.ProjectBookingAndClientPersistAcrossRestartAndBookingOpensInCalendar` — real product and asset SQLite databases, multi-select add/remove/re-add, dispose/recreate, Inspector render assertions, exact Asset → Calendar Booking callback, Booking Detail thumbnail count, and Booking/Project logical filter assertions PASS.
- Inspiration Project evidence: `AssetLibraryProductRelationEndToEndTests.InspirationCollectionProjectUsesPickerAndPersistsSelectionAndRemoval` — multiple real Projects, explicit picker selection, rendered Project name, restart persistence, removal and second restart PASS.
- Recent-library evidence: `AssetLibraryRecentLibrarySwitcherEndToEndTests.SwitchPersistsAcrossReloadAndRemoveRecentNeverDeletesLibrary` — two real portable containers, switch/settings reload/restart, Online/Offline presentation, remove-entry-only and retained container PASS.
- Context-menu evidence: `AssetLibraryContextMenuEndToEndTests.MultiSelectMetadataLifecycleAndSourceSafetyPersistAcrossRestart` — real multi-select rating/workflow/archive/restore/trash/recover, restart persistence and source-file retention PASS.
- Current targeted WPF product flow: 58/58 relation, client, visual, accessibility and embedded tests PASS.
- Visual audit: `docs/implementation-reports/RC12_VISUAL_AUDIT.md` records the screenshot-by-screenshot result and remaining tuning notes.

## Gate conclusion

Completion Pass II has no remaining P0 product-integration blocker. Project/Booking/Client, both Calendar directions, Inspiration Project relation, Recent Libraries, Context Menu, EXIF, current visual evidence, sealed P2 acceptance and 10K/50K/100K performance are all backed by current product evidence. Final RC12 packaging is complete and its immutable artifact metadata is recorded above. The release now waits for the user's real Windows install verification; no RC13 work is authorized.
