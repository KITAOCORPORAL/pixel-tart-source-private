# Pixel Tart Core Reliability Hotfix：Modal、Overlay、Tutorial 交互根因

## 范围

本轮只处理弹窗、覆盖层和新手教程的交互可靠性，不新增产品业务功能，不修改日历业务、SchemaVersion、产品版本、云端能力或在线拍摄架构。验证构建类型为 `CoreReliability_InteractionHotfix_DevValidation`，不是 RC3。

## 精确根因

此前没有统一的 Modal 会话合同。设置、排期编辑器、工具箱 Popup、RAW 转 JPG、批量压缩和教程覆盖层分别通过 `Visibility`、Popup 状态、页面导航或窗口关闭处理；Esc 只覆盖少数页面。教程退出直接触发主窗口关闭事件，未等待教程后台任务、取消当前操作或释放覆盖层。异步命令原来只有 fire-and-forget 入口，调用方无法在退出前等待完成。Step18 只依据布尔报告标记，不读取实际磁盘文件，因此默认关闭 JSON/TXT 时不能准确报告缺失文件。

## 受影响页面与组件

- RAW 转 JPG、批量压缩、拼图、归片页面及其取消命令。
- 设置 Modal、QuickCreate、QuickEdit Drawer、FullPlanning 编辑器。
- 工作台工具箱 Popup、快捷工具溢出 Popup、在线选片创建项目 Modal。
- 教程遮罩、教程卡片、教程退出/重试/重建/返回，以及 Step18 报告导出。
- `MainWindow` 的 Esc 路由、`ThemedMessageDialog` 错误对话框、任务中心状态同步。

## 共享组件

新增 `IModalSession`、`IModalHost`、`ModalSession` 和 `ModalHost`。关闭与取消均通过会话请求执行；会话回调失败时保持打开并允许重试；取消请求可并发调用但只执行一次。`AsyncRelayCommand.ExecuteAsync` 提供可等待的命令合同，同时保留 `ICommand.Execute` 兼容入口。

## 修复方案

1. `MainWindow` 将统一 Esc 入口映射到 ModalHost；RAW、批量压缩、拼图和归片先执行现有取消命令，再返回工作台。排期编辑器、设置和工具箱 Popup 也经由 ModalSession 收口。
2. 教程退出先取消当前令牌，等待取消演示、操作完成和教程动作，再调用 `OnboardingService.ExitAsync` 保存未完成步骤并退出教程；随后恢复普通工作区。重复退出保持幂等，不调用主窗口关闭事件，也不删除用户数据。
3. 教程 Step18 强制检查 `匹配报告.csv`、`匹配报告.json`、`操作日志.txt` 的真实存在性，记录期望数、生成数和缺失列表；缺失时保持错误状态并允许重试、重建、返回或退出。
4. 遮罩显式拦截背景输入，教程卡片提升 ZIndex 且可交互，退出按钮提供稳定自动化名称。所有异步路径在 `finally` 恢复 Busy/取消状态。

## Step18 缺失报告合同

Step18 的成功条件是三个文件都在教程输出目录中存在。界面应显示 `generated/expected`，并列出缺失文件；不能用设置默认值或单一布尔值代替磁盘校验。CSV、JSON、TXT 任何一个缺失都必须允许重试、重建数据、返回上一步或安全退出，不能让全局 Busy 状态禁用退出。

## 文件安全与范围边界

教程数据仍只写入独立 Tutorial Sandbox。退出只保存当前步骤并恢复普通工作区，不删除真实项目、客户资料、LocalAppData 数据、RAW 原片或用户输出。没有新增数据库表、迁移、Schema 变更或 RC3 功能。

## 验证限制

`ModalCloseSmokeTests` 和 Core 合同测试不实例化真实 WPF 窗口，覆盖源码合同、绑定、取消路径和错误对话框 Esc 行为。前台安装版的 `InstalledUiVerified`、`UserVerified` 仍必须由真实用户确认；自动测试、隐藏桌面或截图不能替代人工验收。Step18 的真实文件结果需在独立临时目录中复核。
