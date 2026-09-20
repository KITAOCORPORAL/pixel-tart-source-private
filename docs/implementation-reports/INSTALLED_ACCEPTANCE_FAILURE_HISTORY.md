# Installed acceptance failure history

Product baseline: `8cb95e6d2c4814717dc31a8c6aa19f8b2c89c9cc`. Overall status: **BLOCKED**, not a product crash or completed acceptance.

| Run | Cause | Regression requirement |
| --- | --- | --- |
| v1 / 20260920-135509-417cc2 | `date-value`: DatePicker and its inner Edit shared the name 拍摄日期; untyped lookup was ambiguous. | Real DatePicker peer, inner PART_TextBox/Edit/Value and calendar keyboard behavior. |
| v2 / 20260920-141549-24c662 | `planning-enabled`: SidebarRoot is a sibling landmark, not the navigation ancestor. | Real SidebarNavigationScroll/Pane contains all primary navigation buttons. |
| v3 / 20260920-143726-3bddf3 | `reference-loaded`: named, focusable StackPanel lacks a peer; assumed Custom reference container does not exist. | Full image family: exposed actionable peer, focus, Enter, Esc, Shift+F10, menu items, focus restoration. |

`tools/PixelTart.InstalledAcceptance/Preserve-History.ps1` copies available evidence without modifying originals, verifies SHA256, and writes manifests under `artifacts/installed-acceptance-history/v1`, `v2`, `v3`. The original v1 run is no longer present at its supplied location; its folder records SOURCE_NOT_PRESENT rather than inventing an archive. v2 and v3 logs, trees, diagnostics, screenshots and results are retained. These artifacts are local and ignored by Git; this report is tracked.

v3 has 113 result records: 75 PASS, 36 UNIQUE_1 checkpoint records, one expected optional skip and one BLOCKED record. This is **not 113/124 formal steps**. Navigation, date input, create, body editing, save/restart/persistence, seven modules and preview/Esc passed. Import dialog completed and the title/image appeared, but the interactive reference target was absent. Quick Preview, context menu, booking, PDF, tether, online selection, final close and upgrade were not completed. ExitResult PASS refers to the intermediate restart.

Ten screenshots exist, but screenshot production is not visual approval. Physical DPI and user visual acceptance remain unverified.

No further user debugging package is authorized. A final Candidate requires all internal contracts and gates; existing v1–v3 deliveries are historical only.
