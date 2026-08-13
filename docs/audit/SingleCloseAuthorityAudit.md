# Single Close Authority Audit

## 规则

- 同一个 Surface 在任何时刻必须恰好显示一个 X。
- Full Page Module 只显示根级 `ShellSurfaceCloseButton`；模块内部的 X 删除或隐藏。
- Modal / Drawer 只显示自己的 Header X；本地 Header X 可见时，Shell X 由 `ShellSurfaceCloseStyle` 折叠。
- Tutorial 只显示 `TutorialCalloutCloseButton`，同时保留文字按钮“退出教程”；两者均委托 Shell 的教程退出路径。
- 共用 X 的可见图形为 16 DIP，完整命中区域至少为 40 × 40 DIP。

## 审计矩阵

| Surface | 当前可见 X 数量 | X 所属 VisualTree | AutomationId | 最终保留 | 删除 / 隐藏 |
|---|---:|---|---|---|---|
| RAW 转 JPG | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | `RawToJpegModal/SurfaceHeader` 的 X 由 `ShowCloseButton=False` 隐藏 |
| 批量压缩 | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | `BatchCompressionModal/SurfaceHeader` 的 X 由 `ShowCloseButton=False` 隐藏 |
| 拼图 | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | 删除 `CollageView` Header 内嵌 X |
| 整理图片 | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | 删除 `OrganizePhotosView` Header 内嵌 X |
| 归片工作区 | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | 删除 `WorkflowWorkspace` Header 内嵌 X |
| 本地分片 | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | 删除 `LocalSplitWorkspace` Header 内嵌 X |
| 工具箱 | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | 删除 `ToolboxFullPage` Header 内嵌 X |
| 在线选片项目 | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | 删除在线选片主 Header 内嵌 X |
| 摄影收支 | 1 | `RootGrid/ShellSurfaceCloseButton` | `ShellEmergencyCloseButton` | Shell X | 删除收支主 Header 内嵌 X |
| Tutorial | 1 | `TutorialOverlay/TutorialCard` | `TutorialCalloutCloseButton` | Tutorial Card X | `IsOnboardingActive=True` 时隐藏 Shell X；“退出教程”为文字按钮，不计入 X |
| 设置 Modal | 1 | `SettingsModal` Header | 无专用 ID | Modal Header X | `IsSettingsModalOpen=True` 时隐藏 Shell X |
| 排期 Quick Modal / Drawer | 1 | `BookingEditorOverlay/QuickBookingEditorView` | 无专用 ID | Header X | `BookingEditorOverlay=Visible` 时隐藏 Shell X |
| 排期 Full Planning Modal | 1 | `BookingEditorOverlay/ShootBookingEditorView` | 无专用 ID | Header X | `BookingEditorOverlay=Visible` 时隐藏 Shell X |
| 排期详情 Drawer | 1 | `WorkCalendarView/ShootBookingDetailsView` | 无专用 ID | Header X | `WorkCalendarPage.IsDetailsOpen=True` 时隐藏 Shell X |
| 在线选片创建 Modal | 1 | `OnlineSelectionCreateSurface` | 无专用 ID | Header X | `OnlineSelectionPage.IsCreateModalOpen=True` 时隐藏 Shell X |
| 摄影收支编辑 Drawer | 1 | `FinanceEditorSurface` | 无专用 ID | Header X | `FinancePage.IsEditorOpen=True` 时隐藏 Shell X |
| 任务详情 Drawer | 1 | `TaskDetailsSurface` | 无专用 ID | Header X | `TaskCenter.IsTaskDetailsOpen=True` 时隐藏 Shell X |
| 独立消息 / 错误对话框 | 1 | `ThemedMessageDialog` Header | 无专用 ID | Dialog Header X | 独立 Window 不渲染 MainWindow Shell X |

## 自动门禁

`SingleCloseAuthorityTests` 固化以下约束：

- Full Page 视图不得声明本地 `SurfaceCloseButton`。
- RAW、Batch 的共享 Header 必须设置 `ShowCloseButton=False`。
- Tutorial 中只允许一个 X，并保留一个文字退出按钮。
- Modal / Drawer 状态必须触发 Shell X 折叠。
- Surface 可见 X 数量必须等于 1，且不得大于 1。
- 共用 X 的有效命中区域不得小于 40 × 40 DIP。
