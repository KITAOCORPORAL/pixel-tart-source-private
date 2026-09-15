# RC12 Completion Gap Audit — Pass II

Date: 2026-09-15
Branch: `integration/pixel-tart-developer-preview`

This audit records implementation truth, not inherited regression claims. `DONE` means the current RC12 path has UI, command, persistence/reload behavior and a current targeted test. `SERVICE_ONLY`, `UI_ONLY` and `PARTIAL` are intentionally not product-gate passes.

| Area | Current truth | Evidence / remaining gap |
|---|---|---|
| Project Picker | DONE | Dark picker, search/recent/current-booking grouping and persistent `ProjectAssetLink` are wired. A current real-database WPF test covers multi-select add, remove, re-add and restart persistence. |
| Booking Picker | DONE | Dark picker, search/recent/current-project grouping, date sorting and persistent `BookingAssetLink` are wired. A current real-database WPF test covers multi-select add, remove, re-add and restart persistence. |
| Client Resolver | DONE | `ClientDisplayResolver` consumes real Booking and online-Project client names, explicitly handles multiple clients, and a current WPF restart fixture asserts the rendered real Booking client after persistence/reload. |
| Workflow Status | DONE | Five statuses are persisted as `AssetWorkflowMetadata`; Inspector, Context Menu and multi-select entry points are wired. |
| Calendar asset strip | DONE | Booking Detail renders shared-provider thumbnails, offline badge and real workflow counts; the current `CalendarBookingAssets` product capture proves the rendered strip. |
| Calendar deep link | DONE | Booking Detail “查看全部素材 / 查看项目素材” emits the correct Booking/Project request and applies logical relation filters. The current real-database WPF round-trip fixture proves the thumbnail count, both routes, exact IDs and exclusion of an unrelated asset. |
| Asset → Calendar | DONE | Real booking navigation resolves date/month, opens booking and detail. A current real-database WPF restart test executes “查看拍摄” from the persisted Inspector booking item and verifies the exact Booking ID sent to Calendar. |
| Inspiration Collection UI | DONE | Existing SQLite CRUD is exposed as visual collection panel/grid with create/open/archive/add/rename, project header, drag/drop and manual reorder. Project association now opens a real picker, renders the project name and supports removal; a current WPF test proves select, restart persistence, remove and second restart. |
| Collection DragDrop | DONE | Gallery/tray/collection payloads are wired through `InspirationDragDropBehavior` to add, move and reorder commands; the UI contract verifies all target kinds and forbids source file move/delete. |
| Recent Library Switcher | DONE | Current/recent menu shows name/path/time/Online-Offline, open/switch, Explorer locate and remove-entry-only behavior. A current two-container WPF test proves switching, settings reload/restart, Online/Offline presentation and that removing a recent entry leaves its `.ptlibrary` intact. |
| Safe Library Switching | DONE | Old page thumbnails and tracked operations are cancelled/drained before releasing the write lease. Current WPF regression holds an A-library provider request open, activates/completes B, releases A, and proves no old-library writeback or pending request remains. |
| Real EXIF | DONE | Shared metadata pipeline surfaces capture time, dimensions, orientation and photography fields for JPEG plus TIFF/DNG-compatible containers. Standard EXIF dimension fallback, corrupt/unsupported RAW fail-safe behavior, and no-fabrication semantics have current tests. |
| Context Menu | DONE | Exactly six product groups are wired to viewer/default app/Explorer/copy path, organize/rating/color, Project/Booking/Workflow, Inspiration, real export and recoverable lifecycle commands; permanent delete is absent. Current WPF tests cover menu structure plus multi-select rating/workflow/archive/restore/trash/recover persistence and source-file safety across restart. |
| WPF Process Isolation | PARTIAL | Corrected current producer ran 79 fixtures in 79 processes: 1153 discovered, 1151 passed, 0 failed, 2 not executed; `process_per_fixture=true`, `application_singleton_shared=false`. The performance opt-in was then run separately and passed with a fresh 10,128-row fixture; the P2 sealed-run probe still lacks an eligible historical run root. Two legacy export tests remain tracked separately. |
| Current DPI Evidence | DONE | RC12 Product Visual Harness produced 32 current-DPI captures at 100/125/150/200%; independent validator passed all 50 captures. |
| Product Screenshot Harness | DONE | Real App.xaml/MainWindow Pixel Tart dark harness produced all 12 required screenshots plus 6 resolution captures; evidence validator passed. |
| Visual Performance | DONE | Current WPF diagnostics now run three samples at 10K/50K/100K for library open, first visible real thumbnail, scroll, search, rating filter, Project/Booking logical filters, restart cached preview, working set, thumbnail queue and virtualized realization. At 100K, P95 library open is 328.22ms, scroll 37.50ms and search 1676.45ms; only 500 rows are loaded and 20 containers realized. The earlier 10,128-row public batch-command evidence remains complementary. |

## Gate decision

RC12 remains **not complete**. Current code now has verified product paths for relation pickers, client resolution, both Calendar ↔ Asset directions, workflow state, Inspiration Collections, safe recent-library switching, Context Menu, real TIFF/DNG-compatible EXIF reads and the 10K/50K/100K visual-performance matrix. The remaining gate blocker is the P2 sealed-run probe/full zero-skip isolation seal. Do not generate a new RC13 or final RC12 installer until that fail-closed gate is resolved. The installer currently present remains the earlier, unaccepted RC12 build.
