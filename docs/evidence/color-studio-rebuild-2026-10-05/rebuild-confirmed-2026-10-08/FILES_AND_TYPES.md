# Files and types

Source changes relative to protected START_HEAD. No binaries, private originals, runtime databases or raw TRX paths are published.

| File | Type | Purpose |
|---|---|---|
| src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasView.cs | production logic/control | Real context commands, clipboard vs duplicate, capability visibility |
| src/PixelTart.Modules.AssetLibrary/HistogramDrawing.cs | production logic/control | Visible true histogram peaks without artificial baseline |
| src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioRenderPipeline.cs | production logic/control | Versioned matching, alpha/node-input statistics and shared processing; see ALGORITHM_REVIEW.md |
| src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioSessionStore.cs | production logic/control | Compatible persisted layout/metadata and read-only image navigation |
| src/RAWSelectionAssistant.Core/Services/Projects/ReferenceColorCoreV2.cs | production logic/control | Regression or scoped support; inspect named file in source commit |
| src/RAWSelectionAssistant.Core/Services/Projects/ReferenceLook.cs | production logic/control | Versioned matching, alpha/node-input statistics and shared processing; see ALGORITHM_REVIEW.md |
| src/RAWSelectionAssistant.Core/Services/Projects/ReferencePixelStatistics.cs | production logic/control | Versioned matching, alpha/node-input statistics and shared processing; see ALGORITHM_REVIEW.md |
| src/RAWSelectionAssistant/MainWindow.xaml | production UI layout | Global language entry and concise workflow copy |
| src/RAWSelectionAssistant/Resources/Studio/en-US.json | product localization | Three-language resources and live/persisted presentation |
| src/RAWSelectionAssistant/Resources/Studio/zh-CN.json | product localization | Three-language resources and live/persisted presentation |
| src/RAWSelectionAssistant/Resources/Studio/zh-TW.json | product localization | Three-language resources and live/persisted presentation |
| src/RAWSelectionAssistant/Services/ColorStudioBitmapRenderer.cs | production logic/control | Versioned matching, alpha/node-input statistics and shared processing; see ALGORITHM_REVIEW.md |
| src/RAWSelectionAssistant/Services/ReferenceLookPreviewService.cs | production logic/control | Versioned matching, alpha/node-input statistics and shared processing; see ALGORITHM_REVIEW.md |
| src/RAWSelectionAssistant/Services/StudioQuickExport.cs | production logic/control | Regression or scoped support; inspect named file in source commit |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.RangeSelection.cs | production logic/control | Existing workspace docking, grouping and photo strip presentation |
| src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Session.cs | production logic/control | Compatible persisted layout/metadata and read-only image navigation |
| src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs | production logic/control | Regression or scoped support; inspect named file in source commit |
| src/RAWSelectionAssistant/Views/ColorStudioZoomPanState.cs | production logic/control | Compatible persisted layout/metadata and read-only image navigation |
| src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.Filmstrip.cs | production logic/control | Existing workspace docking, grouping and photo strip presentation |
| src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.Layout.cs | production logic/control | Existing workspace docking, grouping and photo strip presentation |
| src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml | production UI layout | Existing workspace docking, grouping and photo strip presentation |
| src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs | production logic/control | Existing workspace docking, grouping and photo strip presentation |
| src/RAWSelectionAssistant/Views/ReferenceNavigator.xaml | production UI layout | Compatible persisted layout/metadata and read-only image navigation |
| src/RAWSelectionAssistant/Views/ReferenceNavigator.xaml.cs | production logic/control | Compatible persisted layout/metadata and read-only image navigation |
| src/RAWSelectionAssistant/Views/StudioColorBalanceWheel.cs | production logic/control | Graphical controls editing existing nodes/transactions |
| src/RAWSelectionAssistant/Views/StudioExportMenu.cs | production logic/control | Regression or scoped support; inspect named file in source commit |
| src/RAWSelectionAssistant/Views/StudioLanguageMenu.cs | production logic/control | Three-language resources and live/persisted presentation |
| src/RAWSelectionAssistant/Views/StudioLevelsGraph.cs | production logic/control | Graphical controls editing existing nodes/transactions |
| src/RAWSelectionAssistant/Views/StudioNumericEditor.cs | production logic/control | Graphical controls editing existing nodes/transactions |
| src/RAWSelectionAssistant/Views/StudioToolPanel.cs | production logic/control | Graphical controls editing existing nodes/transactions |
| tests/RAWSelectionAssistant.Tests/ColorStudioSessionStateTests.cs | test | Compatible persisted layout/metadata and read-only image navigation |
| tests/RAWSelectionAssistant.Tests/ReferencePixelStatisticsTests.cs | test | Versioned matching, alpha/node-input statistics and shared processing; see ALGORITHM_REVIEW.md |
| tests/RAWSelectionAssistant.WpfTests/CanvasContextMenuContractTests.cs | test | Real context commands, clipboard vs duplicate, capability visibility |
| tests/RAWSelectionAssistant.WpfTests/ColorSpaceSurfaceWpfTests.cs | test | Regression or scoped support; inspect named file in source commit |
| tests/RAWSelectionAssistant.WpfTests/ColorStudioZoomPanTests.cs | test | Compatible persisted layout/metadata and read-only image navigation |
| tests/RAWSelectionAssistant.WpfTests/ReferenceNavigatorDecodeTests.cs | test | Compatible persisted layout/metadata and read-only image navigation |
| tests/RAWSelectionAssistant.WpfTests/ReferenceWorkspaceWideRatioTests.cs | test | Regression or scoped support; inspect named file in source commit |
| tests/RAWSelectionAssistant.WpfTests/StudioColorBalanceWheelTests.cs | test | Graphical controls editing existing nodes/transactions |
| tests/RAWSelectionAssistant.WpfTests/StudioInteractiveSchedulingTests.cs | test | Regression or scoped support; inspect named file in source commit |
| tests/RAWSelectionAssistant.WpfTests/StudioLocalizationTests.cs | test | Three-language resources and live/persisted presentation |
| tests/RAWSelectionAssistant.WpfTests/StudioRebuildVisualEvidenceTests.cs | test | Regression or scoped support; inspect named file in source commit |
| tools/Write-StudioRebuildEvidence.ps1 | reproducible evidence tool | Regression or scoped support; inspect named file in source commit |

Evidence files: MD=review/report, JSON=sanitized machine results and manifest, PNG=explicitly labeled capture. Raw test reports and release payload remain ignored local artifacts.

See SCREENSHOT_INDEX.md for actual included images and capture limitations.

| Evidence file | Type | Purpose |
|---|---|---|
| 00_START_HERE.md | MD | Unique review/reproduce entry and identities |
| SYNC_AND_ENVIRONMENT.md | MD | Protected Git scene, environment and display |
| REQUIREMENTS_TRACE.md | MD | All41 A/B/C requirements and CU relationships |
| ISSUE_MATRIX.md | MD | Root causes, repaired paths, failed runs and limits |
| UI_LAYOUT_SPEC.md | MD | Five-category migration, docking and command contracts |
| RUNTIME_ACCEPTANCE.md | MD | All95 new-binary checks, historical status retained separately |
| PERFORMANCE.md | MD | Measured automatic correctness vs unmeasured native latency |
| ALGORITHM_REVIEW.md | MD | Actual processing domains/versioning/comparison limits |
| BUILD_AND_TESTS.md | MD | Executed commands and project results |
| TEST_RESULTS.json | JSON | All TRX counts/hashes and failed/skipped names, sanitized |
| RELEASE_MANIFEST.json | JSON | ExactSourceHead/version/287 payload identities |
| SCREENSHOT_INDEX.md | MD | Capture source and native/AUTO distinction |
| ROADMAP.md | MD | Confirmed remaining work distinct from later engineering |
| FILES_AND_TYPES.md | MD | This inventory |
| screenshots/NATIVE_00_old_company_start.png | PNG | Old production empty isolated Workbench |
| screenshots/NATIVE_01_final_release_start.png | PNG | Final production empty isolated Workbench |
| screenshots/AUTO_1180x720dip_mode0_100percent.png | PNG | Synthetic automatic narrow-layout render |
| screenshots/AUTO_1600x920dip_mode1_100percent.png | PNG | Synthetic automatic Color page/render |
| screenshots/AUTO_1600x920dip_mode2_100percent.png | PNG | Synthetic automatic Levels graph/render |
