# Pixel Tart P3 通用筛选、智能文件夹与标签管理实施报告

日期：2026-09-03  
结论：**BLOCKED（产品候选已实现，正式自动验收闭环未关闭）**

起点分支：`source-private/feature/asset-library-eagle-parity-p2`  
起点完整 SHA：`c5fde036e13abd2039d517f2a4022e9a32452c2f`  
P3 分支：`feature/asset-library-eagle-parity-p3-query-metadata`  
当前验收候选代码 HEAD：`9f14abe1cb76507bfa4605b39db0cd9fe2222d32`  
交付文档提交后的最终 SHA：由包含本报告的 Git 提交决定，以最终 `git ls-remote` 核验和交付回传为准；Git 提交无法在自身正文中自含自己的哈希。

> `BLOCKED` 同时包含两项事实：附件要求的“同一最终 HEAD 三轮独立正式 Run + 每轮只读 validator + run-set 聚合”尚未产生，当前计数为 `0/3`；历史执行已超过最多五轮自动修复循环的绝对上限。它不否定已经提交并通过本地测试的产品候选，也绝不把失败轮次的局部证据拼接成成功闭环。

## 1. 结论与 P3 边界

| P3 目标 | 产品候选状态 | 正式闭环状态 |
|---|---|---|
| 通用 Query Composer | 已实现：当前/全库范围、IME/debounce/cancellation、建议与历史、嵌套 AND/OR、锁定/清除、保存为智能文件夹、参数化 query plan | 前 11 个正式场景曾在失败轮中重复执行到达；最终候选 HEAD 尚无完整正式 Run，不能单独宣称验收关闭 |
| 智能文件夹通用规则编辑器 | 已实现：任意嵌套规则组、实时预览、保存/编辑/复制/归档/恢复、失效引用 fail closed、v6→v7 迁移 | 同上；正式总门仍为 `0/3` |
| 标签管理器与批量元数据编辑 | 已实现：标签/组管理、移动/排序、归档/恢复、预览后合并、批量 metadata、统一 v2 journal、跨重启 undo/redo | 最近五个失败根呈现了第 12 场景的小视口布局和完成信号根因链；最后一个失败根后的响应式修复已通过回归，识别绝对五轮上限已超后没有启动第 18 个根 |

未混入 Viewer、媒体播放、导入/导出、Eagle Adapter、多素材库、逻辑回收站、永久删除、AI/MCP、反向图片搜索或 P4 产品代码。默认导入策略仍为 `Reference`，P3 路径只写产品私有 metadata、规则、标签、workspace settings 和 journal。

## 2. 基线与现状审计

执行前后重新 fetch 并核验：远端 P2 仍为 `c5fde036e13abd2039d517f2a4022e9a32452c2f`，且是当前 P3 HEAD 的祖先；分支不是从 `main`、P1 或截图中的短 SHA 建立。

| 审计项 | 发现与决策 |
|---|---|
| P0/P1/P2 契约 | 读取 F-001～F-083 原表、P1 自动验收关闭报告、P2 实施报告和 P2 自动验收契约；P1/P2 证据目录不被 P3 覆盖。 |
| 统一查询 | P2 `AssetLibraryQuery`、稳定排序、分页游标和四视图选择状态继续保留；P3 新增唯一的版本化 `AssetQueryDocument`，UI 不拼 SQL。 |
| 智能文件夹 | P2 平面规则无法无损表达任意嵌套和稳定 canonical hash，因此选择素材私有 DB schema v7，而不是把隐含语义继续塞入 v6 平面列。 |
| 标签与命令 | 复用 P2 repository、`AssetLibraryBrowserCommandService` 和 durable journal；不建立第二套 undo/redo。 |
| 四视图一致性 | grid/waterfall/justified/list 继续共享 query、排序、分页和 `selectedAssetIds`；P3 composer 只改变同一查询文档。 |
| 暗色样式 | 新建 P3 局部样式字典并扫描交互控件，覆盖普通、悬停、按下、选中、焦点、禁用和错误状态；没有依赖 Windows 默认白色控件。 |
| 数据迁移 | v6→v7 前生成完整 SQLite 备份，迁移在事务内执行；未知未来版本、损坏 JSON、hash 不一致或旧引用无法解析均 fail closed。 |
| 自动验收 | 新建独立 P3 runner/validator/run-set；PowerShell 5.1 兼容，使用产品公开 seam、WPF Dispatcher 和 run-owned SQLite，不使用桌面输入或 UIA Invoke。 |

## 3. 架构与修改文件

相对 P2 基线共修改/新增 65 个受控文件（约 22,412 行新增、166 行删除）。职责按边界如下。

实现归属必须如实说明：schema v7、Core、UI、测试和初版 P3 自动验收包被合并在首个 `e6e8f4a...` 大提交中，没有按附件的“建议”拆成多个功能提交；其后 20 个提交均为正式验收发现的定点修复或测试加固。报告不把现有历史反写成并不存在的提交结构。

| 组件 | 文件 | 职责 |
|---|---|---|
| Canonical query 模型 | `src/RAWSelectionAssistant.Core/Models/AssetQueryModels.cs`、`AssetLibraryModels.cs`、`AssetLibraryWorkspaceSettings.cs` | 版本化 AST、规范化/校验/hash、scope、建议/历史、workspace 恢复 allowlist |
| Schema 与 repository | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibrarySchema.cs`、`SqliteAssetLibraryRepository.cs`、`SqliteAssetLibraryRepository.V15.cs`、`SqliteAssetLibraryRepository.P3.cs`、`AssetLibraryContracts.cs` | v7 迁移/备份、参数化查询编译、引用完整性、智能文件夹、标签管理、批量事务和 durable journal |
| 兼容与引用 | `AssetQueryReferenceIntegrity.cs`、`LegacySmartFolderAdapter.cs`、`VisualAnalysis/VisualAssetQueryService.cs` | 旧规则迁移、folder/tag 稳定 ID 引用、视觉查询统一编译与 fail closed |
| Query Composer UI | `AssetLibraryViewModel.P3QueryComposer.cs`、`AssetQueryComposerView.xaml(.cs)`、`P3QueryNodeView.cs`、`P3QueryReferenceConverters.cs` | 当前/全库、输入防抖/取消、建议/历史、规则树/chip/锁定/清除、保存当前筛选 |
| 智能文件夹 UI | `AssetLibraryViewModel.P3SmartFolder.cs`、`AssetSmartFolderEditorView.xaml(.cs)`、`AssetLibraryViewModel.SmartFolderEditor.cs` | 新建/编辑/复制/归档、嵌套规则、异步实时预览、取消和重试 |
| 标签/批量 UI | `AssetLibraryViewModel.P3TagManager.cs`、`AssetTagManagerView.xaml(.cs)`、`AssetLibraryBrowserCommandService.cs` | 标签/组 CRUD、移动排序、合并预览、批量 metadata 预演/提交、稳定完成 generation、同一 journal undo/redo、小视口响应式滚动 |
| 页面与主题 | `AssetLibraryPage.xaml(.cs)`、`AssetLibraryP3Styles.xaml`、`AssetLibraryViewModel.cs`、`AssetLibraryViewModel.P2Browser.cs`、模块 `.csproj` | 页面集成、共享状态刷新、暗色/焦点/可访问样式和布局边界 |
| 产品自动验收 seam | `AssetLibraryP3AutomatedAcceptanceDriver.cs`、`MainWindow.AssetLibraryP3AutomatedAcceptance.cs`、`AssetLibraryP3AutomatedAcceptanceController.cs`、`App.xaml.cs`、应用 `.csproj` | 受编译门保护的真实产品路径、14 场景/17 会话驱动、证据与进程身份 |
| Core 测试 | `tests/RAWSelectionAssistant.Tests/AssetLibraryP3*.cs`、`AssetLibraryP2CoreTests.cs`、`AssetLibraryV16Tests.cs` | AST/真值表/SQL/迁移/引用/标签/journal/10k 性能及 P2 回归 |
| WPF/契约测试 | `tests/RAWSelectionAssistant.WpfTests/AssetLibraryP3*.cs`、`AssetLibrarySmartFolderEditorWpfTests.cs`、P1/P2 回归测试文件 | Composer、规则树、标签三态、布局、暗色、可访问性、sealed run、negative fixtures、run-set fail closed |
| P3 自动验收包 | `tools/AssetLibraryP3AutomatedAcceptance/*` | `DryRun`、`RecoveryTest`、`Run`、`ValidateExistingRun`、fixture、validator、三轮聚合器和 v1 契约 |
| P1 兼容修正 | `tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1` | 适配素材 schema v7，保持 P1 只读验收合同 |

完整 65 文件清单（大括号仅压缩共同目录，不省略文件）：

- Core 模型/契约/状态（4）：`src/RAWSelectionAssistant.Core/Models/{AssetLibraryModels.cs,AssetLibraryWorkspaceSettings.cs,AssetQueryModels.cs}`；`src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs`。
- Core schema/存储/计划（7）：`src/RAWSelectionAssistant.Core/Services/AssetLibrary/{AssetLibrarySchema.cs,AssetQueryReferenceIntegrity.cs,LegacySmartFolderAdapter.cs,SqliteAssetLibraryRepository.P3.cs,SqliteAssetLibraryRepository.V15.cs,SqliteAssetLibraryRepository.cs,VisualAnalysis/VisualAssetQueryService.cs}`。
- 产品模块 UI/行为（18）：`src/PixelTart.Modules.AssetLibrary/{AssetLibraryBrowserCommandService.cs,AssetLibraryP3Styles.xaml,AssetLibraryPage.cs,AssetLibraryPage.xaml,AssetLibraryViewModel.P2Browser.cs,AssetLibraryViewModel.P3QueryComposer.cs,AssetLibraryViewModel.P3SmartFolder.cs,AssetLibraryViewModel.P3TagManager.cs,AssetLibraryViewModel.SmartFolderEditor.cs,AssetLibraryViewModel.cs,AssetQueryComposerView.xaml,AssetQueryComposerView.xaml.cs,AssetSmartFolderEditorView.xaml,AssetSmartFolderEditorView.xaml.cs,AssetTagManagerView.xaml,AssetTagManagerView.xaml.cs,P3QueryNodeView.cs,P3QueryReferenceConverters.cs}`。
- 验收 runtime/构建隔离（6）：`src/PixelTart.Modules.AssetLibrary/{AssetLibraryP3AutomatedAcceptanceDriver.cs,PixelTart.Modules.AssetLibrary.csproj}`；`src/RAWSelectionAssistant/{App.xaml.cs,MainWindow.AssetLibraryP3AutomatedAcceptance.cs,RAWSelectionAssistant.csproj,Services/AssetLibraryP3AutomatedAcceptanceController.cs}`。
- Core tests（12）：`tests/RAWSelectionAssistant.Tests/{AssetLibraryP2CoreTests.cs,AssetLibraryP3ArchivedRelationshipSemanticsTests.cs,AssetLibraryP3IntegrityTests.cs,AssetLibraryP3MigrationBackupTests.cs,AssetLibraryP3MigrationTests.cs,AssetLibraryP3PerformanceTests.cs,AssetLibraryP3QueryContractHardeningTests.cs,AssetLibraryP3QueryDocumentTests.cs,AssetLibraryP3QueryPlanTests.cs,AssetLibraryP3QuerySemanticSafetyTests.cs,AssetLibraryP3RepositoryTests.cs,AssetLibraryV16Tests.cs}`。
- WPF/验收契约 tests（10）：`tests/RAWSelectionAssistant.WpfTests/{AssetLibraryP1AutomatedEvidenceContractTests.cs,AssetLibraryP1LoadStateAcceptanceTests.cs,AssetLibraryP2DragDropHardeningTests.cs,AssetLibraryP3AccessibilityTests.cs,AssetLibraryP3AutomatedAcceptanceSeamTests.cs,AssetLibraryP3AutomatedEvidenceContractTests.cs,AssetLibraryP3AutomatedRunSetContractTests.cs,AssetLibraryP3RunSealContractTests.cs,AssetLibraryP3WpfTests.cs,AssetLibrarySmartFolderEditorWpfTests.cs}`。
- 工具/contract（8）：`tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1`；`tools/AssetLibraryP3AutomatedAcceptance/{Invoke-P3AssetLibraryAutomatedAcceptance.ps1,Invoke-P3NegativeEvidenceProofs.py,New-P3SyntheticFixture.py,README.md,Test-P3AssetLibraryAutomatedEvidence.ps1,Test-P3AssetLibraryAutomatedRunSet.ps1,automated-acceptance-contract.json}`。

## 4. 单一 Query AST 与范围语义

### 4.1 文档结构与 canonical 规则

`AssetQueryDocument` 当前版本为 1，包含 `Version`、`Scope`、`Text`、可选 `SearchClauses`、`RootGroup`、`SortField`、`SortDirection` 和 `IncludeArchived`。节点只有两种：

- `Group`：`Logic=All|Any`、`Negated`、`Enabled`、`Children`。
- `Rule`：`Field`、`Operator`、`Values`、`CaseSensitivity`、`Negated`、`Enabled`、`Locked`。

规范化会做 Unicode NFC、空白整理、集合值去重与稳定排序、InvariantCulture 数字/日期/颜色编码和无效空规则清理。canonical JSON 有意保留编辑器中的规则顺序；语义 SHA-256 才会对可交换的组 children 和 `SearchClauses` 稳定排序，因此等价表达式 hash 相同而编辑顺序仍可恢复。限制为：每段文本不超过 500 字符、搜索段不超过 16、树深不超过 8、总节点不超过 256、单组 children 不超过 64、单规则 values 不超过 128。未知文档版本、成员、字段、操作符、非法数值域、坏 JSON、坏 hash、未来 schema、悬空/归档 folder/tag 引用全部 fail closed；不会退化为“全部素材”。SQL 和 `EXPLAIN QUERY PLAN` 均使用参数，排序始终带 `AssetId` 兜底。

### 4.2 Current 与 AllAssets

| Scope | 语义 |
|---|---|
| `Current` | 在当前系统集合、文件夹、标签或已选智能文件夹的候选集合上继续 AND 搜索和规则；切换组织源时只清理未锁定条件，锁定条件保留。智能文件夹本身是完整保存结果，临时 Current 规则再作为 AND 层叠加。 |
| `AllAssets` | 清除当前组织源约束，从全库候选开始；默认仍是 active-only，只有 `IncludeArchived` 或显式归档规则才扩大候选。规则、文本、排序不会因 scope 切换丢失。 |

最后一个合法 scope、query、搜索历史和浏览状态写入版本化 workspace JSON；非法旧 scope 回退 `Current`。保存为智能文件夹时，Current 组织源被转换为稳定 ID 规则，已选智能文件夹被嵌入为完整 AST，不保存不稳定的智能文件夹递归引用。多个 `Text/SearchClauses` 分别对文件名、扩展名、Comment、活动标签名和活动文件夹名搜索，各段之间为 AND；反斜线、`%`、`_` 均按 LIKE 字面量转义。

### 4.3 字段 × 操作符完整矩阵

| 字段组（完整字段） | 支持操作符 |
|---|---|
| 关系集合：`Folder`、`Tag` | `AnyOf`、`AllOf`、`NoneOf` |
| 布尔状态：`IsUncategorized`、`IsUntagged`、`IsMissing`、`IsArchived` | `IsTrue`、`IsFalse` |
| 数字：`Rating`、`FileSize`、`Width`、`Height`、`LongEdge`、`ShortEdge`、`PixelCount`、`AspectRatio`、`VisualDominantHue`、`VisualAverageLuma`、`VisualAverageSaturation`、`VisualLumaSpread`、`VisualShadowRatio`、`VisualHighlightRatio`、`VisualBlackClipRatio`、`VisualWhiteClipRatio` | `Equals`、`NotEquals`、`GreaterThan`、`GreaterThanOrEqual`、`LessThan`、`LessThanOrEqual`、`Between`、`Unknown`、`Known` |
| 日期：`AddedAt`、`CaptureTime` | 同数字组；日期值 canonical 为固定 ISO-8601 |
| 精确颜色：`VisualDominantColor` | `Equals`、`NotEquals` |
| 文本/枚举：`FileName`、`Extension`、`MediaType`、`Comment`、`Orientation`、`VisualAnalysisStatus`、`VisualHarmony`、`VisualToneKey`、`VisualContrast`、`VisualSaturation`、`VisualWarmCool` | `Contains`、`NotContains`、`Equals`、`NotEquals`、`StartsWith`、`EndsWith`、`IsEmpty`、`IsNotEmpty`、`AnyOf`、`NoneOf`、`Unknown`、`Known` |
| 显式 regex 子集：`FileName`、`Comment` | 在上一行基础上额外支持 `Regex`；普通搜索不是 regex |

仅 `FileName`、`Extension`、`Comment` 暴露大小写敏感选项。可靠枚举值包括 media type、横/竖/方向、视觉分析状态、配色和影调分类；视频/音频时长、区域标注、链接和 AI 语义没有可靠字段，因此没有伪造入口。

权威矩阵总计 36 fields × 21 operators = 756 个组合，其中 312 个支持、444 个明确拒绝；UI 直接调用 Core 的同一矩阵。数值和日期 `Between` 为闭区间且要求下限不大于上限；Hue 特许 350°→10° 这种跨零区间。视觉规则只接受当前分析版本、成功结果且 source content hash 与素材 hash 一致的特征，缺失/过期特征为 SQL NULL，否定规则也不会误纳。

## 5. 智能文件夹与 schema v7

主程序工作流数据库 **Schema 5 含义不变**。只把独立的素材私有 SQLite 从 v6 升到 **v7**。素材文件名仍为 `asset-library-v16.db`；其中 `v16` 是历史文件命名，不代表 schema 版本。

### 5.1 Schema diff

- `AssetLibrarySchema.Version`：6 → 7。
- 新增 `SmartFolderQueryDocuments`：`SmartFolderId` 主键/外键、`DocumentVersion`、canonical `QueryJson`、`QueryHash`、`LegacyRulesBackupJson`、`UpdatedAt`。
- `SmartFolderRules.GroupId/GroupLogic` 在 v6 已存在，不是本次新增 DDL。v7 继续保留全部 v6 原表和原始规则，不原地删除旧规则；新表主键已覆盖按 `SmartFolderId` 的访问，不额外盲目加索引。

### 5.2 迁移、备份与回滚限制

1. 初始化先拒绝高于 v7 的未来版本。
2. 检测到 v6 时，使用 SQLite backup API 生成 `<database>.schema-v6-backup.sqlite`；若已存在则使用带 UTC 时间和 GUID 的新文件名。
3. 备份先写 `.partial-*`，通过 `PRAGMA quick_check` 且版本确认为 6 后才原子移动为正式备份；失败只删除 partial，原库不迁移。
4. 新表创建、旧规则读取/转换、稳定 ID 引用解析、canonical JSON/hash 写入、全表校验和 v7 marker 均在一个事务中；任一步失败整体 rollback。
5. 再开 v7 只补缺失文档，迁移幂等；未知未来版本、损坏备份、坏 JSON/hash、无法解析的旧字段/操作符/引用均拒绝打开。

直接 schema/迁移测试共 11 项，覆盖成功迁移、原始 payload 备份、事务失败回滚、重复打开幂等、损坏备份、未来版本、名称→稳定 ID、缺失/歧义引用。v7 canonical 文档是唯一权威源；旧 API 只能通过 `LegacySmartFolderAdapter` 做受限无损投影，无法表达的多层/否定/锁定/大小写/scope/sort 语义会拒绝而不降级覆盖。代码回滚不能把已经迁移的 live v7 数据库“自动降级”为 v6；如需回退产品提交，必须先保留 live DB，再由经过校验的 `.schema-v6-backup*.sqlite` 恢复，禁止手工删行或改 schema marker。

### 5.3 编辑与预览

编辑器复用同一 AST，支持新建、名称/描述、任意层级 All/Any、受验证的否定、节点增删/移动、实时预览、保存/更新、复制稳定不冲突名称、归档/恢复和取消。预览使用 debounce、cancellation 与 generation guard；关闭或新请求后旧结果不能回写。无效规则禁用保存并定位错误；保存失败保留编辑内容；失效 folder/tag 引用不扩大为全库。P3 不允许智能文件夹引用另一个智能文件夹，因而没有循环引用入口。

## 6. 标签、合并、批量 metadata 与 journal

- 标签组和标签支持新建、重命名、移动、排序、搜索/过滤、归档/恢复；“删除”语义没有映射到硬删除。
- 名称统一做 trim/NFC/长度/空值/大小写唯一性校验；移动和排序拒绝循环、孤儿和重复目标。
- 合并必须先生成带影响素材数、重复 membership 和目标标签的 fingerprint 预览；提交时重新验证预览，事务内去重 membership、归档源标签并迁移智能文件夹稳定 ID 引用。
- 批量 metadata 支持添加/移除标签、设置/清除评分、加入/移出文件夹、设置归档/恢复、缺失状态和 Comment；0/1/多选展示空、共同或混合值，提交前显示影响数和冲突覆盖摘要。
- 100/500 项操作使用单事务；预览包含 request/state fingerprint，状态漂移会拒绝提交，部分失败整体 rollback。
- 所有新操作写入既有 durable v2 journal；`asset-batch-metadata-v2`、tag state/merge 等保存 before/after image，跨 repository 重启恢复 undo/redo。旧 v1 journal 只保留其原有撤销语义，不伪造 after image。
- 批量完成信号使用单调 generation、operation id、outcome 和 undo token，不再依赖会被异步刷新覆盖的状态文案；等价 selection ID 集合同步是幂等的。

## 7. UI、暗色与可访问性

P3 控件全部使用 `AssetLibraryP3Styles.xaml` 的局部暗色样式。XAML 扫描、对比度、AutomationId 唯一性、可访问名称、焦点可见性、键盘路径和真实 WPF 页面测试已通过。中文 UI 使用“向左方向键/向右方向键”等可理解文本，不把 `Left`/`Right` 暴露给用户。

最后一个已保存失败根（总第 17 个）后，标签管理器内部改为命名滚动视口 `P3TagManagerViewport`，高度绑定真实 `AssetLibraryPage.ActualHeight`：`clamp(pageHeight - 550, 60, 300)`。最终回归在 1366×768 对应的 660.67 DIP 和更小的 612.67 DIP 容器中，强制显示足量标签和“加载更多”行，检查三栏、滑块、全部非滚动按钮未越界且浏览工作区至少保留 130 DIP。独立只读审查确认绑定依赖外层页面高度，不形成测量循环。

## 8. 自动构建与测试（候选 HEAD `9f14abe...`）

本节数字来自 2026-09-03 当前 Codex 会话直接执行命令后的控制台结果，未另存为 TRX/QualityGate 日志，因此只能作为可复现的候选级会话观察，**不是** sealed 正式验收证据。命令分别对 `RAWSelectionAssistant.Tests.csproj`、`RAWSelectionAssistant.WpfTests.csproj`、`PixelTart.ModularHarness.Tests.csproj` 执行严格 `dotnet test`，P1/P2/P3 行是同一最终候选二进制上的名称过滤测试，不是重新验证 P1/P2 的历史 sealed run。

| 门项 | 结果 |
|---|---|
| Core 全量 | `PASS；1260/1260，0 failed，0 skipped` |
| WPF 全量 | `PASS；1105/1105，0 failed，0 skipped` |
| Modular Harness | `PASS；14/14，0 failed，0 skipped` |
| P1 automated acceptance 回归 | `PASS；60/60，0 failed，0 skipped` |
| P2 automated acceptance 回归 | `PASS；37/37，0 failed，0 skipped` |
| P3 Core 专项 | `PASS；62/62，0 failed，0 skipped` |
| P3 WPF/契约专项 | `PASS；56/56，0 failed，0 skipped` |
| 最终小视口定向布局 | `PASS；1/1`；另一次独立 P3 WPF/可访问复核 `29/29` |
| Debug solution build | `PASS；0 warnings / 0 errors` |
| Release solution build | `PASS；0 warnings / 0 errors` |
| Debug DevPreview + P3 acceptance build | `PASS；0 warnings / 0 errors` |
| P3 DryRun | `PASS；status=ready-for-automated-run；source_head=9f14abe1cb76507bfa4605b39db0cd9fe2222d32；devpreview_process_count=0` |
| P3 RecoveryTest | `PASS；status=recovery-test-passed；environment_restored=true；devpreview_process_count=0；desktop_input_injection=0；display_setting_writes=0` |
| `git diff --check` | `PASS` |
| 历史 DPI 证据债 | 沿用并如实保留 P2 记录：`101 total / 75 passed / 26 failed / 0 skipped`；26 项是既有截图 hash/历史文件缺失，本轮未删除、skip 或冒充修复 |

严格构建均使用 warnings-as-errors、禁用共享编译、禁用并行构建和 node reuse。没有删除、跳过或放宽旧测试。

### 8.1 候选级性能护栏（不是正式三轮最差值）

同一候选 HEAD 上的 10,000 项纯 synthetic metadata 定向测试通过；以下为本次测试进程的 worst-of-three/单事务测量。它只证明代码级阈值护栏，不替代正式 Run 的进程、UI 阻塞和三轮独立证据。

| 指标 | 候选测量 | 阈值 |
|---|---:|---:|
| 10,000 项默认首屏 | 9.0 ms | ≤ 1,500 ms |
| 普通文本建议 | 4.7 ms | ≤ 200 ms |
| 单条件筛选更新 | 19.5 ms | ≤ 300 ms |
| 8 条规则、3 层嵌套查询 | 27.3 ms | ≤ 600 ms |
| 智能文件夹预览 | 26.9 ms | ≤ 750 ms |
| 当前/全库范围切换 | 19.0 ms | ≤ 400 ms |
| 100 项批量标签 | 10.4 ms | ≤ 750 ms |
| 500 项批量标签 | 18.2 ms | ≤ 2,000 ms |
| 单次 UI 线程阻塞 | N/A（只能由完整正式 Run 给出） | ≤ 100 ms |

## 9. 正式 P3 自动验收与有界阻断

### 9.1 最终要求的三轮结果

| 正式轮次 | run root | run id | source HEAD/hash | 场景/截图 | validator |
|---:|---|---|---|---|---|
| 1 | N/A | N/A | N/A | N/A | N/A |
| 2 | N/A | N/A | N/A | N/A | N/A |
| 3 | N/A | N/A | N/A | N/A | N/A |

最终同 HEAD 完整 PASS：**0/3**。三轮性能最差值：**N/A**。run-set 聚合验证：**N/A**。不能用以下失败 run 的前 11 个局部场景、截图、数据库、hash 或安全计数填入这张表。

### 9.2 止损规则偏差与全部历史失败根

`.validation` 中共保留 **17** 个 P3 失败根。按附件 §13.2 的绝对口径，“运行 → 定位 → 修复 → 回归”最多五轮；该历史执行次数已经超过上限，不能把最后五个解释成可滑动的新额度。这是本次执行过程本身的一项合规偏差，也是必须保持 `BLOCKED` 并立即停止新 Run 的原因。当前没有删除或改写任何旧根，也没有启动第 18 个根。

前 12 个根使用共同前缀 `D:\AI AGENT\worktrees\modular-harness-v1\.validation\`，逐项如下：

| 序号 | 根目录 / run id | HEAD | 第一失败分类 |
|---:|---|---|---|
| 1 | `P3-Automated-Acceptance-20260902-171746-26fd747ba38f` / `p3-auto-e11f1922322f47068258dd5facb2a382` | `7936a21c395bc1e43730a100275fbf43efaf620d` | runner 基础设施：PowerShell 空集合 `.Sum` 属性错误 |
| 2 | `P3-Automated-Acceptance-20260902-171910-ddb535391749` / `p3-auto-eb9a9e52967a42e1ad585fedcc4abd59` | `89da32b85742714cd7ee31d909f33f969851e494` | fixture 基础设施：输入树清单受 locale 排序影响 |
| 3 | `P3-Automated-Acceptance-20260902-172218-8f30ea2524d1` / `p3-auto-415405fd2cb74eb396cb18bc155d050a` | `5a4ee8028d4e5f14ce7ee0248788ae00af5f21f3` | 产品场景：1366×768 P3 页面布局越界 |
| 4 | `P3-Automated-Acceptance-20260902-173059-a28d3b371005` / `p3-auto-6eb44116f95944198653ab2920effe3f` | `5a0cf5ccfa13684415ac895d8d718d0d0843b640` | 产品/fixture 契约：folder/tag 名称未解析为稳定 ID，query fail closed |
| 5 | `P3-Automated-Acceptance-20260902-174743-d7300a161913` / `p3-auto-d3e8e90ba8ec4c72b4b090be626e8f64` | `ad5c383838d00476c5debd5c455cd6106fb98b50` | 产品场景：智能文件夹预览/保存/取消 round-trip 不一致 |
| 6 | `P3-Automated-Acceptance-20260902-180606-f8f0b40190df` / `p3-auto-3fd185151bca4582a8c2b5199885f4b0` | `1b66157725017326bff49821e0bc345b595f9e63` | 产品场景：1366×768 布局越界 |
| 7 | `P3-Automated-Acceptance-20260902-181835-26e5451e2533` / `p3-auto-f7f669d5b7af4c59befe990ebdb4dc55` | `8ad3229b334e59021661c6158bf94ba5ed20b98a` | 产品场景：三栏及缩略图滑块纵向越界 |
| 8 | `P3-Automated-Acceptance-20260902-182352-df74ba552502` / `p3-auto-9ac9028ecd294803bd2b181bd1827b8a` | `e358ef0ec2361cb94224d9587bfc97feb3fcf3db` | 启动预检：临时布局诊断测试尚未移除，工作树非 clean |
| 9 | `P3-Automated-Acceptance-20260902-182544-b45a98ebccea` / `p3-auto-27a37f57236b448792b889ee18bdf7a4` | `e358ef0ec2361cb94224d9587bfc97feb3fcf3db` | 产品场景：未打标签入口及视觉筛选按钮底部裁剪 |
| 10 | `P3-Automated-Acceptance-20260902-182923-a56605506697` / `p3-auto-802d7ada86434f6a863745618928b64b` | `ce530a7676a7d1394485661937c86fa2742770d7` | 产品场景：缺失素材入口底部裁剪 |
| 11 | `P3-Automated-Acceptance-20260902-183249-77327015680f` / `p3-auto-a49d94eff13b42aa8e2f6c0c37c18fe3` | `52c8d77610caa85598cfe4d4d32161577a12f52a` | 产品场景：已归档入口底部裁剪 |
| 12 | `P3-Automated-Acceptance-20260902-183725-59bd5a1ca2e4` / `p3-auto-e176b9578b3f40d9a13d236e3906d1e1` | `1e4fcb83ae48a9332b730cbfa9a09a84586d8712` | 产品场景：等待公开 Batch Metadata Apply 命令超时 |

最后五个根用于呈现最终根因链，**不是**新增五轮额度。它们同样保持原样，没有补写、续跑或跨轮拼接。

| 轮次 | 完整 run root / run id | HEAD | 第一个失败与修复 |
|---:|---|---|---|
| 1 | `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P3-Automated-Acceptance-20260903-104213-15c07f0ac225` / `p3-auto-bdf679f80bf943d08c83cd06e24c3e5d` | `dfff1fd68affd81dfad4658d483dcc12481e4817` | 第 12 场景等待公开批量 metadata Apply 超时；等价选区的迟到同步覆盖成功摘要。由 `98472c4...` 增加独立完成 generation/outcome/operationId 与幂等选区，`8041394...` 隔离 SQLite pools。 |
| 2 | `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P3-Automated-Acceptance-20260903-120837-6e92b693bb5d` / `p3-auto-20eeb932cd0c491ea7bc1e3665e024ce` | `8041394bec785982012395422d81135c17d2dff7` | 1366×768 下标签管理器无高度上限，合并按钮、三栏与滑块越出页面；`e7e85b4...` 加入内部滚动视口。 |
| 3 | `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P3-Automated-Acceptance-20260903-125524-19de0485dfe3` / `p3-auto-4a397b126a8d4fce86a8d97a72d6d9e8` | `e7e85b43ae211c8c812f0c77ebb5505b6bb7a637` | 浏览排序和 Undo/Redo 按钮仍裁剪；`27230f1...` 收紧视口并扩展非滚动按钮回归。 |
| 4 | `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P3-Automated-Acceptance-20260903-130246-80096bcb2e40` / `p3-auto-548189b417784d7e854c97456a240ae7` | `27230f18bf2889128fcf8e7e48670beb57fc8cd2` | `ClearVisualResults` 底部裁剪；`6493266...` 继续预留浏览区并要求 workspace ≥130 DIP。 |
| 5 | `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P3-Automated-Acceptance-20260903-131248-aef17d52a38f` / `p3-auto-4ae24814016f4208972ba851dff0e7e6` | `6493266d25304a82c7cba3b163e45ad5d3d5fb20` | `AssetLoadMoreButton[748,648.91,85.33,36]` 底部只剩约 11.76 DIP 可见；`9f14abe...` 改为按真实页面高度动态计算视口并把加载更多状态加入回归。 |

最后五轮都停在 `primary → 第 12 场景 tag-manager-lifecycle/v1`。**原始异常文本只存在于**各根的 `run-manifest.json` 和 `app/evidence/summary-tag-manager-lifecycle-v1-primary.json`；`plans/12-tag-manager-lifecycle.json`、数据库、event journal 和 runtime log 仅提供身份、状态和数据库上下文，不能称为异常来源。五轮 `app-12` stdout/stderr 均为 0 字节。后四轮另有场景标签 JSON 与 PNG；第一轮在布局捕获前失败，因此没有该场景 PNG/bounds；后四轮在检测越界后、写 bounds 前 fail closed，因此有 PNG 而无失败帧 bounds JSON。

### 9.3 剩余最小动作

`9f14abe...` 是最后一个已保存失败根之后的候选修复。识别到绝对五轮上限已经被历史执行超过后，本次立即停止，没有自动启动新 Run。下一次只有在新的、明确授权的自动验收执行窗口中，才可在同一最终验收代码 HEAD 上生成三个全新、互不复用的 Run；每轮 Run 后只读 `ValidateExistingRun`；三轮全绿后运行 `Test-P3AssetLibraryAutomatedRunSet.ps1`。全过程不需要真人键盘、鼠标或 DPI 操作。

## 10. 安全与工作树卫生

候选 HEAD 的三个产品自动验收 seam 按正式固定规则做了等价静态复扫：桌面输入、UIA Invoke、强制前台、真实显示写、Eagle IO、网络上传、直接栏宽写、直接 settings 反射写、直接 SQLite 行编辑命中均为 0。`RecoveryTest` 的运行时输入注入和显示写入也均为 0；Git 跟踪的 `.validation`、`bin`、`obj`、`TestResults`、fixture `.sqlite/.db` 为 0。

| 红线 | 候选级证据 | 最终三轮运行时计数 |
|---|---|---|
| Eagle `.library/.eaglepack` 读写/修改 | 静态 seam 命中 0 | N/A |
| 用户源文件写/移/删/改名/永久删除 | 产品设计为 metadata-only；相关测试通过 | N/A |
| 网络、第三方、AI、MCP 上传 | 静态 seam 命中 0 | N/A |
| 桌面输入注入 / UIA Invoke | 静态 0；RecoveryTest 输入注入 0 | N/A |
| 强制前台 / 真实显示设置写 | 静态 0；RecoveryTest 显示写 0 | N/A |
| 运行产物、未参数化 SQL、秘密入库 | tracked runtime artifacts 0；query plan 参数化测试通过 | N/A |

因为最终正式 Run 为 0/3，本报告不把候选级静态/RecoveryTest 结果冒充“正式三轮安全计数全 0”。

## 11. 十二层自检

| 层 | 结果 | 证据/阻断 |
|---:|---|---|
| 1 范围 | PASS | 仅 P3 三目标，无 Viewer/导入导出/回收站/Eagle Adapter。 |
| 2 基线 | PASS | fetch 后 P2 完整 SHA 未变且为 P3 祖先。 |
| 3 架构 | PASS | 单一 AST/query plan、共享选择、同一 metadata command/journal。 |
| 4 语义 | PASS | Current/All、ANY/ALL/NOT、NULL、归档、缺失、空集合测试通过。 |
| 5 并发 | PASS | IME、debounce、cancellation、generation guard、关闭页面回归通过。 |
| 6 数据 | PASS | v6→v7 备份/事务/幂等/损坏/未来版本测试通过。 |
| 7 安全 | BLOCKED | 候选静态与 RecoveryTest 通过；最终三轮运行时计数缺失。 |
| 8 UI | BLOCKED | 全量 WPF 和小视口回归通过；最终响应式修复尚无正式 Run 截图/bounds。 |
| 9 可访问性 | PASS（候选） | AutomationId、名称、焦点、键盘和对比度测试通过；正式三轮仍缺。 |
| 10 性能 | BLOCKED | 10k 单元护栏通过；正式三轮最差值和 UI block 缺失。 |
| 11 证据 | BLOCKED | 最终三轮 `0/3`，run-set validator N/A；失败证据未拼接。 |
| 12 Git | 待最终推送核验 | 当前提交历史可回滚、无运行产物；文档提交后再核对本地/远端完整 HEAD。 |

任一层未关闭即不宣布 P3 COMPLETE，因此总状态保持 BLOCKED。

## 12. F-001～F-083 完整矩阵

口径：`COMPLETE` 表示该功能映射在产品候选或已关闭的 P1/P2 中完整，不等同于本次 P3 总门已关闭；`PARTIAL` 表示可用但仍有明确缺口；`DEFERRED` 是适用但后置；`NOT_APPLICABLE` 是不复制的 Eagle 专属能力。统计：COMPLETE 23、PARTIAL 19、DEFERRED 20、NOT_APPLICABLE 21。

| ID | 能力 | 状态 | 证据/边界 |
|---|---|---|---|
| F-001 | 启动应用 | COMPLETE | 单应用、同窗路由与一级页恢复已由 P1 闭环。 |
| F-002 | 首次欢迎引导 | NOT_APPLICABLE | 沿用 Pixel Tart 全局引导，不复制 Eagle 素材欢迎/激活页。 |
| F-003 | 主题选择 | COMPLETE | 素材库继承宿主动态主题并通过暗色/高对比样式测试。 |
| F-004 | 创建资源库 | DEFERRED | 当前仍为固定单一私有素材库，多库创建不在 P3。 |
| F-005 | 打开/切换资源库 | DEFERRED | 尚无库历史与安全切换模型。 |
| F-006 | 合并资源库 | NOT_APPLICABLE | 明确不读取或合并 Eagle `.library`。 |
| F-007 | 清缓存并重载 | PARTIAL | 有刷新、缩略图/分析缓存与错误重试，缺完整清理重建入口。 |
| F-008 | 侧栏基础视图 | COMPLETE | P2 已交付七个批准的固定入口，回收站保持禁用占位。 |
| F-009 | 快速访问 | PARTIAL | favorite/recent 可用，固定、排序、封面等完整管理未完成。 |
| F-010 | 文件夹树 | COMPLETE | P2 已实现无限层级、创建、改名、归档、移动排序和展开记忆。 |
| F-011 | 智能文件夹树 | COMPLETE | P2 分组浏览与 P3 通用智能文件夹结果均接入统一查询。 |
| F-012 | 侧栏搜索/过滤 | PARTIAL | 组织源与统一 query 联动，独立树节点管理过滤器仍不完整。 |
| F-013 | 本地文件导入 | COMPLETE | 现有文件选择与 Reference 引用导入保留，默认不移动源文件。 |
| F-014 | 本地文件夹导入 | DEFERRED | 仅有递归预览/验收 seam，生产入口与完整闭环未做。 |
| F-015 | Eagle 素材包导入 | NOT_APPLICABLE | 不移植 `.eaglepack`。 |
| F-016 | 链接/书签导入 | NOT_APPLICABLE | 不复制 Eagle 网页收藏链路。 |
| F-017 | ArtStation/花瓣导入 | NOT_APPLICABLE | 不复制站点抓取器。 |
| F-018 | 屏幕截图 | DEFERRED | 用户截图采集来源不在 P1～P3 范围。 |
| F-019 | 自动导入监视目录 | DEFERRED | 素材库仍无 watcher。 |
| F-020 | 浏览器扩展采集 | NOT_APPLICABLE | 不建立浏览器扩展或第二本地服务。 |
| F-021 | 剪贴板导入 | DEFERRED | 当前仅复制路径，不导入剪贴板内容。 |
| F-022 | 新建文件夹/子文件夹 | COMPLETE | P2 文件夹树已提供同级/子级创建并刷新。 |
| F-023 | 新建智能文件夹 | COMPLETE | P3 候选支持从 canonical query 新建、重开、编辑、取消、复制、归档和预览。 |
| F-024 | 智能文件夹群组 | DEFERRED | P3 的嵌套规则组不是智能文件夹组织群组模型。 |
| F-025 | 文件夹重命名/移动/排序 | COMPLETE | P2 UI/repository 闭环并拒绝循环及无效目标。 |
| F-026 | 文件夹密码保护 | NOT_APPLICABLE | 不复制 Eagle 密码机制。 |
| F-027 | 快速访问/封面/图标 | PARTIAL | Folder 有 Icon/Color，缺完整封面和快速访问管理 UI。 |
| F-028 | 评分与标签 | COMPLETE | P2 共享命令层支持单/多选评分和标签增删及撤销重做。 |
| F-029 | 标签管理器 | PARTIAL | 能力已实现，但正式第 12 场景未在最终响应式修复后重跑。 |
| F-030 | 标签组 | PARTIAL | P2 浏览与 P3 管理候选已实现，同属未关闭的第 12 正式场景。 |
| F-031 | 批量重命名 | DEFERRED | 未实现素材或磁盘文件批量改名；P3 禁止源文件写回。 |
| F-032 | 批量动作 | PARTIAL | P2 命令/事务/undo 与 P3 批量编辑候选已实现，完整正式场景 13 尚未到达。 |
| F-033 | 回收站与恢复 | DEFERRED | 仍为禁用占位，无逻辑删除/恢复，永久删除继续禁止。 |
| F-034 | 当前/全部搜索 | COMPLETE | P3 候选覆盖范围切换、计数、持久化和四视图一致性。 |
| F-035 | 搜索建议与历史 | COMPLETE | 覆盖 IME、debounce/cancel、历史去重/删除/清空及重启恢复。 |
| F-036 | 文件夹筛选 | COMPLETE | 多文件夹 ANY/ALL/NOT 进入统一参数化 query。 |
| F-037 | 标签筛选 | COMPLETE | 多标签 ANY/ALL/NOT 进入统一参数化 query。 |
| F-038 | 颜色/形状筛选 | PARTIAL | 主色、配色、影调、分析状态和比例/方向已接入；无可靠通用形状元数据。 |
| F-039 | 评分/日期/大小筛选 | COMPLETE | 评分、导入/拍摄日期、文件大小及 NULL/范围语义已接入。 |
| F-040 | 格式/尺寸/时长筛选 | PARTIAL | 格式、扩展名、MIME、尺寸、像素和比例已接入，媒体时长未实现。 |
| F-041 | 注释/标注/链接筛选 | PARTIAL | Comment 已接入；区域标注和链接模型缺失。 |
| F-042 | 语义/以图筛选 | DEFERRED | 不接 AI/MCP 或不稳定以图语义能力。 |
| F-043 | 反向图片搜索 | NOT_APPLICABLE | 明确禁止向 Eagle/第三方上传用户素材。 |
| F-044 | 保存/锁定筛选 | COMPLETE | 实现锁定、清除和 canonical query 直接保存智能文件夹。 |
| F-045 | 排序与布局信息 | COMPLETE | P2 四视图共享稳定排序、方向、缺失值和 AssetId 兜底。 |
| F-046 | 重复文件扫描 | PARTIAL | 导入 hash 去重存在，独立扫描/比较/处置未实现。 |
| F-047 | 四种布局 | COMPLETE | grid/waterfall/justified/list 共享 query 与 selection。 |
| F-048 | 图片内部预览 | PARTIAL | 仅缩略图和检查器静态预览，未形成 P4 Viewer shell。 |
| F-049 | 缩放/平移/灰度/透明背景 | DEFERRED | Viewer 变换状态未实现。 |
| F-050 | 旋转/翻转/裁切/拼图 | DEFERRED | 高风险源文件写回明确排除。 |
| F-051 | GIF/WebP/AVIF 播放 | PARTIAL | 仅已有首帧/部分解码，逐帧与 AVIF 完整证据缺失。 |
| F-052 | 视频播放 | DEFERRED | 后置 Viewer/媒体阶段。 |
| F-053 | 音频播放 | DEFERRED | 后置 Viewer/媒体阶段。 |
| F-054 | URL/HTML/MHTML 预览 | NOT_APPLICABLE | 不复制 WebView。 |
| F-055 | PDF 查看器 | DEFERRED | 后置 Viewer Registry。 |
| F-056 | 字体查看器 | DEFERRED | 后置 Viewer Registry。 |
| F-057 | 3D 模型查看器 | NOT_APPLICABLE | 不在 Pixel Tart 核心格式范围。 |
| F-058 | RAW/纹理/EXIF/文本查看 | PARTIAL | RAW 登记与安全 metadata 可见，完整代理/viewer 未完成。 |
| F-059 | 检查器 | COMPLETE | P2 已实现无选择、单选、多选三态及安全 metadata/视觉入口。 |
| F-060 | 区域标注/评论 | DEFERRED | 仅整项 Comment，无区域标注模型。 |
| F-061 | 播放/新窗口/外部打开 | PARTIAL | 复制路径、查看信息与不自动执行的打开位置入口可用，统一播放/新窗未实现。 |
| F-062 | 幻灯片/随机模式 | DEFERRED | 未实现 Viewer 幻灯片/随机播放。 |
| F-063 | 导出到计算机 | DEFERRED | 后置安全导出阶段，P3 不复制或移动源文件。 |
| F-064 | eaglepack/专有格式导出 | NOT_APPLICABLE | 不生成 `.eaglepack`。 |
| F-065 | 插件中心 | NOT_APPLICABLE | 不建立第二插件市场。 |
| F-066 | 插件开发者面板 | NOT_APPLICABLE | 不复制插件开发壳。 |
| F-067 | Plugin/Web API | NOT_APPLICABLE | 不开放素材库本地写 API。 |
| F-068 | AI Search/模型/MCP | NOT_APPLICABLE | 当前产品路线明确排除。 |
| F-069 | 更新、日志、调试报告 | COMPLETE | 继续复用宿主日志、通知、错误壳和审计。 |
| F-070 | 托盘/开机启动 | NOT_APPLICABLE | 不在素材模块复制宿主级能力。 |
| F-071 | 常用设置 | PARTIAL | workspace JSON 保存浏览/query 状态，无独立完整素材设置页。 |
| F-072 | 左栏设置 | COMPLETE | P1 栏宽/折叠与 P2 树展开、活动组织源恢复已交付。 |
| F-073 | 操控/预览设置 | PARTIAL | 视图、排序、缩略图、滚动锚点与 query 可恢复，Viewer 偏好未实现。 |
| F-074 | 截图设置 | NOT_APPLICABLE | 自动验收截图不是用户截图产品功能。 |
| F-075 | 快捷键设置 | PARTIAL | 查询/规则树/浏览键盘路径存在，无用户可配置快捷键中心。 |
| F-076 | 通知设置 | PARTIAL | 复用全局任务和可见错误，缺素材专属通知偏好。 |
| F-077 | 密码锁 | NOT_APPLICABLE | 复用宿主许可边界，不建第二账号锁。 |
| F-078 | 自动导入设置 | DEFERRED | 无 watcher，因此无 watcher 配置。 |
| F-079 | 开发者设置 | NOT_APPLICABLE | 不新增素材 API token 或开发者中心。 |
| F-080 | 许可证激活 | NOT_APPLICABLE | 沿用宿主许可，不复制 Eagle 激活。 |
| F-081 | 设备管理 | NOT_APPLICABLE | 不移植 Eagle 设备管理。 |
| F-082 | 关闭重开恢复 | COMPLETE | P2 浏览状态与 P3 合法 scope/query/history 均持久化，非法值安全回退。 |
| F-083 | 空/加载/错误/权限状态 | COMPLETE | P1/P2 状态壳与 P3 错误/取消/失效引用路径存在。 |

## 13. 提交与回滚点

P3 提交的回滚必须用 `git revert <完整 SHA>`，不得 reset/clean。对相互依赖的 P3 提交按 21→1 倒序 revert；顺序 0 的 P2 SHA 只是回退目标锚点，**不能 revert 基线本身**。单独回滚修复提交会有意恢复表中所述缺陷，只适用于诊断。

| 顺序 | 完整 SHA | 提交 | 回滚边界 |
|---:|---|---|---|
| 0 | `c5fde036e13abd2039d517f2a4022e9a32452c2f` | P2 远端基线 | P3 全部回退到此点。 |
| 1 | `e6e8f4a361fbc49ad7aada2efe19f5fd1e5bf895` | `feat(asset-library): deliver P3 query and metadata workflows` | 回退 P3 产品、schema v7、测试和自动验收包；已迁移 DB 必须用受校验 v6 备份恢复。 |
| 2 | `7936a21c395bc1e43730a100275fbf43efaf620d` | `fix(acceptance): make Python preflight portable on Windows PowerShell` | 回退 PS5.1 Python 定位修复。 |
| 3 | `89da32b85742714cd7ee31d909f33f969851e494` | `fix(acceptance): handle empty safety scan matches on Windows PowerShell` | 回退空安全扫描集合兼容。 |
| 4 | `5a4ee8028d4e5f14ce7ee0248788ae00af5f21f3` | `fix(acceptance): compare fixture inventory independent of locale order` | 回退跨 locale fixture 排序修复。 |
| 5 | `5a0cf5ccfa13684415ac895d8d718d0d0843b640` | `fix(asset-library): prevent stale suggestions from expanding layout` | 回退过期建议布局保护。 |
| 6 | `ad5c383838d00476c5debd5c455cd6106fb98b50` | `fix(asset-library): resolve acceptance query references` | 回退验收 stable-ID 引用解析。 |
| 7 | `1b66157725017326bff49821e0bc345b595f9e63` | `fix(asset-library): preserve smart-folder query metadata` | 回退智能文件夹查询 metadata 保留。 |
| 8 | `8ad3229b334e59021661c6158bf94ba5ed20b98a` | `test(acceptance): report P3 layout overflow offenders` | 回退越界 offender 诊断，不改产品。 |
| 9 | `e358ef0ec2361cb94224d9587bfc97feb3fcf3db` | `fix(asset-library): bound smart-folder editor layout` | 回退规则编辑器高度约束。 |
| 10 | `ce530a7676a7d1394485661937c86fa2742770d7` | `fix(asset-library): preserve workspace room beside smart-folder editor` | 回退横向工作区预留。 |
| 11 | `52c8d77610caa85598cfe4d4d32161577a12f52a` | `fix(asset-library): leave migration workspace safety margin` | 回退迁移场景工作区余量。 |
| 12 | `1e4fcb83ae48a9332b730cbfa9a09a84586d8712` | `fix(asset-library): scroll organization content as one pane` | 回退组织栏统一滚动。 |
| 13 | `11d4c60d1bf5fbe6942f8e1a48b1b6d81159e08a` | `fix(asset-library): guard stale search debounce refreshes` | 回退搜索 generation guard。 |
| 14 | `23af13692b8383a69d3dd07d6f030c4f2022c8fe` | `fix(asset-library): publish stable batch completion` | 回退第一版独立批量完成信号。 |
| 15 | `dfff1fd68affd81dfad4658d483dcc12481e4817` | `fix(asset-library): harden stable batch refresh` | 回退完成信号刷新加固。 |
| 16 | `98472c4f5badb80bc3bfb710b62c2425b42a8eb9` | `fix(asset-library): stabilize batch apply completion` | 回退 generation/outcome/operationId 和等价选区幂等修复。 |
| 17 | `8041394bec785982012395422d81135c17d2dff7` | `test(asset-library): isolate P3 sqlite pools` | 回退测试 SQLite pool 隔离。 |
| 18 | `e7e85b43ae211c8c812f0c77ebb5505b6bb7a637` | `fix(asset-library): keep tag manager within viewport` | 回退标签管理器首版滚动视口。 |
| 19 | `27230f18bf2889128fcf8e7e48670beb57fc8cd2` | `fix(asset-library): preserve browser toolbar below tag manager` | 回退浏览工具栏余量修复。 |
| 20 | `6493266d25304a82c7cba3b163e45ad5d3d5fb20` | `fix(asset-library): reserve full browser controls under tag manager` | 回退固定 150 DIP 与完整控件回归。 |
| 21 | `9f14abe1cb76507bfa4605b39db0cd9fe2222d32` | `fix(asset-library): size tag manager against live viewport` | 回退动态高度公式；会恢复已知的 Load More 裁剪，仅作机械诊断回滚点。 |
| 22 | 本报告提交（完整 SHA 在最终 git log/远端核验中回传） | `docs(asset-library): record P3 bounded acceptance status` | 只回退本文档。 |

## 14. 已知风险与建议 P4 边界

1. 唯一已知的未复验产品缺陷是标签管理器小视口布局；证据闭环还整体缺少三轮正式 Run、每轮只读 validator、50 类动态负例证明、run-set 聚合、正式性能最差值和运行时安全计数。不得通过延长 timeout、放宽 bounds 或复用旧截图关闭。
2. WPF 定向测试通过反射调用私有 `SetNextCursor` 使“加载更多”进入可见状态；这是非阻断测试维护风险，未来方法改名需同步测试，但不影响产品运行时。
3. F-024 智能文件夹组织群组仍未实现；嵌套 query group 不能冒充组织树群组。
4. Viewer F-048～F-062 保持原边界。建议 P4 建独立 Viewer Registry 和安全格式降级，不把播放/写回混入查询或浏览卡片。
5. 多库、导入/导出、逻辑回收站需要单独数据安全设计；永久删除、Eagle 写回和用户源文件写回继续禁止。

## 15. 最终远端身份

- 目标远端：`KITAOCORPORAL/pixel-tart-source-private`
- 目标分支：`feature/asset-library-eagle-parity-p3-query-metadata`
- 本地验收候选代码 HEAD：`9f14abe1cb76507bfa4605b39db0cd9fe2222d32`
- 报告提交后最终 HEAD：以最终 `git rev-parse HEAD` 与 `git ls-remote source-private refs/heads/feature/asset-library-eagle-parity-p3-query-metadata` 的完全一致结果为准，并在最终回传给出完整 SHA。
- 不创建 PR、不合并 `main`、不合并其他功能分支、不改写历史。
