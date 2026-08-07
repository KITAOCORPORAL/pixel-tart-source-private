# 像素蛋挞 2.3.0 RC5：日历与任务中心布局修复报告

## 1. 基线与 Git

- 开始 HEAD：`619986a4eb1da053aba75bc9ba0acd5490f2e87b`
- 修复分支：`fix/2.3.0-rc5-calendar-taskcenter-layout`
- 功能提交：
  - `7af014030f2936f4a6db9fc57036044ebcab2cc5` — `fix(calendar): render workflow state on day number badges`
  - `a53eb84dd07e32eb88b4777e9c9ac263f41d1c67` — `fix(calendar): improve calendar header spacing`
  - `ec2c9e8f85be458be9e9d6ee1d184f071a2e73e9` — `fix(workbench): expand task center and simplify scrolling`
  - `21bb24abc697bb9862b3e703886487cfe63c2d56` — `test(ui): cover calendar badges and task center layout`
- 合并提交：`f8436e21a897a42a20eda45e2a01132ef0d781bc` — `merge: integrate 2.3.0 RC5 calendar task center layout fix`
- 合并方式：正常非快进合并到 `release/2.3.0`。
- 合并后工作树：干净。

## 2. 版本与数据边界

- ProductVersion：`2.3.0`
- Assembly/FileVersion：`2.3.0.0`
- SchemaVersion：`4`（`BusinessSchemaMigration.Version` 未修改）
- 数据库：未新增表、未修改迁移、未修改业务字段或持久化语义。
- Provider：仍为 `None`；Release 未启用 Mock/Fake Camera；源码未引入 localhost。
- 未生成安装包，未执行 Publish，未合并 main，未创建 `v2.3.0` Tag，未进入 2.4.0。

## 3. 迷你日历日期数字 Badge

日期数字现在由独立的 `DayNumberBadge` 承载，最小尺寸为 `28×24`，状态背景直接应用于数字格，数字使用状态专用高对比前景色，不再被底部状态条或覆盖层遮挡。空闲状态仍为灰色，且保留清晰的日期数字。

五种业务状态保持不变：

| 状态 | 日期格颜色 |
| --- | --- |
| 空闲 | 灰色 |
| 有拍摄/待拍摄 | 红色 |
| 已拍摄 | 绿色 |
| 待返片 | 黄色 |
| 已返片 | 蓝色 |

今天使用独立的 Badge 描边通道，当前选择使用整格 Accent 描边；二者不会互相覆盖状态背景。多任务日期按“待拍摄 > 已拍摄 > 待返片 > 已返片”确定主状态，右上角显示数量 Badge，并在 Tooltip 中列出日期、任务数、标题和状态。

## 4. 完整日历

- 顶部工具栏拆为视图组、日期导航组、筛选搜索组。
- 年份与月份使用独立文本标签，月份为纯 `M月`，不再误显示为 `M月d日`。
- 年月之间使用稳定间距；周标题、图例、日期网格增加明确的垂直留白。
- 1280、1440、1600、1920 宽度均有响应式布局合同；低于紧凑阈值时筛选组换行，搜索框和日期导航按宽度收缩。

## 5. 工作台任务中心

- 右侧区域使用约 `52/48` 的日历/任务中心比例，任务中心最小高度 `320`，窗口宽度达到 1920 时使用 360 像素宽度，否则使用 320 像素。
- 任务中心页头、摘要、页脚固定，只有中间任务列表 `ScrollViewer` 滚动。
- 任务卡显示任务来源、进度、状态、更新时间，并保留当前步骤、当前文件和结果摘要。
- 无任务时显示“暂无处理任务”及任务历史入口。
- 1280×768、1280×720 等短窗口会压缩工作台概览与排期行高，避免最近项目空状态溢出；紧凑宽度下任务中心仍通过抽屉入口访问。

## 6. 测试与真实 WPF 证据

- 原基线：1493 项；新增 RC5 专项测试 32 项；最终总数：**1525 项**。
- Debug：Core 1024 + WPF 409 + DPI 92 = **1525/1525，通过 0，跳过 0**；构建 0 警告、0 错误。
- Release：Core 1024 + WPF 409 + DPI 92 = **1525/1525，通过 0，跳过 0**；构建 0 警告、0 错误。
- 真实 WPF UI Review：24/24 场景通过，包含五种状态、今天/选择、多任务、任务中心空/5/20/滚动、1280/1600/1920、深色/浅色/高对比、150%/200% DPI。
- UI 证据目录：`artifacts\ui-review\2.3.0-rc5-layout-fix\`
- UI 总览：`artifacts\ui-review\2.3.0-rc5-layout-fix\像素蛋挞_2.3.0_RC5日历与任务中心布局修复总览.png`

## 7. 结论

- 日期数字框状态表达：完成。
- 日期数字遮挡问题：已修复。
- Schema：未修改。
- main：未合并。
- Tag：未创建。
- 2.4.0：未进入。

