# Pixel Tart Developer Preview RC7：Real-machine Blocking UI Fix

日期：2026-09-11
分支：`integration/pixel-tart-developer-preview`

## RC6 为什么失败

RC6 实机验收失败，用户截图是最终证据：DatePicker 弹层仍发生月份/星期/日期网格错位；“新建素材库”要求用户在 Windows 保存对话框里理解空文件名，点击保存没有明确创建结果；摄影收支一级页面仍显示并覆盖工具栏的页面级关闭 X。

自动化没有发现问题的原因是：RC6 主要验证 XAML 属性、尺寸字符串、路由和组件源码，没有创建真实 Popup Visual Tree，也没有读取 `ActualWidth` / `ActualHeight`、`TransformToAncestor` 或边界相交关系。素材库测试覆盖容器格式和服务层，没有走 SaveFileDialog 的真实用户流程。关闭按钮测试覆盖共享 Header，却遗漏了 MainWindow 顶层浮动 X，因此测试 PASS 与实机表现不一致。

## DatePicker 真正根因

RC6 自定义了 `CalendarItem`、`PART_MonthView` 与日期按钮模板；这些布局骨架与 WPF 原生 Calendar 的 ItemsPresenter/MonthView 约束不一致，在真实 DPI 和应用全局 Button 样式继承下产生巨大空白、漂移和重叠。

RC7 删除自定义 DatePicker/Calendar/CalendarItem 布局模板，恢复 WPF 原生 Calendar 骨架。Pixel Tart 仅通过 `CalendarStyle` 的资源覆盖保留文字颜色、选中/非当前月状态；Calendar 内部提供空的本地 Button 隐式样式，阻断全局 Button `MinHeight`/Padding 污染。

真实 RenderTarget/Visual Tree 回归覆盖 2026-02、2026-09、2026-12，并检查 42 个 `CalendarDayButton` 的 7 列、6 行、Actual bounds 与 pairwise intersection。RC7 通过，无重叠、无裁切、无异常空白。9 月另覆盖 125%/150%/200% 逻辑缩放渲染。

## 是否恢复原生 Calendar Template

是。Calendar/CalendarItem/MonthView/ItemsPresenter/Grid/CalendarDayButton 的布局结构全部交还 WPF 原生模板，仅保留 Pixel Tart 颜色和交互状态换肤。

## 素材库创建流程旧问题

`.ptlibrary` 不是单个文件，而是目录型 Library Package：根目录包含 `library.manifest.json`、`database/asset-library-v16.db`、`assets/managed`、`previews` 等固定物理目录。旧流程用 `SaveFileDialog` 模拟文件保存，用户选择目录后文件名为空，导致创建无结果或静默失败。

## 新素材库创建 UX

`NewAssetLibraryDialog` 提供“素材库名称 + 保存位置 + 最终位置预览 + 创建素材库”。名称默认“我的素材库”，位置使用 Folder Picker；创建前校验空名称、非法文件名、目录不存在和同名库。创建成功后立即创建、打开、记录 Recent Libraries、更新左栏根目录/空状态和状态栏；失败显示真实错误对话框并保留当前库。

## Close X 全局审计

Workspace：0 处页面级 Close X。工作台、素材库、归片工作区、工作日历、联机拍摄、摄影收支、项目历史、工具箱均通过左侧导航切换。

Dialog：7 处共享 `SurfaceCloseButton`。

Overlay：教程、任务详情、预约详情和创建抽屉使用各自单一关闭出口。

Window：消息对话框使用 Window 自有关闭区。

所有标题区采用 Grid：标题列 `*` + 关闭列 `Auto`；关闭按钮独占至少 40×40 DIP，不再通过 Margin/Canvas 规避碰撞。摄影收支右上仅保留“＋收入 / ＋支出 / 导出 CSV”工具栏，无页面级 X。

## Visual Regression Results

`RealLayoutRegressionTests.ProductionControls_ReportNonIntersectingActualBounds`：PASS。

验证内容：Finance 工具栏按钮边界互不相交；Dialog 标题与 Close rect 不相交；素材库名称/位置/创建按钮均可操作；2026-02/09/12 日历 42 单元格连续排列且无相交；9 月 125/150/200% Render 通过。

定向 RC7 套件：8/8 PASS。Release solution build：0 warning / 0 error。素材库容器/包定向测试：11/11 PASS。正式发布 EXE 启动探针成功。

全量历史套件不是 RC7 发布门禁：它依赖未复制到当前 worktree 的 2.0.4 截图证据、限制在原素材库特性分支运行的验收 fixture，并包含与本轮无关的既有素材库 XAML/P3 契约与性能用例。本次未把这些失败伪写成 RC7 通过。

证据目录：`artifacts/ui-review/2.3.0-rc7/`，包含：

- `01_calendar_closed.png`
- `02_calendar_open.png`
- `03_calendar_150.png`
- `04_asset_library_new.png`
- `05_asset_library_created.png`
- `06_finance_toolbar.png`
- `07_dialog_close.png`
- `datepicker_feb_100.png`
- `datepicker_sep_100.png`
- `datepicker_sep_125.png`
- `datepicker_sep_150.png`
- `datepicker_sep_200.png`
- `datepicker_dec_100.png`

## RC7 Installer

Filename: `像素蛋挞_Setup_2.3.0_RC7_x64.exe`

Version: `2.3.0_RC7`（程序集版本保持 `2.3.0`）

SHA-256：B7A5F0054F8E83D91F5DEA06F172DFF395E118CB41AB32E9CB1412B15975332B

## 尚待用户真实机验证

仅剩实体显示器在最终 DPI 设置下的主观视觉体验；已知布局错误不再列为“待验证”。

