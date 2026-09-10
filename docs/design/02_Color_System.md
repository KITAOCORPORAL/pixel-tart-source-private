# Pixel Tart Color System

状态：Design Constitution v1.0  
目标：建立不影响照片判断、只有一个品牌强调色、可跨 Dark / Light / High Contrast 使用的语义色系统。

---

## 01 原则

1. 颜色首先区分层级与状态，不装饰空白。
2. 照片周围使用中性、低色偏背景；任何高饱和色不得大面积邻接图片。
3. 全产品只有一个交互品牌强调色：`Accent.Primary`。
4. Logo 内的暖金等品牌图形颜色不自动成为 UI 操作色。
5. Success、Warning、Error、Info 只表达状态，不表达模块归属。
6. 颜色永远不是状态的唯一通道；必须同时使用文字、图标、形状或位置。
7. 新页面只引用语义 token，不直接引用十六进制值或“Blue500”一类原始色阶。

---

## 02 Dark 主题基准

Dark 是 Pixel Tart 默认摄影工作环境。

### 2.1 Background / Surface

| Token | 值 | 用途 |
| --- | --- | --- |
| `Color.Background.App` | `#0D0F12` | 应用最底层、窗口壳层 |
| `Color.Background.Panel` | `#121519` | 主工作区与稳定面板 |
| `Color.Background.Elevated` | `#222830` | Menu、Dialog、Drawer、Tooltip |
| `Color.Background.Hover` | `#1D2228` | 安静 Hover、轻量 Selected 背景 |
| `Color.Background.Input` | `#171B20` | 输入、筛选和局部控制器 |
| `Color.Background.Preview` | `#08090B` | 沉浸预览与客户监看，保持中性 |
| `Color.Background.Overlay` | `#B8000000` | Modal / Preview 遮罩，约 72% 黑 |

Surface 不是 Card 层级计数器。相邻区域若可通过留白和对齐区分，不再增加背景层。

### 2.2 Text

| Token | 值 | 用途 |
| --- | --- | --- |
| `Color.Text.Primary` | `#F2F4F6` | 标题、正文、关键值 |
| `Color.Text.Secondary` | `#B2B8C0` | 辅助说明、标签值 |
| `Color.Text.Muted` | `#747C86` | Metadata、时间、非关键提示 |
| `Color.Text.Disabled` | `#555C65` | 不可操作文字；同时解释原因 |
| `Color.Text.OnAccent` | `#FFFFFF` | 强调色实底上的文字 |
| `Color.Text.OnImage` | `#FFFFFF` | 仅配合渐隐遮罩出现在图片上 |

正文与背景目标对比度不低于 4.5:1；大字或粗体不低于 3:1。`Muted` 不用于关键错误、文件影响或主操作说明。

### 2.3 Border / Divider / Focus

| Token | 值 | 用途 |
| --- | --- | --- |
| `Color.Border.Subtle` | `#292F37` | 安静分隔、输入默认边界 |
| `Color.Border.Strong` | `#3A424C` | 明确边界、拖拽目标 |
| `Color.Border.Selected` | `#18A88C` | 选中对象细边框 |
| `Color.Focus` | `#38C6AA` | 键盘焦点，必须清晰可见 |

边框默认 1 DIP；焦点允许 2 DIP。禁止用多层描边、发光或彩色阴影制造焦点。

---

## 03 Accent

| Token | 值 | 用途 |
| --- | --- | --- |
| `Color.Accent.Primary` | `#18A88C` | 当前视域唯一 Primary、选择与焦点 |
| `Color.Accent.Hover` | `#20B89B` | Primary Hover |
| `Color.Accent.Pressed` | `#128671` | Primary Pressed |
| `Color.Accent.Soft` | `#1F18A88C` | 约 12% 强调色背景，仅用于小面积选择 |

### 3.1 使用预算

- 一个 Page、Drawer 或 Dialog 的可见区域最多一个实底 Primary。
- Accent 实底面积原则上不超过当前视域的 3%。
- Accent 可用于焦点、选中、进度和主链接，但不得同时在所有元素上出现。
- 当前设置中的多强调色预设属于迁移对象；新设计不得依据用户选择改变业务状态色或图片周边背景。
- 品牌 Logo 可以保留自身图形色，但 Logo 色不能被复用为第二套按钮或选中色。

---

## 04 Status Colors

| Token | 值 | 语义 | 推荐表达 |
| --- | --- | --- | --- |
| `Color.Status.Success` | `#42B883` | 已完成、同步成功、安全保存 | 图标 + 文本、Toast |
| `Color.Status.Warning` | `#E0AD43` | 需注意、部分完成、冲突 | 图标 + 文本、细边框 |
| `Color.Status.Error` | `#D85A5A` | 失败、不可恢复、危险影响 | 原因 + 影响 + 解决方式 |
| `Color.Status.Info` | `#4E8FD4` | 中性系统信息 | 小图标、短说明 |

每种状态可有 `Soft` 版本，透明度建议 10%—14%，只用于局部提示背景。禁止：

- 整张图片蒙上状态色；
- 整个项目卡用红、绿、蓝填满；
- 把 Warning 当品牌金使用；
- 用 Success 表示“当前选中”；
- 为每种任务类型发明一种颜色。

图片状态优先放在缩略图外的 Metadata 行；必须叠加时，使用小型中性底图标并保留非颜色通道。

---

## 05 日历与业务状态

日历需要区分真实业务阶段，可保留集中管理的五色映射：

| Token | 值 | 含义 |
| --- | --- | --- |
| `Color.Calendar.Free` | `#59616B` | 空闲 |
| `Color.Calendar.Scheduled` | `#E05252` | 有拍摄 |
| `Color.Calendar.Shot` | `#3DB879` | 已拍摄 |
| `Color.Calendar.PendingReturn` | `#DDAF32` | 待返图 |
| `Color.Calendar.Returned` | `#3E8ED0` | 已返图 |

约束：

- 五色仅在日历与拍摄阶段语境出现，不扩散为通用按钮色。
- 日期格仍保持中性背景；状态以日期标记、短标签或窄条表达。
- 今天、选中、键盘焦点、关闭档期与业务状态是五个独立维度。
- 关闭档期使用锁图标、降亮和辅助文字，不增加第六个业务色。
- 月、周、日、迷你日历必须共用同一状态解析器。

---

## 06 图片观看保护

- Gallery 背景在 `Background.App` 与 `Background.Panel` 中选择，不使用偏蓝或偏绿表面。
- 图片之间不放高饱和分隔线。
- 选中缩略图使用 2 DIP Accent 外框，外框在图片边界外绘制，不覆盖像素。
- Hover 只提升边框或 Metadata 对比度，不给图片加彩色蒙版。
- 评分、Pin、缺失、离线等标记总覆盖面积不超过缩略图面积的 8%。
- Histogram、色板和色彩分析属于数据本身，可以显示真实色彩；其容器和标签仍保持中性。
- Preview 默认 `#08090B`，不受用户 Accent 或系统强调色影响。

---

## 07 Light 与 High Contrast

Light 主题必须一一映射相同语义，不改变结构和操作层级。建议基础值：

| Token | Light 值 |
| --- | --- |
| `Background.App` | `#F2F2F0` |
| `Background.Panel` | `#FAFAF8` |
| `Background.Elevated` | `#FFFFFF` |
| `Background.Hover` | `#E9EAE7` |
| `Text.Primary` | `#17191C` |
| `Text.Secondary` | `#4D535A` |
| `Text.Muted` | `#747A81` |
| `Border.Subtle` | `#D7D9D5` |
| `Border.Strong` | `#AEB3B8` |

High Contrast 使用 Windows 系统颜色，不固定十六进制值。焦点、今天、选中、关闭档期和业务状态必须在不依赖细微色差时仍可辨认。

---

## 08 禁止模式

- 页面、模块或任务各自定义“专属主色”；
- 强烈渐变、霓虹、玻璃高光、发光边框；
- 白色输入框直接泄漏进 Dark 主题；
- 用透明度低到不可读的灰色表达禁用；
- 依靠红绿差异作为唯一状态；
- 在照片上用大面积彩色 Badge、Ribbon 或遮罩；
- 将品牌色、状态色、日历色混为一套可自由选择的 Accent；
- 页面内硬编码颜色值。

---

## 09 实现契约

- XAML 使用 `DynamicResource` 语义 Brush；不得从页面直接引用 `Color` 原值。
- Token 名按 `Color.Role.State` 命名，不按页面或色相命名。
- 所有主题必须提供完全相同的 token 集。
- 新增 token 必须附用途、禁用场景、Dark / Light / High Contrast 映射和对比度验证。
- 旧的 `BrandSoftBrush`、页面专用颜色和多 Accent 预设在迁移期可作为别名，但不得被新组件引用。

