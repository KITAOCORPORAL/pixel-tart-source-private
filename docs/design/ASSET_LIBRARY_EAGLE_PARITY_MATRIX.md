# Asset Library Eagle parity matrix

Weekend Sprint start: `e496b6b557adf9e378e0d11de3a3d50a70950f20` on `integration/pixel-tart-developer-preview`.
Eagle behavior is based on `docs/product/eagle-reference/`. A row is `DONE` only when UI, command, persistence, reload behavior and an automated test all exist.

## RC9 truth audit

| Capability | UI | Command | Persistence | Reload test | Status / evidence |
|---|---:|---:|---:|---:|---|
| Library tree | Yes | Yes | Yes | Partial | PARTIAL — portable-library switching is covered; recent-library tree presentation is incomplete. |
| Folder | Yes | Yes | Yes | Yes | DONE — hierarchy/create/rename/move/reorder/archive, multi-drop membership and undo/redo are covered by `AssetLibraryV15Tests`, `AssetLibraryP2CoreTests` and P2/P3 WPF acceptance. |
| Smart Folder | Yes | Yes | Yes | Yes | DONE — one canonical Query AST is stored in `SmartFolderQueryDocuments`; save/copy/edit/archive/reopen and invalid-reference behavior are covered by P3 repository/integrity/WPF tests. |
| Tag | Yes | Yes | Yes | Yes | DONE — groups, create/rename/move/reorder/archive/merge, batch membership and drag targets are covered by P3 repository/integrity/WPF tests. |
| Filter | Yes | Yes | Yes | Yes | PARTIAL — query AST, chips and reload exist; the surface is still an expanding toolbar panel rather than an anchored popover. |
| Search | Yes | Yes | Yes | Yes | DONE — name/file/tag/comment search, debounce, IME composition, clear/history and selection restoration are tested. Project/client search remains unavailable because those relations are not yet stored. |
| Density | Yes | Yes | Yes | Yes | DONE — thumbnail continuum and viewport anchor are covered by WPF layout tests. |
| Layout | Yes | Yes | Yes | Yes | DONE — Grid/Masonry/Justified/List and formal viewport behavior are tested. |
| Inspector core metadata | Yes | Read-only | Yes | Yes | DONE — file, dimensions, dates, rating, folders and tags read real repository data. |
| Inspector workflow fields | Yes | No | No | No | PARTIAL — `InspectorAssetOrigin`, camera/lens/exposure, workflow/project/booking/client added by `e496b6b` are display properties only. Static `未指定/未记录/未关联/未处理` values are not completion evidence. |
| Context menu | Yes | Partial | Partial | Partial | PARTIAL — selection-aware organize/rating/tray/archive commands are real; Viewer, external open/reveal, workflow links, export and Trash are not all wired. |
| Preview / Viewer | Thumbnail only | No | N/A | No | PARTIAL — Inspector thumbnail is not a dedicated Viewer. |
| Import | Yes | Yes | Yes | Yes | DONE — picker and Explorer drop both create source-safe reference records; managed-copy remains explicit. |
| Export | Disabled menu | Partial services | Partial | Partial | PARTIAL — package/report plumbing exists but the Asset Library selection menu is not a usable export workflow. |
| Archive | Yes | Yes | Yes | Yes | PARTIAL — repository and archived collection/undo are real; restore is not exposed symmetrically in every context. |
| Trash | Disabled collection | No | No | No | PARTIAL — lifecycle behavior is documented, but no durable Trashed state exists. |
| Multi-select | Yes | Yes | Yes | Yes | DONE — extended/marquee/context selection survives paging and query refresh. |
| Drag & drop | Yes | Yes | Yes | Yes | DONE — gallery single/multi selection to Folder/Tag changes metadata membership only; Explorer drop imports references without moving source files. |
| Inspiration Tray | Yes | Yes | Yes | Yes | PARTIAL — durable reference service and commands exist; the current visual/persistence/relation experience still needs completion. |
| Project / Booking / Client | Placeholder | No | No | No | NOT DONE — must use dedicated relationships, never ordinary tags. |

Permanent Delete is `NOT PLANNED / DEFERRED` for this sprint. Reference assets never authorize deletion or mutation of source files.

## RC10 closure target

The Weekend Sprint report records the final before/after status and points to concrete persistence and test evidence for each promoted row. Rows without all five proof layers remain `PARTIAL` with an explicit reason.
