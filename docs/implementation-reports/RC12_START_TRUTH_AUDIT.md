# Pixel Tart RC12 Start Truth Audit

Date: 2026-09-14  
Starting HEAD: `15dbe966c682cfce922efdf5cc5124e5cf9cc2b3`

This audit distinguishes product UI from service/data foundations. A database table or model alone is not counted as a completed user workflow.

| Area | Truth at RC12 start |
|---|---|
| ProjectAssetLink / BookingAssetLink | SERVICE ONLY — durable repository APIs and reload tests exist; picker UI and navigation are not connected. |
| Inspiration Collection | SERVICE ONLY — RC11 added SQLite CRUD, project relation, membership, reorder and archive; visual collection UI is still missing. |
| Client resolver | MISSING — booking/client fields exist in calendar domain models, but Asset Inspector has no resolver-backed display. |
| EXIF | PARTIAL — existing MetadataExtractor/JpegMetadataService reads JPEG dimensions, camera, date, orientation and quality fields; Asset Inspector previously exposed placeholders. |
| Calendar thumbnails | MISSING — no proven Booking Detail mini-gallery using the shared thumbnail provider. |
| Recent Libraries | PARTIAL — workspace/container switching APIs exist; complete recent-library presentation and remove-without-delete flow are not evidenced. |
| Context menu | PARTIAL — viewer, organize, workflow, tray, export, archive and trash actions are live; Project/Booking/Collection actions are absent or disabled. |
| Screenshot harness | MISSING — RC10/RC11 renders were synthetic bare controls, not real App-themed MainWindow screenshots. |
| Preview cache | DONE — RC11 shared memory + bounded disk cache with offline/restart test. |
| WPF acceptance infrastructure | PARTIAL — branch coupling was fixed in RC11, but Application singleton/process isolation and current-version DPI producer remain open. |

## RC12 first implementation

The Inspector now calls the existing metadata pipeline for a selected, existing source file and publishes real camera/lens/exposure values. No values are fabricated; absent tags remain `未记录`, and missing/offline sources do not create fake metadata.

The remaining start-state gaps require product UI integration or a real WPF process/screenshot harness and are tracked as such in the RC12 product report.
