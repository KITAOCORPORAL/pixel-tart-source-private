# 素材库一级导航与嵌入式三栏壳 P1 实施报告（Gate A 阻断，未闭环）

日期：2026-08-18

阶段：P1（实现检查点、自动回归和部分真实前台证据已完成；空/加载/错误重试与真实 DPI 矩阵未闭环）

执行分支：`feature/modular-harness-v1-p1`

P0 父检查点：`140e34348000174986c6e503dcedff8f90a78c34`（`docs(asset-library): add P0 Eagle migration audit`）

P1 实现检查点：`b4bd38f53d6a44756289eeda8bfc4feb343443c7`（`feat(asset-library): add P1 primary navigation shell`）

本轮 Gate A 起点：`20c1df775673cec790b1daa9db25072c2e34926c`；本报告所述诊断、恢复和证据辅助器作为非闭环检查点保存，不代表验收完成。

> 本文不是 P1 完成声明。已执行的自动测试、构建、真实物理交互和截图按实际证据逐项记录；未形成完整物理链或未执行的空/加载/错误重试与真实 DPI 矩阵明确标为“未验证/阻断”。本文不宣称 Eagle 全功能等价，也不把 P2～P6 能力计入 P1。

## 1. 本阶段目标与实际完成范围

P1 的目标是把既有素材库从两个工具箱重复入口迁移为主程序第 2 个一级页面，同时保留单一 `MainWindow`、全局导航、全局任务中心、现有 route/ViewModel/服务/数据库，并完成可调、可折叠、可恢复的嵌入式三栏壳及基础状态壳。

当前工作树实际实现了以下代码范围：

- 一级导航固定为“工作台 → 素材库 → 归片工作区 → 工作日历 → 联机拍摄 → 摄影收支 → 项目历史”；素材库是唯一的第 2 个一级入口。
- 在线选片保留原 route、页面与数据，但移入“工具”区域，不再占一级导航位置。
- 删除 `ToolboxAssetLibraryEntry`、`ToolboxPageAssetLibraryEntry` 两个可见重复入口及素材页局部“返回工作台”按钮；`AssetLibrary`、`asset-library`、页面、服务与数据全部保留。
- 素材模块的导航归属从 `toolbox` 调整为 `primary`，继续由同一 `ModuleWorkspaceHost` 嵌入同一主窗口，没有新增独立 Window、EXE、首页、设置中心或任务系统。
- 素材页改为占满主内容区的左组织 / 中素材 / 右检查器三栏壳；左右栏支持拖动、折叠，检查器支持固定；中央栏保留最小宽度与现有虚拟化列表。
- 左右栏宽度、折叠、检查器固定、缩略图宽度、搜索文本、文件夹/标签/智能文件夹选择和最后一级页进入现有 JSON 设置对象，读取时执行 allowlist 与数值范围规范化。
- 修复真实 `GridSplitter` 拖动或方向键调整后本地 `Width` 覆盖响应式绑定的问题：鼠标拖动完成和键盘左右键完成后都会保存实际宽度、恢复中央星号列并重新绑定左右栏；折叠后再展开可恢复调整后的宽度。
- 三栏响应逻辑改为按已保存栏宽、两个 splitter 与中央栏 360 DIP 最小宽度计算；最大保存宽度、检查器固定和窄窗组合不会把中央素材区挤出工作区，过窄时按钮标签与 tooltip 会明确说明临时收起行为。
- 修正缩略图滑杆只改 ViewModel、不改变虚拟面板和卡片几何的问题；虚拟面板、卡片宽高现在共享同一尺寸状态。
- 增加素材页加载、首次空库/无结果、查询失败与检查器无选择状态；初始化改为 single-flight，加载完成前以 `IsReady` 禁用搜索刷新和素材读写命令，重试不会与初始化并发；增加模块 route 缺失、工厂异常、重试与成功视图缓存壳。
- 素材页获得初始焦点路径；全局 `Ctrl+F` 在素材页聚焦素材搜索框；文本输入与 IME 合成上下文不会被素材页局部快捷键抢占。
- 窄窗素材页会收窄主程序全局导航，并根据可用宽度响应左右素材栏；离开素材页后恢复其他页面原有最小窗口约束。
- 补充一级导航顺序/唯一入口、状态 allowlist、设置往返、三栏/缩略图/状态壳、模块错误重试与初始焦点等自动测试。

当前不能宣称 P1 已闭环：本轮已经取得七项一级导航、素材进入、搜索/中文 IME、鼠标分栏、栏位折叠、缩略图、单项选择与关闭重启恢复的真实前台证据；但首次空库、加载、错误/重试、键盘分栏完整物理链以及四组真实 DPI/分辨率矩阵未完成。P1 实现已形成独立检查点 `b4bd38f`，本轮部分证据不能替代尚缺的 Gate A 必需项。

明确不在本阶段的范围：P2 的完整左栏组织、四布局、排序、框选/拖放/右键闭环；P3 的全库/当前范围搜索、历史建议、通用 AND/OR 筛选与保存查询；P4 的 Viewer Registry、完整单选/多选检查器和摄影分析交互；P5 的完整导入/批量/回收恢复/导出；P6 的 EagleAdapter。Eagle 独立产品能力继续排除。

## 2. 执行前分支、HEAD、工作区状态和测试基线

P1 从已独立提交的 P0 检查点开始：

| 项目 | P1 执行前记录 |
|---|---|
| 分支 | `feature/modular-harness-v1` |
| HEAD | `140e34348000174986c6e503dcedff8f90a78c34` |
| P0 父提交 | `140e343`，P0 审计与 F-001～F-083 迁移地图 |
| 工作区 | P0 提交后 clean；P1 产品代码与测试已提交为 `b4bd38f`，本报告在后续文档提交中记录验收边界 |
| 总任务起点 | `5be53f6393bba1069e921476bff976257d4f8505` |
| SDK | .NET SDK 10.0.302 |

修改前正式 `build_debug.ps1` 基线：

| 项目 | 结果 |
|---|---:|
| Debug build | PASS，0 warnings / 0 errors，17.53 s |
| 全部测试 | 2095 total / 2068 passed / 27 failed / 0 skipped |
| Modular Harness | 14/14 passed |
| WPF | 792/792 passed |
| DPI | 75/101 passed，26 failed |
| Core | 1187/1188 passed，1 failed |
| 总耗时 | 63.347 s |

基线失败已保留：26 个 DPI 失败来自既有 `artifacts/automated-dpi-review/2.0.4/*.json` 缺失；Core 的 1 个失败来自 `Version220AcceptanceIsolationTests.AcceptanceExecutable_UsesExplicitIsolatedAppDataRootOnly` 仍断言旧隔离根错误文案。P1 同步了该测试与现有产品行为，没有放宽产品验收条件。

## 3. 修改文件清单及每个文件的职责

### 产品代码

| 文件 | P1 职责 |
|---|---|
| `src/RAWSelectionAssistant.Core/Models/AssetLibraryWorkspaceSettings.cs` | 新增素材工作区 JSON 状态、数值规范化与固定七项一级导航 allowlist/别名规则；消解“检查器同时固定且折叠”的矛盾恢复状态；本轮增加可失效清空的 `SelectedAssetId`。 |
| `src/RAWSelectionAssistant.Core/Models/AppSettings.cs` | 在既有应用设置中增加最后一级页与素材工作区状态对象。 |
| `src/RAWSelectionAssistant.Core/Services/SettingsService.cs` | 加载设置时恢复默认对象、规范化最后一级页与素材栏宽/缩略图/搜索状态。 |
| `src/PixelTart.Modules.AssetLibrary/AssetLibraryModule.cs` | 将素材模块导航归属改为 `primary` 并调整顺序，保留原 ModuleId、route 与能力。 |
| `src/PixelTart.Modules.AssetLibrary/AsyncCommand.cs` | 增加同步素材命令封装，供折叠、固定等壳层命令共用。 |
| `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs` | 承载按实际栏宽计算的响应式三栏、缩略图真实尺寸、持久状态、single-flight 初始化、`IsReady` 命令门禁、加载/空/错误/选择状态及安全重试；本轮保存并在查询结果中核对选中素材 ID。 |
| `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml` | 将页面改成扁平满幅三栏壳，增加两个支持鼠标和键盘的 splitter、动态 min/max 宽度、折叠/固定入口、窄窗 tooltip、状态壳、检查器空态与自动化标识；本轮给缩略图滑杆增加稳定 AutomationId。 |
| `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs` | 接入宿主设置/日志；在 splitter 鼠标拖动或键盘调整后保存实际宽度并恢复响应式绑定；响应窗口宽度，提供初始焦点和素材搜索焦点，保护 TextBox/IME；本轮把有效恢复选择同步到真实列表选择。 |
| `src/RAWSelectionAssistant/App.xaml.cs` | route 工厂向素材页注入共享设置与日志；继续使用现有数据库路径、任务桥和模块注册表。 |
| `src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Navigation.xaml` | 增加像素蛋挞自己的素材库一级导航几何图标，不复制 Eagle 品牌资产。 |
| `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs` | 恢复并保存最后一级页，统一 `AssetLibrary`/`asset-library` 路由，增加素材页状态文本与窄窗全局侧栏响应。 |
| `src/RAWSelectionAssistant/MainWindow.xaml` | 建立固定七项一级导航、唯一素材入口与选中态；把在线选片移至工具区，移除两个素材工具箱重复入口。 |
| `src/RAWSelectionAssistant/MainWindow.xaml.cs` | 为素材页路由初始焦点和 `Ctrl+F`，切换页面最小窗口约束，并同步侧栏宽度响应。 |
| `src/RAWSelectionAssistant/MainWindow.PhysicalPointerDiagnostics.cs` | 仅在 Acceptance/Dev Preview 诊断构建中关联分隔条与缩略图滑杆的鼠标/键盘前后状态；普通产品构建不启用。 |
| `src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs` | 将已确认的普通按钮点击与分隔条/滑杆状态转换写入隔离诊断 JSON，记录 Layer1～4 与 state_changed；普通产品构建保持关闭。 |
| `src/RAWSelectionAssistant/Views/ModuleWorkspaceHost.cs` | 增加 per-route 视图缓存、缺失 route/工厂异常的共享内联状态、重试与初始键盘焦点。 |

### 测试与验收契约

| 文件 | P1 职责 |
|---|---|
| `tests/RAWSelectionAssistant.Tests/AssetLibraryP1SettingsTests.cs` | 验证固定七项顺序、别名/非法值回退、设置往返、边界值与非有限数规范化。 |
| `tests/RAWSelectionAssistant.Tests/NavigationSafety204Tests.cs` | 验证生产恢复最后安全一级页、非法页回工作台，以及 Dev Preview 仍固定从工作台启动。 |
| `tests/RAWSelectionAssistant.Tests/Version220AcceptanceIsolationTests.cs` | 把陈旧隔离根错误文案断言同步到现有 Acceptance/Dev Preview 行为。 |
| `tests/PixelTart.ModularHarness.Tests/ModularHarnessAcceptanceContractTests.cs` | 将素材模块契约从工具箱归属更新为正式一级归属与顺序。 |
| `tests/RAWSelectionAssistant.WpfTests/NavigationWorkbenchClosureTests.cs` | 验证七项顺序、素材唯一入口/选中态、在线选片退出一级区、焦点和全局搜索路由。 |
| `tests/RAWSelectionAssistant.WpfTests/EmbeddedAssetLibraryWpfTests.cs` | 验证满幅三栏、真实 splitter 鼠标/键盘调整后绑定修复、折叠恢复、最大保存宽度/固定检查器窄窗约束、逻辑 DPI 矩阵、真实缩略图尺寸及 loading/empty/error 状态。 |
| `tests/RAWSelectionAssistant.WpfTests/ModuleWorkspaceHostTests.cs` | 验证 route 缓存、缺失 route、工厂异常、重试恢复与可见宿主初始焦点。 |
| `tests/RAWSelectionAssistant.WpfTests/ModularHarnessEmbeddedEvidenceContractTests.cs` | 将证据契约从“两个工具箱入口”反转为“唯一一级入口”，保留同窗 route/数据库/模块契约。 |
| `tests/RAWSelectionAssistant.WpfTests/PhysicalPointerDiagnosticContractTests.cs` | 锁定 Acceptance/Dev Preview 诊断门控、普通按钮记录以及分隔条/滑杆状态转换字段。 |
| `tests/RAWSelectionAssistant.WpfTests/AssetLibraryP1EvidenceToolContractTests.cs` | 锁定窗口证据工具的 PID/完整路径/标题唯一性、窗口稳定性、DPI 与无输入边界。 |
| `tests/RAWSelectionAssistant.WpfTests/ClickRoutingFixFocusedTests.cs` | 同步旧物理点击契约：普通已确认按钮必须可审计，关闭类动作继续保持去重与单一关闭权。 |
| `tools/ModularHarnessV1Acceptance/evidence-contract.json` | 将入口场景的必需自动化目标改为 `AssetLibraryNavigationButton`，并把 `capture_status` 明确置为 `not_captured`；当前没有把旧截图冒充 P1 新证据。 |
| `tools/AssetLibraryP1Acceptance/Capture-AssetLibraryP1WindowEvidence.ps1`、`README.md` | 以精确 PID/路径/标题捕获 ScreenPixels 或 PrintWindow PNG 和无 BOM JSON 清单；工具不生成 UI 输入，拒绝进程/窗口歧义。 |

### 报告

| 文件 | P1 职责 |
|---|---|
| `docs/implementation-reports/P1_ASSET_LIBRARY_PRIMARY_NAV_SHELL_2026-08-18.md` | 记录 P1 范围、基线、自动回归、真实 Gate A 部分证据、Computer Use 恢复上限、未验证边界、回滚方法与继续执行指令。 |

## 4. 路由、模块、数据、配置和缓存变化

### 路由与模块

- 一级 Shell surface 统一使用 `AssetLibrary`；既有模块 route `asset-library` 规范化到同一页面。
- `pixel-tart.asset-library`、8 项公开能力、Selection 适配、任务桥和数据库服务没有复制或改名。
- 素材 route 仍由 `ModuleWorkspaceHost` 在主 `MainWindow` 内容区承载；不存在第二套全局导航或独立标题栏。
- 素材模块 `NavigationGroup` 从 `toolbox` 改为 `primary`，顺序调整为一级导航第 2 位。
- `OnlineSelection` route 与页面保留，只从一级导航移到工具区。

### 数据

- 正式产品数据库 Schema 5：未变更。
- 素材私有数据库 `asset-library-v16.db` 及其 schema v6、14 张表、索引、journal 和现有行：未变更。
- 没有执行数据库迁移、Eagle `.library` 写入、源文件移动/改名/覆盖/删除或永久删除。
- 引用式导入仍默认不移动、不改名、不删除源文件。

### 配置

在现有原子 JSON 设置中增加：

- `LastPrimaryPage`；仅接受固定七项一级页，`asset-library` 安全归一到 `AssetLibrary`，非法值回到 `Workbench`。
- `AssetLibraryWorkspace.OrganizationPaneWidth`、`InspectorPaneWidth`、左右折叠、检查器固定、`ThumbnailWidth`。
- 素材搜索文本以及选中的文件夹、标签、智能文件夹和单项素材 ID；恢复时若素材不在当前真实查询结果中则安全清空，不制造幽灵选择。
- 宽度与缩略图值在读取时限制到安全范围；NaN/Infinity 回到默认值，搜索文本去除首尾空白并限制长度；检查器固定时强制取消矛盾的折叠状态。
- `MODULAR_HARNESS_DEV_PREVIEW` 继续固定从 Workbench 启动，避免验收预览被用户历史状态污染；正式产品恢复最后一个安全一级页。

以上均为向后兼容的 JSON 默认字段，没有数据库 Schema 变化。

### 缓存

- `ModuleWorkspaceHost` 新增当前进程内、按 route 的视图缓存；更换模块注册表时清空，工厂失败不会缓存失败对象。
- 素材 ViewModel 的初始化 gate、查询 generation 与 `IsReady` 只属于当前进程并发控制，不产生新磁盘格式。
- 没有新增磁盘缓存格式，也没有改动现有缩略图 LRU、视觉分析缓存表或缓存版本。

## 5. 用户可见变化与关键操作路径

### 固定导航路径

主窗口左侧“主要导航”现在只有以下七项，顺序固定：

1. 工作台
2. 素材库
3. 归片工作区
4. 工作日历
5. 联机拍摄
6. 摄影收支
7. 项目历史

在线选片仍可从“工具”进入。工作台弹出工具箱和工具箱全页不再显示素材库重复入口。

### 素材页路径

1. 在全局导航选择“素材库”，进入同一个主窗口内容区；底部任务中心和全局导航保留。
2. 初始焦点进入素材页；`Ctrl+F` 聚焦并全选素材搜索框内容。
3. 顶部按钮可展开/收起左组织栏和右检查器，可切换检查器固定状态。
4. 两个分隔条的代码路径支持鼠标拖动和键盘左右键；调整完成后保存宽度并重新绑定响应式列，折叠/展开恢复该宽度。缩略图滑杆会同时改变虚拟化单元和可见卡片尺寸。
5. 无数据、查询无结果、加载中、加载错误和检查器无选择使用不同的可见状态；可恢复错误提供重试入口。
6. 切到其他一级页再回来时，route 视图由同一宿主复用；应用保存设置后，正式产品代码会在下次启动恢复最后安全一级页与素材工作区状态。该重启路径已有自动覆盖，但尚无物理交互证据。
7. 在输入框或 IME 合成期间，素材页不会把普通输入误当成局部命令。

当前代码还包含以下响应行为：素材页允许比其他业务页更小的主窗口边界；素材页可用宽度不足时，全局导航变为图标宽度，左右素材栏按已保存宽度、固定状态和中央栏 360 DIP 下限响应，临时隐藏不会覆盖用户保存宽度。该路径已获自动契约覆盖，但 1366×768 配合 150%/175% 的真实桌面观感仍未验证。

## 6. F-001～F-083 对照矩阵的 P1 状态变化

本节只列出 P1 直接改变实现证据的条目，不重抄其余 76 项。没有列出的 F 项沿用 P0 判定。由于 P1 仅完成导航与壳层，以下多数条目仍为“部分完成”；没有把后续阶段能力提前改成“已完成”。

| ID | 功能 | P0 状态 | P1 当前状态 | 本阶段新增证据与保留缺口 |
|---|---|---|---|---|
| F-001 | 启动应用 | 已存在 | 已存在（证据增强） | 单应用/同窗不变；增加最后安全一级页恢复和非法值回退。真实重启指针路径待验证。 |
| F-045 | 排序与布局信息 | 部分完成 | 部分完成（证据增强） | 缩略图宽度现在真实驱动虚拟化面板和卡片并持久化；排序和四种布局仍属 P2。 |
| F-059 | 检查器 | 部分完成 | 部分完成（证据增强） | 增加检查器折叠/固定/宽度恢复和无选择空态；完整元数据编辑、多选共同/混合值仍属 P4。 |
| F-069 | 更新、日志、调试报告 | 已存在 | 已存在（证据增强） | 素材页继续接入宿主日志并使用共享错误壳；没有新增素材独立更新器或调试中心。 |
| F-072 | 左栏设置 | 部分完成 | 部分完成（范围扩大） | 左组织栏和右检查器均可调、可折叠并持久化，检查器可固定；splitter 的鼠标/键盘调整后会恢复响应绑定，最大保存宽度和窄窗计算保留中央栏下限；完整侧栏内容与专用设置 UI 仍未完成。 |
| F-075 | 快捷键设置 | 部分完成 | 部分完成（证据增强） | 修正上下文 `Ctrl+F`、初始焦点、方向/Tab 导航、splitter 左右键持久化与 TextBox/IME 防冲突；F2/Delete/Ctrl+A/Space/Alt+1..4 的完整统一命令闭环仍后置。 |
| F-082 | 关闭重开恢复 | 部分完成 | 部分完成（范围扩大） | 增加最后一级页、左右栏宽度/折叠、固定、缩略图、搜索与集合选择的 JSON 往返，并规范化固定且折叠的矛盾检查器状态；真实关闭重开尚未做前台鼠标验收，布局/排序完整恢复仍后置。 |
| F-083 | 空/加载/错误/权限状态 | 部分完成 | 部分完成（范围扩大） | 增加 loading、首次空库/无结果、查询错误/重试、检查器无选择，以及 missing-route/factory-error/retry 壳；初始化改为 single-flight，未 ready 时素材命令受门禁；权限、文件缺失、不支持格式、部分失败和任务完成的完整原因化状态仍在后续阶段。 |

P0 的总体适用性边界不变：Eagle 的欢迎/激活/设备/商店/更新器、插件/Web API、密码锁、品牌推广、AI/MCP 与专有包能力不迁入像素蛋挞。

## 7. 自动测试、构建、真实鼠标、截图、DPI 和性能结果

### 已执行的自动验证

| 验证 | 结果 |
|---|---:|
| 既有 Core P1 聚焦测试 | 26/26 passed |
| 既有 WPF P1 聚焦测试 | 41/41 passed |
| Gate A 证据工具与物理诊断聚焦测试 | 9/9 passed |
| 真实 GridSplitter 生产 XAML 路径定向回归 | 1/1 passed |
| 正式 Debug solution build（`-warnaserror`） | PASS，0 warnings / 0 errors，2.54 s |
| Release solution build（`--no-restore -warnaserror`） | PASS，0 warnings / 0 errors，6.67 s |
| Acceptance + InputRoutingDiagnostics Debug build | PASS，0 warnings / 0 errors，3.17 s |
| DevPreview + InputRoutingDiagnostics Debug build | PASS，0 warnings / 0 errors，3.01 s |
| 修改后完整 solution tests | 2128 total / 2102 passed / 26 failed / 0 skipped |
| Core 完整测试 | 1192/1192 passed |
| WPF 完整测试 | 821/821 passed |
| Modular Harness 完整测试 | 14/14 passed |
| DPI 完整测试 | 75/101 passed，26 failed |

与修改前基线相比，测试总数从 2095 增至 2128（+33），通过数从 2068 增至 2102（+34），失败数从 27 降至 26。基线中的 1 个 Core 失败是陈旧的 isolation assertion，现已与既有 Acceptance 隔离根行为对齐；本轮完整 WPF 首跑还发现一条旧测试反向要求“只记录关闭按钮”，该测试已同步为“所有物理确认按钮均可审计、关闭动作仍去重”，新增窗口证据工具契约后最终 WPF 821/821。剩余 26 个失败全部仍是基线已经存在的 `artifacts/automated-dpi-review/2.0.4/*.json` 自动 DPI 证据文件缺失，没有新增失败、隐藏失败、跳过测试或降低断言。

聚焦覆盖包括：固定七项导航与唯一入口、`AssetLibrary`/`asset-library` 归一化、生产/Dev Preview 启动策略、设置往返与矛盾状态规范化、single-flight 初始化及 ready 门禁、三栏宽度/折叠/固定/响应、真实 splitter 鼠标与键盘事件后的宽度持久化和绑定恢复、最大保存栏宽与窄窗中央栏下限、缩略图真实尺寸、loading/empty/error、ModuleWorkspaceHost 缓存/缺失 route/工厂失败/重试/初始焦点、同窗/单入口 evidence contract。WPF 的显示矩阵以逻辑布局测试覆盖 1366×768、1920×1080、2560×1440 与 100%/125%/150%/175% 的换算尺寸；它不是物理显示器或真实 Windows 缩放证据。

### Gate A 真实前台部分证据

本轮运行标识为 `P1-GateA-Real-20260818-170520-db3d4e4b`。运行时数据库、诊断与 PNG 全部位于隔离的 `%TEMP%`/`.validation` 根；只导入 12 张本轮生成的合成 JPEG，没有读取或写入真实用户素材。可核验证据位于 `.validation/P1-GateA-Real-20260818-170520-db3d4e4b/evidence/`：

- `navigation-7-physical-pointer-session.json` 记录恰好 7 次物理确认按钮点击，AutomationId 顺序为 `PrimaryNavigationWorkbench`、`AssetLibraryNavigationButton`、`PrimaryNavigationWorkflow`、`PrimaryNavigationWorkCalendar`、`PrimaryNavigationTether`、`PrimaryNavigationFinance`、`PrimaryNavigationHistory`；每次 `physical_target_confirmed=true`。
- `initial-import-0-to-12.json` 记录同一隔离库从 0 导入到 12：selected/imported/current query/ViewModel/grid 均为 12，skipped/failed/thumbnail failure 均为 0。
- 在同一 `MainWindow` 内真实进入素材库；物理 `Ctrl+F` 聚焦 `AssetLibrarySearch`，ASCII `warm` 得到 1 项；随后切换中文输入法并输入 `s,u,c,a,i`，组合串为 `su'cai`，空格提交“素材”后得到 0 项，证明 IME 组合未被页面快捷键抢占。
- 两个分隔条的鼠标拖动分别把组织栏从 220 调到 260、检查器从 320 调到 360；缩略图滑杆从 180 调到约 248.376。`controls-physical-pointer-session-before-exit.json` 对这三次鼠标变化记录 `Layer1～4=true`、`state_changed=true`、`result=Confirmed`。
- 组织栏与检查器均完成真实折叠并分别形成截图；随后恢复两栏，固定检查器并选择 `12_complementary_mid_high_contrast.jpg`，检查器显示 1 项及当前分析。
- 正常关闭后以同一隔离根启动 `KitaoPhotoSelector.Acceptance.exe`，直接恢复到素材库，恢复检查器固定、约 270/370 的持久栏宽、约 248.376 的缩略图宽度和有效单项素材选择。失效素材 ID 的安全清空由自动回归覆盖。
- 证据工具对每张正式截图核对精确 PID、完整 EXE 路径、标题、全局同路径进程唯一、前景主窗口唯一、截图前后 HWND/矩形/DPI 稳定及 SHA-256；工具清单明确 `ui_input_generated=false`。正式可用截图为 `01d-default-three-pane-150pct-printwindow.png`、`02-ime-composition-150pct.png`、`03-search-no-results-chinese-150pct.png`、`04-left-organization-collapsed-150pct.png`、`05-right-inspector-collapsed-150pct.png`、`06-selected-asset-150pct.png`、`07-restart-restored-150pct.png` 及各自 `.window-evidence.json`。

本机变更前实际桌面是 3840×2160@60、150%（DPI 144），不是指令文字假定的 2560×1440@150%。本轮正式截图窗口为 2400×1380 物理像素，均在 150% 下生成；没有改变系统显示设置。

### 未验证与失败证据

| 验证项 | 当前记录 |
|---|---|
| Computer Use / Gate A 总状态 | **BLOCKED**。常规场景已取得上述真实交互；首次空库/加载/错误重试的 Acceptance 启动被既有 22 步教程遮挡，两次可见退出点击均返回 `Input or refresh outcome is unknown`。两个全新 Dev Preview 空根随后分别在素材入口恢复时触发最小化/`user input was detected`，以及坐标无状态变化后元素重试 `Input or refresh outcome is unknown after retry`。按 computer-use 的有界恢复规则已经停止，不继续盲点或循环重试。 |
| 分隔条键盘链 | **部分、不能判 PASS**。方向键实际把检查器 360→370、组织栏 260→270 并写回设置，但诊断 JSON 的两条键盘转换均为 `InputUnconfirmed`/`layer4_action_confirmed=false`；不能把状态变化冒充完整 `Win32 → WPF → HitTest → Action` 证据。 |
| 空/加载/错误/重试截图 | **未验证**。首次空库、加载和错误/重试场景未形成可审计截图；因此 `tools/ModularHarnessV1Acceptance/evidence-contract.json` 继续保持 `capture_status: not_captured`，不能只因已有部分 PNG 就翻成 captured。 |
| P1 真实 DPI | **未验证**。自动逻辑矩阵和 26 个历史证据缺失失败都不能替代真实 Windows 100%/125%/150%/175% 检查。 |
| 指令要求的真实矩阵 | **未验证**。驱动只读测试表明 1366×768@60、1920×1080@60、2560×1440@120 可接受，但没有实际切换到 1366×768@100%、1920×1080@125/150%、2560×1440@175%，也没有形成每组截图、命中和滚动证据；只读模式探测不能算验收。 |
| 性能 | **未重跑**。本阶段没有新的 1K/10K/100K 搜索、滚动、选中、检查器切换耗时与内存记录；历史 100K 结果不冒充本阶段结果。 |
| 其余六个一级业务页的物理进入回归 | **已取得物理点击链**。七项一级按钮均有 `physical_target_confirmed=true`；但这不补足空/错误/DPI 阻断项。 |

## 8. 未完成项、限制、风险和失败证据

- P1 的前台完成条件尚未满足：虽然七项导航、常规素材工作区、搜索/IME、鼠标分栏、折叠、缩略图、选择和关闭重启已有真实证据，首次空库、加载、错误/重试、键盘分栏完整链和真实 DPI 矩阵仍缺失。实现检查点已创建，但 Gate A 验收闭环尚未创建。
- Computer Use 先出现 `SetIsBorderRequired` / `0x80004002`，常规场景通过文本状态与严格 PrintWindow 辅助器继续取得部分证据；空状态的三个有界新进程恢复最终仍停在 `Input or refresh outcome is unknown` / `user input was detected`。本轮不再继续 UI 输入，evidence contract 保持 `not_captured`。
- 完整测试仍有 26 个失败；共同原因是基线即存在的 `artifacts/automated-dpi-review/2.0.4/*.json` 缺失。报告保留该失败，不把 2102/2128 写成“全部通过”。
- 1366×768 配合 150%/175% 时，素材页会压缩全局侧栏并响应内部栏；自动逻辑布局测试覆盖中央栏下限和元素边界，但真实文本溢出、命中区、遮挡和滚动行为仍有视觉风险。
- 检查器“固定”在窄窗下会优先保留右栏；中央栏仍有最小宽度。极窄窗口的实际可用性需要前台证据确认。
- 设置模型已保存搜索、集合选择 ID 与有效单项素材 ID；失效素材 ID 会安全清空，但集合/筛选的完整 P2/P3 UI 与用户提示仍未完成。
- ModuleWorkspaceHost 提供缺失 route 和工厂异常重试，但素材库内部权限、文件丢失、不支持格式、部分失败等原因化状态尚未齐全。
- 本阶段没有重跑 1K/10K/100K 性能场景，不能宣称性能没有回归。
- P2～P6 全部保持未完成/暂缓；目前只有一个虚拟化 Wrap 网格，不能宣称四布局、完整选择、Viewer、完整检查器、批量/回收/导出或 EagleAdapter 已完成。
- “等价”仅指 P1 范围内的入口、三栏结构和基础状态行为；没有复制 Eagle 源码、Logo、图标、品牌文案、独立产品壳或专有技术。

当前已经形成部分 P1 物理指针证据和 150% 截图路径，但没有形成空/错误/重试、完整键盘链、真实 DPI 矩阵或性能新结果；实现检查点 `b4bd38f` 已提交，本轮 Gate A 增量按非闭环 evidence-tooling 检查点独立保存。部分证据不能把整体验收改写为 PASS，这些边界不以占位符或历史证据代替。

## 9. 数据安全与回滚方法

- P1 没有修改产品数据库 Schema 5，也没有修改素材数据库 schema v6、表结构、索引或任何素材行。
- 没有移动、重命名、覆盖或删除用户源文件；没有调用 Eagle `.library` 写接口；没有增加永久删除入口。
- 删除的只是两个可见工具箱入口和页面内局部返回按钮；正式 route、旧参数归一化、ViewModel、服务、数据库与恢复记录保留。
- JSON 设置字段是可选的向后兼容增量。旧设置缺字段时使用安全默认值；非法最后页面回到 Workbench；越界或非有限尺寸回到受控范围；检查器同时固定且折叠时恢复为固定且展开。
- 新增 route 视图缓存仅存在于进程内，关闭应用即释放，不需要磁盘回滚或缓存迁移。
- P0 可回滚边界是 `140e343`；P1 实现回滚边界是 `b4bd38f`。需要回退时优先对 P1 实现提交执行可审计的 `git revert b4bd38f`，不需要数据库回滚，也不得使用 `git reset --hard` 或删除用户数据。

## 10. Gate A 起始 HEAD 和建议的下一阶段

本轮 Gate A 未提交增量开始时的分支 HEAD 为：

`20c1df775673cec790b1daa9db25072c2e34926c`

该 HEAD 已包含 P0 `140e343` 与 P1 实现检查点 `b4bd38f`。本轮 Gate A 诊断、选中素材恢复、证据辅助器、测试和本报告作为非闭环检查点保存；因此仍没有验收闭环 SHA。

下一步不是直接启动 P2，而是先完成 P1 验收闭环：

1. 只在 Computer Use 外部状态发生实际恢复或开启全新可控桌面会话后继续；不得在本轮已耗尽恢复次数的窗口上循环重试，也不得用自动树、合成事件或旧截图冒充物理证据。
2. 在新隔离根补齐首次空库、加载、错误/重试截图，并让两个 splitter 的方向键变化获得完整 `Win32 → WPF → HitTest → Action` 确认；已通过的七项导航、搜索/IME、鼠标分栏、折叠、缩略图、选择和重启恢复无需伪重写。
3. 通过 Windows 设置 UI 实际验证 1366×768@100%、1920×1080@125%、1920×1080@150%、2560×1440@175%；每次切换后重新绑定窗口并记录 DPI/窗口/截图。完成后必须恢复本轮实测原始状态 3840×2160@60、150%，不能按文字假定恢复到 2560×1440。
4. 如产品或测试再有变化，重跑 Core/WPF 聚焦测试、Debug/Release 构建和完整 solution tests，保留全部失败原因。
5. 完成最终差异、UTF-8、数据安全和证据审计；只有真实截图形成后才把 evidence contract 从 `not_captured` 改为相符状态。
6. 在上述证据完成后追加 P1 验收闭环记录；实现检查点 `b4bd38f` 已存在，无需重写或合并。只有验收条件实际满足后，才建议从该检查点进入 P2；当前不得进入 P2。

## 11. 当前 Gate A 继续条件

> 在 `feature/modular-harness-v1-p1` 上从 Gate A 起点 `20c1df775673cec790b1daa9db25072c2e34926c` 继续。P0 父检查点仍为 `140e34348000174986c6e503dcedff8f90a78c34`，P1 实现检查点仍为 `b4bd38f53d6a44756289eeda8bfc4feb343443c7`。不得进入 P2～P6，不得修改正式 Schema 5、素材 schema v6、Eagle `.library` 或用户源文件。当前自动结果是 2128 total / 2102 passed / 26 inherited DPI-evidence failures / 0 skipped；Core 1192/1192、WPF 821/821、Harness 14/14，Debug/Release 均 0 warnings / 0 errors。Gate A 已真实验证七项一级导航、0→12 合成导入、同窗素材页、Ctrl+F、中文 IME、鼠标分栏、左右折叠、缩略图、固定检查器、有效素材选择和 Acceptance 关闭重启恢复；证据在 `.validation/P1-GateA-Real-20260818-170520-db3d4e4b/evidence/`。仍需在新的可控 Computer Use 会话补首次空库/loading/error/retry、分隔条方向键完整 Layer1～4 链，以及 1366×768@100%、1920×1080@125/150%、2560×1440@175% 的真实 Windows 矩阵，并最终恢复实测基线 3840×2160@60、150%。当前 `capture_status` 必须保持 `not_captured`；只有全部必需证据、回归与安全审计通过后才允许提交/推送验收闭环并进入 P2。

## 12. 历史继续指令（执行前状态，仅作审计留档）

> 执行前指令曾记录 20c1df 之前“物理输入、截图与 DPI 全部未验证”的状态；该快照已被本报告第 7～11 节的本轮真实证据与阻断结论取代。完整历史仍可从 Git 中审计，这里不重复陈旧测试数字或继续步骤。
