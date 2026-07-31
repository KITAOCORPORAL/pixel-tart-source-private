# 北尾鸲归片助手 1.5.1 菜单深色主题与表格横向滚动修复报告

## 1. 完成结论

已完成 1.5.1 界面可用性修复。本次仅修改菜单/弹出菜单主题、滚动条主题、结果表滚动配置、版本与测试，不改动编号解析、索引、匹配、冲突处理、复制、报告、授权、新手教程或其他归片业务逻辑。

工作区中唯一的主工程基线实际标记为 1.4.4，未发现任何 1.5.0 工程副本或版本标记。为避免对不存在的代码进行猜测，本次以唯一主工程为准直接统一升级到 1.5.1。

## 2. 深色模式子菜单为何仍然是白色

原 `Controls.Menu.xaml` 只为 `Menu`、`MenuItem` 和 `Separator` 设置了背景、前景、尺寸与内边距，没有接管 `MenuItem` 的 `ControlTemplate`。

WPF 的子菜单并不是主菜单栏内部普通面板，而是由默认 `MenuItem` 模板中的独立 `Popup` 窗口承载。原样式虽然把主菜单项文字和表面设置为动态资源，但弹出的 `Popup`、子菜单边框、快捷键文字、箭头、禁用状态和分隔线仍继续使用 Windows 默认菜单模板与系统菜单色，因此在应用深色主题下仍显示亮白弹层。

## 3. 菜单主题修复方案

### 3.1 完整接管 MenuItem 弹层模板

`Controls.Menu.xaml` 现在为 `MenuItem` 提供完整模板，并根据 `MenuItem.Role` 处理：

- 顶层菜单头：弹层向下展开；
- 顶层普通项：保持完整点击热区；
- 子菜单头：弹层向右展开，并显示箭头；
- 子菜单普通项：隐藏箭头；
- 选中/单选项：显示主题化勾选标记；
- 键盘焦点：显示克制的强调色边框。

弹层 `PART_Popup` 的背景、边框、文字、快捷键、箭头、悬停、打开、禁用、选中与焦点状态均使用 `DynamicResource`。主题字典被替换后，菜单无需重启即可同步更新。

### 3.2 ContextMenu

新增 `ContextMenu` 模板，使用与子菜单相同的：

- `MenuPopupBackgroundBrush`；
- `MenuPopupBorderBrush`；
- `TextPrimaryBrush`；
- `MenuItem` 状态模板；
- 主题化分隔线。

后续右键菜单或“更多”弹层只要使用标准 `ContextMenu` / `MenuItem`，即可自动获得同一主题。

### 3.3 各主题资源

浅色、深色和高对比主题新增：

- `MenuPopupBackgroundBrush`
- `MenuPopupBorderBrush`
- `MenuItemHoverBrush`
- `MenuItemOpenedBrush`
- `MenuShortcutBrush`
- `MenuSeparatorBrush`

深色弹层背景为深灰 `#252C35`，不是纯黑或纯白；悬停为 `#303945`，快捷键文字为 `#94A2B0`。高对比模式继续使用 Windows 系统菜单、菜单文字与高亮颜色。

## 4. 表格横向滚动缺失原因

结果表原本已声明 `HorizontalScrollBarVisibility="Auto"`，列宽也大多是固定值，但所有滚动条仍使用 Windows 默认模板。深色模式下默认滚动槽和滑块与表格背景对比不足，窗口变窄后虽然 `DataGrid` 具有横向滚动能力，滚动条不够清晰、主题不统一，也缺少设计系统级的高度、滑块最小尺寸和交互状态约束，因此用户会感知为“没有可用横向滚动条”。

## 5. 横向滚动条布局位置

结果表继续位于归片工作区主结果区域的 `Grid.Row="3"`，快捷摘要位于其后的 `Grid.Row="4"`。

横向滚动条使用 `DataGrid` 内置 `ScrollViewer` 的底部栏：

```text
表头
数据区
DataGrid 内部横向滚动条
所选记录快捷摘要
```

因此滚动条只属于结果表，不会移动到整个窗口底部，不会覆盖摘要，也不会与“查看完整明细”按钮重叠。

## 6. 表头与数据体同步滚动

没有另外制作第二套表头或外置滚动条，而是继续使用一个标准 `DataGrid` 内部滚动视口：

- `ScrollViewer.HorizontalScrollBarVisibility="Auto"`
- `ScrollViewer.VerticalScrollBarVisibility="Auto"`
- `ScrollViewer.CanContentScroll="True"`
- `FrozenColumnCount="3"`

表头和单元格由同一个 `DataGrid` `ScrollViewer` 驱动，拖动时天然同步，不需要手工复制偏移量，也不会产生两个滚动位置不同步的问题。前三列保持冻结，其余列标题与对应数据同步横向移动。

列继续使用明确固定宽度，例如客户原始输入 145、标准化名称 125、JPG/RAW 文件名 155、备注 260；窗口缩窄时不会把全部列无限压缩，而是通过横向滚动浏览。

## 7. 深色模式滚动条样式

`Controls.Tables.xaml` 新增统一 `ScrollBar`、水平/垂直 `Thumb` 和翻页区域模板：

- 水平滚动条高度：14；
- 水平滑块最小宽度：28；
- 滚动槽：`ScrollBarTrackBrush`；
- 滑块：`ScrollBarThumbBrush`；
- 悬停：`ScrollBarThumbHoverBrush`；
- 拖动：`AccentBrush`；
- 禁用：降低透明度但保持可辨认。

深色模式滚动槽为 `#171D24`，滑块为 `#687684`，悬停为 `#94A2B0`；浅色和高对比模式分别使用对应主题资源。滚动条不是只在悬停时才出现滑块。

## 8. 修改文件清单

- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Menu.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Tables.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Theme.HighContrast.xaml`
- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/Views/HelpWindow.xaml`
- `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`
- `src/RAWSelectionAssistant/app.manifest`
- `src/RAWSelectionAssistant.Core/Models/Branding.cs`
- `Directory.Build.props`
- `installer/RAWSelectionAssistant.iss`
- `README.md`
- `docs/UI设计系统_1.4.0.md`
- `tests/RAWSelectionAssistant.Tests/UsabilityFix151Tests.cs`
- `tests/RAWSelectionAssistant.Tests/UiDesignSystem140Tests.cs`
- `tests/RAWSelectionAssistant.Tests/UiPolish142Tests.cs`
- `tests/RAWSelectionAssistant.Tests/UiSimplification144Tests.cs`
- `tests/RAWSelectionAssistant.Tests/FeedbackSidebar143Tests.cs`

## 9. 自动化测试结果

- Release 全量测试：337/337 通过
- 失败：0
- 跳过：0
- 测试编译警告：0

新增覆盖包括：

1. 深色菜单弹层不使用纯白硬编码；
2. 子菜单文字、快捷键、箭头可读；
3. Hover、Opened、Disabled、Checked、Focus 状态；
4. ContextMenu 与 Separator 主题；
5. 浅色、深色、高对比主题菜单资源；
6. 跟随系统主题后动态替换字典；
7. 结果表横向滚动为 Auto；
8. 同一 DataGrid 视口驱动表头与内容；
9. 滚动条位于结果表之前、摘要区域之后的正确层级；
10. 正常宽度不强制显示滚动条；
11. 深色滚动槽、滑块和悬停配色；
12. 125% 与 150% DPI 指标；
13. 1.5.1 版本、WinExe、自包含和安装包命名。

## 10. 编译与发布结果

- Debug 编译：通过，0 错误、0 警告
- Release 编译：通过
- win-x64 self-contained 发布：通过
- 发布目录文件数：261
- Inno Setup 安装包：通过
- Release Provider：None
- 安装后 Provider：None
- Release Mock：继续由 `allowMockProvider: false` 禁止

## 11. 安装与启动测试

- 安装位置：`C:\Program Files\北尾鸲归片助手`
- 已安装程序版本：1.5.1
- 启动结果：通过
- 进程状态：运行中
- 窗口标题：北尾鸲归片助手
- 菜单、侧栏和项目中心无障碍结构读取：通过

自动化准备打开主题菜单时检测到用户正在操作并随后将窗口最小化。为避免抢夺焦点，停止了后续自动点击；未使用坐标猜测或强制恢复窗口。深色菜单修复由完整自定义模板、动态资源测试、Release 编译和安装版启动加载共同验证。

## 12. 安装包路径

- 自包含发布目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\publish\win-x64`
- 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\北尾鸲归片助手_Setup_1.5.1_x64.exe`
- 安装包大小：48,708,350 字节
- SHA-256：`5DB1095F897B95CB2B88219497B7D86D1CABD8CD47ACC7FC27867E52EA29D6B0`

## 13. 已知问题

1. 需求描述称基线为 1.5.0，但当前工作区唯一主工程实际为 1.4.4，且未找到 1.5.0 副本。本次按唯一主工程直接升级到 1.5.1。
2. 安装验收期间检测到用户输入并最小化窗口，自动化遵循安全规则停止继续点击，因此没有生成深色子菜单运行截图。该限制不影响源代码模板、自动化测试、Release 构建、安装或启动结果。
3. 未发现应用自身的已知功能缺陷。
