# Pixel Tart P1 周末冻结审计与真人验收待机

日期：2026-08-28
分支：`feature/modular-harness-v1-p1`
远程：`source-private`（`KITAOCORPORAL/pixel-tart-source-private`）
指令 HEAD：`17841c01933374bc0ee5467f1e45a4f0710a2e09`
实际起始 HEAD：`17841c01933374bc0ee5467f1e45a4f0710a2e09`
最终审计代码 HEAD：`a8aa46847be486f31fac3f0b2a44ae9cc3fe6983`

> 本报告自身的文档提交位于上述审计代码 HEAD 之后，无法在自身内容中自引用最终提交 SHA。最终远端 delivery HEAD 以任务最终回传和 `git ls-remote` 核验为权威。

## 结论

本轮完成冻结审计、一个最小证据契约修复、修复后全矩阵和无人值守卫生验证。没有运行正式 `-Mode Run`，没有启动验收 GUI，没有修改显示设置，没有模拟键鼠或强制前台，没有进入 P2～P6。

真实 Gate A 状态仍为：

**READY_FOR_MANUAL_RUN**

正式 contract 仍为 `capture_status=not_captured`。这不是 PASS，也不是 CLOSED；周末无需看守。

## 版本锁定与工作树

- 仓库没有名为 `origin` 的远程；唯一配置远程是 `source-private`。因此按实际远程执行 `fetch --prune` 与 `pull --ff-only`。
- 抓取后本地与远程均精确为指令 HEAD `17841c01933374bc0ee5467f1e45a4f0710a2e09`，ff-only 返回 `Already up to date.`。
- 起始 tracked 工作树为空；无 `AGENTS.md` 或 `SECURITY.md`。
- 没有 reset、clean、force push、merge 或覆盖未知工作树。
- 代码修复形成独立 clean commit `a8aa46847be486f31fac3f0b2a44ae9cc3fe6983`。
- 版本锁定原始日志位于 `.validation/P1-Weekend-Freeze-20260828-17841c0/version-lock/`。

## 报告、人工包与 contract 一致性

| 项目 | 结果 | 证据与判断 |
| --- | --- | --- |
| Gate A 状态 | PASS | 两份 2026-08-28 报告、历史 closure、README 与 contract 均未把真实 Gate A 写成 PASS/CLOSED。 |
| HEAD 逻辑 | PASS | 运行时从 clean current HEAD 取得并交叉验证；无过期非零 40 位 SHA 硬编码，只有 RecoveryTest 全零 sentinel。 |
| EXE / Asset DLL 身份 | FIXED | 审计发现入口虽生成并复核双 hash，但正式 contract/validator 原先只闭合 EXE；本轮将 Asset DLL 身份纳入 fail-closed contract。 |
| README 根目录命令 | PASS | 包内 README 的单条命令从仓库根可直接运行；包内只有入口 `.ps1` 与 README 两个文件。 |
| V3 真人存在门 | PASS | 60 秒摘要、真人新输入 + 控制台前台 + 交互桌面组合门、安全 READY 退出均存在。 |
| 提示、倒计时与单次重试 | PASS | 单条中文动作提示、每 5 秒倒计时、唯一 Retry、重复动作 fatal、结构化超时摘要均存在。 |
| 自动模式边界 | PASS | DryRun/RecoveryTest 不启动 GUI、不改显示、不写用户数据；ValidateExistingRun 在工具快照存在时只读。 |
| DPI 数字 | PASS | 所有报告仍明确 `101 total / 75 pass / 26 historical missing-evidence fail / 0 skipped`。 |
| 旧 run 隔离 | PASS | 旧失败 run 只作诊断，不与新 run 拼接；入口要求正式 Run 使用新的 TEMP root。 |

## 唯一代码修复：Asset DLL 身份闭环

失败证据：Run 入口的 build/manual manifest 已写入 `asset_module_path` 与 `asset_module_sha256`，且 Run 内会复核 Asset DLL；但 `gate-a-evidence-contract.json` 没有声明该身份，原 validator 与事后 ValidateExistingRun 不能拒绝缺失、改名、内容被改或 manual/build 不一致的 DLL。

最小修改：

- Contract 增加预期模块名 `PixelTart.Modules.AssetLibrary.dll`、大写 SHA-256 格式、磁盘实体 hash 必须匹配、manual/build 身份必须一致。
- Validator 校验 Asset DLL 绝对路径、精确文件名、与 EXE 同目录、文件存在、实体 SHA-256，以及 manual/build path/hash 完全一致。
- 新增 missing file、wrong name、mutated bytes、manual path mismatch、manual hash mismatch 五个 fail-closed 负例；每例沿用输入树前后指纹不变检查。

修改文件仅有：

- `tools/AssetLibraryP1Acceptance/gate-a-evidence-contract.json`
- `tools/AssetLibraryP1Acceptance/Test-AssetLibraryP1GateAEvidence.ps1`
- `tests/RAWSelectionAssistant.WpfTests/AssetLibraryP1GateAEvidenceContractTests.cs`

验证：目标 Gate A evidence tests 从 74 增至 **79/79**；完整 WPF **941/941**；Windows PowerShell 5.1 AST 0；contract JSON 可解析；`git diff --check` 通过。没有改入口的真人动作、产品代码、数据库 Schema 5、素材库 schema v6 或全局主题。

回滚点：仅撤回此修复时，revert `a8aa46847be486f31fac3f0b2a44ae9cc3fe6983`；其父提交为 `17841c01933374bc0ee5467f1e45a4f0710a2e09`。

## 构建与测试矩阵

第一次环境探测错误解析到了没有 SDK 的系统 `dotnet.exe`，54 ms 内以宿主错误退出，编译器未启动。分析 stderr 后改用工作区已配置的 SDK 10.0.302；下表是修复后 clean commit 上唯一一次有效矩阵。后续构建禁用节点复用与共享编译，避免空闲 MSBuild 子节点拖住外层等待。

| 项目 | 命令摘要 | Exit | 耗时 | 结果 | 日志 |
| --- | --- | ---: | ---: | --- | --- |
| Debug build | `dotnet build RAWSelectionAssistant.sln -c Debug --no-restore ... TreatWarningsAsErrors=true` | 0 | 22.027 s | 0 warning / 0 error | `final-matrix/01-build-debug.*` |
| Release build | 同上，`-c Release` | 0 | 21.489 s | 0 warning / 0 error | `final-matrix/02-build-release.*` |
| 诊断 DevPreview build | 产品项目 + `InputRoutingDiagnostics=true` + `ModularHarnessDevPreview=true` | 0 | 9.019 s | 0 warning / 0 error | `final-matrix/03-build-diagnostics-devpreview.*` |
| Core | `dotnet test tests/RAWSelectionAssistant.Tests ...` | 0 | 44.484 s | 1192/1192 | `final-matrix/04-test-core.*`、`trx/core.trx` |
| 全部 WPF | `dotnet test tests/RAWSelectionAssistant.WpfTests ...` | 0 | 102.925 s | 941/941 | `final-matrix/05-test-wpf.*`、`trx/wpf.trx` |
| Modular Harness | `dotnet test tests/PixelTart.ModularHarness.Tests ...` | 0 | 1.721 s | 14/14 | `final-matrix/06-test-harness.*`、`trx/harness.trx` |
| DPI | `dotnet test tests/RAWSelectionAssistant.DpiTests ...` | 1 | 1.213 s | 75 pass / 26 historical fail / 0 skipped | `final-matrix/07-test-dpi-expected-history.*`、`trx/dpi.trx` |

日志根：`.validation/P1-Weekend-Freeze-20260828-17841c0/`。

总计：**2248 total / 2222 passed / 26 historical missing-evidence failures / 0 skipped**。

WPF 重点类：Button 8/8、evidence tool 5/5、Gate A evidence 79/79、load state 7/7、manual packet 14/14、state seam 6/6、Embedded Asset Library 25/25、physical pointer 9/9。

## 历史 DPI 26 项

当前 DPI TRX 与旧 `final-head-33ed7b1` TRX 均为 `101/75/26/0`：

- 26 个失败测试名逐项完全一致。
- 新增失败 0，移除失败 0，失败原因变更 0。
- 26 条当前失败全部仍是 `artifacts/automated-dpi-review/2.0.4/` 下历史 JSON 证据缺失。
- 没有删除、Skip 或改写为通过。

权威比较：`.validation/P1-Weekend-Freeze-20260828-17841c0/final-matrix/08-dpi-baseline-comparison.json`。

## 三轮 DryRun / RecoveryTest

连续三轮共 6 次，全部 exit 0：DryRun 均为 `dry-run-passed`，RecoveryTest 均为 `recovery-test-passed`。

- 耗时依次为 0.44 / 1.76 / 0.43 / 1.75 / 0.42 / 1.74 秒。
- 每个阶段前后，Get-Process 与 Win32_Process 的 DevPreview 数量均为 0。
- 显示始终为 `3840×2160@60Hz / 150% / DPI 144`。
- TEMP/TMP、MSBuild node reuse 与全部 P1 验收环境变量逐值不变。
- 六个 stderr 均为空；所有 run root 的 DB/WAL/SHM 数量均为 0。
- 入口 SHA-256、HEAD 与 tracked clean 状态前后不变。
- `formal_run_mode_invoked=false`。

权威汇总：`.validation/P1-Weekend-Freeze-20260828-17841c0/dry-recovery/summary.json`。

## 旧 V2 run 的只读复验

报告列出的两个旧 V2 失败 run root 均仍存在。按“一次且不重试”规则，只对第一根执行一次 `ValidateExistingRun`：

- Exit 1，精确原因：旧 V2 root 不含 V3 创建时冻结的 `validation/tool` 快照。
- 这是安全 fail-closed，不允许回退到当前工具冒充旧工具，也不允许向旧 root 补写快照。
- 文件树 337 → 337；相对路径、大小和 SHA-256 差异 0。
- manifest SHA-256 前后相同；两个进程表均为 0；显示状态完全一致。
- 只读不变量 PASS，但命令结果如实记为 **FAIL_CLOSED_V2_NO_TOOL_SNAPSHOT**，没有改写成成功。

权威汇总：`.validation/P1-Weekend-Freeze-20260828-17841c0/validate-existing-v2-readonly/summary.json`。

新的 V3 Run 会在创建时冻结修复后的 contract/validator，因此未来同一 V3 run 的 ValidateExistingRun 会自动包含 Asset DLL 身份复验。

## 静态安全与运行时卫生

- Windows PowerShell 5.1：6/6 脚本 AST，0 parse error。
- 当前 39 个生产/工具文件复扫：前台变更、输入注入、UIAutomation Invoke/SetFocus、显示写、Eagle 写、直接用户源文件写均为 0。
- 新增 validator 仅读取 manifest、检查路径/存在性并执行 SHA-256；文件写、网络与 UI 操作均为 0。
- 生产验收脚本中过期非零 40 位 SHA 为 0；仅保留 RecoveryTest 全零 sentinel。
- Asset Library Button 27 个：Primary 4、Secondary 12、Chip 9、Icon 1、PaletteSwatch 1；裸 Button 0。
- A7 六目标均具有唯一 AutomationId、可访问名称、Focusable/IsTabStop、Tab 与无输入负向路径。
- 最终未观察到 DevPreview/验收 GUI 残留、显示变化、环境泄漏、用户数据写入或安全边界违规。

静态汇总：`.validation/P1-Weekend-Freeze-20260828-17841c0/static-audit/summary.json`。整轮 machine summary：`.validation/P1-Weekend-Freeze-20260828-17841c0/machine-summary.json`。

## 最终人工包与唯一下一步

人工包：`artifacts/manual-acceptance/P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET/`

README 完整，包内没有 BAT 或多余入口。从仓库根目录只执行一次：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\artifacts\manual-acceptance\P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET\Invoke-P1AssetLibraryGateAManualAcceptance.ps1" -Mode Run
```

只有北尾回来后、能全程真人看守时才执行。若失败，只回传最后显示的完整 `run root`。周末无需看守。

## 风险、限制与停止条件

- 真实 08～11、唯一 Retry、splitter/折叠/缩略图、重启恢复、四组真实 DPI 和最终基线恢复尚未由同一个 V3真人 run 完成。
- 历史 DPI 自动套件仍缺 26 项 JSON 证据；这是保留的环境债，不是真实 DPI Gate 通过。
- 旧 V2 root 没有不可追溯补造的 V3 工具快照，只能 fail closed；不能作为新 V3 证据。
- 本轮未读写 Eagle `.library` / `metadata.json` / `.info`，未读取、移动、重命名、覆盖或删除用户素材/源文件。
- 未修改正式 Schema 5、素材库 schema v6、全局主题或素材库产品范围。
- P1 未真人闭环，因此禁止进入 P2～P6。本轮到此停止。
