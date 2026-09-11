# Pixel Tart UI Phase 2 — Asset Library Visual Migration Review

状态：通过（Phase 2）
日期：2026-09-11
分支：`feature/asset-library-eagle-shell-context-p36`
范围：仅 `AssetLibraryPage` 页面 UI、Thumbnail 视觉契约、页面验收测试与本评审证据。

## 1. 结论

Asset Library 已迁移到 Pixel Tart Design System，作为第一张 Image First 样板页。页面继续保持左侧组织导航、中央 Gallery、右侧 Inspector 的工作区骨架，但 Gallery 获得剩余空间；无选择时 Inspector 收起，选中后按“图片 → 名称 / 项目 / 标签 / 评分 → 拍摄信息 → 技术信息”渐进披露。

顶部常驻操作收敛为搜索、筛选、排序、布局和唯一 Primary「导入」；低频组织、分析、撤销 / 重做和灵感托盘进入「更多」或对象 Context Menu。图片保持原始比例，选择框位于图片外，Hover 只改变安静边界。

## 2. 使用的 Design Token

### Color

- `Brush.Background` / `Brush.Panel` / `Brush.Surface` / `Brush.Surface.Elevated`
- `Brush.Border` / `Brush.Border.Strong`
- `Brush.Text.Primary` / `Brush.Text.Secondary` / `Brush.Text.Muted` / `Brush.Text.Disabled`
- `Brush.Accent` / `Brush.Accent.Subtle` / `Brush.Accent.Hover` / `Brush.Accent.Active`
- `Brush.OnAccent`、`Brush.Status.Error`

页面没有新增 Hex、RGB、页面级命名色或第二套 Accent；图片底色和错误占位均使用中性语义资源。

### Typography

- `PixelTart.Type.SectionTitle`
- `PixelTart.Type.Metadata`
- `PixelTart.Type.Caption`

页面标题、照片名称、事实型 Metadata 与辅助说明没有重新声明字号。

### Space / Size / Radius

- `Space.Inset.1 / 2 / 3`
- `Space.Inline.Start.1 / End.1`
- `Space.Stack.1 / 1.Symmetric`
- `Space.Control.Compact.Padding`
- `SpacingVertical8 / SpacingVertical12 / SpacingHorizontal8`
- `Size.Control.Compact`、`Size.Inspector.MaxWidth`
- `RadiusSmall`、`RadiusControl`、`RadiusCard`

Gallery gap、Panel padding、工具组间距和 Inspector 信息间距均落在 4px 基础序列；页面没有新增任意间距、圆角或阴影。

## 3. 使用的 Components

- `PixelTart.Thumbnail`：统一 ListBoxItem 状态；Default、Hover、Selected、Keyboard Focus、Disabled。
- `PixelTart.Button.Primary`：唯一主操作「导入」。
- `PixelTart.Button.Ghost`：搜索辅助、筛选、排序、布局、导航和上下文低权重动作。
- `PixelTart.Button.Secondary`：空状态、Inspector 与托盘的稳定动作。
- `PixelTart.Surface.Panel`：导航与 Gallery 工作区表面。
- `PixelTart.Surface.Inspector`：当前选择的右侧上下文面板。
- `PixelTart.Surface.Border`：抽屉、批量选择摘要与浮层边界。
- `PixelTart.Menu.Context` / `PixelTart.Menu.Item`：对象菜单和低频菜单。

没有创建新的 Button、Card、Tab 或页面私有颜色组件。高级视觉分析、查询编辑、标签管理与批量分析仍由现有命令 / Drawer / 菜单承载，未改变业务逻辑。

## 4. 修改范围

### 页面 UI

- `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml`
  - 重新组织 Toolbar、Gallery、Navigation 与 Inspector。
  - 删除页面私有按钮 / Tab 视觉样式与硬编码色值。
  - 统一 Thumbnail 模板的比例、留白、失败占位和选中外围轮廓。
  - 移除 Inspector 常驻视觉分析、颜色检索和批处理控制；这些能力从「更多」/ Context Menu 进入。
  - Inspector 单选层级改为图片、名称 / 项目 / 标签 / 评分、拍摄信息、技术信息；多选只显示共同 Metadata 和源文件安全说明。
  - 灵感托盘仍是引用集合；未改变引用、删除或源文件语义。

- `src/PixelTart.Modules.AssetLibrary/AssetLibraryThumbnail.xaml`
  - 复用公共 `PixelTart.Thumbnail` 视觉契约。

- `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs`
  - Toolbar 菜单使用公共 Menu token；筛选按钮控制现有 Query Composer；保留现有选择、布局切换、排序与键盘行为。

- `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs`
  - Inspector 仅在有选择且空间足够时显示；取消选择后自动收起右侧面板。
  - 未修改 Asset Model、Repository、Service、Import、Query 或 Database 语义。

### 验收测试与证据

- `tests/RAWSelectionAssistant.WpfTests/AssetLibraryVisualPhase2Tests.cs`
  - 使用合成比例混合图片覆盖 Grid / Masonry / Justified / List。
  - 验证异步缩略图解码、`Stretch=Uniform`、无失败、Gallery 宽度比例和导入源文件 SHA-256 不变。
- `docs/design/evidence/phase2/`
  - 真实 WPF RenderTargetBitmap 截图矩阵与 `results.json`。

## 5. 截图证据

### 1920×1080 / 100%

- [Grid](evidence/phase2/asset-library-1920x1080-100-Grid.png)
- [Masonry](evidence/phase2/asset-library-1920x1080-100-Masonry.png)
- [Justified](evidence/phase2/asset-library-1920x1080-100-Justified.png)
- [List](evidence/phase2/asset-library-1920x1080-100-List.png)

### 2560×1440 / 100%

- [Grid](evidence/phase2/asset-library-2560x1440-100-Grid.png)
- [Masonry](evidence/phase2/asset-library-2560x1440-100-Masonry.png)
- [Justified](evidence/phase2/asset-library-2560x1440-100-Justified.png)
- [List](evidence/phase2/asset-library-2560x1440-100-List.png)

### 3840×2160 / 100% 与 200%

- [4K Grid / 100%](evidence/phase2/asset-library-3840x2160-100-Grid.png)
- [4K Masonry / 100%](evidence/phase2/asset-library-3840x2160-100-Masonry.png)
- [4K Justified / 100%](evidence/phase2/asset-library-3840x2160-100-Justified.png)
- [4K List / 100%](evidence/phase2/asset-library-3840x2160-100-List.png)
- [4K Grid / 200%](evidence/phase2/asset-library-3840x2160-200-Grid.png)
- [4K Masonry / 200%](evidence/phase2/asset-library-3840x2160-200-Masonry.png)
- [4K Justified / 200%](evidence/phase2/asset-library-3840x2160-200-Justified.png)
- [4K List / 200%](evidence/phase2/asset-library-3840x2160-200-List.png)

`results.json` 记录了每张截图的 Gallery 宽度比例和成功解码缩略图数量。1920×1080 Gallery 比例为 0.71875，2560×1440 为 0.7890625，3840×2160 为 0.859375；4K / 200% 回落到 0.71875，符合先保护图片观看、再收缩 Chrome 的响应式策略。

## 6. 测试结果

- `dotnet build RAWSelectionAssistant.sln -c Debug --no-restore`：通过，0 警告、0 错误。
- `AssetLibraryVisualPhase2Tests.ThemeImportBrowseAndResolutionMatrix`：通过。
  - 12 张合成素材成功导入。
  - Grid / Masonry / Justified / List 均成功浏览。
  - 缩略图全部成功解码且保持比例。
  - 导入前后源文件 SHA-256 一致，未修改源文件。
- 设计系统 Foundation 与已有 Asset Library 自动化测试所需的公开 AutomationId 保留；旧的 Inspector 视觉分析控制不再作为页面常驻入口。

## 7. 人工验收记录

通过真实 WPF 渲染截图人工检查：

- 1080p：中央 Gallery 获得主要内容宽度；Toolbar 不超过五个同层级操作；Inspector 信息可读。
- 2K：增加图片数量 / 留白，没有增加控制器密度。
- 4K：图片区域继续扩大；100% 与 200% 下没有裁切、重叠或比例变化。
- Selected：Accent 细线位于图片外，未染色覆盖像素。
- Inspector：默认图片、名称、项目、标签、评分；拍摄信息和技术信息折叠。
- Empty / Loading / Error：保留真实状态容器、源文件安全文案和重试入口。

本环境的 Windows Computer-use 辅助进程未启动，因此没有进行额外的桌面鼠标录制；截图矩阵来自与生产页面相同的 WPF 渲染树和资源字典，并已逐张视觉检查。

## 8. Review 结论

**通过。** 本阶段只迁移 Asset Library UI，未修改 Asset Model、Repository、Service、Import、Query 或 Database。提交信息：

`refactor(ui): migrate asset library to Pixel Tart design system`
