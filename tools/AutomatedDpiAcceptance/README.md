# Automated Logical DPI Acceptance

This release-only tool builds the existing WPF application with the existing `UI_REVIEW_BUILD` test symbol, loads the real application shell, views, view models, resource dictionaries, dialogs and popup controls, and renders the 2.0.4 acceptance matrix at logical 125%, 150% and 200% DPI.

It is not compiled into the production installer. It does not change Windows display scaling and it must not be described as a physical monitor DPI test.

Run `Invoke-AutomatedDpiAcceptance.ps1` from PowerShell. Evidence is written to `artifacts/automated-dpi-review/2.0.4`.

`Invoke-InstalledInteraction.ps1` is disabled by default. It may run only in a dedicated isolated acceptance session with both `-IsolatedAcceptanceRun` and `PIXEL_TART_ALLOW_INSTALLED_AUTOMATION=1`; it must never attach to a user's normal installed application. Its `finally` block owns process shutdown, isolated uninstall, settings restoration and clipboard restoration.
