# 像素蛋挞 2.0.3 工作台提示优化与工具箱增强报告

## 一、交付结论

- 版本：2.0.3
- Release Build：通过，0 警告、0 错误
- 自动化测试：405 / 405 通过，失败 0，跳过 0
- 发布方式：win-x64 self-contained，WinExe，无控制台窗口
- 授权状态：Provider 继续保持 `None`，Release 不启用 Mock
- 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\像素蛋挞_Setup_2.0.3_x64.exe`
- 安装包大小：48,754,028 字节
- SHA-256：`D6768A5DDC8A3CBCD5AA9C9FB21675006252A7FE480DA9BEF1A9AC60FD8F1AA9`

## 二、首页主卡问号提示

“开始本地分片”主卡不再常驻显示长段说明，只保留主标题。右上角增加独立的 `?` 帮助按钮，避免按钮嵌套造成点击区域冲突。

- Tooltip 绑定 `LocalSplitHelpText`；
- 文案为“导入 TXT、客户选图 JPG 或照片编号，匹配本地 JPG、RAW 及相关文件。”；
- 支持键盘焦点；
- `AutomationProperties.Name` 为“本地分片说明”；
- 点击帮助按钮不会误触发主卡导航。

## 三、深色主题控件统一修复

在设计系统中集中补齐以下控件的深浅主题样式：

- TextBox；
- PasswordBox；
- ComboBox；
- ComboBoxItem；
- ListBox；
- ListBoxItem；
- CheckBox；
- RadioButton；
- ToolTip。

深色主题新增输入框背景、输入框边框、输入文字、下拉背景、Hover、选中项和 Tooltip 背景 Token；浅色主题同步提供对应 Token，避免主题切换后结构或可读性回退。

修复前容易出现“过曝白块”的位置主要包括设置弹窗中的主题、强调色、密度和侧栏下拉框，本地分片页的格式下拉框，以及 FTP、批量处理工具页中的文本输入和下拉选项。修复后统一使用主题资源，不再依赖系统默认白底模板。

## 四、快捷工具固定与取消固定

新增 `QuickToolsService`、`ToolboxItemDefinition` 和 `TogglePinnedToolCommand`：

- 默认快捷项：归片工作区、整理图片、批量压缩、工具箱；
- 工作台显示三个可配置快捷工具，工具箱作为固定第四项；
- 支持从工具箱弹层或完整工具箱页固定/取消固定；
- 固定状态切换后立即触发 `PinnedToolboxItems` 刷新；
- 超过四项时显示提示，不覆盖已有选择；
- 允许用户取消全部动态快捷工具，只保留固定的工具箱入口；
- 工具箱本身不能被移除。

### 持久化

固定状态保存在 `settings.json` 的 `PinnedQuickTools` 数组中。旧设置文件缺少该字段时由 `AppSettings` 默认值和 `SettingsService` 升级逻辑自动补齐；显式空数组会被保留。安装版实测取消固定批量压缩、固定拼图并重启后，配置仍然保留。

## 五、整理图片工具

工具入口统一命名为“整理图片”，页面标题为“整理图片工具”，页面内能力名为“分组整理”。页面壳子包含：

- 已分组区域；
- 新建分组；
- 清晰的深色空状态；
- 缩略图、组名和图片数量说明；
- 批量创建文件夹；
- 导出分组结果占位；
- 为后续拖拽照片、重命名分组和移动到分组预留结构。

核心模型新增 `PhotoGroup`，包含 Id、Name、CoverImagePath、Count 和 Items。

## 六、拼图工具

新增深色拼图页面壳子：

- 中央预览画布；
- JPG、PNG 导入空状态；
- 右侧模板与参数面板；
- 2、3、4、5 张分类；
- 2 图左右、2 图上下、3 图竖排、4 图基础形式、1 主图 + 2 副图等模板；
- 边框、间距、圆角参数；
- 导出按钮占位。

本版本完成可扩展 UI 框架，实际图片渲染和导出算法继续保持禁用，未伪装为已完成能力。

## 七、工具箱与工作台联动

- Popup 中新增“整理图片”和“拼图”；
- 完整工具箱页新增两项工具卡；
- 两项工具均可固定到首页快捷区；
- 首页快捷区通过 `PinnedToolboxItems` 数据绑定即时刷新；
- 工具箱始终作为第四个稳定入口，不会与动态快捷卡重叠。

## 八、主要修改文件

- `Directory.Build.props`
- `README.md`
- `installer/RAWSelectionAssistant.iss`
- `src/RAWSelectionAssistant.Core/Models/AppSettings.cs`
- `src/RAWSelectionAssistant.Core/Models/Branding.cs`
- `src/RAWSelectionAssistant.Core/Models/QuickToolsService.cs`
- `src/RAWSelectionAssistant.Core/Models/ToolboxItemDefinition.cs`
- `src/RAWSelectionAssistant.Core/Services/SettingsService.cs`
- `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`
- `src/RAWSelectionAssistant/app.manifest`
- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml`
- `src/RAWSelectionAssistant/Views/HelpWindow.xaml`
- `tests/RAWSelectionAssistant.Tests/WorkbenchEnhancement203Tests.cs`
- 多个旧版本 UI 测试文件的版本和结构断言。

## 九、新增测试范围

`WorkbenchEnhancement203Tests` 新增 25 项专项测试，覆盖：

- 问号帮助、Tooltip 文案、键盘和无障碍名称；
- 深浅主题输入控件 Token 与 ComboBox 下拉模板；
- 默认快捷工具、数量上限、去重、未知项过滤、空选择；
- settings.json 保存与重载；
- 固定命令、即时刷新和工具箱固定入口；
- Popup、完整工具箱、整理图片和拼图页面；
- 设置入口、查看全部工具、侧栏折叠；
- Provider=None、Release 禁用 Mock、WinExe 和 2.0.3 安装包命名。

全量测试结果：405 / 405 通过，0 失败，0 跳过。

## 十、安装版复验

隔离安装目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\install-verification\2.0.3\app`（复验结束后已卸载）

复验结果：

- 静默隔离安装成功，退出码 0；
- 安装目录包含主程序和卸载程序；
- 主程序正常启动，窗口标题“像素蛋挞”，进程响应正常；
- 首页“本地分片说明”帮助按钮可被无障碍树识别；
- 工具箱 Popup 可打开；
- “查看全部工具”可进入完整工具箱；
- 完整工具箱可识别批量压缩、整理图片、拼图和固定操作；
- “整理图片工具”页面可打开，已分组、新建分组、批量创建文件夹均存在；
- “拼图模式”页面可打开，模板与参数、导入提示、导出入口均存在；
- 取消固定批量压缩后 settings.json 正确移除；
- 固定拼图后 settings.json 正确写入；
- 重启后拼图仍显示在首页快捷区，批量压缩保持取消状态；
- 测试前用户 settings.json 已按 SHA-256 完整恢复；
- 静默卸载成功，退出码 0，隔离应用目录已删除；
- 用户设置目录保留，未随测试卸载删除。

安装日志：`D:\AI AGENT\RAWSelectionAssistant\artifacts\install-verification\2.0.3\install.log`

由于当前会话未提供专用 Windows 截图自动化入口，本次未生成安装版截图；安装版交互通过 Windows UI Automation 结构和真实配置文件变化完成复验。

## 十一、已知问题

- 整理图片目前是高质量 UI 和数据结构壳子，真实拖拽分组、文件夹创建、移动和导出尚未接入；
- 拼图目前是模板和参数壳子，真实画布排版、图片渲染和导出尚未接入；
- 工具箱完整页面的固定操作当前重点覆盖批量压缩、整理图片和拼图，后续可为其他预览工具补齐同样的显式固定按钮；
- 本次没有生成安装版视觉截图，但编译、全量测试、真实安装启动、页面导航、持久化、重启和卸载均已验证。
