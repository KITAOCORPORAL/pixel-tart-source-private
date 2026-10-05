# 本轮修改文件清单

2026-10-05。生产与测试列表已对照起点到已提交源码 `31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591` 的Git差异核对；阶段文档和发布manifest仍待证据提交。只列仓库相对路径，不包含用户输入或Release二进制。

起点：`5427406c4ef3c805ef56fbb814251df69365032d`。生产提交：`11379a66072ba88cce61dc1d16efd097262680cd`（重构实现）及 `31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`（文件末尾空行规范）。后者相对前者仅删4处末尾空行，无处理行为变更。r1的SourceHead为31d3ac3；核验时工作树src/tests与其无差异。

两个 Guardian 自动生成文件明确排除：`docs/evidence/ui-guardian/P0_ROOT_CAUSE_ANALYSIS.json`、`docs/evidence/ui-guardian/static-xaml-lint.json`。本表未恢复、覆盖或提交它们。

| 类型 | 文件数 |
|---|---:|
| 生产 Core | 15 |
| 生产 ViewModel | 16 |
| 生产 UI / 绘制 / 资源 | 18 |
| 生产服务 / 应用 / 项目配置 | 5 |
| Core 测试 | 7 |
| WPF 测试 | 15 |
| 阶段文档及身份JSON | 23 |
| 发布manifest | 1 |
| 合计 | 100 |

## 生产 Core

| Git 类型 | 文件 |
|---|---|
| 修改 | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisEngine.cs` |
| 修改 | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisModels.cs` |
| 新增 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorSpaceSurface.cs` |
| 修改 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioColorSpace.cs` |
| 新增 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioEffectiveState.cs` |
| 修改 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioModels.cs` |
| 新增 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioProcessingAnalysis.cs` |
| 修改 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioRenderPipeline.cs` |
| 新增 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioSessionStore.cs` |
| 新增 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioToolCatalog.cs` |
| 新增 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioToolProcessor.cs` |
| 修改 | `src/RAWSelectionAssistant.Core/Services/Projects/MatchV4ProductSession.cs` |
| 修改 | `src/RAWSelectionAssistant.Core/Services/Projects/PixelTartFilm.cs` |
| 新增 | `src/RAWSelectionAssistant.Core/Services/Projects/RawImageOrientation.cs` |
| 修改 | `src/RAWSelectionAssistant.Core/Services/Projects/RawMatchTiff16ProductPipeline.cs` |

## 生产 ViewModel

| Git 类型 | 文件 |
|---|---|
| 修改 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Analysis.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.BatchHistory.cs` |
| 修改 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.ExportFormat.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Filmstrip.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.RangeSelection.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.RawExport.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.RawOrientation.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Session.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Thumbnails.cs` |
| 修改 | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.DetailPreview.cs` |
| 修改 | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Develop.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.History.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Selection.cs` |
| 新增 | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Tools.cs` |

## 生产 UI / 绘制 / 资源

| Git 类型 | 文件 |
|---|---|
| 修改 | `src/PixelTart.Modules.AssetLibrary/HistogramDrawing.cs` |
| 新增 | `src/RAWSelectionAssistant/Resources/Studio/en-US.json` |
| 新增 | `src/RAWSelectionAssistant/Resources/Studio/zh-CN.json` |
| 新增 | `src/RAWSelectionAssistant/Resources/Studio/zh-TW.json` |
| 修改 | `src/RAWSelectionAssistant/Views/CloudInspectionControls.cs` |
| 修改 | `src/RAWSelectionAssistant/Views/ColorSpace3DViewport.cs` |
| 修改 | `src/RAWSelectionAssistant/Views/ColorStudioImageViewport.cs` |
| 修改 | `src/RAWSelectionAssistant/Views/ImageHighlightOverlay.cs` |
| 新增 | `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.Layout.cs` |
| 修改 | `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml` |
| 修改 | `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs` |
| 新增 | `src/RAWSelectionAssistant/Views/StudioColorLabelFilters.cs` |
| 新增 | `src/RAWSelectionAssistant/Views/StudioCurveEditor.cs` |
| 新增 | `src/RAWSelectionAssistant/Views/StudioExportFormatSelector.cs` |
| 新增 | `src/RAWSelectionAssistant/Views/StudioHistogramView.cs` |
| 新增 | `src/RAWSelectionAssistant/Views/StudioSpaceControls.cs` |
| 新增 | `src/RAWSelectionAssistant/Views/StudioTextExtension.cs` |
| 新增 | `src/RAWSelectionAssistant/Views/StudioToolPanel.cs` |

## 生产服务 / 应用 / 项目配置

| Git 类型 | 文件 |
|---|---|
| 修改 | `src/RAWSelectionAssistant/App.xaml.cs` |
| 修改 | `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj` |
| 修改 | `src/RAWSelectionAssistant/Services/ColorStudioBitmapRenderer.cs` |
| 新增 | `src/RAWSelectionAssistant/Services/StudioLocalizationService.cs` |
| 修改 | `src/RAWSelectionAssistant/Services/StudioQuickExport.cs` |

## Core 测试

| Git 类型 | 文件 |
|---|---|
| 新增 | `tests/RAWSelectionAssistant.Tests/ColorSpaceSurfaceTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.Tests/ColorStudioEffectiveStateTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.Tests/ColorStudioRealCorpusTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.Tests/ColorStudioSessionStateTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.Tests/ColorStudioSpatialScaleTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.Tests/ColorStudioToolProcessorTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.Tests/RawImageOrientationTests.cs` |

## WPF 测试

| Git 类型 | 文件 |
|---|---|
| 修改 | `tests/RAWSelectionAssistant.WpfTests/BatchExportProcessedPixelsTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/ColorRangePreviewOverlayTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/ColorSpaceSurfaceWpfTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/ColorStudioFilmstripStateTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/ColorStudioToolExportParityTests.cs` |
| 修改 | `tests/RAWSelectionAssistant.WpfTests/EveningFeedbackWpfTests.cs` |
| 修改 | `tests/RAWSelectionAssistant.WpfTests/RawMatchTiff16ProductWpfTests.cs` |
| 修改 | `tests/RAWSelectionAssistant.WpfTests/ReferenceWorkspaceWideRatioTests.cs` |
| 修改 | `tests/RAWSelectionAssistant.WpfTests/RuntimeCorrectionWpfTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/StudioFinalStateRegressionTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/StudioLocalizationTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/StudioQuickExportFormatTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/StudioRawDetailPreviewTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/StudioToolEditingTests.cs` |
| 新增 | `tests/RAWSelectionAssistant.WpfTests/StudioToolPanelLayoutTests.cs` |

## 阶段文档

| Git 类型 | 文件 |
|---|---|
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/00_START_HERE.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/EVIDENCE_INDEX.json` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/C1_FEATURE_BEHAVIOR_MATRIX.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/COLOR_STUDIO_DESIGN_SPEC.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/FILE_CHANGE_LIST.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/IMPLEMENTATION_MATRIX.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/LOCALIZATION_SCOPE.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/MODULE_RUNTIME_OBSERVATIONS.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/OPEN_ISSUES.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/PARAMETER_CONTRACT.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/PIPELINE_AUDIT.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/PROCESSING_IMPLEMENTATION.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/QUICK_EXPORT_FORMAT_CONTRACT.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/RAW_PREVIEW_COORDINATE_CONTRACT.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/RELEASE_IDENTITY.json` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/RELEASE_RUNTIME_REPLAY.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/SPACE_AUDIT.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/SPACE_IMPLEMENTATION.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/SPATIAL_SCALE_VERIFICATION.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/START_STATE.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/TEST_RESULTS.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/WORKSPACE_AUDIT.md` |
| 新增 | `docs/evidence/color-studio-rebuild-2026-10-05/WORKSPACE_STATE_IMPLEMENTATION.md` |

## 发布manifest

| Git 类型 | 文件 |
|---|---|
| 新增（待提交） | `artifacts/releases/color-studio-rebuild-2026-10-05-r1/release-manifest.json` |

r1发布包含287个文件，已逐一核对长度和SHA256，零缺失、零不匹配。它们不进入Git；manifest只保存路径/长度/hash和构建身份。`RELEASE_IDENTITY.json`的manifest hash、EXE/DLL hash和ProductVersion均与实际相符。最终r1构建身份见TEST_RESULTS.md；本清单不修改manifest、JSON或运行验收状态。

## 证据提交范围与忽略规则

- 现有 `.gitignore` 已允许 `artifacts/releases/**/release-manifest.json`，并忽略 `artifacts/releases/**/publish/` 和所有 `*.log`；不需要放宽规则。
- 核验时 `git ls-files --others --exclude-standard artifacts/releases/color-studio-rebuild-2026-10-05-r1` 仅列出这一个manifest。`git status`显示整个r1目录untracked是目录折叠显示，不表示287文件可被普通add收入。
- 可提交本阶段21份Markdown、`RELEASE_IDENTITY.json`、`EVIDENCE_INDEX.json`和上述单个manifest；用精确文件/目录路径暂存后再次检查staged清单。不要使用force add、不要放行publish，也不将二进制或用户照片加到证据目录。
- 原始publish/build/runtime日志和TRX保留本机artifacts并继续ignored；仓库保留本文件与TEST_RESULTS的脱敏摘要。若需新日志证据，先脱敏为单独Markdown/JSON摘要，不把原日志私人路径粘入Git。
- manifest顶层使用既有实际工作树绝对EXE/DLL路径，未含机器用户名；每个发布文件路径为相对路径。当前manifest与身份JSON的digest闭环已核对，不为美化路径改写manifest后遗留旧digest。

## 核对范围

生产行为、每项测试与运行状态见 IMPLEMENTATION_MATRIX.md；测试计数与跳过见 TEST_RESULTS.md。App.xaml.cs 的变更仅让显式隔离运行目录取得独立实例锁，防止验收候选激活用户其他实例；没有改变普通启动单实例策略。新增的 ColorStudioEffectiveState 与最终状态测试属于末轮修复，已由新集中编译/P2聚焦50/0/1及独立Core9/0/0覆盖；最终发布及实机仍独立。本清单不把文件存在作为测试或验收通过。
