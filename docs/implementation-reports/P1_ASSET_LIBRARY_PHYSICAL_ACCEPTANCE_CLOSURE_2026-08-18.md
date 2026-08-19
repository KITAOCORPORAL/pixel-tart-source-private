# 素材库 P1 Gate A 最终物理验收记录（BLOCKED）

日期：2026-08-19

分支：`feature/modular-harness-v1-p1`

本轮起始 HEAD：`fab900d8478cbe8b8712a212cb7b2e32a702cd55`

P0：`140e34348000174986c6e503dcedff8f90a78c34`

P1 实现：`b4bd38f53d6a44756289eeda8bfc4feb343443c7`

## 结论

P1 Gate A 状态为 **BLOCKED**，`capture_status` 继续保持 `not_captured`，不得进入 P2～P6。

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
