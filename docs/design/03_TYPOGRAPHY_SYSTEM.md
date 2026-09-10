# Pixel Tart Typography System v1.0

## 1. 字体方向

字体应像安静、准确的画册说明与工作室标记，可长时间观看。禁止科技感字体、游戏字体、装饰性无衬线字体和为了“AI 感”使用的宽体/等宽字体。

中文首选 `Microsoft YaHei UI`；拉丁与数字首选 `Segoe UI Variable`，回退 `Segoe UI`。路径、EXIF 和时间码可使用系统等宽字体，但只能作为数据局部，不成为界面气质。

## 2. 六级文字角色

所有页面只使用以下六个角色：

| Role / Token | Size | Line Height | Weight | 典型用途 |
| --- | ---: | ---: | --- | --- |
| `Type.Display` | 32 DIP | 40 | SemiBold | 空状态核心命题、沉浸式项目名；低频 |
| `Type.PageTitle` | 24 DIP | 32 | SemiBold | 页面唯一 H1 |
| `Type.SectionTitle` | 16 DIP | 24 | SemiBold | 页面主要章节 H2 |
| `Type.Body` | 14 DIP | 22 | Regular | 正文、表单值、按钮文字 |
| `Type.Metadata` | 12 DIP | 18 | Regular / Medium | 日期、镜头、尺寸、路径摘要 |
| `Type.Caption` | 12 DIP | 16 | Regular | 图注、辅助说明、快捷键提示 |

Display 不用于 Dashboard 数字；Page Title 每页只能有一个；Section Title 不得通过任意放大 Body 临时制造。Metadata 与 Caption 同字号但通过行高、颜色和用途区分：Metadata 是作品事实，Caption 是界面解释。

## 3. 字色

- Display、Page Title：Primary Text；
- Section Title、Body：Primary Text；
- Metadata：Secondary Text，关键值可为 Primary；
- Caption：Muted Text；
- Disabled Text 只表达不可操作，不承载原因；原因使用 Secondary Text；
- Error / Warning 文本只有关键词或图标使用状态色，长段文字仍用 Primary / Secondary。

## 4. 排版规则

- 中文正文每行建议 28–42 个汉字；长文最大宽度 680 DIP；
- 标题最多两行，工具栏标题必须单行省略并提供完整 Tooltip；
- 正文左对齐；数值表格可右对齐；不得居中排长文；
- 中文与英文、数字之间由文案层维护合理空格；
- 使用真正的省略号“…”；按钮不使用句号；
- 避免全大写英文；必要缩写（RAW、EXIF、DPI）例外；
- 作品名和用户标签保持原始大小写，不由界面强制转换；
- 日期、时间、文件大小与分辨率全产品使用同一格式。

## 5. 数字与摄影 Metadata

- 评分使用 `0–5`，不混用五角星文本与数字说明；
- 快门、光圈、ISO、焦距保留摄影惯例，例如 `1/250 s · f/2.8 · ISO 400 · 85 mm`；
- 像素尺寸写作 `6000 × 4000 px`，文件大小按本地化格式显示；
- 路径默认中段省略，Hover/Focus 或详情中提供完整值；
- 表格数字启用等宽数字特性（tabular numerals），不强制整段等宽字体。

## 6. DPI 与可访问性

字号以 DIP 定义，由 Windows DPI 自动缩放。125%、150%、200% 下不得通过硬编码像素抵消系统缩放；不得因空间不足把文字降到 12 DIP 以下。200% 时允许工具栏换行、侧栏折叠和 Dialog 纵向滚动，不允许裁字。

用户开启大字号时，Body 与 Caption 应增大但层级关系保持。所有按钮与输入允许两行或内容驱动宽度；Focus 后不得因字重变化产生布局跳动。

## 7. 文案语气

使用摄影师能直接行动的词：`查看原片`、`加入初选`、`打开项目`。避免企业后台话术：`数据看板`、`资源管理`、`流程赋能`。避免人格化 AI 话术和夸张成功文案。

错误文案依次说明：发生了什么、源文件是否安全、用户可以做什么。空状态说明一个下一步，不罗列全部能力。

## 8. 禁止

- 页面自行声明 9、10、11、13、15、18、20、22 等字号；
- 用颜色、粗体、全大写和字号同时强调同一句；
- 将 Placeholder 当 Label；
- 在照片上叠加长标题或多行 Metadata；
- 用图标字体代替语义图标资源；
- 为营销氛围使用手写体或复古打字机体；
- 用极细字重营造“高级感”。
