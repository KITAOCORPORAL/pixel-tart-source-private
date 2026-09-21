# Page Header and Close Safe Area

Purpose: prevent page actions, menus and labels from intersecting the shell-owned close target. A closable route receives a structural right column; individual pages must not compensate with negative margins.

Metrics: `ShellSurfaceCloseTargetSize` is 40 DIP, `ShellSurfaceCloseGap` is 8 DIP and `ShellSurfaceCloseReservedWidth` is 56 DIP. The icon remains 16 DIP. The safe-area column is zero only for non-closable routes or when a modal owns close authority.

Responsive: content reflows inside its own column at 1180–2560 widths and 100–200% layout scale. High-frequency primary actions remain textual when space permits; secondary actions may use Studio icon buttons with Chinese tooltip or overflow. They never enter the close column.

Keyboard/accessibility: close retains AutomationId, AutomationName, HelpText, tooltip, 40 DIP hit target, focus ring and Esc behavior. Modals and drawers use `Title | Actions | Close` columns and hide the Shell close while active.

Do: reserve layout space at the shell, measure runtime rectangles, preserve one close authority. Don't: float a high-Z close over route content, shrink its hit target, globally reduce text, or patch every page with a right margin.

Implementation: `MainWindow.xaml`, `SurfaceCloseButton.xaml`, `GlobalCloseSafeAreaTests`.
