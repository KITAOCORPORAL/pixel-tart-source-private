# Asset Library Eagle parity matrix

Weekend Sprint start: `e496b6b557adf9e378e0d11de3a3d50a70950f20` on `integration/pixel-tart-developer-preview`.
Eagle behavior is based on `docs/product/eagle-reference/`. A row is `DONE` only when UI, command, persistence, reload behavior and an automated test all exist.

## RC9 truth audit

| Capability | UI | Command | Persistence | Reload test | Status / evidence |
|---|---:|---:|---:|---:|---|
| Library tree | Yes | Yes | Yes | Partial | PARTIAL — portable-library switching is covered; recent-library tree presentation remains incomplete. |
| Folder | Yes | Yes | Yes | Yes | DONE — hierarchy/create/rename/move/reorder/archive, multi-drop membership and undo/redo are covered by `AssetLibraryV15Tests`, `AssetLibraryP2CoreTests` and P2/P3 WPF acceptance. |
| Smart Folder | Yes | Yes | Yes | Yes | DONE — one canonical Query AST is stored in `SmartFolderQueryDocuments`; save/copy/edit/archive/reopen and invalid-reference behavior are covered by P3 repository/integrity/WPF tests. |
| Tag | Yes | Yes | Yes | Yes | DONE — groups, create/rename/move/reorder/archive/merge, batch membership and drag targets are covered by P3 repository/integrity/WPF tests. |
| Filter | Yes | Yes | Yes | Yes | DONE — the canonical Query AST now drives an anchored overlay popover with count badge and compact chips; closing it no longer consumes gallery layout space. |
| Search | Yes | Yes | Yes | Yes | DONE — name/file/tag/comment search, debounce, IME composition, clear/history and selection restoration are tested. Project/client search remains unavailable because those relations are not yet stored. |
| Density | Yes | Yes | Yes | Yes | DONE — thumbnail continuum and viewport anchor are covered by WPF layout tests. |
| Layout | Yes | Yes | Yes | Yes | DONE — Grid/Masonry/Justified/List and formal viewport behavior are tested. |
| Inspector core metadata | Yes | Read-only | Yes | Yes | DONE — file, dimensions, dates, rating, folders and tags read real repository data. |
| Inspector workflow fields | Yes | Partial | Yes | Yes | PARTIAL — AssetOrigin, WorkflowStatus and dedicated many-to-many Project/Booking links are real and reload-tested. Camera/lens/exposure remain file metadata, Client and cross-module Calendar navigation are not yet connected. |
| Context menu | Yes | Yes | Yes | Yes | PARTIAL — selection-aware Viewer/external/reveal, organize/rating, workflow state, tray, export, archive and recoverable Trash are live. Project/Booking picker and Inspiration Collection actions remain intentionally disabled. |
| Preview / Viewer | Yes | Yes | N/A | Yes | DONE — double click and context command open the dark source-safe Viewer with Fit/100%/zoom/wheel/pan/previous/next/Esc; it shares the thumbnail provider and never writes source files. RAW is preview/proxy only, not claimed as full decode. |
| Import | Yes | Yes | Yes | Yes | DONE — picker and Explorer drop both create source-safe reference records; managed-copy remains explicit. |
| Export | Yes | Yes | N/A | Yes | DONE — multi-selection original/managed-copy export and metadata CSV are exposed; conflict auto-numbering and no-overwrite/source-hash contracts are tested. Existing package export remains reused separately. |
| Archive | Yes | Yes | Yes | Yes | DONE — archive, Archived collection, restore and undo are symmetrical and source-safe. |
| Trash | Yes | Yes | Yes | Yes | DONE — recoverable Trash persists across restart, supports restore/undo/redo, preserves prior archive state and never deletes source files. Permanent Delete stays deferred. |
| Multi-select | Yes | Yes | Yes | Yes | DONE — extended/marquee/context selection survives paging and query refresh. |
| Drag & drop | Yes | Yes | Yes | Yes | DONE — gallery single/multi selection to Folder/Tag changes metadata membership only; Explorer drop imports references without moving source files. |
| Inspiration Tray | Yes | Yes | Yes | Yes | PARTIAL — it is now a thumbnail card tray with source/offline badges and per-item removal over durable stable references. Named collection CRUD, drag reorder and Project relation remain foundation gaps. |
| Project / Booking / Client | Partial | Partial | Yes | Yes | PARTIAL — dedicated ProjectAssetLink and BookingAssetLink many-to-many persistence exists and Inspector reads it; Project/Booking pickers, Client resolution and Calendar deep links remain unconnected. |

Permanent Delete is `NOT PLANNED / DEFERRED` for this sprint. Reference assets never authorize deletion or mutation of source files.

## RC10 closure result

The Weekend Sprint report records the before/after status and concrete persistence/test evidence for every promoted row. Rows without all five proof layers remain `PARTIAL`; no placeholder-only surface is promoted to `DONE`.

## RC11 closure result

| Capability | RC11 evidence | Status |
|---|---|---|
| Persistent shared preview cache | Memory + bounded 512 MiB disk LRU, stable content-hash key, offline/restart WPF test | DONE |
| Inspiration Collections persistence | SQLite CRUD, Project relation, membership add/remove/reorder/archive, restart test | DONE (service layer) |
| Acceptance branch coupling | P1/P2/P3 runners and validators accept any named development branch and reject protected branches | DONE |
| Project / Booking / Client picker UI | Existing relationship tables only; picker/client/calendar UI not connected | PARTIAL |
| Calendar ↔ Asset deep link | No end-to-end visual navigation proof | PARTIAL |
| Recent library switching UI | Workspace APIs exist; complete recent-library presentation not evidenced | PARTIAL |
| Photography inspector fields | File metadata remains the source; no fabricated camera/lens/exposure values | PARTIAL |
| RC11 visual screenshot harness | No real themed product screenshot set with the required 11 filenames | PARTIAL |
| 10K/50K/100K performance | Existing RC10 gate remains green; RC11 cache-specific restart/offline path is covered | DONE (inherited gate + cache coverage) |

The RC11 report records the exact commits, tests, installer hash and unresolved evidence. Rows remain `PARTIAL` where UI, real product screenshots or full WPF isolation proof is absent.

## RC12 truth status

RC12 connects the existing photography metadata pipeline to the Inspector and preserves RC11's persistent preview cache and collection service foundations. The following target rows remain `PARTIAL` until real product UI and acceptance proof exists: Library tree recent-switcher presentation, Inspector workflow pickers/client resolution, Calendar ↔ Asset visual navigation, Inspiration Collection visual UI, full contextual Project/Booking/Collection actions, and the real themed screenshot harness. These are deliberately not promoted to `DONE` by persistence alone.
