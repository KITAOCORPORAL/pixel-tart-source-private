# Asset Library Context Menu — Eagle Review

## Scope

This review applies only to the RC12 Asset Library context menu. It uses the local Eagle interaction evidence in `docs/product/eagle-reference/` and preserves Pixel Tart's existing WPF design system, repository services, and non-destructive file semantics.

## What the reference establishes

- A right-click menu is a short path to a user task, not a mirror of the application's internal modules.
- Frequent viewing and organizing actions appear before workflow, creative, export, and lifecycle actions.
- A second level is used when the first level names a choice set: destination folder, tag, rating, workflow state, inspiration destination, or export form.
- Opening a submenu must retain a visible active row. A small edge-facing arrow communicates the next level without becoming the strongest visual element.
- Destructive-looking management actions are isolated at the bottom, and permanent deletion is never implied when source files remain untouched.

## Pixel Tart decisions

### Information order

1. **查看** — 查看大图、默认程序打开、在资源管理器中显示、复制文件。
2. **整理** — 移到文件夹、添加标签、颜色标记相关动作、评分。
3. **摄影工作流** — 关联项目、关联拍摄、工作流状态。
4. **创作与导出** — 加入灵感板、导出。
5. **管理** — 从当前位置移除、归档、恢复、移到回收站。

Section labels are quiet, non-interactive wayfinding. Actual actions stay directly scannable. Choice sets keep their submenu.

### Dark-theme contrast

- Popup: `#222830`; border: `#3A424C`.
- Hover: `#303843`; open submenu: `#36414C`.
- Primary text remains `#F2F4F6`; shortcuts and arrows use `#AAB2BC`.
- Separator: `#46505C`.

This keeps the dark Pixel Tart tone while making hover and open states lighter than the popup rather than disappearing into it.

### Asset Action icon family

`AssetActionIcons.xaml` defines a dedicated 20 × 20, 1.5 px outline family. High-frequency actions receive icons; section labels and low-frequency recovery variants remain text-first. The shared menu template now reserves one consistent icon column, so icons, labels, shortcuts, and submenu arrows do not drift between levels.

### Language and safety

- “照片查看器” becomes “查看大图”.
- “默认应用” becomes “默认程序打开”.
- “灵感托盘 / 灵感集” is presented as “灵感板 / 临时收集”.
- “复制文件” puts the existing source file on the Windows clipboard and does not write, move, or delete it.
- No “永久删除” action is exposed. Moving to the application recycle bin continues to preserve the source file.

## Acceptance evidence

`AssetContextMenuVisualTests` locks the group order, action order, dedicated icons, submenu presence, dark interaction tokens, and the absence of the retired viewer/permanent-delete language. Existing end-to-end context tests continue to cover multi-selection, rating, workflow, archive, recycle-bin restore, restart persistence, and source-file safety.
