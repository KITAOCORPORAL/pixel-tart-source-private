# 像素蛋挞产品重构总验收报告

## Git 与版本

- 开始 HEAD：`b091be37479e95d4f63944f9f4eb61fbbf6f19a7`。
- 开发分支：`feature/pixel-tart-product-redesign`。
- 验收代码与候选发布 HEAD：`6443d3dc193723b74357e27d2c13c5259970a9f1`。
- 报告归档：本报告与另外四份回传报告随后以独立 `docs(report)` 提交归档；工作树最终 HEAD 以最终回传摘要和 `git rev-parse HEAD` 为准。
- ProductVersion：2.3.0。
- FileVersion：2.3.0.0。
- SchemaVersion：4。
- 本次是否修改 Schema：否。
- 是否合并 main：否。
- 是否创建正式 Tag：否。

## 主要提交

| 提交 | 内容 |
|---|---|
| `188532d` | Visual Design System A-v2 |
| `fc1db9d` | 在线选片 Desktop、本地模型、API 合同/骨架、小程序原型 |
| `6f2e0d8` | LibRaw 安全 RAW 转 JPG |
| `17b330d` | 安全批量压缩 |
| `df0599f` | RAW/批量压缩任务恢复检查点 |
| `97949c2` | 在线代理与结果持久化安全 |
| `e9a5739` | Shell、工作台、日历、Booking、工具箱集成 |
| `bc9f77b` | 产品重构端到端专项测试与 UI 证据工具 |
| `5069aff` | 主题 ComboBox 运行时一致性修复 |
| `31f0798` | A-v2 五份页面母版正式纳入 Git |
| `6443d3d` | ProductRedesign 候选安装配置、验收脚本与发布证据索引 |

## 功能验收

| 范围 | CodeVerified | AutomatedVerified | InstalledUiVerified | UserVerified |
|---|---:|---:|---:|---:|
| Visual Design System A-v2 | 是 | 是 | 部分 | 否 |
| 七个一级业务模块 | 是 | 是 | 主要入口已验证 | 否 |
| 工作台与四项概览 | 是 | 是 | 是 | 否 |
| 完整工作日历 60/40 | 是 | 是 | 入口已验证，比例由自动视觉证据验证 | 否 |
| 日期详情、右键、五色、关闭档期 | 是 | 是 | 右键/关闭档期未验证 | 否 |
| 快速新建、快速编辑、完整策划 | 是 | 是 | 是 | 否 |
| 工具箱四工具与 Pin | 是 | 是 | 是 | 否 |
| 批量压缩 Modal | 是 | 是 | 是 | 否 |
| RAW 转 JPG | 是 | 是 | 是（入口/Modal） | 否 |
| 在线选片 Desktop 四页签 | 是 | 是 | 入口已验证，项目四页签未验证 | 否 |
| RAW 代理 JPG 与结果同步 | 是 | 是 | 代理已验证，同步未验证 | 否 |
| 联机拍摄既有工作区 | 是 | 是 | 是 | 否 |

## RAW 验收

- 实际验证格式：`.ARW`、`.CR2`、`.NEF`，每种一份公开真实样本。
- LibRaw 解码、原尺寸 sRGB JPEG、WPF 再解码、SHA-256、UndoJournal 均完成。
- 源文件长度、修改时间和 SHA-256 前后不变。
- CreateNew、AutoNumber、Flush，不覆盖、不移动、不删除源文件。
- 仅有限重建 Make、Model、拍摄时间、Orientation；不宣称完整 EXIF 透传。

## 在线选片验收

- Desktop 本地闭环、四页签、代理生成、规则、客户进度、结果归档和 RAW-only 归片同步完成。
- `IOnlineSelectionProvider` 完成；Release 默认 Provider 为 None。
- Fake 仅测试使用。
- API Contract 和无监听 Skeleton 完成。
- 微信小程序 V1 可评审原型完成。
- Stage 9 生产云未进入；缺少服务器、域名、HTTPS、对象存储、数据库、微信 AppID 和生产凭证。

## 测试与构建

- 基线：1787。
- 最终 Debug：Core 1098 + WPF 723 + DPI 101 = **1922/1922**。
- 最终 Release：Core 1098 + WPF 723 + DPI 101 = **1922/1922**。
- 失败：0；跳过：0；警告：0；错误：0。
- DPI：100%、125%、150%、175%、200%。
- 结果目录：`artifacts/test-results/product-redesign-final/`。
- 初次 DPI 命令携带 `Platform=x64 --no-build` 时未找到该项目实际输出目录；纠正为该项目正式输出路径后 Debug/Release 均为 101/101。产品测试没有失败。

## 视觉验收

- Codex 预评分：90/100。
- 原尺寸 PNG：30 张，30/30 自动布局通过，阻断问题 0。
- 证据目录：`artifacts/ui-review/product-redesign/`。
- `UserVerified=false`，最终视觉通过仍由用户决定。

## Publish 与候选安装包

- Release、win-x64、self-contained、WinExe。
- Publish 文件：280；总大小：172,099,342 bytes。
- ProductVersion=2.3.0，FileVersion=2.3.0.0，Provider=None。
- 无测试程序集、API Skeleton、PDB、日志、数据库、真实图片、RAW 样本或用户数据。
- 无 localhost / 127.0.0.1、Fake Camera、Mock Camera、厂商相机 SDK。
- 开源 LibRaw runtime 与 9 份第三方声明/许可随包发布。
- 候选安装包：`artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_ProductRedesign_RC1_x64.exe`。
- 大小：50,677,426 bytes。
- SHA-256：`D8A997A463D64BB1D44D3ACFFBDD4A7213DC4A1EDD91FF404CBA4711C2804660`。
- 旧安装包：10/10 保留，大小和 SHA-256 未改变。
- 候选使用独立 AppId 和独立安装目录，不覆盖稳定 2.3.0。
- 隔离安装和启动成功，退出码 0；隔离数据库 SchemaVersion=4，`PRAGMA integrity_check=ok`。
- 隔离真实点击结果：`artifacts/diagnostics/2.3.0/product-redesign-installed-ui/5a4e50af98444997863f5ac0031448aa/result.json`，SHA-256 `75F3C31FDA85D8127E4FBEF434C60BF7B0ADDD752757A26FD7BEE9778D2C89EB`。
- 已验证工作台、三种 Booking、完整日历入口、四工具与 Pin、收支、在线入口/Provider/代理、联机和优雅退出。
- 未验证日历右键/关闭档期/重启保持及在线项目打开/四页签/结果同步；隐藏桌面输入限制已保留失败证据，未假记为通过。

## 已知限制

- UserVerified 仍为 false。
- RAW 只对本轮三份真实样本宣称 ARW/CR2/NEF 验证，不扩大到全部相机型号和候选扩展。
- EXIF 是有限重建，不是所有标签透传。
- 在线选片真实云服务、小程序生产发布和运维能力尚未配置。
- 候选安装包未进行代码签名。
- 物理显示器、真实长期工作数据和用户主观视觉仍需用户最终验收。
- 安装版交互门禁为部分通过，六个项目仍需用户前台实机复核。

## 最终结论

Stage 0–7 和 Stage 8 的构建、测试、视觉证据、Publish、安装包与隔离启动已完成；Stage 8 安装版交互门禁部分通过，六项待用户前台实机复核。Stage 9 Production Cloud Deployment 按要求停止。候选包不是正式 Tag，不合并 main，也不覆盖已有安装包。
