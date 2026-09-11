# UI_PHASE24_RC8_DARK_EAGLE_SHELL

分支：`integration/pixel-tart-developer-preview`  
版本：Pixel Tart Developer Preview RC8 / 2.3.0

## Single Dark Theme

RC8 删除了用户可访问的主题、强调色和自定义颜色入口；设置页固定显示 `PixelTart Dark Theme`。旧设置在加载时迁移为 Dark + Tart Teal。Windows 强制 High Contrast 仍由系统辅助功能接管，不被应用阻断。

全局浮层契约覆盖 Window、Dialog、Popup、ContextMenu、MenuItem、ComboBox dropdown、Calendar、ToolTip、Drawer、Overlay、Toast 和素材库创建窗口；素材库样式不再引用 `SystemColors.WindowBrush` 或 `SystemColors.ControlBrush`。

## Calendar Dark Skin

保留 WPF 原生 Calendar/CalendarItem/MonthView/ItemsPresenter/CalendarDayButton 骨架，仅覆盖暗色背景、文字、边框、选中、今天和 hover 资源。没有重新实现 6×7 布局。

## New Asset Library Dark Dialog

`NewAssetLibraryDialog` 现在是无系统白底的 Pixel Tart 深色窗口：名称、目录、最终路径预览、取消和创建按钮均位于 Raised Surface；系统文件夹选择器仍可保持 Windows 自身主题。

## Thumbnail Black Border Root Cause

根因是素材卡固定 frame 使用纯黑容器在不同图片比例下形成 letterbox；不是源图黑边。RC8 将缩略图 frame 设为透明/图库背景、保持 `Stretch=Uniform`，并移除选中态填充，避免 renderer 人工制造黑框。原比例由 Grid/Masonry/Justified 布局引擎保持。

## Asset Library Eagle Shell

- 左栏：分类、最近/未分类/未标签/缺失/归档/回收站、文件夹树、智能文件夹、标签分组，紧凑可展开。
- Toolbar：搜索优先；筛选、排序、布局、导入、更多；导入仍是唯一 Primary，布局/排序/筛选使用 Ghost。
- Gallery：Grid/Masonry/Justified/List + 实时密度滑块，图片主体、弱化元数据、选择保持。
- Inspector：预览、文件名、评分、标签、拍摄/技术信息，并保留 Pixel Tart 摄影工作流关系入口。
- Context Menu：选择感知的查看、整理、视觉工具、灵感托盘、归档等命令；未实现能力不伪装成可执行按钮。

## Eagle Parity

详细状态见 [`ASSET_LIBRARY_EAGLE_PARITY_MATRIX.md`](../design/ASSET_LIBRARY_EAGLE_PARITY_MATRIX.md)。RC8 完成单暗色、四布局、密度、导入与多选基础；完整 viewer、Trash 冲突恢复、项目/booking/client Inspector 字段与 Eagle 全部筛选浮层仍为 PARTIAL。

## Pixel Tart Difference

Pixel Tart 保留 AssetOrigin、Project/Booking/Calendar relation、Online Selection、Retouch/Delivery state、Inspiration Tray、Moodboard 和 Planning Center 的产品关系模型；本阶段只把可用部分嵌入 Eagle shell，不开发新的业务编辑器。

## Tests

- `SingleDarkThemeRegressionTests`：6/6 PASS。
- `AssetLibraryP36ContextMenuContractTests`：2/2 PASS。
- `AssetLibraryVisualPhase2Tests.ThemeImportBrowseAndResolutionMatrix`：1/1 PASS；生成 1920×1080、2560×1440、3840×2160（含 200% DPI）四布局证据，所有缩略图保持 `Stretch=Uniform` 且源文件哈希不变。
- `PixelTart.ModularHarness.Tests`：14/14 PASS。
- 核心套件：1298/1299 PASS；唯一失败为 `Scenario11_TenKSwitchAndHundredKMetadataPreview` 在本机 30,383 ms 超过 30,000 ms 性能阈值。
- WPF 全量仍受既有按钮审计/P1 分支白名单和未签入旧 DPI evidence fixture 约束；本轮新增 RC8 暗色、菜单、缩略图契约均已单独通过。

## Screenshots

RC8 证据目录：`artifacts/ui-review/2.3.0-rc8/`。已生成并保留 1920/2560/3840 及 200% DPI 的 Grid/Masonry/Justified/List 视觉回归截图；仓库既有命名的 shell/menu/calendar/dialog PNG 也已存在。Windows `sky` 运行时在本次桌面未配置，因此未新增真实 UI 自动化截图。

## RC8 Installer

Filename: `像素蛋挞_Setup_2.3.0_RC8_x64.exe`  
SHA-256: `B890B4F4F54683AC904C6DD3B0F87E3F732EB2B68C6DD94D30D14E3E3330787A`
