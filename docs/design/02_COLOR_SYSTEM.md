# Pixel Tart Color System v1.0

## 1. 目标

颜色系统为照片提供中性工作环境，并用最少颜色表达层级与状态。颜色 Token 描述语义，不描述某个页面或某个具体色值。Dark 是主要创作主题；Light 与 High Contrast 必须保持同样语义，不另造信息结构。

## 2. Core Tokens

### 2.1 Dark（默认）

| Token | Hex | 用途 |
| --- | --- | --- |
| `Color.Background` | `#0D0F12` | Window 与最外层背景 |
| `Color.Surface` | `#121519` | 标准内容表面 |
| `Color.Surface.Elevated` | `#1D2228` | Menu、Dialog、Popover、浮层 |
| `Color.Panel` | `#171B20` | Navigation、Inspector、稳定侧栏 |
| `Color.Border` | `#292F37` | 分隔线与静态边界 |
| `Color.Border.Strong` | `#3A424C` | Focus 外的强调边界、可拖分隔条 |
| `Color.Text.Primary` | `#F2F4F6` | 标题、正文、关键 Metadata |
| `Color.Text.Secondary` | `#B2B8C0` | 次级说明、标签 |
| `Color.Text.Muted` | `#747C86` | 低权重辅助信息 |
| `Color.Text.Disabled` | `#555C65` | 禁用文字；不得承担必要信息 |

### 2.2 Light（辅助）

| Token | Hex | 用途 |
| --- | --- | --- |
| `Color.Background` | `#F3F2EF` | Window 背景，轻微暖中性 |
| `Color.Surface` | `#FAF9F7` | 内容表面 |
| `Color.Surface.Elevated` | `#FFFFFF` | 浮层 |
| `Color.Panel` | `#ECEBE7` | 稳定侧栏 |
| `Color.Border` | `#D7D5CF` | 静态边界 |
| `Color.Border.Strong` | `#AAA7A0` | 强边界 |
| `Color.Text.Primary` | `#202226` | 主文字 |
| `Color.Text.Secondary` | `#565B62` | 次级文字 |
| `Color.Text.Muted` | `#777C83` | 辅助文字 |
| `Color.Text.Disabled` | `#A0A3A7` | 禁用文字 |

Light 不用于色彩关键判断的默认工作区；进入 Viewer、调色与联机监看时建议回到 Dark 或由用户选择。

## 3. 唯一品牌强调色

Pixel Tart 只允许一个品牌强调色：`Tart Teal`。

| Token | Dark | Light | 用途 |
| --- | --- | --- | --- |
| `Color.Accent` | `#18A88C` | `#087D6A` | Primary、选中指示、Focus |
| `Color.Accent.Hover` | `#20B89B` | `#0A8C76` | Hover |
| `Color.Accent.Active` | `#128671` | `#066957` | Pressed / Active |
| `Color.Accent.Subtle` | `#1F18A88C` | `#181087D6` | 轻量选中背景 |
| `Color.OnAccent` | `#07100E` | `#FFFFFF` | 强调色上的文字/图标 |

规则：

- 一个页面或浮层同一时刻最多一个实心 Accent 主按钮；
- Accent 仅表示可行动焦点、选择或品牌连续性，不表示“所有重要内容”；
- Logo、摄影金、图片主色不得自动成为 UI 强调色；
- 用户项目色、标签色仅属于内容数据，不能改变组件主题；
- Accent 面积建议不超过当前视域非照片面积的 5%。

## 4. Status Tokens

| Token | Hex | 语义 |
| --- | --- | --- |
| `Color.Status.Success` | `#42B883` | 已完成、连接正常、安全写入 |
| `Color.Status.Warning` | `#E0AD43` | 需要注意、部分完成、容量临界 |
| `Color.Status.Error` | `#D85A5A` | 失败、断连、破坏性操作 |
| `Color.Status.Info` | `#4E8FD4` | 中性系统信息、同步提示 |

状态色使用顺序：图标或 2–4 DIP 标记 → 短文字 → 细边框 → 低透明背景。必须同时提供文字或图标，不得只靠颜色。Status 不得替代 Accent，也不得给普通按钮着色。

## 5. Interaction Derivation

- Hover：表面亮度变化 4%–6%，或边框从 Border 到 Border.Strong；不得同时改变三个属性。
- Active：比 Hover 再暗 4%–6%，可有 1 DIP 内压视觉，但不缩放控件。
- Selected：Accent 细线/Focus ring + `Accent.Subtle`；照片本身不染色。
- Focus：2 DIP Accent 外轮廓，轮廓与控件至少间隔 1 DIP，High Contrast 使用系统高亮色。
- Disabled：降低文字与图标对比；不可用控件若长期存在，应移除而非灰置。
- Loading：保持布局和原背景，用中性 Skeleton；不闪烁彩色骨架。

## 6. 图片周围的颜色

- Viewer 背景使用 `#08090B` 或 `Color.Background`，不得出现偏蓝紫环境光；
- Thumbnail 底色使用 Surface，未加载区域不得用 Accent；
- 图片边框默认透明；选中时只在图片外绘制，不覆盖像素；
- 评分、Pick、客户选择与交付状态采用小型非侵入标记，并提供非颜色符号；
- Histogram、色板与照片内容色属于数据可视化例外，但其容器仍使用中性 Token。

## 7. 禁止

- 大面积渐变；
- 蓝紫 AI 风或“智能”专属霓虹色；
- 游戏化发光、呼吸光、彩色阴影；
- 彩色按钮堆积；
- 页面级 Hex、RGB 或命名色；
- 以透明度层层叠加制造不可预测颜色；
- 将 Success 绿色用于普通确认按钮；
- 从当前照片自动取色后改变整个 UI；
- 为每个模块定义一套 Accent。

## 8. 对比度与检验

正文与背景目标对比度至少 4.5:1；大字与必要图标至少 3:1；Focus、选中边界和重要图形至少 3:1。Muted 不用于路径风险、错误原因、确认影响或唯一标签。High Contrast 主题优先使用系统色并移除仅靠阴影表达的层级。

任何新增颜色必须先证明现有语义 Token 无法表达；批准后同时给出 Dark、Light、High Contrast 映射与使用禁区。
