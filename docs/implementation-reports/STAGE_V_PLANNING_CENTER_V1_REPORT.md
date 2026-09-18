# Pixel Tart Stage V Planning Center v1 — Implementation Report

## 结论

Stage V Planning Center v1 已完成代码、持久化、UI、跨模块衔接与自动化验收门。结论为 **PARTIAL / READY FOR CONTROLLED HUMAN ACCEPTANCE**：自动化门通过；真实 WPF 应用截图、实体显示器 DPI/多屏、真实相机链路未在当前执行环境完成，因此不伪造“真实机器已验收”。

## 版本与提交

| 项目 | 值 |
|---|---|
| Branch | `integration/pixel-tart-developer-preview` |
| Start SHA | `79360e724c882136b8b38603d24e75780462af4b` |
| Stage V architecture | `0ad58ba` |
| Planning domain/persistence | `06027b6` |
| Planning workspace | `e58baa4` |
| Visual/captured-asset closure | `00f64e4` |
| Final HEAD | `29314c6d69a742ba8c906b6e3c8f3d46f0b690dd` |

## 实现范围

- Project overview、日期/地点/进度摘要与保存状态。
- Shot 列表：新建、复制、删除、排序、当前拍摄、已拍/跳过状态。
- Shot inspector：标题、场景、预计时长、备注，带防抖自动保存。
- 参考图：已有素材库引用、项目本地引用、参考类型与备注，排序和删除。
- 视觉方向：项目色板、色彩方案、目标影调分布、灵感板与自由画布摘要。
- Planning → Tether：进入当前 shot 的拍摄执行上下文。
- Tether → Planning：保存执行上下文并将新捕获素材关联回当前 shot。
- Capture relation：稳定键、重复写入去重、Library/Tether 来源区分、可选 LibraryId。
- Calendar/Booking：有项目的预约进入 Planning Center，无项目预约继续使用原编辑流程。
- Asset Library 反向导航：从 Planning 当前项目查看已拍素材。
- Source safety：引用保持来源类型、来源 ID、原始路径与只读语义，不复制或移动用户源文件。

## 架构与迁移

复用现有 `PlanningProjectStore`、`ProjectVisualReferenceStore`、`InspirationProjectStore`、`FreeCanvasProjectStore`、`WorkCalendarViewModel`、`TetherCaptureViewModel` 与 `TetherShotExecutionViewModel`。新增结构集中在 Planning domain、执行上下文和 capture relation；旧项目 JSON 使用兼容默认值读取，缺失字段不破坏启动。没有新增 Moodboard/AI/Browser Clipper 等范围外页面或功能。

## 自动化验收

| Gate | 结果 | 证据 |
|---|---|---|
| Restore | PASS | `dotnet restore RAWSelectionAssistant.sln` |
| Release x64 build | PASS | 0 warnings / 0 errors |
| Core full suite | PASS | 1367 passed, 0 failed, 0 skipped |
| WPF full suite | PASS with environment skip | 1252 passed, 0 failed, 1 skipped |
| Planning core filter | PASS | 8 passed |
| Planning WPF filter | PASS | 14 passed |
| Performance smoke | PASS | 200 shots / 1000 refs under 10 s gate |
| Capture dedupe smoke | PASS | repeated relation writes collapse to one relation |
| Product language scan | PASS | visible XAML text is scanned; internal identifiers are not user-facing text |
| Migration/default read | PASS | legacy project state loads with safe defaults |

TRX evidence is stored under `artifacts/stage-v-planning/test-results/`.

## 截图与实体设备状态

- `REAL_APP_SCREENSHOT = NO`：当前 CUA 环境没有可操作的本机 WPF 应用窗口（仅检测到浏览器/内置页面），所以没有生成合成截图。
- 1920×1080、2560×1440、4K：未在实体显示器上执行。
- 100%、125%、150%、200% DPI：未在实体显示器上执行。
- 多屏拖拽：未执行。
- 真实相机/联机拍摄：未执行。

上述项目保留给普通摄影师电脑的受控人工验收，不以自动化通过替代。

## 已知限制与后续人工门

WPF 全套中有 1 项因当前环境能力跳过；它不构成代码失败，但需要在具备对应桌面能力的机器上重跑。Stage IV 历史报告仍标记为 PARTIAL 的项目保持原状态；本阶段没有扩大范围去做视觉重设计。正式 Release Candidate 需在真实设备清单、真人第一次使用记录和真实截图补齐后再签署。
