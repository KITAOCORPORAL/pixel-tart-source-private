# Asset Library Embedded Integration

Branch: `feature/modular-harness-v1`

Asset source: `3da45d53e4628a743a2809f908cbdfd60d706c43`

Final Harness HEAD: pending final commit.

## Result and wording boundary

Asset Library is embedded in the Modular Harness Pixel Tart Shell, but the Harness branch has not been merged into the P0 branch.

The production Asset Library domain, SQLite repository, visual analysis, query services, WPF page, and view-model behavior were adapted into the Harness branch. The standalone preview bootstrap, second shell, installer entry, and acceptance-only user entry were not imported into the embedded route.

The module is registered as `pixel-tart.asset-library` (`WorkspaceModule`, version `1.6.0-dev`) at route `asset-library`. `MainWindow` owns one `ModuleWorkspaceHost`; its route factory creates `AssetLibraryPage` in the same WPF window. No embedded code path calls `Process.Start` or starts `PixelTart_AssetLibrary_V1_6_Preview.exe`.

`asset_library_standalone_user_facing=false` and `asset_library_standalone_development_only=true`.

## Integration matrix

| Area | Code/automated state | Foreground state |
| --- | --- | --- |
| Embedded route | Complete; one `ModuleWorkspaceHost` resolves `asset-library` | Verified in the retained foreground chain |
| Navigation entry | Complete; the toolbox exposes one user-recognizable Asset Library feature and removes Photo Organize in DevPreview | Workbench -> Toolbox -> Asset Library verified |
| Same MainWindow | Structurally verified; global shell and navigation remain owners | `same_mainwindow_verified=true`; one root process at all three snapshots |
| Second GUI process | Embedded source has no standalone launch path | `asset_library_second_gui_process=false`; process counts are `1/1/1` |
| Reference import | Reference mode records metadata and leaves source bytes/path unchanged | 12 synthetic JPEGs imported; diagnostics and grid count are 12 |
| AssetGrid | Repository query, virtualized multi-column cards, lazy/cancelled thumbnails, bounded cache, selection and Inspector bindings are implemented | 12-item grid and Inspector verified |
| Visual analysis | Canonical local analysis provides palette, histogram, tone, contrast, saturation, warm/cool, clipping, provenance, cache and stale state | Palette, histogram, and tone views verified; palette correction rendered |
| Visual filter | Production visual query service and active filter chips are embedded | `asset_library_visual_filter_embedded=true`; foreground filter changes the grid |
| Visual Smart Folder | Builder persists combined tag/rating/visual rules; production SQL handles visual rules without per-item N+1 | `asset_library_visual_smart_folder_embedded=true`; save/open verified |
| Color similarity | DeltaE76 color search stays in the embedded workspace | `asset_library_color_similarity_embedded=true`; embedded result mode verified |
| Palette similarity | Uses Top-5 palette colors and weights, not dominant hue alone | `asset_library_palette_similarity_embedded=true`; embedded result mode verified |
| Visual similarity | Returns Overall, Color, Tone, Contrast, and Saturation scores with stable Top-100 ordering | `asset_library_visual_similarity_embedded=true`; foreground result mode verified |
| Global Task Center | Batch analysis runs through the shell `TaskOperationBridge`; no Asset-local second task panel is created | Same foreground trigger plus SQLite/AuditLogs persisted `Queued`/`Running`/`Completed`; `Completed` observed, `Queued`/`Running` not visually observed |
| Return to Workbench | Page and global navigation bind to the shell navigation command | Return in the same MainWindow verified |
| Module diagnostics | DevPreview-only diagnostics expose Asset, RAW, and Online descriptors with stable automation IDs | `module_diagnostics_foreground_verified=true` |

## Palette presentation correction

The canonical database palette for the partial foreground sample contained real colors and weights; the first partial palette capture showed gray swatches because an empty swatch Border had no rendered width and relied on implicit brush conversion. The embedded page now uses `HorizontalContentAlignment="Stretch"` and an explicit `HexToBrushConverter`. The WPF acceptance seam checks that the number of generated swatches matches `Analysis.Palette`, that each swatch has nonzero width, and that each resolved brush matches the corresponding `#RRGGBB` value.

This is a code/runtime-test correction reflected in the retained foreground run. The palette view is foreground verified, and the final evidence contract verifies all ten required screenshots as distinct, metadata-free PNGs with no sensitive markers.

## Foreground Task Center evidence

Batch visual analysis was triggered from the embedded Asset Library in the same foreground chain. The task is persisted in the isolated SQLite database and its `AuditLogs`/task records contain the `Queued`, `Running`, and `Completed` transitions. `Completed` was observed in the foreground at 100% with 12 successes and 0 failures. `Queued` and `Running` were not visually observed; those two transitions are verified from SQLite/AuditLogs persistence, not claimed as visible screenshots. No Asset-local second task panel or second GUI process was created.

## Data and safety boundaries

- The formal Pixel Tart product database remains SchemaVersion 5. Asset Library feature-private tables are created only in its isolated Asset database; no Schema 6 migration is registered against a P0/formal database.
- Reference import is the default and does not mutate, move, rename, delete, upload, or overwrite a source file.
- The DevPreview requires an explicit isolated acceptance root. Demo seeding and module diagnostics are enabled only by `MODULAR_HARNESS_DEV_PREVIEW`; ordinary product builds fail closed for those seams.
- Generated fixtures are synthetic JPEG files in temporary acceptance roots. Databases, logs, RAW/JPG fixtures, publish output, and process snapshots remain ignored artifacts and are not committed.
- `color_management_reference_verified=false`; the ICC adapter is not promoted to numerically verified without a trusted fixture.
- `raw_visual_proxy_verified=false`; RAW uses no invented demosaic or cloud path and remains unverified without a trusted embedded-preview fixture.
- `P0Merged=false`, `RCGenerated=false`, and `UserVerified=false`.

## Automated verification snapshot

- Harness focused: `14/14`.
- Asset focused: `24/24`.
- Visual focused: `26/26`.
- WPF embedded/evidence: `12/12` (all five embedded Asset tests and all seven evidence-contract tests pass).
- 100K production scale: `2/2`.
- Final formal total: `78/78` (`0` failed, `0` skipped); foreground, exact ten-image evidence, publish identity, and top-level `complete` are all verified.
- Product Debug and Release builds completed with exit code `0`, warnings disallowed, and `verified=true`.
- The verified publish is `%TEMP%\PixelTart_ModularHarness_V1_Acceptance\Final-20260817-173437-94ea7749a9ce4eb4b3232396da4b4a6a\formal-acceptance-complete-20260817-192824\publish\PixelTart_ModularHarness_V1_DevPreview.exe` (SHA-256 `827767075FD022DD5D89990F3C5A595A2E91173BC93B0FD4D7C922F0B4BA0FB9`); the foreground result records the same executable hash and Asset module hash `892C658628215AC78FEB07801EE08FF5A39064B7C05D97C4B8252BF090BA82D9`.
- Publish classification records `application_executable_count=1`, `published_executable_count=2`, and `unexpected_executables=[]`. `createdump.exe` has exact provenance from `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.10` (file version `10.0.1026.32716`) and `runtime_helper_provenance_verified=true`.

Formal integration acceptance is complete. Final Harness HEAD and Handoff SHA remain pending until commit; no standalone Asset Library Preview evidence is accepted as proof of this embedded integration. `P0Merged=false`, `RCGenerated=false`, and `UserVerified=false` remain explicit.
