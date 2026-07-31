# 回传给 GPT：北尾鸲归片助手 1.4.3 侧栏与反馈功能报告

## 一、完成状态

- 版本：1.4.3
- 本次两个增量功能均已完成：帮助菜单新增“建议与问题反馈”；侧边栏收起后完全隐藏并释放布局宽度。
- 原有编号解析、索引、匹配、复制、报告、项目历史、首次教程、主题、授权和安装逻辑未重写。
- Release 保持 `WinExe`，启动时不出现 CMD 或 PowerShell。
- 正式授权配置继续为 `Provider=None`，`allowMockProvider: false`，未启用 Mock 专业版。

## 二、侧边栏旧问题原因

### 1. 收起后图标仍显示

旧实现把 `SidebarWidth` 从 220 改为 68，只隐藏导航文字，没有隐藏导航按钮和 PathGeometry 图标；侧栏背景、分隔线和导航容器仍在布局中，因此形成一条只显示图标的窄栏，主内容区也没有获得全部宽度。

### 2. 底部按钮被裁切

旧实现让底部切换按钮继续留在 68 像素宽侧栏中，同时继承导航按钮的左右 Padding、Margin 和图标间距。按钮期望宽度大于可用宽度，边框与图标被父容器裁切，看起来只显示一部分。

## 三、新侧边栏布局结构

- 主体继续使用两列 Grid：侧边栏列和主内容列。
- 展开状态下 `SidebarWidth=220`；收起状态下 `SidebarWidth=0`，`SidebarContainer.Visibility=Collapsed`。
- 收起时导航按钮、图标、文字、分组标题、分隔线、版本卡、背景和边框全部随父容器退出视觉树。
- 主内容列为 `*`，侧栏归零后自动占用释放出的宽度。
- 侧栏内部为三行 Grid：
  - Row 0：可滚动导航区；
  - Row 1：固定版本状态卡；
  - Row 2：固定“收起侧栏”按钮。
- 版本卡和底部按钮不进入 ScrollViewer；侧栏底部保留 14 像素内边距，按钮横向 Stretch、`MinWidth=0`，不再被状态栏覆盖或裁切。

## 四、展开按钮位置与互斥

- 收起后的“展开侧边栏”按钮位于顶部应用栏左侧，完全在侧栏容器之外。
- 按钮尺寸为 36×36，使用项目内置 `IconExpand` PathGeometry，带 ToolTip 和 `AutomationProperties.Name=展开侧栏`。
- 展开按钮绑定 `IsSidebarCollapsed`；底部收起按钮位于只在 `IsSidebarExpanded` 时显示的侧栏容器中，两个按钮不会同时出现。

## 五、状态保存与快捷键

- 状态继续写入 `settings.json` 的 `Appearance.SidebarCollapsed`。
- 用户切换侧栏后立即通过现有设置服务保存；重启后恢复上次状态，不重新扫描、不清空项目、不改变当前步骤。
- 旧设置缺少字段时，布尔默认值为 false，即默认展开。
- `Ctrl+B` 同时配置窗口 KeyBinding 和 `PreviewKeyDown` 焦点兜底；焦点在搜索框等子控件时仍转发到同一 `ToggleSidebarCommand`。
- “视图”菜单提供“显示侧边栏”，勾选状态绑定 `IsSidebarExpanded`，并增加访问键 B。
- 为保证减少动态效果模式稳定，本版采用直接布局切换，不播放宽度、缩放或透明度动画。

## 六、反馈菜单位置

“帮助(H)”菜单顺序为：

1. 新手教程
2. 快捷键
3. 使用说明
4. 建议与问题反馈
5. 打开日志目录
6. 关于北尾鸲归片助手

“建议与问题反馈”提供 `Ctrl+Shift+F`，并增加菜单访问键 F。快捷键同样有 `PreviewKeyDown` 焦点兜底。

## 七、反馈窗口结构

- 新增内部 `FeedbackDialog`，没有浏览器页面、localhost 或后台服务。
- 标题为“建议与问题反馈”，使用当前 DynamicResource 设计系统，跟随浅色、深色、系统主题和强调色。
- 包含只读作者邮箱、复制邮箱、关闭、撰写邮件及窗口内部状态提示。
- “撰写邮件”为默认按钮；Enter 执行撰写邮件，Escape 关闭；按钮均有无障碍名称。
- 邮箱地址只在 `Branding.SupportEmail` 中定义一次，当前值为 `3183483929@qq.com`。

## 八、复制邮箱

- `FeedbackService.CopyEmail()` 通过 Windows Unicode 剪贴板写入邮箱地址。
- 成功时在窗口内部显示“邮箱已复制”，不使用 MessageBox。
- 剪贴板不可用时捕获异常，显示“复制失败，请手动选择邮箱地址复制。”，程序不崩溃。

## 九、撰写邮件与失败兜底

- 只有用户主动点击“撰写邮件”后，才以 `UseShellExecute=true` 调用 `mailto:`。
- 收件人：`3183483929@qq.com`。
- 主题：`北尾鸲归片助手建议与问题反馈`。
- 正文自动包含问题/建议、操作步骤、期望结果、实际结果模板，以及软件版本和 Windows 版本。
- 主题和正文使用 URI 编码。
- 默认邮件应用缺失或启动异常时，服务捕获异常并自动复制作者邮箱，内部提示“未检测到可用的默认邮件应用，作者邮箱已为你复制。”；复制按钮同步显示为“再次复制邮箱”。

## 十、隐私保护

邮件模板不会自动填写或上传以下信息：

- 用户照片、照片文件名或完整路径；
- 客户姓名、客户编号或项目名称；
- EXIF 信息；
- 激活码、设备指纹或授权 Token；
- 日志内容。

反馈服务只构建固定模板，不读取当前项目、索引、选片、授权存储或日志文件。

## 十一、主要修改文件

- `Directory.Build.props`
- `README.md`
- `settings.example.json`
- `docs/UI设计系统_1.4.0.md`
- `docs/UI页面验收清单_1.4.0.md`
- `installer/RAWSelectionAssistant.iss`
- `src/RAWSelectionAssistant.Core/Models/Branding.cs`
- `src/RAWSelectionAssistant.Core/Models/FeedbackModels.cs`
- `src/RAWSelectionAssistant.Core/Services/FeedbackRequestBuilder.cs`
- `src/RAWSelectionAssistant.Core/Services/FeedbackService.cs`
- `src/RAWSelectionAssistant/App.xaml.cs`
- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/MainWindow.xaml.cs`
- `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`
- `src/RAWSelectionAssistant/app.manifest`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Navigation.xaml`
- `src/RAWSelectionAssistant/Services/FeedbackPlatformServices.cs`
- `src/RAWSelectionAssistant/Services/IDialogService.cs`
- `src/RAWSelectionAssistant/Services/WpfDialogService.cs`
- `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`
- `src/RAWSelectionAssistant/Views/FeedbackDialog.xaml`
- `src/RAWSelectionAssistant/Views/FeedbackDialog.xaml.cs`
- `src/RAWSelectionAssistant/Views/HelpWindow.xaml`
- `tests/RAWSelectionAssistant.Tests/FeedbackSidebar143Tests.cs`
- `tests/RAWSelectionAssistant.Tests/UiDesignSystem140Tests.cs`
- `tests/RAWSelectionAssistant.Tests/UiPolish142Tests.cs`

## 十二、自动化测试结果

- Release 全部测试：287/287 通过，失败 0，跳过 0。
- 1.4.3 新增测试：36 项。
- 覆盖反馈菜单、内部对话框、邮箱、mailto 收件人/主题/正文、版本字段、隐私排除、邮件客户端失败兜底、侧栏 0 宽、导航隐藏、按钮互斥、底部按钮、Ctrl+B、状态持久化、主题、高 DPI、WinExe、Provider=None 和 Mock 禁用。
- Release Build：通过，0 警告，0 错误。
- win-x64 self-contained 发布：通过。

## 十三、安装与启动验收

- 独立验收安装包静默安装退出码：0。
- 验收安装路径：`C:\Program Files\北尾鸲归片助手_验收测试`。
- 安装版版本：1.4.3。
- 安装版 Provider：None。
- 安装版正常启动，窗口标题为“北尾鸲归片助手”。
- 收起状态持久化为 true；启动后辅助功能树只显示独立“展开侧边栏”按钮，六个侧栏导航按钮均不存在。
- 帮助菜单的“建议与问题反馈”及完整菜单顺序已在安装版辅助功能树中识别。
- 正式发布目录无 PDB，正式 EXE ProductVersion 为 1.4.3、FileVersion 为 1.4.3.0。

## 十四、正式安装包

- 路径：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\北尾鸲归片助手_Setup_1.4.3_x64.exe`
- 大小：48,716,616 字节。
- SHA-256：`C84A1BF11E58B2B78C454AEEA39A86335FD4A8FBA23AFDEC1AA21E6182C9A72F`

## 十五、已知问题

- 本机 Windows.Graphics.Capture 调用返回 `0x80004002`，无法生成自动截图；辅助功能树与自动化测试不受影响。
- 本机自动化驱动可以读取 WPF 弹出菜单，但不会把弹出菜单设为键盘焦点，无法通过自动化工具完成菜单字母或组合键的最终视觉点击。相关命令绑定、焦点兜底、对话框逻辑和失败路径已由自动化测试覆盖；实际安装版的菜单入口与侧栏收起视觉树已确认。
- 未发现产品代码或发布工件中的其他已知阻塞问题。
