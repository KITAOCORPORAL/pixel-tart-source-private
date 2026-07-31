# 像素蛋挞 2.0.0 工作台重构与品牌改版报告

## 1. 完成结论

本次 2.0.0 工作台重构、品牌改版、自动化测试、Release 构建、win-x64 self-contained 发布、安装包生成、实际安装与启动验收均已完成。

## 2. 软件名称替换

用户可见名称已由“北尾鸲归片助手”替换为“像素蛋挞”，覆盖窗口标题、主界面品牌区、帮助与关于窗口、反馈邮件主题、教程文案、程序集产品信息、安装包名称和卸载项。为兼容历史设置、索引和授权数据，内部程序集名、命名空间与本地数据目录继续沿用既有稳定标识；这些内部标识不会显示为产品品牌。

版本统一为 `2.0.0`，文件版本为 `2.0.0.0`。

## 3. Logo 设计与资源

原创 Logo 使用“像素 + 蛋挞 + 照片负片”组合：石墨色圆角底强调桌面工具属性，暖金与奶油色构成蛋挞，照片负片表明摄影场景，青绿色像素块表达数字文件处理。图形没有使用外部商标或字体字形。

已输出并接入：

- `AppIcon.svg`：主矢量资源。
- `AppIcon.Light.svg`：浅色环境资源。
- `AppIcon.Small.svg`：小尺寸简化资源。
- `AppIcon.png`：1024 像素主位图。
- `AppIcon.ico`：包含 16、20、24、32、40、48、64、128、256 共 9 帧，并通过 WPF 解码启动验收。
- `Assets/Brand/PixelTart-*.png`：9 种导航与品牌位图尺寸。

应用窗口、安装包、侧栏品牌区和帮助页均使用新资源。侧栏与工具箱继续使用统一 PathGeometry 线性图标，避免字体图标乱码。

## 4. 布局思路与原创性

工作台采用“左侧平台导航 + 中央任务与项目 + 右侧工具箱”的桌面生产力结构，用于提升本地摄影工具的可发现性。布局只吸收通用的信息层级和操作路径，没有复制参考产品的商标、原始图标、经营模块、营销卡片或专有文案。

视觉使用暖金品牌主色、青绿工具提示色与石墨中性色，保留 WPF 原生窗口、菜单、键盘访问、高 DPI、浅色/深色/高对比度和强调色机制。

## 5. 经营型入口取消清单

下列入口没有出现在 2.0.0 导航或首页快捷入口中：极速选片、预约管理、我的收入、橱窗管理、客资管理、团队管理、AI 挑图。也没有新增营销横幅、活动推广或云端经营看板。

## 6. 工具整合清单

工作台首页工具箱和左侧工具箱均整合 8 个入口：本地分片、批量压缩、批量水印、删废片、FTP 工具、照片整理、批量重命名、批量转档。

本地分片复用既有解析、索引、匹配、冲突、复制与报告能力。其余 7 个工具建立了清晰页面壳、输入区、参数区、结果或预览区；未接入的执行按钮均明确标记“开发中”并禁用。删废片页面在 2.0.0 不会删除、移动或修改照片，FTP 页面不会自动连接外部服务器。

## 7. 工作台首页

首页提供：品牌化“开始本地分片”主卡、进入专业归片工作区的次级入口、本地项目/待匹配/已完成/最近处理文件四项真实状态、真实项目历史列表，以及右侧 8 工具快捷区。数据均绑定本地项目状态，没有伪造云端统计。

## 8. 左侧导航

- 工作台：工作台首页、本地分片、归片工作区、项目历史。
- 工具箱：批量压缩、批量水印、删废片、FTP 工具、照片整理、批量重命名、批量转档。
- 应用：授权与版本、设置、帮助。

侧栏支持折叠/展开，保留 ToolTip、AutomationProperties.Name 与 `Ctrl+B` 键盘操作。

## 9. 顶部菜单

保留文件、项目、编辑、视图、工具、帮助六组原生菜单及 Alt 访问键。菜单内容已围绕本地分片、归片、7 个照片工具、主题/紧凑模式、教程、反馈与关于页面重新组织。

## 10. 原有功能与边界

原有 RAW 匹配、JPG + RAW 联合归片、自定义格式、来源目录优先、客户 JPG 防混入、冲突处理、复制、报告、设置、教程、历史项目和授权门控均保留。应用仍为 WPF `WinExe`、win-x64、自包含桌面程序，不启动 CMD/PowerShell、浏览器或本地网页服务。

生产授权配置保持 `Provider=None`、`ProductId=0`，Release 启动明确使用 `allowMockProvider: false`，没有启用 Mock 专业版或伪造生产授权。

## 11. 主要修改文件

- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Light.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Theme.Dark.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Theme.HighContrast.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Cards.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Navigation.xaml`
- `src/RAWSelectionAssistant/Assets/AppIcon.*`
- `src/RAWSelectionAssistant/Assets/Brand/PixelTart-*.png`
- `src/RAWSelectionAssistant.Core/Models/Branding.cs`
- `src/RAWSelectionAssistant.Core/Models/AppearanceSettings.cs`
- `src/RAWSelectionAssistant.Core/Services/FeedbackRequestBuilder.cs`
- `src/RAWSelectionAssistant.Core/Services/SettingsService.cs`
- `src/RAWSelectionAssistant/Services/AppearanceService.cs`
- `src/RAWSelectionAssistant/Views/HelpWindow.xaml`
- `src/RAWSelectionAssistant/Views/FeedbackDialog.xaml`
- `src/RAWSelectionAssistant/Views/UpgradeTutorialWindow.xaml`
- `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`
- `src/RAWSelectionAssistant/app.manifest`
- `Directory.Build.props`
- `installer/RAWSelectionAssistant.iss`
- `tools/generate_brand_assets.ps1`
- `tests/RAWSelectionAssistant.Tests/ProductPlatform200Tests.cs`
- 既有 1.4/1.5 UI 验收测试中的版本与品牌断言
- `README.md`
- `docs/品牌与工作台设计说明_2.0.0.md`

## 12. 测试结果

- 全部自动化测试：383/383 通过，0 失败，0 跳过。
- 新增 46 项 2.0.0 验收，覆盖品牌、窗口标题、关于页、工作台、导航、8 个工具入口、经营功能移除、深浅主题、侧栏折叠、Logo/ICO 帧、顶部菜单、原有工作流、WinExe、无本地网页服务、Provider None 和 Release 禁用 Mock。
- Release Build：通过，0 警告，0 错误。
- win-x64 self-contained publish：通过。
- 安装程序编译：通过。
- 实际安装：通过，Windows 卸载项显示“像素蛋挞 2.0.0”。
- 实际启动：通过，窗口标题为“像素蛋挞”，进程稳定运行。
- 桌面可访问性验收：顶部六组菜单、品牌区、工作台、本地分片、左侧 7 个工具入口和首页 8 个工具入口均可识别。

## 13. 发布物

- 发布目录：`D:\AI AGENT\RAWSelectionAssistant\artifacts\publish\win-x64`
- 安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\installer\像素蛋挞_Setup_2.0.0_x64.exe`
- 安装包大小：48,678,418 字节。
- SHA-256：`87556790EAD50C337229583BFAD0E989E9962F0337BD623370AEEC66EF684356`

## 14. 已知问题

1. 批量压缩、批量水印、删废片、FTP、照片整理、批量重命名、批量转档在 2.0.0 为安全页面框架，执行按钮禁用，后续版本需分别接入处理引擎。
2. 因沿用同一升级 AppId，本机从旧版原位升级后安装文件夹仍是历史路径 `C:\Program Files\北尾鸲归片助手`；Windows 产品名、窗口标题、文件产品名、快捷方式和卸载项已显示“像素蛋挞”。全新电脑安装默认目录为 `C:\Program Files\像素蛋挞`。保留 AppId 和内部数据目录是为避免丢失既有项目、设置、索引与授权状态。
3. 授权平台仍未配置，当前只能以免费版运行；这符合本次保持 Provider None 的要求。

