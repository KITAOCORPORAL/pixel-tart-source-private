# Pixel Tart 素材库缺口与实施优先级

## 范围与证据等级

审计目录为 `D:\AI AGENT\worktrees\modular-harness-v1`，基线 HEAD 为 `73530b9cbe2510e5e1266af8115a4e50eb4bc000`。判断依据是当前工作树源码，包含原有 17 项未提交修改；未修改源码、构建、运行或操作真实素材库。本文的“已实现”指已追踪到代码路径，不表示通过运行验收。

录屏事实见 [01 录屏索引](01_Eagle录屏索引.md)、[02 行为规范](02_Eagle交互行为规范.md)、[03 功能清单](03_Eagle功能清单.md)。摄影领域模型和功能取舍统一见 [04 映射](04_Eagle_to_PixelTart映射.md)，本清单不新增相关 UI。后续交互约束见 [06 交互基线](06_PixelTart素材库交互基线.md)。P0–P3 是本清单的优先级，与仓库历史 P1/P2/P3 开发阶段无关。

## 已有能力：应复用，不能重复造轮子

| 能力 | 当前实现与限制 | 代码入口 |
|---|---|---|
| 模块和工作区 | WPF 模块提供查询、导入、文件夹、标签、视觉分析；工作区 host 管理新建/打开/切库 | [AssetLibraryModule.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryModule.cs#L21) L21–61；[AssetLibraryWorkspaceHost.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryWorkspaceHost.cs#L94) L94、228 |
| 实体与持久化 | AssetItem、Folder、Tag、成员关系、Smart Folder、undo journal、视觉特征已存在；SQLite schema 为 7 | [AssetLibraryModels.cs](../../../src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs#L8) L8；[AssetLibrarySchema.cs](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibrarySchema.cs#L9) L9–249 |
| 查询与分页 | QueryAsync 将当前范围和 Smart Folder 文档合并；参数化查询、游标、引用校验及筛选规则已实现 | [SqliteAssetLibraryRepository.cs](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs#L166) L166；[SqliteAssetLibraryRepository.V15.cs](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.V15.cs#L150) L150–192；[AssetQueryModels.cs](../../../src/RAWSelectionAssistant.Core/Models/AssetQueryModels.cs#L33) L33–94 |
| 搜索与智能文件夹 | 搜索防抖、IME 暂停提交、建议、历史、条件 chip、规范化文档与智能文件夹保存均有实现 | [AssetLibraryViewModel.P3QueryComposer.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3QueryComposer.cs#L188) L188、237、363、507 |
| 文件夹、标签、批量元数据 | 支持多对多关系、层级、排序、标签组和合并；批量预览与应用校验请求和前态指纹，修改和 journal 同事务 | [AssetLibraryContracts.cs](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs#L45) L45–98；[SqliteAssetLibraryRepository.P3.cs](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.P3.cs#L914) L914 起；[AssetLibraryViewModel.P3TagManager.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3TagManager.cs#L503) L503 |
| 四布局与状态 | Grid/Masonry/Justified/List、7 种排序字段、缩略图尺寸、面板宽度/折叠和选中 ID 已存在；切换布局记录首个可见 AssetId | [AssetLayoutEngine.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLayoutEngine.cs#L23) L23；[VirtualizingAssetPanel.cs](../../../src/PixelTart.Modules.AssetLibrary/VirtualizingAssetPanel.cs#L28) L28；[AssetLibraryPage.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs#L454) L454；[AssetLibraryWorkspaceSettings.cs](../../../src/RAWSelectionAssistant.Core/Models/AssetLibraryWorkspaceSettings.cs#L12) L12 |
| 选择与检查器 | 右击已选项保留多选，右击未选项收敛单选；零/单/多选检查器有实现；跨页选中会重新验证是否属于当前查询 | [AssetLibraryContextSelectionPolicy.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryContextSelectionPolicy.cs#L10) L10；[AssetLibraryViewModel.P2Browser.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs#L61) L61；[AssetLibraryViewModel.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1553) L1553 |
| 视觉分析与缓存 | 栅格像素/ICC、视觉特征、颜色与相似搜索真实存在；批量分析已接入任务桥；缩略图异步解码、取消、失败状态和内存缓存已存在 | [WpfVisualAnalysisDecoder.cs](../../../src/PixelTart.Modules.AssetLibrary/WpfVisualAnalysisDecoder.cs#L18) L18；[AssetLibraryViewModel.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1368) L1368；[AsyncThumbnail.cs](../../../src/PixelTart.Modules.AssetLibrary/AsyncThumbnail.cs#L11) L11 |
| 文件安全基础 | 引用/托管导入、SHA-256 去重、事务回滚；容器 manifest/路径/重解析点校验、写租约；素材包快照、hash、压缩比和路径检查 | [SqliteAssetLibraryRepository.cs](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs#L82) L82、790；[AssetLibraryContainerService.cs](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContainerService.cs#L33) L33、127、292；[AssetLibraryPackageService.cs](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryPackageService.cs#L44) L44、144 |

## 缺口与落地任务

### P0：未确认阻断级缺陷

本轮没有证据充分、应列为 P0 的数据破坏缺陷，不能为了凑齐分级而强造。这里也不等于全面安全审计通过。若实际验证发现跨库提交、引用删除原文件、事务破坏或错误覆盖，应直接升级 P0 并附复现。

### P1-01：元数据生产链未闭合

**现状。** [UpsertAssetAsync](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs#L779) L779–785 将 Width、Height、Orientation、CaptureTime 初始写为 NULL。当前素材库服务中未找到普通导入后的尺寸/EXIF 回填路径。查询字段和检查器虽存在，新导入素材不能因此被视为已具备数据；布局宽高比还会退回默认值。

**实施。** 先为图片导入增加轻量元数据抽取和独立补齐任务：像素尺寸、EXIF 方向、拍摄时间及来源/时区可信度。NULL 保持“未知”，不转成 0 或伪造导入时间。不要把旋转后的显示尺寸与原始宽高混在一起；未知时区保留来源标记。以 LibraryId、AssetId、文件版本/hash 绑定回填，防止文件变化后提交过期数据。已导入项目提供可重试的补齐路径。

**完成条件。** 横竖图、带旋转 EXIF、无 EXIF、损坏图都得到正确值或明确未知；宽高范围/比例/拍摄日期查询命中真实导入数据，刷新和重启后相同。录像 V07 02:13 只证明宽高范围控件存在，不能当作本功能验证通过。

### P1-02：真实 Viewer、RAW 与格式能力契约

**现状。** [AssetLibraryPage.xaml](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml#L369) L369 的“打开大图预览”与“查看信息”同用 [ShowContextInfoAsync](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs#L840) L840，只展开检查器；检查器预览高度为 150。L370–374 外部打开/新窗口/定位全部禁用。RAW 分析在 [WpfVisualAnalysisDecoder.cs](../../../src/PixelTart.Modules.AssetLibrary/WpfVisualAnalysisDecoder.cs#L23) L23 直接拒绝，未接代理解析器；文件 >512 MiB 在 L27 拒绝。

**实施。** 图片优先交付只读 Viewer：适应窗口/原始比例、缩放平移、上下项与返回原选择/位置。RAW 先提供已有内嵌预览或代理，不承诺完整 RAW 渲染；失败允许定位/外部打开。PSD/PSB、SVG、GIF、视频、PDF、字体逐类建立“可索引/缩略图/可查看/可分析”能力表，再分阶段接 provider，不能以分类枚举代表支持。视频首帧、GIF 静帧不冒称可播放。外部打开来自用户明确命令；只启动选中路径，禁止拼接任意命令。

**完成条件。** JPG 原图可缩放返回；RAW 代理可用与不可用两支清晰；非支持格式显示原因和后续入口；不能再用检查器冒充大图。源文件 hash 不因浏览改变。

### P1-03：普通导入的进度、取消与切库隔离

**现状。** [ImportAsync](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1030) L1030–1060 直接调用仓储，没有传入任务 progress 或 lifetime token。批量视觉分析的任务桥并不代表普通导入也被保护。关闭时 [DisposeCoreAsync](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1822) L1822 取消并 drain P3 操作，但普通导入不在同一跟踪路径中。

**实施。** 普通导入进入现有任务桥：扫描/校验/索引或复制/收尾阶段、真实数量、重复/失败明细与取消。任务持有目标库会话及 LibraryId，库切换后旧结果不能发布到新页。切换决策只允许等待、取消后切换、经验证可安全绑定旧库完成，或明确禁止当前任务中切换；是否开放安全后台切换取决于实现能力。“强制切换”不是绕过事务/写锁。参考 V06 00:33 的可强制切换提示，不能改写成 Eagle 一律阻止切库。

**完成条件。** 千张图片、混合错误、取消中复制、导入时切库/关闭均不跨库写、不丢已提交状态；取消剩余数与成功数可信。大任务不只显示一个最终状态句。

### P1-04：安全归档/恢复闭环；回收站属于产品缺口

**现状。** 归档、恢复、持久化 undo 已实现；[RestoreContextCommand](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs#L141) L141 未绑定当前卡片菜单。用户仍可通过“更多→标签管理与批量编辑→归档状态=否→预览/应用”恢复，所以不能声称后端完全没有恢复。回收站则在 [BuildSystemCollections](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs#L205) L205–219 与 XAML L446 明确禁用，没有完整生命周期。

**实施。** 先补对称的归档/恢复入口和计数，再按 [06](06_PixelTart素材库交互基线.md) 实施 Trash。存储模式 Reference/Managed、生命周期 Active/Archived/Trashed、可用性 Available/Missing/LibraryOffline 分轴表达。删除默认可恢复；Reference 不删除原文件。Shift+Delete 不得默认硬删除，文件夹内可使用“移出当前文件夹”的历史语义；永久删除只在未来明确回收站清理动作后确认；本轮仅定义契约，不开放永久删除。保留原集合/标签、删除时间、操作 ID、原生命周期及恢复策略。

**完成条件。** 三项删除数量与 Toast/回收站一致；归档恢复对称；部分失败准确；重启可恢复。原文件夹/原物理位置不存在不自动覆盖或随意新建；按 06 规则恢复或等待定位。V07 展示删除和清空，未展示恢复；历史 TXT API 恢复证据不可替代本版本 UI 验收。

### P2-01：快速操作与命名一致性

- F 的 [FocusFolderClassifier](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1737) L1737 仅改状态，未弹出分类器；T 也只提示“输入标签”（[AssetLibraryPage.cs](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs#L605) L605）。补真实聚焦、最近目标、键盘选择和可撤销提交。V07 03:58 的方向键/Enter/Esc 是界面提示，不能当作按键实测。
- 将当前“打开大图预览”先正名为“查看信息”；真实 Viewer 完成再使用预览文案。外部打开、定位文件、复制路径分别定义。外部安全打开和定位对只读素材流非常有价值，可与 P1 Viewer 同批交付，但不以虚构风险继续永久禁用。
- [IsSupportedReferencePath](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs#L1092) L1092 只允许常见静态图片和若干 RAW；仓储分类支持 PSD/PSB/MP4/MOV/AVI（[ClassifyMediaType](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs#L836) L836），两者不一致。建立统一能力登记和被拒文件提示；外部拖放导入是新入口，当前拖放为库内成员关系，不能声称已有文件导入拖放。

### P2-02：筛选语义、批量范围与浮层

现有名称/扩展名/备注/标签/文件夹搜索、All/Any/NOT 规则、范围校验和未知值操作符应复用。对 V07 的搜索字段勾选（00:25/00:28）、宽高（02:13）、时长秒（02:35）、大小 KB（02:40）、注释有/无/关键字（02:48）建立统一条件模型：当前 MediaType/数值模型没有时长字段，应作为扩展而非假定已支持。语义搜索 02:19 只打开输入框；本期不直接启用没有 provider 的语义搜索。

标签左键包含/右键排除与标签管理必须共用同一 TagId；标签合并对已保存查询的引用更新要保留。浮层需可滚动、触边自动避让、Esc 分层关闭；批量命令明确“已选择 N 项”，不能把当前视口、已加载页、当前查询全部混淆。V07 05:49 只能证明六选保持与子菜单向左展开。

### P2-03：密度锚点和异步完成状态

[ViewModel_ViewModeChanging/Changed](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs#L454) L454 已提供布局切换的首个可见 AssetId/选中项回退，[RememberScrollAnchor](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs#L598) L598 已按布局存储；不是从零新增。补密度连续拖动、锚点尚未加载/已失效、排序变化的规则及验收。V07 04:22–04:43 只证明密度变为约 5/2/1 列且保持选中，不证明精确像素锚点。

现有批量应用区分“提交失败”和“已提交但刷新未完成”（[ApplyP3BatchMetadataAsync](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3TagManager.cs#L503) L503）；其他异步命令必须继承该语义，禁止刷新失败时诱导再次提交。

### P3-01：缓存与次级效率

[AsyncThumbnail](../../../src/PixelTart.Modules.AssetLibrary/AsyncThumbnail.cs#L11) L11 的 64 MiB 进程内缓存、失效指纹和失败状态已存在；容器 previews 目录尚未接入该缓存。可新增有界磁盘缓存、provider/version/hash 失效、失败重试和重建入口。若真实大库验收显示滚动卡顿，升级 P2。时长/多页/字体等深度预览和跨应用编辑版本后续再排，不阻挡图片索引与只读 Viewer。

## 实施顺序与验收门槛

先统一格式能力与元数据/任务契约，再补图片 Viewer 和安全外部定位；同时收口归档恢复，随后启用 Trash。快速分类、标签浮层和密度细节在同一查询/选择/命令模型上实现。不要先摆满禁用菜单再声称功能完成。

| 场景 | 必须观察的结果 |
|---|---|
| 真实混合导入 | 每类可索引/预览/分析状态明确；未支持数量可见；尺寸与 EXIF 查询来自真实文件 |
| 导入取消、切库、关闭 | 写入目标 LibraryId 不变；旧页结果不污染新页；无遗留不可识别临时文件 |
| 查询快速输入 + IME | 旧结果不覆盖新查询；输入法候选态不触发归档/评分/删除/搜索快捷键 |
| 多选与分页 | 右击已选项保留集合；右击未选项按契约收敛；跨页只保留当前查询成员，批量数与实际提交一致 |
| 四布局 + 密度 | 选择保持；已有布局锚点正常；密度锚点和失效回退有可重复验证 |
| 归档/Trash/恢复 | Reference 源 hash 不变；计数一致；位置冲突或缺失不覆盖；部分失败/undo/重启准确 |
| 失败与刷新 | 区分未提交、部分文件成功、已提交刷新失败；重试不重复写 |
| 小窗口 + 菜单 | 标签长列表可滚动，子菜单不越界，Esc 先关顶层浮层，关闭后焦点回发起控件 |

全部场景为下一步验收要求；本文没有执行运行测试，也没有用历史测试通过记录替代当前 HEAD 的验收。
