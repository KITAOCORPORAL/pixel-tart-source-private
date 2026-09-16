# Pixel Tart Current Development State Report

> Historical state snapshot: this report was prepared from source HEAD `3f8f8450a3b663a2112095eaf722826fa03b974f` immediately before the Asset Library UX Closure Pass. The closure report and its current-HEAD manifests supersede its open UX/preview risks.

日期：2026-09-16  
分支：`integration/pixel-tart-developer-preview`  
当前 HEAD：`3f8f8450a3b663a2112095eaf722826fa03b974f`  
最近提交：`refactor(ux): complete asset library visual workspace pass`

## 1. 结论

Pixel Tart 当前不是“继续扩功能”的阶段，而是 **RC12 已完成工程门禁后、实机验收仍未关闭、同时叠加了一轮尚未重新封版的 Asset Library UX 修订**。

更准确地说，仓库同时存在三条事实：

1. RC12 在历史产品源 HEAD `4364b458` 上完成了产品集成、WPF 隔离、DPI、视觉和 10K/50K/100K 性能门禁；安装包由后续干净 packaging HEAD `3639f7e` 构建。
2. `RC12_BASELINE_FREEZE.md` 与 `RC12_REAL_MACHINE_ACCEPTANCE.md` 仍明确记录 `REAL_MACHINE_ACCEPTANCE = PENDING`，因此不能称为 production-ready，也不能 merge `main` 或创建正式 release tag。
3. 当前 HEAD `3f8f845` 已晚于上述安装包和视觉证据，加入查看大图的原图加载、Quick Loupe、灵感板合并、卡片比例/分辨率、Inspector 星级和统一筛选中的颜色入口。这些变更已能编译并通过定向测试，但尚未进入新的安装包、完整 WPF 隔离、50 图视觉证据或真人实机验收。

因此当前产品阶段应定义为：

> **RC12 Post-Freeze UX Candidate / Real-Machine Acceptance Pending**  
> RC12 基础能力已封口；当前源码是未重新封版的 UX 候选，不是已验收安装包，也不是 RC13。

## 2. 审计范围与证据规则

本报告读取并交叉核对：

- `docs/implementation-reports/`：P1–P3.6、RC10–RC12、UX Simplification、视觉与实机验收记录。
- `docs/design/`：Eagle parity、Context Menu、产品语言、UI 字符串与设计系统规则。
- `docs/product/eagle-reference/`：Eagle 行为、Pixel Tart 映射、缺口与交互基线。
- 当前产品源码、模块注册、工具目录以及最近提交 `3f8f845` 的完整 diff。

状态判断遵循仓库已有规则：只有 UI、命令、持久化/重载和当前测试相互吻合时才记为完成。旧文档中的“未完成”若已被 RC11/RC12 后续代码关闭，不继续列为缺失；反过来，旧 HEAD 的截图、性能和安装包证据也不自动继承给当前 HEAD。

本次对当前 HEAD 的补充验证：

- `dotnet build RAWSelectionAssistant.sln -c Release --no-restore`：通过，0 warning / 0 error。
- 最近 Asset Library UX 变更定向 WPF 测试：21/21 通过，0 skipped。
- 未在本次报告中重跑完整 1,182 项进程隔离套件、Product Visual Harness、100/125/150/200% DPI 证据或 10K/50K/100K 性能门禁。

## 3. 当前产品阶段

### 3.1 已经完成的阶段

- P1：素材库一级导航、三栏壳与自动验收基础。
- P2：Eagle 式组织、四布局、分页/虚拟化、多选和命令层。
- P3：统一 Query AST、Smart Folder、标签管理、批量 metadata 与 durable undo。
- P3.5：`.ptlibrary` 可迁移素材库、切库、写租约与专注工作区。
- P3.6：Eagle 式操作壳、右键菜单、灵感临时收集。
- RC10–RC12：项目/拍摄关系、日历双向联动、灵感集合、磁盘预览缓存、最近素材库、EXIF、上下文菜单、视觉证据和规模性能门禁。
- RC12 UX Simplification：整理图片、RAW 转 JPG、Smart Folder、Inspector、Task Center 和错误语言的产品化减法。

### 3.2 当前进行中的阶段

当前真正进行的是两件事，而不是新功能开发：

1. **实机验收与基线重建**：`RC12_REAL_MACHINE_ACCEPTANCE.md` 的启动/升级、素材库、Viewer、项目/拍摄、日历、灵感、离线缓存、最近素材库、右键菜单、EXIF 和 DPI 清单仍未由用户勾选。
2. **Asset Library UX Priority Override 收口**：最近两次提交重排 Context Menu，并在 `3f8f845` 中完成一批视觉工作区改造，但仍有需求与实际实现不一致，见第 6 节。

## 4. 已完成模块

### 4.1 素材库核心：完成度最高，属于当前主产品

| 能力 | 当前事实 | 主要实现/证据 |
|---|---|---|
| 素材库容器 | `.ptlibrary`、LibraryId、最近素材库、在线/离线状态、切库、写租约、包导入合并 | `AssetLibraryContainerService`、`AssetLibraryWorkspaceHost`；RC12 Recent Libraries 与 safe switching 测试 |
| 导入与文件安全 | Reference / Managed Copy、内容 hash、重复处理、资源管理器拖入；不修改 Reference 源文件 | `SqliteAssetLibraryRepository.ImportAsync`、`ImportDroppedFilesAsync` |
| 查询与筛选基础 | 参数化查询、分页游标、搜索防抖、IME、历史、组合条件、范围和 Smart Folder 共用 Query AST | `AssetLibraryViewModel.P3QueryComposer.cs`、schema v7 |
| 文件夹与标签 | 层级、创建/重命名/移动/排序/归档、批量成员关系、标签组与合并 | P2/P3 repository + WPF 测试 |
| 四种布局 | Grid、Masonry、Justified、List，虚拟化、布局/选择锚点 | `AssetLayoutEngine`、`VirtualizingAssetPanel` |
| 多选与批量操作 | 扩展选择、框选、右键选择策略、跨页成员校验、批量评分/工作状态/归档/恢复/回收站 | P2/P3 tests、RC12 context end-to-end |
| Inspector | 真实格式、大小、位置、EXIF、项目、拍摄、客户、工作状态；当前增加五颗可操作星级 | `AssetLibraryViewModel`、`AssetLibraryPage.xaml` |
| 项目 / 拍摄 / 客户 | 多对多持久关系、暗色 picker、重启恢复、客户解析 | `AssetLibraryProductRelationEndToEndTests` |
| 日历双向联动 | Booking Detail 素材条、Project/Booking 过滤、素材侧“查看拍摄” | RC12 Calendar round-trip evidence |
| 灵感板基础 | 临时收集、命名集合、CRUD、项目关联、拖放/重排、归档、重启持久化 | `SqliteInspirationTrayService`；当前 UI 已合并为“灵感板” |
| 生命周期 | Archive / Restore / recoverable Trash / undo / redo；不开放永久删除 | Eagle parity matrix、Context Menu end-to-end |
| 导出 | 原文件副本、托管副本、metadata CSV；冲突自动编号、不覆盖源 | RC10/RC12 export contracts |
| 共享预览缓存 | 64 MiB memory + 512 MiB disk LRU，content-hash key，离线/重启缓存 | RC11 preview cache implementation/tests |
| 视觉分析 | 本地像素分析、颜色/配色/相似搜索、缓存和任务桥 | `LocalPixelVisualAnalysisProvider`、`WpfVisualAnalysisDecoder` |

### 4.2 最近提交新增或重做的素材库能力

`3f8f845` 修改 25 个文件，增加 889 行、删除 138 行。实际交付包括：

- `AssetViewerWindow` 从只显示 512px 首帧，改为“首帧立即显示 → 后台解码源图 → 无闪烁替换”，并加入 Fit、100%、滚轮、平移、前后张、Esc、邻图预载与三项缓存。
- 新增 `AssetQuickLoupePreviewProvider`，悬停 420ms 后加载最长边 1600 的预览，缓存 4 项。
- Grid 改为按每张图片比例计算高度；Grid/Masonry/Justified 卡片改用透明舞台和 `UniformToFill`，并显示真实 `宽 × 高`。
- 系统集合增加图标与实时数量；文件夹树行高、图标、层级和数字样式收紧。
- Inspector 增加五颗可点击星级；再次点击当前星级可清零。
- 原“灵感托盘 + 灵感集”视觉上合并为“灵感板”，临时收集成为其中的特殊入口。
- Smart Filter 面板增加颜色预设和 Hex 输入；搜索防抖从 280ms 调为 180ms。
- 增加 12 个针对集合计数、Folder Tree、Full Preview、无黑边、Loupe、分辨率、按钮对比度、星级、灵感板与颜色筛选的测试文件。

这些变更的代码和定向测试存在，但还没有对应当前 HEAD 的产品截图/安装包/实机证据。

### 4.3 其他可用产品模块

| 模块 | 当前判断 | 依据与边界 |
|---|---|---|
| 工作台 | 已形成主入口 | 快捷工具、项目摘要、日历摘要和任务中心均在 `MainWindow` / `MainViewModel` 中接线。 |
| 工作日历 | 已形成可用工作区 | 月/列表/总览、排期、提醒、资料、归档恢复、Asset 关联和双向导航已有实现及 RC12 证据。 |
| 联机拍摄 | 已有实质功能 | 文件夹看守、RAW/JPG 配对、代理、监看、直方图、比较、客户屏、标记、任务中心和恢复逻辑均存在；并非“相机 SDK 直连”，主要是看守目录工作流。 |
| 整理图片 | Production | 已完成产品语言简化，保留预览和安全复制语义。 |
| RAW 转 JPG | Production | 输出位置、尺寸、质量为主路径，高级选项折叠；只读解码并保留源文件。 |
| 批量压缩 | Production | 在 `ProductToolboxPolicy.ProductionCatalog` 中开放，并接入简化后的任务与错误语言。 |
| 拼图 | Production | 在正式工具目录中可用；本轮文档没有对其做与素材库同等级的最新专项验收。 |
| 在线选片本地流程 | 部分可用 | 实际 WPF 页面和本地草稿/导入/选择/同步工作区存在；外部在线服务可未配置，独立 TXT/CSV 导出仍显示“准备中”。模块层仍保留不可见 placeholder route，说明新模块架构和旧主程序接线尚未完全统一。 |

## 5. 仍缺失或未完成模块

### 5.1 明确未实现，不应在下一步偷偷扩展

以下项目在 `CREATIVE_ASSET_ARCHITECTURE_PREPARATION.md` 中只有合同或规划，不是产品模块：

- Moodboard 页面、自由画布、编辑器、分组和持久化。
- Planning Center / 项目策划中心及其独立持久化。
- 创作中心一级导航。
- Browser/Web Clipper。
- AI 搜索、AI 生成工作流与 MCP。
- DeliveryBatch、完整精修流程。
- `ICreativeAssetReferenceResolver` 的正式活动库/离线库/hash mismatch/包合并映射解析器。

`CreativeAssetContracts.cs` 中出现 `MoodboardItem`、`ProjectPlanningReference` 或 resolver 接口，只代表领域契约已预留，不能算功能完成。

### 5.2 已露出界面但仍是 Preview / Placeholder

- 批量水印：只能浏览布局预览，添加图片和批量导出仍禁用。
- 删废片：安全说明和空界面存在，回收站/批量删除仍为开发中；它与素材库内已经完成的 recoverable Trash 不是同一条完整工具流。
- FTP：表单存在，测试连接和上传禁用。
- 批量重命名：静态规则/结果预览存在，执行禁用。
- 通用批量转档：设置壳存在，开始转换禁用；不要与已可用的 RAW 转 JPG 混为一谈。
- 检查更新：禁用。
- 授权/购买：生产授权服务和购买地址仍可能未配置。
- 在线选片：云服务可未配置，独立导出未开放；当前可依赖的是本地原型和同步归片工作区。

### 5.3 素材库内仍未闭合的能力

1. **普通导入没有统一长任务契约。** `ImportAsync`、拖入和 demo import 仍直接调用 repository；没有把扫描、hash、索引、成功/重复/失败、取消和目标 LibraryId 统一发布到 Task Center，也没有像 P3 操作一样显式 drain。
2. **快速分类快捷键仍是提示，不是完整交互。** `FocusFolderClassifier()` 只设置状态文字；`T` 只提示用户输入标签，没有真正聚焦可键盘操作的分类器/标签器。
3. **特殊格式能力矩阵未统一。** Import allowlist、repository media classification、thumbnail provider、Viewer 和 visual analyzer 对 RAW/PSD/PSB/SVG/GIF/视频/PDF/字体的能力边界并不一致。
4. **当前“全分辨率”只对 WPF 可直接解码的源文件成立。** 最近测试只验证 PNG。RAW 仍依赖首帧 provider；直接 `BitmapImage` 打开 RAW 失败后会保留低分辨率首帧，尚未证明 embedded full-size preview / high-quality proxy。
5. **颜色筛选并未达到需求中的完整 Color Picker。** 当前是七个预设色 + Hex 输入；没有完整二维色板、Hue slider，也没有在该新面板中露出“窄—宽”容差滑杆。
6. **“视觉筛选”仍保留独立入口。** `AssetLibraryPage.cs` 的 More 菜单仍创建 `Header = "视觉筛选"` 子菜单，与“全部筛选统一为一个入口”的目标冲突。
7. **Quick Loupe 交互与设计稿不一致。** 当前是进入任意卡片后 420ms 自动弹出；没有缩略图右下角的隐藏放大镜按钮，也没有“只在进入放大镜时出现”的明确意图边界。
8. **最新 UX 状态未进入截图 harness。** 用户要求的 `01_asset_clean` 至 `10_asset_full_preview` 当前没有一套绑定 `3f8f845` 的新 manifest 和审计报告。

## 6. 当前最大 UX 问题

### 6.1 最大问题：产品状态不一致，而不是缺少更多控件

当前最严重的 UX 问题是 **同一个产品里同时存在成熟工作区、半成品工具壳和已经改变但未重新验收的素材库体验**。

具体表现：

- 素材库已经是高密度、可用的核心工作区，但工具箱仍公开 Watermark、Delete Rejects、FTP、Batch Rename、Batch Convert 等禁用或静态演示页面。用户会把它们理解为“坏掉的功能”，而不是路线图。
- 在线选片同时存在真实 WPF 页面、未配置在线 provider、准备中的导出，以及模块层的不可见 placeholder route；产品边界不清楚。
- 最新素材库改动声称“统一筛选”和“Quick Loupe”，但实际仍有独立“视觉筛选”，Loupe 也会在整张卡片悬停后自动弹出，容易遮挡连续浏览。
- 颜色筛选的视觉表达仍是按钮预设 + 文本框，距离摄影师预期的二维色板、Hue 和容差控制还有明显差距。
- 旧 RC12 截图显示的是 `4364b458` 的 Tray/Collection、旧 Viewer 和旧卡片；当前 `3f8f845` 已改变这些关键表面。团队若继续引用旧截图，会造成“文档里已通过，用户看到却不同”的验收错位。

因此下一步 UX 工作不应再添加页面，而应：隐藏或移出未完成工具、完成统一筛选和 Loupe 的真实交互、用当前 HEAD 重做视觉/实机验收。

## 7. 当前最大架构风险

### 7.1 最大风险：Asset Library 已形成第二套图片加载路径并继续集中在巨型 ViewModel/Page

仓库此前明确要求所有图片表面复用 `IAssetThumbnailProvider`、稳定引用和统一缓存。但最近提交新增：

- `AssetViewerWindow.LoadFullResolutionAsync` 直接用 `BitmapImage` 打开源路径并维护自己的全分辨率字典缓存。
- `AssetQuickLoupePreviewProvider` 再次直接用 `BitmapImage` 打开 `ThumbnailPath`，维护另一套 4 项缓存。
- `AssetVisualMatchView.ThumbnailPath` 只在 Managed Copy 与 SourcePath 之间选择，不表达 cached preview、RAW embedded preview、代理类型或解析状态。

后果包括：

1. RAW、PSD、视频等格式在 Gallery 可有 provider 预览，但 Viewer/Loupe 可能直接解码失败，形成同一素材在不同表面能力不一致。
2. Viewer 最多缓存当前和相邻两张完整 Bitmap；缓存按“张数”而不是按字节预算。三张高像素、16-bit 或 TIFF 图像可能瞬间占用数百 MB，现有 RC12 working-set 性能结论不能覆盖这条新路径。
3. 离线磁盘缓存、content-hash 失效、代理版本和 stable reference 解析无法自然复用。
4. Page code-behind 和 ViewModel 继续承担视图弹层、查询、导入、关系、灵感、分析和状态刷新。仅六个 `AssetLibraryViewModel*` partial 已超过 6,000 行；`AssetLibraryViewModel.cs` 2019 行、`AssetLibraryViewModel.P2Browser.cs` 1759 行。最近功能继续向这里堆叠，会提高回归和切库竞态风险。

这比“缺一个功能”更危险，因为它会让 Gallery、Loupe、Viewer、灵感板、日历条和未来 Moodboard 各自演化成不同的图片能力栈。

### 7.2 次级架构风险

- `RefreshSystemCollectionCountsAsync` 在每次素材刷新后并行查询所有系统集合数量；旧 100K 性能证据早于此改动，需重新量测查询次数和数据库锁竞争。
- `AssetLibraryModule` 已声明 task/provider/capability，但主程序仍在 `App.xaml.cs` 手工创建多种 service/viewmodel；Online Selection 模块 route 还是不可见 placeholder。模块架构与实际 composition root 存在双轨。
- 普通导入未绑定 TaskOperationBridge 和 LibraryId 生命周期，仍是切库、取消和关闭时最值得优先防守的跨库写风险面。
- 当前报告、冻结文件、安装包、截图和源码分别指向多个 HEAD；如果没有 manifest 驱动的“source HEAD = artifact HEAD”检查，发布证据容易再次漂移。

## 8. 下一阶段开发优先级

### P0 — 重新建立当前 HEAD 的可信候选，不加功能

1. 为 `3f8f845` 或其最小修复后继提交运行完整 Release、进程隔离 WPF、Product Visual Harness、100/125/150/200% DPI 和 10K/50K/100K 性能门禁。
2. 生成新的十张 Asset Library 状态图：clean、context menu、submenu、folder tree、filter color、inspector rating、inspiration board、loupe idle/active、full preview。
3. 构建新的 RC12 UX 候选安装包，manifest 必须记录当前 source HEAD；旧 `3639f7e` 安装包保留为历史，不覆盖证据。
4. 按 `RC12_REAL_MACHINE_ACCEPTANCE.md` 完成真实 Windows 安装/升级/使用验收。未完成前继续保持 `REAL_MACHINE_ACCEPTANCE=PENDING`。

### P1 — 修正当前 UX Override 与实现不一致

1. 把 Quick Loupe 改为卡片右下角显式放大镜 hover target；离开即关，不在普通卡片停留时自动打断浏览。
2. 删除 More 中独立“视觉筛选”，把颜色、视觉特征、相似图片完全放入统一筛选面板。
3. 补齐二维色板、Hue、Hex Enter 和可见的颜色范围滑杆；色号可实时更新，重查询保持 50–100ms 有界 throttle/debounce。
4. 检查 Grid 的 `UniformToFill` 是否造成用户不可接受的裁切。需求是无人工黑边且保留真实比例；当前“按比例计算容器 + Fill 裁切”需要真实横/竖/超宽/超长图截图验证，不能只靠字符串测试。
5. 对 Context Menu 子菜单、Folder Tree 拖放/嵌套、Inspector 星级和灵感板用真实操作做回归，而不只验证 XAML token。

### P1 — 统一图片解码、代理与内存策略

1. 在统一 provider 上增加 Preview Purpose/Quality（Gallery、Loupe、Viewer、Original）和字节预算，不让 Viewer/Loupe直接各建一套路径缓存。
2. Viewer 使用源图、embedded RAW full preview 或高质量 proxy 的明确优先级；失败时显示可操作原因，并保留“默认程序打开/在资源管理器中显示”。
3. 以字节而不是张数限制 full-resolution cache；预载必须尊重取消、窗口关闭和内存压力。
4. 建立格式能力登记：每种格式分别标记可索引、可抽元数据、可缩略图、可 Loupe、可 Viewer、可视觉分析、可外部打开。

### P1 — 关闭导入任务与切库生命周期缺口

1. 普通导入接入 Task Center：扫描、hash、索引/复制、成功/重复/失败、取消和部分完成。
2. 每个任务冻结 `(LibraryId, operation scope)`，切库/关闭必须 wait、cancel+drain 或安全绑定旧库继续，禁止结果发布到新页。
3. 让 F/T 快捷键进入真实可聚焦、键盘可操作、可取消的 Folder/Tag 分类器。

### P2 — 清理产品公开面

1. ProductionCatalog 只保留真正可完成主任务的工具。Preview/Coming Soon 页面从默认工具箱移出，避免“开发中”控件成为主要 UX。
2. 统一 Online Selection 的模块注册与主程序 composition；明确“本地选片可用、云服务可选、哪些导出未开放”。
3. 拆分 Asset Library orchestration：Viewer/Loupe service、relation service、inspiration service、system-count service、import coordinator 与页面状态分离；ViewModel 只组合可观察状态和命令。
4. 为 source HEAD、installer、visual manifest、performance manifest 建立自动一致性检查。

### P3 — 只有 RC12 实机关闭后再讨论

- `ICreativeAssetReferenceResolver` 与 AssetOrigin 正式迁移。
- Moodboard / Planning Center / Browser Clipper / AI / MCP。
- 更广的特殊格式、视频播放和跨应用编辑版本。

这些不应抢在 RC12 当前候选的证据重建、图片管线统一和导入生命周期之前。

## 9. 建议的下一阶段停止条件

下一阶段只有同时满足以下条件才可结束：

- 当前 source HEAD 与安装包、视觉 manifest、性能 manifest 完全一致。
- 新 Asset Library 十状态截图通过，且 100/125/150/200% 无裁切/溢出。
- JPG/PNG/TIFF 与至少一类真实 RAW 的 Gallery → Loupe → Viewer 能力路径一致；Viewer 不放大 Gallery thumbnail 冒充高清。
- Viewer/Loupe 有统一 provider 和可证明的内存上限。
- “视觉筛选”不再作为第二入口，颜色筛选包含色板/Hue/Hex/范围并流畅更新。
- 普通导入可取消、可报告进度、切库不跨库写。
- 默认工具箱不再把禁用开发页当成正式能力展示。
- 用户完成 `RC12_REAL_MACHINE_ACCEPTANCE.md` 并明确给出通过结论。

在此之前，最正确的产品策略仍是：**不创建 RC13、不进入新创作功能，先把当前 RC12 UX 候选变成可安装、可验证、可由摄影师稳定使用的同一份产品。**
