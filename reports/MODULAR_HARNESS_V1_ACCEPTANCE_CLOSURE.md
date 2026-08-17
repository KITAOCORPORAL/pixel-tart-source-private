# Modular Harness v1 Acceptance Closure

Branch: `feature/modular-harness-v1`

Acceptance-run HEAD: `44cf978f8fe9a62c1908dc7bd8520ced85ab2cd2`

Final Harness HEAD and commit SHA: pending final commit. This report does not predeclare the delivery SHA.

## Closure status

**Formal acceptance complete: `78/78` passed, `0` failed, `0` skipped.** The final same-run acceptance JSON completed at `2026-08-17T19:29:22.3632657+08:00` with `publish_identity_verified=true`, foreground `verified=true`, exact ten-image evidence `verified=true`, and top-level `complete=true`.

This closes the implementation and acceptance gates, but not delivery metadata: the final Harness commit SHA and Handoff SHA remain pending until commit. `P0Merged=false`, `RCGenerated=false`, and `UserVerified=false` remain explicit.

## Audited acceptance artifacts

- Acceptance JSON: `%TEMP%\PixelTart_ModularHarness_V1_Acceptance\Final-20260817-173437-94ea7749a9ce4eb4b3232396da4b4a6a\formal-acceptance-complete-20260817-192824\modular-harness-v1.acceptance.json`
- Scale metrics: `%TEMP%\PixelTart_ModularHarness_V1_Acceptance\Final-20260817-173437-94ea7749a9ce4eb4b3232396da4b4a6a\formal-acceptance-complete-20260817-192824\visual-scale-100k.metrics.json`
- Foreground result: `%TEMP%\PixelTart_ModularHarness_V1_Acceptance\Final-20260817-173437-94ea7749a9ce4eb4b3232396da4b4a6a\foreground-result.json`

## Current audited seams

- The kernel exposes module, capability, provider, route, navigation, task, settings, lifecycle, dependency, failure, and diagnostics contracts.
- Failed registration rolls back every registry contribution; initialization and deactivation failures are isolated; dependency cycles are rejected.
- The product composition root registers Asset Library, RAW Tool, and Online Selection descriptors.
- `MainWindow` owns one `ModuleWorkspaceHost` for `asset-library`; the route creates the real embedded page without `Process.Start`.
- DevPreview keeps the global shell, navigation, and Task Center, removes the duplicate Photo Organize user entry, uses an isolated acceptance root, and starts at the Workbench.
- The embedded page contains reference import, virtualized grid, Inspector, palette/histogram/tone analysis, visual filters, Smart Folder builder, color/palette/visual similarity, batch analysis bridge, module diagnostics, and return navigation.
- The palette swatch correction uses stretched swatch layout and an explicit hex-to-brush converter; the retained foreground run displays the corrected palette.

## Exact registry snapshot

| Registry | Count |
| --- | ---: |
| Modules | 3 |
| Capabilities | 14 (`core 4 + asset 8 + raw 1 + online 1`) |
| Providers | 1 |
| Routes | 2 |
| Navigation | 1 |
| Tasks | 5 |
| Settings | 4 |

## Foreground chain and process truth

The retained foreground result is `verified` and records one synthetic-only chain: Workbench -> Toolbox -> Asset Library, import 12 synthetic JPEGs, 12-item AssetGrid, Inspector palette/histogram/tone, visual filter, Smart Folder, color similarity, palette similarity, visual similarity, module diagnostics, and return to Workbench. The same root process (`21488`) is retained at all three snapshots: `gui_process_count_before_asset=1`, `gui_process_count_after_asset=1`, and `gui_process_count_after_return=1`; no child GUI process is enumerated. Therefore `same_mainwindow_verified=true`, `single_gui_process_verified=true`, and `asset_library_second_gui_process=false` for this run.

Batch visual analysis was triggered from that same foreground Asset Library. SQLite task records and `AuditLogs` persist the `Queued`, `Running`, and `Completed` transitions for the same task. `Completed` was visible in the foreground at progress 100% with 12 succeeded and 0 failed. `Queued` and `Running` were not visually observed; they are verified by the persisted SQLite AuditLogs transitions. This is composite lifecycle verification, not a claim that all three states were visible.

## Acceptance truth fields

### Verified

- `modular_harness_v1=true`
- `module_registry_complete=true`
- `capability_registry_complete=true`
- `provider_registry_complete=true`
- `module_manifest_complete=true`
- `module_route_registration_complete=true`
- `module_navigation_registration_complete=true`
- `module_failure_isolation_complete=true`
- `asset_library_module_complete=true`
- `asset_library_embedded=true`
- `asset_library_same_main_window=true`
- `asset_library_visual_analysis_embedded=true`
- `asset_library_visual_filter_embedded=true`
- `asset_library_visual_smart_folder_embedded=true`
- `asset_library_color_similarity_embedded=true`
- `asset_library_palette_similarity_embedded=true`
- `asset_library_visual_similarity_embedded=true`
- `asset_library_batch_analysis_taskcenter_embedded=true` by the composite foreground trigger plus persisted SQLite AuditLogs evidence described above
- `raw_tool_module_registered=true`
- `online_selection_contract_registered=true`
- `visual_query_100k_test=true`
- `saved_smart_folder_100k_test=true`
- `similarity_100k_candidate_test=true`
- `synthetic_jpeg_fixture_generator_verified=true`
- `harness_focused_verified=true`
- `asset_focused_verified=true`
- `visual_focused_verified=true`
- `wpf_embedded_verified=true`
- `wpf_foreground_evidence_verified=true`
- `same_mainwindow_verified=true`
- `single_gui_process_verified=true`
- `foreground_synthetic_chain_verified=true`
- `module_diagnostics_foreground_verified=true`
- `return_workbench_verified=true`
- `product_debug_build_verified=true`
- `product_release_build_verified=true`
- `exact_devpreview_publish_verified=true`
- `publish_identity_verified=true`
- `complete=true`

### Intentionally false or pending delivery metadata

- Final Harness commit SHA and Handoff SHA are pending until commit.
- `color_management_reference_verified=false` because no trusted ICC numerical fixture is available.
- `raw_visual_proxy_verified=false` because no trusted RAW embedded-preview fixture is available.
- `asset_library_standalone_user_facing=false` and `asset_library_standalone_development_only=true`.
- `ExternalPluginRuntime=false`, `P0Merged=false`, `RCGenerated=false`, and `UserVerified=false`.

## Formal suite matrix

| Suite | Total | Passed | Failed | Verified |
| --- | ---: | ---: | ---: | --- |
| `harness-focused` | 14 | 14 | 0 | true |
| `asset-focused` | 24 | 24 | 0 | true |
| `visual-focused` | 26 | 26 | 0 | true |
| `wpf-embedded` | 12 | 12 | 0 | true |
| `visual-scale-100k` | 2 | 2 | 0 | true |
| **Current unique total** | **78** | **78** | **0** | **true** |

The WPF filter is five Embedded Asset tests plus seven Modular Harness evidence-contract tests. All twelve pass.

## Builds and publish identity

Product Debug and Release builds each completed with exit code `0`, warnings disallowed, and `verified=true`. The verified self-contained publish is:

`%TEMP%\PixelTart_ModularHarness_V1_Acceptance\Final-20260817-173437-94ea7749a9ce4eb4b3232396da4b4a6a\formal-acceptance-complete-20260817-192824\publish\PixelTart_ModularHarness_V1_DevPreview.exe`

The application SHA-256 is `827767075FD022DD5D89990F3C5A595A2E91173BC93B0FD4D7C922F0B4BA0FB9`, matching the foreground result; the Asset module SHA-256 is `892C658628215AC78FEB07801EE08FF5A39064B7C05D97C4B8252BF090BA82D9`.

Publish classification records `application_executable_count=1` (`executable_count=1` in the acceptance JSON), `published_executable_count=2`, and `unexpected_executables=[]`. The second `.exe`, `createdump.exe`, is a runtime helper with exact `.deps.json` provenance from `runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.10`, file version `10.0.1026.32716`; `runtime_helper_provenance_verified=true`.

## Latest 100K production query and similarity run

The final scale metrics use the production repository and SQLite query service against 100,000 synthetic assets and visual-feature rows, 100,000 palette rows, and 10,000 tagged rows. Candidate pool limit is 5,000 and result limit/top K is 100.

| Metric | Final same-run result |
| --- | ---: |
| Seed 100,000 assets/features/palette rows and 10,000 tag memberships | 14,706.9047 ms |
| Tone `Low` | 33,334 total / 100 returned / 481.2145 ms |
| Hue `30..60` | 8,618 total / 100 returned / 93.8299 ms |
| Saturation `High` | 33,333 total / 100 returned / 414.2106 ms |
| Contrast `Medium` | 33,333 total / 100 returned / 417.9619 ms |
| Tag + Visual | 3,334 total / 100 returned / 418.2820 ms |
| Saved Smart Folder | 5 rules / 286 total / 100 returned / 470.8595 ms |
| Similarity cold | 4,786 candidates / 2,905.7369 ms pruning / 20.4865 ms exact / 2,944.3309 ms service / 2,946.7210 ms wall / 100 returned |
| Similarity warm | 4,786 candidates / 2,892.4671 ms pruning / 11.1491 ms exact / 2,904.1184 ms service / 2,904.1805 ms wall / 100 returned |

Similarity made 2 reference feature-store calls, created 0 pairwise-cache tables, and returned 100 rows in both cold and warm modes. `visual_query_100k_verified=true` and `similarity_100k_candidate_verified=true`.

## Evidence closure

The required scenes `01_workbench.png` through `10_module_diagnostics.png` are all present and unique: `required_count=10`, `present_count=10`, `unique_count=10`, `missing_files=[]`, `invalid_files=[]`, `unexpected_files=[]`, and `capture_status=captured`. Each file is a valid metadata-free PNG with no sensitive marker. The recaptured `08_visual_similarity.png` SHA-256 is `2AC48FBF2F5BE888959FA46A744101BAEC86ED97950A98B002CD68A7885AA3D3`; evidence `verified=true`.

Formal acceptance is complete. The only remaining report placeholders are the final Harness and Handoff commit SHAs. No installer, RC, P0 merge, user verification, or external plugin runtime is produced by this closure.
