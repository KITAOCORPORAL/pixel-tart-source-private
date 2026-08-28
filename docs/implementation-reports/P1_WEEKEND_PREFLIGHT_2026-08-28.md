# Pixel Tart P1 周末无人值守预检

日期：2026-08-28
分支：`feature/modular-harness-v1-p1`
指令起始 HEAD：`cae44dbf7217273bc1af56a9dd6b508737b51c35`
实际起始 HEAD：`cae44dbf7217273bc1af56a9dd6b508737b51c35`
最终 clean-code 测试 HEAD：`e50275a43cd7a17958cf8afce256586af8624c1e`

## 结论

P1 本轮代码、验收脚本和诊断预检通过；没有启动 `-Mode Run`，没有要求真人操作，也没有修改显示设置、用户素材、Eagle `.library`、正式 Schema 5 或素材库 schema v6。

真实 Gate A 仍为 **READY_FOR_MANUAL_RUN**。历史 DPI 自动证据套件仍有 26 项缺证失败；失败数量、测试名称与旧 TRX 完全相同，分类为“历史环境债（不阻塞本轮代码）”，没有隐藏、跳过或改写为通过。

所有原始日志、TRX 和机器可读汇总位于：

`.validation/P1-Weekend-Preflight-20260828-fc317ad7c66d/`

## Gate 汇总

| Gate | 状态 | 实测结果 |
| --- | --- | --- |
| 仓库与版本一致性 | PASS | 最终 fetch 成功；远端跟踪分支仍为指令起始 HEAD，当前分支只新增本轮 3 个提交，无其他功能分支合入。 |
| PowerShell 5.1 | PASS | 6/6 个相关脚本 AST 解析，0 error；无 PowerShell 7 专用前提。 |
| DryRun / RecoveryTest | PASS | clean commit 上连续 3 轮、共 6 次，全部 exit 0；manifest 状态正确。 |
| 状态与严格证据契约 | PASS | Gate A evidence 契约测试 74/74；正向只接受完整合成 fixture，负向均 fail-closed 且输入树不变。 |
| 按钮可读性与角色 | PASS | XAML 共 27 个 Button；裸/隐式样式 0；Primary 4、Secondary 12、Chip 9、Icon 1、PaletteSwatch 1。 |
| 可访问性与焦点 | PASS | 标准按钮采用固定占位深浅双焦点框；文本、关键边框及深色/浅色/高对比主题契约通过。 |
| 键盘链 | PASS | Retry、两个 splitter、缩略图 Slider、两个栏位 Toggle 均有唯一 AutomationId、显式名称/可聚焦声明和诊断路径；正反向 Tab 与无输入不改变状态已验证。 |
| 运行时卫生 | PASS | DevPreview 始终为 0；显示和环境变量不变；6 个自动 run root 均无 DB/WAL/SHM；提交差异无运行产物、密钥或机器绝对路径。 |
| 历史 DPI 自动证据 | BLOCKED（历史债） | 75/101 passed、26 failed、0 skipped；与旧 `final-head-33ed7b1` TRX 的 26 个失败名称完全一致，均因 `artifacts/automated-dpi-review/2.0.4/*.json` 缺失。 |
| 真实 Gate A | READY_FOR_MANUAL_RUN | 本任务明确未调用 `-Mode Run`；没有生成同一次真实 08～11、splitter、四组 DPI 和 validator=0 的闭环。 |

## 可复现缺陷与最小修复

### 1. 浅色背景上的标准按钮焦点框不足以稳定达到 3:1

- 原始证据：旧单色薄荷焦点框在浅色主题表面低于非文本关键视觉 3:1 门槛。
- 修改：只在素材库局部模板中预留固定 3 DIP 焦点区域，改为 `#061311` 外框和 `#F5FFFC` 内框；聚焦前后不改布局。
- 新测试：完整 27 按钮角色清单、任意表面亮度的互补双框 3:1 证明、深色/浅色/高对比主题检查。

### 2. A7 键盘目标的显式元数据和诊断不完整

- 原始证据：缩略图 Slider 无显式可访问名称；栏位 Toggle 的 Enter/Space 会被 Retry 专用早退逻辑排除；Slider transition 的预期方向为空。
- 修改：六个 A7 目标显式锁定 AutomationId、名称、Focusable/IsTabStop；Slider 左右键映射到 Decrease/Increase；两个 Toggle 纳入完整 WPF 四事件与 Button.Click 关联。
- 新测试：真实 WPF 正向/反向 Tab 顺序、无输入不改变宽度/折叠/缩略图、Slider Right 写回、Toggle 早退防回归。聚焦集 42/42，完整 WPF 936/936。

### 3. 严格 validator 的 Retry 污染负例不够完整

- 原始证据：retry 隔离根内的 file-picker/import 诊断没有被最终 validator 逐字段拒绝；混合 PSModulePath 还会让 WinPS 5.1 子进程加载不兼容模块。
- 修改：validator 对 retry 会话的精确 `InputDiagnostics/asset-library-import.json` fail-closed；测试子进程使用纯 Windows PowerShell 模块路径。
- 新测试：缺 08、缺 09/10/11、提前/重复 Retry、file picker/import 污染、未批准辅助窗口、缺 splitter WPF 层、DPI 未恢复、PNG hash 不一致等负例；每例验证输入树不变。最终 74/74。

### 4. V2 人工包不能安全用于无人桌面交接

- 原始证据：V3 红测首次为 11/14；缺少 60 秒真人存在门、可见倒计时、结构化超时末态、READY 安全退出、只读 `ValidateExistingRun`、EXE/DLL 双哈希和北尾 README。
- 修改：生成 V3；`Run` 构建后先执行 60 秒同采样真人输入/前台门，证据不足只写 READY 且不启动 GUI；每步显示单条中文提示和剩余时间；超时先写诊断再清理；增加只读验证模式和 DLL 哈希复核。
- 新测试：V3 静态/动态契约、DryRun、RecoveryTest、ValidateExistingRun 不变性和无 GUI 检查。最终人工包测试 14/14。

## 最终 clean commit 自动矩阵

以下命令均在 `e50275a43cd7a17958cf8afce256586af8624c1e`、tracked clean 状态执行。完整命令行和原始输出见 `final-clean/`。

| 项目 | 命令摘要 | Exit | 耗时 | 结果 |
| --- | --- | ---: | ---: | --- |
| Debug build | `dotnet build RAWSelectionAssistant.sln -c Debug --no-restore -p:TreatWarningsAsErrors=true` | 0 | 22.528 s | 0 warning / 0 error |
| Release build | 同上，`-c Release` | 0 | 22.158 s | 0 warning / 0 error |
| 诊断 DevPreview build | `dotnet build ...RAWSelectionAssistant.csproj -p:InputRoutingDiagnostics=true -p:ModularHarnessDevPreview=true` | 0 | 9.310 s | 0 warning / 0 error |
| Core | `dotnet test tests/RAWSelectionAssistant.Tests` | 0 | 45.378 s | 1192/1192 passed |
| 全部 WPF | `dotnet test tests/RAWSelectionAssistant.WpfTests` | 0 | 106.738 s | 936/936 passed |
| Modular Harness | `dotnet test tests/PixelTart.ModularHarness.Tests` | 0 | 3.019 s | 14/14 passed |
| DPI | `dotnet test tests/RAWSelectionAssistant.DpiTests` | 1 | 1.567 s | 75 passed / 26 historical missing-evidence failures / 0 skipped |

合计：`2243 total / 2217 passed / 26 historical evidence failures / 0 skipped`。

WPF TRX 内的重点类：Button 8/8、Gate A evidence 74/74、manual packet 14/14、physical pointer 9/9、Embedded Asset Library 25/25、load-state 7/7、state seam 6/6、evidence tool 5/5。

## 三轮自动模式卫生

最终 clean commit 连续三轮 DryRun → RecoveryTest 的 `execution-envelope.json` 与 `summary.json` 位于 `final-clean-dry-recovery/`：

- 6 次执行全部 exit 0；DryRun=`dry-run-passed`，RecoveryTest=`recovery-test-passed`。
- 执行前、每个阶段后及最终复核，Get-Process/CIM DevPreview 均为 0。
- 显示始终为 `3840×2160@60Hz / 150% / DPI 144`。
- TEMP/TMP、MSBuild node reuse 变量和 7 个 P1 接受变量前后逐值一致。
- 所有 run root 递归检查 `.db/.db-wal/.db-shm` 均为 0；6 个 stderr 均为空。
- 执行前后 HEAD 相同、tracked 状态为空，入口脚本 SHA-256 不变。

## 静态与卫生证据

- `static-audit/08-static-audit-summary-v4-final.txt`：WinPS AST 0、过期非零 SHA 0、前台写/输入注入/UIA Invoke/显示写/Eagle 写/用户源文件直接写均为 0。
- `static-audit/07-asset-library-buttons-v4-final.tsv`：27 个按钮的 AutomationId、文案/用途和样式角色清单。
- `final-clean/07-dpi-baseline-comparison.json`：当前与旧基线均为 101/75/26/0，新增失败 0、移除失败 0。
- `final-clean/08-runtime-hygiene.txt`：`git diff --check=0`、运行产物 0、机器路径 0、密钥模式 0。

## 预检状态边界

合成 fixture 只证明 validator 和证据契约能正确通过/拒绝，不是真实 Gate A。正式契约的 `capture_status` 仍为 `not_captured`。只有一次真实 `-Mode Run` 同时完成全部状态、键盘、DPI、恢复并得到 validator=0 和 Harness runner=0 后，P1 才能改为 PASS/CLOSED。
