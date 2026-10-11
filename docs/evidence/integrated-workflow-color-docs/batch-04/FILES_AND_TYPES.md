# 文件清单及类型

本批源码差异相对 144de200a563c3648caa70b49373932d0df25f57。仅提交源码、测试、脚本及脱敏文档。

| 变更 | 文件 | 类型 |
|---|---|---|
| A | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.AnnotationWrites.cs` | C# 产品实现 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.ContextualInspector.cs` | C# 产品实现 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs` | C# 产品实现 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3TagManager.cs` | C# 产品实现 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Models/PublishingModels.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/PhotographerAnnotationStore.cs` | C# 产品实现 |
| A | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.AnnotationUndo.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.P3.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.V15.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioSessionStore.cs` | C# 产品实现 |
| A | `src/RAWSelectionAssistant.Core/Services/Projects/ExportRecovery.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Projects/PlanningDocumentPackage.cs` | C# 产品实现 |
| A | `src/RAWSelectionAssistant.Core/Services/Projects/PlanningRichLayout.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Projects/StudioEditVersion.cs` | C# 产品实现 |
| A | `src/RAWSelectionAssistant.Core/Services/Projects/StudioVersionRepository.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Publishing/PublishingExportService.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Tethering/TetherLibraryHandoffService.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/Resources/Studio/en-US.json` | 语言资源 |
| M | `src/RAWSelectionAssistant/Resources/Studio/zh-CN.json` | 语言资源 |
| M | `src/RAWSelectionAssistant/Resources/Studio/zh-TW.json` | 语言资源 |
| A | `src/RAWSelectionAssistant/Services/PlanningDocumentLayout.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/Services/PlanningDocxAdapter.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/Services/PlanningProposalPdf.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/Services/Publishing/WpfPublishingRenderer.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel.Document.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Session.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Versions.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/Views/PlanningRichEditor.cs` | C# 产品实现 |
| M | `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml` | WPF 界面 |
| M | `src/RAWSelectionAssistant/Views/StudioStartupWindow.cs` | C# 产品实现 |
| A | `tests/Kitao.ProcessProbe/Kitao.ProcessProbe.csproj` | 命令行进程测试/脚本 |
| A | `tests/Kitao.ProcessProbe/Program.cs` | 命令行进程测试/脚本 |
| A | `tests/Kitao.ProcessProbe/Run-ProcessChecks.ps1` | 命令行进程测试/脚本 |
| M | `tests/RAWSelectionAssistant.Tests/AssetLibraryP2CoreTests.cs` | 自动测试 |
| M | `tests/RAWSelectionAssistant.Tests/AssetLibraryP3RepositoryTests.cs` | 自动测试 |
| A | `tests/RAWSelectionAssistant.Tests/BatchFourPersistenceTests.cs` | 自动测试 |
| M | `tests/RAWSelectionAssistant.Tests/HomeMigrationBaselineTests.cs` | 自动测试 |
| M | `tests/RAWSelectionAssistant.Tests/PublishingCoreTests.cs` | 自动测试 |
| A | `tests/RAWSelectionAssistant.WpfTests/BatchFourLayoutTests.cs` | 自动测试 |
| A | `tests/RAWSelectionAssistant.WpfTests/BatchFourVersionTests.cs` | 自动测试 |
| M | `tests/RAWSelectionAssistant.WpfTests/ColorStudioFilmstripStateTests.cs` | 自动测试 |
| M | `tests/RAWSelectionAssistant.WpfTests/EmbeddedAssetLibraryWpfTests.cs` | 自动测试 |

## 证据文件

| 文件 | 类型 |
|---|---|
| ANNOTATION_WRITE_PATH_AUDIT.md | 中文实施/验收报告 |
| DELIVERY.md | 中文实施/验收报告 |
| DOCUMENT_LAYOUT_RESULTS.md | 中文实施/验收报告 |
| DOCUMENT_MEASUREMENTS.txt | 合成文档测量摘要 |
| EDIT_VERSION_PERSISTENCE.md | 中文实施/验收报告 |
| EXPORT_RECOVERY_RESULTS.md | 中文实施/验收报告 |
| MANUAL_ACCEPTANCE.md | 中文实施/验收报告 |
| PROCESS_RESULTS.json | 脱敏机器证据 |
| RELEASE_IDENTITY.json | 脱敏机器证据 |
| REQUIREMENTS_TRACE.md | 中文实施/验收报告 |
| SKIPPED_TESTS.md | 中文实施/验收报告 |
| STARTUP_REFERENCE.md | 中文实施/验收报告 |
| TEST_AND_RELEASE_MATRIX.md | 中文实施/验收报告 |
| TEST_SUMMARY.json | 脱敏机器证据 |
| FILES_AND_TYPES.md | 本清单 |
| PUSH_VERIFICATION.md | 远端核验 |
