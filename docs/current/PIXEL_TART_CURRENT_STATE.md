# Pixel Tart current capability state

Generated: 2026-09-30T02:48:45.9532271+00:00
Generated from source HEAD: 68cf45bfc557d616682de01dc416c8a1efaf04a9
Branch: integration/pixel-tart-developer-preview

| Capability | Implementation | Verification | Evidence | Next gate |
|---|---|---|---|---|
| ReferenceMatchV3 | IMPLEMENTED | PASS | src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioRenderPipeline.cs | Existing acceptance maintenance |
| ReferenceMatchV4 | PARTIAL | NOT_RUN | src/RAWSelectionAssistant.Core/Services/Projects/ReferenceMatchV4.cs | Product correctness closure |
| RawHighPrecision | PARTIAL | NOT_RUN | src/RAWSelectionAssistant.Core/Services/Color/HighBitDepthImageBuffer.cs<br>src/RAWSelectionAssistant.Core/Services/Projects/FrozenRawMaster.cs | Real corpus and decoder compatibility |
| Tiff16Publishing | PARTIAL | NOT_RUN | src/RAWSelectionAssistant.Core/Services/Export/TiffExport.cs<br>src/RAWSelectionAssistant.Core/Services/Export/AtomicTiffWriter.cs | ICC/metadata/read-back closure |
| ColorPipelineSnapshot | SPEC_ONLY | NOT_RUN | docs/color-architecture/COLOR_STAGE_SNAPSHOT_PROPOSAL.md | Canonical snapshot contract |
| Photography | PARTIAL | PASS | src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs<br>tests/RAWSelectionAssistant.Tests/PhotographerWorkflowFoundationTests.cs | Native 2-Up walkthrough and action-target closure |
| Publishing | PARTIAL | PASS | src/RAWSelectionAssistant.Core/Services/Publishing/<br>src/RAWSelectionAssistant/ViewModels/PublishingExportViewModel.cs | Live snapshot UI acceptance |
| Compare | PARTIAL | PASS | src/RAWSelectionAssistant.Core/Models/Photography/PhotographyCompareModels.cs<br>src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs | Native physical-pixel walkthrough |
| CreativeFilm | PARTIAL | PASS | src/RAWSelectionAssistant.Core/Services/Projects/PixelTartFilm.cs<br>tests/RAWSelectionAssistant.Tests/PixelTartFilmTests.cs | Color architecture integration |
| SpectralFilm | SPEC_ONLY | NOT_RUN | docs/research/OPEN_SOURCE_COLOR_ENGINE_PHASE0.md | Film Lab Phase 1 |
| InstantFilm | SPEC_ONLY | NOT_RUN | docs/PIXEL_TART_PHOTOGRAPHER_OS_ROADMAP.md | Future characterization study |
| AdobeXmpPreset | PARTIAL | PASS | src/RAWSelectionAssistant.Core/Services/Presets/AdobeXmpPresets.cs<br>tests/RAWSelectionAssistant.Tests/AdobeXmpPresetTests.cs | Full semantic coverage |
| CubeLutPreset | PARTIAL | PASS | src/RAWSelectionAssistant.Core/Services/Tethering/LutServices.cs<br>tests/RAWSelectionAssistant.Tests/Version230StageDColorCoreTests.cs | Product browser consolidation |
| CaptureOnePreset | SPEC_ONLY | NOT_APPLICABLE | src/RAWSelectionAssistant.Core/Services/RawToJpeg/CaptureOnePresetImporter.cs | Legal fixture adapter verification |
| 3DColorSpaceCore | PARTIAL | PASS | src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioColorSpace.cs<br>tests/RAWSelectionAssistant.Tests/ColorSpaceDataModelTests.cs | Windows renderer |
| 3DColorSpaceRenderer | PARTIAL | NOT_RUN | src/RAWSelectionAssistant/Views/ColorSpace3DViewport.cs<br>tests/RAWSelectionAssistant.WpfTests/ColorSpace3DViewportTests.cs<br>tests/RAWSelectionAssistant.WpfTests/NativeClosureObservationTests.cs<br>src/RAWSelectionAssistant.Core/Services/Projects/ColorSpaceRendererContract.cs<br>tests/RAWSelectionAssistant.Tests/ColorSpaceBoundsFitTests.cs | Bounds-aware Fit automated PASS; native pointer/resize gates require permitted input/capture environment; user visual review |
| GPUFullPixel | PARTIAL | NOT_RUN | src/PixelTart.MatchV4.Dx12/<br>docs/color-studio/COLOR_MATCH_GPU_CURRENT_STATE_2026-09-28.md | Full product integration |
| ICCRegistry | PARTIAL | PASS | src/RAWSelectionAssistant.Core/Services/Color/ColorProfileRegistry.cs<br>docs/color-studio/ICC_PIPELINE_AUDIT.md | Validated conversion |
| ICCConversion | PARTIAL | NOT_RUN | src/RAWSelectionAssistant/Services/Publishing/WpfPublishingRenderer.cs | Pixel conversion validation |

This file is generated from the explicit mapping in `scripts/generate-current-capabilities.ps1`. Historical acceptance and roadmap documents remain preserved evidence, not the current source of truth.
