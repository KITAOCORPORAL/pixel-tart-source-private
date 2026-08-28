# Pixel Tart P1 周末稳定化与最终人工交接

日期：2026-08-28
分支：`feature/modular-harness-v1-p1`
指令起始 HEAD：`cae44dbf7217273bc1af56a9dd6b508737b51c35`
实际起始 HEAD：`cae44dbf7217273bc1af56a9dd6b508737b51c35`
最终 clean-code 测试 HEAD：`e50275a43cd7a17958cf8afce256586af8624c1e`

> 报告提交会在上述 clean-code HEAD 之后只增加文档；最终远端 delivery HEAD 以本任务最终回传和 `git ls-remote` 为权威。本报告不把自身提交伪写成可自引用 SHA。

## 交付结论

P1 代码和自动验收基础已稳定，V3 人工包可一次性交给真人执行。**本任务没有运行真实 `-Mode Run`**，因此真实 Gate A 状态为：

**READY_FOR_MANUAL_RUN**

不是 PASS，也不是 CLOSED。没有复用或拼接旧 08～11、旧 DPI、合成 fixture 或 Computer Use 来替代真人闭环。

## 提交与回滚点

| 提交 | 内容 | 可回滚边界 |
| --- | --- | --- |
| `d0db1f6db46891fea081b560268b36bb8998345e` | `fix(asset-library): harden accessible keyboard controls` | 素材库局部焦点、A7 键盘元数据与诊断、对应 WPF 测试 |
| `21ba833fd354604ba49253cf49ebce6de64c59c5` | `fix(acceptance): harden Gate A evidence validation` | validator Retry 污染检查和负向 fixture 测试 |
| `e50275a43cd7a17958cf8afce256586af8624c1e` | `fix(acceptance): add unattended Gate A V3 handoff` | V3 入口、READY/倒计时/超时诊断/只读验证、北尾 README |

要完整撤回本轮代码，回滚到父检查点 `cae44dbf7217273bc1af56a9dd6b508737b51c35`；推荐按上表逆序 revert，避免覆盖未知工作树。

## 修改文件

产品局部与诊断：

- `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml`
- `src/RAWSelectionAssistant/MainWindow.PhysicalPointerDiagnostics.cs`
- `src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs`

验收工具与人工包：

- `tools/AssetLibraryP1Acceptance/Test-AssetLibraryP1GateAEvidence.ps1`
- `artifacts/manual-acceptance/P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET/Invoke-P1AssetLibraryGateAManualAcceptance.ps1`
- `artifacts/manual-acceptance/P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET/README_给北尾.md`

测试：

- `tests/RAWSelectionAssistant.WpfTests/AssetLibraryButtonReadabilityContractTests.cs`
- `tests/RAWSelectionAssistant.WpfTests/AssetLibraryP1GateAEvidenceContractTests.cs`
- `tests/RAWSelectionAssistant.WpfTests/AssetLibraryP1ManualPacketV2ContractTests.cs`
- `tests/RAWSelectionAssistant.WpfTests/EmbeddedAssetLibraryWpfTests.cs`
- `tests/RAWSelectionAssistant.WpfTests/PhysicalPointerDiagnosticContractTests.cs`

报告：

- `docs/implementation-reports/P1_WEEKEND_PREFLIGHT_2026-08-28.md`
- `docs/implementation-reports/P1_WEEKEND_STABILIZATION_AND_MANUAL_HANDOFF_2026-08-28.md`

## 未修改的产品边界

- 未修改正式 Schema 5、素材库 schema v6、表、索引或用户素材行。
- 未读取、写入、移动、重命名、覆盖或删除 Eagle `.library` 和用户源文件。
- 未改全局主题；按钮变化仅位于素材库局部资源。
- 未加入自动前台抢占、SendInput、UIAutomation Invoke 或显示设置写 API。
- 未实现或合入 P2～P6 的视图、组织树、拖放、右键、搜索、Viewer、批量导入/导出、回收站或 Eagle Adapter。

## 修复证据链

1. 标准按钮旧单色焦点框在浅色表面不足 3:1 → 改为固定占位深浅双框 → 27 按钮完整角色/对比度测试及相关聚焦测试通过。
2. Slider 名称/方向和两个 Toggle 的键盘诊断不完整 → 显式 A7 元数据、正反向 Tab、四个 WPF 键事件与 Click 关联 → 聚焦集 42/42、全 WPF 936/936。
3. validator 未逐字段拒绝 Retry 导入污染 → 精确读取 retry 隔离根诊断并 fail-closed → Gate A evidence 74/74，新增负例均验证输入树不变。
4. V2 缺无人桌面安全门与可诊断超时 → V3 增加 60 秒同采样真人存在门、READY 安全退出、倒计时、结构化超时、EXE/DLL 双哈希和只读 ValidateExistingRun → manual packet 14/14，clean commit 三轮自动模式全部通过。

## 自动矩阵

详表、命令、退出码、耗时和日志位置见 `P1_WEEKEND_PREFLIGHT_2026-08-28.md`。最终 clean-code 结果：

- Debug / Release warnings-as-errors：均 0 warning / 0 error。
- 启用 InputRoutingDiagnostics + ModularHarnessDevPreview 的产品构建：0 warning / 0 error。
- Core：1192/1192。
- WPF：936/936。
- Modular Harness：14/14。
- DPI：75/101；26 个历史证据文件缺失失败；0 skipped。
- 合计：2243 total / 2217 passed / 26 historical evidence failures / 0 skipped。
- 三轮 DryRun + RecoveryTest：6/6 exit 0；DevPreview、显示、环境和 DB/WAL/SHM 卫生全部通过。

## 历史 DPI 26 项对照

当前 `final-clean/trx/final-dpi.trx` 与旧 `.validation/final-head-33ed7b1/dpi/dpi.trx` 对比：总数均为 101，通过均为 75，失败均为 26，跳过均为 0；26 个失败测试名称完全相同。当前失败全部是 `artifacts/automated-dpi-review/2.0.4/` 下以下历史证据缺失：

- `AutomatedDpiScreenshotHashes.json`
- `AutomatedDpiResults.json`
- `LayoutBoundsResults.json`
- `ThemeResults.json`
- `SourceFileIntegrity.json`

这 26 项保留为历史环境债，不能代表真实四组 DPI 已通过，也不阻塞本轮代码修复。

## 已知失败与 run root

本任务没有新的真实 V3 失败 run root，因为正式 `-Mode Run` 从未启动。三轮 DryRun/RecoveryTest 均成功，其完整 `%TEMP%` roots 记录在 ignored `final-clean-dry-recovery/summary.json`。

继承的最近真实 V2 未闭环根仍保持原结论、不得拼接：

- `%TEMP%\PixelTart-P1-GateA-Manual-V2-20260827-173432-a38fcc3785ea46b29b377505f99534cf`：08/09/10 存在，Retry 真人动作未完成。
- `%TEMP%\PixelTart-P1-GateA-Manual-V2-20260827-180037-64099c8b35f442998495d6a91539c0c3`：08 存在，真人关闭动作超时。

## 最终人工包

目录：`artifacts/manual-acceptance/P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET/`

目录精确包含一个 PowerShell 入口和一个 `README_给北尾.md`，没有 BAT。真人只需从仓库根执行一次：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\artifacts\manual-acceptance\P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET\Invoke-P1AssetLibraryGateAManualAcceptance.ps1" -Mode Run
```

启动后在 60 秒内保持 PowerShell 前台并移动鼠标一次；随后每次只按当前一条中文提示完成一个动作。失败或安全停止时，只回传屏幕最后显示的完整 `run root`。

`-Mode ValidateExistingRun -RunRoot <path>` 只用于事后只读验证；它不启动 GUI、不写入或删除目标树。

## 下一条允许做什么

只允许真人执行一次新的 V3 `-Mode Run`。只有同一 run 完成 08～11、唯一 Retry、两个 splitter、折叠/展开、缩略图、重启恢复、四组真实 DPI、最终显示恢复，且严格 validator=0、随后同 run Harness runner=0，才能把 P1 改为 PASS/CLOSED。

本轮没有进入 P2，因为 P1 的真实 Gate A 尚未闭环，且本指令明确禁止 P2～P6。
