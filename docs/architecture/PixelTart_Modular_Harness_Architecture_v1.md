# Pixel Tart Modular Harness Architecture v1

## Scope

This branch adds a small in-process module harness to the Pixel Tart shell. The kernel owns contracts, registries, lifecycle ordering, capability checks, and failure isolation; it does not know Asset Library feature types or Online Selection implementation details.

## Built-in modules

`pixel-tart.asset-library` is a `WorkspaceModule` at route `asset-library`. Its route is hosted by `ModuleWorkspaceHost` inside the existing `MainWindow`, so the production launch remains the single `KitaoPhotoSelector.exe` process.

`pixel-tart.raw-to-jpeg` is a `ToolModule` that contributes the `raw.decode` capability and a shared task descriptor.

`pixel-tart.online-selection` is a contract-only descriptor route. It is registered for discovery but is not added to the navigation registry; the production shell remains provider-neutral.

## Registries and lifecycle

Module IDs, routes, navigation routes, capabilities, providers, task types, and settings keys reject duplicates. Initialization follows declared module dependencies and detects missing dependencies/cycles. A module initialization or activation failure is recorded in diagnostics without preventing independent modules from loading.

## Isolation

The Asset Library route is an embedded WPF view. It does not launch the standalone development preview, start a second GUI process, alter the installer, or register a new product database migration. Feature-private Asset Library storage and unfinished visual features remain governed by the Asset Library branch’s existing deferred flags.

The source contract `RAWSelectionAssistant.Core.Services.AssetSelection.AssetSelectionContracts` is kept byte-identical with the Asset and Online feature branches so future integration can use a stable adapter without merging either branch wholesale.
