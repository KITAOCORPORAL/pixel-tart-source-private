# Asset Library Eagle parity matrix

Weekend Sprint start: `e496b6b557adf9e378e0d11de3a3d50a70950f20` on `integration/pixel-tart-developer-preview`.
Eagle behavior is based on `docs/product/eagle-reference/`. A row is `DONE` only when UI, command, persistence, reload behavior and an automated test all exist.

## RC9 truth audit

| Capability | UI | Command | Persistence | Reload test | Status / evidence |
|---|---:|---:|---:|---:|---|
| Library tree | Yes | Yes | Yes | Yes | DONE — current/recent library presentation includes name, path, last-opened time and Online/Offline state. Current two-container WPF coverage proves switch, settings reload/restart and remove-entry-only source safety. |
| Folder | Yes | Yes | Yes | Yes | DONE — hierarchy/create/rename/move/reorder/archive, multi-drop membership and undo/redo are covered by `AssetLibraryV15Tests`, `AssetLibraryP2CoreTests` and P2/P3 WPF acceptance. |
| Smart Folder | Yes | Yes | Yes | Yes | DONE — one canonical Query AST is stored in `SmartFolderQueryDocuments`; save/copy/edit/archive/reopen and invalid-reference behavior are covered by P3 repository/integrity/WPF tests. |
| Tag | Yes | Yes | Yes | Yes | DONE — groups, create/rename/move/reorder/archive/merge, batch membership and drag targets are covered by P3 repository/integrity/WPF tests. |
| Filter | Yes | Yes | Yes | Yes | DONE — the canonical Query AST now drives an anchored overlay popover with count badge and compact chips; closing it no longer consumes gallery layout space. |
| Search | Yes | Yes | Yes | Yes | DONE — name/file/tag/comment search, debounce, IME composition, clear/history and selection restoration are tested. Project/client search remains unavailable because those relations are not yet stored. |
| Density | Yes | Yes | Yes | Yes | DONE — thumbnail continuum and viewport anchor are covered by WPF layout tests. |
| Layout | Yes | Yes | Yes | Yes | DONE — Grid/Masonry/Justified/List and formal viewport behavior are tested. |
| Inspector core metadata | Yes | Read-only | Yes | Yes | DONE — file, dimensions, dates, rating, folders and tags read real repository data. |
| Inspector workflow fields | Yes | Yes | Yes | Yes | DONE — AssetOrigin, five-state WorkflowStatus and dedicated many-to-many Project/Booking links are real and reload-tested; Client resolves real names and the Asset → Calendar route is covered. Camera/lens/exposure remain truthful file metadata. |
| Context menu | Yes | Yes | Yes | Yes | DONE — exactly six groups expose selection-aware Viewer/default app/Explorer/copy path, organize/rating/color, Project/Booking/Workflow, Inspiration, export, archive and recoverable Trash. Multi-select persistence/restart and source safety have current WPF coverage; permanent delete is absent. |
| Preview / Viewer | Yes | Yes | N/A | Yes | DONE — double click and context command open the dark source-safe Viewer with Fit/100%/zoom/wheel/pan/previous/next/Esc; it shares the thumbnail provider and never writes source files. RAW is preview/proxy only, not claimed as full decode. |
| Import | Yes | Yes | Yes | Yes | DONE — picker and Explorer drop both create source-safe reference records; managed-copy remains explicit. |
| Export | Yes | Yes | N/A | Yes | DONE — multi-selection original/managed-copy export and metadata CSV are exposed; conflict auto-numbering and no-overwrite/source-hash contracts are tested. Existing package export remains reused separately. |
| Archive | Yes | Yes | Yes | Yes | DONE — archive, Archived collection, restore and undo are symmetrical and source-safe. |
| Trash | Yes | Yes | Yes | Yes | DONE — recoverable Trash persists across restart, supports restore/undo/redo, preserves prior archive state and never deletes source files. Permanent Delete stays deferred. |
| Multi-select | Yes | Yes | Yes | Yes | DONE — extended/marquee/context selection survives paging and query refresh. |
| Drag & drop | Yes | Yes | Yes | Yes | DONE — gallery single/multi selection to Folder/Tag changes metadata membership only; Explorer drop imports references without moving source files. |
| Inspiration Tray | Yes | Yes | Yes | Yes | DONE — thumbnail tray and visual Collection grid expose source/offline badges, add/remove, multi-select drag/reorder, archive and a real Project picker with remove/restart proof. |
| Project / Booking / Client | Yes | Yes | Yes | Yes | DONE — dark Project/Booking pickers cover search/context groups and multi-select add/remove over existing link tables; Client and both Calendar directions have current real-database restart/round-trip proof. |

Permanent Delete is `NOT PLANNED / DEFERRED` for this sprint. Reference assets never authorize deletion or mutation of source files.

## RC10 closure result

The Weekend Sprint report records the before/after status and concrete persistence/test evidence for every promoted row. Rows without all five proof layers remain `PARTIAL`; no placeholder-only surface is promoted to `DONE`.

## RC11 closure result

| Capability | RC11 evidence | Status |
|---|---|---|
| Persistent shared preview cache | Memory + bounded 512 MiB disk LRU, stable content-hash key, offline/restart WPF test | DONE |
| Inspiration Collections persistence | SQLite CRUD, Project relation, membership add/remove/reorder/archive, restart test | DONE (service layer) |
| Acceptance branch coupling | P1/P2/P3 runners and validators accept any named development branch and reject protected branches | DONE |
| Project / Booking / Client picker UI | Real picker/client/restart flow verified against current WPF product code | DONE |
| Calendar ↔ Asset deep link | Booking/Project logical filters and Asset → Calendar callback verified with linked and unrelated assets | DONE |
| Recent library switching UI | Real two-container switch/reload/remove-without-delete flow verified | DONE |
| Photography inspector fields | File metadata remains the source; no fabricated camera/lens/exposure values | PARTIAL |
| RC11 visual screenshot harness | No real themed product screenshot set with the required 11 filenames | PARTIAL |
| 10K/50K/100K performance | Existing RC10 gate remains green; RC11 cache-specific restart/offline path is covered | DONE (inherited gate + cache coverage) |

The RC11 report records the exact commits, tests, installer hash and unresolved evidence. Rows remain `PARTIAL` where UI, real product screenshots or full WPF isolation proof is absent.

## RC12 truth status

RC12 now has current WPF product evidence for recent libraries, Project/Booking/Client, Calendar ↔ Asset, Inspiration Collection, the six-group Context Menu and 10K/50K/100K visual performance. The remaining release blocker is the fail-closed P2 sealed-run/zero-skip isolation seal tracked in `RC12_COMPLETION_GAP_AUDIT.md`. These rows are not promoted from service persistence alone.
