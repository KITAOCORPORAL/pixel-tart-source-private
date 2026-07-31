# 像素蛋挞 2.0.2 设置与工具箱交互修复报告

## 一、完成结论

- 版本：**2.0.2**。
- 左侧“设置”入口：已修复，展开与收起状态均可打开设置窗口。
- 工具箱 Popup“查看全部工具”：已修复，可关闭 Popup 并进入完整工具箱页面。
- 侧栏收起后图标裁切：已修复。
- 150% 显示缩放安装版复验：已完成并通过。
- Release 构建：通过，0 警告、0 错误。
- 自动化测试：**380/380 通过，失败 0，跳过 0**。
- Provider：保持 `None`；Release 未启用 Mock。
- 程序类型：保持 WinExe，PE Subsystem 为 2，无控制台窗口。

## 二、设置入口修复

### 原因

设置入口原来复用通用 `NavigateCommand`。该命令受忙碌状态和新手教程状态的统一可执行条件约束，界面上可能出现按钮存在但点击无反馈的情况。

### 修复

- 新增独立 `OpenSettingsCommand`。
- 侧栏设置按钮直接绑定 `OpenSettingsCommand`。
- “视图 → 外观设置”同步改用该命令。
- 通用导航收到 `Settings` 参数时也转交给独立命令，保持旧调用兼容。
- 设置命令不再被普通页面导航条件误禁用。

### 安装版结果

实际隔离安装后的 2.0.2 中，通过 `SidebarSettingsButton` 调用设置入口，成功检测到“关闭设置”、常规、外观、输出与报告和完成按钮，结果为：`SettingsOpened = true`。

## 三、工具箱交互修复

### 原因

Popup 属于独立窗口层，原“查看全部工具”仅依赖 Popup 内的通用命令绑定。Popup 关闭、焦点恢复和页面导航之间缺少明确的执行顺序，实际点击可能只关闭弹层而不完成页面切换。

### 修复

- 新增独立 `OpenToolboxPageCommand`。
- “工具 → 打开工具箱”直接进入完整工具箱页面。
- Popup 的“查看全部工具”增加稳定的 `OpenToolboxPage_Click` 链路：
  1. 关闭 `WorkbenchToolboxPopup`；
  2. 执行 `OpenToolboxPageCommand`；
  3. 标记路由事件已处理。
- 保留侧栏和顶部快捷卡打开同一工具箱 Popup 的行为。

### 安装版结果

对实际隔离安装进程调用后：

- `ToolboxPopupOpened = true`
- `ToolboxFullPageOpened = true`

说明侧栏工具箱入口、Popup 和“查看全部工具 → 完整工具箱页面”链路均可用。

## 四、侧边栏收起图标裁切修复

### 根本原因

旧侧栏收起宽度为 54 DIP，同时叠加：

- 容器左右内边距各 5 DIP；
- 按钮左右外边距各 5 DIP；
- 按钮左右内边距各 10 DIP；
- 17 DIP 图标；
- 图标右侧仍保留 10 DIP 展开态文字间距。

收起后文字虽然隐藏，但展开态布局、内边距和图标右间距仍然参与测量，图标可用宽度不足。在 150% DPI 下像素取整会进一步放大裁切现象。

### 宽度与尺寸调整

| 项目 | 修复前 | 修复后 |
|---|---:|---:|
| 展开宽度 | 172 DIP | 172 DIP |
| 收起宽度 | 54 DIP | 60 DIP |
| 导航按钮高度 | 38 DIP | 40 DIP |
| 按钮水平外边距 | 5 DIP | 6 DIP |
| 图标容器 | 未固定 | 20 × 20 DIP |
| 实际图标 | 17 DIP，并保留右间距 | 18 × 18 DIP，居中 |
| 选中指示条 | 可能挤占图标 | 固定预留 3 DIP |

### 统一设计令牌

新增 `SidebarLayoutMetrics`，统一声明：

- `ExpandedWidth = 172d`
- `CollapsedWidth = 60d`
- `ButtonHeight = 40d`
- `ButtonHorizontalMargin = 6d`
- `IconContainerSize = 20d`
- `IconSize = 18d`
- `SelectedIndicatorWidth = 3d`

ViewModel 与 `AppearanceService` 均使用该命名尺寸来源；XAML 设计令牌同步提供对应 Double、Thickness 和 GridLength 资源，删除 54/172 等分散宽度判断。

### 收起与展开模板

- 新建 `SidebarNavButton` 统一模板。
- 展开状态使用“20 DIP 图标列 + 10 DIP 间距 + 文字列”。
- 收起状态完全隐藏展开内容树，仅显示独立的居中图标容器。
- 不保留隐藏文字列，不使用负边距，不使用 `ScaleTransform`。
- PathGeometry 位于固定 Viewbox 中，`Stretch=Uniform`。
- 启用 `UseLayoutRounding` 与 `SnapsToDevicePixels`。
- 焦点环使用独立边框，不通过改变选中指示条厚度挤压图标。
- 收起后切换按钮显示 `IconExpand` 和“展开侧栏”；展开后显示 `IconCollapse` 和“收起侧栏”。

### 底部区域

侧栏底部拆分为独立行：

- 主导航：`*`
- 工具入口：`Auto`
- 版本状态：`Auto`
- 收起/展开按钮：`Auto`

收起后隐藏版本长文案和升级文字，仅保留居中状态点；工具箱、使用教程、建议与问题反馈和展开按钮均保持完整点击区域。

### 150% 安装版实测

当前系统 `AppliedDPI = 144`，即 150%。对实际安装进程读取 UI Automation 边界：

- 收起状态：11 个侧栏按钮全部存在，单项约 **70 × 60 物理像素**，`IsOffscreen=false`。
- 展开状态：11 个侧栏按钮全部存在，单项约 **238 × 60 物理像素**，`IsOffscreen=false`。
- 再次收起：11 个按钮尺寸恢复为 70 × 60，未出现累计偏移或裁切。
- 设置、工具箱 Popup、完整工具箱页面均能在收起状态下进入。

安装版详细结果：

`D:\AI AGENT\RAWSelectionAssistant\artifacts\install-verification\2.0.2\installed-ui-verification.json`

## 五、DPI 验证

专项测试覆盖：

- 100%
- 125%
- 150%
- 175%
- 200%

验证内容包括收起最小宽度、按钮高度、图标容器、图标实际尺寸、指示条预留、Uniform Viewbox、无负边距、无缩放变换、文字列完全移除和所有入口可访问性名称。

150% 额外使用实际隔离安装版本完成运行复验。

## 六、测试结果

- Release Build：成功。
- 构建警告：0。
- 构建错误：0。
- 全部测试：380。
- 通过：380。
- 失败：0。
- 跳过：0。
- 新增 2.0.2 专项测试：24 项，对应需求编号 27–50。

专项覆盖：

- 统一侧栏宽度令牌；
- 收起宽度不低于 56 DIP；
- 60 DIP 推荐值；
- 所有主导航与底部入口；
- 图标边界与 Uniform Viewbox；
- 指示条不覆盖图标；
- 无负边距和 ScaleTransform；
- 键盘焦点环；
- 100%–200% DPI；
- 收起状态设置命令；
- Popup 查看全部工具命令。

原有本地分片、归片工作区、JPG/RAW 匹配、报告导出、授权门控、WinExe 和 Provider=None 测试继续通过。

## 七、版本与安装包

- 产品版本：2.0.2。
- 文件版本：2.0.2.0。
- win-x64 self-contained 发布：成功。
- 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\像素蛋挞_Setup_2.0.2_x64.exe`
- 安装包大小：**48,745,274 字节**。
- SHA-256：`8ADBDEB86F3096423AC26C9C6BE1A3B820C9BE6AC39764E32033B79354F0DDE6`

## 八、隔离安装、启动与卸载

- 最终安装包隔离安装：成功。
- 隔离安装程序版本：2.0.2。
- 窗口标题：像素蛋挞。
- 默认窗口可启动：成功。
- 150% DPI 侧栏复验：成功。
- 设置入口：成功。
- 工具箱 Popup：成功。
- 查看全部工具：成功。
- 完整工具箱页面：成功。
- WinExe 无控制台：成功。
- 验证副本卸载退出码：0。
- 隔离安装目录：已清理。
- 用户设置与照片：未删除。
- 原电脑 2.0.0 卸载注册记录：已恢复。
- 原电脑程序位置：`C:\Program Files\北尾鸲归片助手\`
- 恢复后原程序版本：2.0.0。

安装日志：

`D:\AI AGENT\RAWSelectionAssistant\artifacts\install-verification\2.0.2\install.log`

## 九、主要修改文件

- `Directory.Build.props`
- `README.md`
- `installer\RAWSelectionAssistant.iss`
- `src\RAWSelectionAssistant.Core\Models\Branding.cs`
- `src\RAWSelectionAssistant.Core\Models\SidebarLayoutMetrics.cs`（新增）
- `src\RAWSelectionAssistant\app.manifest`
- `src\RAWSelectionAssistant\RAWSelectionAssistant.csproj`
- `src\RAWSelectionAssistant\MainWindow.xaml`
- `src\RAWSelectionAssistant\MainWindow.xaml.cs`
- `src\RAWSelectionAssistant\ViewModels\MainViewModel.cs`
- `src\RAWSelectionAssistant\Services\AppearanceService.cs`
- `src\RAWSelectionAssistant\Resources\DesignSystem\DesignTokens.xaml`
- `src\RAWSelectionAssistant\Resources\DesignSystem\Controls.Navigation.xaml`
- `src\RAWSelectionAssistant\Resources\DesignSystem\Controls.Status.xaml`
- `src\RAWSelectionAssistant\Resources\DesignSystem\Icons.Navigation.xaml`
- `src\RAWSelectionAssistant\Views\HelpWindow.xaml`
- `tests\RAWSelectionAssistant.Tests\SidebarInteractionFix202Tests.cs`（新增）
- 相关版本与侧栏既有测试文件。

## 十、已知事项

当前 Windows.Graphics.Capture 在本机自动化环境中返回“不支持此接口”，因此本轮没有把窗口截图作为验收依据。该限制不影响软件运行；本轮改用实际安装进程的 UI Automation 控件调用、真实物理边界、离屏状态、安装日志和全量自动化测试完成复验。

未发现影响本次三个修复点的产品已知问题。
