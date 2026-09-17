# Creative workflow pipeline

## Ownership

`Asset Library ↔ Inspiration Board ↔ Free Canvas → future Planning Center`

The library owns asset identity and source location. An inspiration entry carries `(LibraryId, AssetId, ContentHash)`. A canvas owns only its document and object transforms. Neither board creation nor canvas editing duplicates or modifies source files.

## Data relationships

| Entity | Relationships and stored state |
| --- | --- |
| Asset | Stable library/asset IDs, source path, content hash, dimensions, library metadata |
| Inspiration board | Ordered stable asset references; optional ProjectId |
| CanvasDocument | SchemaVersion, CanvasId, name, optional ProjectId, optional PlanningSectionId, updated time, objects |
| CanvasObject | ObjectId, CanvasId, LibraryId, AssetId, source path/hash, source dimensions, X/Y/Width/Height, Rotation, FlipX/Y, CropRect, ZIndex, Locked, GroupId |
| Text object | Same geometry/history; plain Text, FontSize and TextColor instead of an asset |
| Project | One project can be referenced by many boards/canvases; no project is also valid |

`PlanningSectionId` is a nullable persistence seam only. There is no public placeholder button, separate Moodboard editor, planning page, or new booking/calendar workflow.

## Component boundaries

- `RAWSelectionAssistant.Core/Services/FreeCanvas`: immutable document records, validated reference-only storage, pure edit/history operations. No WPF bitmap pipeline and no source write API.
- `PixelTart.Modules.AssetLibrary/FreeCanvas`: WPF retained rendering, hit tests/gestures, floating tools, autosave and drawer.
- `AssetLibraryPage.Canvas`: existing workspace host, close/save protection, library/board entry, project picker, full-image window.
- `AssetLibraryViewModel.Canvas`: stable-reference resolution, deduplicated board round trip, source listings and project choices.
- Existing preview provider: online decoding, persistent high-quality previews and offline lookup. Gallery thumbnail requests stay separate from canvas high-quality requests.

Canvas source references retain library identity even when an external library is unavailable. This version cannot reconnect an arbitrary external library automatically; a missing source/cache is represented explicitly. Current-project sources are filtered through existing project-asset links.

## Transactions and history

Each document edit creates a new document snapshot. A continuous gesture batches into one undo record; group members remain separate objects. JSON saves use a unique temporary sibling and replace the canvas target only. The UI queues/debounces edits and drains changes before explicit close. A source cannot be the canvas target.

Saving images to a board reuses the existing inspiration service, deduplicates stable references, then associates existing entries with a new or chosen collection. Canvas geometry is intentionally not copied into a board: boards collect references; the canvas keeps the composition.

The future Moodboard product must reuse this document/editor core. Planning integration may reference a canvas and ProjectId/PlanningSectionId; it must not fork a second image editor.
