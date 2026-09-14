# Pixel Tart RC12 Product Integration Report

Date: 2026-09-14  
Branch: `integration/pixel-tart-developer-preview`  
Final HEAD: recorded by the final commit on this branch (see repository HEAD).

## Git and test infrastructure

RC12 began at RC11 `15dbe966`. `RC12_START_TRUTH_AUDIT.md` classifies every requested area as DONE, SERVICE ONLY, UI ONLY, PARTIAL or MISSING. `WPF_FULL_SUITE_RC12.md` separates current product-gate suites from legacy archival WPF evidence. The Release solution build is green with 0 warnings and 0 errors.

## Product integration status — Completion Pass II checkpoint

- Project / Booking links: Asset Inspector now exposes dark picker panels backed by existing Projects/ShootBookings, with search, recency/context grouping, multi-select add/remove persistence and restart-safe link tables. Current full end-to-end UI evidence remains PARTIAL.
- Client: `ClientDisplayResolver` is wired to real Booking client fields and online-selection Project client fields; multiple clients are explicit. Current full UI evidence remains PARTIAL.
- Calendar ↔ assets: Booking Detail loads BookingAssetLink records into a shared-thumbnail strip with real workflow counts. “查看全部素材 / 查看项目素材” applies logical BookingId/ProjectId filters, and Asset Inspector “查看拍摄” returns to the dated Booking detail. Current product harness evidence remains PARTIAL.
- Persistent Preview: RC11 implementation remains active and restart/offline-tested through the shared provider.
- Inspiration Collections: visual panel/grid, create/open/archive and add-selection actions are connected to the RC11 SQLite service; rename, project header, drag/drop and manual reorder UI remain open.
- Recent Libraries: current/recent UI now shows path/time/Online-Offline, open/switch, Explorer locate and remove-entry-only. Safe task-drain is implemented; a full background-task acceptance remains open.
- EXIF: Inspector loads real JPEG metadata through `JpegMetadataService` for camera, lens, ISO, exposure, aperture, focal length, capture time, dimensions and orientation. Missing tags remain `未记录`; no values are fabricated. TIFF/RAW safe-read expansion remains open.
- Context Menu: six product groups are wired to live viewer, organize, Project/Booking/Workflow, Inspiration, export and lifecycle actions. Current UI contract passes; remaining unavailable actions are intentionally not claimed.
- Visual Harness / DPI: current product screenshot harness, process-isolated WPF host and current-version DPI producer are not yet complete; historical evidence remains archival-only.
- Performance: inherited 10K/50K/100K repository gate is green; RC12 visual-library timing awaits a real themed MainWindow harness.

## Tests

- Release solution build: PASS, 0 warnings / 0 errors.
- Thumbnail provider: PASS, 4/4.
- Inspiration tray/collection: PASS, 4/4.
- RC10 visual-scale gate: PASS, 1/1.
- RC11 Core baseline: PASS, 1303/1303.

## Installer

RC12 installer is generated separately from RC11:

`artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC12_x64.exe`

File size: 51,168,959 bytes  
SHA-256: `4664E930E70C3A873EA05BD7032528A29806CC92CE980E58CD301D5AC122D1EE`

Build outputs remain ignored and no source files are deleted or modified.

## Remaining gaps

The explicit RC12 target rows requiring real UI acceptance remain PARTIAL/FAIL as detailed in `RC12_COMPLETION_GAP_AUDIT.md`: relation/client end-to-end UI evidence, Calendar visual workflow, Inspiration drag/drop/rename/project header, safe background-task switching acceptance, TIFF/RAW EXIF, process-isolated WPF, current DPI producer, themed 12-screenshot set and RC12-specific visual timing. These are tracked honestly rather than promoted from service-layer evidence.
