# Pixel Tart Component System v1.0
新增组件前必须确认现有组件不能通过属性或组合完成，并同时提交：用途边界、全部状态、键盘行为、屏幕阅读名称、Dark/Light/High Contrast、100%–200% DPI、Loading/Empty/Error 样例。未完成契约不得进入公共资源字典。
## 1. 通用契约

所有组件必须定义 Default、Hover、Active/Pressed、Focus、Disabled、Loading；选择型组件还需 Selected，异步组件还需 Empty、Error。状态变化不改变组件外部尺寸。组件使用语义 Token，不包含页面名与局部 Hex。

通用尺寸：Compact 32 DIP、Default 38 DIP、Large 44 DIP；图标 16/20/24 DIP；Corner Radius 6 DIP，Card 8 DIP；Focus ring 2 DIP。高频指针目标建议不小于 40 × 40 DIP。

## 2. Button

### Primary

- 用途：当前视域唯一主操作，例如“导入照片”“保存策划”；
- 尺寸：高 38，水平 Padding 16，图标与文字 Gap 8；
- Hover：`Accent.Hover`；Active：`Accent.Active`；
- Disabled：中性表面与 Disabled Text，不使用透明到不可读；
- Loading：保留原宽，显示 16 DIP Progress + 动词进行时，阻止重复提交。

### Secondary

- 用途：与主操作并列但非首要；
- 外观：Surface + Border，Hover 提升表面或边界；
- 同一组不超过两个；更多操作进入菜单。

### Ghost

- 用途：工具栏、关闭、返回、轻量上下文动作；
- 默认透明，Hover 显示 Surface.Hover；
- 图标按钮必须有 Tooltip 与可访问名称。

### Danger

- 用途：确实会删除、断开或不可逆覆盖的最终动作；
- 默认可为 Secondary 风格，仅在确认 Dialog 的最终按钮使用 Error 实心色；
- 不以红色装饰“移出集合”等可撤销操作。

## 3. Input

- 默认高 38，最小宽 160，Padding 12；Label 永远在输入框外；
- Placeholder 仅给格式示例，不承担字段名；
- Hover 增强边界；Focus 使用 Accent ring；Active 为编辑光标状态；
- Disabled 保留当前值可读性；Read-only 与 Disabled 外观不同；
- Loading 时保留值并显示尾部 Progress；Error 在字段下方给出原因与修复，不只红边；
- 多行输入必须有合理最小高和字数/保存状态，不在长文本中套固定高滚动框。

## 4. Dropdown

- 高 38；菜单项高 32–40；当前选择清楚可读；
- Hover/Active 使用中性表面，Selected 使用 Accent 细标记；
- Disabled 说明原因；Loading 不打开空菜单；
- 超过约 12 项时支持搜索；复杂层级改用 Picker，不做三级级联菜单；
- Esc 关闭、方向键导航、Enter 选择，关闭后焦点回到触发器。

## 5. Context Menu

- 用途：当前对象的低频和高效动作，不是功能垃圾桶；
- 宽 200–280，项高 32，Padding 8，分组间用 1 DIP 分隔与 4 DIP 空间；
- Hover 仅高亮一项；Active 给即时反馈；Disabled 项仅在解释上下文时短暂保留；
- Loading 动作在原项显示进度并关闭或锁定菜单，结果由 Toast/任务中心承接；
- 详细分组与排序见 `08_CONTEXT_MENU_SYSTEM.md`。

## 6. Dialog

- 用途：需要完成后才能回到工作区的短任务、风险确认或关键选择；
- 尺寸：Small 420、Default 560、Wide 760 DIP；最大高为可用窗口 80%；
- 标题、简短说明、内容、Footer 四区；一个 Primary，取消在其左侧；
- Hover/Active 由内部组件承担；背景遮罩只降低干扰，不纯黑遮死；
- Loading 阻止重复提交但保持取消策略清楚；Error 留在 Dialog 内并保持用户输入；
- Esc 默认取消，危险进行中若不能取消必须提前说明。

## 7. Toast

- 用途：非阻塞、短时、操作结果；不承载需要用户决策的错误；
- 位置：工作区右下，距边 24；宽 320–420；最多堆叠 3 条；
- Info/Success 4 秒，Warning 6 秒，Error 保留到关闭或进入详情；
- Hover 暂停计时；Active 仅用于“撤销/查看”；
- Loading 不使用 Toast 模拟持续进度，交给任务中心；
- 不连续弹出每个文件的成功提示，批处理只汇总一次。

## 8. Card

- 用途：可独立选择、移动或进入的对象，例如项目、Booking、Shot；
- Card 不是所有布局的默认容器；静态段落优先使用留白和 Section；
- Hover 轻微提升表面/边界，不上浮缩放；Active/Selected 用外轮廓；
- Disabled Card 极少使用，通常隐藏不可用入口；
- Loading 保留封面比例和文字行；Empty 不渲染空 Card；
- 禁止 Card 套 Card、统计卡墙、每张 Card 永久一排按钮。

## 9. Thumbnail

- 用途：观看、比较和选择照片；是核心组件，不等于 Card；
- 尺寸：由 Gallery 密度决定，最短边建议不低于 120 DIP；保持原始比例；
- 默认只显示图片；Hover 可显示 1–3 个当前任务操作；
- Selected 在图片外绘制 2 DIP Accent ring；Active 不缩放图片；
- Disabled 用于离线/缺失并同时显示符号和原因，不降低到无法辨图；
- Loading 使用固定比例中性占位与渐进解码；Error 显示重试/重新定位；
- Metadata 默认最多一行作品名 + 一行任务相关事实，完整信息进 Inspector。

## 10. Image Viewer

- 用途：沉浸观看、像素检查、前后比较；独立于 Inspector；
- 图片至少占 Viewer 可用面积 80%，Chrome 可自动淡出；
- 支持 Fit、100%、受控放大、平移、前后张、Esc 返回；
- Hover 只唤醒必要工具；Active 拖动画布；Focus 顺序不覆盖图片；
- Loading 先展示可用 Preview 再渐进到高质量图，禁止空白闪烁；
- Error 明确是解码失败、文件缺失还是权限问题，并说明源文件未被更改；
- Compare 使用中性分隔线，同步缩放可切换，绝不自动裁成统一比例。

## 11. Menu、Panel、Inspector、Navigation

- Menu：只放跨对象命令与低频全局能力；层级不超过两层；
- Panel：稳定工作区域，不自动拥有边框、标题背景和 Card 外观；
- Inspector：只描述当前选择，分组可折叠；无选择即收起；
- Navigation：只承载“去哪里”，不承载对象操作、统计和营销入口；
- 各组件均遵守通用状态、键盘、Focus、DPI 和 Token 契约。

## 12. 组件准入

新增组件前必须确认现有组件不能通过属性或组合完成，并同时提交：用途边界、全部状态、键盘行为、屏幕阅读名称、Dark/Light/High Contrast、100%–200% DPI、Loading/Empty/Error 样例。未完成契约不得进入公共资源字典。
