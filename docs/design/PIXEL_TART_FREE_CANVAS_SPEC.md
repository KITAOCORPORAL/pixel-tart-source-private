# Pixel Tart Free Canvas v1

2026-09-17 · 2.3.0-RC12 · Creative Workflow Foundation

## Scope

A quiet, non-destructive visual thinking desk, hosted inside the existing Asset Library workspace. Images remain the visual focus. No default grid, separate Moodboard editor, Planning Center UI, AI, clipper, brush, mask, or layer effects.

## Entry and navigation

- Select library assets → 快速工具 → 自由画布. Context menu: 用于创作 → 放到自由画布.
- Select inspiration-board cards → 在自由画布中打开.
- The initial grid preserves current library order and 24 logical units between objects.
- 更多 → 打开已保存画布 reopens documents without creating another canvas first. The canvas header also has 打开画布.
- A canvas can have no project or one project; a project can contain multiple canvases.

## Interaction

| Action | Interaction |
| --- | --- |
| Selection | Click; Shift-click; drag marquee; Ctrl+A; group members select together |
| Move | Drag; arrows = 1 unit; Shift+arrows = 10 units |
| Resize | Four corner handles; proportional; multi-selection uses a common anchor |
| Rotate | Rotation handle; Shift snaps the drag to 15°; menu ±90°, 180°, 15° |
| Crop | Double-click or 裁切; drag region; free, original, 1:1, 4:5, 3:2, 16:9; Enter apply, Esc cancel |
| Mirror | Horizontal/vertical transforms |
| Duplicate | Ctrl+C/Ctrl+V or Ctrl+D; offset references, not files |
| Remove | Delete/Backspace or 移出画布; undoable; never removes the asset |
| Group | Ctrl+G; Ctrl+Shift+G to ungroup |
| Order/lock | Top/bottom; lock/unlock; locked items remain selectable |
| History | Ctrl+Z; Ctrl+Shift+Z or Ctrl+Y; a drag is one history entry |
| View | Wheel zoom; Space-drag pan; Fit All, 100%, Focus Selection |
| Text | Text tool then click; edit plain content, size, color; double-click to edit |
| Arrange | Horizontal, grid, compact; operates on selection, or all if none; undoable |

The small floating toolbar follows the selection. Multi-selection exposes grouping, alignment, order, lock and removal. Image menus include full preview, reveal in library and inspiration-board actions.

## Material drawer

Collapsible drawer: library, inspiration board, temporary collection, current project and recently used. Search and proportional thumbnail rows; drag or add selected rows. In the board source, the active board is used when populated; otherwise available boards are combined and deduplicated.

## Persistence and safety

- Reference-only JSON under the current library database directory's `canvases` folder.
- 650 ms debounced autosave, serialized flush, atomic temporary-file replacement.
- Explicit close waits for persistence. A failure leaves the canvas open with a visible explanation.
- Source-path collisions are rejected. Transforms never encode or overwrite source images.
- Shared `IAssetPreviewProvider`, Canvas purpose, high quality. Requests depend on display width, zoom and crop. Cached previews survive source unavailability; offline objects retain their geometry and badge.
- Preview cache is bounded to 128 objects and approximately 192 MiB of decoded surface pixels. A source with no existing high-quality cache shows a placeholder, not an enlarged thumbnail.

## Acceptance boundary

Automated tests and real-App logical screenshots establish implementation behavior. They do not establish photographer usability, all monitor/DPI combinations, 10K real-photo performance or a formal RC release. See the implementation report for measured results and remaining manual checks.
