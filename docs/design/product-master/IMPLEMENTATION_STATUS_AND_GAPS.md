# 实现状态、分支差异与未决缺口

## 1. 真实现场（2026-10-12）

工具实际运行目录为 D:/AI AGENT；不是提示中的Linux路径，也无要求迁移到N盘。C/D/E/F实际盘信息由只读Get-PSDrive取得（容量快照未持续监控）。共享Git主仓库 D:/AI AGENT/RAWSelectionAssistant，文档分支工作树 D:/AI AGENT/kitao-studio-product-master-spec-20261012。未切换、reset、stash、合并或清理原工作树。

|位置/分支|读取SHA|结论|
|---|---|---|
|integration/pixel-tart-developer-preview / rc6|6a27e166af5c77f8455f44089aa9de5d38e86495|旧集成基线，文档分支父提交|
|feature/pixel-tart-integrated-workflow-color-docs|b07de7fe6f389367044986af20a12e311fccc02c|第八批远端；生产树已索引|
|feature/pixel-tart-batch09-instant-film-quality-20261012|51b3f4f891a36acb6bd4f8a3a2e0dd83340bd1c2|第九批已在origin/source-private；不是已合入旧集成|
|feature/pixel-tart-batch10-raw-current-edits-20261012|061c4cf57a72e59f676142ae629ff7b11cd5fb99|审计开始时本地ahead 2；结束时功能分支7184966已推送且clean|
|独立rc7克隆|cc445e566c132b67587b747b27586ee584579609|旧克隆，刷新后落后集成72提交；不当最新|
|独立source-delivery克隆|c5f332fe7ba7375c018c80a600079e38580aa960|独立快照，记录但不混入当前生产树|
|独立handoff克隆|40b95738e6017c4d294712b3ef7eaed857d1a395|独立main快照；不推断当前功能齐全|

共享仓库origin/source-private均已成功fetch --prune。独立克隆批量fetch曾超时，未声称所有独立远端都成功刷新；SOURCE_AUDIT保留它们的本地refs、HEAD、工作树与remote。全部已发现工作树和dirty路径见该JSON。仓库发现范围为D:/AI AGENT及其worktrees和已识别相关克隆，不是全盘无边界搜索所有Git仓库。

## 2. 未提交内容保留

- D:/AI AGENT/RAWSelectionAssistant：0 条dirty/untracked记录；HEAD 4dac5f8e4460b7a67309646b6133bd186c121fea；feature/pixel-tart-product-redesign。
- D:/AI AGENT/pixel-tart-batch09-20261012：0 条dirty/untracked记录；HEAD 51b3f4f891a36acb6bd4f8a3a2e0dd83340bd1c2；feature/pixel-tart-batch09-instant-film-quality-20261012。
- D:/AI AGENT/pixel-tart-batch10-20261012：32 条dirty/untracked记录；HEAD 061c4cf57a72e59f676142ae629ff7b11cd5fb99；feature/pixel-tart-batch10-raw-current-edits-20261012。
- D:/AI AGENT/RAWSelectionAssistant_sqlite_gate：7 条dirty/untracked记录；HEAD c9af9ebad8e36fc99d753d330f59a4a3c6b70146；detached。
- D:/AI AGENT/worktrees/al-f01-foundation：0 条dirty/untracked记录；HEAD 225c589495c3f73d706752aaf31e92c0cd5ef3c1；feature/asset-library-import-foundation-hardening。
- D:/AI AGENT/worktrees/asset-library-v1：0 条dirty/untracked记录；HEAD 3da45d53e4628a743a2809f908cbdfd60d706c43；feature/asset-library-v1。
- D:/AI AGENT/worktrees/migration-asset-library-20260911：0 条dirty/untracked记录；HEAD 7fd63b5064db774aa3a8f3ce5a24f676f1592993；migration/asset-library-work-20260911。
- D:/AI AGENT/worktrees/modular-harness-v1：19 条dirty/untracked记录；HEAD ea075b23603d01a393f29621fdeaa39adb627a6a；feature/asset-library-eagle-shell-context-p36。
- D:/AI AGENT/worktrees/online-selection-v1：279 条dirty/untracked记录；HEAD 393c1a545bb3d504ac1d3721576dc96f0c220f2f；feature/online-selection-v1。
- D:/AI AGENT/worktrees/pixel-tart-developer-preview-rc6：0 条dirty/untracked记录；HEAD 6a27e166af5c77f8455f44089aa9de5d38e86495；integration/pixel-tart-developer-preview。

第十批dirty为验收文档/图像；不收进本次文档提交。sqlite_gate有7个测试文件修改；modular-harness有生产源码/测试修改；online-selection有大量未跟踪发布文件。生产/测试补丁另存DIRTY_SOURCE_EVIDENCE，不把用户未提交实现当成已发布功能。未上传个人照片、数据库、凭据或二进制。

## 3. 跨分支未集成

### refs/remotes/source-private/feature/asset-library-import-foundation-hardening
SHA 225c589495c3f73d706752aaf31e92c0cd5ef3c1；merge-base 73530b9cbe2510e5e1266af8115a4e50eb4bc000。不是第十批祖先。其独有改动路径：

- M src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml
- M src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs
- M src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs
- M src/PixelTart.Modules.AssetLibrary/AsyncThumbnail.cs
- M src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetFormatCapabilityRegistry.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContainerService.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibrarySchema.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetTechnicalMetadata.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs
- M src/RAWSelectionAssistant.Core/Services/Tasks/TaskOperationBridge.cs

### refs/heads/feature/asset-library-v1
SHA 3da45d53e4628a743a2809f908cbdfd60d706c43；merge-base 4dac5f8e4460b7a67309646b6133bd186c121fea。不是第十批祖先。其独有改动路径：

- A src/PixelTart.AssetLibrary.Preview/App.xaml
- A src/PixelTart.AssetLibrary.Preview/App.xaml.cs
- A src/PixelTart.AssetLibrary.Preview/AssetLibraryPreviewViewModel.cs
- A src/PixelTart.AssetLibrary.Preview/AssetThumbnailConverter.cs
- A src/PixelTart.AssetLibrary.Preview/AsyncCommand.cs
- A src/PixelTart.AssetLibrary.Preview/AsyncThumbnail.cs
- A src/PixelTart.AssetLibrary.Preview/ClassifierWindow.cs
- A src/PixelTart.AssetLibrary.Preview/HistogramDrawing.cs
- A src/PixelTart.AssetLibrary.Preview/MainWindow.xaml
- A src/PixelTart.AssetLibrary.Preview/MainWindow.xaml.cs
- A src/PixelTart.AssetLibrary.Preview/PixelTart.AssetLibrary.Preview.csproj
- A src/PixelTart.AssetLibrary.Preview/PreviewImportDiagnostics.cs
- A src/PixelTart.AssetLibrary.Preview/TagManagerWindow.cs
- A src/PixelTart.AssetLibrary.Preview/ThumbnailSize.cs
- A src/PixelTart.AssetLibrary.Preview/VirtualizingWrapPanel.cs
- A src/PixelTart.AssetLibrary.Preview/WpfVisualAnalysisDecoder.cs
- A src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs
- A src/RAWSelectionAssistant.Core/Models/AssetLibraryV15Models.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibrarySchema.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.V15.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisBatchProcessor.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisEngine.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisModels.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisPersistence.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAssetFeatures.cs
- A src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAssetQueryService.cs
- A src/RAWSelectionAssistant.Core/Services/AssetSelection/AssetLibrarySelectionSource.cs
- A src/RAWSelectionAssistant.Core/Services/AssetSelection/AssetSelectionContracts.cs

### refs/heads/feature/online-selection-v1
SHA 393c1a545bb3d504ac1d3721576dc96f0c220f2f；merge-base 4dac5f8e4460b7a67309646b6133bd186c121fea。不是第十批祖先。其独有改动路径：

- A src/PixelTart.OnlineSelection.LocalDevPreview/App.xaml
- A src/PixelTart.OnlineSelection.LocalDevPreview/App.xaml.cs
- A src/PixelTart.OnlineSelection.LocalDevPreview/LocalDevOnlineSelectionProvider.cs
- A src/PixelTart.OnlineSelection.LocalDevPreview/LocalDevPreviewDialogService.cs
- A src/PixelTart.OnlineSelection.LocalDevPreview/PixelTart.OnlineSelection.LocalDevPreview.csproj
- A src/PixelTart.OnlineSelection.LocalDevPreview/PreviewWindow.xaml
- A src/PixelTart.OnlineSelection.LocalDevPreview/PreviewWindow.xaml.cs
- M src/PixelTart.SelectionApi.Contracts/SelectionApiContracts.cs
- A src/PixelTart.SelectionApi.Server/LocalDevJpegVariantService.cs
- A src/PixelTart.SelectionApi.Server/LocalDevSelectionRepository.cs
- A src/PixelTart.SelectionApi.Server/PixelTart.SelectionApi.Server.csproj
- A src/PixelTart.SelectionApi.Server/Program.cs
- A src/PixelTart.SelectionApi.Server/SelectionApiServerContract.cs
- A src/PixelTart.SelectionApi.Server/SelectionObjectStorage.cs
- M src/PixelTart.SelectionApi.Skeleton/Program.cs
- M src/RAWSelectionAssistant.Core/Models/OnlineSelectionModels.cs
- A src/RAWSelectionAssistant.Core/Services/AssetSelection/AssetSelectionContracts.cs
- M src/RAWSelectionAssistant.Core/Services/OnlineSelection/OnlineSelectionContracts.cs
- A src/RAWSelectionAssistant.Core/Services/OnlineSelection/SelectionClientChoiceMock.cs
- A src/RAWSelectionAssistant.Core/Services/OnlineSelection/SelectionResultExportService.cs
- M src/RAWSelectionAssistant/App.xaml
- M src/RAWSelectionAssistant/RAWSelectionAssistant.csproj
- A src/RAWSelectionAssistant/Resources/DesignSystem/Elevation.xaml
- A src/RAWSelectionAssistant/Resources/DesignSystem/PixelTart.Components.xaml
- A src/RAWSelectionAssistant/Resources/DesignSystem/PixelTart.Theme.xaml
- M src/RAWSelectionAssistant/ViewModels/OnlineSelectionViewModels.cs
- M src/RAWSelectionAssistant/Views/OnlineSelectionView.xaml
- M src/RAWSelectionAssistant/Views/TetherCaptureView.xaml
- M src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs

### refs/remotes/source-private/migration/asset-library-work-20260911
SHA 7fd63b5064db774aa3a8f3ce5a24f676f1592993；merge-base 0566f8ef04664649053bc6cfa043dbdfc6c26802。不是第十批祖先。其独有改动路径：

- M src/PixelTart.Modules.AssetLibrary/AssetLibraryP3Styles.xaml
- M src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs
- M src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml
- A src/PixelTart.Modules.AssetLibrary/AssetLibraryThumbnail.xaml
- M src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs
- M src/RAWSelectionAssistant.Core/Models/CollageModels.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContainerService.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.P3.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.V15.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisPersistence.cs
- M src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAssetQueryService.cs
- M src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentService.cs
- M src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs
- M src/RAWSelectionAssistant.Core/Services/Business/BusinessServices.cs
- M src/RAWSelectionAssistant.Core/Services/Database/CalendarWorkflowSchemaMigration.cs
- M src/RAWSelectionAssistant.Core/Services/Database/SqliteBookingDocumentRepository.cs
- M src/RAWSelectionAssistant.Core/Services/Database/SqliteBusinessRepositories.cs
- M src/RAWSelectionAssistant.Core/Services/Database/SqliteShootBookingRepository.cs
- M src/RAWSelectionAssistant.Core/Services/OrganizeService.cs
- M src/RAWSelectionAssistant.Core/Services/RawToJpeg/RawToJpegSafeConversionService.cs
- M src/RAWSelectionAssistant.Core/Services/Tethering/WatchFolderCameraAdapter.cs
- M src/RAWSelectionAssistant/App.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Buttons.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Cards.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Colors.Dark.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Colors.HighContrast.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Colors.Light.xaml
- A src/RAWSelectionAssistant/Resources/DesignSystem/Components.Foundation.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/ContextMenu.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Buttons.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Cards.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/DesignTokens.xaml
- A src/RAWSelectionAssistant/Resources/DesignSystem/Elevation.xaml
- A src/RAWSelectionAssistant/Resources/DesignSystem/PixelTart.Components.xaml
- A src/RAWSelectionAssistant/Resources/DesignSystem/PixelTart.Theme.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Spacing.xaml
- M src/RAWSelectionAssistant/Resources/DesignSystem/Typography.xaml

### refs/remotes/source-private/migration/pixel-tart-handoff-20260911
SHA f27a9bf72169ca4e51ff9953e38ae2209d8ba63b；merge-base 4dac5f8e4460b7a67309646b6133bd186c121fea。不是第十批祖先。其独有改动路径：

- A src/PixelTart.OnlineSelection.LocalDevPreview/App.xaml
- A src/PixelTart.OnlineSelection.LocalDevPreview/App.xaml.cs
- A src/PixelTart.OnlineSelection.LocalDevPreview/LocalDevOnlineSelectionProvider.cs
- A src/PixelTart.OnlineSelection.LocalDevPreview/LocalDevPreviewDialogService.cs
- A src/PixelTart.OnlineSelection.LocalDevPreview/PixelTart.OnlineSelection.LocalDevPreview.csproj
- A src/PixelTart.OnlineSelection.LocalDevPreview/PreviewWindow.xaml
- A src/PixelTart.OnlineSelection.LocalDevPreview/PreviewWindow.xaml.cs
- M src/PixelTart.SelectionApi.Contracts/SelectionApiContracts.cs
- A src/PixelTart.SelectionApi.Server/LocalDevJpegVariantService.cs
- A src/PixelTart.SelectionApi.Server/LocalDevSelectionRepository.cs
- A src/PixelTart.SelectionApi.Server/PixelTart.SelectionApi.Server.csproj
- A src/PixelTart.SelectionApi.Server/Program.cs
- A src/PixelTart.SelectionApi.Server/SelectionApiServerContract.cs
- A src/PixelTart.SelectionApi.Server/SelectionObjectStorage.cs
- M src/PixelTart.SelectionApi.Skeleton/Program.cs
- M src/RAWSelectionAssistant.Core/Models/OnlineSelectionModels.cs
- A src/RAWSelectionAssistant.Core/Services/AssetSelection/AssetSelectionContracts.cs
- M src/RAWSelectionAssistant.Core/Services/OnlineSelection/OnlineSelectionContracts.cs
- A src/RAWSelectionAssistant.Core/Services/OnlineSelection/SelectionClientChoiceMock.cs
- A src/RAWSelectionAssistant.Core/Services/OnlineSelection/SelectionResultExportService.cs
- M src/RAWSelectionAssistant/App.xaml
- M src/RAWSelectionAssistant/RAWSelectionAssistant.csproj
- A src/RAWSelectionAssistant/Resources/DesignSystem/Elevation.xaml
- A src/RAWSelectionAssistant/Resources/DesignSystem/PixelTart.Components.xaml
- A src/RAWSelectionAssistant/Resources/DesignSystem/PixelTart.Theme.xaml
- M src/RAWSelectionAssistant/ViewModels/OnlineSelectionViewModels.cs
- M src/RAWSelectionAssistant/Views/OnlineSelectionView.xaml
- M src/RAWSelectionAssistant/Views/TetherCaptureView.xaml
- M src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs

BRANCH_DIFFERENCES为从各自共同祖先到旁支的源码补丁；不能把“与第十批不同”都当新增功能，也不能把commit非祖先直接等同语义从未移植。资产预览独立项目/online-selection本地服务/AL-F01技术元数据是明确需单列调查项。已存在同路径但内容不同的旁支仍需逐语义合并评审，不能只看缺失路径。

第九→第十批仅9个生产源码路径变化，加3个测试文件。重点是LegacyRawInstantInput、InstantInputRecipe、ColorStudioRenderPipeline、ProfessionalRawPipeline、RawMatchTiff16ProductPipeline、HighBitDepthImageBuffer、ProfessionalRawExport及两个VM分部；完整路径与补丁见BATCH_SOURCE_DIFFERENCES。

## 4. 按真实清单统计

- 功能登记 255：其中242项用户范围分解，13项通用/待归属入口桶。不是255项全部完成。
- 页面/表面 82：含弹窗、嵌入组件、规划端及旁支页面，不等于独立导航页。
- 动作/候选/目标 2288：包含控件值、快捷键、内部事件、未绑定Command、设置字段、旁支入口与规划动作，不等于独立按钮。
- 源码候选 2184；目标补位动作 104。
- 功能状态：仅规划 43；占位 1；实现但尚未接入 5；已实现且已接入 121；已弃用 4；跨分支未集成 2；部分实现 79。
- 动作分类：BRANCH_ENTRY 59；CODE_EVENT 106；CODE_UI 156；CONTROL_VALUE 416；DATA_DRIVEN_PARAMETER 75；DYNAMIC_MENU 18；DYNAMIC_OPTION 68；KEYBOARD 178；SETTING_WITHOUT_UI 63；TEMPLATE_COMMAND 1；UNBOUND_COMMAND 77；UNRESOLVED_CONTROL 57；XAML_COMMAND 694；XAML_EVENT 216。
- 993条仍属于通用/待归属交互，89条有本次针对性静态语义复核标记；其他条目不能当逐项人工签核。

无编造覆盖率。ID外键完整只说明清单结构完整，不证明动态运行路径全部可达。

## 5. 未实现/未接入与部分能力重点

1. 手机APP、浏览器扩展、浏览器日历与三端端到端同步：未交付/仅规划。
2. 同步契约已有内存实现，但重复/冲突处理不能直接用于生产；高风险冲突返回Applied=true需明确阻断策略。
3. 在线选片默认None提供方；旁支本地API/导出未集成。真实公网身份、权限、隔离与回收须独立验收。
4. 当前有基础原生文档编辑器，不是完整WPS Word。复杂分节/页眉页脚/修订协作/高级表格/高保真DOCX往返等独立目标。
5. 当前BasicTone已有“HDR 影调”控件，不是HDR合成/显示/输出。完整ICC软打样与跨设备一致性是目标。
6. 第十批旧RAW当前编辑输入已在功能分支推送，未合入旧集成分支；第九批限制不能被改写成已经完成。
7. 联机拍摄生产路径为看守文件夹；厂商SDK快门/曝光参数不因接口存在而完成。
8. 项目归档/恢复未找到明确项目域入口；不能挪用档期或编辑版本归档。保持目标，不新增永久删除。
9. 收支导入/归档、全局搜索、完整自动更新、画布文档对象/通用导出需继续核对/规划。
10. 旧工程术语/旧品牌/冲突对话框JSON直出仍存在；设计改为用户语言，但本次没有改代码。

## 6. 覆盖核对与风险

本次扫描677份生产/原型文本文件和361份测试源码，读取所有相关引用树/旁支补丁，独有旁支源码记录49条（含不同SHA同路径）。测试方法索引不是测试运行结果。生产扫描包含验收辅助类，候选提取会尽量排除Automated/Acceptance/Diagnostics，但仍需编译条件复核。

词法规则覆盖XAML Command、事件、值绑定、模板Command、代码按钮/菜单、动态选项、键盘Key、未绑定Command、未暴露设置字段以及数据驱动调色目录。局限：
- 不是Roslyn完整语义绑定；同名Command/DataContext可能歧义。
- 反射、运行时服务注入、工具工厂、资源模板和事件转发可能遗漏或重复。
- Key引用与事件订阅不必然是独立用户动作；保留为候选，不机械画按钮。
- 旁支同路径变更尚未全部重新生成独立动作语义；完整补丁保留。
- 多个私有/公开方法的存在未被当可见入口；未解析者明确标注。
- 状态矩阵为设计要求，不是现有状态全实现证明；有些反馈仍为规范模板，需要产品逐行收口。

因此本交付可用于全范围设计启动与补审排期，**尚不符合“所有按钮均逐项核验完毕的最终权威规格”冻结条件**。不能把PARTIAL改成READY。

## 7. 测试与桌面验收

本任务未build、未运行产品测试、未启动EXE、未做桌面/相机/手机/浏览器验收。第九批DELIVERY、MANUAL_ACCEPTANCE中已有历史测试与NOT_RUN记录，只作为原SHA历史证据；未重写旧报告，不重新宣称通过。文档校验另见VALIDATION_REPORT，不与产品测试混淆。

## 8. 结束时并发更新（优先于开始快照）

审计期间另一任务正常提交并推送第十批。最终HEAD为7184966a4921215a1a306019578f23646bcf872a；git ls-remote origin已核实一致，工作树clean。061c4cf→7184966只新增第十批证据文档/合成关键帧，src/prototypes/tests无差异，因此本清单SourceSHA继续使用061c4cf精确绑定相同生产源码。四份证据文件指纹在审计间变化来自并发任务；本任务未写原工作树，不声称它们字节未变。

新增DELIVERY历史报告声明真实Canon路径通过、Sony解码失败、Fuji预算拒绝、真实JPEG输入NOT_RUN、桌面/物理DPI未批准；这些是第十批报告原文事实，不是本任务重跑或认可全产品READY。

## 9. 具体入口扫描残余风险

XAML事件扫描采用显式允许列表，并非WPF所有路由事件字典。例如部分MouseLeftButtonDown/MouseLeftButtonUp、触控、拖拽完成等事件可能只存在于RawBinding而未逐事件独立建立Action；代码属性SetBinding、CommandBinding、事件重写和工厂返回控件也需语义补审。此项阻止宣称“所有用户可触发入口100%覆盖”。ENTRY_COVERAGE只报告被实际枚举的候选，不伪造完整覆盖率。
