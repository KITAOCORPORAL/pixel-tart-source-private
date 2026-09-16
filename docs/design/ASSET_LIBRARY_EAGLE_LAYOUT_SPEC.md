# Asset Library Eagle Layout Specification

## Status

- Product: Pixel Tart 2.3.0-RC12
- Scope: Asset Library shell and interaction reconstruction
- Reference: `Pixel_Tart_Asset_Library_Eagle_Layout_Reconstruction_v2.md`
- Completion: implemented and verified

This change keeps the existing database, asset-origin model, thumbnail provider, and project/booking relationships intact. It changes the Asset Library presentation shell and the interaction routes that expose existing product capabilities.

## Implemented shell

The page now uses one continuous workspace:

1. Icon-first toolbar
2. Eagle-style organization sidebar
3. image-first gallery
4. contextual photo information pane

The default gallery mode is Masonry. Grid, Justified, and List remain available through the layout icon because they are existing supported views, not new pages.

## Toolbar

The primary row contains search plus icon actions for color, tags, rating, date, sort, layout, import, undo, redo, and more. Every icon action has a tooltip and stable automation identity. Low-frequency organization and inspector toggles remain available to automation and narrow-layout recovery without consuming primary-row space.

Filtering opens as a popover over the workspace. The existing real-time color plane, Hex input, hue control, and range adjustment remain connected to the query composer. Hardcoded foreground colors in this surface were replaced with design-system tokens.

## Sidebar

The system collection order is:

- 全部素材
- 未分类
- 未标签
- 最近添加
- 收藏
- 缺失文件
- 已归档
- 回收站

Folders, smart folders, and tag groups retain their existing tree models, drag targets, and context actions. The active system collection now has an explicit token-based highlight and icon accent.

## Gallery

Masonry is the default workspace view. Photo cards preserve their source aspect ratio with `Stretch="Uniform"`; no image stage uses a black fill. Primary card metadata is limited to filename and resolution. The existing virtualized layout engine remains responsible for large collections.

Single selection records an immediate request to open the photo information pane. The request is retained even if the control is still in its zero-width initialization pass; responsive visibility is applied once the host has enough width. Double-click continues to open the high-resolution viewer.

## Photo information pane

The single-photo state contains:

- high-resolution preview
- filename
- processing status and project/booking relations
- five-star rating
- color palette labels
- comment
- source path with copy action
- tags and folders
- basic file and capture information
- image export action

The analysis tabs use a local dark-token template so the selected tab cannot fall back to a bright system surface.

## Quick Preview

The card loupe and the context-menu quick preview use the same preview provider. The popup is centered, sized to approximately 50% of page width and 55% of page height within bounded limits, and closes on pointer leave. The displayed bitmap comes from the preview provider rather than scaling the visible thumbnail control.

## Context menu

The product-facing groups are:

- 查看
- 整理
- 项目
- 灵感
- 导出
- 管理

Actions use dedicated vector icons and user language such as “打开文件位置”, “处理状态”, “导出图片”, and “复制路径”. Destructive source-file deletion is not exposed; existing safe archive and recycle-bin behavior is preserved.

## Creative Board

The existing temporary tray and saved inspiration collections are presented through a unified `Creative Board` surface. It remains an overlay in the Asset Library rather than a new page. Collection thumbnails now preserve aspect ratio. No Moodboard, planning page, AI feature, or browser clipper was introduced.

## Verification

- x64 Debug and Release UI Review builds: 0 warnings, 0 errors
- Eagle layout and related contract tests: passed
- isolated Inspector, splitter, keyboard, and export tests: passed
- final visual evidence: 12 product screenshots, 10 UX screenshots, 10 Asset Library closure screenshots, 32 DPI captures, 6 resolution captures, and 1 aspect-ratio capture
- screenshot lifecycle isolation: passed; unique process per capture: passed
- synthetic source images unchanged after capture: passed

Final evidence root:

`artifacts/eagle-layout-final-v2/`

Key screenshots:

- `asset-library-ux-closure/01_clean.png`
- `asset-library-ux-closure/02_context_menu.png`
- `asset-library-ux-closure/05_filter_color.png`
- `asset-library-ux-closure/06_inspector_rating.png`
- `asset-library-ux-closure/07_inspiration_board.png`
- `asset-library-ux-closure/09_loupe_active.png`

## Next acceptance stage

This reconstruction is ready for physical-machine verification at 1920×1080, 2560×1440, and 4K across 100%, 125%, 150%, and 200% DPI. Automated logical-DPI evidence is not a substitute for the remaining human physical-machine pass.
