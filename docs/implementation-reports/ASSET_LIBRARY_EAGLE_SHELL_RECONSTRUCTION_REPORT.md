# Asset Library Eagle Shell — Remote Sync and Acceptance

Date: 2026-09-17. Product: Pixel Tart 2.3.0-RC12. Branch: `integration/pixel-tart-developer-preview`. This report is an acceptance record, not a claim of complete Eagle parity or physical-machine release approval.

## Identity and synchronization

- Starting implementation HEAD: `2b16483db51fd8497a4e8ac0cfbde55e1493ea28`, `refactor(asset-library): rebuild eagle style shell layout`.
- The remote is named `source-private` in this checkout (no `origin` remote), at `https://github.com/KITAOCORPORAL/pixel-tart-source-private.git`. The target ref is `refs/heads/integration/pixel-tart-developer-preview`.
- Before push: remote `b8552458818d8540ad6399318ae32605046e0ad6`; merge-base `b855245`; behind 0 / ahead 2. A normal fast-forward push advanced the remote to `2b16483`; fetch and SHA equality were verified.
- Final acceptance-report commit and remote HEAD are verified by `git rev-parse HEAD` and `git rev-parse source-private/integration/pixel-tart-developer-preview` after the last push. The final-HEAD screenshot manifest, not the historical `84d28df` evidence, is authoritative.
- `docs/design/ASSET_LIBRARY_EAGLE_LAYOUT_SPEC.md` was already tracked in `2b16483`; this acceptance pass adds the explicit mismatch matrix.

## Changed files and product boundary

The implementation commit changes Asset Library shell XAML/code-behind, organization/query view models, workspace settings, dark/light tokens, icon resources and related WPF tests; its full changed-file list is `git show --stat 2b16483`. It supplies the icon-first toolbar, system/folder/tag sidebar, original-proportion gallery, automatic photo information pane, compact filter entry, grouped context menu, centered high-quality preview route and the existing Creative Board overlay. The separate acceptance commit changes only this report, the design status matrix and the x64 WPF isolation runner's platform selection. Existing uncommitted RC12 installer/real-user/readiness reports are not part of either commit. No schema, AssetOrigin, thumbnail provider, project/booking data model or customer media was changed.

## Automated verification

| Gate at implementation HEAD `2b16483` | Result | Evidence |
| --- | --- | --- |
| Release x64 solution build (`--no-restore -warnaserror`) | DONE: 0 warnings / 0 errors | Terminal build output, 2026-09-17 |
| Eagle-focused WPF tests | DONE: 53/53, 0 failed, 0 skipped | `tests/RAWSelectionAssistant.WpfTests/TestResults/eagle-targeted-2b16483.trx` |
| WPF process-per-fixture, x64 | DONE: 97 fixtures, 1199/1199, 0 failed, 0 skipped | `artifacts/rc12-wpf-process-isolation/eagle-shell-x64-2b16483/rc12-wpf-process-isolation.json` |
| Real App.xaml / Pixel Tart dark theme / MainWindow visual harness | DONE as automated capture: 71/71; 12 product, 10 UX, 10 Asset Library, 32 logical-DPI, 6 resolution, 1 aspect-ratio | `artifacts/eagle-shell-acceptance-final-head/rc12-product-visual-evidence.json`; strict validator passed |
| 100%, 125%, 150%, 200% | DONE for automated logical-DPI only: eight states per scale | `artifacts/eagle-shell-acceptance-final-head/dpi-current/` |
| Physical monitor/DPI, user gestures, installer | PARTIAL: not performed in this pass | Separate RC12 real-user/installer reports remain uncommitted and must not be read as completed here |

The first isolation attempt ran `dotnet test` against the default `bin/Release` assembly although the current solution build emitted `bin/x64/Release`. It produced 9 failures in 8 fixtures from stale layout assertions (1184 passed / 9 failed / 0 skipped). The runner now passes `-p:Platform=x64` on both build and test. The clean x64 rerun above is the authoritative result; no test was disabled or weakened.

The old monolithic TRX's skipped `AssetLibraryP2AutomatedEvidenceContractTests.ValidateExistingRunAcceptsLatestCapturedP2RunWithoutChangingIt` is an `Assert.Inconclusive` when no sealed, non-reparse P2 run root exists under `.validation`. This is historical acceptance evidence availability, not the Eagle shell's Application singleton, dispatcher or shared-static contamination. Its presence in this worktree's current process-isolated run is recorded as passed; 0 skips. The opt-in scale-performance diagnostic is excluded by the runner's documented category policy and is not claimed as part of this 1199-test gate.

## Screenshot audit

Final-HEAD set: `artifacts/eagle-shell-acceptance-final-head/`. Ten named acceptance images are copied into `acceptance-screenshots/`. The capture manifest and every current-capture metadata JSON record the final remote HEAD; the pre-change aspect comparison alone is explicitly labeled `f4fbe9c577a9728d9b4305640bebad092ddb3db5`. The earlier implementation-checkpoint run remains at `artifacts/eagle-shell-acceptance-2b16483/` but is not final-HEAD proof. The producer launches a fresh real application process per fixture using synthetic JPEGs; source hashes match before/after. These are automated logical screenshots, not physical monitor photographs.

| Visual item | Status | Observation |
| --- | --- | --- |
| Main layout / Sidebar | PARTIAL | Continuous dark workspace, system tree and selected row; an extra application navigation rail remains, and current-library tab is overly bright. |
| Gallery | DONE for captured ratios | Landscape, portrait, ultra-wide and ultra-tall keep proportions with no visible letterbox; filename and pixel dimensions show. Scale scrolling and 10K real-photo behavior are not established here. |
| Inspector | PARTIAL | Single selection shows photo details and clear stars; relation and analysis controls make it denser than the reference. |
| Context menu | PARTIAL | Groups and icons align, but menu is tall and secondary-panel image shows only highlighted parent, not the child panel. Arrow contrast merits real review. |
| Color filter | MISMATCH | Popover is narrower than the workspace, yet its expanded state pushes the gallery down and shows an empty result; not the compact reference treatment. |
| Folder tree | PARTIAL | Folder hierarchy exists, but the named capture does not prove expanded child rows. |
| Creative Board | PARTIAL | Overlay captured with source-proportional thumbnails; real drag and switching remain manual. |
| Quick Preview | PARTIAL | Contract covers high-quality provider, centered popup and pointer-leave closure, but `09_loupe_active.png` visibly lacks a popup; visual closure is not proven. |
| Viewer | PARTIAL | Full-size viewer image and 100% / next / previous controls captured; mouse pan and physical perceived quality await acceptance. |
| Keyboard operation | PARTIAL | Focus contracts pass; real keyboard sequence remains unobserved. |

The current validator checks capture identity, file hashes and structural layout/theme conditions, but does not detect a missing floating submenu or loupe in the composed image. Those two captures must not be treated as visual DONE merely because metadata says `passed=true`. No new product feature or visual redesign was undertaken to paper over these gaps.

## Completion levels and remaining work

- Code implementation: DONE for the scoped `2b16483` shell; no further feature work in this pass.
- Automated testing: DONE for the x64 WPF gate and strict 71-image producer; PARTIAL for visual-semantic assertions of submenu and loupe.
- Visual acceptance: PARTIAL; filter is MISMATCH and the overlay evidence gaps above remain.
- User physical-machine acceptance: PARTIAL / not executed. The 1920×1080, 2560×1440 and 4K hardware/DPI combinations, 100/1000/10000 real-photo imports and photographer first-use observations require actual user machines.

Stop after synchronization and evidence handoff. Do not label a formal Release Candidate ready until the visual gaps and user-operated installation/physical pass are closed.
