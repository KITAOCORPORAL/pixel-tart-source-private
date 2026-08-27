# 素材库 P1 Gate A 最终物理验收记录（BLOCKED / READY_FOR_MANUAL_RUN）

日期：2026-08-20

分支：`feature/modular-harness-v1-p1`

本轮 V2 起始 HEAD：`ab21ef0bec2eb04f1b0e720418770e9025286e4c`

V2 代码与契约提交：`029e57e4f0f937894738ac2593d5608e0bae2c65`

V2 人工包提交：`65e4206e1468950789c86a4f98b876c5889d524a`

Windows PowerShell 5.1 兼容修复：`638afa44b121d4d0d897a0ef36ab579e6439e7d9`

瞬态辅助窗口捕获修复：`c5f67425cb51ba7b443db9f61b6326f8039def8f`

Gate A 专属构建节点复用修复：`e1739807d0e11d52da9125eecad567235843f9a4`

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
| 人工包聚焦契约测试 | 7/7 passed |
| Debug solution build（warnings-as-errors） | PASS，0 warnings / 0 errors |
| Release solution build（warnings-as-errors） | PASS，0 warnings / 0 errors |
| Core | 1192/1192 passed |
| WPF | 865/865 passed |
| Modular Harness | 14/14 passed |
| DPI | 75/101 passed，26 failed |
| 完整 solution | 2172 total / 2146 passed / 26 failed / 0 skipped |

26 个 DPI 失败仍逐项来自既有 `artifacts/automated-dpi-review/2.0.4/*.json` 缺失，没有新增失败或跳过。WPF 新增一项真实 STA 键盘遍历回归：从素材页根开始最多 12 次前向焦点移动必须到达 `RetryAssetLibraryLoad`，且移动焦点不得触发 attempt 2；本机路径 9 步通过。严格 Gate A validator 与 Modular Harness runner 未在真实 V2 run 上执行；在证据尚未产生时不得把它们写成通过。

## Windows PowerShell 5.1 实跑修复

2026-08-20 的首次真人 V2 Run 已真实完成 08 first-empty 和 09 loading 自动捕获，随后在创建 release gate 前因 `.NET Framework` 不提供 `Path.IsPathFullyQualified` 而 fail-closed。失败清单正确写出 `status=failed`、`display_restored=true`，DevPreview 残留进程为 0；失败根继续保留在 `%TEMP%`，不得提交。

人工包已改用 PowerShell 5.1 兼容的盘符绝对路径/UNC 判断，并继续对规范化后的 release 路径执行 retry 会话根边界检查。三个相关脚本的 Windows PowerShell 5.1 AST 均为 0 error；系统 `powershell.exe` 下的 drive-root/UNC/relative/drive-relative 正反验证、DryRun、RecoveryTest 与人工包聚焦测试 6/6 均通过。静态审计未发现第二个 .NET Core-only API。

由于该修复改变源码 HEAD 和专属 EXE 哈希，失败根中的 08/09 只能作为失败诊断，不能与修复后的证据拼接。下一次必须从新的 run root 全量重跑。

## 瞬态 WPF 辅助窗口捕获修复

2026-08-20 的第二次真人 V2 Run 在 `c69a3835750a449c994f49235c3f8e4bbaa006ef` 上真实通过 first-empty 状态探针：attempt 1、真实 SQLite v6、0 项和素材库直达均成立；但在写入 08 之前，严格捕获器发现同一 PID 下出现一个空标题、非前台的动态 `HwndWrapper[...]` 可见顶层窗口，因它不在四个精确 IME 类白名单内而 fail-closed。该窗口与鼠标悬停产生的瞬态 WPF ToolTip/Popup 相符，但失败运行未记录足够的 owner/style/rect 证明，所以没有按类名前缀放行，也没有把它伪装成合法辅助窗口。

失败根保留在 `%TEMP%`：没有生成 08 PNG/window-evidence，validator 未启动，DevPreview 无残留进程，显示前后均为 `3840x2160@60/150%` 且 `display_restored=true`。该根只能用于失败诊断，不能继续执行或与后续证据拼接。

修复提交 `c5f67425cb51ba7b443db9f61b6326f8039def8f` 保留原有严格口径：任何非白名单辅助顶层窗口出现时，捕获器最多等待 15 秒，每 200 ms 重新枚举，并持续要求同一精确主 HWND 保持前台；只有辅助窗口真实消失后才截图。超时、主 HWND 改变或失去前台仍立即失败。截图后再次枚举辅助窗口，严格 validator 现在同时要求捕获前后 `unexpected_auxiliary_window_count=0` 和 `no_unapproved_auxiliary_window_during_capture=true`。没有宽泛允许 `HwndWrapper`，也没有放宽第二窗口检测。

Windows PowerShell 5.1 下三个相关脚本 AST 均为 0 error；捕获器、人工包与严格 validator 聚焦测试 32/32 通过；人工包 DryRun 和 RecoveryTest 均退出 0。由于修复再次改变 HEAD 与专属 EXE 哈希，下一次真人 Run 必须使用全新 run root 从第 1 步全量重跑。P1 Gate A 仍为 **BLOCKED（READY_FOR_MANUAL_RUN）**，`capture_status` 仍为 `not_captured`。

## Retry 步骤越序与真实素材误导修复

2026-08-20 在 `bec9fa024309f731310d2f3e322147ce05c18a2d` 上的第三次真人 V2 Run 已真实完成 08 first-empty 与 09 loading。retry 会话随后于 `13:45:47` 进入真实 `IOException` 可恢复错误态；但在脚本要求“用 Tab/Shift+Tab 聚焦重试”时，第一条物理输入于 `13:45:57` 直接用鼠标点击了 `RetryAssetLibraryLoad`。四层诊断证明该点击已触发 attempt 2，并于约 20 ms 后进入真实 SQLite v6 / ready / 0 项。错误层因此正常消失，不是错误态未生成或 Retry 控件丢失。

旧人工包只查询历史中是否曾有 `error-visible`，又在该聚焦步骤通过后才建立 Retry activation baseline；所以提前发生的 Retry 被吞进 baseline，脚本继续等待已经消失的“错误态 + Retry 焦点”，直到五分钟超时。期间又发生了对 `ImportFromEmptyAssetLibrary` 的物理点击，并通过文件选择器向该 retry 会话的隔离数据库引用导入 1 个本地 PNG，最终画面显示 1 个普通素材。源文件没有被移动、改名、覆盖或删除，但该 run 已不再满足 synthetic-only，不能用于 Gate A。

人工包现改为严格顺序：release gate 后先要求用户保持前台且不操作，实时拒绝提前 Retry、attempt 2 或任何文件选择/导入；真实 attempt 1 错误态稳定后先自动捕获 10，再建立新的 activation baseline。随后一个动作步骤明确允许二选一：`Tab/Shift+Tab` 聚焦 Retry 后只按一次 Enter/Space，或鼠标只单击一次 Retry；两种方式只能选一种。若 Retry 多次激活、attempt 2 没有同一步骤的唯一四层输入，或 retry 会话出现任何导入，脚本立即 fail-closed，不再盲等五分钟。RecoveryTest 新增“干净 attempt 1 放行、提前 attempt 2 拒绝、file-picker 导入拒绝”三项动态门禁；人工包/证据工具/严格校验器聚焦测试 32/32 通过，PowerShell 5.1 AST、DryRun、RecoveryTest 与 diff check 均通过。

失败根仅保留在 `%TEMP%` 作诊断，`validator_started=false`，显示已恢复并复核为 `3840x2160@60Hz / 150%`，两个 DevPreview PID 均已退出。它不能续跑、不能与修复后证据拼接，也不能进入 Git。下一次必须从新的 clean HEAD、新的专属构建和新的 run root 从第 1 步全量重跑；Gate A 继续为 **BLOCKED（READY_FOR_MANUAL_RUN）**。

## 会话退出后的进程枚举竞态修复

2026-08-20 在 `6a99ab5fa2b81d3c6f81dc8a369a24680d3b17a2` 上的第四次真人 V2 Run 已真实完成 first-empty、08 自动捕获及第一个窗口的用户正常关闭。`close-first-empty` 于 `06:41:01.452Z` 根据该会话进程的 `HasExited` 通过；约 16 ms 后，下一会话启动前的瞬时全局名称检查仍短暂枚举到 1 个同名进程，因而 fail-closed。当前与事后复核均为 0 个残留 DevPreview；本轮没有启动 retry 会话，validator 未启动，显示前后均为 `3840x2160@60Hz / 150%` 且 `display_restored=true`。这不是用户漏关窗口，而是退出完成与 Windows 两套进程枚举视图收敛之间的竞态。

人工包不再用单次 `Get-Process` 判断清零。现在同时读取精确进程名的托管进程表和 `Win32_Process`，取 PID 并集；任何一个视图仍见进程都会重置稳定计时，只有两者连续 1000 ms 均为零才允许继续，最长等待 10 秒。枚举错误、持续存在的 PID、未达到完整稳定窗口都会继续失败并报告最后 PID；脚本不会忽略、过滤或自动结束残留软件。用户正常关闭路径在 `HasExited + WaitForExit` 后先经过该门，下一会话入口再经过同一门形成二次防线；单次 CIM 查询另有 2 秒操作超时，避免系统查询无限挂起。

RecoveryTest 已动态覆盖“残留后归零”“归零期间重新出现并重置计时”“持续非零必须超时拒绝”及本机双进程表连续稳定清零；PowerShell 5.1 AST、DryRun、RecoveryTest、人工包聚焦 6/6 与完整 WPF 864/864 均通过。第四次失败根只保留在 `%TEMP%` 作诊断；由于修复改变 HEAD 和专属 EXE 哈希，08 不能续用，下一次仍须新 run root 从第 1 步全量重跑。Gate A 继续为 **BLOCKED（READY_FOR_MANUAL_RUN）**。

## 最终 Run 的专属构建等待与正常关闭失败

2026-08-20 在 `6bc2f5b3e0dac31e28873c541366b090ffaaf411` 上按最终指令创建了全新 run root：`%TEMP%\PixelTart-P1-GateA-Manual-V2-20260820-162637-4af97c1430b140c184f295002709b6db`。本地分支、fetch 后远端分支与服务器公布 HEAD 均精确一致，tracked/untracked 工作树为 clean，启动前托管进程表和 `Win32_Process` 均为 0。

专属 publish 实际于 `16:26:41` 成功完成且 stderr 为空，但 Windows PowerShell 5.1 的 `Start-Process -Wait` 把整个子进程树纳入等待；四个 `/nodemode:1 /nodeReuse:true` MSBuild 节点在主 `dotnet publish` 退出后继续空闲存活，直到约 15 分钟后才自然回收。脚本随后正常启动 first-empty PID `13156`，真实 SQLite v6 / attempt 1 / 0 项状态通过，并于 `16:41:49` 成功生成本轮 08 PNG 与 window-evidence。

脚本进入 `close-first-empty` 后，Computer Use 对标题栏关闭按钮的首次动作和一次 fresh-state 重试都返回输入结果未知/失败；窗口及 PID 此后仍存活。该步骤在五分钟后以“等待用户正常关闭软件超时：PID 13156”失败，`finally` 安全清理才使窗口消失。因此用户随后观察到的“自己关闭”是失败清理，不是正常关闭门通过。本轮没有启动 retry 会话，只有 08，`validator_started=false`，显示前后均为 `3840x2160@60Hz / 150%` 且 `display_restored=true`，最终两套进程表均为 0。该 root 不能续跑或与下一轮证据拼接。

已按“仅修复本轮可复现代码缺陷”的边界，在提交 `e1739807d0e11d52da9125eecad567235843f9a4` 中只为 `Invoke-DedicatedBuild` 的等待进程临时设置 `MSBUILDDISABLENODEREUSE=1`，继续保留 `Start-Process -Wait`、stdout/stderr 重定向、完整 ExitCode 校验和非零失败。环境只在专属构建作用域生效并在返回后恢复；捕获器、validator 与普通构建路径不受影响。RecoveryTest 使用父进程 sentinel 动态验证作用域内值为 `1` 且退出后精确恢复，静态契约同时证明该精确覆盖只出现一次。

修复后真实专属 publish 用时 `2.686 s`，目标 EXE 存在，残留 node-reuse 进程为 0，环境恢复为 true。PowerShell 5.1 AST、DryRun、RecoveryTest、人工包聚焦 7/7、Debug/Release warnings-as-errors 均通过；完整回归为 Core `1192/1192`、WPF `865/865`、Modular Harness `14/14`、DPI `75/101`，其中 26 个失败仍全部来自既有 `artifacts/automated-dpi-review/2.0.4/*.json` 缺失。合计 `2172 total / 2146 passed / 26 failed / 0 skipped`，无新增失败。

本轮也明确证明，最终真实验收的关闭、Retry、splitter 和 DPI 必须由真人完成；Computer Use 输入不能替代附件要求的真人动作。下一轮必须以 `e1739807d0e11d52da9125eecad567235843f9a4` 或其报告提交后的 clean HEAD、新专属 EXE 和全新 run root 从第 1 步重跑。P1 Gate A 继续为 **BLOCKED（READY_FOR_MANUAL_RUN）**，不得进入 P2。

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

## 2026-08-27 后续修复与最新受控尝试

- 在 `e461904b5f97daaa04dab06827ffb48c923addf0` 前后完成了三项验收工具修复：前台捕获器在同一截止时间内重新校验并在瞬态漂移时重置稳定性；状态等待不再抢占捕获器的前台资格；PowerShell 5.1 严格模式下的空辅助窗口集合保持为空数组，不再因 `.Count` 诊断异常中断。
- 在隔离的 Windows PowerShell 5.1 模块路径下，聚焦契约测试为 `17/17`，完整 WPF 测试为 `919/919`；未隔离的宿主模块路径只产生环境级 `Get-FileHash` 解析错误，不计入代码失败。
- 运行根 `PixelTart-P1-GateA-Manual-V2-20260827-173432-a38fcc3785ea46b29b377505f99534cf`：08、09、10 已取得真实截图；Retry 超时是因为所需的真人键盘动作未完成，进程随后正常清理，没有闪退。
- 运行根 `PixelTart-P1-GateA-Manual-V2-20260827-180037-64099c8b35f442998495d6a91539c0c3`：08 已取得真实截图；`close-first-empty` 等待真人关闭窗口超过有界时限后失败，进程正常清理，没有闪退。该根和前述失败根均保留，未拼接、未重跑冒充完整链。
- 因而本次收尾仍明确为 **BLOCKED / READY_FOR_MANUAL_RUN**，`capture_status` 仍为 `not_captured`；未进入 P2，也未把 Computer Use 或后台清理替代真人关闭、Retry、splitter、DPI 操作。

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
