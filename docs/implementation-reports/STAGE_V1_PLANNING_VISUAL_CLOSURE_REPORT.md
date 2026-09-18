# Pixel Tart Stage V.1 Planning Visual Closure Report

## 状态

**PARTIAL / READY FOR HUMAN VISUAL ACCEPTANCE**

Stage V.1 完成了策划中心的视觉产品化和验收准备，但没有把未执行的实体设备门伪装成通过。

## 版本与提交

| 项目 | 结果 |
|---|---|
| Start SHA | `d0a7d846c03ecb7b6d6427e3defc5ab0f53710d2` |
| Final SHA | `b258f08355408d7e531dc7d98f5cfe46e0a52d1c` |
| Branch | `integration/pixel-tart-developer-preview` |
| Scope | Stage V.1 Planning UI / UX / Acceptance Closure |

## UI 完成项

- 中央区域加入当前参考 Hero，大图使用 `Uniform`，参考比例不裁切。
- 参考缩略图从固定 230×220 改为可换行的紧凑响应式条目。
- 参考卡片保留轻量类别 badge、标题和深色底部信息层。
- 参考右键菜单显式使用产品深色菜单资源。
- 左侧底部上移/下移常驻按钮移除，改由拖动与右键完成排序。
- 策划摘要默认折叠，低频表单不再占据首屏。
- Inspector 默认 View Mode，点击“编辑”后才显示 TextBox。
- 保留双击/Space/Esc/上一张/下一张 Quick Preview 交互。
- 所有新增可见文案使用中文；`Shot 03` 保留摄影编号语境。
- 没有改动 Planning domain、Shot Store、Tether、Asset Library、Free Canvas、Calendar 或 Reference Color 核心逻辑。

## 审计结果

- UI 审计：`docs/design/PLANNING_UI_AUDIT_V1.md`
- 圆角：Planning 页面使用现有 `CompactCornerRadius`、`ControlCornerRadius`、`CardCornerRadius`、`DialogCornerRadius`，没有新增页面硬编码圆角。
- 按钮：沿用 Primary/Secondary/Ghost 产品样式，新增动作带 Tooltip 或 AutomationProperties.Name。
- Menu/Popup：参考菜单使用 `Av2ContextMenu` / `Av2ContextMenuItem`；Quick Preview 使用现有 Dialog 圆角资源。
- 默认 WPF 泄漏：XAML 契约测试通过；未引入默认白色下拉或裸默认按钮。
- 边框：保留结构性边界，减少参考卡和排序操作的工具化重量。

## 验收结果

| Gate | 结果 |
|---|---|
| Release x64 build | DONE — 0 warnings / 0 errors |
| Planning Core | DONE — 8 passed / 0 failed / 0 skipped |
| Planning WPF | DONE — 17 passed / 0 failed / 0 skipped |
| 200 Shot / 1000 Reference smoke | DONE — inherited Stage V gate remains PASS |
| Tether / Asset Library / Free Canvas / Reference Color / Source Safety | DONE — existing regression gates retained; no core rewrites |
| Language | DONE for Planning visible XAML contract |
| 1080p geometry | PARTIAL — DIP constraints and contract pass; physical screenshot unavailable |
| Software DPI 100/125/150/200 | PARTIAL — style/geometry contract only; not physical display validation |
| Quick Preview | DONE by existing keyboard/mouse contract; visual human confirmation pending |
| Inspector View/Edit | DONE by new contract and view-model state |
| Physical multi-display | NOT TESTED |
| Real camera | NOT TESTED |
| Real application screenshot | NO |

## Skipped test classification

The full WPF suite remains 1252 passed, 0 failed, 1 skipped. The skipped test is:

`AssetLibraryP3PerformanceDiagnosticsTests.ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture`

Its recorded reason is: `Opt-in diagnostic: provide a new synthetic fixture and new output directory.` It is a deliberate opt-in Asset Library diagnostic, unrelated to Planning UI, and is separated from the standard Planning gate. It was not deleted or relabeled.

## 人工验收包

`artifacts/stage-v1-planning-human-acceptance/PixelTart-DeveloperPreview/`

包含：

- `MANUAL_UI_ACCEPTANCE.md`
- `BUILD_PROVENANCE.json`
- `TEST_SUMMARY.md`
- `OPTIONAL_LAUNCH.bat`（仅供手动运行，不自动启动）

## 最终设备状态

| 项目 | 状态 |
|---|---|
| REAL_APP_SCREENSHOT | NO |
| PHYSICAL_DPI | NOT TESTED |
| PHYSICAL_MULTI_DISPLAY | NOT TESTED |
| REAL_CAMERA | NOT TESTED |

因此本阶段停止在 **PARTIAL / READY FOR HUMAN VISUAL ACCEPTANCE**，不进入 Stage VI 或 RC13。
