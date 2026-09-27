# Preset Browser architecture

The core now has a provider-neutral, lazy `PresetBrowserCatalog` with search, category, provider,
favorites and recent-used queries. `PresetPreviewCoordinator` cancels stale hover requests and
returns a result only for the latest revision; preview is not a commit or undo operation.

Adobe XMP remains the verified importer subset. Capture One remains an explicit provider boundary
without a proprietary-format compatibility claim. WPF card rendering and full mouse-leave wiring
remain PARTIAL and are next UI integration work.
