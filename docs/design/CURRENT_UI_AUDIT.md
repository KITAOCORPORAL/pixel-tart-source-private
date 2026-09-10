# Pixel Tart Current UI Audit

审计日期：2026-09-10

审计分支：`feature/asset-library-eagle-shell-context-p36`

审计范围：当前页面、XAML/UI 组件、Theme、Style、ResourceDictionary、Color、Font、Layout 与已有运行截图

性质：只读审计；本阶段未修改页面、组件、数据库或功能

## 1. Executive Summary

当前 Pixel Tart 已具备成熟桌面软件的功能骨架、暗色主题、语义资源基础和大量真实工作流，但视觉体验仍更接近“摄影业务后台 + 工具集合”，尚未稳定成为“摄影师的数字暗房”。

最主要矛盾不是缺少样式文件，而是内容优先级没有被统一执行：工作台由统计、任务、日历和空容器主导；素材库虽已有缩略图 Gallery，却被左侧分类、右侧分析/查询/批处理 Inspector、顶部筛选和底部状态持续压缩。界面资源同时存在新旧两代字典与大量页面局部 Style，使已建立的 Token 无法成为全产品唯一来源。

本轮结论：基础可迁移，不建议推倒重写。先冻结 v1.0 规范，后续按“Shell → 素材库 → Viewer/Thumbnail → 工作台 → 日历/项目 → 次级页面”分阶段收敛。

## 2. 审计方法与证据

### 2.1 阅读范围

- 应用资源入口：`src/RAWSelectionAssistant/App.xaml`；
- 主 Shell：`src/RAWSelectionAssistant/MainWindow.xaml`；
- 页面：`src/RAWSelectionAssistant/Views/*.xaml`；
- 素材库模块：`src/PixelTart.Modules.AssetLibrary/*.xaml`；
- 主题与公共资源：`src/RAWSelectionAssistant/Resources/DesignSystem/*.xaml`；
- 运行截图：`ui-review/modular-harness/01_workbench.png` 至 `10_module_diagnostics.png`；
- 既有设计资料：`docs/design/` 中本次 v1.0 之前的早期规范。

### 2.2 静态量化

对 `src` 中排除公共 DesignSystem 字典后的 XAML 统计：

| 项目 | 当前数量 | 风险解释 |
| --- | ---: | --- |
| XAML 行数 | 5,295 | 页面结构与样式集中，变更影响面大 |
| `Border` 实例 | 299 | 容器/卡片倾向明显 |
| `Button` 实例 | 488 | 常驻操作密度高，控件容易抢主体 |
| 页面级硬编码色值 | 181 | 主题与语义颜色可能漂移 |
| 硬编码 Margin | 1,110 | 大量碎片间距，难形成统一节奏 |
| 硬编码 Padding | 157 | 控件密度与触达面积不一致 |
| 显式 Width/Height | 439 | 长中文、高 DPI、小窗下存在裁切风险 |
| 页面/模块局部 Style | 130 | 公共组件出现分支，难以全局治理 |

最集中的文件：

| 文件 | 行数 | Border | Button | 硬编码色 | 硬编码 Margin |
| --- | ---: | ---: | ---: | ---: | ---: |
| `AssetLibraryPage.xaml` | 1,264 | 39 | 57 | 34 | 86 |
| `MainWindow.xaml` | 818 | 98 | 126 | 10 | 314 |
| `AssetLibraryP3Styles.xaml` | 584 | 9 | 10 | 102 | 23 |
| `TetherCaptureView.xaml` | 283 | 18 | 59 | 0 | 136 |

数量不等于单独缺陷，但与运行截图中的密集边界、工具竞争和局部色板相互印证。

## 3. 当前基础与可保留能力

- `App.xaml` 已集中合并 Dark、Accent、Spacing、Radius、Typography 与 Controls 字典；
- 已存在 Background / Surface / Text / Status 等语义色基础；
- 已有 4 DIP 间距 Token、字体资源、圆角与动效时长；
- Button、Input、Card、Table、Navigation、Menu、Dialog、Status 等已有公共 Style；
- Shell 已实现 Ctrl+N、Ctrl+S、Ctrl+B、F1、Alt+Enter 等键盘入口；
- 素材库已有统一缩略图方向、多布局、虚拟化、选择、右键与分析能力；
- Dark、Light、High Contrast 主题结构已经存在；
- 现有运行截图可用于之后建立视觉回归基线。

这些基础应被整理、命名统一和逐页采用，不应在 UI 实现阶段整体重写。

## 4. 当前视觉问题

### 4.1 企业后台感：明显

工作台截图由原生菜单栏、左侧分组导航、顶部工具入口、项目概览数字、处理任务、日程大框、最近项目大框、右侧日历与任务中心共同构成。首屏主要视觉对象是“0、空容器、更多、查看全部”，没有真实照片或项目封面。其结构符合 Dashboard 信息汇总，而非摄影桌面。

日历、收支、项目历史等模块大量依赖表格、状态与操作列；业务能力合理，但缺少封面、照片序列和拍摄故事入口。产品身份主要靠文案与相机图标表达，而不是作品本身。

### 4.2 廉价卡片感：中到高

`MainWindow.xaml` 中 98 个 Border 与 126 个 Button 反映出页面大量用“带边界矩形”组织内容。运行画面中项目概览、处理任务、日程、最近项目、日历、任务中心均有独立框体，几乎每块内容都像 Card。细碎边框替代了留白与排版，形成低成本 SaaS 面板感。

素材库也有分类框、Gallery 框、Inspector 框、筛选框与分析框层层包围。Card/Panel 的语义不清，同一视觉容器承担章节、工具、空状态和对象。

### 4.3 信息过载：明显

素材库右侧 Inspector 同时承载预览、分析状态、智能文件夹条件、视觉分析 Tab、颜色检索和批量分析。左栏同时包含模块诊断、文件夹、智能文件夹、标签；顶栏另有查询、导入和尺寸控制。大量功能真实存在，却没有按“当前选择/当前任务”渐进披露。

工作台同时回答项目统计、处理任务、今日日程、最近项目、工作日历、任务中心和异常文件，用户无法迅速判断下一步应继续哪项摄影工作。

### 4.4 图片区域不足：明显

- 工作台首屏没有真实图片主体；
- 素材库中央 Gallery 是主要区域，但三侧 Chrome 和多排工具共同挤压；
- 右侧 Inspector 更像分析控制台，而不是克制的当前选择信息；
- 当前截图中的缩略图只占页面上方一小段，大量下方区域为空，却未转化为更大缩略图或沉浸 Viewer；
- 灵感托盘当前实现呈现为 `AssetId` ListBox，不符合“一叠照片”的视觉模型；
- 项目与日历缺少稳定的图片关系入口。

### 4.5 控件抢主体：明显

素材库截图中高亮/白色输入框、禁用按钮、展开器、下拉框和 Slider 的对比度高于图片周围的 Metadata。顶部一排状态筛选以按钮形态占据高权重；模块诊断文字与用户内容同屏。工作台顶部工具按钮与右侧绿色新增按钮比空的作品区域更醒目。

### 4.6 颜色过多：中等，治理不足

公共 Dark Palette 已较中性，但同时存在：品牌青绿、Photography Gold、状态四色、日历多状态色、用户可选 Accent 以及素材库局部 102 个硬编码色值。当这些颜色在同一工作区并存时，没有统一面积预算和优先级。

部分输入与禁用控件在暗色界面中显示成大面积亮白块，既打断暗房氛围，也让不可用控件比可用照片更强。颜色已经“有命名”，但尚未完全“被治理”。

### 4.7 字体层级混乱：中到高

公共资源已有 PageTitle、Hero、Section、Card、Body、Secondary、Caption、Numeric 等多个级别，同时保留 legacy alias。页面局部仍直接指定 FontSize/FontWeight，标题、空状态、指标数字和面板标题之间的语义不稳定。

现有基础 Body 13、Secondary 12、Minimum Caption 11 DIP 偏小；复杂 Inspector 中小字与高密度字段叠加，不利于长时间观看和 150%/200% DPI。不同页面用字号和粗体临时建立层级，导致“处处像标题，主体仍不清楚”。

### 4.8 间距不统一：明显

公共 Spacing 已定义 4/8/12/16/24/32/48，但页面存在 1,110 处硬编码 Margin，并广泛出现 3、5、6、7、9、10 等数值。工作台的大面积空黑与素材库的密集工具并存，说明“空”尚未被设计成负空间，“挤”也未被统一密度约束解决。

## 5. 当前组件问题

### 5.1 Button

**现状**：公共按钮资源较完整，但 `Controls.Buttons.xaml` 与 legacy `Buttons.xaml` 并行；页面又定义 LocalSplitHeroButton、AssetLibraryPrimaryButton 等局部变体。Button 数量高，多个页面一屏出现很多同权重动作。

**问题**：Primary/Secondary/Ghost/Danger 的用途边界未全局执行；低频和未来动作长期占位；禁用按钮仍有强视觉面积；纯图标/符号/文字按钮混用。

**后续方向**：收敛到四类按钮；一个视域一个 Primary；工具栏默认 Ghost；低频进入 Menu/Context Menu/Command；Loading 保持尺寸。

### 5.2 Card

**现状**：`Controls.Cards.xaml` 与 `Cards.xaml` 并行，`PanelBorder` 等样式在页面承担多种容器职责。

**问题**：Card 被用于静态章节、统计、任务、空状态、日历和工具容器；嵌套边框密集；对象 Card 与布局 Panel 无法一眼区分。

**后续方向**：Card 只表示独立对象；章节用留白与标题；Panel 不自动有边框；禁止 Card 套 Card。

### 5.3 Menu / Context Menu

**现状**：原生顶部 Menu 长期可见，素材库已有右键扩展与命令基础。

**问题**：全局菜单、侧栏、工具栏和右键存在重复入口风险；右键缺少全产品统一的分组、命名、省略号与危险操作顺序；未来能力容易通过禁用项堆入。

**后续方向**：使用 `08_CONTEXT_MENU_SYSTEM.md` 的六组顺序；空分组不显示；菜单层级最多两层；冻结选择快照后再执行。

### 5.4 Dialog

**现状**：ThemedMessageDialog、多个业务 Window/Modal 和公共 Dialog Style 并存。

**问题**：短确认、复杂编辑、长期工作区可能都采用 Modal；宽度、Footer、Esc、Loading/Error 保留输入等契约不完全一致；页面式能力容易被塞进宽弹窗。

**后续方向**：Dialog 只承载短而阻塞的任务；复杂策划/标签/查询进入 Drawer 或 Workspace；统一 Small/Default/Wide 与 Focus Trap。

### 5.5 Panel

**现状**：左导航、右 Inspector、工作台右栏、Drawer 和内容章节都大量使用 Border/Panel 外观。

**问题**：Panel 角色交叉；空 Panel 仍长期占位；多面板同时展开压缩 Canvas；滚动区域可能嵌套。

**后续方向**：明确 Navigation/Canvas/Inspector；Canvas 获得剩余空间；无内容 Panel 收起；一个页面一个主滚动区。

### 5.6 Thumbnail

**现状**：素材库已有 Thumbnail Provider、异步加载、多种布局与基础 Metadata；这是现有 UI 最接近 Image First 的部分。

**问题**：缩略图周围仍显示文件名、评分与多项控制；图片尺寸偏小，空白没有转化为更大观看面积；选中、Loading、Error、缺失与离线的统一视觉契约需确认；局部色板可能影响选择状态。

**后续方向**：统一 `Thumbnail` 核心组件，保持比例，选择框在图外，渐进加载，默认 Metadata 减量，双击进入独立 Viewer。

### 5.7 Inspector

**现状**：素材库 Inspector 功能极强，包含预览、视觉分析、智能文件夹、颜色检索与批处理。

**问题**：角色越界最严重。Inspector 同时是详情、筛选器、编辑器、分析器、任务启动器；无选择也占固定宽度；大量常驻控件降低照片面积并形成 AI/工具控制台感。

**后续方向**：Inspector 只显示当前选择；高级分析、查询编辑和批处理进入按需 Drawer/专门工作区；无选择收起；多选只显示共同属性和批量动作。

### 5.8 Navigation

**现状**：Shell 左侧按工作/工具/系统分组，素材库内部另有分类树；支持折叠。

**问题**：一级导航包含较多业务、工具与系统入口；模块内诊断信息出现在用户导航区域；Shell 导航 + 模块导航形成双侧层级；图标、文字与分组选项密度高。

**后续方向**：一级导航只保留稳定工作空间；低频工具与系统入口降权；模块 Navigation 只负责内容集合；诊断仅在明确开发模式出现。

### 5.9 Input / Dropdown

**现状**：存在公共资源，但素材库和业务页面有大量局部输入布局。

**问题**：亮色输入块在暗色主题中过强；Label/Placeholder、Read-only/Disabled、Error 原因不总是一致；字段密集时依赖小间距与固定宽度，DPI 风险高。

**后续方向**：统一 38 DIP、外部 Label、Focus/Error 契约；复杂筛选使用 Query Drawer，避免 Inspector 两列小表单。

## 6. Theme 与 ResourceDictionary 问题

`App.xaml` 同时合并：

- 新资源：`Controls.Buttons/Inputs/Cards/Tables/Navigation/Menu/Dialogs/Status.xaml`；
- 兼容资源：`Buttons.xaml`、`Inputs.xaml`、`Cards.xaml`、`Navigation.xaml` 等；
- `DesignTokens.xaml` 内也保留 Space/Sidebar 等 legacy alias；
- `Theme.Dark.xaml` 与 `Colors.Dark.xaml`、Accent、模块 P3 Styles 之间存在多层颜色来源。

风险包括 Key 覆盖顺序、同语义不同名字、页面误用 legacy key、修改公共 Style 时只影响部分页面。后续应建立 token usage 清单，将 legacy 资源标记 Deprecated，按页面迁移后再删除；不能一次性移除兼容字典。

## 7. Layout 与 DPI 问题

Shell 当前默认 1600 × 920、最小 1180 × 720，方向合理；但大量显式 Width/Height、固定列宽、窄 Inspector 字段和硬编码 Margin 在 150%/200% DPI、长中文、小窗下仍有裁切风险。素材库多个右侧浮层使用 760/820/900 固定宽度；虽然设置 MaxWidth，内容在高 DPI 时仍可能需要纵向/横向重新组织。

后续不应建立每个 DPI 一份页面，而应通过 DIP、内容驱动、可折叠栏、Overflow 和明确响应式降级顺序解决。2K/4K 的新增空间应优先扩大图片与留白，不应把所有分析面板一起展开。

## 8. 与 v1.0 目标的差距

| v1.0 原则 | 当前状态 | 差距 |
| --- | --- | --- |
| 图片 > 空间 > 信息 > 控件 | 部分达成 | 工作台无图片主体；素材库控件密度过高 |
| 一个品牌 Accent | 部分达成 | 品牌金、日历状态、用户 Accent、局部色值缺少治理 |
| 六级字体 | 未统一 | 公共层级与页面局部字号并存 |
| 4px spacing | Token 已有 | 页面大量碎片硬编码 |
| 一个视域一个 Primary | 未稳定 | 工具栏、Card、右栏多个同权重动作 |
| Inspector 只服务选择 | 未达成 | 查询、分析、批处理长期共存 |
| Card 有明确边界 | 未达成 | Card/Panel/Section 视觉混用 |
| 完整状态与 Keyboard | 部分达成 | Shell 快捷键较好；组件全状态需逐一验证 |
| DPI 从设计开始 | 部分达成 | 有专项测试基础，固定尺寸仍多 |

## 9. 后续 UI 改造建议（不在本阶段实现）

### P0：系统收敛

1. 建立 v1.0 Token 到现有 XAML Key 的映射表；
2. 标记 legacy ResourceDictionary 与局部 Style，禁止新增引用；
3. 建立 Button/Card/Input/Thumbnail/Inspector 的视觉状态基线；
4. 补充 100%/125%/150%/200% 与 Dark/Light/High Contrast 的审查证据。

### P1：素材库 Image First

1. 将高级查询、智能文件夹编辑、标签管理、视觉分析批处理移出常驻 Inspector；
2. 无选择时收起 Inspector；
3. 提高 Gallery/Thumbnail 面积并减量 Metadata；
4. 建立独立 Image Viewer；
5. 将灵感托盘由 AssetId 列表改为图片胶片带（等待实现指令）。

### P2：工作台去 Dashboard 化

1. 用真实项目封面、最近素材和“继续工作”替代统计零值；
2. 合并日程/任务信息，只在有内容时显示；
3. 工具入口降级到命令/菜单；
4. 减少 Card 与空边框，建立摄影桌面节奏。

### P3：日历、项目与创作空间

1. 落实 `日期 → Booking → Project → Asset`；
2. 项目索引/详情加入封面与照片序列；
3. 灵感托盘、Moodboard、策划中心按视觉工作流实现；
4. 统一跨页返回、选择和滚动锚点。

### P4：次级页面与兼容资源退场

迁移收支、帮助、设置、业务表格、Dialog 和工具页；完成引用审计后逐步删除 legacy 字典与页面局部 Style。每一步保持行为不变并用截图/自动测试验证。

## 10. 本阶段未实现

- 未修改任何现有页面 UI；
- 未开发灵感托盘、Moodboard、策划中心或新一级导航；
- 未重写组件、Theme 或 ResourceDictionary；
- 未修改数据库、模型或业务逻辑；
- 未执行上述 P0–P4 改造；
- 未将设计 Token 写入 XAML；
- 未改变现有用户数据、源文件或运行行为。

## 11. 审计结论

Pixel Tart 已有足够工程基础进入统一设计阶段，但下一阶段不能继续在页面内部追加局部样式和控制器。v1.0 应成为所有新 UI 的唯一规范，并通过分阶段迁移让界面从“管理摄影数据”转向“观看并推进摄影工作”。
