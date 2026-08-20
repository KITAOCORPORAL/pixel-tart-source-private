# 素材库 P1 Gate A 最终物理验收记录（BLOCKED / READY_FOR_MANUAL_RUN）

日期：2026-08-20

分支：`feature/modular-harness-v1-p1`

本轮 V2 起始 HEAD：`ab21ef0bec2eb04f1b0e720418770e9025286e4c`

V2 代码与契约提交：`029e57e4f0f937894738ac2593d5608e0bae2c65`

V2 人工包提交：`65e4206e1468950789c86a4f98b876c5889d524a`

P0：`140e34348000174986c6e503dcedff8f90a78c34`

P1 实现：`b4bd38f53d6a44756289eeda8bfc4feb343443c7`

## 结论

P1 Gate A 状态为 **BLOCKED（READY_FOR_MANUAL_RUN）**，`capture_status` 继续保持 `not_captured`，不得进入 P2～P6。

2026-08-20 已完成一次性人工验收包 V2 的代码、契约、校验器和自动回归修复，但没有在无人操作桌面的情况下运行 `-Mode Run`，也没有生成 08～11、splitter 或四组真实 DPI 证据。因此“包已可交给真人一次执行”不等于 P1 已通过。

## 人工验收包 V2 修复结果

- 正常 `Run` 路径不再使用 `Read-Host`。PowerShell 只显示一个中文动作提示，在后台观察真实 DevPreview 前台、状态控制器、物理输入诊断和稳定时间窗；用户无需切回终端逐步确认。
- 脚本不调用 `SetForegroundWindow`、`SendInput`、UIAutomation Invoke 或显示设置写 API；截图前后仍由捕获器核对同一 PID/HWND/完整 EXE 路径/标题及 foreground。
- first-empty 与 loading/error/Retry/recovered 使用两个独立新根；普通 12 项 synthetic、splitter、重启恢复和 DPI 使用验收专用 route-only 直达，不再第三次依赖曾被桌面控制器阻断的 `AssetLibraryNavigationButton`。
- route-only 仍要求专属编译门、精确 DevPreview 进程名、绝对 acceptance root、`asset-library` allowlist 与当前小写 40 位 HEAD；它不启用状态注入，也不关闭 synthetic preview。正式 Debug、Acceptance、Release 均无法启用。
- 专属构建把 SDK `SourceRevisionId` 写入程序集 informational version。人工包从 clean tracked HEAD 构建后生成独立 `build-manifest.json`；严格校验器交叉核对 manual/build、四个 session、窗口、EXE 路径/hash 与 HEAD，契约不再永久硬编码历史 SHA。
- DPI interaction 现在同时支持原有真实 mouse click/drag，或严格 Left/Right 键盘四层链；键盘必须有 Win32 down/up、WPF Preview/KeyDown/Preview/KeyUp、同一 splitter 焦点、同 attempt 的真实持久值变化，并位于 default/interaction capture 时间窗内。
- 每个 splitter、折叠/展开、缩略图滑杆与重启恢复动作均逐步局部校验；超时或失败会停在当前步骤并保留 run root，不跳步。
- 显示矩阵只读观察真人在 Windows 设置中的变化。取消/异常路径会进入恢复检查，只有真实观察到 `3840x2160@60Hz / 150%` 才写 `display_restored=true`。

## V2 自动验证（唯一当前运行）

| 验证 | 结果 |
|---|---:|
| PowerShell AST / contract JSON / diff check | PASS |
| 人工包目录 | 1 个 `.ps1`，0 个 `.bat` |
| DryRun | PASS；GUI=false、validator=false、显示未修改 |
| RecoveryTest | PASS；环境恢复、辅助进程清理、显示基线正反判断均为 true |
| V2 整合聚焦测试 | 42/42 passed |
| Debug solution build（warnings-as-errors） | PASS，0 warnings / 0 errors |
| Release solution build（warnings-as-errors） | PASS，0 warnings / 0 errors |
| Core | 1192/1192 passed |
| WPF | 863/863 passed |
| Modular Harness | 14/14 passed |
| DPI | 75/101 passed，26 failed |
| 完整 solution | 2170 total / 2144 passed / 26 failed / 0 skipped |

26 个 DPI 失败仍逐项来自既有 `artifacts/automated-dpi-review/2.0.4/*.json` 缺失，没有新增失败或跳过。严格 Gate A validator 与 Modular Harness runner 未在真实 V2 run 上执行；在证据尚未产生时不得把它们写成通过。

## 历史导航证据复用边界

历史证据 `.validation/P1-GateA-Real-20260818-170520-db3d4e4b/evidence/navigation-7-physical-pointer-session.json` 的七项一级导航均含 Win32→WPF→精确 AutomationId→Button.Click 真实链。对 `b4bd38f53d6a44756289eeda8bfc4feb343443c7..ab21ef0bec2eb04f1b0e720418770e9025286e4c` 的差异审计确认以下生产路径零变更，因此仅复用“已验证且未变化的导航行为”：

- `MainWindow.xaml`：blob `f32b97123c07e648c955373b9a62bb918224e196`
- `MainWindow.xaml.cs`：blob `7a478b145b83e7efd5d2bf3ad74895ac46a2493c`
- `MainViewModel.cs`：blob `f405c24bc7061d045d16a2cde8de4bb56b55d4fd`
- `ModuleWorkspaceHost.cs`：blob `7a20b1f95e4ccc6f6678cbd2db08b62e7cd2afa9`
- `AssetLibraryModule.cs`：blob `7298fb71f132471e5a7d6ec0aa6599c0d63dfe54`

这项复用不替代本轮 08～11、Retry、splitter、DPI、恢复或严格 validator 的新证据。

## V2 唯一人工入口

`artifacts/manual-acceptance/P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET/Invoke-P1AssetLibraryGateAManualAcceptance.ps1`

真人只需运行该脚本的 `-Mode Run`，随后按屏幕中文提示在像素蛋挞与 Windows 显示设置中完成每次一个动作，不需要回 PowerShell 确认。失败时只需回传脚本输出的 run root 或最终摘要。

## 2026-08-19 前次物理尝试（历史记录）

本轮确实从 `fab900d` 的 clean 工作树开始，生成了新的隔离运行根、唯一 TRX 基线、专属 DevPreview 发布及两个独立状态会话。两次会话均能精确绑定唯一 PID、完整 EXE 路径、精确标题和包含 `AssetLibraryNavigationButton` 的一级导航树；但 Computer Use 对该按钮的首次物理点击均返回未知结果，重新枚举、重新绑定、激活和刷新后允许的唯一重试也均失败。按有界恢复规则停止，没有使用坐标盲点、命令直达、自动化树 Invoke、旧截图或循环重试冒充真实物理证据。

## 本轮运行与发布

运行根：`.validation/P1-GateA-Physical-20260819-151509-732ceb65/`

SDK：`.NET SDK 10.0.302`

专属发布：`Debug + ModularHarnessDevPreview=true + AssetLibraryP1StateAcceptance=true + InputRoutingDiagnostics=true`，0 warnings / 0 errors。

发布 EXE SHA-256：`827767075FD022DD5D89990F3C5A595A2E91173BC93B0FD4D7C922F0B4BA0FB9`

素材模块 DLL SHA-256：`523E2AC0B420198EB5A9CF7CEB2551277EA4DA3A6D213A936A1E996291F62842`

所有运行时 DB、WAL/SHM、日志、EXE/DLL、窗口清单和机器绝对路径均保留在 ignored `.validation` 中，不进入 Git。

## 基线构建与测试数字

本轮只统计 `.validation/P1-GateA-Physical-20260819-151509-732ceb65/baseline/trx/` 内四份当前运行 TRX 的唯一 `executionId` / `testId`；未累计历史运行。

| 验证 | 本轮结果 |
|---|---:|
| Debug solution build（warnings-as-errors） | PASS，0 warnings / 0 errors |
| Release solution build（warnings-as-errors） | PASS，0 warnings / 0 errors |
| Core | 1192/1192 passed |
| WPF | 837/837 passed |
| Modular Harness | 14/14 passed |
| DPI | 75/101 passed，26 failed |
| 完整 solution | 2144 total / 2118 passed / 26 failed / 0 skipped |

26 个失败逐项仍来自既有 `artifacts/automated-dpi-review/2.0.4/*.json` 缺失；没有新增失败、不同失败原因或跳过项。原始命令、耗时、退出码、完整日志和 TRX 保存在本轮 `baseline/` 下。

## 状态会话实际结果

### first-empty

- 隔离根：`states/first-empty/`；启动前精确确认素材 DB/WAL/SHM 不存在。
- PID 3296；窗口标题为 `像素蛋挞 [Modular Harness Dev]`；EXE 路径和哈希与本轮发布一致。
- 状态控制器真实创建了 SQLite v6 数据库，并在原始清单中记录 `repositorySource=real-repository`、`repositoryImplementation=SqliteAssetLibraryRepository`、`repositorySchemaVersion=6`、`repositoryAssetCount=0` 和 attempt 1 ready。
- Computer Use 的素材库物理点击结果未知；重新绑定后唯一一次重试仍失败。没有 08 PNG/window-evidence 对，因此 first-empty **不能判 PASS**。

### loading / error / Retry / recovered

- 独立隔离根：`states/retry/`；启动前精确确认素材 DB/WAL/SHM 不存在。
- PID 35000；窗口标题、EXE 路径和哈希与本轮发布一致。
- 首次物理点击结果未知但状态控制器确实进入 `loading-barrier-waiting`；重新绑定后的唯一重试仍失败。
- 为遵守停止规则，没有创建 release gate，没有取得 09 loading、10 error、11 recovered 截图，也没有执行物理 `RetryAssetLibraryLoad`。因此 error、Retry、attempt 2 真实仓储查询和 recovered 均 **未完成**。

状态控制器原始 JSON 证明机制能够 fail-closed；它不能替代本轮缺失的可见窗口截图和物理输入链。

## splitter、DPI 与显示恢复

- 本轮未能进入可审计素材页，所以没有执行两个 splitter 的 Left/Right、边界无变化、折叠/展开和新进程恢复；没有形成键盘 Layer 1～4 证据。
- 四组真实 DPI 的每组验收都要求先进入同一素材页并在 default 后完成至少一个物理点击/拖动。该前置条件已在两个独立会话中用尽有界恢复，故没有修改 Windows 显示设置，也没有把只读模式探测写成实测。
- 未执行：`1366x768@100%`、`1920x1080@125%`、`1920x1080@150%`、`2560x1440@175%`。
- 系统显示从未改变；进程清理后的最终只读回读为 `3840x2160 @ 60 Hz / 150%`，与本轮实测原始状态相同。该事实不是四组 DPI 矩阵 PASS。

## 严格校验器与 Modular Harness runner

严格 Gate A 校验器已对本轮根执行，退出码 1，报告 41 个缺失/未捕获条件；主要包括 08～11 状态证据、Retry 物理点击、splitter 键盘链、重启恢复、8 张 DPI 图、最终 restore 截图及 0→12 合成导入诊断。失败与真实缺口一致，校验器没有被放宽。

本轮没有完整 `foreground-result.json`、三阶段 process snapshot、同 run 合成导入与完整 Gate A 前台材料，因而没有把旧前台证据转发给 Modular Harness acceptance runner，也没有冒报新的 runner 结果。历史 runner 结果不得替代本轮 Gate A。

## 安全边界

- 正式产品 Schema 5 未改；素材库 schema v6、表、索引和用户素材行未改。
- 没有读取、移动、重命名、覆盖或删除用户源文件。
- 没有写 Eagle `.library`，没有进入 P2～P6。
- 两个会话只使用全新隔离根；所有本机路径、DB、日志和运行时发布均留在 `.validation`。
- `tools/AssetLibraryP1Acceptance/gate-a-evidence-contract.json` 继续为 `capture_status: not_captured`。

## 剩余闭环项

1. 在新的可控桌面会话中取得 08 first-empty 真实窗口证据。
2. 取得同 PID/HWND 的 09 loading、10 error、物理 Retry 和 11 recovered 完整链。
3. 完成两个 splitter 全部方向键 Layer 1～4、边界、折叠/展开和新进程恢复。
4. 通过 Windows 设置 UI 实际完成四组 DPI default/interaction，并恢复、截取、复核 `3840x2160@60/150%`。
5. 让严格校验器退出 0；只在同 run foreground artifacts 齐全后运行 Modular Harness acceptance runner。

上述任一项缺失时，P1 仍为 BLOCKED，禁止进入 P2。
