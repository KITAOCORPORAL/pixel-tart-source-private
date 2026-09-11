# Pixel Tart Developer Preview RC6：真实机回归修复报告

日期：2026-09-11

## Git / Branch topology

- RC5 来源 branch：`feature/online-selection-v1`，报告记录的构建 HEAD 为 `393c1a5`。
- Asset Library branch：`feature/asset-library-eagle-shell-context-p36`，验收实现 HEAD 为 `ea075b2`（其上游设计系统提交为 `0566f8e`）。
- RC5 与 Asset Library 分支共同基线：`4dac5f8`。
- 最终 Integration branch：`integration/pixel-tart-developer-preview`。
- 本次修复在独立工作树 `pixel-tart-developer-preview-rc6` 完成；没有 merge `main`，没有 force push，也没有覆盖 RC5 文件。

结论：RC5 确实由不完整的 `feature/online-selection-v1` 构建。该分支带入了设计系统和素材库视觉迁移提交，但没有带入 `AssetLibraryModule`、模块宿主和一级导航所在的完整 Asset Library 集成提交，因此出现“设计系统已存在、素材库功能入口缺失”的分叉。

## Root Cause 1

素材库一级导航本身在完整集成分支中存在，但 RC5 分支没有保留 `AssetLibrary` 路由、`ModuleWorkspaceHost` 和对应模块注册的完整组合。RC6 基于专门 integration branch，复用现有 `AssetLibraryPage`、Gallery、Inspector、Thumbnail、Query 和 Import，不复制页面、不改数据模型。

## Root Cause 2

DatePicker 使用的 CalendarItem 模板把日历头部和月份视图放在普通按钮的紧凑尺寸约束下，月份标题、星期栏和 6×7 日期网格没有独立的最小尺寸。RC6 为 `CalendarDayButton`、CalendarItem 和 MonthView 设置独立宽高/最小宽高，并为上一月、下一月和标题按钮设置独立命中区域；42 个单元格不再继承 Compact Button 高度。

## Root Cause 3

侧栏工具箱复用了工作台快捷工具的 `ToolboxQuickButton_Click`，该处理器在非工作台页面先执行 `NavigateCommand("Workbench")` 再打开浮层，导致一级导航回到工作台。RC6 将侧栏入口改为 `NavigateCommand` + `Toolbox`，并保留浮层仅供工作台快捷按钮使用。

## Root Cause 4

部分标题区以 StackPanel 流布局承载标题和关闭按钮，并通过固定右边距避让。长中文标题、缩放或字体变化时仍可能发生碰撞。RC6 的共享 `SurfaceHeader` 使用标题列 + 固定关闭按钮列；`SurfaceCloseButton` 保持独立 40×40 hit target、无文字 Content、居中图标和统一 hover/pressed/focus 行为。

## 修复文件

- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/MainWindow.xaml.cs`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml`
- `src/RAWSelectionAssistant/Views/SurfaceHeader.xaml`
- `installer/RAWSelectionAssistant.iss`
- `tests/RAWSelectionAssistant.WpfTests/NavigationWorkbenchClosureTests.cs`
- `tests/RAWSelectionAssistant.WpfTests/Version230Rc6RegressionContractTests.cs`

## 自动测试

- `dotnet build RAWSelectionAssistant.sln --no-restore`：PASS，0 warning / 0 error。
- RC6 定向 UI 契约：PASS，21/21（导航、工具箱路由、日历尺寸、SurfaceHeader、全局关闭出口）。

## 真实截图验收

当前环境未连接用户的真实显示器会话，无法冒充 100%/125%/150%/200% 实机截图。RC6 安装包已生成；待用户在真实电脑安装后采集并补齐：

- `01_asset_library_navigation.png`
- `02_asset_library_page.png`
- `03_toolbox_workspace.png`
- `04_new_shoot_datepicker_100.png`
- `05_new_shoot_datepicker_150.png`
- `06_dialog_close_button.png`
- `07_tether_page.png`

## RC6

- Installer：[像素蛋挞_Setup_2.3.0_RC6_x64.exe](../../artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC6_x64.exe)
- Version：`2.3.0_RC6`（产品程序集版本保持 `2.3.0`，候选标识由安装包文件名区分）
- SHA-256：`3C4011F1579A85AE05723DF028E48F63727C72DA893A241002D35D5473018A27`
- Publish：`artifacts/releases/2.3.0/publish/win-x64`

## 未解决问题

- 尚未在用户真实电脑完成全新安装、覆盖 RC5 升级安装和四档 DPI 人工验收。
- 尚未生成上述 7 张真实 WPF 截图；这是当前唯一需要用户真实机参与的验收项。
- 安装包未签名（沿用现有发布环境）。
