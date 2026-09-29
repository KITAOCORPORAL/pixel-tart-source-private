# Pixel Tart current capability state

Generated: 2026-09-29T07:13:14.5262996+00:00
HEAD: 8885dff6d0c3e6d9d3aac3a6cfbd6b8252c89967
Branch: integration/pixel-tart-developer-preview

| Module | Status | Evidence | Next gate |
|---|---|---|---|
| Photography | PARTIAL | src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs<br>tests/RAWSelectionAssistant.Tests/PhotographerWorkflowFoundationTests.cs | Native 2-Up walkthrough and action-target closure |
| AssetLibrary | IMPLEMENTED | src/PixelTart.Modules.AssetLibrary/<br>tests/PixelTart.ModularHarness.Tests/ | Existing acceptance maintenance |
| Tether | PARTIAL | src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs<br>src/RAWSelectionAssistant/Views/TetherCaptureView.xaml | Native WPF review |
| Compare | PARTIAL | src/RAWSelectionAssistant.Core/Models/Photography/PhotographyCompareModels.cs<br>src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs<br>tests/RAWSelectionAssistant.Tests/PhotographerWorkflowFoundationTests.cs | Independent viewport/action integration |
| Publishing | PARTIAL | src/RAWSelectionAssistant.Core/Services/Publishing/<br>src/RAWSelectionAssistant/ViewModels/PublishingExportViewModel.cs<br>tests/RAWSelectionAssistant.Tests/PublishingCoreTests.cs | Live snapshot UI acceptance |
| ColorStudio | PARTIAL | src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs<br>docs/color-studio/COLOR_STUDIO_CURRENT_STATUS.md | Historical closure gates |
| MatchV3 | IMPLEMENTED | src/RAWSelectionAssistant.Core/Services/Projects/<br>docs/color-studio/ | No new gate in this phase |
| MatchV4 | PARTIAL | src/RAWSelectionAssistant.Core/Services/Projects/<br>docs/color-studio/COLOR_MATCH_GPU_CURRENT_STATE_2026-09-28.md | Product correctness closure |
| GPU | PARTIAL | src/PixelTart.MatchV4.Dx12/<br>docs/color-studio/COLOR_MATCH_GPU_CURRENT_STATE_2026-09-28.md | Full product integration |
| RAW | PARTIAL | src/RAWSelectionAssistant.Core/Services/RawToJpeg/<br>src/RAWSelectionAssistant.Core/Services/Raw/ | Modern LibRaw and real corpus |
| TIFF16 | PARTIAL | src/RAWSelectionAssistant.Core/Services/Export/TiffExport.cs<br>src/RAWSelectionAssistant/Services/Publishing/WpfPublishingRenderer.cs | ICC/metadata/read-back closure |
| Preset | PARTIAL | src/RAWSelectionAssistant.Core/Services/Publishing/<br>src/RAWSelectionAssistant/ViewModels/PublishingExportViewModel.cs | Preset product consolidation |
| Film | SPEC_ONLY | docs/design-system/19_FILM_CONTROLS.md<br>docs/research/OPEN_SOURCE_COLOR_ENGINE_PHASE0.md | Film Lab architecture |
| ICC | PARTIAL | src/RAWSelectionAssistant.Core/Services/Color/ColorProfileRegistry.cs<br>docs/color-studio/ICC_PIPELINE_AUDIT.md | Validated profile conversion |
| 3DColorSpace | PARTIAL | src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioColorSpace.cs<br>tests/RAWSelectionAssistant.Tests/ColorSpaceDataModelTests.cs | WPF renderer acceptance |
| Planning | IMPLEMENTED | src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel.cs | Roadmap maintenance |
| OnlineSelection | PARTIAL | src/RAWSelectionAssistant/ViewModels/OnlineSelectionViewModels.cs | Product closure |
| BrowserExtension | DEFERRED | docs/PIXEL_TART_PHOTOGRAPHER_OS_ROADMAP.md | Deferred by workflow correctness phase |

This file is generated from the explicit mapping in `scripts/generate-current-capabilities.ps1`. Historical acceptance and roadmap documents remain preserved evidence, not the current source of truth.
