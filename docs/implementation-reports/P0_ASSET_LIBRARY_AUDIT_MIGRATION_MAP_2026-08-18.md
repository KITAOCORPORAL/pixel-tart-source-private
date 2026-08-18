# 素材库 Eagle 等价迁移 P0 审计与迁移地图

日期：2026-08-18
阶段：P0（只读审计；P1 尚未计入）
执行分支：`feature/modular-harness-v1`
执行前 HEAD：`5be53f6393bba1069e921476bff976257d4f8505`
执行前工作区：clean

## 1. 本阶段目标与实际完成范围

本阶段完整审阅了开发指令、仓库 README、最新 Modular Harness/Asset Library 报告与交接状态，并逐篇审阅 Eagle 分析报告 `README.md`、`01_feature_inventory.md` 至 `08_learning_notes.md`。完成内容：

- 找出素材库入口、路由、宿主、View/ViewModel、模型、服务、数据库、缓存、测试与已知缺口。
- 建立“保留 / 迁移 / 合并 / 删除入口但保留数据 / 暂缓”迁移地图。
- 建立 F-001～F-083 对照矩阵。
- 执行修改前正式 Debug 构建和完整测试基线。
- 确认 P1 不需要产品 Schema 迁移、不可逆文件操作或用户数据覆盖，可以继续实施。

本阶段没有修改产品代码、数据库、用户文件或运行时设置。

## 2. 执行前基线

使用仓库正式 `build_debug.ps1` 路径（Debug x64 build，然后同配置全 solution test）验证：

| 项目 | 结果 |
|---|---:|
| .NET SDK | 10.0.302 |
| Debug build | PASS，0 warnings / 0 errors，17.53 s |
| 全部测试 | 2095 total / 2068 passed / 27 failed / 0 skipped |
| Modular Harness | 14/14 passed |
| WPF | 792/792 passed |
| DPI | 75/101 passed，26 failed |
| Core | 1187/1188 passed，1 failed |
| 总耗时 | 63.347 s |

失败证据没有被隐藏：

- 26 个 DPI 失败都来自既有 `artifacts/automated-dpi-review/2.0.4/*.json` 证据缺失，不是本阶段修改引入。
- Core 唯一失败是 `Version220AcceptanceIsolationTests.AcceptanceExecutable_UsesExplicitIsolatedAppDataRootOnly` 仍断言旧错误文案；实现已扩展为 Acceptance + Modular Harness Dev Preview 的隔离根提示。
- 运行后仓库仍 clean。

## 3. 现状清单与文件职责

| 范围 | 当前文件/组件 | 审计结论 |
|---|---|---|
| 全局入口 | `MainWindow.xaml` | 一级导航没有素材库；WorkBench 工具箱弹窗与工具箱全页各有一个重复素材库入口。 |
| Shell 状态 | `MainViewModel.cs` | `AssetLibrary` 已是可导航 Surface，但启动无条件回 Workbench；没有安全一级页恢复。 |
| 正式模块 | `AssetLibraryModule.cs` | WorkspaceModule、route=`asset-library`、8 项能力、全局 Task/Settings 契约均已存在；NavigationGroup 仍是 toolbox。 |
| 同窗宿主 | `ModuleWorkspaceHost.cs` | 同一 MainWindow 内创建 UserControl；无独立 Window/进程；缺 missing-route、factory-error、loading/retry 壳。 |
| 页面 | `AssetLibraryPage.xaml` / `.cs` | 已有 220/*/330 三列、虚拟网格、检查器与视觉分析；列宽写死、无 splitter/折叠/恢复，仍像嵌套卡片。 |
| ViewModel | `AssetLibraryViewModel.cs` | 查询、导入、分类、Smart Folder、视觉筛选/相似、全局批任务已接通；缺页面状态持久化和明确 loading/empty/error 壳。 |
| 领域模型 | `AssetLibraryModels.cs`、`AssetLibraryV15Models.cs` | 素材、文件夹、标签、Smart Folder、查询模型可复用。 |
| 仓储 | `SqliteAssetLibraryRepository*.cs` | 引用导入、哈希去重、多文件夹/标签、Smart Folder、重链、撤销 journal、分页查询可复用。 |
| 视觉分析 | `VisualAnalysis/*`、`WpfVisualAnalysisDecoder.cs` | 颜色/直方图/影调/相似查询与缓存可复用；ICC/RAW 完整代理仍未完成。 |
| 缓存 | `AsyncThumbnail.cs`、视觉缓存表 | 64 MiB LRU 缩略图与版本化分析缓存可复用。 |
| 数据 | `asset-library-v16.db` | 私有素材库数据库与 14 张表全部保留；不改正式产品 Schema 5，也不删除 Asset 内部 schema v6 的任何行。 |
| 测试 | Asset V1/V1.5/V1.6、Visual、Embedded WPF、Modular Harness | 已有核心与同窗回归；现有 evidence contract 反向要求两个工具箱入口，P1 必须同步改成唯一一级入口。 |

## 4. 迁移地图

### 保留

- 单一 `MainWindow`、全局导航、全局任务中心、Single Close Authority、主题、日志、通知、错误处理。
- `pixel-tart.asset-library` ModuleId、`asset-library` route、8 项公开能力、Selection 适配契约。
- `asset-library-v16.db`、14 张素材表、全部索引与用户数据；正式产品 Schema 5 不变。
- 引用式导入（默认不移动/不改名/不删除源文件）、哈希去重、多文件夹、多标签、Smart Folder、重链、撤销 journal。
- 视觉分析、缓存、颜色/配色/相似查询、分页、虚拟化和既有 100K 验收能力。

### 迁移

- 把两个工具箱素材库入口迁移到全局导航第 2 位，复用原 route/ViewModel/DB，不复制服务。
- 模块导航归属由 toolbox 改为 primary；`AssetLibrary` 与 `asset-library` 都规范化到同一 Shell Surface。
- 把固定 220/*/330 页面迁为占满内容区的可调、可折叠、可恢复三栏壳。
- 把局部“返回工作台”职责迁回全局一级导航和 Shell X。
- 将最后一级页、左右栏宽度/折叠、缩略图尺寸接入现有原子 JSON 设置；无数据库迁移。
- 将现有搜索、筛选、Smart Folder、视觉分析和批任务原样嵌入新壳，不重写服务。

### 合并

- 工具条、菜单、右键和快捷键逐步合并到同一命令层；P1 只处理导航/焦点/壳层命令。
- 素材长任务继续合并到全局 Task Center；不得新增第二套任务系统。
- 主题、设置、日志、通知和错误处理继续使用宿主体系。
- 未来把重叠的 PhotoOrganize 安全整理能力合入素材库；等价验证前不删除其数据/恢复配置。

### 删除入口但保留数据

- P1 一级入口验证后移除 `ToolboxAssetLibraryEntry` 与 `ToolboxPageAssetLibraryEntry` 两个可见重复入口。
- 移除素材页局部“返回工作台”按钮；保留 page、route、状态和数据库。
- 当前分支没有素材库独立 Window/EXE，故没有可删除的独立应用；DevPreview 诊断仍只在编译门控下存在。
- 任何时候都不删除 `asset-library-v16.db`、用户源文件、旧设置 ID 或恢复记录。

### 暂缓

- Eagle 专属欢迎/激活/设备/商店/更新器、`.library` 合并、eaglepack、站点抓取、浏览器扩展、Webview、插件中心/Web API、密码锁、AI/MCP。
- P2：完整左栏组织、四布局、排序、框选/拖放/上下文闭环。
- P3：全库/当前搜索、建议/历史、通用筛选 AND/OR、锁定/保存查询。
- P4：Viewer Registry、多格式降级、完整单选/多选检查器。
- P5：文件夹/剪贴板/拖放导入、批量预演、逻辑回收/恢复、导出。
- P6：受控 EagleAdapter。永久删除不是“暂缓”，而是明确禁止。

## 5. 用户可见迁移路径（P1 设计输入）

目标一级顺序固定为：工作台 → 素材库 → 归片工作区 → 工作日历 → 联机拍摄 → 摄影收支 → 项目历史。在线选片保留 route 和页面，但退出一级导航；工具箱继续承载工具，不再重复承载素材库。

旧入口或命令参数 `AssetLibrary`、正式模块 route `asset-library` 都必须进入同一一级页面。进入后仍是同一个窗口、同一个左侧全局导航与同一个底部任务中心。页面内部只保留左组织 / 中素材 / 右检查器三栏。

## 6. F-001～F-083 对照矩阵

状态统计：适用 62 项、Eagle 独立产品专属 21 项；已存在 3、部分完成 38、缺失 17、本轮暂缓 25。暂缓项包含 21 项独立产品能力，以及 4 项适用但后置的能力（F-018/F-042/F-050/F-062）。

| ID | 功能 | 适用性 | 当前状态 | P0 判定 |
|---|---|---|---|---|
| F-001 | 启动应用 | 适用 | 已存在 | 单应用、同窗 route 已成立。 |
| F-002 | 首次欢迎引导 | Eagle 专属 | 暂缓 | 保留 Pixel Tart 全局 onboarding，不建素材欢迎页。 |
| F-003 | 主题选择 | 适用 | 已存在 | 动态资源继承宿主主题。 |
| F-004 | 创建资源库 | 适用 | 缺失 | 当前固定单库路径。 |
| F-005 | 打开/切换资源库 | 适用 | 缺失 | 无库历史/切换契约。 |
| F-006 | 合并资源库 | Eagle 专属 | 暂缓 | 不直接合并 Eagle `.library`。 |
| F-007 | 清缓存并重载 | 适用 | 部分完成 | 有缓存与刷新，无完整清理/重建入口。 |
| F-008 | 侧栏基础视图 | 适用 | 部分完成 | 后端集合多于当前可见入口；缺随机/全部标签/回收站。 |
| F-009 | 快速访问 | 适用 | 部分完成 | 有运行期 favorite/recent，缺完整持久管理。 |
| F-010 | 文件夹树 | 适用 | 部分完成 | 后端有树/计数/移动/排序，UI 仍平铺。 |
| F-011 | 智能文件夹树 | 适用 | 部分完成 | 可保存/列出，缺树分组/克隆/快捷访问。 |
| F-012 | 侧栏搜索/过滤 | 适用 | 部分完成 | VM 有 FolderSearch，页面未形成底部过滤框。 |
| F-013 | 本地文件导入 | 适用 | 部分完成 | 文件选择+引用导入可用，缺拖放与完整结果闭环。 |
| F-014 | 本地文件夹导入 | 适用 | 部分完成 | 递归导入仅预览/验收 seam，生产 UI 无入口。 |
| F-015 | Eagle 素材包导入 | Eagle 专属 | 暂缓 | `.eaglepack` 不移植。 |
| F-016 | 链接/书签导入 | Eagle 专属 | 暂缓 | 不属于素材核心阶段。 |
| F-017 | ArtStation/花瓣导入 | Eagle 专属 | 暂缓 | 不复制站点抓取器。 |
| F-018 | 屏幕截图 | 适用 | 暂缓 | 后置收集来源，不在 P1。 |
| F-019 | 自动导入监视目录 | 适用 | 缺失 | 素材库无 watcher。 |
| F-020 | 浏览器扩展采集 | Eagle 专属 | 暂缓 | 不建第二本地服务。 |
| F-021 | 剪贴板导入 | 适用 | 缺失 | 无素材剪贴板命令。 |
| F-022 | 新建文件夹/子文件夹 | 适用 | 部分完成 | 命令/仓储存在，树 UI 绑定不完整。 |
| F-023 | 新建智能文件夹 | 适用 | 部分完成 | 固定 AND 视觉构造器可用，缺通用编辑器。 |
| F-024 | 智能文件夹群组 | 适用 | 缺失 | 无领域模型。 |
| F-025 | 文件夹重命名/移动/排序 | 适用 | 部分完成 | 仓储存在，UI 未闭环。 |
| F-026 | 文件夹密码保护 | Eagle 专属 | 暂缓 | 不复制 Base64 密码技术债。 |
| F-027 | 快速访问/封面/图标 | 适用 | 部分完成 | Folder 有 Icon/Color，缺完整管理 UI。 |
| F-028 | 评分与标签 | 适用 | 部分完成 | 单批更新与撤销存在，可见编辑入口不完整。 |
| F-029 | 标签管理器 | 适用 | 部分完成 | 后端有搜索/重命名/合并/归档，无完整管理 UI。 |
| F-030 | 标签组 | 适用 | 部分完成 | 模型/DB 支持，UI 未完成。 |
| F-031 | 批量重命名 | 适用 | 缺失 | 素材命令层未实现。 |
| F-032 | 批量动作 | 适用 | 部分完成 | 评分/标签/文件夹/分析存在，缺统一预演/报告。 |
| F-033 | 回收站与恢复 | 适用 | 缺失 | 资产无逻辑回收 API/入口。 |
| F-034 | 当前/全部搜索 | 适用 | 部分完成 | 有防抖搜索，缺显式搜索范围。 |
| F-035 | 搜索建议与历史 | 适用 | 缺失 | 仅有视觉查询 history。 |
| F-036 | 文件夹筛选 | 适用 | 部分完成 | 单 FolderId，缺组合。 |
| F-037 | 标签筛选 | 适用 | 部分完成 | 单 TagId，缺 AND/OR。 |
| F-038 | 颜色/形状筛选 | 适用 | 部分完成 | 颜色/配色较完整，形状/比例不完整。 |
| F-039 | 评分/日期/大小筛选 | 适用 | 部分完成 | 查询能力存在，UI 不完整。 |
| F-040 | 格式/尺寸/时长筛选 | 适用 | 部分完成 | 部分字段存在，无完整 UI/时长。 |
| F-041 | 注释/标注/链接筛选 | 适用 | 部分完成 | Comment 可检索，区域/链接模型缺失。 |
| F-042 | 语义/以图筛选 | 适用 | 暂缓 | 不接不稳定 AI/MCP/反向图片能力。 |
| F-043 | 反向图片搜索 | Eagle 专属 | 暂缓 | 明确排除第三方上传。 |
| F-044 | 保存/锁定筛选 | 适用 | 部分完成 | Smart Folder 可保存部分规则，缺锁定状态。 |
| F-045 | 排序与布局信息 | 适用 | 部分完成 | 只有缩略图滑杆/单网格。 |
| F-046 | 重复文件扫描 | 适用 | 部分完成 | 导入哈希去重存在，缺扫描比较流程。 |
| F-047 | 四种布局 | 适用 | 部分完成 | 仅虚拟化 Wrap 网格。 |
| F-048 | 图片内部预览 | 适用 | 部分完成 | 有缩略图/检查器静态预览，缺 Viewer shell。 |
| F-049 | 缩放/平移/灰度/透明背景 | 适用 | 缺失 | 无 viewer 变换状态。 |
| F-050 | 旋转/翻转/裁切/拼图 | 适用 | 暂缓 | 高风险写回，不在 P1。 |
| F-051 | GIF/WebP/AVIF 播放 | 适用 | 部分完成 | 有部分首帧解码，缺逐帧/AVIF 证据。 |
| F-052 | 视频播放 | 适用 | 缺失 | 无 viewer 闭环。 |
| F-053 | 音频播放 | 适用 | 缺失 | 无 ingest/viewer/control。 |
| F-054 | URL/HTML/MHTML 预览 | Eagle 专属 | 暂缓 | 不复制 Webview。 |
| F-055 | PDF 查看器 | 适用 | 缺失 | 无 viewer/降级 registry。 |
| F-056 | 字体查看器 | 适用 | 缺失 | 无 viewer/降级 registry。 |
| F-057 | 3D 模型查看器 | Eagle 专属 | 暂缓 | 不在 Pixel Tart 核心格式范围。 |
| F-058 | RAW/纹理/EXIF/文本查看 | 适用 | 部分完成 | RAW 可登记，完整代理/viewer 未完成。 |
| F-059 | 检查器 | 适用 | 部分完成 | 有视觉区，缺完整元数据编辑/多选混合值。 |
| F-060 | 区域标注/评论 | 适用 | 缺失 | 只有整项 Comment。 |
| F-061 | 播放/新窗口/外部打开 | 适用 | 缺失 | 无统一命令。 |
| F-062 | 幻灯片/随机模式 | 适用 | 暂缓 | 后置阶段。 |
| F-063 | 导出到计算机 | 适用 | 缺失 | 无素材导出命令。 |
| F-064 | eaglepack/专有格式导出 | Eagle 专属 | 暂缓 | 不移植专有包。 |
| F-065 | 插件中心 | Eagle 专属 | 暂缓 | 不建第二插件市场。 |
| F-066 | 插件开发者面板 | Eagle 专属 | 暂缓 | 不复制插件开发壳。 |
| F-067 | Plugin/Web API | Eagle 专属 | 暂缓 | 不复制不安全本地 API。 |
| F-068 | AI Search/模型/MCP | Eagle 专属 | 暂缓 | 明确排除。 |
| F-069 | 更新、日志、调试报告 | 适用 | 已存在 | 共享宿主日志/审计/通知；更新仍属宿主。 |
| F-070 | 托盘/开机启动 | Eagle 专属 | 暂缓 | 不在素材模块复制。 |
| F-071 | 常用设置 | 适用 | 部分完成 | 有 settings 描述符，无素材设置 section。 |
| F-072 | 左栏设置 | 适用 | 部分完成 | 素材内部左右栏仍固定。 |
| F-073 | 操控/预览设置 | 适用 | 部分完成 | 有少量快捷键/尺寸，无 viewer 偏好。 |
| F-074 | 截图设置 | Eagle 专属 | 暂缓 | 不复制 Eagle 截图偏好。 |
| F-075 | 快捷键设置 | 适用 | 部分完成 | 现有键有限且命令层分散。 |
| F-076 | 通知设置 | 适用 | 部分完成 | 已接全局任务，缺素材通知偏好/重试 UX。 |
| F-077 | 密码锁 | Eagle 专属 | 暂缓 | 不建第二账号锁。 |
| F-078 | 自动导入设置 | 适用 | 缺失 | 无 watcher 配置。 |
| F-079 | 开发者设置 | Eagle 专属 | 暂缓 | 不新增 API token/开发者中心。 |
| F-080 | 许可证激活 | Eagle 专属 | 暂缓 | 使用宿主许可，不复制 Eagle。 |
| F-081 | 设备管理 | Eagle 专属 | 暂缓 | 不移植 Eagle 设备管理。 |
| F-082 | 关闭重开恢复 | 适用 | 部分完成 | 数据持久，页面/布局/集合/筛选不恢复。 |
| F-083 | 空/加载/错误/权限状态 | 适用 | 部分完成 | 有状态文本，缺原因化壳层。 |

## 7. 验证结果

- 自动构建/测试：见第 2 节，真实基线为 build PASS、tests 2068/2095。
- 真实鼠标：P0 为只读审计，未运行前台；记为未验证。
- 截图：P0 未新增截图。
- DPI：未生成新证据；既有 26 个 DPI 证据缺失失败保留。
- 性能：未重跑 100K；最新已提交 Modular Harness 报告中的 100K 结果仅作为历史证据，不冒充本阶段运行。

## 8. 未完成项、限制、风险和失败证据

- P1 尚未实施；素材库仍不是第 2 个一级入口。
- 两个工具箱重复入口仍存在；现有 WPF evidence contract 还在强制要求它们。
- 页面焦点路由缺失；全局 Ctrl+F 会聚焦隐藏的归片搜索框。
- 素材页裸键快捷键未排除 TextBox/IME。
- 三栏固定 220/*/330；无 splitter、折叠、窄窗响应或重启恢复。
- 缩略图 slider 修改 VM 数值，但虚拟面板/卡片仍用固定 178/170 DIP。
- ModuleWorkspaceHost 未处理 missing route 或 ViewFactory exception。
- 页面没有独立 loading/empty/error 壳；初始化错误只写底部状态。
- 正式产品 `MinWidth=1180` 与 1366×768 + 150%/175% 场景存在风险，P1 必须验证并如实记录。

## 9. 数据安全与回滚

- P0 仅新增本报告；回滚时只需回退本报告提交。
- P1 不得修改产品数据库 Schema 5，也不得删除 Asset schema v6 表/数据。
- 不执行用户文件移动、重命名、覆盖或删除；引用导入策略保持不变。
- 工具箱入口只能在新的一级入口、旧参数重定向和状态恢复测试通过后移除。
- P0/P1 使用独立提交，P1 可整体回退到本报告检查点。

## 10. 当前 HEAD 与下一阶段

本报告生成时 HEAD 仍为 `5be53f6393bba1069e921476bff976257d4f8505`；报告将作为独立 P0 检查点提交。下一阶段只实施 P1：唯一一级入口、同窗 route、可调/可折叠/可恢复三栏、loading/empty/error 壳、焦点/键盘/DPI 修复及相应测试，不提前宣称 P2～P6 完成。

## 11. 可直接复制给 Codex 的下一阶段指令

> 在 `feature/modular-harness-v1` 上从 P0 报告检查点继续执行 P1。保持 Pixel Tart 2.3.0、正式产品数据库 Schema 5、Single Close Authority、全局教程/输入路由、现有 Asset Library 数据库与服务不变。把素材库放到全局导航第 2 位，固定一级顺序为工作台/素材库/归片工作区/工作日历/联机拍摄/摄影收支/项目历史；移除两个可见工具箱素材库重复入口，但保留 `AssetLibrary` 与 `asset-library` 安全重定向。让页面占满主内容区，加入左右 GridSplitter、折叠、检查器固定、中央最小宽度、重启状态恢复、有效缩略图尺寸、loading/first-empty/no-result/error 壳；修复素材页初始焦点、上下文 Ctrl+F 和 TextBox/IME 快捷键冲突。补齐唯一入口、选中/焦点、状态 allowlist、splitter/窄窗/DPI、empty/loading/error、Single Close Authority 与 ModuleWorkspaceHost error/retry 测试。运行目标测试、正式 Debug build 和全 solution 回归，再用真实鼠标验证进入/离开/重启恢复和三栏操作。生成 `docs/implementation-reports/P1_..._2026-08-18.md` 并以独立提交形成回滚边界；所有 P2～P6 功能必须继续标为未完成或暂缓。
