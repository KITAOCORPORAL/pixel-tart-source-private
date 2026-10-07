# Computer Use 实机问题记录

日期：2026-10-07（Asia/Shanghai）。对象：同一 `color-studio-rebuild-2026-10-05-r1` Release。生产源码和测试未在本轮修改。旧版 `CUI-001`（把 100% 直接判为物理像素失败）已撤回：本轮没有完成源像素与物理屏幕像素的对应测量，因此不登记为问题。

## 结论

本轮发现 6 个可复现问题：P1 两项、P2 三项、P3 一项。所有问题均保留为 `USER_VISUAL_REVIEW=NOT_APPROVED`；本轮总状态为 `NOT_READY_FOR_USER_RETEST`。`CODE` 记录当前代码行为，`TEST` 只记录有独立范围的验证，不能替代实机结果。

## 问题清单

| ID | 位置 | 输入代号 | 窗口 / 实际 DPI | 严重度 | RELEASE_RUNTIME | 复现性 |
|---|---|---|---|---|---|---|
| CU-01 | 右侧编辑栏、窄窗口 | ChartPNG | 1180×720 DIP / DPI120（125%）；1180×720 DIP / DPI192（200%） | P1 | FAIL | 两档均观察到 |
| CU-02 | 展开 3D 浮窗 | ChartPNG | 1600×920 DIP / DPI144（150%） | P2 | FAIL | 展开后稳定复现 |
| CU-03 | 同步到所选弹层 | ChartPNG | 1600×920 DIP / DPI96（100%） | P2 | FAIL | 选择 9 张时复现 |
| CU-04 | 语言切换文本 | ChartPNG | 1180×720 DIP / DPI120；1600×920 DIP / DPI144 | P2 | FAIL | English/繁體均可复现 |
| CU-05 | 数值编辑框和 Escape | synthetic public input | 1920×1080 DIP / DPI144（150%） | P1 | FAIL | `abc` 输入路径复现 |
| CU-06 | 影调区间分析组 | ChartPNG | 1600×920 DIP / DPI144（150%） | P3 | PARTIAL | 需要内滚才能发现 |

## CU-01｜窄窗口右侧编辑内容不可达

- **位置 / 复现步骤**：进入 Color Studio；将窗口调整到 1180×720 DIP；在 125%（DPI120）和 200%（DPI192）各启动一次；打开右侧 Color/Levels 等页并向下检查数值框、复位按钮和 Creative 页。
- **预期**：面板可通过自身滚动、折叠或明确的窄窗布局到达所有编辑参数和操作，不裁掉关键控件。
- **实际**：右栏可见区域高度不足，RGB选择器、数值编辑/复位和 Creative 内容出现裁切或不可达；英文窄窗还出现左侧模式文字截断。主图仍可显示，但主要编辑流程无法完成。
- **证据**：[112](evidence/112-r1-after.png)、[114](evidence/114-r1-after.png)、[123](evidence/123-r1-after.png)。
- **源码线索**：`src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml` 的 `EditingRail`、工具页和 `AnalysisScroll`；`ReferenceColorWorkspaceView.xaml.cs` 的 `UpdateResponsiveLayout`（响应式列宽、窄窗单栏逻辑）。本轮未修改源码，根因仍需在新修复构建中确认。
- **状态**：`CODE=CONFIRMED_CURRENT_BEHAVIOR`；`TEST=NOT_RUN_THIS_REVIEW`；`RELEASE_RUNTIME=FAIL`；`USER_VISUAL_REVIEW=NOT_APPROVED`。
- **建议**：为右栏工具内容提供独立可见滚动区和窄窗折叠策略，逐一重放 100/125/150/200% 与三档窗口。

## CU-02｜展开 3D 浮窗覆盖工作区

- **位置 / 复现步骤**：在右侧 3D Color 页点击展开；拖动旋转、开启平移、滚轮缩放并执行 Fit；观察浮窗与主窗口关系，再关闭浮窗。
- **预期**：展开视图可检查点云，同时主图仍是工作区主体；浮窗不覆盖中央照片和右侧编辑栏，设置/相机状态行为应有明确合同。
- **实际**：展开窗口覆盖中央照片右侧和编辑栏；浮窗相机与 mini view 相机独立，旋转/平移后的状态不会以一致方式回写。Orbit、Pan、Zoom、Fit 本身有可见响应，但工作区被遮挡。
- **证据**：[105](evidence/105-r1-before.png)、[106](evidence/106-r1-after.png)、[107](evidence/107-r1-after.png)。
- **源码线索**：`ReferenceColorWorkspaceView.xaml.cs` 的 `OnExpandColorSpace`、`CreateInspectionWindow`、`PlaceInspectionWindow`；浮窗由独立 `ColorSpace3DViewport` 创建。运行时覆盖属于窗口放置/职责问题，不能由“点云可旋转”关闭。
- **状态**：`CODE=CONFIRMED_CURRENT_BEHAVIOR`；`TEST=NOT_RUN_THIS_REVIEW`；`RELEASE_RUNTIME=FAIL`；`USER_VISUAL_REVIEW=NOT_APPROVED`。
- **建议**：扩大/停靠左侧辅助区或使用不遮挡主图的受限面板；明确 mini/expanded 是否共享相机并测试边缘翻转。

## CU-03｜同步弹层数量没有说明源图排除

- **位置 / 复现步骤**：选择 9 张照片，其中一张是当前源图；打开“同步选定/应用调整”弹层；执行同步并观察弹层数量与完成提示。
- **预期**：弹层明确显示“源图 1 张、目标 8 张”，执行数量与文案一致，不误导用户以为会写入 9 张目标。
- **实际**：弹层显示 9 张，实际执行 toast 显示 8 张；当前源图确实被排除，但用户无法在操作前看到这一点。
- **证据**：[100](evidence/100-r1-before.png)、[101](evidence/101-r1-after.png)。
- **源码线索**：`src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Filmstrip.cs` 的 `SyncDestinationTargets` 已排除 `ActiveTarget`；`ReferenceColorWorkspaceView.xaml` 同步弹层使用 `SelectedTargetCount`，而 `ReferenceColorWorkspaceViewModel.cs` 的完成提示使用目标集合长度，造成两个口径。
- **状态**：`CODE=PARTIAL_BEHAVIOR`；`TEST=NOT_RUN_THIS_REVIEW`；`RELEASE_RUNTIME=FAIL`；`USER_VISUAL_REVIEW=NOT_APPROVED`。
- **建议**：弹层统一显示源图、可见所选数、排除源图后的目标数；完成提示沿用同一个冻结目标集合。

## CU-04｜语言切换后存在大量简体中文硬编码

- **位置 / 复现步骤**：在 Color Studio 从简体中文切换到 English，再切换到繁體中文；打开左侧模式、右侧工具页、分析组、底部同步/导出和帮助提示。
- **预期**：产品界面文本随语言设置更新；用户节点名和用户数据可保留原文。
- **实际**：顶层模式名称有变化，但分析来源、部分工具说明、底栏摘要和旧帮助文本仍是简体中文。用户自定义节点名保留原文属于预期，不计入此问题。
- **证据**：[114](evidence/114-r1-after.png)、[115](evidence/115-r1-after.png)。
- **源码线索**：`src/RAWSelectionAssistant/Resources/Studio/zh-CN.json`、`en-US.json`、`zh-TW.json` 已有资源；`ReferenceColorWorkspaceView.xaml` 与 `.xaml.cs` 仍可见直接中文字符串和硬编码提示。具体缺口需按控件逐项迁移资源键。
- **状态**：`CODE=PARTIAL_LOCALIZATION`；`TEST=NOT_RUN_THIS_REVIEW`；`RELEASE_RUNTIME=FAIL`；`USER_VISUAL_REVIEW=NOT_APPROVED`。
- **建议**：补齐 Color Studio 所有产品文本资源键，保留用户节点/文件名原样，重启后复验语言持久化。

## CU-05｜非法数值被拒后 Escape 没有清除草稿

- **位置 / 复现步骤**：打开 Color Studio 数值编辑框；输入 `abc` 并按 Enter；确认输入被拒绝且照片/节点不变；再次聚焦同一框按 Escape；观察页面和草稿文本。
- **预期**：非法输入被拒绝后，Escape 清除未提交草稿并留在当前编辑页；不会因为清除草稿而退出 Color Studio。
- **实际**：Enter 拒绝 `abc`；再次聚焦按 Escape 后草稿未按预期清除，随后路由回到 Home。批次数据仍保留，但编辑上下文丢失。
- **证据**：[299](evidence/299-r1-after.png)、[300](evidence/300-r1-after.png)、[302](evidence/302-r1-after.png)、[303](evidence/303-r1-after.png)、[304](evidence/304-r1-after.png)。
- **源码线索**：`ReferenceColorWorkspaceView.xaml` 数值绑定（例如 Exposure 等编辑框）；`src/RAWSelectionAssistant/MainWindow.xaml.cs` 的 shell Escape 路由；`ReferenceColorWorkspaceView.xaml.cs` 的 `TryClearTransientInspection` 只处理采样/overlay，不负责数值草稿。本轮没有改动。
- **状态**：`CODE=CONFIRMED_CURRENT_BEHAVIOR`；`TEST=NOT_RUN_THIS_REVIEW`；`RELEASE_RUNTIME=FAIL`；`USER_VISUAL_REVIEW=NOT_APPROVED`。
- **建议**：为数值编辑器定义统一的提交/取消合同；在 shell 路由前先让活动编辑框清除草稿并保持当前 surface，增加非法输入和 Escape 的 WPF 实机测试。

## CU-06｜影调区间需要内滚才可见

- **位置 / 复现步骤**：打开右侧分析组，在 1600×920 DIP、DPI144 下查看 RGB/明度直方图下方的影调分区；先不滚动，再在分析区域内滚动到底部并悬停分区。
- **预期**：分析组的标题、分区色块和可交互入口具有明显发现性；必要时滚动应有明确提示。
- **实际**：首次视口只看到说明顶行，0–X 分区需要在内层滚动后才出现；滚动后各区可达，悬停可在主图显示对应区域，Esc 可清除 overlay。功能本身有响应，问题属于发现性和布局。
- **证据**：[063](evidence/063-r1-after.png)、[065](evidence/065-r1-after.png)、[066](evidence/066-r1-after.png)、[067](evidence/067-r1-after.png)。
- **源码线索**：`ReferenceColorWorkspaceView.xaml` 的 `AnalysisScroll`、`ToneZoneDrawing`；`ReferenceColorWorkspaceViewModel.Analysis.cs` 的 `HighlightToneZone` 与成员计算。映射在本轮已看到，但没有把线性亮度等分冒称曝光 Zone System。
- **状态**：`CODE=PARTIAL_BEHAVIOR`；`TEST=NOT_RUN_THIS_REVIEW`；`RELEASE_RUNTIME=PARTIAL`；`USER_VISUAL_REVIEW=NOT_APPROVED`。
- **建议**：提高分区色块和状态提示的首屏可见性，保持分析组独立滚动，并在窄窗复验命中区域。

## 证据与批准边界

所有公开图片均为完整窗口、同一 r1 Release 的脱敏副本；详细文件身份见 [BUILD_IDENTITY.json](BUILD_IDENTITY.json) 和 [SCREENSHOT_MANIFEST.json](SCREENSHOT_MANIFEST.json)。本文件不把历史自动测试、XAML 存在或旧截图当作运行通过。`VisualApproved=false`、`UserVerified=false`、`USER_VISUAL_REVIEW=NOT_APPROVED`。
