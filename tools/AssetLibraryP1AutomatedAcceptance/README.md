# P1 素材库全自动验收

这是一套与历史 `P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET` 分离的自动验收合同。它只声明 `validation_mode: automated`，人工体验烟测由所有者明确豁免；它绝不把应用内驱动、合成素材、DevPreview 或模拟布局写成真人、物理操作或真实显示切换。

从仓库根目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "./tools/AssetLibraryP1AutomatedAcceptance/Invoke-P1AssetLibraryAutomatedAcceptance.ps1" -Mode Run
```

入口由 Codex 无人值守运行，不使用 `Read-Host`、真人存在门、桌面键鼠注入、UI Automation Invoke、强制前台、坐标点击或 Windows 显示设置写入。运行产物只能写入被忽略的 `.validation` 或临时目录；历史 manual run 始终只读。

独立验证已有运行目录：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "./tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1" -RunRoot "<absolute-run-root>"
```

## 证据文件格式

- `run-manifest.json`：唯一 run 身份、源码 HEAD、严格排序的 9 个场景、全局安全计数，以及应用退出后的两套进程表、运行数据库与环境清理结果。
- `build-manifest.json`：Debug EXE / 素材库模块的绝对路径和 SHA-256，并记录按钮审计所用四个 Git blob 的对象 ID、SHA-256 与字节数。
- `plans/*.json`：每个隔离场景及其 primary / restart 阶段的输入计划。
- `app/evidence/events.ndjson`：应用内动作的追加式事件链；每行以 `previous_event_hash` / `event_hash` 串联。
- `app/evidence/summary.ndjson`：9 个 primary 与 3 个 restart 阶段的追加式摘要链；每行以 `previous_summary_hash` / `summary_hash` 串联。
- `app/evidence/summary.json` 与 `summary-*.json`：最终聚合索引及不可覆盖的阶段摘要，包含场景、截图、bounds、数据库快照引用和应用自身退出状态。
- `app/evidence/screenshots/**`、`bounds/**`、`databases/**`：由真实 WPF 进程写出的 PNG、布局矩形和关闭连接后的只读 SQLite v6 快照；所有路径都必须是 run-relative，且在摘要 artifact 列表中带 SHA-256 与阶段身份。
- `logs/**`：构建与每阶段进程输出，仅作诊断，不替代上述结构化证据。

合同、run / build manifest 和应用摘要声明 `automated_capture_status: captured`；事件、artifact、bounds 与数据库证据至少携带三项基础诚实标记及 `historical_manual_gate: not_closed_superseded_as_release_blocker`。验证器不会把自动捕获描述为真人证据。

验证器只读输入树，自行重算两条追加式哈希链、所有 artifact 哈希和四个不可变 Git blob，并用 Python 标准库以只读 URI 真实查询每个 SQLite 快照的 schema v6 与 `AssetItems` 数量。它拒绝缺截图、拼接其他 run/PID/HWND/hash 的证据、非 v6 数据库、重复 Retry、直接宽度或 settings 改写、非选择恢复场景的导入、run root 外合成来源、进程残留、模拟 DPI 越界，以及任何桌面输入、真实显示、Eagle 或用户文件访问计数。

`layout-dpi-buttons/v1` 只表示应用内模拟布局/DPI 矩阵，不表示真实 Windows 显示设置被切换。`pane-collapse-expand/v1`、`thumbnail-slider/v1`、`selection-navigation-restart/v1` 各有同一隔离场景根下的第二个进程，PID / HWND 必须与 primary 不同。只有选择恢复场景可通过公开应用路径导入位于本次 run root 内的 synthetic 素材；其他场景导入数必须为 0。
