# Pixel Tart UI Design System Phase 1 Implementation Report

状态：完成  
日期：2026-09-10  
设计系统：Pixel Tart UI Design System v1.0  
范围：Design Token、Theme、ResourceDictionary 入口、公共 Button / Surface / Menu 基础与渐进兼容迁移

## 结论

Phase 1 已建立可运行的工程基础。应用继续保持现有页面结构和业务行为，同时获得统一的 `PixelTart.Theme` 与 `PixelTart.Components` 入口、跨主题语义 Token、公共组件状态和 legacy 兼容层。素材库 P3 公共样式中的 102 个局部硬编码色值已全部迁移到语义 Token。

本阶段遵守 `Image First`、`Calm Interface` 与 `Photography Darkroom`：使用中性色表面、唯一 Accent、克制边界与阴影，不新增页面 Chrome、卡片墙、装饰色或高干扰动画。本阶段没有重新设计页面，也没有修改 Asset Model、Service、Repository、Database、Import 或 Query 逻辑。

## 已迁移 Token

### Colors

- 新增 `Brush.Background`、`Brush.Surface`、`Brush.Surface.Hover`、`Brush.Surface.Elevated`、`Brush.Panel`。
- 新增 `Brush.Border`、`Brush.Border.Strong`。
- 新增 `Brush.Text.Primary`、`Brush.Text.Secondary`、`Brush.Text.Muted`、`Brush.Text.Disabled`。
- 新增 `Brush.Accent`、`Brush.Accent.Hover`、`Brush.Accent.Active`、`Brush.Accent.Subtle`、`Brush.OnAccent`。
- 新增 `Brush.Status.Success`、`Brush.Status.Warning`、`Brush.Status.Error`、`Brush.Status.Info`、`Brush.Overlay`、`Color.Shadow`。
- Dark、Light、High Contrast 三套主题均提供完整语义键；High Contrast 使用 Windows 系统颜色，阴影为透明。
- 保留现有颜色键作为兼容别名，没有一次性删除旧资源。

### Typography

- 新增统一字体族 `Font.Family.UI` 与 `Font.Family.Numeric`。
- 新增六级文字角色：`Type.Display`、`Type.PageTitle`、`Type.SectionTitle`、`Type.Body`、`Type.Metadata`、`Type.Caption`。
- 为六级角色建立字号、行高与 `PixelTart.Type.*` 样式。
- 现有字体资源继续保留，供未迁移页面使用。

### Spacing

- 建立 `Space.1 / 2 / 3 / 4 / 6 / 8 / 12`，对应 4 / 8 / 12 / 16 / 24 / 32 / 48 DIP。
- 建立页面、Panel、Card、Gallery、Menu、MenuItem、Control 与紧凑 Control 的语义 Thickness。
- 建立 `Space.Inset.*`、`Space.Inline.*` 与 `Space.Stack.*`，用于替代局部 Margin / Padding。
- 素材库 P3 公共样式已将 16 处数字 Margin 和 9 处数字 Padding 收敛到 4px Token；模板内部为焦点环、箭头与 ComboBox Chrome 服务的数学定位暂时保留。

### Size / Motion / Elevation

- 新增 Compact / Default / Large 控件尺寸、MenuItem 高度和 Inspector 最小/最大宽度。
- 新增 Instant / Fast / Standard / Slow / Reveal 动效时长。
- 新增 None / Floating / Dialog Elevation；阴影零位移、低透明度，并通过主题化 `Color.Shadow` 控制，高对比度不使用阴影表达层级。

## ResourceDictionary 收敛

- `PixelTart.Theme.xaml` 是 Token、Typography 与 Elevation 的统一入口。
- `PixelTart.Components.xaml` 是无页面语义的规范组件入口，当前聚合 Menu 基础与 Foundation。
- `App.xaml` 首先加载可运行时替换的 `Theme.Dark.xaml`，然后加载两个统一入口。
- 现有 `Controls.*` 与 legacy 字典仍按原依赖顺序位于 App 顶层并保持自包含。原因是 WPF 编译视图和脱离 App 的嵌入式宿主测试不能可靠解析嵌套兄弟字典依赖；本阶段以可运行和渐进迁移为优先，不提前删除兼容层或建立反向依赖。
- AppearanceService 仍可替换顶层 `Theme.Dark / Light / HighContrast` 槽位，未改变主题切换业务代码。

## 已迁移组件

### Button

- 规范 API：`PixelTart.Button.Base / Primary / Secondary / Ghost / Danger / Loading`。
- 已覆盖 Normal、Hover、Active / Pressed、Focus、Disabled、Loading。
- Disabled 使用中性表面和 Disabled Text；Loading 保持布局稳定并阻止重复交互。
- `PrimaryButton`、`SecondaryButton`、`Av2PrimaryButton`、`Av2SecondaryButton` 等现有键继续保持自包含兼容实现；新页面可直接使用规范 API，旧页面按后续页面批次迁移。

### Panel / Card / Border / Inspector

- 规范 API：`PixelTart.Surface.Border / Panel / Card / Inspector / Loading`。
- 已覆盖 Normal、Hover、Active（`Tag=Active` 兼容状态）、Focus、Disabled、Loading。
- Panel 默认不依赖阴影；Card 通过安静表面与细边界表达；Inspector 使用受约束宽度与单侧边界。
- `PanelBorder`、`PanelSurface`、`CardSurface` 等现有键继续保持自包含兼容实现；同时补充兼容的 Inspector / Loading 变体，避免影响独立嵌入式宿主。

### Menu

- 规范 API：`PixelTart.Menu.Context / Item / Loading`。
- 复用现有 Menu / ContextMenu 模板的 Hover、Active、Focus、Disabled 行为，补充 Loading 键。
- `Av2ContextMenu` 与 `Av2ContextMenuItem` 继续保持自包含兼容实现，并补充兼容 Loading 变体；新页面使用规范 API。

### 素材库公共样式

- `AssetLibraryP3Styles.xaml` 的 Button、Toggle、TextBox、DatePicker、ComboBox、List、Tree、Popup 与 Query Node 表面已从局部 Hex 改用 `Brush.*`。
- Dark、Light、High Contrast 现在共享同一语义层；原有交互、AutomationId、Command 与数据绑定不变。

## 未迁移页面

Phase 1 不做页面重设计。以下页面仍主要使用 legacy 键、页面内 Style 或数字布局值，留给后续按页面迁移：

- Shell / 工作台：`MainWindow.xaml`。
- 素材库页面结构：`AssetLibraryPage.xaml`、`AssetTagManagerView.xaml`、`AssetSmartFolderEditorView.xaml`、`AssetQueryComposerView.xaml`。本阶段只迁移其共享 P3 样式中的颜色和部分间距。
- 日历 / 排期：WorkCalendar、Month / Week / Day Calendar、Booking、Reminder、Weather、Document 等页面。
- 图片工作流：Organize Photos、Collage、RAW to JPEG、Tether Capture、Candidate Selection、Client Monitor、Media Details。
- 在线选片、项目历史、摄影收支、设置、帮助、反馈、教程与业务弹窗。
- 页面内 130 个局部 Style 尚未收敛；Thumbnail、Input、Dropdown、Dialog、Toast、Navigation 的完整规范组件迁移不在本阶段扩大范围。

## 剩余硬编码统计

统计范围：`src/**/*.xaml`，排除 `Resources/DesignSystem`、`bin`、`obj`。数值属性只统计直接以数字开头的 XAML 值；DynamicResource / StaticResource 不计入。

| 项目 | HEAD 基线 | Phase 1 当前 | 变化 | 说明 |
| --- | ---: | ---: | ---: | --- |
| 硬编码颜色 | 181 | 79 | -102 | 素材库 P3 公共样式已清零局部 Hex |
| 硬编码 Margin | 1,118 | 1,102 | -16 | 设计审计曾记录约 1,110；本报告使用可重复的当前 Git HEAD 扫描 |
| 硬编码 Padding | 176 | 167 | -9 | 仅迁移共享样式中可安全映射项 |
| 硬编码字号 | 131 | 131 | 0 | Token 层已建立，页面级替换留给渐进迁移 |
| 页面 / 模块局部 Style | 130 | 130 | 0 | 本阶段建立公共落点，不批量改页面 |

剩余颜色主要位于 `AssetLibraryPage.xaml`（34）、`AssetTagManagerView.xaml`（12）、`MainWindow.xaml`（10）、`AssetSmartFolderEditorView.xaml`（9）、`AssetQueryComposerView.xaml`（7）和 `ClientMonitorWindow.xaml`（7）。剩余 Margin 主要位于 `MainWindow.xaml`、`TetherCaptureView.xaml`、`AssetLibraryPage.xaml` 与 Booking / Finance 页面。

## 测试与验收

- 全解决方案 Debug 构建：通过，0 警告、0 错误。
- Design System 锁定测试：9 / 9 通过。
- WPF 设计系统、素材库四种浏览布局、缩略图 Provider、嵌入式素材库导入与布局测试：41 / 41 通过。
- 素材库导入、源文件不变、分页 / 取消、合并与启动安全测试：33 / 33 通过。
- 隔离 Acceptance 发布与真实进程启动：通过；主窗口可见、可响应，`WorkbenchRoot` 可由 UI Automation 定位。
- 素材库导航：通过；路由进入真实“未创建素材库”空状态并显示“新建素材库”。为遵守不修改业务数据，本阶段没有在该隔离 UI 中创建持久库。
- 图片浏览 / 缩略图 / 导入：由嵌入式 WPF 合成 JPEG 测试覆盖。测试通过公开应用导入路径填充 Gallery，验证四种浏览布局、缩略图异步完成与缓存、Missing / Offline 状态，并校验源文件字节未变化。

## 风险

- `Controls.*` 与 legacy 字典仍存在。重复键与加载顺序仍需治理；删除前必须按引用审计逐个迁移，不能一次移除。
- 79 个页面级硬编码颜色、1,102 个数字 Margin、167 个数字 Padding、131 个数字字号和 130 个局部 Style 仍会造成局部主题与密度漂移。
- 当前公共 Loading 是稳定视觉与重复提交保护的基础状态，不等同于完整 Progress 内容模板；复杂异步组件仍需在后续组件阶段补齐 Busy / Empty / Error / Success 契约。
- Menu 继续依赖现有模板；边缘避让、二级 Hover 路径与全 DPI 真实屏幕矩阵需随页面迁移持续验证。
- 本阶段没有改变页面信息架构，因此工作台 Dashboard 感、素材库 Inspector 密度、图片面积与未实现菜单项等审计问题仍存在；这些属于下一阶段 UI Prototype / 页面迁移范围。
- 工作树中存在与本阶段无关的业务层本地修改。本次提交只包含 Theme、Styles、Components、设计系统测试与本报告，不纳入那些业务改动。

## 下一阶段

Phase 1 完成后停止。下一阶段：Asset Library UI Prototype。
