# Stage V.2 Installable Reference Color Report

## 架构审计

| 项目 | 结果 | 证据 |
|---|---|---|
| 独立参考仿色核心 | PASS | `ReferenceLookMatcher` / `ReferenceLookPreviewService` / `ReferenceLookStore` |
| 独立参考仿色完整工具入口 | PASS（源码，待安装后验收） | `ReferenceColorWorkspaceView` / `ReferenceColorWorkspaceViewModel` / 正式工具箱入口 |
| 联机折叠参考模式 | PASS | `TetherCaptureView.xaml` 的「参考模式」Expander |
| 联机高级调整折叠 | PASS | `Advanced` Expander 与统一 `ReferenceLookParameters` |
| 共享仿色引擎 | PASS | `TetherReferenceModeViewModel` 调用 `ReferenceLookPreviewService` / `ReferenceLookMatcher` |
| 共享参数模型 | PASS | `ReferenceLookParameters` |
| 共享左右对比 | PASS | `TetherReferenceSplitView` 与同一 Preview 状态 |
| Session 临时状态 | PASS（源码修复，待安装后验收） | Tether 现场参数改为内存中的 session look，不再由滑块直接写入 `ReferenceLookStore`；安装后 round-trip 仍待人工验收 |
| 结果一致性 | PASS（核心） | Stage IV 色彩核心与 LUT 测试 19/19 通过 |
| Capture ingest 不阻塞 | PASS（代码契约） | Tether ingest 与参考 preview 分离、带 cancellation/revision |
| 显式保存当前调整 | PASS（源码，待安装后验收） | Tether UI 已接入显式保存命令；Slider 不再隐式写 Store |
| 恢复色彩方案 | PASS（源码，待安装后验收） | Tether UI 已接入恢复命令，恢复持久化方案参数 |
| 完整编辑器跳转 / Round Trip | PASS（源码，待安装后验收） | Tether 通过 `FullEditorRequested` 传入稳定 ID、项目、当前照片引用并导航到独立 workspace |

## 安装后流程状态

Fresh Install、Installed Launch、Planning Toggle、Tether、Reference Mode、Upgrade、Uninstall、Reinstall、真实用户视觉验收均未在本环境自动执行；报告不虚报为 PASS。安装器已生成，等待用户双击完成物理验收。

## 已修复阻塞

- `PlanningCenterView` 的 `ToggleButton` 不再使用 `Button` 类型样式。
- 启动失败后的 booking editor 清理链增加空引用保护。
- 新增 `StageV2StartupCompatibilityTests`，3/3 通过。
- 独立工具箱入口、共享对比、中文语言、Session 隔离与显式保存选择相关 WPF 聚焦测试 16/16 通过。
- 工具注册、工具箱、色彩与参考核心聚焦测试 67/67 通过。

## 运行时包安全

干净 publish 与 Inno Setup 文件过滤已排除 `*Acceptance.dll`、`*.Tests.dll`、`*TestHost*`、PDB、TRX、源码和 XAML。发布目录实际包含 `PixelTart.exe` / `PixelTart.dll`，不包含 `KitaoPhotoSelector.Acceptance.dll`。

## 结论

源码架构门已闭合，安装包已达到“可交给用户双击安装”的候选状态；真实安装、升级、卸载、重装、独立↔Tether round trip 及物理 UI 结果必须由用户在普通电脑上确认后，才能将最终 Release Readiness 标记为通过。不要进入 Stage VI。
