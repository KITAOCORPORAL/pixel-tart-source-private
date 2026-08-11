# Pixel Tart Visual Design System v1 实施报告

## 结论

- 视觉方向：方案 A，Darkroom Studio，A-v2。
- 状态：`CodeVerified=true`，`AutomatedVerified=true`，`InstalledUiVerified=部分通过`，`UserVerified=false`。
- 本报告中的视觉评分仅为 Codex 预检查，不替代用户验收。

## 正式规范

- 规范目录：`docs/design/`。
- 正式文件：
  - `PixelTart_Visual_Design_System_v1.md`
  - `PixelTart_UX_Container_Rules_v1.md`
  - `PixelTart_Component_Catalog_v1.md`
  - `PixelTart_Visual_AntiPatterns_v1.md`
  - `PixelTart_Accessibility_Rules_v1.md`
- 页面母版：
  - `docs/design/reference/Workbench_Av2_Master.md`
  - `docs/design/reference/FullCalendar_Av2_Master.md`
  - `docs/design/reference/ToolModal_Av2_Master.md`
  - `docs/design/reference/OnlineSelectionHome_Av2_Master.md`
  - `docs/design/reference/OnlineSelectionProject_Av2_Master.md`

## Token 落地

- 颜色：深色、浅色、高对比三套基础色板；主强调色为 `#18A88C`，暖金仅用于 Pin、摄影识别和重要提示。
- 状态色：Success、Warning、Danger、Info，以及工作日历固定五色 Free、Scheduled、Shot、PendingReturn、Returned。
- 字体：中文 `Microsoft YaHei UI`，英文和数字 `Segoe UI Variable`；正文最小字号 11 DIP。
- 间距：仅使用 4、8、12、16、24、32、48。
- 圆角：4、6、8、10、12，避免大面积移动端卡片感。
- 层级：App 背景、工作区、局部面板、浮层四层；优先用留白、亮度和排版建立层级。

## 组件体系

- 已落地资源字典：Buttons、Inputs、Cards、Navigation、Calendar、Modal、Drawer、Tooltip、ContextMenu、ScrollBars、EmptyState、Icons。
- `App.xaml` 按依赖顺序统一合并资源；运行时主题继续通过正式 Theme 字典切换。
- ComboBox 已修复为继承主题模板，RAW 与批量压缩 Modal 不再出现系统白色控件泄漏。
- 兼容旧资源键，但正式 A-v2 组件只从统一 Token 取值。

## 容器与交互

- Page：工作台、归片、工作日历、在线选片、联机、整理、拼图、收支等长期工作。
- Modal：RAW 转 JPG、批量压缩、快速新建等短任务。
- Drawer：快速编辑和上下文详情。
- 每个当前工作面最多一个 Primary Action；Secondary、Ghost、Danger 层级明确。
- Hover 只做边框、图标、轻阴影变化，不做大面积闪烁或跳动。

## 禁止项执行

- 组件字典禁止任意硬编码十六进制颜色，色值集中在 Colors、Theme 和兼容 Accent 入口。
- 未引入 Emoji、混合图标体系、超大圆角、彩色卡片墙或多 Primary Action。
- 侧栏移除重复“使用教程”“问题反馈”，功能并入帮助页。
- 页面保持摄影内容优先，未改造成 ERP、SaaS Dashboard 或运营后台。

## 视觉证据

- 证据目录：`artifacts/ui-review/product-redesign/`。
- 独立原尺寸 PNG：30 张。
- 自动布局检查：30/30 通过，阻断问题 0。
- 主题：深色、浅色、高对比均有证据。
- 窗口：1280×720、1280×768、1366×768、1440×900、1600×900、1920×1080、2560×1440。
- DPI：100%、125%、150%、175%、200%。
- 场景覆盖：工作台、完整日历、右键菜单、关闭档期、快速新建、快速编辑、完整策划、工具箱、Pin、RAW Modal、批量压缩 Modal、在线选片首页/项目/新建、EmptyState、联机工作区。

## Codex 视觉预评分

| 维度 | 得分 |
|---|---:|
| 信息层级 | 18/20 |
| 操作清晰 | 14/15 |
| 视觉节奏 | 14/15 |
| 留白 | 9/10 |
| 字体 | 9/10 |
| 控件密度 | 9/10 |
| 色彩 | 9/10 |
| 一致性 | 8/10 |
| 总分 | **90/100** |

## 已知限制

- 90/100 仅为实现侧预检查，最终视觉是否通过仍由用户决定。
- 物理显示器、真实字体渲染差异和用户长期工作习惯仍需用户在安装版中确认。
- 安装候选已在隐藏隔离桌面真实打开工作台、三种 Booking 形态、工具箱及四工具、收支、在线选片入口和联机页；日历右键浮层与在线项目四页签受隐藏桌面输入限制，未取得完整 InstalledUiVerified。
