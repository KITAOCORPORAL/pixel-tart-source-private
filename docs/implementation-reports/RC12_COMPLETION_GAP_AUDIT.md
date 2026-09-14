# RC12 Completion Gap Audit — Pass II

Date: 2026-09-14  
Branch: `integration/pixel-tart-developer-preview`

This audit records implementation truth, not inherited regression claims. `DONE` means the current RC12 path has UI, command, persistence/reload behavior and a current targeted test. `SERVICE_ONLY`, `UI_ONLY` and `PARTIAL` are intentionally not product-gate passes.

| Area | Current truth | Evidence / remaining gap |
|---|---|---|
| Project Picker | PARTIAL | Dark picker, search/recent/current-booking grouping, multi-select add/remove and persistent `ProjectAssetLink`; no dedicated end-to-end restart UI test yet. |
| Booking Picker | PARTIAL | Dark picker, search/recent/current-project grouping, date sorting and persistent `BookingAssetLink`; no dedicated end-to-end restart UI test yet. |
| Client Resolver | PARTIAL | `ClientDisplayResolver` now receives real Booking and online-Project client names and explicitly reports multiple clients; no full UI fixture assertion yet. |
| Workflow Status | DONE | Five statuses are persisted as `AssetWorkflowMetadata`; Inspector, Context Menu and multi-select entry points are wired. |
| Calendar asset strip | PARTIAL | Booking Detail renders shared-provider thumbnails, offline badge and real workflow counts; current product harness evidence is still missing. |
| Calendar deep link | PARTIAL | Booking Detail exposes “查看全部素材 / 查看项目素材”; navigation applies logical `CandidateAssetIds` filters. Asset → Calendar exposes “查看拍摄”; current product harness evidence is still missing. |
| Asset → Calendar | PARTIAL | Real booking navigation resolves date/month, opens booking and detail; no current screenshot/harness evidence yet. |
| Inspiration Collection UI | PARTIAL | Existing SQLite CRUD is exposed as visual collection panel/grid with create/open/archive/add; rename, project header, drag/drop and manual reorder UI remain. |
| Collection DragDrop | SERVICE_ONLY | Service supports membership/reorder; no complete current UI drag/drop path. |
| Recent Library Switcher | PARTIAL | Current/recent menu shows name/path/time/Online-Offline, open/switch, Explorer locate and remove-entry-only behavior; targeted persistence test exists. |
| Safe Library Switching | PARTIAL | Old page thumbnails and tracked operations are cancelled/drained before releasing the write lease; a full background thumbnail → switch A→B isolation acceptance is not yet present. |
| Real EXIF | PARTIAL | JPEG metadata pipeline now surfaces capture time, dimensions, orientation and photography fields; TIFF/RAW safe-read expansion and complete current Inspector test remain. |
| Context Menu | PARTIAL | Six product groups are wired, with viewer/default app/Explorer/copy path, Project/Booking/Workflow, Inspiration, export and lifecycle actions; current UI contract passes, but some “other app” and replacement actions remain intentionally unavailable. |
| WPF Process Isolation | FAIL | Existing targeted suites still include legacy same-process fixtures; process-per-fixture implementation is not complete. |
| Current DPI Evidence | FAIL | Current-version producer/validator and required DPI evidence set are not complete. |
| Product Screenshot Harness | FAIL | Real themed MainWindow harness and required 12 screenshot set are not complete. |
| Visual Performance | PARTIAL | Repository 10K/50K/100K gate remains inherited; RC12 visual timing harness for thumbnails, scroll, relation filters, switching and restart cache is not complete. |

## Gate decision

RC12 is **not complete**. Do not generate a new RC13 or final RC12 installer until every P0 row is promoted with current evidence. The installer currently present remains the earlier, unaccepted RC12 build.
