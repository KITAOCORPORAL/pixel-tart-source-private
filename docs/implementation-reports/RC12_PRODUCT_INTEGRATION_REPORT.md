# Pixel Tart RC12 Product Integration Report

Date: 2026-09-14  
Branch: `integration/pixel-tart-developer-preview`  
Final HEAD: recorded by the final commit on this branch (see repository HEAD).

## Git and test infrastructure

RC12 began at RC11 `15dbe966`. `RC12_START_TRUTH_AUDIT.md` classifies every requested area as DONE, SERVICE ONLY, UI ONLY, PARTIAL or MISSING. `WPF_FULL_SUITE_RC12.md` separates current product-gate suites from legacy archival WPF evidence. The Release solution build is green with 0 warnings and 0 errors.

## Product integration status

- Project / Booking links: Asset Inspector now exposes dark picker panels backed by existing Projects/ShootBookings, with multi-select add/remove persistence; Calendar navigation remains open.
- Client: existing booking client fields were audited and a unified resolver exists; wiring the resolved display into Inspector remains open.
- Calendar ↔ assets: Booking Detail now loads BookingAssetLink records into a shared-thumbnail strip with real workflow counts; deep-link navigation and BookingId-filtered library routing remain open.
- Persistent Preview: RC11 implementation remains active and restart/offline-tested through the shared provider.
- Inspiration Collections: visual panel/grid, create/open/archive and add-selection actions are now connected to the RC11 SQLite service; drag/drop, rename, project header and offline thumbnail routing remain open.
- Recent Libraries: switching APIs remain available; full recent-library UI and safe session-drain acceptance are open.
- EXIF: Inspector now loads real JPEG metadata through `JpegMetadataService` for camera, lens, ISO, exposure, aperture and focal length. Missing tags remain `未记录`; no values are fabricated.
- Context Menu: existing real actions remain; Project/Booking/Collection entries are not promoted until their UI commands are connected.
- Visual Harness / DPI: current product screenshot harness and process-isolated WPF host are not yet complete; historical evidence remains archival-only.
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

The explicit RC12 target rows requiring real UI acceptance remain PARTIAL: Project/Booking/Client picker controls, Calendar visual asset workflow, Inspiration Collection visual UI, recent-library switcher presentation, complete contextual actions, current-version process-isolated WPF/DPI harness, themed 12-screenshot set, and RC12-specific visual timing. These are tracked honestly rather than promoted from service-layer evidence.
