# 像素蛋挞相机厂商 SDK 调研矩阵

## 1. 调研结论

2.3.0 不应集成真实厂商 SDK。推荐实际适配顺序为 Sony → Canon → Nikon → Fujifilm；第一候选为 Sony Camera Remote SDK，但仍需取得测试相机、下载正式 SDK、锁定具体版本、完成最终许可复核和安装包分发验证。

本调研只读取厂商和 Microsoft 官方公开页面，未下载 SDK、未登录开发者门户、未接受许可证、未复制第三方库。

## 2. 总矩阵

| 项目 | Sony | Canon | Nikon | Fujifilm |
|---|---|---|---|---|
| 官方名称 | Camera Remote SDK | EOS Digital Software Development Kit（EDSDK）；无线另有 CCAPI | Remote Module SDK；另有 NEF/NRW Image SDK | X Series / GFX System Digital Camera Control SDK |
| Windows | 官方公开页列 Windows 11；Intel/AMD，不支持 Windows ARM | EDSDK：Windows；CCAPI：跨平台、选定机型 | 2026 统一 Z 系列 Remote Module SDK；当前公开信息已结束 Windows 10 支持，按 Windows 11/x64 评估 | Windows 11 x64；公开页同时列出部分旧版 Windows |
| 下载/申请 | 按地区申请；中国大陆有独立申请入口 | 注册 Canon Developer Programme/Community 后获取 SDK、文档和许可 | 免费，但必须完成申请流程；官方明确不提供技术支持 | 个人可在同意公开 EULA 后下载；企业有独立联系入口 |
| 公开许可清晰度 | 较高：公开 EULA 允许将库的二进制以不可分离方式并入应用并分发 | 中低：公开页说明许可需在获得访问后于下载页查看，不能预设可再分发 | 中低：公开页说明申请和免费，但完整再分发条款需在申请流程内确认 | 高但约束重：公开 EULA 明确 `REDISTRIBUTABLE` 库的目标代码分发条件 |
| 支持机型来源 | 官方 Camera Remote Toolkit 页面实时列表 | Developer Programme 的兼容机型/API 参考；发布公告只能作为补充 | 官方 SDK Download 和 Information/FAQ 列表 | 官方 Camera Control SDK Compatibility 列表 |
| USB | 是，官方列 USB | EDSDK 定位为有线控制；具体机型复核 | 连接电脑的远程控制；具体接口/API 需包内文档复核 | 是，官方明确 USB 直连 |
| Wi-Fi/网络 | 是，官方列 Wireless LAN；逐机型复核 | Wi-Fi 主要通过 CCAPI、选定机型；不可与 EDSDK 能力混为一谈 | 公开下载页未给统一保证，需逐机型/Command API 复核 | 是，官方列经接入点的 TCP/IP；个别机型/固件例外 |
| 实时取景 | 是，官方明确 live view monitoring | 业界能力成熟，但产品门禁必须按当前 EDSDK API/机型表确认 | 公开下载页未给统一承诺，按“待包内 API 确认”处理 | 公开页只明确图像自动传输和基础控制；实时取景不得先宣称 |
| 远程控制 | 设置、快门和更多命令；逐机型能力表 | 设置、拍摄行为和相机控制 | 快门速度、光圈、ISO、快门释放等 | 基础控制；逐机型复核 |
| 文件传输 | 官方明确获取相机拍摄图像数据 | 官方明确传输相机图像 | 公开下载页未给统一保证，待 API 文档确认 | 官方明确自动传输图像；不提供 RAF RAW 转换信息 |
| .NET 封装 | 需要适配层；不得让 WPF/ViewModel 直接引用原生库 | 需要 P/Invoke 或 C++/CLI 包装；CCAPI 应独立 Provider | 需要原生封装；不得把 MAID/命令对象泄漏到核心 | 需要原生封装；只分发 EULA 允许的 Library 目标代码 |
| 原生 DLL 位数 | 公开首页未完整列明；下载后必须确认 x64，产品不接受 x86-only | 下载后确认；像素蛋挞只接受 x64 Release | 当前 Windows 路径按 x64 门禁；统一 SDK 包内复核 | Windows 11 路径按 x64；旧 OS 的 x86 条目不进入产品 |
| 商业发布 | 官方 FAQ 允许合法销售应用；公开 EULA 允许不可分离二进制分发，但包含最终用户告知、支持和使用限制 | 在取得当前许可全文前一律视为“禁止随包分发” | 在取得当前许可全文前一律视为“禁止随包分发” | 允许按 EULA 分发 `REDISTRIBUTABLE` Library，但必须向客户施加相应义务并自行提供支持 |
| 关键风险 | SDK/固件兼容矩阵、最终用户告知、原生崩溃和版本更新 | 许可在受限门户内、EDSDK 与 CCAPI 两套能力边界、机型矩阵 | 完整许可/分发条件未公开、无官方技术支持、旧机型与统一 Z SDK 并存 | 使用 SDK 控制相机将触发厂商保修例外，且必须在向客户提供应用前明确告知；责任与支持义务重 |
| 风险级别 | 中 | 高 | 高 | 极高 |

## 3. Sony

### 3.1 官方事实

- Camera Remote SDK 可让主机远程改变设置、释放快门、监看实时取景并取得拍摄图像。
- 官方列 USB、有线 LAN 和无线 LAN；支持能力必须按设备 API 参考确认。
- 官方公开页列 Windows 11、macOS 和 Linux，Windows 仅 Intel/AMD 处理器。
- SDK 免费；FAQ 表示开发者可为合法目的销售基于 SDK 的应用。
- 公开 EULA 允许把库文件二进制以不可分离方式并入应用并向第三方分发。
- EULA 同时包含最终用户告知、厂商保修例外、由开发者承担支持、出口和禁止用途等义务。

### 3.2 像素蛋挞门禁

- 首个测试机建议 `ILCE-7M4`，它在当前官方支持列表中，且适合作为常见全画幅工作流样本。
- 必须再准备一台不同代际机型验证能力差异，不能以一台机推断全系列。
- SDK Host 必须为 x64 独立进程；位数、依赖 DLL、VC 运行库和签名在取得正式包后确认。
- 安装包只包含许可允许的二进制，不包含示例源代码、文档或未列为可分发的组件。

### 3.3 结论

四家中最适合作为第一个真实适配器，但建议进入 2.4.0，不提前混入 2.3.0。

## 4. Canon

### 4.1 官方事实

- EDSDK 面向 Windows、Mac、Raspberry Pi OS 和 Ubuntu 的相机远程控制；官方说明可跨兼容 Canon 相机复用代码。
- EDSDK 提供相机设置、拍摄行为和图像传输能力。
- CCAPI 面向选定机型的无线、多平台场景；它与 EDSDK 是不同传输和部署边界。
- SDK/API 资源需要加入 Canon Developer Programme/Community。
- Canon Europe 公开页明确：获得访问后才能在下载页阅读对应许可协议。

### 4.2 像素蛋挞门禁

- 在取得当前 EDSDK 许可全文前，不把 DLL 加入仓库或安装包。
- 首个测试机可选择 `EOS R5 Mark II`，但采购前必须在登录后的当前支持矩阵中复核其 EDSDK 版本、Windows 11/x64 和实时取景/传输能力。
- CCAPI 如未来接入，必须是独立 Provider，不能在 `CanonCameraAdapter` 内静默切换网络协议。
- 需要断线、相机被 EOS Utility 占用、存储卡策略、保存目标和 USB 枚举专项测试。

### 4.3 结论

技术成熟度高，但公开再分发信息不完整，排在 Sony 之后。

## 5. Nikon

### 5.1 官方事实

- Nikon 免费提供 SDK，但必须完成申请，且官方明确不提供技术支持。
- Remote Module SDK 提供远程设置和快门控制；官方公开页列出 Library Programs 和 Command API Specifications。
- 2026 年公开信息显示 Z 系列已改为统一 Remote Module SDK 2.0.0，同时大量旧 DSLR 仍有独立模块。
- 当前统一 Z 系列列表包括 Z9、Z8、Z6III、Z7II、Z6II、Z7、Z6、Z5II、Z5、Zf、Z50II、Z50、Z30、Zfc、ZR。

### 5.2 像素蛋挞门禁

- 首个测试组合建议 `Z6III` 加 `Z6II`：验证统一 Z SDK 和旧兼容路径。
- 文件传输、实时取景、网络连接、分发组件和完整许可必须以申请后的当前包为准。
- 不接入已经过期的 32 位模块；2.4.0 只允许 x64 Host。
- 由于官方不提供 SDK 技术支持，需要预留更高的诊断、固件回归和故障隔离成本。

### 5.3 结论

可行，但许可和支持成本高，排在 Canon 之后。

## 6. Fujifilm

### 6.1 官方事实

- Camera Control SDK 支持 Windows/macOS，当前公开页还列 Linux、Raspberry Pi OS 和 Android 条目。
- 官方明确 USB、TCP/IP、自动图像传输和兼容相机的基础控制。
- SDK 不提供 RAF RAW 数据转换信息。
- 公开 EULA 对 `REDISTRIBUTABLE` 文件夹中的 Library 允许目标代码形式并入应用后分发。
- EULA 要求开发者向客户施加相应义务、承担支持和合规责任，并及时跟随修复版本。
- 官方页面和 EULA 明确：使用该 SDK 连接或控制兼容相机将使相机落入厂商保修例外；向客户提供应用前必须解释并让客户充分理解。

### 6.2 像素蛋挞门禁

- 最高风险不是“能否写出适配器”，而是保修告知和商业责任。
- 首个测试机如进入后续阶段建议 `X-T5` 或 `X-H2S`，两者均出现在当前官方兼容列表；仍需企业许可/法务复核。
- 不得把个人下载 EULA 自动等同于面向所有商业地区的企业发布许可。
- 不得依赖 SDK 做 RAF 解码；RAW 预览必须是独立、合法、可替换的解码边界。

### 6.3 结论

技术上可行，许可与保修风险最高，最后适配。

## 7. 测试相机建议

| 厂商 | 最小首轮 | 第二轮 | 目的 |
|---|---|---|---|
| Sony | ILCE-7M4 | 一台不同代际、官方支持机型 | 能力差异、USB/Wi-Fi、固件回归 |
| Canon | EOS R5 Mark II（门户复核后） | 一台入门 R 系列（门户复核后） | EDSDK 能力差异、资源占用、传输 |
| Nikon | Z6III | Z6II | 统一 Z SDK 与旧兼容路径 |
| Fujifilm | X-T5 或 X-H2S | 一台 GFX（如业务确有需求） | USB/TCP-IP、保修告知、RAF 边界 |

没有实机时只能运行合同测试和 Fake Host 测试；Fake 只存在测试项目，不注册到 Release。

## 8. 发布前统一许可门禁

每个真实 Provider 必须同时满足：

1. 从官方渠道获得、保留版本与下载日期证据；
2. 法务确认开发、商业发布、二进制再分发、地域和最终用户告知；
3. 列明允许随安装包分发的每个文件；
4. x64、Windows 11、签名、运行库和安装路径通过验证；
5. 至少两台代表性相机和指定固件通过实机矩阵；
6. 卸载、升级、SDK 缺失和 SDK 崩溃不影响普通归片；
7. 第三方通知和 EULA 在安装包/帮助中正确展示；
8. SDK 更新后重新跑全部实机与安装包门禁。

任何一项不满足，Provider 保持未注册。

## 9. 官方资料

- Sony：[Camera Remote SDK](https://support.d-imaging.sony.co.jp/app/sdk/en/index.html)
- Sony：[Camera Remote SDK License Agreement](https://support.d-imaging.sony.co.jp/app/sdk/licenseagreement/en.html)
- Canon：[SDK overview](https://www.usa.canon.com/support/sdk)
- Canon：[Camera Overview](https://developers.canon-europe.com/developers/s/camera)
- Canon：[Developer Programme](https://developers.canon-europe.com/s/)
- Nikon：[SDK Download Service for Developers](https://sdk.nikonimaging.com/apply/)
- Nikon：[SDK Information/FAQ](https://sdk.nikonimaging.com/information/en/)
- Fujifilm：[Camera Control SDK](https://www.fujifilm-x.com/en-us/camera-control-sdk/)
- Fujifilm：[Camera Control SDK EULA](https://www.fujifilm-x.com/en-us/camera-control-sdk/agreement/)

