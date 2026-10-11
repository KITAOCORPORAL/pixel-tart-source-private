# 文件及类型

以下为本批源码提交相对 START_HEAD 的清单。原片、数据库、bin/obj、TRX 与发布二进制均不在 Git 交付中。

| 变更 | 路径 | 类型 |
|---|---|---|
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.Canvas.cs` | C# 产品代码 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs` | C# 产品代码 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml` | WPF UI |
| A | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.Identity.cs` | C# 产品代码 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.QuickTools.cs` | C# 产品代码 |
| M | `src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasSurface.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContainerService.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryIdentityResolver.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant.Core/Services/Database/IdentityBridgeRepository.Recovery.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant.Core/Services/Database/IdentityBridgeRepository.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioLinearCandidate.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioSessionStore.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant.Core/Services/Projects/PlanningCenter.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant.Core/Services/Projects/PlanningDocument.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant.Core/Services/Projects/PlanningDocumentPackage.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant.Core/Services/Projects/TetherCaptureContextStore.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant.Core/Services/Tethering/LinkedTetherAnnotationService.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant.Core/Services/Tethering/TetherAnnotationMerge.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant.Core/Services/Tethering/TetherLibraryHandoffService.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant.Core/Services/Tethering/WatchFolderCameraAdapter.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/App.xaml.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/MainWindow.xaml.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant/Services/PlanningCanvasSnapshot.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel.Document.cs` | C# 产品代码 |
| A | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Identity.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Session.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs` | C# 产品代码 |
| M | `src/RAWSelectionAssistant/Views/TetherCaptureView.xaml` | WPF UI |
| A | `tests/PixelTart.DocumentPrototype.Tests/DocxRoundTripProbe.cs` | 自动测试/实验工程 |
| A | `tests/PixelTart.DocumentPrototype.Tests/PixelTart.DocumentPrototype.Tests.csproj` | 自动测试/实验工程 |
| A | `tests/RAWSelectionAssistant.Tests/BatchTwoColorGoldenTests.cs` | 自动测试/实验工程 |
| A | `tests/RAWSelectionAssistant.Tests/BatchTwoDocumentTests.cs` | 自动测试/实验工程 |
| A | `tests/RAWSelectionAssistant.Tests/BatchTwoRelationshipTests.cs` | 自动测试/实验工程 |
| M | `tests/RAWSelectionAssistant.Tests/IdentityBridgeFoundationTests.cs` | 自动测试/实验工程 |
| A | `tests/RAWSelectionAssistant.Tests/IdentityBridgeRecoveryTests.cs` | 自动测试/实验工程 |
| M | `tests/RAWSelectionAssistant.Tests/Version230WatchFolderSessionTests.cs` | 自动测试/实验工程 |
| M | `tests/RAWSelectionAssistant.WpfTests/AssetLibraryButtonReadabilityContractTests.cs` | 自动测试/实验工程 |
| A | `tests/RAWSelectionAssistant.WpfTests/BatchTwoDocumentWpfTests.cs` | 自动测试/实验工程 |
| M | `tests/RAWSelectionAssistant.WpfTests/ColorStudioFilmstripStateTests.cs` | 自动测试/实验工程 |

## 证据文件

| 文件 | 类型 |
|---|---|
| CROSS_MODULE_IMPLEMENTATION.md | 中文实施/验收报告 |
| DELIVERY.md | 中文实施/验收报告 |
| DOCUMENT_PACKAGE_AND_CANVAS.md | 中文实施/验收报告 |
| MANUAL_ACCEPTANCE.md | 中文实施/验收报告 |
| MIGRATION_AND_RECOVERY_RESULTS.md | 中文实施/验收报告 |
| RELEASE_IDENTITY.json | 脱敏机器可读证据 |
| REQUIREMENTS_TRACE.md | 中文实施/验收报告 |
| SKIPPED_TESTS.md | 中文实施/验收报告 |
| TEST_AND_RELEASE_MATRIX.md | 中文实施/验收报告 |
| TEST_SUMMARY.json | 脱敏机器可读证据 |
| FILES_AND_TYPES.md | 本清单 |
| PUSH_VERIFICATION.md | 远端核验回执 |
