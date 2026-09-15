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
| WPF Process Isolation | DONE | Final current producer ran 83 fixtures in 83 independent processes: 1162 discovered, 1162 passed, 0 failed, 0 skipped; `process_per_fixture=true`, `application_singleton_shared=false`. The run includes the P2 sealed-run validator probe and opt-in 10K/50K/100K performance coverage. |
| Current DPI Evidence | DONE | Current-HEAD RC12 Product Visual Harness produced 32 current-DPI captures at 100/125/150/200%; independent validator passed all 50 captures. |
| Product Screenshot Harness | DONE | Real App.xaml/MainWindow Pixel Tart dark harness produced all 12 required screenshots plus 6 resolution captures at source HEAD `4364b458`; evidence validator and visual audit passed. |
| Visual Performance | DONE | Current WPF diagnostics now run three samples at 10K/50K/100K for library open, first visible real thumbnail, scroll, search, rating filter, Project/Booking logical filters, restart cached preview, working set, thumbnail queue and virtualized realization. At 100K, P95 library open is 328.22ms, scroll 37.50ms and search 1676.45ms; only 500 rows are loaded and 20 containers realized. The earlier 10,128-row public batch-command evidence remains complementary. |

## Gate decision

RC12 Completion Pass II is **product-gate complete** at validated product source HEAD `4364b458ee6d9ee37025056d7f2ab7253a996935`. The sealed P2 automated acceptance completed successfully and passed immutable `ValidateExistingRun`; the final WPF producer passed 1162/1162 with zero failures and zero skips; current-HEAD visual evidence passed 50/50. All requested product paths, source-safety guarantees and scale-performance checks are green. The final RC12 installer was then built from clean packaging HEAD `3639f7e03f47522ef84391064ba88d140c5e8f0b`; that delta contains only gate documentation and the reproducible RC12 packaging script. RC13 remains out of scope.
