# Modular Harness v1 Acceptance Tooling

This directory owns the acceptance boundary for the full Pixel Tart Modular Harness DevPreview. It does not launch or validate the standalone Asset Library Preview.

## Synthetic foreground fixture

Generate a new synthetic-only fixture under the Windows temporary root:

```powershell
.\tools\ModularHarnessV1Acceptance\New-ModularHarnessSyntheticFixture.ps1
```

The generator refuses existing or non-temporary output paths. It creates 12 programmatic JPEG files without EXIF and writes relative paths plus SHA-256 values to `fixture-manifest.json`.

## Non-foreground run

Run from PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\ModularHarnessV1Acceptance\Invoke-ModularHarnessAcceptance.ps1
```

The default output root is a new timestamped child of the Windows temporary directory. The runner:

1. restores the current solution;
2. runs harness, Asset, Visual, embedded WPF, and dedicated 100K suites separately;
3. treats a zero-test suite as unverified;
4. calculates the current-run unique total from TRX test IDs instead of adding historic totals;
5. builds product Debug and Release with warnings treated as errors;
6. publishes `PixelTart_ModularHarness_V1_DevPreview.exe` self-contained for `win-x64` and records both the host EXE and `PixelTart.Modules.AssetLibrary.dll` SHA-256 values;
7. requires a real 100K metrics artifact before either 100K field becomes true;
8. requires a real foreground result, three exact process-tree snapshots, and all ten unique evidence PNGs;
9. writes UTF-8-without-BOM JSON and returns a non-zero exit code while any closure gate is incomplete.

`PIXEL_TART_MODULAR_HARNESS_METRICS_PATH` is scoped to the dedicated scale suite. The production-backed test must write `visual-scale-100k.metrics.json` with a corpus count of 100,000, measured query and similarity times, result counts, and `pairwise_cache_built=false`.

## Foreground boundary

The runner deliberately does not operate the desktop. Foreground validation starts only after the main implementation is declared ready. Use only generated JPEGs and the exact ten scenes in `evidence-contract.json`.

During that real foreground run:

1. set `PIXEL_TART_ASSET_LIBRARY_DEMO_DIR` to the generated `images` directory and launch with a new explicit `PIXEL_TART_ACCEPTANCE_ROOT`;
2. after the embedded page imports all 12 files, copy `InputDiagnostics/asset-library-import.json` before applying filters or similarity and keep that snapshot in the acceptance output root;
3. copy `foreground-result.template.json` to the acceptance output root as `foreground-result.json`, set the runtime-only absolute manifest/diagnostics paths plus the exact foreground EXE and Asset module SHA-256 values, and update a field only after the action succeeds;
4. use `Get-ModularHarnessProcessSnapshot.ps1` against the same shell PID before Asset navigation, after Asset navigation, and after returning to Workbench;
5. save the snapshots as `process-before-asset.json`, `process-after-asset.json`, and `process-after-return.json`;
6. capture all ten images directly from the Modular Harness DevPreview into `ui-review/modular-harness`;
7. set `capture_status` in `evidence-contract.json` to `captured` only after all ten files exist.

The self-contained publish may also contain the .NET runtime's exact `createdump.exe` helper. The publish gate classifies that known runtime-pack helper separately: it requires exactly one application executable (`PixelTart_ModularHarness_V1_DevPreview.exe`), exactly one `createdump.exe` backed by the generated runtime-pack `.deps.json`, and rejects every other executable.

The foreground result records `visual_smart_folder_verified`, `color_similarity_verified`, and `palette_similarity_verified` independently. For the global Task Center, record the same real foreground trigger and completed row together with the isolated `pixel-tart.db` task id, exact input/result, and persistent `CreatedAt`, `StartedAt`, and `CompletedAt` values. The WPF acceptance suite opens that database read-only and requires the matching `AuditLogs` transitions `Pending -> Preparing`, `Preparing -> Running`, and `Running -> Completed` in order. Set `task_center_verification_source` to `foreground_action+sqlite_audit`; Queued and Running were persistence-verified rather than visually observed, so `task_center_queued_foreground_observed` and `task_center_running_foreground_observed` remain false while `task_center_completed_foreground_observed` is true. Keep the compatibility fields `global_task_center_queued_verified`, `global_task_center_running_verified`, `global_task_center_completed_verified`, and `global_task_center_verified`; set them true only after the database-backed lifecycle gate passes. Every field defaults to false.

Every snapshot enumerates the exact root process, all descendants, and every globally running process with the exact DevPreview executable name. Closure requires one process in the tree, zero descendants, exactly one matching DevPreview process globally, one GUI PID, the same root PID at all three stages, and GUI counts `before=after asset=after return=1`.

Missing screenshots remain missing. Do not copy or rename standalone Asset Library evidence into `ui-review/modular-harness`.
