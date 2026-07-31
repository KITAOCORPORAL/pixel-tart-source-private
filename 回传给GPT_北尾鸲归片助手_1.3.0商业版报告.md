# 北尾鸲归片助手 1.3.0 商业版回传报告

## 1. 交付结论

- 版本：1.3.0。
- 产品形态：同一个 WPF WinExe、同一个代码仓库、同一个安装包，同时支持免费版和专业版。
- 默认状态：未激活时进入免费版；生产授权配置缺失时仍可完成免费版归片，不崩溃、不启用任何专业功能。
- 专业版权限：授权抽象、Mock 测试提供器、Cryptolens 适配器、设备绑定、DPAPI 缓存、离线宽限、功能门控、停用和安全降级均已完成。
- 生产授权服务：未配置、未宣称上线。当前发布配置的 Provider 为 `None`。
- 正式安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\北尾鸲归片助手_Setup_1.3.0_x64.exe`。
- 安装包 SHA-256：`D28B051FCFA25015127003CF226257C333C7D9CA6302F726B99FE4B593C4F4E1`。

## 2. 新界面结构

主窗口已调整为“项目中心 + 四步工作流”。

左侧导航包含：

1. 项目中心；
2. 归片工作区；
3. 项目历史；
4. 授权与版本；
5. 设置；
6. 帮助。

项目中心包含：

- 新建归片项目；
- 继续最近项目；
- 最近项目卡片；
- 项目状态、选片数量、匹配数量、完成时间和摘要；
- 免费版 / 专业版状态；
- 显式的“升级专业版 / 查看授权”入口。

项目工作区顶部包含四步步骤条：

1. 来源与索引；
2. 导入选片；
3. 匹配检查；
4. 输出交付。

来源、客户选片和输出设置按当前步骤显示；匹配结果继续使用表格。缩略图仍只在冲突、JPG 质量对比和用户主动查看详情时出现。右上角始终显示“免费版 · 升级专业版”或“专业版 · 已激活”。受限功能只显示内部中文说明，不自动打开购买页面。

## 3. 免费版功能

免费版无需激活，可以正常完成一次完整归片：

- JPG 归片；
- RAW 归片；
- JPG + RAW 联合归片；
- 拖入客户 JPG；
- 粘贴编号；
- TXT、CSV 和文件夹输入；
- 完整文件名和数字编号匹配；
- 递归扫描；
- 基础冲突候选选择；
- 基础分类复制；
- 基础 CSV 报告；
- 22 步首次教程。

已新增真实的免费版 30 组 JPG + RAW 集成测试：建立索引、匹配 30 个唯一编号、复制 60 个文件并导出基础 CSV，测试通过。

## 4. 免费版限制

| 能力 | 免费版规则 | 失败后的数据安全 |
|---|---|---|
| 选片数量 | 每项目最多 30 个唯一编号；重复编号不重复计数 | 第 31 个新编号被拒绝，已有 30 个和重复输入保留 |
| 来源目录 | 最多 1 个 | 不删除既有目录；超限旧项目以安全只读方式打开 |
| 项目历史 | 显示最近 1 个 | 磁盘上的旧历史不删除，升级后立即恢复显示 |
| 自定义格式 | 不可用于新任务 | 旧项目配置保留，不清除数据 |
| 高速索引 | 当前会话可扫描匹配，不保存持久缓存 | 不影响当前扫描结果 |
| 高级 JPG | 只保留基础安全规则；客户 JPG 默认仍只作编号输入 | 不误复制客户压缩图 |
| 输出预设 | 不可保存 | 当前输出设置仍可直接使用 |
| 批量项目 | 不启动批处理 | 单项目仍可正常完成 |
| 报告 | 导出 CSV | 降级项目仍可导出现有结果 |

限制同时在界面层和服务层执行。`ProjectEntitlementService`、`MediaIndexService`、`MediaMatchService`、`OutputPresetService`、`BatchProjectService` 和 `MediaReportService` 均执行相应门控，不能只靠隐藏按钮绕过。

## 5. 专业版功能

专业版通过同一个 EXE 即时解锁：

- 不限选片编号；
- 多来源目录和多磁盘扫描；
- 持久化高速索引；
- 自定义扩展名；
- 完整 JPG 尺寸、EXIF、质量风险和来源对比；
- 高级冲突处理能力；
- 不限项目历史；
- CSV、JSON 和完整技术日志；
- 自定义输出目录规则；
- 输出预设；
- 顺序批量项目处理。

激活成功后 `LicenseChanged` 会立即刷新界面和业务门控，不要求重启，不清空当前项目，也不要求重新导入或重新扫描。

## 6. 授权服务架构

核心接口：

- `ILicenseService`：初始化、激活、验证、停用和当前授权状态；
- `ILicenseProvider`：隔离具体授权平台；
- `IFeatureGateService`：统一判断 `LicensedFeature`；
- `ILicenseStorageService`：本地签名凭证的安全存储；
- `IDeviceFingerprintService`：匿名设备指纹。

实现：

- `LicenseService`；
- `FeatureGateService`；
- `DpapiLicenseStorageService`；
- `DeviceFingerprintService`；
- `MockLicenseProvider`；
- `CryptolensLicenseProvider`；
- `UnavailableLicenseProvider`；
- `LicenseProviderFactory`。

正式应用调用 `LicenseProviderFactory` 时固定 `allowMockProvider: false`。即使有人把发布配置改成 `Provider=Mock`，正式客户端也只会返回不可用提供器，不会获得专业版权限。Mock 没有源码内置万能激活码，测试必须显式注入许可定义。

## 7. 设备指纹

设备指纹读取 Windows `MachineGuid`、机器名和系统架构，组合后使用 SHA-256 散列，只返回匿名哈希。授权请求不上传原始 MachineGuid。

授权请求模型只包含：

- 激活码；
- 匿名设备指纹；
- 软件版本；
- 产品 ID；
- 操作系统版本；
- 随机请求 ID。

不会上传用户照片、文件名、路径、客户编号、项目名、EXIF 或报告。默认设备上限为 1 台。

## 8. 激活与停用流程

“授权与版本”页显示：

- 当前版本和版本类型；
- 授权状态；
- 设备名称与设备占用；
- 激活时间；
- 最近验证时间；
- 离线到期时间与剩余天数；
- 激活码尾号；
- 生产授权配置状态；
- 激活、立即验证、停用本机和购买页面按钮。

激活码输入使用 `LicenseKeyFormatter`：自动大写、去空格、过滤非法字符、自动添加连字符，格式固定为 `KQGP-XXXXX-XXXXX-XXXXX`。日志不输出完整激活码，只记录末尾 4 位。

停用前显示确认。停用成功后只清除本地授权凭证并退回免费版；项目、索引、照片、输出和历史不删除。停用请求失败时保留现有有效凭证。Mock 测试确认停用后同一许可可以在模拟新设备激活。

## 9. 本地授权缓存与 DPAPI

授权数据存放于：

`%LocalAppData%\KitaoPhotoSelector\License\license.dat`

完整激活码和签名凭证只存在于 DPAPI `CurrentUser` 加密数据中，不写入 `settings.json`，也不存在可编辑的 `isPro=true` 字段。保存使用临时文件后原子替换。

启动时先使用授权提供器公钥或 Mock 测试签名验证本地凭证，再决定是否需要联网。本地文件损坏、篡改、设备不匹配或签名不正确时不会解锁专业版，也不会删除用户数据。

自动化测试确认：

- DPAPI 文件中不存在明文完整激活码；
- 随机篡改 DPAPI 文件后读取失败；
- 修改签名载荷后不能解锁；
- 日志只有激活码尾号。

## 10. 离线宽限规则

- 第一次激活必须联网；
- 每 7 天尝试一次在线验证；
- 在线验证成功后刷新离线凭证；
- 最长离线宽限 90 天；
- 网络错误与无效、过期、暂停、设备超限、签名错误分别处理；
- 单纯断网且凭证有效时进入 `OfflineGracePeriod`，继续使用专业版；
- 离线剩余时间在授权页显示；
- 离线期结束后安全退回免费版；
- 检测到时间回拨时要求在线复核；断网时只进入安全宽限，不删除凭证或用户数据。

## 11. 未配置生产授权时的行为

发布包中的 `appsettings.license.json` 为：

```json
{
  "Provider": "None",
  "ProductId": 0,
  "PublicKey": "",
  "PublicValidationToken": "",
  "PurchaseUrl": "",
  "OfflineGraceDays": 90,
  "ValidationIntervalDays": 7,
  "MaxDevices": 1
}
```

结果：

- 软件以免费版启动；
- 激活页显示“授权服务尚未配置”；
- 购买按钮不可用，不打开空网址；
- 不崩溃；
- 不启用 Mock；
- 不偷偷启用专业功能。

`appsettings.license.example.json` 只包含 Cryptolens 示例字段，不包含管理员密钥、私钥、产品管理令牌、支付密钥或万能激活码。

## 12. 自动化测试结果

最终 Release 测试：

- 全部测试：160/160 通过，0 失败，0 跳过；
- 商业版与授权专项：63/63 通过；
- 原有测试：全部继续通过。

需求中的 25 项授权测试对应情况：

| # | 验收项 | 结果 |
|---:|---|---|
| 1 | 未激活默认免费版 | 通过 |
| 2 | 免费版完成 30 张以内归片 | 通过，真实完成 30 JPG + 30 RAW |
| 3 | 第 31 个唯一编号限制 | 通过 |
| 4 | 重复编号不占新额度 | 通过 |
| 5 | 免费版只能添加 1 个来源 | 通过，界面服务和索引服务双层校验 |
| 6 | 专业版多个来源 | 通过 |
| 7 | 激活后无需重启 | 通过，动态 FeatureGate |
| 8 | 无效激活码不解锁 | 通过 |
| 9 | 激活失败不丢项目 | 通过 |
| 10 | 停用退回免费版 | 通过 |
| 11 | 停用不删除照片和项目 | 通过 |
| 12 | DPAPI 保护 | 通过 |
| 13 | 断网有效缓存继续用 | 通过 |
| 14 | 宽限到期安全退回免费版 | 通过 |
| 15 | 网络错误不判盗版 | 通过 |
| 16 | 修改授权文件验证失败 | 通过 |
| 17 | 日志不含完整激活码 | 通过 |
| 18 | 服务层不能绕过限制 | 通过 |
| 19 | 专业版自定义格式 | 通过 |
| 20 | 旧设置迁移 | 通过原有设置迁移测试 |
| 21 | 教程绕过免费额度 | 通过 |
| 22 | 设备超限明确错误 | 通过 |
| 23 | 停用后新设备激活 | 通过 |
| 24 | 缺少生产配置仍可免费启动 | 通过 |
| 25 | Release 不启用 Mock | 通过 |

额外覆盖：试用/有效状态、许可格式化、离线时钟回拨、项目历史降级保留、输出预设、批处理顺序、免费版不持久化索引、专业版索引恢复、免费 CSV、专业 JSON/日志、配置损坏、无客户端私密管理字段等。

## 13. 编译、发布与安装验收

- Restore：通过；
- Clean：通过；
- Release Build：通过，0 警告、0 错误；
- Release 全量测试：160/160；
- Release 商业专项：63/63；
- Publish：`win-x64`、`self-contained=true`；
- 主程序 ProductVersion：1.3.0；
- PE Machine：`0x8664`，x64；
- PE Subsystem：2，Windows GUI，WinExe 无控制台；
- 发布清单：主程序和 Core 依赖均为 1.3.0；
- 安装包：48,689,812 字节；
- 安装包编译：成功；
- 隔离测试安装：成功；
- 安装后启动：进程持续运行、窗口句柄有效；
- 安装后授权 Provider：`None`；
- 安装后 Core 版本：1.3.0；
- 子进程数量：0，没有 CMD、PowerShell、localhost 或后端窗口；
- 启动日志：`D:\AI AGENT\RAWSelectionAssistant\artifacts\acceptance\startup-1.3.0-final.log`；
- 测试卸载：退出码 0，程序和测试快捷方式均已清除。

Windows 桌面采集验收工具成功启动了验收版窗口，但在截图采集阶段返回 `SetIsBorderRequired failed: 0x80004002`。为避免不可靠坐标操作，没有伪造鼠标验收；改用真实进程、窗口句柄、启动日志、XAML 编译、可访问性专项测试和安装后检查完成验收。该限制不影响软件自身启动。

## 14. 安装包与发布路径

- 正式安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\北尾鸲归片助手_Setup_1.3.0_x64.exe`；
- Self-contained 发布目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\publish\win-x64\`；
- 主程序 SHA-256：`C89909153DB16829B430D37457648919DCC92FDD2783C6EFE0A1084D187CC802`；
- 安装包 SHA-256：`D28B051FCFA25015127003CF226257C333C7D9CA6302F726B99FE4B593C4F4E1`。

## 15. 新增和修改文件

### 版本、文档与安装

- `Directory.Build.props`
- `README.md`
- `installer/RAWSelectionAssistant.iss`
- `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`
- `src/RAWSelectionAssistant/app.manifest`
- `src/RAWSelectionAssistant/appsettings.license.json`
- `src/RAWSelectionAssistant/appsettings.license.example.json`
- `src/RAWSelectionAssistant/Views/HelpWindow.xaml`

### 授权模型和服务

- `src/RAWSelectionAssistant.Core/Models/LicenseModels.cs`
- `src/RAWSelectionAssistant.Core/Models/ProjectModels.cs`
- `src/RAWSelectionAssistant.Core/Services/LicensingAbstractions.cs`
- `src/RAWSelectionAssistant.Core/Services/LicenseKeyFormatter.cs`
- `src/RAWSelectionAssistant.Core/Services/LicenseConfigurationService.cs`
- `src/RAWSelectionAssistant.Core/Services/DeviceFingerprintService.cs`
- `src/RAWSelectionAssistant.Core/Services/DpapiLicenseStorageService.cs`
- `src/RAWSelectionAssistant.Core/Services/FeatureGateService.cs`
- `src/RAWSelectionAssistant.Core/Services/UnavailableLicenseProvider.cs`
- `src/RAWSelectionAssistant.Core/Services/MockLicenseProvider.cs`
- `src/RAWSelectionAssistant.Core/Services/CryptolensLicenseProvider.cs`
- `src/RAWSelectionAssistant.Core/Services/LicenseProviderFactory.cs`
- `src/RAWSelectionAssistant.Core/Services/LicenseService.cs`
- `src/RAWSelectionAssistant.Core/RAWSelectionAssistant.Core.csproj`
- `src/RAWSelectionAssistant.Core/Utilities/AppDataPaths.cs`

### 项目、权限和业务服务

- `src/RAWSelectionAssistant.Core/Services/ProjectEntitlementService.cs`
- `src/RAWSelectionAssistant.Core/Services/ProjectHistoryService.cs`
- `src/RAWSelectionAssistant.Core/Services/OutputPresetService.cs`
- `src/RAWSelectionAssistant.Core/Services/BatchProjectService.cs`
- `src/RAWSelectionAssistant.Core/Services/InputParserService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaIndexService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaMatchService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaReportService.cs`
- `src/RAWSelectionAssistant.Core/Models/Branding.cs`

### 界面与教程

- `src/RAWSelectionAssistant/App.xaml.cs`
- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/MainWindow.xaml.cs`
- `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`
- `src/RAWSelectionAssistant/Services/IDialogService.cs`
- `src/RAWSelectionAssistant/Services/WpfDialogService.cs`
- `src/RAWSelectionAssistant/Views/MediaDetailsWindow.xaml`
- `src/RAWSelectionAssistant/Views/MediaDetailsWindow.xaml.cs`
- `src/RAWSelectionAssistant.Core/Models/OnboardingModels.cs`
- `src/RAWSelectionAssistant.Core/Services/OnboardingService.cs`
- `src/RAWSelectionAssistant.Core/Services/SettingsService.cs`

### 测试

- `tests/RAWSelectionAssistant.Tests/CommercialEditionTests.cs`
- `tests/RAWSelectionAssistant.Tests/OnboardingRequirementTests.cs`
- `tests/RAWSelectionAssistant.Tests/OnboardingServiceTests.cs`

## 16. 仍需用户提供的生产授权配置

在正式启用 Cryptolens 前，需要用户从自己的授权平台账户提供或确认：

1. `ProductId`；
2. 仅供客户端验证的 RSA 公钥；
3. 最小权限公开验证 Token；
4. 正式购买页面 URL；
5. 许可有效期、试用策略和每个激活码最大设备数；
6. Cryptolens 当前账号实际返回的签名载荷格式。

不得提供或写入客户端：管理员 API 密钥、私钥、产品管理 Token、支付密钥或可以生成许可证的凭据。

## 17. 仍需人工完成的平台设置

1. 在 Cryptolens 后台创建正式产品；
2. 配置单设备激活策略和换机停用规则；
3. 创建最小权限客户端验证 Token；
4. 配置并备份签名私钥，客户端只放公钥；
5. 配置正式许可证模板、试用、过期和暂停策略；
6. 用测试产品完成 Activate、GetKey、Deactivate 的真实联调；
7. 验证 Cryptolens 实际签名 JSON 与当前 `CryptolensLicenseProvider` 映射一致；
8. 配置销售页、支付后发码流程和售后换机流程；
9. 对正式 EXE 和安装包进行 Authenticode 代码签名；
10. 使用允许商业分发的 Inno Setup 许可/编译环境重新生成最终对外安装包。

## 18. 已知问题

1. 生产 Cryptolens 参数尚未提供，当前专业版只能通过自动化测试中的显式 Mock 定义验证，正式 EXE 不启用 Mock。
2. `CryptolensLicenseProvider` 已实现安全抽象和失败关闭，但尚未用真实账号做端到端联调；生产发布前必须核对官方当前响应签名格式。
3. 当前 EXE 和安装包未做 Authenticode 代码签名，Windows 可能显示未知发布者提示。
4. 本机 Inno Setup 7.0.2 编译器输出明确显示 `Non-commercial use only`。本安装包可用于本次开发验收；商业分发前必须更换为许可条款允许商业使用的编译环境或取得相应许可。
5. 桌面截图采集工具与该 WPF 窗口接口不兼容，返回 `0x80004002`；已通过窗口句柄、日志和自动化测试替代验证。

## 19. 安全风险说明

- 软件授权只能降低普通共享和滥用，无法成为不可破解的 DRM；拥有本机管理员权限的攻击者仍可能尝试修改程序。
- 客户端只包含公开验证配置，不能包含任何可以生成、管理或批量吊销许可证的秘密。
- DPAPI 保护当前 Windows 用户下的本地凭证，配合签名和设备指纹防止直接复制或文本修改；它不能替代代码签名和服务端吊销策略。
- 时钟回拨只触发复核或进入安全宽限，不因误判删除授权和用户文件。
- 授权失败、过期、暂停、断网、配置损坏和降级都不会删除照片、项目、索引、报告或输出文件。
- 购买页面只有用户主动点击时才打开；应用不会自动启动浏览器，不会运行 localhost、命令行授权服务或后台窗口。
