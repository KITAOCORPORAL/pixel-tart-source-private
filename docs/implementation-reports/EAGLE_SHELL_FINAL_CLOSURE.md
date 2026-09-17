# Eagle Shell final closure

2026-09-17 · integration/pixel-tart-developer-preview · 2.3.0-RC12

## Changes

- Color filter: 360 × 440 maximum overlay instead of contributing to the toolbar Auto row. Smaller padding/plane, readable seven-color row, visible hue legend/hex/range. Cursor follows hex edits.
- Nested menus: explicitly use the canonical product item template, including arrows, opened highlight, dark border and padding. Evidence captures the actual open popup child alongside the actual parent menu; submenu position follows its parent row.
- Quick preview: centered shared high-quality provider preview; deferred gallery-leave handling permits transitions into the popup; popup leave closes it. The lower-right entry uses the existing vector search icon (the font glyph was missing on this machine). Keyboard focus also exposes the entry.
- Screenshot preparation now waits for real floating surfaces immediately before rendering. Missing submenu/preview fails capture. Added leave/closed evidence. Full manifest inventory is now 72, not 71.

## Verification

- Release x64 UiReview build: zero warnings/errors.
- Targeted WPF run: 16 passed, zero failed/skipped, including real HWND-backed high-quality preview and leave-close behavior.
- The intermittent test was `RealSplitterDragKeepsSideWidthBindingsAndCollapseRestoresTheDraggedWidths`. Its page was attached before asynchronous initialization completed, allowing Loaded to disable the slider while keyboard input was asserted. Initialize before attachment and assert readiness/enabled state; the original one-second deadline is unchanged.
- Twenty independent repeated processes passed 20/20. TRX: `artifacts/creative-workflow-phase0/timeout-repeat/`.
- Diagnostic product screenshots: `artifacts/creative-workflow-phase0/verified/` (idle/active/closed), `verified-filter/` (matching color), `final-submenu/` (parent and child). Each capture uses the real MainWindow, App resources and isolated library. Synthetic source hashes unchanged.

These are automated logical-resolution renderings, not a claim of physical monitor/DPI or photographer acceptance. Diagnostic renders precede this commit; final workflow evidence must be regenerated against the eventual committed HEAD. No RC13 or main merge.
