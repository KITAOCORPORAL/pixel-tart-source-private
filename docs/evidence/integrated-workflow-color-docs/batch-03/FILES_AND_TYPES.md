# 文件及类型

源码相对 START_HEAD 的实际变更。构建产物、原始日志、图库数据库及客户照片未提交。

| 变更 | 路径 | 类型 |
|---|---|---|
| M | `README.md` | 品牌说明 |
| M | `installer/RAWSelectionAssistant.iss` | 安装器显示名称（未构建） |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.Canvas.cs` | C# 实现 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml` | WPF 界面 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.Identity.cs` | C# 实现 |
| M | `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.QuickTools.cs` | C# 实现 |
| M | `src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasView.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Models/Branding.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Models/TetherModels.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryIdentityResolver.cs` | C# 实现 |
| A | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetRelocationService.cs` | C# 实现 |
| A | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/CrossLibraryRelocation.cs` | C# 实现 |
| A | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/PhotographerAnnotationStore.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Database/SqliteTetherRepositories.cs` | C# 实现 |
| A | `src/RAWSelectionAssistant.Core/Services/Projects/CandidateColorBoundary.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioSessionStore.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Projects/PlanningDocumentPackage.cs` | C# 实现 |
| A | `src/RAWSelectionAssistant.Core/Services/Projects/StudioEditVersion.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Tethering/LinkedTetherAnnotationService.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant.Core/Services/Tethering/TetherLibraryHandoffService.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/App.xaml.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/MainWindow.xaml` | WPF 界面 |
| M | `src/RAWSelectionAssistant/MainWindow.xaml.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj` | 依赖/构建配置 |
| M | `src/RAWSelectionAssistant/Resources/Studio/en-US.json` | 语言资源 |
| M | `src/RAWSelectionAssistant/Resources/Studio/zh-CN.json` | 语言资源 |
| M | `src/RAWSelectionAssistant/Resources/Studio/zh-TW.json` | 语言资源 |
| A | `src/RAWSelectionAssistant/Services/PlanningDocxAdapter.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/Services/PlanningProposalPdf.cs` | C# 实现 |
| A | `src/RAWSelectionAssistant/ThirdPartyNotices/OpenXML-LICENSE.txt` | 第三方许可 |
| M | `src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel.Document.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Session.cs` | C# 实现 |
| A | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Versions.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/ViewModels/TetherCaptureViewModel.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs` | C# 实现 |
| A | `src/RAWSelectionAssistant/Views/PlanningRichEditor.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml` | WPF 界面 |
| A | `src/RAWSelectionAssistant/Views/StudioStartupWindow.cs` | C# 实现 |
| M | `src/RAWSelectionAssistant/Views/TetherCaptureView.xaml` | WPF 界面 |
| A | `tests/RAWSelectionAssistant.Tests/BatchThreeIdentityTests.cs` | 测试 |
| M | `tests/RAWSelectionAssistant.Tests/IdentityBridgeFoundationTests.cs` | 测试 |
| M | `tests/RAWSelectionAssistant.Tests/UiPolish142Tests.cs` | 测试 |
| M | `tests/RAWSelectionAssistant.WpfTests/AssetLibraryButtonReadabilityContractTests.cs` | 测试 |
| A | `tests/RAWSelectionAssistant.WpfTests/BatchThreeDocumentTests.cs` | 测试 |
| M | `tests/RAWSelectionAssistant.WpfTests/ColorStudioFilmstripStateTests.cs` | 测试 |

## 证据文件

| 文件 | 类型 |
|---|---|
| BRAND_AND_STARTUP.md | 中文报告 |
| COLOR_PIPELINE_RESULTS.md | 中文报告 |
| DELIVERY.md | 中文报告 |
| DOCUMENT_EDITOR_RESULTS.md | 中文报告 |
| EDIT_VERSION_AND_EXPORT.md | 中文报告 |
| IDENTITY_AND_CONCURRENCY.md | 中文报告 |
| MANUAL_ACCEPTANCE.md | 中文报告 |
| RELEASE_IDENTITY.json | 脱敏机器证据 |
| REQUIREMENTS_TRACE.md | 中文报告 |
| SKIPPED_TESTS.md | 中文报告 |
| TEST_AND_RELEASE_MATRIX.md | 中文报告 |
| TEST_SUMMARY.json | 脱敏机器证据 |
| FILES_AND_TYPES.md | 本清单 |
| PUSH_VERIFICATION.md | 推送核验回执 |
