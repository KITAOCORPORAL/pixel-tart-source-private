# RC6 Real-machine Failure Analysis

日期：2026-09-11

RC6 的自动化通过不构成视觉验收通过。用户真实 Windows 安装环境已确认：DatePicker 仍严重损坏、新建素材库不可用、一级页面 Close X 与工具栏碰撞。以下结论以实机反馈为准。

## 自动化为什么没有发现问题

RC6 新增测试主要读取 XAML 文本，确认 `MinWidth`、`MinHeight`、路由参数与 Grid 列声明存在。它没有实例化 DatePicker Popup 的完整 WPF Visual Tree，没有读取 `ActualWidth` / `ActualHeight`，也没有用 `TransformToAncestor` 比较真实边界。因此，42 个日期项即使被创建在错误的位置，测试仍会通过。

## 只验证属性、没有验证真实布局的测试

- `Version230Rc6RegressionContractTests.Calendar_UsesIndependentSemanticDimensions`：只搜索尺寸字符串，没有渲染 CalendarItem。
- `Version230Rc6RegressionContractTests.SurfaceHeader_SeparatesTitleAndCloseColumns`：只验证共享组件源码；没有证明 Finance 等页面实际使用该组件，也没有覆盖 MainWindow 顶层浮动 X。
- `NavigationWorkbenchClosureTests.ToolboxSidebar_IsFirstClassWorkspaceNavigation`：路由契约有效，但与本轮三个实机视觉/创建阻塞无关。

## 哪些页面没有真正使用共享组件

摄影收支一级页面的碰撞 X 不是 `FinanceView` 的页面 Header，而是 `MainWindow` 在所有非 Workbench 页面上方绘制的 `ShellSurfaceCloseButton`。因此修改 `SurfaceHeader` 无法改变 Finance 工具栏上方的 X。若编辑器抽屉打开，其内部另有正确语义的 `SurfaceCloseButton`。

Asset Library 的新建流程也没有共享应用内 Dialog；它直接调用 Windows `SaveFileDialog`，把目录型 `.ptlibrary` 伪装为文件保存流程。

## 为什么 PASS 与实机表现不一致

测试验证的是静态契约，不是真实像素布局；RC6 同时把 CalendarItem 骨架继续自定义并加大固定高度，使错误的 PART 布局在测试中看似“有尺寸”，在实机却形成大面积空白和漂移。Close 测试覆盖了局部共享 Header，却没有覆盖 MainWindow 的全局叠加层。素材库测试覆盖底层容器格式，却没有走用户实际的 SaveFileDialog 空文件名交互。
