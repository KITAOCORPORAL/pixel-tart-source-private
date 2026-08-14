# Asset Library Embedded Integration

## Result

The Asset Library is registered as `pixel-tart.asset-library` (`WorkspaceModule`, version `1.6.0-dev`) and exposed at `asset-library`. `MainWindow` contains a `ModuleWorkspaceHost` for that route; the host resolves the module route and creates the embedded `AssetLibraryPage` in the same WPF window.

The shell does not start `PixelTart_AssetLibrary_V1_6_Preview.exe`, does not create a second GUI process, and does not add an installer entry for the preview. The preview remains development acceptance infrastructure only.

## Boundaries

The kernel consumes only module contracts and capability/provider descriptors. Asset-specific selection access uses the stable `IAssetSelectionSource` contract. Visual analysis is represented by the local provider capability and embedded page contract; the unfinished visual filter, visual smart-folder, palette/similarity UI, batch task center, ICC reference, RAW proxy, and full UI evidence gates remain false/deferred.

The formal product database schema remains version 5. No P0 interaction files, installer definitions, or Online Selection implementation were merged into this branch.

## Verification

The harness tests verify module lifecycle, duplicate rejection, route creation, navigation visibility, module types, capabilities, and provider registration. Product Debug and Release builds are the relevant integration checks; no standalone preview process is used by the embedded path.
