# 像素蛋挞 2.0.4 整理图片与拼图可用版报告

## 1. 结论

2.0.4 的功能开发、Release 编译、自动化测试、UI Review、self-contained 发布、安装、启动、快捷工具持久化、工具页面烟测和卸载均已完成。当前产物仍定义为**发布候选**，没有合并到 `main`，也没有创建 `v2.0.4` Tag，原因是 125%、150%、200% 的真实 Windows 物理 DPI 人工矩阵尚未完成。

最终代码分支：`release/2.0.4`  
最终代码提交：`20d5d5bf385a7636ca645e5bf84aabfcc11eb449`  
基线：`v2.0.3.1` / `19536e891c5677e88f8704d305c07a70804a22bc`  
正式 Tag：未创建  
是否合并 main：否

## 2. 完成范围

### 2.1 快捷工具

- 支持鼠标拖放排序、拖动预览和插入位置指示。
- Escape 可取消拖动。
- 支持 Alt+左/右方向键排序、Enter 打开工具。
- 右键菜单支持向左/向右移动、移除和打开管理窗口。
- 新增双栏“快捷工具管理”窗口，支持添加、移除、上下调整、拖动、保存、取消和恢复默认。
- 顺序通过 `QuickToolLayout` 与 `settings.json` 持久化，兼容旧 `PinnedQuickTools`。
- 1280 宽度只展示两个快捷工具，其余进入“更多”溢出 Popup。
- 工具箱仍可从顶部、侧栏和工具菜单访问。
- 隔离安装实测：将 `PhotoOrganize` 移到首位后，持久化顺序为 `PhotoOrganize / Workflow / BatchCompress`，重启后仍保留。
- 安装复验中发现并修复工具箱 Popup 的数据上下文隔离问题；“管理快捷工具”最终改用明确的 `Click` 入口并已实际打开验证。

### 2.2 整理图片

- 已替换预览壳，提供真实三栏页面：来源与规则、分组结果、操作摘要与执行。
- 支持导入照片、目录和拖放文件/目录。
- 支持 JPG、JPEG、PNG、TIFF、TIF、WEBP、HEIC，以及现有归片系统支持的 RAW 扩展名。
- 元数据读取失败安全降级，缺少拍摄信息进入“元数据缺失”，不伪造日期。
- 已实现原文件夹、拍摄日期/年份/年月/小时、相机品牌/型号、镜头、格式、横竖方图、文件名前缀/数字段、大小区间、每 N 张、自定义关键字和手动分组。
- 支持新建、合并、拆分、拖放/多选移动、删除空组、排序、设为封面和排除照片；删除分组只删除定义，不删除源照片。
- 支持仅保存方案、复制到新目录、移动到新目录三种模式。

### 2.3 OrganizePlan 与 Manifest

新增 `OrganizePlan`、`OrganizeManifest`、`OrganizeManifestItem`，包含：

- `SchemaVersion`
- `OperationId`
- `SourcePath`
- `DestinationPath`
- `OperationType`
- `ConflictPolicy`
- `ExpectedSourceSize`
- `ExpectedSourceModifiedAt`
- `OptionalSourceHash`
- `State`
- `ErrorCode`

清单预览包含来源数、有效文件数、分组数、元数据缺失数、冲突风险、预计输出大小、来源根目录、输出根目录、操作类型与风险摘要。用户确认的是具体清单摘要，不是模糊的“开始”。

### 2.4 文件安全

- 默认复制、默认自动编号、不覆盖、不删除源文件。
- 来源文件只读打开；目标使用 `FileMode.CreateNew`。
- 写入完成后 Flush，并校验文件长度；可选 SHA-256。
- 输出目录不得等于来源目录，也不得位于来源目录内部。
- 移动采用复制、校验、再删除源文件，并要求二次确认。
- 覆盖要求额外确认，并在任务专属目录创建备份；失败时恢复。
- 取消复制任务只清理本任务创建的不完整输出，不删除未知文件。
- 移动任务取消不会删除已完成且已校验的目标文件，避免数据丢失。
- CSV、JSON、TXT 三种报告均已实现。

### 2.5 撤销机制

移动整理生成反向记录。撤销前重新检查目标存在、文件大小或哈希一致、原路径没有新同名文件，并且反向恢复不会覆盖。任一条件不满足即停止，不强制撤销。

### 2.6 拼图

- 已替换预览壳，提供顶部工具栏、中央画布、右侧模板与参数区、底部待用照片条。
- 支持模板拼图、纵向长图、横向长图。
- `CollageTemplateCatalog` 使用数据驱动模板，覆盖 2 至 6 张、共 23 个以上模板，不为每个模板复制 XAML 页面。
- 支持多图导入、自动填充、槽位交换、替换、删除、旋转 90°、水平/垂直翻转、重置、缩放、构图偏移、填充裁切和完整显示。
- 支持自由、1:1、4:5、3:4、2:3、3:2、16:9、9:16、A4 横竖版及常用社交尺寸。
- 支持外边距、图片间距、圆角、边框、背景、透明背景和阴影参数，滑块显示真实数值。
- 编辑阶段通过 OnLoad 代理位图避免长期锁定源文件；导出阶段读取原图并及时释放句柄。
- 支持 JPG 与 PNG；JPG 默认质量 95；默认自动编号且不覆盖；取消时清理不完整输出；源照片字节不变。
- WPF 实际导出测试验证 JPG 源文件不变、透明 PNG、自动编号。

### 2.7 主题收尾

- 为 CheckBox、RadioButton、Slider、ProgressBar、ScrollViewer 补充主题模板。
- 修复深色页面滚动条交汇处的系统白色方块。
- 继续覆盖 ComboBox、TextBox、ListBox、ToggleButton、ContextMenu、Popup、MenuItem、TabControl、ScrollBar、DatePicker、DataGrid、ToolTip、禁用、Hover、Focus 与高对比度资源。
- UI Review 中未发现整理图片、拼图和 1280 溢出状态的白底系统控件或调试边框。

## 3. 测试结果

最终 Release 自动化结果：

- 总计：441
- 通过：441
- 失败：0
- 跳过：0
- Core/结构/安全测试：438
- WPF 实际导出测试：3
- Release 编译警告：0
- Release 编译错误：0

证据：

- `D:\AI AGENT\RAWSelectionAssistant\artifacts\tests\2.0.4\test-summary.json`
- `D:\AI AGENT\RAWSelectionAssistant\artifacts\tests\2.0.4\2.0.4-core.trx`
- `D:\AI AGENT\RAWSelectionAssistant\artifacts\tests\2.0.4\2.0.4-wpf.trx`

## 4. UI Review

- 截图目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.0.4`
- 截图数量：15
- 唯一 SHA-256 数量：15
- 总览：`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.0.4\像素蛋挞_2.0.4_UI验收总览.png`
- 总览 SHA-256：`534DBCEC86A229763718B3CBF208D5A2EF5358A15C390A9DB39259383A7E7332`

截图生成于提交 `427bd8399324553cc34a45ff7814d84adc06938b`。最终提交只把工具箱 Popup 的“管理快捷工具”从命令绑定改为 Click 处理器，没有改变视觉属性；用户明确要求继续时不重跑已完成截图，因此没有再次生成。该事实已写入 release-manifest。

## 5. 真实 DPI 门禁

125%、150%、200% 的真实系统缩放尚未由人工完成。逻辑尺寸截图没有被冒充为物理 DPI 验证。

复核清单：`D:\AI AGENT\RAWSelectionAssistant\docs\release\像素蛋挞_2.0.4真实DPI人工复核清单.md`

因此：

- 2.0.4 当前为发布候选；
- 不合并 `main`；
- 不创建 `v2.0.4` Tag；
- 不建议开始 2.1.0。

## 6. 隔离安装复验

- 安装退出码：0
- 安装文件版本：2.0.4.0
- 产品版本：2.0.4
- 主窗口标题：像素蛋挞
- 主窗口可见且进程响应正常：是
- 快捷工具管理窗口打开：通过
- 快捷工具排序保存并重启保留：通过
- 整理图片页面、添加照片、添加文件夹、清单预览入口：通过
- 拼图页面、导入、导出、模板参数区：通过
- 卸载退出码：0
- 安装目录清理：通过
- 用户设置保留：通过，卸载前后 SHA-256 一致

文件操作和拼图导出的真实数据安全由自动化集成测试覆盖；本次安装版烟测验证窗口和功能入口，不把 UI 烟测冒充完整人工文件操作录像。

证据目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\install-verification\2.0.4`

## 7. 发布产物

- 发布目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.0.4\publish\win-x64`
- 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.0.4\installer\像素蛋挞_Setup_2.0.4_x64.exe`
- 安装包大小：48,796,563 字节
- 安装包 SHA-256：`72CF771D7BEE5E9AC2D09C1EF8F79E709721001A0445CAED1A854FDE76342127`
- release-manifest：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.0.4\release-manifest.json`

2.0.3 和 2.0.3.1 安装包均保留，未被覆盖。2.0.3.1 基线安装包 SHA-256 仍为 `A43A2C18203D40942A754884EABF1B556B6B6050FD374725EFF496A0F6DBDB79`。

## 8. 新增文件

- `docs/release/像素蛋挞_2.0.4真实DPI人工复核清单.md`
- `src/RAWSelectionAssistant.Core/Models/CollageModels.cs`
- `src/RAWSelectionAssistant.Core/Models/OrganizeModels.cs`
- `src/RAWSelectionAssistant.Core/Models/QuickToolLayout.cs`
- `src/RAWSelectionAssistant.Core/Services/OrganizeService.cs`
- `src/RAWSelectionAssistant/Services/CollageExportService.cs`
- `src/RAWSelectionAssistant/Views/QuickToolsManagerWindow.xaml`
- `src/RAWSelectionAssistant/Views/QuickToolsManagerWindow.xaml.cs`
- `tests/RAWSelectionAssistant.Tests/Version204FeatureTests.cs`
- `tests/RAWSelectionAssistant.WpfTests/CollageExportTests.cs`
- `tests/RAWSelectionAssistant.WpfTests/Properties/AssemblyInfo.cs`
- `tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj`

## 9. 主要修改文件

- 版本与发布：`CHANGELOG.md`、`README.md`、`RAWSelectionAssistant.sln`、`build/Version.props`、`installer/RAWSelectionAssistant.iss`、应用清单和项目文件。
- 快捷工具：`AppSettings.cs`、`QuickToolsService.cs`、`ToolRegistry.cs`、`SettingsService.cs`、`MainViewModel.cs`、`MainWindow.xaml/.cs`、对话服务。
- 整理与拼图：`ToolPageViewModels.cs`、`OrganizePhotosView.xaml/.cs`、`CollageView.xaml/.cs`。
- 主题：`Controls.Inputs.xaml`、`Controls.Tables.xaml`。
- 验收工具：`capture_ui_review_set.ps1`、`prepare_ui_review.ps1`、`create_ui_contact_sheet.py`。
- 原有测试中的版本断言已统一更新为 2.0.4，既有业务测试继续保留。

## 10. 未完成范围与已知问题

- 未开始 2.1.0 统一任务引擎。
- 未引入 SQLite、云选片、AI、联机拍摄 SDK、Lightroom/Capture One 数据库或自动更新。
- Provider 继续保持 None，Release 未启用 Mock。
- 真实 125%/150%/200% DPI 人工矩阵待完成，是唯一正式发布门禁。
- 最终 Click 处理器修复后按用户要求没有重拍 UI Review；视觉结构没有变化，但截图提交与最终代码提交不同。

## 11. 是否建议进入 2.1.0

**不建议。** 应先由人工完成三组真实 DPI 复核；全部通过后再合并 `release/2.0.4` 到 `main`、创建 `v2.0.4` Tag，并等待用户明确确认后再开始 2.1.0。
