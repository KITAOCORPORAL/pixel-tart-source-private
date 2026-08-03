# 像素蛋挞 2.0.4 隔离自动验收与正式封版报告

## 结论

像素蛋挞 2.0.4 已完成最终隔离自动验收，符合正式封版条件。验收全过程由 Codex 在 Win32 `CreateDesktopW` 创建的独立 Windows 桌面执行，没有切换、置前或操作用户当前桌面，没有要求用户关闭软件、停止输入、切换缩放、截图或运行脚本。

## 1. 隔离方式

- Windows Sandbox 功能状态为 Disabled，因此按既定优先级采用 Win32 `CreateDesktopW` 独立桌面。
- 每次运行使用唯一 `AcceptanceRunId`、独立安装目录、独立测试图片和独立证据目录。
- UI Automation 只绑定本轮安装版进程；安装版程序集业务探针只加载本轮隔离安装目录中的 DLL。
- 最终通过运行 ID：`c7bd9d0cf878473c841abebc0ff9a8ea`。
- 用户当前桌面是否被操作：否。

## 2. 完整导航烟测

- 默认启动进入工作台：通过。
- 启动时未自动弹出拼图、未自动导入照片、未自动选择 2×2：通过。
- 首页“开始本地分片”打开本地分片快速向导：通过。
- 通过“工作台”返回关闭快速向导：通过。
- 侧栏“归片工作区”打开完整归片工作区：通过。
- 本地分片快速向导与归片工作区为不同页面：通过。
- 侧栏不存在“本地分片”一级入口：通过。
- 项目历史、工作台、工具箱 Popup、完整工具箱、设置打开与关闭：通过。
- 单次点击单次导航；7 次预期导航对应 7 次执行：通过。

## 3. 拼图专项

- 拼图入口只产生一个页面实例：通过。
- 等待后无第二次自动导航、无自动文件选择器、无自动导入、无自动 2×2：通过。
- 快速重复触发仍为单实例：通过。
- 导入防重入门控：通过。
- 4 张隔离图片、2×2 模板、JPG 导出、PNG 导出：通过。
- 导出文件可解析：通过。

## 4. 快捷工具专项

- 工具箱 Popup 和“管理快捷工具”窗口：通过。
- 排序保存、重启后设置仍存在、恢复默认并保存：通过。
- 验收使用 `KitaoPhotoSelector.Acceptance` 专用配置目录，结束后删除，不改动用户正式设置。

## 5. 整理图片专项

- 整理图片页面入口：通过。
- 4 张隔离测试图片、按文件格式分组、预览操作清单：通过。
- 默认复制、不覆盖、不删除源文件：通过。
- 从隔离安装目录加载正式程序集执行复制，成功 4/4：通过。
- 输出文件存在，源文件仍存在：通过。

## 6. 源文件完整性

整理复制和拼图导出前后对 4 张测试源图计算 SHA-256，全部一致。`sourceFileIntegrityVerified=true`。

## 7. DPI 验证

- `dpiValidationMode=automated-logical-simulation`。
- 125%：通过。
- 150%：通过。
- 200%：通过。
- 阻断级越界、重叠、文字裁切：0。
- `physicalDpiManuallyTested=false`。本报告不宣称在真实物理显示器上完成人工 DPI 测试；该已知限制按批准策略不阻断 2.0.4。

## 8. 自动化与发布门禁

- 测试：490/490 通过（核心 458、逻辑 DPI 27、WPF 导出 5；其中导航安全专项 20）。
- Provider：None。
- Release Mock：禁用。
- WinExe：是，无控制台。
- localhost / 独立后台服务器：无。
- 安装、启动、重启、关闭、卸载：通过。
- 未发现新的产品缺陷；本轮只修正了隔离验收工具的窗口定位和证据采集。

## 9. 构建与安装包

- 本轮未再次构建或覆盖安装包；使用此前在产品修复和 490 项测试后生成的唯一候选。
- 路径：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.0.4\installer\像素蛋挞_Setup_2.0.4_x64.exe`
- 大小：48,791,104 字节。
- SHA-256：`8F2131EBF6E13EDF990639F57FE8BEB189BD4829464382CADEF682CDEFAAFEFD`。
- 旧候选保存在 `artifacts\releases\2.0.4\obsolete-candidates`。

## 10. 证据与清单

- 隔离验收：`D:\AI AGENT\RAWSelectionAssistant\artifacts\diagnostics\2.0.4\isolated-desktop-acceptance\latest-result.json`
- 逻辑 DPI：`D:\AI AGENT\RAWSelectionAssistant\artifacts\automated-dpi-review\2.0.4\AutomatedDpiResults.json`
- Release manifest：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.0.4\release-manifest.json`
- Manifest 状态：`released`；`releaseEligible=true`；`installedInteractionTested=true`；`sourceFileIntegrityVerified=true`。

## 11. 产品缺陷与重建说明

此前发现的导航重复和拼图导入防重入问题已在 `release/2.0.4` 修复，并已在 490 项测试与本次隔离验收中验证。本轮未发现新的产品缺陷，因此没有重新构建安装包。

## 12. 工作日历蓝图

- 蓝图已存在：`docs/roadmap/像素蛋挞_工作日历设计.md`。
- 正式产品计划版本：2.2.0。
- 2.1.0 仅预留 SQLite 与本地提醒接口，不提前开发工作日历页面。
- 已包含 `ShootBooking`、`ShootRequirementItem`、`BookingDocument`、`BookingReminder`；文档本体不写入 SQLite；金额仅本地记录；不加入支付、CRM 或预约商城；默认不联网。

## 13. Git 封版

- 发布分支：`release/2.0.4`。
- 正式源码提交、main 合并提交和 `v2.0.4` 标签由封版步骤完成，并以 Git 最终状态为准。
- 完成标签后停止开发，不自动开始 2.1.0。

## 14. 后续建议

建议在用户明确确认后进入 2.1.0，但当前任务完成后保持停止状态，不提前开发 2.1.0 或工作日历页面。
