# Pixel Tart Layout System v1.0

## 1. 空间模型

标准工作区由五层组成：

`Window → Workspace → Navigation + Canvas + Inspector`

- Window：系统窗口与全局命令边界；
- Workspace：当前摄影任务上下文；
- Navigation：去往不同工作空间或内容集合；
- Canvas：图片、项目、时间故事或自由编排的主体；
- Inspector：当前选择的辅助信息与上下文编辑。

Canvas 永远获得剩余空间。Navigation 和 Inspector 不得同时以最大宽度挤压 Canvas。

## 2. 4px Spacing System

所有 Margin、Padding、Gap 使用 4 DIP 基础系统：

| Token | Value | 用途 |
| --- | ---: | --- |
| `Space.1` | 4 | 图标与短标签、紧密 Metadata |
| `Space.2` | 8 | 同组控件、紧凑图片间距 |
| `Space.3` | 12 | 标准 Gallery Gap、字段内部 |
| `Space.4` | 16 | Panel Padding、控件组间距 |
| `Space.6` | 24 | 页面 Margin、Section 间距 |
| `Space.8` | 32 | 大章节、沉浸内容边界 |
| `Space.12` | 48 | 空状态和叙事留白 |

禁止发明 3、5、6、7、9、10、14、18、20、28 等视觉间距。例外只允许数学居中、1 DIP 边框或图片排布算法产生的小数，且不写成公共 Token。

## 3. Window

- 默认启动尺寸：1600 × 920 DIP；
- 最小可用尺寸：1180 × 720 DIP；
- 小于最小尺寸不靠压缩字体解决；
- Window 顶部只承载全局命令、当前上下文与全局任务状态；
- 状态栏仅显示持续、必要、不可从内容推断的状态，可为空时收起；
- 系统标题栏、菜单栏与应用 Top Bar 不得形成三条等权横栏。

## 4. Workspace Grid

| Region | Default | Range | 行为 |
| --- | ---: | ---: | --- |
| Navigation expanded | 176 | 168–208 | 可收起至 56 |
| Canvas | `*` | min 640 | 永远获得剩余空间 |
| Inspector | 320 | 280–360 | 无选择收起，可拖调 |
| Top Bar | 48 | 44–56 | 最多一行主控件 |
| Status Bar | 28 | 24–32 | 无必要状态时隐藏 |

Inspector 宽度不得超过 Workspace 的 28%；Navigation + Inspector 合计不得超过 Workspace 的 38%。若冲突，先收起 Inspector，再收起 Navigation。

## 5. Margin、Padding、Gap

| 场景 | Default | Compact | Display |
| --- | ---: | ---: | ---: |
| 页面外边距 | 24 | 16 | 32 |
| 稳定 Panel 内边距 | 16 | 12 | 24 |
| Gallery 图片间距 | 12 | 8 | 16 |
| Card 内边距 | 16 | 12 | 16 |
| Card 间距 | 12 | 8 | 16 |
| 同组控件 | 8 | 4 | 8 |
| 控件组之间 | 16 | 12 | 24 |
| Section 之间 | 24 | 16 | 32 |

Compact 是预定义密度，不是逐处减小 1–2 DIP。图片页面优先减少 Chrome，再减少 Gap；不得先缩小缩略图和文字。

## 6. 分辨率策略

### 1080p

在 1920 × 1080、100% 下，以 Gallery / Viewer 获得内容宽度 65% 以上为门槛。允许一侧栏展开；另一侧栏按上下文显示。工作台最多两列主体，不做四列统计卡。

### 2K

在 2560 × 1440 下增加图片数量或图片尺寸，而不是把所有面板同时展开。Gallery 目标宽度 70% 以上；外边距可到 32。

### 4K

4K 用于更清晰、更大的图片与更多呼吸空间，不用于缩小 UI 以显示更多控制器。Viewer 可使用接近全屏；大屏内容不无限拉宽，文字与表单保持舒适行宽。

## 7. Windows DPI

- 125%、150%、200% 都以 DIP 和布局约束工作，不建立按倍率复制的页面；
- 图标使用矢量或匹配 DPI 的位图；1 DIP 线必须 Pixel Snap；
- 200% 时优先折叠 Navigation/Inspector、让工具栏溢出到菜单、允许 Dialog 滚动；
- 禁止固定像素截图、绝对坐标排版和以 ScaleTransform 整页缩放；
- 所有关键控件目标高度至少 36 DIP，频繁点击目标建议 40 DIP；
- 长中文、系统大字号与 200% DPI 必须同时测试。

## 8. Canvas 规则

- 每页只有一个主要滚动区；Navigation 和 Inspector 可独立滚动，但不得劫持 Canvas 滚轮；
- Gallery 保持图片比例，允许 Grid / Masonry / Justified，但选择与滚动锚点一致；
- Viewer 使用 Fit、100%、Fill 三种明确模式，Fill 不作为默认；
- Moodboard 画布提供平移/缩放、可视范围导航与安全工作区；
- 图片 Loading 保留预期比例，避免布局跳动。

## 9. 响应式降级顺序

空间不足时按此顺序处理：

1. 收起空 Inspector；
2. 将 Inspector 变为临时 Drawer；
3. 收起 Navigation 到图标模式；
4. 将低频工具移入 Overflow / Context Menu；
5. 工具栏分组换行或缩短标签；
6. 降低 Gallery Gap 到 8；
7. 最后才减少同屏图片数量。

绝不降低文字可读性、裁切关键控件或把图片压成装饰缩略图。
