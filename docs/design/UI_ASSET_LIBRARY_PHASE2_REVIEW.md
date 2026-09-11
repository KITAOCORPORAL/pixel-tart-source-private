# Asset Library — Pixel Tart UI Phase 2 Review

状态：已完成（2026-09-11）

## 范围

仅迁移 `TetherCaptureView` 的视觉表现与响应式列宽：左侧缩略图浏览器、中央图片预览区、右侧检查器和会话工具栏。未修改 Asset Model、Repository、Service、Import、Query 或 Database。

## 页面改动

- 左侧从厚重的横向文件条目改为图片优先缩略图：固定预览区使用 `Uniform` 保持原始比例，文件名、时间与评分退为次要信息。
- 去除逐项重边框。默认仅为安静的 `SurfaceTertiary` 图片底；选中时使用细 `AccentBrush` 描边。
- 中央图片区保持最大弹性宽度；缩略图列收窄至 236 DIP、检查器至 296 DIP，并维持中央区 640 DIP 最小宽度。
- 检查器默认以当前图片、名称、素材库归属、格式、评分和配对状态开始；标记与备注紧随其后；会话、直方图、拍摄/技术信息均默认折叠。未选择素材时沿用既有收起逻辑。
- 顶部只保留浏览器、自动最新、锁定、一个“更多”菜单和全屏；LUT、客户监看、检查器、任务中心移入“更多”。

## 使用的 Design Tokens

- 色彩：`ContentBackgroundBrush`、`SurfaceSecondaryBrush`、`SurfaceTertiaryBrush`、`AccentBrush`、`PhotographyGoldBrush`、`WarningBrush`、`DividerBrush`。
- 排版：`CardTitleText`、`SectionTitleText`、`CaptionText`。
- 尺寸与空间：`Space4Thickness`、`Space8Thickness`、`Space12Thickness`、`ControlCornerRadius`。
- 响应式约束：中央预览最小 640 DIP；窄窗口沿用检查器抽屉逻辑。

## 使用的 Components

- `PixelTartPanel`：左侧素材浏览器与当前素材摘要。
- `PrimaryButton`、`SecondaryButton`、`GhostButton`：沿用已有按钮体系，未增加页面私有按钮样式。
- 标准 `ComboBox`、`ListBox`、`Expander`、`TextBox` 和 `Slider`：均由全局 Design System 提供输入与交互状态。
- `AssetLibraryThumbnailItemStyle` / `AssetLibraryThumbnailFrame`：页面内仅为现有 Thumbnail component 的布局与选中呈现，不定义颜色、按钮或卡片体系。

## 修改的文件

- `src/RAWSelectionAssistant/Views/TetherCaptureView.xaml`
- `src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs`
- `tests/RAWSelectionAssistant.DpiTests/Version230StageCLiveMonitorDpiTests.cs`
- `tests/RAWSelectionAssistant.DpiTests/Version230TetherDpiGateTests.cs`

## 验证

- `dotnet build RAWSelectionAssistant.sln -c Debug --no-restore`：通过，0 warnings / 0 errors。
- 素材缩略图、虚拟化浏览、导入不写入源文件和素材库引用导入：11 项 WPF 测试通过。
- 素材浏览器三栏布局、检查器折叠与缩略图按需加载：79 项 WPF 测试通过。
- 1080p、2K、4K：逻辑 DPI 门禁覆盖 100%、125%、150%、175%、200%，并验证无手工 DPI 变换、中央最小宽度和窄屏检查器抽屉。

## 截图

已配置隔离的 WPF RenderTarget 截图状态 `TetherAssets`（1920×1080），但当前既有 UI Review 可执行程序启动时在未加载该页面前即因全局 `GhostButton` 静态资源加载顺序失败，无法生成图片。此故障存在于本阶段页面渲染之前；未将临时资源加载顺序试验纳入提交。逻辑布局、缩略图和导入验证均已完成。

待基础主题加载缺陷修复后，应重新执行：

`tools/RC2Review/Invoke-RC2Review.ps1 -SkipBuild -OutputRoot artifacts/ui-review/phase2-asset-library-20260911`

预期输出路径：`artifacts/ui-review/phase2-asset-library-20260911/AssetLibrary_1920x1080.png`。
