# 北尾鸲归片助手 1.2.0 回传报告

生成日期：2026-07-29  
验收系统：Microsoft Windows 10 Pro x64，10.0.19045

## 1. 最终交付结论

- 软件正式名称：北尾鸲归片助手
- 英文内部产品名称：KitaoPhotoSelector
- 版本号：1.2.0
- 主程序：KitaoPhotoSelector.exe
- 产品形态：WPF Windows 桌面 WinExe，无控制台、无浏览器、无 localhost、无后台服务器
- 发布方式：win-x64 self-contained，用户无需另装 .NET
- 安装方式：Inno Setup 7 图形安装包
- 实现状态：品牌修改、21 步强制交互式教程、沙盒、恢复、旧用户兼容和交付均已完成

## 2. 正式安装包

- 完整路径：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\北尾鸲归片助手_Setup_1.2.0_x64.exe`
- 文件大小：48,656,878 字节（46.40 MiB）
- SHA-256：`239FC2A1840B0082CA77D2036E49279D0F15DA45603E7D194C6AC160BD8EEE8C`
- 默认安装目录：`C:\Program Files\北尾鸲归片助手\`
- 桌面快捷方式：北尾鸲归片助手
- 开始菜单入口：北尾鸲归片助手
- Windows 应用列表名称：北尾鸲归片助手
- 安装完成选项：运行北尾鸲归片助手

发布目录包含 260 个文件、164,836,608 字节，包含 .NET 10 Windows x64 运行环境。

## 3. 版本与品牌验证

主程序文件属性实测：

- ProductName：北尾鸲归片助手
- FileDescription：北尾鸲归片助手
- CompanyName：北尾鸲归片助手
- FileVersion：1.2.0.0
- ProductVersion：1.2.0

已统一修改的位置：主窗口标题、顶部品牌、欢迎与完成页、全部教程文案、帮助窗口、旧用户教程提示、消息框标题、任务栏窗口标题、文件说明、程序集产品信息、安装向导、安装目录、桌面和开始菜单快捷方式、应用列表、卸载项、安装完成启动项、报告字段、操作日志、README、安装包文件名。

源文件用户可见文本扫描未发现 `RAW 归片助手` 或 `RAW归片助手`。旧英文名称只保留在内部命名空间、源代码目录、解决方案名和旧配置兼容路径中，不显示在普通界面。

## 4. 教程架构

教程逻辑未堆积在窗口代码中，主要职责如下：

- `OnboardingService`：21 步状态机、动作门控、完成条件、进度保存、完成凭证和回放模式。
- `TutorialDataService`：创建、验证、重置和删除 Tutorial 沙盒。
- `TutorialSpotlightLayoutService`：按窗口和控件尺寸计算遮罩、高亮、指示线和提示卡位置。
- `ExistingUserDetectionService`：区分新安装、教程中断和旧用户升级。
- `AppDataMigrationService`：只复制旧设置、索引和必要日志，不移动、不覆盖、不删除旧数据。
- `TutorialStep`：步骤编号、标题、说明、动作、目标、返回权限、演示标记、错误、完成条件、下一步。
- `TutorialState`：Required、Replay、Inactive 模式及当前步骤、选项访问记录和错误状态。
- `TutorialAction`、`TutorialTarget`、`TutorialValidationResult`：动作、目标控件和校验结果模型。

主窗口只负责 WPF 控件定位和视觉呈现。业务动作仍调用正式的编号解析、索引、匹配、复制和报告服务。

## 5. 教程界面与操作限制

- 主界面非目标区域由四块半透明遮罩锁定。
- 当前控件保留可点击区域，并显示绿色边框、阴影和指示线。
- 提示卡按目标左右可用空间自动摆放，使用实际高度防止错误信息越界。
- 窗口调整大小和滚动后自动重算位置，并调用 BringIntoView 定位目标。
- 支持 Tab 聚焦当前动作及 Enter 执行按钮。
- 当前步骤以外的业务命令由动作门控拒绝。
- 强制教程只有“退出教程”，没有“跳过教程”或通用“下一步”。
- 强制教程退出只关闭软件并保存进度，不写完成状态。
- 教程路径之外的拖入、输出和删除操作均被拒绝。

## 6. 21 步流程与完成条件

| 步骤 | 标题 | 必须执行的动作 | 完成条件 |
|---:|---|---|---|
| 1 | 欢迎使用北尾鸲归片助手 | 开始教程 | 用户点击开始教程 |
| 2 | 认识照片来源目录 | 添加照片来源目录 | Tutorial\Source 成功加入且目录存在 |
| 3 | 安全移除搜索目录 | 删除来源目录 | 显示只移除记录、不删除硬盘照片的说明，教程目录保留 |
| 4 | 选择归片类别 | 依次选择四类并回到 JPG + RAW | 四个选项均访问，最终值为 JPG + RAW |
| 5 | 扫描照片文件 | 扫描照片文件 | 真实索引包含 3 个 JPG 和 3 个 RAW |
| 6 | 安全取消耗时任务 | 取消当前任务 | 取消模拟任务，已建教程索引保持不变 |
| 7 | 加载客户选片 | 加载教程选片 | 通过正式拖放解析流程加入 DSC01234 客户依据 JPG |
| 8 | 粘贴编号 | 粘贴编号 | 写入 `1235、DSC01236.JPG` |
| 9 | 解析编号 | 解析编号 | 三条演示编号存在，保留原始输入并标记重复项 |
| 10 | 了解清空选片 | 清空选片 | 清空内存列表后自动恢复三条演示记录，不删除文件 |
| 11 | 开始匹配 | 开始匹配 | 三组编号均完整匹配 JPG + ARW |
| 12 | 查看匹配详情 | 查看第一条明细并关闭 | 显示路径、来源、尺寸、EXIF、质量风险和推荐理由 |
| 13 | 理解 JPG 质量判断 | 我知道了 | 用户确认理解来源目录优先及质量指标仅作辅助 |
| 14 | 选择输出目录 | 选择输出目录 | 输出固定在 Tutorial\Output 内 |
| 15 | 输入项目名称 | 输入教程示例项目 | 项目名精确为 `教程示例项目` |
| 16 | 选择输出分类 | 依次选择三种方式并回到按文件类别 | 三个选项均访问，最终按 JPG/RAW 分类 |
| 17 | 复制已匹配文件 | 复制已匹配文件 | 真实复制 3 个 JPG、3 个 RAW，不覆盖源文件 |
| 18 | 导出匹配报告 | 导出匹配报告 | CSV、JSON、操作日志三个文件存在 |
| 19 | 打开输出文件夹 | 用户主动打开输出目录 | 资源管理器启动成功，否则显示中文错误并允许重试 |
| 20 | 清空当前任务 | 清空当前任务 | 内存任务清空，已复制文件和报告仍保留 |
| 21 | 你已经完成第一次归片 | 开始使用北尾鸲归片助手 | 最后一次点击后才写完成状态、解除锁定并清空演示任务 |

## 7. Tutorial 演示目录

```text
%LocalAppData%\KitaoPhotoSelector\Tutorial\
├─ Source\
│  ├─ JPG\
│  │  ├─ DSC01234.JPG
│  │  ├─ DSC01235.JPG
│  │  └─ DSC01236.JPG
│  └─ RAW\
│     ├─ DSC01234.ARW
│     ├─ DSC01235.ARW
│     └─ DSC01236.ARW
├─ CustomerSelection\
│  ├─ DSC01234.JPG
│  └─ 选片编号.txt
└─ Output\
```

JPG 是可以正常读取尺寸的有效小型 JPEG。RAW 是非空安全占位内容，只参与扩展名索引、匹配和复制，不进行 RAW 解码。教程复制、CSV、JSON 和操作日志全部位于 Tutorial 目录。

## 8. 状态保存与防绕过

设置文件包含小驼峰字段：

```json
{
  "onboardingCompleted": false,
  "onboardingVersion": "1.2.0",
  "onboardingCompletedAt": null,
  "onboardingCurrentStep": 1
}
```

每个有效步骤完成后原子保存当前步骤。只有第 21 步调用完成逻辑，写入完成时间和 SHA-256 完成凭证。手工把 `onboardingCompleted` 改为 true 但没有有效凭证时，程序会恢复强制教程，不会直接进入普通模式。

第 6 步重启时会恢复可取消的模拟任务；第 9 步会恢复待解析文本；第 18 步以后使用已经保存的 Tutorial 输出目录而不重复复制；第 21 步恢复时保持任务已清空状态。

## 9. 已完成用户与回放

有效完成凭证存在时，后续启动直接进入主界面。帮助窗口提供：

- 重新查看完整教程
- 重置教程演示数据
- 删除教程演示数据

回放前保存当前来源、选片记录、索引、匹配结果、项目和输出设置。退出或完成回放后恢复原工作区；回放期间只保存教程状态，不覆盖正常业务设置，也不修改首次完成时间。

## 10. 教程安全

- 删除只接受应用数据根下精确的 `Tutorial` 根路径。
- 外部路径、父目录、兄弟目录和任意用户照片路径均拒绝。
- 重置前执行同样的路径验证。
- 添加、删除来源、选择输出、拖入和打开目录都限制在 Tutorial 内。
- 清空操作只清理内存任务，不删除源文件或输出文件。
- 复制使用现有不覆盖策略；源 JPG 和 RAW 在复制后仍存在。

## 11. 新旧用户识别与迁移

新用户没有旧设置、旧索引、旧日志、来源目录或历史报告，必须进入 Required 教程。

旧用户检测到任一旧使用信号时，不进入强制锁定；首次启动显示：

- 立即体验教程
- 稍后在帮助中查看

迁移路径：

```text
%LocalAppData%\RAWSelectionAssistant\
    -> %LocalAppData%\KitaoPhotoSelector\
```

实测旧设置和旧索引被复制，旧目录仍存在，目标较新文件不会被覆盖，并生成 `Logs\migration.log`。若复制失败，启动代码在目标文件缺失时回读旧设置和旧综合索引。

教程自身生成的日志和索引不会被误判为旧用户历史；这是安装中断实测中发现并修复的边界条件。

## 12. 编译和自动化测试

- Clean：通过
- Release Build：通过，0 警告，0 错误
- 全部自动化测试：96/96 通过，0 失败，0 跳过
- 教程专项测试方法：30 个，超过至少 25 个场景的要求
- win-x64 self-contained publish：通过
- Inno Setup 7 正式安装包编译：通过

旧功能回归覆盖 JPG、RAW、JPG + RAW、自定义格式、编号解析、索引、冲突、复制不覆盖、CSV/JSON 报告、JPG 来源优先、三种客户 JPG 模式、损坏元数据、中文路径和单实例逻辑。

教程专项覆盖新安装锁定、错误动作不能越步、步骤进度持久化、异常重启恢复、只有最后一步完成、完成凭证防篡改、21 步顺序、四种归片类别、三种输出模式、真实 JPG/RAW 索引匹配复制报告、Tutorial 删除边界、旧用户提示、回放时间不变、低驼峰 JSON 字段、报告品牌字段和 125%/150%/175% DPI 布局。

## 13. 安装、启动和卸载实测

- 正式安装包静默验收安装：退出码 0。
- 安装目录：`C:\Program Files\北尾鸲归片助手\`，通过。
- 主程序直接启动：窗口标题为北尾鸲归片助手，响应正常。
- 全新安装：自动显示“第 1 步，共 21 步”，普通业务按钮锁定，通过。
- 未完成后重启：仍恢复强制教程，不出现旧用户提示，通过。
- 第 6 步恢复：取消当前任务按钮可用，通过。
- 第 9 步恢复：待解析文本和解析编号按钮可用，通过。
- 第 21 步恢复：只允许最终完成动作，普通清空任务仍锁定，通过。
- 有效完成状态重启：不显示教程，帮助和添加来源按钮可用，通过。
- 旧用户升级：显示两个明确选项且普通主界面未锁定，通过。
- 单实例：第二次启动后进程数仍为 1，通过。
- 无控制台：主程序没有 conhost、cmd、PowerShell 子进程，通过。
- 卸载：退出码 0，主程序和桌面快捷方式删除，通过。
- 卸载默认保留用户数据，通过。
- 验收前用户应用数据已备份并在验收后原样恢复。

本机桌面输入注入接口返回 `GetCursorPos 访问被拒绝`，因此无法由自动化工具在安装程序窗口中完成整套 21 次物理点击。已通过无截图的 Windows 可访问性树读取真实安装程序状态，并通过 30 个教程专项测试及真实索引/匹配/复制/报告集成测试验证完整流程。该限制属于验收主机自动化权限，不是软件运行故障。

## 14. JPG + RAW 教程结果

真实服务集成测试建立 6 个文件索引，三组编号均完整匹配 JPG + ARW，复制结果为 6 个成功文件：

- `Output\教程示例项目_精选文件_时间\JPG\`：3 个 JPG
- `Output\教程示例项目_精选文件_时间\RAW\`：3 个 ARW

原始演示文件仍存在。报告生成 CSV、JSON、操作日志；CSV 前三个字节为 UTF-8 BOM `EF BB BF`。CSV 和 JSON 都包含软件名称和版本字段。

## 15. Windows 兼容性

- Windows 10 x64 19045：实际构建、安装、启动、恢复、迁移和卸载通过。
- Windows 11 x64：目标框架和安装条件兼容 Windows 10 19041 及以上，不使用 Windows 11 专属视觉 API；本次验收主机不是 Windows 11，未执行实体 Windows 11 安装。
- DPI：应用声明 PerMonitorV2；125%、150%、175% 的聚光灯布局边界自动化测试通过。

## 16. 新增和修改文件

主要新增文件：

- `src\RAWSelectionAssistant.Core\Models\Branding.cs`
- `src\RAWSelectionAssistant.Core\Models\OnboardingModels.cs`
- `src\RAWSelectionAssistant.Core\Services\AppDataMigrationService.cs`
- `src\RAWSelectionAssistant.Core\Services\ExistingUserDetectionService.cs`
- `src\RAWSelectionAssistant.Core\Services\OnboardingService.cs`
- `src\RAWSelectionAssistant.Core\Services\TutorialDataService.cs`
- `src\RAWSelectionAssistant.Core\Services\TutorialSpotlightLayoutService.cs`
- `src\RAWSelectionAssistant\Services\IDialogService.cs`
- `src\RAWSelectionAssistant\Views\HelpWindow.xaml`
- `src\RAWSelectionAssistant\Views\HelpWindow.xaml.cs`
- `src\RAWSelectionAssistant\Views\UpgradeTutorialWindow.xaml`
- `src\RAWSelectionAssistant\Views\UpgradeTutorialWindow.xaml.cs`
- `tests\RAWSelectionAssistant.Tests\OnboardingServiceTests.cs`
- `tests\RAWSelectionAssistant.Tests\OnboardingRequirementTests.cs`

主要修改文件：

- `Directory.Build.props`
- `README.md`
- `run_app.ps1`
- `installer\RAWSelectionAssistant.iss`
- `src\RAWSelectionAssistant.Core\Models\AppSettings.cs`
- `src\RAWSelectionAssistant.Core\Services\MediaReportService.cs`
- `src\RAWSelectionAssistant.Core\Services\ReportService.cs`
- `src\RAWSelectionAssistant.Core\Services\SettingsService.cs`
- `src\RAWSelectionAssistant.Core\Utilities\AppDataPaths.cs`
- `src\RAWSelectionAssistant\App.xaml.cs`
- `src\RAWSelectionAssistant\MainWindow.xaml`
- `src\RAWSelectionAssistant\MainWindow.xaml.cs`
- `src\RAWSelectionAssistant\RAWSelectionAssistant.csproj`
- `src\RAWSelectionAssistant\ViewModels\MainViewModel.cs`
- `src\RAWSelectionAssistant\Services\WpfDialogService.cs`
- `src\RAWSelectionAssistant\Utilities\SingleInstanceManager.cs`
- `src\RAWSelectionAssistant\app.manifest`

## 17. 已知问题与未完成功能

- 已知产品功能问题：无。
- 未完成功能：无。
- 验收环境限制：当前主机拒绝桌面自动化输入注入，完整物理点击回放未自动执行；语义状态读取和代码级真实业务流程均已通过。
- Windows 11 实机测试：未在本次 Windows 10 主机上执行。

## 18. 最终文件

1. 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\北尾鸲归片助手_Setup_1.2.0_x64.exe`
2. 回传报告：`D:\AI AGENT\RAWSelectionAssistant\回传给GPT_北尾鸲归片助手_1.2.0报告.md`
