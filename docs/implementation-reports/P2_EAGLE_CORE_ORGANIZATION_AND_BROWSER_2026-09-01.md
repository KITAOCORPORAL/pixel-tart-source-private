# 素材库 P2 Eagle 核心组织与浏览实施报告

日期：2026-09-01
阶段：P2（Eagle 核心组织栏与素材浏览；不含 P3～P6）
执行分支：`feature/asset-library-eagle-parity-p2`
P1 已关闭起点：`ba25aae315e566db76a5db98a1762d006b91263f`
验收代码 HEAD：`806e2b78441e0a77ec0722efdc1ae0b3f7baeb8c`（交付文档提交后的最终 HEAD 以 git log/ls-remote 回传为准）
远端核验：`报告提交后由 git ls-remote 核验；最终完整值随交付回传`

> 本报告的功能口径来自 `P0_ASSET_LIBRARY_AUDIT_MIGRATION_MAP_2026-08-18.md` 的 F-001～F-083 原表及本轮 P2 开发指令。P2 只宣称组织栏、四视图、统一查询/排序/选择、metadata-only 拖放与上下文命令、三态检查器和独立自动验收闭环；Viewer、通用筛选器、完整导入/导出、逻辑回收站及 Eagle Adapter 均未纳入。所有数字均来自本地最终验收代码 HEAD=806e2b7 的独立日志、sealed run root 与只读 validator；历史失败根目录不与最终证据拼接。

## 1. 结论与范围边界

本轮在既有嵌入式三栏素材库中扩展 Eagle 核心组织与浏览能力，没有创建第二个窗口、第二套素材数据库、第二套查询服务或第二套任务中心：

- 左侧组织栏提供“全部素材、最近添加、未归类、未打标签、缺失文件、已归档、回收站占位”固定入口，以及无限层级文件夹树、智能文件夹和标签分组。
- 中央浏览器由同一 `AssetLibraryQuery`、排序状态和 `selectedAssetIds` 驱动网格、瀑布流、两端对齐和列表四种真实视图。
- 文件夹、智能文件夹、标签、搜索、系统集合和排序统一进入 repository query plan；分页排序使用确定性次序和 `AssetId` 兜底，避免跨页漂移。
- 拖放和右键命令只改变素材库内部元数据；支持批量文件夹/标签归属、评分、缺失标记、归档/恢复、从当前视图移除、撤销/重做及查看信息。
- 检查器区分无选择、单选和多选，不再把多选伪装成第一项的单文件详情。
- 新增独立 P2 自动验收包；其 run root、契约、validator 和受控 fixture 不复用或改写 P1 证据。

以下仍明确不属于 P2：完整 Viewer、视频/音频/PDF/字体/3D 查看器、通用 AND/OR 筛选与建议历史、复杂嵌套智能规则、资源库切换、文件夹/剪贴板/监视目录导入、批量重命名、导出、逻辑回收站恢复、永久删除、Eagle `.library`/`.eaglepack` 写入、插件/Web API、AI/MCP 及 Eagle Adapter。

## 2. 架构与代码变化

### 2.1 Core、query 与持久状态

| 文件/组件 | P2 职责 |
|---|---|
| `src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs` | 扩展归档范围、排序字段/方向及统一 query 描述，保留旧构造调用兼容。 |
| `src/RAWSelectionAssistant.Core/Models/AssetLibraryV15Models.cs` | 固定系统集合到统一 query 的映射；回收站查询 fail closed，不返回普通素材。 |
| `src/RAWSelectionAssistant.Core/Models/AssetLibraryWorkspaceSettings.cs` | 持久化视图、排序、活动集合、展开文件夹、共享选中 ID 和各视图滚动锚点；兼容 P1 单选字段。 |
| `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs` | 增加 metadata-only 归档/缺失/文件夹恢复及重做契约。 |
| `src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository*.cs` | 普通/智能/正则查询共享稳定排序与游标；批量元数据命令写入可审计 journal，支持 v2 撤销/重做。 |
| `tests/RAWSelectionAssistant.Tests/AssetLibraryP2CoreTests.cs` | 覆盖排序/游标、系统集合、资产标志、文件夹恢复、跨重启重做、旧 v1 journal 与 P1 设置兼容。 |

### 2.2 WPF 组织栏、四视图与命令层

| 文件/组件 | P2 职责 |
|---|---|
| `AssetLibraryViewModel.P2Browser.cs` | 统一组织源、query、排序、共享选择、滚动锚点、三态检查器和 P2 命令编排。 |
| `AssetLibraryOrganizationNodes.cs` | 固定入口、树节点、智能文件夹和标签分组的稳定 AutomationId、可访问名称、展开状态及节点命令。 |
| `AssetLayoutEngine.cs`、`VirtualizingAssetPanel.cs` | 网格、瀑布流、两端对齐、列表的共享虚拟化布局和安全窄宽处理。 |
| `AssetLibraryBrowserCommandService.cs` | metadata-only 批量命令、冲突预览、撤销/重做；不承载磁盘文件写操作。 |
| `AssetLibraryDragDropBehavior.cs` | 应用内素材 ID 拖放；拒绝无效/智能文件夹目标，只提交元数据归属变化。 |
| `AssetLibraryPage.xaml` / `.cs` | 组织树、四视图工具栏、列表列头、上下文菜单、拖放目标、检查器三态和视图/滚动状态衔接。 |
| `AssetLibraryViewModel.cs` | 保留 P1 初始化/状态壳，接入 P2 partial，不复制 repository 或加载逻辑。 |

四视图共享同一个 `ItemsSource` 和选择集合；视图切换只改变布局策略。组织栏与浏览器的加载、空、错误状态分别暴露稳定 AutomationId。回收站只显示“暂未启用”，没有删除或恢复流程。

### 2.3 独立 P2 自动验收

| 文件/组件 | P2 职责 |
|---|---|
| `tools/AssetLibraryP2AutomatedAcceptance/Invoke-P2AssetLibraryAutomatedAcceptance.ps1` | `DryRun`、`RecoveryTest`、`Run`、`ValidateExistingRun` 四种模式及 run-owned 进程/环境/数据库清理。 |
| `tools/AssetLibraryP2AutomatedAcceptance/Test-P2AssetLibraryAutomatedEvidence.ps1` | 只读 fail-closed validator；检查同一 run id、进程/二进制身份、证据顺序、输入树指纹和安全计数。 |
| `automated-acceptance-contract.json` | 固定 512 项 synthetic metadata、十类场景、四组模拟布局/DPI、证据字段与性能阈值。 |
| `AssetLibraryP2AutomatedAcceptanceDriver.cs` | 通过公开 ViewModel/WPF/command seam 驱动受控场景，不发送桌面输入。 |
| `MainWindow.AssetLibraryP2AutomatedAcceptance.cs` | 在 P2 acceptance 编译门控内执行十类真实 WPF 场景和截图/边界采集。 |
| `AssetLibraryP2AutomatedAcceptanceController.cs` | 建立 run-owned fixture、写出 manifest/事件/query/selection/command/inspector/performance/DB audit 并封存。 |
| `AssetLibraryP2Automated*Tests.cs` | 锁定独立契约、固定 fixture、四 DPI、场景顺序、validator 负例和禁止桌面/Eagle 驱动边界。 |

验证器运行时的 stdout/stderr/result 日志写入 sealed run root 外的唯一 sibling 目录；runner 同时要求退出码为 0、stderr 为空、stdout 为 schema/status/run_root/source_head 全匹配的 PASS JSON，避免验证器自读锁或 PowerShell 错误被误报为成功。

## 3. 数据、迁移与兼容性

- 素材私有 SQLite schema 保持 **v6**；本轮没有创建 v7、没有删除/改名表或列，也没有原地篡改旧行。
- 新增能力复用既有 `Assets.IsArchived`、`Assets.IsMissing`、文件夹/标签 membership、folder archive 与 undo journal 表。因此数据库升级路径是“无 schema migration”，旧 v6 数据可直接读取。
- 新写 journal 使用可重放的 v2 payload 保存 before/after image，以支持跨 repository 重启的重做；旧 v1 journal 继续按旧契约可撤销，不伪造缺失的 after image。
- P2 工作区状态仅向现有 JSON 设置对象增加可选字段；缺失、非法或旧字段经 allowlist/范围规范化回退。P1 的 `SelectedAssetId` 仍可恢复并映射到新的 `SelectedAssetIds`。
- 默认导入策略继续是 Reference；没有启用 ManagedCopy，没有移动、复制、重命名、覆盖或删除用户源文件。
- 没有读取或写入 Eagle `.library`；P2 fixture 和 acceptance DB 全部位于各自 run root。

迁移/兼容测试结果：`Core P2 兼容筛选 5/5；PowerShell 5.1 脚本 AST/握手契约 12/12；最终 P2 专项 37/37。覆盖 v2 journal、旧 v1 undo-only、旧单选状态、PS5.1 路径/哈希与验证器输出契约；未把契约测试冒充完整迁移测试。`。

## 4. F-001～F-083 完整映射

状态口径：

- **本 P2 已实现**：本轮 P2 范围内有产品代码和自动证据；若功能名还包含更大范围，边界在最后一列明确列出。
- **沿用已有**：P0/P1 已有，本轮未重写，仅做兼容回归。
- **部分实现**：存在可用子集，但功能名所代表的整体能力仍未闭环。
- **明确延期**：适用于 Pixel Tart，但不在本轮 P2，后续阶段再做。
- **Eagle 专属不实现**：不复制 Eagle 独立产品能力；不能被统计为 P2 完成。

| ID | 功能 | P2 结论 | 实现证据与保留边界 |
|---|---|---|---|
| F-001 | 启动应用 | 沿用已有 | 沿用 P1 单应用、同窗 route 和安全一级页恢复；P2 没有新增独立 EXE/窗口。 |
| F-002 | 首次欢迎引导 | Eagle 专属不实现 | 沿用 Pixel Tart 全局 onboarding，不创建 Eagle 式素材欢迎/激活页。 |
| F-003 | 主题选择 | 沿用已有 | 素材页继续继承宿主动态主题；P2 控件使用设计系统资源。 |
| F-004 | 创建资源库 | 明确延期 | P2 继续使用现有单一私有素材库；多库创建不在本轮。 |
| F-005 | 打开/切换资源库 | 明确延期 | 没有库历史、切换或合并流程。 |
| F-006 | 合并资源库 | Eagle 专属不实现 | 不读取或合并 Eagle `.library`。 |
| F-007 | 清缓存并重载 | 部分实现 | 沿用刷新、缩略图/分析缓存和错误重试；完整缓存清理/重建管理入口仍未实现。 |
| F-008 | 侧栏基础视图 | 本 P2 已实现 | 增加七个固定入口；回收站按批准边界仅为禁用占位，随机模式不在 P2。 |
| F-009 | 快速访问 | 部分实现 | 沿用 existing favorite/recent；完整固定、排序和封面管理未纳入。 |
| F-010 | 文件夹树 | 本 P2 已实现 | 无限层级展开/收起、选中、创建同级/子级、重命名、归档/恢复、移动排序及展开记忆。 |
| F-011 | 智能文件夹树 | 本 P2 已实现 | 独立分组、只读结果和基础编辑入口；复杂嵌套规则明确延期到 P3。 |
| F-012 | 侧栏搜索/过滤 | 部分实现 | 组织源与素材搜索统一进入 query；独立的树节点底部过滤器仍未做成完整管理器。 |
| F-013 | 本地文件导入 | 沿用已有 | 沿用文件选择和 Reference 导入；P2 拖放只改元数据，不把它冒充文件导入。 |
| F-014 | 本地文件夹导入 | 明确延期 | 既有递归预览 seam 保留，生产 UI 与完整闭环后置到导入阶段。 |
| F-015 | Eagle 素材包导入 | Eagle 专属不实现 | `.eaglepack` 不移植。 |
| F-016 | 链接/书签导入 | Eagle 专属不实现 | 不复制 Eagle 网页收藏链路。 |
| F-017 | ArtStation/花瓣导入 | Eagle 专属不实现 | 不复制站点抓取器。 |
| F-018 | 屏幕截图 | 明确延期 | 采集来源功能不在 P2。 |
| F-019 | 自动导入监视目录 | 明确延期 | 没有 watcher 或其配置。 |
| F-020 | 浏览器扩展采集 | Eagle 专属不实现 | 不建立浏览器扩展或第二本地服务。 |
| F-021 | 剪贴板导入 | 明确延期 | P2 仅允许“复制路径”，不把剪贴板内容导入素材库。 |
| F-022 | 新建文件夹/子文件夹 | 本 P2 已实现 | 文件夹树节点提供同级/子级创建并刷新树。 |
| F-023 | 新建智能文件夹 | 部分实现 | 沿用保存视觉智能文件夹并增加基础条件编辑入口；通用规则构造器与复杂嵌套仍延期。 |
| F-024 | 智能文件夹群组 | 明确延期 | 现有领域模型没有智能文件夹群组，P2 不伪造。 |
| F-025 | 文件夹重命名/移动/排序 | 本 P2 已实现 | UI 与既有 repository 闭环，循环/无效目标由仓储和命令层拒绝。 |
| F-026 | 文件夹密码保护 | Eagle 专属不实现 | 不复制 Eagle 密码保护机制。 |
| F-027 | 快速访问/封面/图标 | 部分实现 | 沿用 Folder Icon/Color 字段；完整封面和快速访问管理 UI 未完成。 |
| F-028 | 评分与标签 | 本 P2 已实现 | 单选/多选共享命令层支持评分、加入/移出标签并可撤销/重做；标签管理器另列 F-029。 |
| F-029 | 标签管理器 | 部分实现 | 沿用搜索/重命名/合并/归档后端；P2 只做标签分组浏览和筛选。 |
| F-030 | 标签组 | 本 P2 已实现 | 现有 TagGroup/AssetTag 在左栏分组展示并进入统一 query；复杂组管理仍不在 P2。 |
| F-031 | 批量重命名 | 明确延期 | P2 命令层没有素材或磁盘文件重命名。 |
| F-032 | 批量动作 | 本 P2 已实现 | 文件夹/标签归属、评分、缺失、归档/恢复支持批量、冲突摘要和撤销/重做；导入/导出批处理不在此项 P2 边界。 |
| F-033 | 回收站与恢复 | 明确延期 | 只显示“暂未启用”占位；没有逻辑删除、恢复或永久删除。 |
| F-034 | 当前/全部搜索 | 明确延期 | 统一搜索已存在，但显式“当前范围/全库”开关属于 P3。 |
| F-035 | 搜索建议与历史 | 明确延期 | 通用建议/历史属于 P3；视觉查询 history 不冒充通用搜索历史。 |
| F-036 | 文件夹筛选 | 本 P2 已实现 | 单一活动文件夹及系统入口进入统一 query；多文件夹组合/AND/OR 属 P3。 |
| F-037 | 标签筛选 | 本 P2 已实现 | 单一活动标签从标签组进入统一 query；多标签 AND/OR 属 P3。 |
| F-038 | 颜色/形状筛选 | 部分实现 | 沿用颜色、配色和视觉分析筛选；形状/比例的通用筛选器仍不完整。 |
| F-039 | 评分/日期/大小筛选 | 部分实现 | query/repository 字段和评分、添加/拍摄时间、文件大小排序可用；完整组合筛选 UI 属 P3。 |
| F-040 | 格式/尺寸/时长筛选 | 部分实现 | 现有格式/尺寸元数据可搜索或显示；时长与通用组合筛选未完成。 |
| F-041 | 注释/标注/链接筛选 | 部分实现 | Comment 可检索；区域标注和链接模型未实现。 |
| F-042 | 语义/以图筛选 | 明确延期 | 不在 P2；不接不稳定 AI/MCP 或第三方反向图片服务。 |
| F-043 | 反向图片搜索 | Eagle 专属不实现 | 明确禁止把用户素材上传到 Eagle/第三方反搜。 |
| F-044 | 保存/锁定筛选 | 部分实现 | Smart Folder 可保存受支持规则；通用筛选锁定和完整保存查询属于 P3。 |
| F-045 | 排序与布局信息 | 本 P2 已实现 | 四视图共享添加/拍摄时间、文件名、大小、评分、颜色/视觉状态排序及方向；缺失值稳定并以 AssetId 兜底。 |
| F-046 | 重复文件扫描 | 部分实现 | 沿用导入哈希去重；独立扫描/比较/处置流程未实现。 |
| F-047 | 四种布局 | 本 P2 已实现 | 网格、瀑布流、两端对齐、列表由共享虚拟化布局驱动；不是四套查询。 |
| F-048 | 图片内部预览 | 沿用已有 | 沿用缩略图和检查器静态预览；完整 Viewer 明确不在 P2。 |
| F-049 | 缩放/平移/灰度/透明背景 | 明确延期 | 属 Viewer 阶段，P2 没有实现。 |
| F-050 | 旋转/翻转/裁切/拼图 | 明确延期 | 高风险写回功能不在 P2；当前没有源文件写入。 |
| F-051 | GIF/WebP/AVIF 播放 | 部分实现 | 沿用已有首帧/部分解码；逐帧播放与 AVIF 完整证据不属于 P2。 |
| F-052 | 视频播放 | 明确延期 | Viewer/媒体阶段后置。 |
| F-053 | 音频播放 | 明确延期 | Viewer/媒体阶段后置。 |
| F-054 | URL/HTML/MHTML 预览 | Eagle 专属不实现 | 不复制 WebView。 |
| F-055 | PDF 查看器 | 明确延期 | Viewer Registry 阶段后置。 |
| F-056 | 字体查看器 | 明确延期 | Viewer Registry 阶段后置。 |
| F-057 | 3D 模型查看器 | Eagle 专属不实现 | 不在 Pixel Tart 核心格式范围。 |
| F-058 | RAW/纹理/EXIF/文本查看 | 部分实现 | 沿用 RAW 登记和 metadata；P2 单选检查器展示安全元数据，完整代理/viewer 仍未完成。 |
| F-059 | 检查器 | 本 P2 已实现 | 无选择显示查询统计，单选显示安全 metadata/视觉入口，多选显示数量与共同/批量信息；完整 Viewer/元数据编辑仍后置。 |
| F-060 | 区域标注/评论 | 明确延期 | 仅沿用整项 Comment，没有区域标注模型。 |
| F-061 | 播放/新窗口/外部打开 | 部分实现 | “打开所在位置”保留为不自动执行菜单项，复制路径/查看信息可用；统一播放器和新窗口未实现。 |
| F-062 | 幻灯片/随机模式 | 明确延期 | P2 不实现 Viewer 幻灯片或随机播放。 |
| F-063 | 导出到计算机 | 明确延期 | 后置到导出阶段；P2 不复制或移动源文件。 |
| F-064 | eaglepack/专有格式导出 | Eagle 专属不实现 | 不生成 `.eaglepack`。 |
| F-065 | 插件中心 | Eagle 专属不实现 | 不建立第二插件市场。 |
| F-066 | 插件开发者面板 | Eagle 专属不实现 | 不复制插件开发壳。 |
| F-067 | Plugin/Web API | Eagle 专属不实现 | 不开放素材库本地写 API。 |
| F-068 | AI Search/模型/MCP | Eagle 专属不实现 | 本轮及默认路线明确排除。 |
| F-069 | 更新、日志、调试报告 | 沿用已有 | 继续使用宿主日志、通知、错误壳和审计；不增加素材独立更新器。 |
| F-070 | 托盘/开机启动 | Eagle 专属不实现 | 不在素材模块复制宿主级能力。 |
| F-071 | 常用设置 | 部分实现 | P2 workspace JSON 保存浏览状态；独立素材设置页面仍未实现。 |
| F-072 | 左栏设置 | 本 P2 已实现 | 沿用 P1 栏宽/折叠，并增加树展开记忆和活动组织源恢复；专用设置面板不在 P2。 |
| F-073 | 操控/预览设置 | 部分实现 | 缩略图尺寸、视图、排序和滚动锚点可恢复；Viewer 偏好尚未实现。 |
| F-074 | 截图设置 | Eagle 专属不实现 | 自动验收截图不是用户截图产品功能。 |
| F-075 | 快捷键设置 | 部分实现 | 选择/浏览键盘路径与命令层增强，但没有用户可配置快捷键中心。 |
| F-076 | 通知设置 | 部分实现 | 沿用全局任务/通知和可见错误；素材通知偏好未实现。 |
| F-077 | 密码锁 | Eagle 专属不实现 | 使用宿主许可与安全边界，不建第二账号锁。 |
| F-078 | 自动导入设置 | 明确延期 | 没有 watcher，因此也没有 watcher 配置。 |
| F-079 | 开发者设置 | Eagle 专属不实现 | 不新增素材 API token/开发者中心。 |
| F-080 | 许可证激活 | Eagle 专属不实现 | 沿用宿主许可，不复制 Eagle 激活。 |
| F-081 | 设备管理 | Eagle 专属不实现 | 不移植 Eagle 设备管理。 |
| F-082 | 关闭重开恢复 | 本 P2 已实现 | 持久化活动集合、view、sort、共享选择、树展开和各视图滚动锚点；无效/消失 ID 按规则剔除。 |
| F-083 | 空/加载/错误/权限状态 | 本 P2 已实现 | 沿用 P1 页面状态壳并增加组织栏/浏览/缩略图失败/缺失文件状态；权限与不支持格式使用原因化错误而非系统白按钮。 |

覆盖统计（必须由最终代码与证据重新计算）：`83 项：本 P2 已实现 16，部分实现 19，明确延期 22，沿用已有 5，Eagle 专属不实现 21。所有“已实现”均按最后一列边界解释，不把 Eagle 专属或 P3 能力计入。`。

## 5. 自动构建与测试

| 门项 | 最终结果 |
|---|---|
| Debug build（warnings/errors） | `PASS；Debug solution，0 warnings / 0 errors（.validation/QualityGate/debug-build-806e2b7.log）` |
| Release build（warnings/errors） | `PASS；Release solution，0 warnings / 0 errors（.validation/QualityGate/release-build-806e2b7.log）` |
| DevPreview build（warnings/errors） | `PASS；Debug DevPreview + P2 acceptance，0 warnings / 0 errors（.validation/QualityGate/devpreview-build-806e2b7.log）` |
| Core 全量 | `PASS；1197/1197，0 failed，0 skipped（tests/RAWSelectionAssistant.Tests/TestResults/core-full-806e2b7.trx）` |
| WPF 全量 | `PASS；1048/1048，0 failed，0 skipped（tests/RAWSelectionAssistant.WpfTests/TestResults/wpf-full-806e2b7-final.trx）` |
| Modular Harness | `PASS；14/14，0 failed，0 skipped（tests/PixelTart.ModularHarness.Tests/TestResults/harness-full-806e2b7.trx）` |
| P1 automation 回归 | `PASS；59/59，0 failed，0 skipped（tests/RAWSelectionAssistant.WpfTests/TestResults/p1-automation-806e2b7-final.trx；含 37 个 fail-closed 负例）` |
| P2 automation 契约/seam | `PASS；37/37，0 failed，0 skipped（tests/RAWSelectionAssistant.WpfTests/TestResults/p2-all-806e2b7-final.trx；含 36 个 P2 negative-fixture guard 映射、PS5.1 握手和 WPF seam）` |
| P2 DryRun | `PASS；status=ready-for-automated-run，source_head=806e2b78441e0a77ec0722efdc1ae0b3f7baeb8c，devpreview_process_count=0` |
| P2 RecoveryTest | `PASS；status=recovery-test-passed，environment_restored=true，desktop_input_injection=0，display_setting_writes=0` |
| 历史 DPI | `历史证据债保持原值：101 total / 75 passed / 26 failed / 0 skipped；26 项均为缺少既有 AutomatedDpiScreenshotHashes.json/历史文件，不删、不 skip、不伪装修复（.validation/QualityGate/dpi-history-806e2b7-final.log）`（必须如实保留 101 total / 75 passed / 26 failed / 0 skipped 的历史证据债，除非原证据确有改变） |
| 静态安全扫描 | `PASS；P2 包/driver 静态禁用 API、Eagle 写入和桌面驱动命中 0（.validation/QualityGate/static-safety-806e2b7.log）` |

故障注入和 validator 负例必须至少覆盖：证据缺失、字段缺失/类型错误、run id/PID/二进制 hash 不一致、场景乱序、fixture 数量或分布不符、四 DPI 缺项、性能越限、安全计数非零、输入树被修改，以及 DB/WAL/SHM 未清理。最终数字：`P2 contract required_negative_fixtures=36，36/36 有显式 validator guard；P1 fault-injection/场景/边界负例 37/37 通过且输入树不变；P2 WPF/contract 总计 37/37。`。

## 6. 三轮独立 P2 自动验收

每轮必须从同一最终代码 HEAD 建立全新 run root，不复用截图、日志、fixture DB、hash 或 manifest。三轮外部 `ValidateExistingRun` 也必须分别通过。

| 轮次 | 完整 run root | Run | ValidateExistingRun | PID / run id / hash | 场景与截图 |
|---|---|---|---|---|---|
| 1 | `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-175129-52c744fc470f` | `PASS；capture=captured，validator=passed，10/10 scenarios，11 sessions` | `PASS；外部只读验证日志：D:\AI AGENT\worktrees\modular-harness-v1\.validation\QualityGate\p2-final-806e2b7-round1-validate.log` | `run_id=p2-auto-b71014cd7cfe4d218e2fc739589a939c；PID=8060,19004,2412,24724,9420,30192,7688,33308,31892,30536,32824；exe=78b873ea47c2fefdec6b8d24bad8433f1d20b44be94a5c18214369ca1c250e10；app=0e59984d8f26eeb7368832202b58a73624fb7e76d84e42cb6385a1ad8442e398；module=0caba85a2828425f092beb9ed64d3e79faf31243c7484814171ac7ef3454e39d` | `10/10 passed；53 artifacts（bounds15/commands1/databases11/inspectors1/performance2/queries4/screenshots15/selections1/views3）；events=5305；PNG=15；DPI=4 组；cleanup 全 0` |
| 2 | `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-175432-16051d0a12be` | `PASS；capture=captured，validator=passed，10/10 scenarios，11 sessions` | `PASS；外部只读验证日志：D:\AI AGENT\worktrees\modular-harness-v1\.validation\QualityGate\p2-final-806e2b7-round2-validate.log` | `run_id=p2-auto-5a56e1a1d5554f658e3b80bf06f28ba9；PID=23536,24036,31984,14524,22532,23852,25920,31528,3048,10196,29064；exe=78b873ea47c2fefdec6b8d24bad8433f1d20b44be94a5c18214369ca1c250e10；app=0e59984d8f26eeb7368832202b58a73624fb7e76d84e42cb6385a1ad8442e398；module=0caba85a2828425f092beb9ed64d3e79faf31243c7484814171ac7ef3454e39d` | `10/10 passed；53 artifacts（bounds15/commands1/databases11/inspectors1/performance2/queries4/screenshots15/selections1/views3）；events=5305；PNG=15；DPI=4 组；cleanup 全 0` |
| 3 | `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-175733-28fed89eb21a` | `PASS；capture=captured，validator=passed，10/10 scenarios，11 sessions` | `PASS；外部只读验证日志：D:\AI AGENT\worktrees\modular-harness-v1\.validation\QualityGate\p2-final-806e2b7-round3-validate.log` | `run_id=p2-auto-9850fd87ef654a168aab0ee9a4b3d57e；PID=33280,6736,31712,32304,21388,16692,19004,28228,32668,30872,32552；exe=78b873ea47c2fefdec6b8d24bad8433f1d20b44be94a5c18214369ca1c250e10；app=0e59984d8f26eeb7368832202b58a73624fb7e76d84e42cb6385a1ad8442e398；module=0caba85a2828425f092beb9ed64d3e79faf31243c7484814171ac7ef3454e39d` | `10/10 passed；53 artifacts（bounds15/commands1/databases11/inspectors1/performance2/queries4/screenshots15/selections1/views3）；events=5305；PNG=15；DPI=4 组；cleanup 全 0` |

四组模拟布局/DPI 为 1366×768@100%、1920×1080@125%、1920×1080@150%、2560×1440@175%。它们只改变受控 WPF 布局输入，不修改真实 Windows 显示。bounds validator 必须确认无重叠、无裁切、无意外横向滚动条，四视图、上下文菜单和检查器均在安全边界内。


历史失败/诊断根目录均保留且未修改，仅用于解释修复过程，不能与上述三轮拼接：
- `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-123724-59ac0611184d`：旧启动器参数问题。
- `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-131351-91c3fb9f7f11`：旧 PowerShell fixture 编码问题。
- `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-152453-ec8e9b852a71`：选择性能超阈值。
- `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-155619-6227e557d7b7`：旧布局 validator 误判。
- `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-164020-a7a3ce8ada00`：旧 PS5 validator API/日志兼容失败；修复后只读 validator 可通过，但不计入最终三轮。
- `D:\AI AGENT\worktrees\modular-harness-v1\.validation\P2-Automated-Acceptance-20260901-174006-bc4d94f92413`：验证器日志与 sealed root 重叠导致文件锁冲突；该根目录保留为失败证据，修复后重新开始三轮。
证据索引：`每轮均为 53 artifacts：bounds=15、commands=1、databases=11、inspectors=1、performance=2、queries=4、screenshots=15、selections=1、views=3；events.ndjson=5305 行；summary.json 与数据库审计文件均由 validator 逐文件哈希。`。
截图清单与 hash：`每轮 15 张 PNG，validator 已逐文件校验路径、PNG 头、run_id/head 与 SHA256；截图清单聚合 SHA256：轮1=0a055f33f1d6dca6a9b5db6959dd452ffe25d8376775f1b1dff6700f3866838d，轮2=9dbf6424c59428be29f2dda07b78230112b066d3c0579f0ae19014c919d70780，轮3=23d329651be614a9b3e05d6c8abbb7654ac3c97a0d8b35ca5081977528203ba2；完整相对路径均在各 root/app/evidence/screenshots，summary.json 保存逐文件 hash。`。
三轮摘要/审计 hash：轮1 `summary.json=7f80e141123fdfd380b6e6ac9af6021a13abc6e5ef7f4c05fb621d98dc1e0843`、`database-consistency-audit.json=97c8250a88db76352cf193f40c9feae341a29c98b835139907adde0182ba15b5`、`events.ndjson=066493c50d10178cb8fd6e8519c0b7b4f6e402523c08ad1318b16b34a0ca3c1a`；轮2 `summary.json=4ece4120aea647d1fb4a597ac888384e71f71054cb0b6feeccefee2c50de9aa4`、`database-consistency-audit.json=1591f363d0ec749324a8b97620fc4b85659aa2ddc9d279caa1bf235c31f396de`、`events.ndjson=44a4d4962465950f7e9ef1bd856cfd8658b61f3f7e0a3d9465b4d88e4902bff6`；轮3 `summary.json=6ab99c84d8e287e591c2388d7581156685db7c8aadbfb29a0b4bb0d00d82bc42`、`database-consistency-audit.json=557f0e73268b0801ebd8cc298699472f9d06fbf207574b5ec717be1edf762959`、`events.ndjson=8bd746469c18a94605c48a55ac15b720717d69a11182e69c6b6a281bef0ca42c`。

## 7. 性能基线

契约阈值：500+ 素材首屏 ≤ 1500 ms、视图切换 ≤ 250 ms、排序 ≤ 350 ms、选择 100 项 ≤ 250 ms、metadata-only 拖放 100 项 ≤ 750 ms、单次 UI 线程阻塞 ≤ 100 ms。最终值必须来自 sealed run 的 performance snapshot：

| 指标 | 轮 1 | 轮 2 | 轮 3 | 最差值 / 阈值 | 结果 |
|---|---:|---:|---:|---:|---|
| 首屏 | 16.0906 ms | 1.9317 ms | 2.1085 ms | 16.0906 / 1500 ms | PASS |
| 四视图切换 | 107.9784 ms | 63.0686 ms | 65.0396 ms | 107.9784 / 250 ms | PASS |
| 排序 | 204.3314 ms | 204.2032 ms | 213.3612 ms | 213.3612 / 350 ms | PASS |
| 选择 100 项 | 129.2314 ms | 129.6111 ms | 137.7667 ms | 137.7667 / 250 ms | PASS |
| 拖放 100 项 | 331.7885 ms | 351.2817 ms | 328.5482 ms | 351.2817 / 750 ms | PASS |
| UI 线程最大阻塞 | 13.0115 ms | 17.5785 ms | 4.6717 ms | 17.5785 / 100 ms | PASS |

## 8. 数据与操作安全审计

最终静态和运行时审计必须填写以下真实计数；任何一项非零都阻断 P2 完成：

| 禁止项 | 最终计数 |
|---|---:|
| Eagle `.library` / `.eaglepack` 写入 | `0` |
| 用户源文件写入/覆盖 | `0` |
| 用户源文件移动/重命名 | `0` |
| 用户源文件删除/永久删除 | `0` |
| 桌面输入注入 / UIAutomation Invoke | `0` |
| 强制前台/真实显示设置写入 | `0` |

允许的外部行为只有用户主动触发的“复制路径”到剪贴板，以及仅作为不自动执行入口显示的“打开所在位置”。所有归档、缺失、评分、标签、文件夹和从视图移除操作只写 run-owned/产品素材数据库中的 metadata。

## 9. 分阶段提交与回滚点

| 顺序 | 提交 | 完整 SHA | 回滚边界 |
|---:|---|---|---|
| 0 | P1 closed base | `ba25aae315e566db76a5db98a1762d006b91263f` | 回退全部 P2。 |
| 1 | `feat(asset-library): add unified browser state and folder tree` | `f458f536c77465e646d15903015885b1f9a0939f` | 回退 query/sort、系统集合、workspace 状态和 metadata journal 扩展。 |
| 2 | `feat(asset-library): add four virtualized asset views` | `947b5a88222feaebae3aa6b2615cbc0d86df561f` | 回退布局引擎和四视图 UI。 |
| 3 | `feat(asset-library): add shared selection sorting and query commands` | `3d9515502b57e8855ab419f567b9957675fe53df` | 回退共享选择、查询/排序和视图状态衔接。 |
| 4 | `feat(asset-library): add metadata-only drag drop and context commands` | `8a71fd372ec213641e21d0f59343080b607a765f` | 回退应用内拖放和上下文命令层。 |
| 5 | `test(asset-library): add P2 automated acceptance coverage` | `0b076de5a785e7b27c91f470fc45662d2e48bd02` | 回退独立 P2 acceptance 包和测试 seam，不触碰产品数据。 |
| 6 | `docs(asset-library): record P2 Eagle core implementation` | 本报告提交（完整 SHA 在最终 git log/远端核验中回传） | 只回退本报告。 |

所有回滚均应使用提交级 revert；不得 reset/clean 用户工作树。因为 schema 仍为 v6，产品代码回退到 P1 不需要数据库降级。若已产生 v2 journal，旧代码会忽略它不能理解的新操作类型；回滚前仍应保留数据库备份和审计日志，不能手工删除 journal 行。

## 10. 已知风险与 P3 建议

- 四视图虽共享 query/selection，但自定义虚拟化在极端宽高比、超长文件名、混合 DPI 和缩略图持续失败场景仍需依赖三轮 bounds/performance 证据；未填证据前不得关闭风险。
- P2 智能文件夹只提供受支持字段的基础编辑和只读结果；复杂嵌套、任意 AND/OR、锁定/保存当前筛选属于 P3。
- P2 标签组只负责浏览与筛选，不是完整标签管理器；重命名、合并、批量组管理应单独设计并复用 metadata command layer。
- 回收站仍是禁用占位。P3 不应顺手实现永久删除；逻辑回收/恢复应放到后续数据安全阶段，并继续禁止删除源文件。
- 完整 Viewer、媒体播放和格式 registry 不应塞入浏览卡片或检查器；建议后续使用独立 Viewer Registry。
- 资源库切换、导入/导出和 Eagle Adapter 都需要新的安全模型与受控迁移计划，不得借用 P2 拖放命令绕过 Reference-only 边界。
- 旧 v1 journal 没有 after image，只能保持旧撤销兼容；不能通过默认填值伪造重做。新命令使用 v2 journal，相关兼容测试结果见最终测试表。

P3 建议优先级：

1. 通用 query composer：当前/全库搜索范围、建议/历史、文件夹/标签 AND/OR、锁定和保存筛选。
2. 智能文件夹通用规则编辑器：显式嵌套模型、规则预览、迁移和负例 validator。
3. 标签管理器与批量 metadata 编辑：继续复用同一 command/undo/redo 服务。
4. Viewer Registry 和格式降级；与 P2 浏览器保持只读接口，不引入源文件写回。
5. 在独立安全阶段设计逻辑回收/恢复、导入/导出；永久删除继续禁止。

## 11. 最终远端身份

- 分支：`feature/asset-library-eagle-parity-p2`
- 本地验收代码 HEAD：`806e2b78441e0a77ec0722efdc1ae0b3f7baeb8c`
- `git ls-remote` 完整 HEAD：报告提交并推送后实际核验的交付 HEAD（最终回传给出完整 SHA）。
- 远端一致性：报告提交后要求 `git ls-remote source-private refs/heads/feature/asset-library-eagle-parity-p2` 与本地交付 HEAD 完全一致。
- 最终工作树：验收与报告提交前 clean；推送后再次核验 clean。
