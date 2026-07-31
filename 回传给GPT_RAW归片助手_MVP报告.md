# RAW 归片助手 MVP 回传报告

生成时间：2026-07-28

## 1. 执行结论

项目已完成 MVP 源码、自动化测试、Windows x64 自包含发布、Inno Setup 单文件安装包、隔离安装/卸载验收和回传文档。

最终普通用户只需要运行：

```text
RAW归片助手_Setup_1.0.0_x64.exe
```

安装后从桌面或开始菜单双击“RAW 归片助手”，快捷方式直接指向 `RAWSelectionAssistant.exe`，不经过脚本、命令行、dotnet.exe 或其他启动器。

## 2. 技术栈和实际版本

- 语言：C# 14。
- 桌面框架：WPF。
- 架构：MVVM；界面层与扫描、匹配、复制等核心业务分离。
- SDK：.NET SDK 10.0.302。
- 自包含运行时：Microsoft.NETCore.App 10.0.10、Microsoft.WindowsDesktop.App 10.0.10。
- 目标框架：`net10.0-windows10.0.19041.0`。
- 最低实际安装系统版本：Windows 10 22H2（10.0.19045）x64。
- 运行标识：`win-x64`。
- 安装制作：Inno Setup 7.0.2 x64。
- 自动化测试：MSTest 4.0.2。
- 第三方运行依赖：无。主程序只使用 .NET/WPF 内置能力。

## 3. 项目目录结构

```text
RAWSelectionAssistant/
├─ RAWSelectionAssistant.sln
├─ README.md
├─ Directory.Build.props
├─ build_debug.ps1
├─ build_release.ps1
├─ build_installer.ps1
├─ run_app.ps1                 # 仅供开发调试
├─ create_sample_environment.ps1
├─ installer/
│  └─ RAWSelectionAssistant.iss
├─ src/
│  ├─ RAWSelectionAssistant.Core/
│  │  ├─ Models/
│  │  ├─ Services/
│  │  └─ Utilities/
│  └─ RAWSelectionAssistant/
│     ├─ Assets/
│     ├─ Converters/
│     ├─ Resources/
│     ├─ Services/
│     ├─ Utilities/
│     ├─ ViewModels/
│     └─ Views/
├─ tests/
│  └─ RAWSelectionAssistant.Tests/
└─ artifacts/
   ├─ publish/win-x64/
   └─ installer/
      └─ RAW归片助手_Setup_1.0.0_x64.exe
```

## 4. 已完成功能

### 4.1 输入与文件名标准化

- 支持 JPG/JPEG、TXT、CSV、递归文件夹和 Unicode 纯文本输入。
- 支持换行、空格、中英文逗号、顿号、分号、制表符和竖线分隔。
- 忽略客户文件夹内的隐藏文件和系统文件。
- 独立 `FileNameNormalizer`：去路径、去扩展名、Unicode Form KC 标准化、统一大写、去常见副本后缀、提取末尾连续数字。
- 数字编号比较忽略前导零，但不更改任何真实源文件名。
- 保留全部原始输入记录；重复请求标记为重复并默认取消勾选，只复制一次。

### 4.2 RAW 索引

- 支持需求中列出的 16 种 RAW 扩展名，扩展名不区分大小写。
- 递归扫描多个来源目录。
- 使用哈希字典同时建立完整标准化文件名索引和数字编号索引。
- 扫描在 WPF 进程内异步执行，支持取消、进度和当前目录显示。
- 单个目录无权限、路径失效或文件异常时记录日志并继续其他目录。
- 索引缓存保存在 `%LocalAppData%\RAWSelectionAssistant\Indexes\raw-index.json`。
- RAW 来源目录发生添加、删除或清空时立即使旧索引失效，防止误用上一目录的缓存。

### 4.3 RAW 匹配和冲突处理

- 第一优先级：完整标准化文件名精确匹配。
- 第二优先级：末尾数字编号精确匹配，忽略前导零。
- 不使用模糊字符串、编辑距离或随机候选自动认定。
- 多候选标记为“存在冲突”，未解决前禁止复制该记录。
- 候选窗口显示文件名、完整路径、大小、修改时间和来源目录。
- 用户手动选择后状态变为“已手动确认”。
- 支持从候选窗口在文件资源管理器中显示文件；该动作只在用户主动点击时发生。

### 4.4 RAW 复制

- 只复制，不移动、不删除、不重命名、不修改任何源文件。
- 复制前检查未解决冲突、源文件、输出目录可写性和本地磁盘剩余空间。
- 支持平铺输出和保留来源相对目录结构两种模式。
- 异步流式复制，支持取消；保留最后修改时间。
- 单个文件失败不终止其余文件，并记录普通用户可理解的中文错误。
- 目标同名文件不覆盖，自动生成 `_2`、`_3` 等安全文件名。
- 取消或异常时只删除本次确实创建的残缺输出，不会删除原有同名目标文件或任何源文件。

### 4.5 报告、设置和日志

- 复制后生成 `匹配报告.csv`、`匹配报告.json`、`操作日志.txt`。
- CSV 为 UTF-8 with BOM。
- 设置保存于 `%LocalAppData%\RAWSelectionAssistant\settings.json`。
- RAW 索引、缓存和日志均位于 `%LocalAppData%\RAWSelectionAssistant\`，不写入 Program Files。
- 损坏的设置或索引缓存会被安全忽略并恢复默认状态。
- 普通界面只显示简洁中文错误；完整异常和堆栈只写入 `Logs`。

### 4.6 Windows 桌面产品形态

- 主项目明确使用 `<OutputType>WinExe</OutputType>` 和 `<UseWPF>true</UseWPF>`。
- PE 检查结果：Machine `0x8664`（x64），Subsystem `2`（Windows GUI）。
- 自包含 `win-x64` 发布，用户无需安装 .NET Runtime。
- 单实例运行；重复启动激活现有主窗口，不创建第二个完整进程。
- 主进程没有 TCP 监听、localhost、HTTP 服务、后台服务器或子进程。
- 正常启动没有 CMD、PowerShell、Terminal、Python、Node、dotnet 控制台或浏览器。
- 正式多尺寸图标占位已接入 EXE、主窗口、任务栏、安装包、桌面和开始菜单快捷方式。
- “开始匹配”在 RAW 索引为 0 时禁用，并新增明确提示：先点击左侧“扫描 / 重新建立 RAW 索引”；禁用按钮悬停也会显示原因。

### 4.7 安装包

- 单文件 Inno Setup 图形安装向导。
- 默认安装到 `C:\Program Files\RAW归片助手\`。
- 按计算机安装，安装时请求管理员权限，日常运行不请求管理员权限。
- 创建开始菜单入口，默认创建公共桌面快捷方式。
- 快捷方式目标直接是安装目录中的 `RAWSelectionAssistant.exe`。
- 可从 Windows“设置 → 应用”卸载。
- 默认卸载保留用户设置、索引和日志；卸载窗口提供“同时删除用户设置、索引和历史日志”可选框。
- 不安装服务、不修改 PATH、不安装插件、不添加右键菜单或开机启动项。
- 限制 Windows x64，并带有 32 位系统中文拦截提示。

## 5. 第一版未实现的功能

按原需求明确不实现：

- OCR、AI 图像识别、视觉相似度匹配。
- RAW 内容解码和缩略图预览。
- 云端服务、登录、联网更新。
- 自动监听微信文件夹或 FileSystemWatcher 实时索引。
- 自动移动、删除或修改源文件。

另外，以下设置数据结构已经预留，但 MVP 未提供独立设置页：

- 自定义 RAW 扩展名编辑界面。
- “复制后自动打开输出目录”开关界面。

## 6. 编译、测试和验收结果

### 6.1 Clean / Release Build

- Clean：通过。
- Release x64 Build：通过。
- 最终编译：0 警告、0 错误。

### 6.2 自动化测试

- 总数：29。
- 通过：29。
- 失败：0。
- 跳过：0。
- 最终 Release 测试耗时约 0.1 秒。

覆盖内容包括完整名称匹配、数字匹配、前导零、大小写、编号副本、中文副本、冲突、未找到、重复去重、中文路径、TXT/CSV 分隔符、扫描取消、无权限目录继续、同名不覆盖、复制取消清理，以及需求中的完整验收场景。

完整验收场景结果：

- `DSC01234.JPG` → `DSC01234.ARW`：已匹配。
- `1235` → `DSC01235.ARW`：已匹配。
- `1236` → 两个候选：存在冲突；未确认前复制被拒绝。
- 手动确认 `1236` 后：允许复制。
- `IMG_3288.JPG` → `IMG_3288.CR3`：已匹配。
- `9999`：未找到。
- 最终复制 4 个 RAW，并生成三种报告。

### 6.3 主程序运行验收

- 正式自包含 EXE 成功显示“RAW 归片助手”主窗口。
- 可访问性检查确认主要区域和按钮存在：RAW 来源、客户选片、输出设置、开始匹配、复制、导出、打开输出和状态区。
- 重复启动后仍只有 1 个进程、1 个主窗口。
- 主程序进程没有 TCP 连接或监听端口，没有子进程。
- 发现并修复了首次运行时 `ProgressBar.Value` 的只读属性绑定问题。
- 根据实际用户反馈，已为“开始匹配”禁用状态补充“请先扫描 RAW 索引”的明显提示和悬停说明。

### 6.4 安装和卸载验收

为避免覆盖开发电脑上已有的用户安装版，使用同一 Inno 脚本、相同发布文件但独立 AppId/名称的临时验收构建执行测试。临时验收构建测试结束后已删除，正式安装包不受影响。

- 图形安装向导出现：通过，标题为“安装 - RAW 归片助手 1.0.0”。
- 安装到 Program Files：通过。
- 主 EXE 存在：通过。
- 开始菜单快捷方式：通过。
- 公共桌面快捷方式：通过。
- 桌面快捷方式直接目标：`C:\Program Files\RAW归片助手_验收测试\RAWSelectionAssistant.exe`，通过。
- Windows 卸载项：通过。
- 静默卸载退出码：0。
- 卸载后程序目录删除：通过。
- 卸载后桌面和开始菜单快捷方式删除：通过。
- 卸载项删除：通过。
- 默认保留 `%LocalAppData%\RAWSelectionAssistant` 用户数据：通过。
- 未残留临时验收安装、服务或开机启动项。

## 7. 最终安装包信息

- 完整路径：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\RAW归片助手_Setup_1.0.0_x64.exe`
- 文件名：`RAW归片助手_Setup_1.0.0_x64.exe`
- 文件大小：48,360,973 字节（约 46.12 MiB）
- SHA-256：`2A2B95A0916D11B1654980F107AAB01DAE5AA381D57FF5EB513D48B87C5A65A0`
- 发布目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\publish\win-x64`
- 发布目录大小：约 156.10 MiB
- 发布文件数量：258
- PDB：0
- 是否 self-contained：是。
- 是否 WinExe：是。
- 是否 Windows GUI 子系统：是，Subsystem 2。
- 是否 x64：是，Machine 0x8664。
- 是否启动时无控制台窗口：是，WinExe/GUI 子系统并已做进程与窗口检查。
- 是否有 localhost 服务：否。
- 是否有浏览器界面：否。
- 桌面快捷方式是否直接指向主程序 EXE：是。

## 8. 启动方式

普通用户：

1. 双击 `RAW归片助手_Setup_1.0.0_x64.exe`。
2. 按图形安装向导完成安装。
3. 双击桌面的“RAW 归片助手”，或从开始菜单打开。

普通用户不需要、也不应该运行任何 `.ps1`、`.bat`、`dotnet`、Python 或 Node 命令。

开发人员可以使用 `build_debug.ps1`、`build_release.ps1` 和 `build_installer.ps1`。`run_app.ps1` 仅供开发调试，不是正式产品启动方式。

## 9. Windows 兼容性说明

### Windows 10

- 实机：Windows 10 Pro 22H2 x64，版本 10.0.19045。
- Clean、Release、测试、发布、GUI 启动、单实例、安装和卸载均在该系统完成。
- 安装脚本最低版本为 10.0.19045。

### Windows 11

- 当前开发电脑没有可用 Windows 11 实机或虚拟机，因此没有声称完成 Windows 11 实机测试。
- 已完成目标框架和 API 审查：使用 WPF 和 Windows 10 19041 可用 API，不调用 Windows 11 独占 API。
- 界面不依赖云母、亚克力或 Windows 11 圆角效果。
- 自包含 win-x64 包从目标框架上兼容 Windows 11 x64；正式发布前仍建议在 Windows 11 23H2/24H2 x64 做一次安装和功能回归。

## 10. 已知问题与风险

1. 安装包未做商业代码签名，Authenticode 状态为 `NotSigned`。Windows SmartScreen 在低下载量阶段可能显示“未知发布者”。建议正式发行前购买 EV/OV 代码签名证书并签名。
2. 未主动操作或绕过 Windows Defender/第三方杀毒软件，也未执行外部杀毒服务上传；验收期间没有观察到杀毒拦截或误报。不能据此保证所有杀毒产品都不会误报。
3. Windows 10 22H2 上，自动化工具的 Windows.Graphics.Capture 截图接口返回 `0x80004002`，因此界面验收改用 UI Automation 可访问性树、真实进程和窗口句柄完成。该问题属于测试工具截图能力，不影响 WPF 应用显示和操作。
4. Windows 11 尚未实机测试。
5. 自包含发布目录约 156 MiB，安装包约 46 MiB；这是完整包含 .NET/WPF 运行时以换取“用户无需安装运行库”的结果。
6. 为保护开发电脑上已有的用户安装版，没有覆盖或卸载该版本；安装/卸载测试使用独立 AppId 的等价隔离构建。

## 11. 下一阶段建议

1. 购买并接入代码签名证书，签名主 EXE 和安装包。
2. 在 Windows 11 23H2/24H2 x64 实机执行安装、拖放、网络路径、移动硬盘掉线和卸载回归。
3. 增加独立设置页，开放自定义 RAW 扩展名和“复制后自动打开目录”选项。
4. 增加大规模性能基准：10 万 RAW、1000 条客户选片、多磁盘并发场景。
5. 增加增量索引策略，在不引入实时 FileSystemWatcher 的前提下缩短重复扫描时间。
6. 增加 UI 自动化用例，覆盖目录选择、拖放、冲突窗口、复制和报告按钮全流程。

## 12. 所有新增和修改文件清单

本项目从空目录创建，以下均为新增或在生成模板后修改的文件。`bin`、`obj` 和完整自包含运行时文件不逐一列出。

### 根目录与构建

- `.gitignore`
- `Directory.Build.props`
- `RAWSelectionAssistant.sln`
- `README.md`
- `build_debug.ps1`
- `build_release.ps1`
- `build_installer.ps1`
- `run_app.ps1`
- `create_sample_environment.ps1`
- `回传给GPT_RAW归片助手_MVP报告.md`

### 安装包

- `installer/RAWSelectionAssistant.iss`
- `artifacts/installer/RAW归片助手_Setup_1.0.0_x64.exe`

### 核心项目

- `src/RAWSelectionAssistant.Core/RAWSelectionAssistant.Core.csproj`
- `src/RAWSelectionAssistant.Core/Models/AppSettings.cs`
- `src/RAWSelectionAssistant.Core/Models/CopyModels.cs`
- `src/RAWSelectionAssistant.Core/Models/MatchDecision.cs`
- `src/RAWSelectionAssistant.Core/Models/MatchStatus.cs`
- `src/RAWSelectionAssistant.Core/Models/NormalizedFileName.cs`
- `src/RAWSelectionAssistant.Core/Models/OperationProgress.cs`
- `src/RAWSelectionAssistant.Core/Models/OutputMode.cs`
- `src/RAWSelectionAssistant.Core/Models/RawFileEntry.cs`
- `src/RAWSelectionAssistant.Core/Models/RawIndexSnapshot.cs`
- `src/RAWSelectionAssistant.Core/Models/SelectionItem.cs`
- `src/RAWSelectionAssistant.Core/Models/SourceDirectoryEntry.cs`
- `src/RAWSelectionAssistant.Core/Services/FileLogService.cs`
- `src/RAWSelectionAssistant.Core/Services/FileNameNormalizer.cs`
- `src/RAWSelectionAssistant.Core/Services/ILogService.cs`
- `src/RAWSelectionAssistant.Core/Services/InputParserService.cs`
- `src/RAWSelectionAssistant.Core/Services/IRawFileSystem.cs`
- `src/RAWSelectionAssistant.Core/Services/RawCopyService.cs`
- `src/RAWSelectionAssistant.Core/Services/RawIndexService.cs`
- `src/RAWSelectionAssistant.Core/Services/RawMatchService.cs`
- `src/RAWSelectionAssistant.Core/Services/ReportService.cs`
- `src/RAWSelectionAssistant.Core/Services/SettingsService.cs`
- `src/RAWSelectionAssistant.Core/Utilities/AppDataPaths.cs`
- `src/RAWSelectionAssistant.Core/Utilities/ObservableObject.cs`

### WPF 主程序

- `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`
- `src/RAWSelectionAssistant/app.manifest`
- `src/RAWSelectionAssistant/App.xaml`
- `src/RAWSelectionAssistant/App.xaml.cs`
- `src/RAWSelectionAssistant/AssemblyInfo.cs`
- `src/RAWSelectionAssistant/GlobalUsings.cs`
- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/MainWindow.xaml.cs`
- `src/RAWSelectionAssistant/Assets/AppIcon.ico`
- `src/RAWSelectionAssistant/Assets/AppIcon.png`
- `src/RAWSelectionAssistant/Assets/AppIcon.svg`
- `src/RAWSelectionAssistant/Converters/StatusConverters.cs`
- `src/RAWSelectionAssistant/Resources/Styles.xaml`
- `src/RAWSelectionAssistant/Services/IDialogService.cs`
- `src/RAWSelectionAssistant/Services/WpfDialogService.cs`
- `src/RAWSelectionAssistant/Utilities/RelayCommand.cs`
- `src/RAWSelectionAssistant/Utilities/SingleInstanceManager.cs`
- `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`
- `src/RAWSelectionAssistant/Views/CandidateSelectionWindow.xaml`
- `src/RAWSelectionAssistant/Views/CandidateSelectionWindow.xaml.cs`

### 测试项目

- `tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj`
- `tests/RAWSelectionAssistant.Tests/MSTestSettings.cs`
- `tests/RAWSelectionAssistant.Tests/TestSupport.cs`
- `tests/RAWSelectionAssistant.Tests/FileNameNormalizerTests.cs`
- `tests/RAWSelectionAssistant.Tests/InputParserTests.cs`
- `tests/RAWSelectionAssistant.Tests/RawIndexServiceTests.cs`
- `tests/RAWSelectionAssistant.Tests/RawMatchServiceTests.cs`
- `tests/RAWSelectionAssistant.Tests/RawCopyServiceTests.cs`
- `tests/RAWSelectionAssistant.Tests/AcceptanceFlowTests.cs`

## 13. 安装包相关新增文件清单

- `installer/RAWSelectionAssistant.iss`：正式与隔离验收安装配置。
- `build_installer.ps1`：开发阶段发布和安装包构建脚本。
- `build_release.ps1`：Clean、Release、测试和干净自包含 publish。
- `src/RAWSelectionAssistant/Assets/AppIcon.ico`：EXE/窗口/快捷方式/安装包图标。
- `src/RAWSelectionAssistant/app.manifest`：asInvoker、DPI、长路径、Windows 兼容清单。
- `artifacts/publish/win-x64/`：正式自包含运行目录。
- `artifacts/installer/RAW归片助手_Setup_1.0.0_x64.exe`：最终普通用户安装文件。

## 14. 回传给 ChatGPT 时建议提供的文件

最小回传：

1. `RAW归片助手_Setup_1.0.0_x64.exe` — Windows x64 安装程序，二进制 EXE。
2. `回传给GPT_RAW归片助手_MVP报告.md` — 验收和实现报告，Markdown。

源码审查回传：

3. `RAWSelectionAssistant.sln` — Visual Studio 解决方案。
4. `README.md` — 用户和开发说明，Markdown。
5. `src/` — C#、XAML、ICO/PNG/SVG 和项目文件。
6. `tests/` — C# 自动化测试和测试项目。
7. `installer/RAWSelectionAssistant.iss` — Inno Setup 脚本。
8. `build_debug.ps1`、`build_release.ps1`、`build_installer.ps1` — 开发构建脚本。
9. `create_sample_environment.ps1` — 示例验收目录生成脚本。

普通用户不需要源码、测试、发布目录或脚本，只需要安装包 EXE。

---

## 15. 1.1.0 通用归片功能更新

### 15.1 实现方式与 RAW 专用服务迁移

- 保留原有 `RawIndexService`、`RawMatchService`、`RawCopyService` 和对应模型/测试，避免破坏 1.0.x 既有行为。
- 新增 `MediaIndexService`、`MediaMatchService`、`MediaCopyService`、`MediaReportService`，WPF 主流程已切换到通用服务。
- 一个 `MediaSelectionItem` 包含多个 `MediaFormatMatchResult`，JPG、RAW 和每个自定义扩展名分别保存候选、最终文件、匹配状态、复制状态和错误信息。
- 客户 JPG 的原始路径由输入解析器保留；默认仅作选片依据，启用回退后才可作为最终文件，并标记 `IsCustomerProvided`。

### 15.2 新索引结构

`MediaIndexSnapshot` 使用 `MediaFileRecord`，记录文件名、基础名、标准化名称、数字编号、扩展名、文件类别、完整路径、来源目录、大小和修改时间。索引提供：

- 标准化名称 + 扩展名；
- 数字编号 + 扩展名；
- 标准化名称全部格式；
- 数字编号全部格式；
- 完整路径；
- 来源目录。

索引支持多个来源根目录，JPG 和 RAW 无需位于同一目录或同一磁盘。

### 15.3 支持的归片类别

- 仅 JPG；
- 仅 RAW；
- JPG + RAW（默认）；
- 自定义格式。

自定义扩展名支持自动补点、统一大写、去重、非法字符校验和设置持久化。类别切换会保留客户选片并重新计算结果。

### 15.4 匹配、冲突、复制和报告

- JPG 和 RAW 完整匹配、仅 JPG、仅 RAW、跨来源根目录匹配均通过。
- 仅找到一种格式时返回部分匹配并写明缺失格式。
- JPG 冲突与 RAW 冲突相互独立；未冲突且已匹配的格式仍可复制。
- 复制按完整源路径去重，并支持同目录、按 `JPG/RAW/OTHER` 分类、保留相对目录三种模式。
- CSV、JSON、日志按目标格式输出记录，区分客户 JPG 与来源目录 JPG，并包含归片类别、目标扩展名、各格式状态、部分匹配原因和最终路径。

### 15.5 设置兼容

`settings.json` 新增默认归片类别、JPG 扩展名、RAW 扩展名、自定义扩展名、分类输出模式和客户 JPG 回退设置。旧设置缺少这些字段时使用默认值，旧版 RAW 自定义扩展名会并入新版 RAW 扩展名列表。兼容测试通过。

### 15.6 自动化测试结果

- 新增 20 项通用归片专项测试。
- 测试总数：49。
- 结果：49 通过，0 失败，0 跳过。
- 覆盖仅 JPG、仅 RAW、JPG + RAW、自定义 XMP、大小写、部分匹配、独立冲突、客户 JPG 回退、跨根目录、复制去重、分类输出、报告区分和旧设置升级。

### 15.7 新增和修改文件

新增：

- `src/RAWSelectionAssistant.Core/Models/MediaEnums.cs`
- `src/RAWSelectionAssistant.Core/Models/MediaFileRecord.cs`
- `src/RAWSelectionAssistant.Core/Models/MediaIndexSnapshot.cs`
- `src/RAWSelectionAssistant.Core/Models/MediaMatchModels.cs`
- `src/RAWSelectionAssistant.Core/Models/MediaCopyModels.cs`
- `src/RAWSelectionAssistant.Core/Models/ParsedSelectionInput.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaExtensionPolicy.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaIndexService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaMatchService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaCopyService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaReportService.cs`
- `src/RAWSelectionAssistant/Views/MediaDetailsWindow.xaml`
- `src/RAWSelectionAssistant/Views/MediaDetailsWindow.xaml.cs`
- `tests/RAWSelectionAssistant.Tests/MediaMatchServiceTests.cs`
- `tests/RAWSelectionAssistant.Tests/MediaCopyReportSettingsTests.cs`

修改：

- `Directory.Build.props`
- `README.md`
- `installer/RAWSelectionAssistant.iss`
- `src/RAWSelectionAssistant.Core/Models/AppSettings.cs`
- `src/RAWSelectionAssistant.Core/Models/OutputMode.cs`
- `src/RAWSelectionAssistant.Core/Services/InputParserService.cs`
- `src/RAWSelectionAssistant.Core/Services/SettingsService.cs`
- `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`
- `src/RAWSelectionAssistant/App.xaml.cs`
- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/Converters/StatusConverters.cs`
- `src/RAWSelectionAssistant/Services/IDialogService.cs`
- `src/RAWSelectionAssistant/Services/WpfDialogService.cs`
- `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`
- `回传给GPT_RAW归片助手_MVP报告.md`

### 15.8 版本、安装包、未完成项和已知问题

- 版本：1.1.0。
- 正式安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\RAW归片助手_Setup_1.1.0_x64.exe`。
- 未完成项目：无。
- 已知问题：无阻断性已知问题。客户 JPG 回退只能用于实际拖入并仍存在于本机的 JPG 文件；纯文本编号不携带客户文件路径，符合产品规则。

---

## 16. 1.2.0 JPG 原图识别与客户压缩图防混入更新

### 16.1 JPG 质量核验指标

新增 `JpegQualityInfo`、`JpegMetadataService` 和 `JpegQualityAssessmentService`。软件读取并展示文件大小、像素宽高、总像素、EXIF 状态、相机品牌和型号、原始拍摄时间、ICC 色彩配置、软件处理标记和方向；同时结合文件名是否变化、标准化编号、来源目录、来源优先级以及来源文件与客户文件的差异生成风险提示。

元数据读取失败、文件损坏、零字节、文件被占用或字段缺失时均返回“未知”或可读错误，不会使程序崩溃。质量评估只给出风险和推荐理由，不自动宣称文件为百分之百原图。

### 16.2 为什么不只依赖文件大小

JPEG 文件大小同时受像素尺寸、画面细节、噪点、压缩质量和重复编码影响，因此不存在可靠的固定 MB 阈值。1.2.0 只把大小作为辅助风险信号和候选排序的最后一项；来源类型、完整标准化文件名、来源目录优先级、像素尺寸、EXIF 完整度以及相机和拍摄时间一致性全部排在文件大小之前。客户文件即使更大，也不能覆盖来源目录优先规则。

### 16.3 三种客户 JPG 处理模式

- 严格模式（默认）：只有来源目录 JPG 可自动归片；客户 JPG 只用于识别编号，不进入复制队列。
- 智能备用模式：来源 JPG 未找到时展示客户 JPG 的完整质量信息，状态为“客户 JPG 等待确认”；用户必须在明细中手动确认后才能复制。
- 允许客户文件模式：来源 JPG 未找到时允许采用客户 JPG，但强制标记“使用客户返回 JPG”和“原始质量未经确认”，该模式不是默认值。

设置升级会把旧版“允许客户 JPG 回退”布尔值迁移到新模式，同时保留旧字段，确保已有 `settings.json` 继续可用。

### 16.4 来源追踪、目录类型和候选规则

JPG 文件记录新增来源目录、客户返回、用户手动指定三种来源类型。来源目录新增 JPG、RAW、JPG + RAW 混合和其他格式四种用途，并支持调整优先级。多个来源 JPG 的推荐顺序为：来源目录、完整标准化名称、目录优先级、像素尺寸、EXIF 完整度、项目相机和拍摄时间一致性、文件大小、客户返回文件。

多个来源目录 JPG 仍无法确定时状态保持“存在冲突”，不会自动选择最大文件。来源与客户 JPG 同时存在时，明细展示大小、像素、EXIF、相机、时间、文件名和标准化编号差异，并说明优先采用来源目录文件的理由。

CSV 和 JSON 在保留旧字段的基础上新增 `JpgSourceType`、`JpgFileSizeBytes`、`JpgPixelWidth`、`JpgPixelHeight`、`JpgHasExif`、`JpgCameraMake`、`JpgCameraModel`、`JpgDateTimeOriginal`、`JpgHasIccProfile`、`JpgSoftwareTag`、`JpgQualityWarnings`、`UsedCustomerReturnedJpg`、`CustomerJpgManualConfirmation` 和 `RecommendedCandidateReason`，可追溯最终复制来源。

### 16.5 自动化测试结果

- 新增 17 项 JPG 原图保护专项测试。
- 测试总数：66。
- 结果：66 通过，0 失败，0 跳过。
- 覆盖来源优先、客户文件更大不能覆盖、像素和 EXIF 对比、三种模式、手动确认、报告标记、非固定大小阈值、来源冲突、损坏元数据、零字节、文件占用、中文路径、JPG + RAW 联合归片、客户图片仅作编号输入和目录用途过滤。

### 16.6 新增和主要修改文件

新增：

- `src/RAWSelectionAssistant.Core/Models/JpegQualityModels.cs`
- `src/RAWSelectionAssistant.Core/Services/JpegMetadataService.cs`
- `src/RAWSelectionAssistant.Core/Services/JpegQualityAssessmentService.cs`
- `tests/RAWSelectionAssistant.Tests/JpegOriginalProtectionTests.cs`

主要修改：

- `src/RAWSelectionAssistant.Core/Models/AppSettings.cs`
- `src/RAWSelectionAssistant.Core/Models/MediaFileRecord.cs`
- `src/RAWSelectionAssistant.Core/Models/MediaMatchModels.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaIndexService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaMatchService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaReportService.cs`
- `src/RAWSelectionAssistant.Core/Services/SettingsService.cs`
- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`
- `src/RAWSelectionAssistant/Views/MediaDetailsWindow.xaml`
- `src/RAWSelectionAssistant/Views/MediaDetailsWindow.xaml.cs`
- `installer/RAWSelectionAssistant.iss`

### 16.7 版本和交付

- 版本：1.2.0。
- 发布形态：Windows x64、self-contained、WPF WinExe、无控制台窗口。
- 正式安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\RAW归片助手_Setup_1.2.0_x64.exe`。
- 安装包大小：48,635,871 字节（46.38 MB）。
- SHA-256：`5F2260DDAE0854744B0380B025C987C674CCA6D66AD410E665875FAEE92B23DD`。
- Release 编译：0 警告，0 错误；全部 66 项测试通过。
- 安装验收：隔离测试包安装成功，安装后 EXE 产品版本为 1.2.0；真实主界面成功创建，界面树确认存在“照片来源目录”“来源 JPG 未找到时”和客户 JPG 仅作编号输入说明，日志记录“应用程序已启动”。
- 运行验收：在 150% DPI 和旧窗口坐标设置下，窗口会自动修正至当前显示器工作区内；程序正常关闭时不再发生设置保存死锁，也不留下 `settings.json.tmp`。
- 卸载验收：程序保持运行时启动卸载，卸载器可正常关闭程序；进程、隔离安装目录和桌面快捷方式均已清理。
- 界面验收截图：`D:\AI AGENT\RAWSelectionAssistant\artifacts\acceptance-main-window-1.2.0.png`。
- 已知限制：软件能检查尺寸、大小和元数据，但无法仅凭这些信息百分之百证明 JPG 未被压缩或重编码；因此智能备用模式始终要求人工确认。
