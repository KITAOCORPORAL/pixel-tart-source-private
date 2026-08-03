# 像素蛋挞导航去重、拼图异常修复与工作日历蓝图报告

## 1. 结论

本轮已停止像素蛋挞 UI Review、自动 DPI 和安装版交互自动化。拼图反复弹出和“自动导入图片、自动选择 2×2”的根因是外部安装版验收脚本持续控制应用窗口，不是正式 Release 的启动、Loaded、设置恢复或任务事件循环。

产品仍存在两个需要修复的独立缺口：相同目标导航没有显式短路；侧栏把“本地分片”和“归片工作区”同时作为一级页面，语义重复。本轮已修复，并给拼图导入增加命令级防重入。

## 2. 拼图异常复现与根因

复现条件是运行 `tools/AutomatedDpiAcceptance/Invoke-InstalledInteraction.ps1`，脚本安装并启动软件，依次打开工具箱、拼图、文件选择器和 2×2 模板。当系统确认对话框没有被可靠关闭而脚本仍继续后续步骤时，用户会看到页面重复弹出和操作像“自动循环”。

涉及进程为脚本宿主 PowerShell 与其启动的 `KitaoPhotoSelector.exe`。本轮开始审计时相关进程已结束，当前没有残留产品或验收进程；审计文件为 `artifacts/diagnostics/2.0.4/runtime-process-audit.json`。

正式 Release 检查结果：

- 默认进入 `Workbench/ProjectCenter`；
- `CollageView.Loaded` 只渲染预览，不导入图片；
- 自动导入和 `4-grid`（2×2）只存在于 `UI_REVIEW_BUILD` 条件编译代码与外部验收脚本；
- Release 默认不定义 `UI_REVIEW_BUILD`，不会编译验收场景；
- 未发现 `Activated`、设置恢复、重复 `NavigationService`、多个 `MainWindow` 或后台计时器导致正式产品循环。

## 3. 修复方式

- `Navigate` 对当前目标直接短路，一个有效导航请求只写一条带 `NavigationCorrelationId` 的脱敏日志；
- 拼图使用单一 `CollageViewModel` 实例；导入图片命令使用 `_isImporting` 防重入，导出继续使用 `AsyncRelayCommand` 内建防重入；
- 安装交互脚本默认禁用，只有同时提供 `-IsolatedAcceptanceRun` 和 `PIXEL_TART_ALLOW_INSTALLED_AUTOMATION=1` 才允许运行；
- 脚本 `finally` 负责关闭受控进程、卸载隔离安装、恢复设置和剪贴板；
- Release 启动、拼图自动导入、自动选 2×2、Loaded 行为、脚本隔离与生命周期均加入防回归测试。

本轮修改了正式产品源码，也修改了测试/验收工具。

## 4. 本地分片与归片工作区去重

重复原因是两个入口过去都暴露同一套来源、选片、匹配和输出语义，侧栏又将二者同时作为一级页面。

新结构：

- 工作台：保留“开始本地分片”大卡；
- 本地分片快速向导：选择输入、来源目录、格式和扫描，完成后显式进入工作区；
- 归片工作区：完整来源、选片、JPG/RAW 匹配、冲突、人工调整、复制、报告、保存和继续项目；
- 左侧：工作台、归片工作区、项目历史、授权与版本、设置、帮助，底部工具箱/教程/反馈；不再放置“本地分片”一级入口；
- 文件菜单仍可保留“新建本地分片”。

路由保留 `Workbench`、`LocalSplit`（后续命名 `LocalSelectionWizard`）、`Workflow`（后续命名 `WorkflowWorkspace`）、`History`、`Toolbox`、`Settings`。

## 5. 工作日历蓝图

工作日历定位为摄影师本地拍摄排期与项目资料入口，不是 CRM、预约商城、收款平台或员工排班系统。计划版本为 2.2.0；2.1.0 仅预留 SQLite、项目关系和本地提醒接口。

页面包括月/周/日视图、今日、日期跳转、状态/类型筛选、搜索、当天列表和拍摄详情。详情包含基本信息、金额、拍摄要求、准备清单、策划/协议等文档、关联项目和本地备注。

数据模型：

- `ShootBooking`：排期、客户显示名、起止时间、状态、地点、拍摄类型、金额和备注；
- `ShootRequirementItem`：准备项、优先级、排序和完成状态；
- `BookingDocument`：文档类型、显示名、本地路径、大小、修改时间、可选哈希和丢失状态；
- `BookingReminder`：提醒时间、启用状态和触发记录。

文档只保存本地引用，不把文件本体写入 SQLite。移除关联不删除原文件；需要复制到项目目录时走 `FileOperationPlan`。金额只作本地记录，不接支付、发票或收入大屏。排期冲突列出项目、时间、地点和重叠时长，允许用户返回修改、仍然保存或标记可重叠。

工作台后续增加“今日拍摄”和“未来 7 天”，正式 Release 不注入演示数据。提醒默认关闭，由用户主动启用。默认不联网、不上传姓名、电话、金额、协议或策划；日志和诊断包必须脱敏。

## 6. 修改与新增文件

产品：`MainWindow.xaml`、`MainViewModel.cs`、`ToolPageViewModels.cs`。测试：`NavigationSafety204Tests.cs`、两个侧栏旧测试、DPI 测试项目和解决方案。验收工具：`Invoke-InstalledInteraction.ps1`、逻辑 DPI 工具、隔离安装烟测脚本。文档：五份指定路线图、工作日历设计、导航重构、拼图根因分析、发布清单和本报告。

## 7. 测试与产物

- Release 完整测试：490/490 通过，失败 0，跳过 0；核心 458、逻辑 DPI 27、WPF 5；
- 导航/异常专项测试：20 项；
- 构建：Release、win-x64、self-contained、WinExe，0 警告、0 错误；
- 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.0.4\installer\像素蛋挞_Setup_2.0.4_x64.exe`；
- 大小：48,791,104 字节；
- SHA-256：`8F2131EBF6E13EDF990639F57FE8BEB189BD4829464382CADEF682CDEFAAFEFD`；
- 隔离安装烟测：安装 0、主窗口可见、标题“像素蛋挞”、WinExe 无控制台、关闭 0、卸载 0、目录清除；
- 真实导航点击：观察到工作台、侧栏归片工作区且侧栏无本地分片，但点击阶段检测到用户输入，为避免抢占窗口立即停止；源码/结构测试通过，交互序列未冒充完成。

## 8. 发布判断

当前 2.0.4 为 `candidate-blocked`。产品修复、完整测试、重建安装包和隔离安装/启动/卸载已完成，但隔离安装版的完整真实导航点击序列未完成，且既有物理 DPI 门禁仍未完成。因此不建议立即 Tag/合并，不建议进入 2.1.0。下一步应在无人操作窗口的明确隔离时段仅补一次导航点击烟测，确认后再封版。
