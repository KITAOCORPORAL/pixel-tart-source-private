# Modular Harness v1 Progress

Branch: `feature/modular-harness-v1`

Working-tree base: `44cf978f8fe9a62c1908dc7bd8520ced85ab2cd2`

Final Harness HEAD: pending final commit. This report does not predeclare the delivery SHA.

Source baselines: P0 `4dac5f8e4460b7a67309646b6133bd186c121fea`; Asset Library `3da45d53e4628a743a2809f908cbdfd60d706c43`; Online Selection `e30eac4762af7eff837645a8303c47eeb95c5fe2`.

## Branch boundary

Asset Library is embedded in the Modular Harness Pixel Tart Shell, but the Harness branch has not been merged into the P0 branch. This is a DevPreview integration branch only: `P0Merged=false`, `RCGenerated=false`, `UserVerified=false`, and `ExternalPluginRuntime=false`.

No formal installer or RC is produced by this work. The standalone Asset Library preview remains development-only and is not a user-facing entry in the Modular Harness shell.

## Kernel and registries

The existing Harness v1 kernel now has concrete module, capability, provider, route, navigation, task, settings, lifecycle, dependency, failure, and diagnostics contracts. Registration is transactional across registries: a registration failure removes the partial module contributions. Initialization and deactivation failures are isolated, and dependency cycles are rejected.

Current exact registry contract:

| Registry | Count | Detail |
| --- | ---: | --- |
| Modules | 3 | Asset Library, RAW Tool, Online Selection |
| Capabilities | 14 | Core 4 + Asset 8 + RAW 1 + Online 1 |
| Providers | 1 | `visual-analysis.local-pixel` |
| Routes | 2 | Asset Library and Online Selection descriptor route |
| Navigation | 1 | User-facing Asset Library toolbox entry |
| Tasks | 5 | Shared task registry total |
| Settings | 4 | Shared settings registry total |

`ModuleRegistry=true`, `CapabilityRegistry=true`, `ProviderRegistry=true`, `ModuleManifest=true`, `ModuleRouteRegistration=true`, `ModuleNavigationRegistration=true`, `ModuleFailureIsolation=true`, and `DependencyCycleProtection=true`.

## Modules

- Asset Library is `pixel-tart.asset-library`, `WorkspaceModule`, version `1.6.0-dev`, route `asset-library`, with exactly eight public capabilities: `asset.query`, `asset.pick`, `asset.import`, `asset.folder`, `asset.tag`, `asset.smart-folder`, `asset.visual-analysis`, and `asset.visual-search`.
- RAW Tool is registered as the Harness RAW module descriptor with one public RAW capability.
- Online Selection registers the `selection.create-from-assets` contract capability. No production cloud behavior is invented.
- The shared Asset Selection contract remains the boundary between Asset Library and Online Selection.

## Embedded Asset Library

`MainWindow` owns one `ModuleWorkspaceHost` for `asset-library`; the route factory creates the real `AssetLibraryPage` in that host. The DevPreview keeps global navigation and the global Task Center visible, removes the duplicate user-facing Photo Organize entry, and starts an isolated profile at the Workbench.

The embedded page includes reference import, virtualized thumbnail grid, Inspector, palette/histogram/tone analysis, visual filters, Smart Folder builder, color/palette/visual similarity, batch analysis through `TaskOperationBridge`, module diagnostics, and return-to-Workbench navigation.

The retained foreground result is `verified`. It records the Workbench -> Toolbox -> Asset Library route in one `MainWindow`, 12 unique synthetic JPEG reference imports, a 12-item `AssetGrid`, Inspector palette/histogram/tone views, a grid-changing visual filter, a persisted visual Smart Folder, color/palette/visual similarity result modes, module diagnostics, and return to Workbench. The palette presentation correction is present in the captured run.

The same root process (`21488`) was retained before opening Asset Library, after opening it, and after returning to Workbench. Each snapshot records one GUI process, no descendants, and the same executable: `gui_process_count_before_asset=1`, `gui_process_count_after_asset=1`, and `gui_process_count_after_return=1`. Therefore `same_mainwindow_verified=true`, `single_gui_process_verified=true`, and `asset_library_second_gui_process=false` for this foreground run.

Batch visual analysis was triggered in that same foreground chain. SQLite `TaskOperations`/`AuditLogs` evidence persists the `Queued`, `Running`, and `Completed` transitions for the same task, and the final `Completed` state was visually observed at 100% (`12` succeeded, `0` failed). `Queued` and `Running` were **not** visually observed; their verification comes from the persisted SQLite/AuditLogs transitions, not from foreground screenshots.

## Current automated verification

These are current focused-suite counts, not historic totals:

| Suite | Result | Scope |
| --- | ---: | --- |
| Modular Harness focused | 14/14 | Exact manifests and registries, dependency ordering/cycles, rollback, lifecycle isolation, routes/providers/diagnostics |
| Asset focused | 24/24 | Asset V1/V1.5 repositories, import safety, paging, folders/tags/undo, Hue query-plan regression |
| Visual focused | 26/26 | Visual engine, V1.6 canonical features, persistence, filters, similarity, batching |
| WPF embedded/evidence | 12/12 | All 5 Embedded Asset WPF tests and all 7 evidence-contract tests pass |
| 100K production scale | 2/2 | Distributed visual queries, saved Smart Folder, cold/warm similarity diagnostics |

The final same-run formal runner result is `78/78` (`0` failed, `0` skipped). All five suites are verified, all ten required evidence PNGs are present and unique, metadata/marker checks pass, foreground is verified, publish identity is verified, and top-level `complete=true`. The final unique `08_visual_similarity.png` SHA-256 is `2AC48FBF2F5BE888959FA46A744101BAEC86ED97950A98B002CD68A7885AA3D3`.

Product Debug and Release builds both completed with exit code `0`, warnings disallowed, and `verified=true`. The verified formal publish is `%TEMP%\PixelTart_ModularHarness_V1_Acceptance\Final-20260817-173437-94ea7749a9ce4eb4b3232396da4b4a6a\formal-acceptance-complete-20260817-192824\publish\PixelTart_ModularHarness_V1_DevPreview.exe`; its SHA-256 is `827767075FD022DD5D89990F3C5A595A2E91173BC93B0FD4D7C922F0B4BA0FB9`, matching the foreground result. The Asset module SHA-256 is `892C658628215AC78FEB07801EE08FF5A39064B7C05D97C4B8252BF090BA82D9`.

Publish classification records `application_executable_count=1`, `published_executable_count=2`, and zero unexpected executables. The second `.exe`, `createdump.exe`, is not an application entry point: its exact provenance is `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.10` in the publish `.deps.json` (file version `10.0.1026.32716`), and `runtime_helper_provenance_verified=true`.

## Latest 100K production evidence

The production repository and `SqliteVisualAssetQueryService` were run against 100,000 synthetic asset/feature rows and 10,000 tag memberships. The latest retained measurement is:

| Path | Result |
| --- | ---: |
| Seed 100,000 rows | 14,706.9047 ms |
| Tone `Low` | 33,334 total / 100 returned / 481.2145 ms |
| Hue `30..60` | 8,618 total / 100 returned / 93.8299 ms |
| Saturation `High` | 33,333 total / 100 returned / 414.2106 ms |
| Contrast `Medium` | 33,333 total / 100 returned / 417.9619 ms |
| Tag + Visual | 3,334 total / 100 returned / 418.2820 ms |
| Saved Smart Folder | 5 rules / 286 total / 100 returned / 470.8595 ms |
| Similarity cold | 4,786 candidates / 2,905.7369 ms pruning / 20.4865 ms exact / 2,944.3309 ms service / 2,946.7210 ms wall / 100 returned |
| Similarity warm | 4,786 candidates / 2,892.4671 ms pruning / 11.1491 ms exact / 2,904.1184 ms service / 2,904.1805 ms wall / 100 returned |

Top K is 100, the production candidate cap is 5,000, the reference feature store was called twice across cold and warm runs, and no pairwise cache table was created. No fragile machine-specific duration threshold is used.

## Closure and intentionally false

- Formal acceptance is complete: `current_run_total_passed=78`, `current_run_total=78`, `wpf_foreground_evidence_verified=true`, `publish_identity_verified=true`, and top-level `complete=true`.
- Final Harness commit SHA and Handoff commit SHA remain pending; this report does not predeclare either commit.
- `color_management_reference_verified=false` because no trusted ICC numerical fixture is available.
- `raw_visual_proxy_verified=false` because no trusted RAW embedded-preview fixture is available.
- `P0Merged=false`, `RCGenerated=false`, `UserVerified=false`, and `ExternalPluginRuntime=false` remain mandatory.
