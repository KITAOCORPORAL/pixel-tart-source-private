# P1 历史人工 Gate A 替代说明

日期：2026-08-31

分支：`feature/modular-harness-v1-p1`

自动验收代码 HEAD：`27b8811f1911c592483eb3d0eadf209ab13f7940`

## 决定

自本报告起，P1 的 release blocker 由独立的自动验收门承担。项目状态必须按以下四项一起读取，不能省略限定词：

| 状态项 | 结论 |
| --- | --- |
| P1 Automated Acceptance | **PASS** |
| Manual UX Smoke | **OWNER_WAIVED** |
| Historical Manual Gate A | **NOT_CLOSED (superseded as release blocker)** |
| P1 | **CLOSED_FOR_AUTOMATED_ACCEPTANCE** |

这不是把历史人工 Gate A 改成 PASS。`OWNER_WAIVED` 是 owner 对额外真人 UX 烟测的明确豁免；它不等于“真人已操作”或“真人已验证”。

## 历史证据保持不动

- 原 `P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET`、`gate-a-evidence-contract.json` 和历史 V2/V3 run 的语义保持不变。
- 历史 contract 的 `capture_status=not_captured` 没有被改成 `captured`。
- 旧 run root、截图、JSON 和日志没有被修改、补写、拼接、覆盖或删除。
- 历史人工失败只作诊断输入，不作为 2026-08-31 自动 PASS 的证据。
- 新自动验收使用独立 contract、runner、validator、run root 和 hash 链，并明确声明 `validation_mode: automated`、`owner_manual_ux_smoke: waived`、`manual_evidence_claimed: false`。

旧报告中 `BLOCKED` / `READY_FOR_MANUAL_RUN` 描述的是当时“必须完成真人 Gate A”的旧 release contract，仍然是准确历史记录。它们不应被事后改写；本报告只说明从 2026-08-31 起该旧门已被新的自动验收门替代为 release blocker。

## 为什么替代

多轮人工包把内部 QA 操作转移给 owner，并反复受前台焦点、窗口关闭、显示切换和脚本字段契约影响。继续要求 owner 重复按键、拖动、切换显示或关闭窗口，不能提高 P1 证据的可重放性。

新的自动门在运行的 DevPreview 中通过公开应用内 acceptance seam、WPF Dispatcher 和真实隔离 SQLite v6 repository 执行同一 P1 逻辑，并由独立 validator 重新核验实际证据、二进制、数据库、命令次数、清理和安全边界。它不使用桌面键鼠注入、UIAutomation Invoke 或真实显示设置写 API，也不伪称物理输入。

完整实现、矩阵、三连 run、证据哈希和安全审计见 `docs/implementation-reports/P1_AUTOMATED_ACCEPTANCE_CLOSURE_2026-08-31.md`。

## 历史字段故障的处理边界

最后一次相关 V3 失败暴露的是人工包自身的字段生产/消费契约错误：`Wait-KeyTransitionStep` 已找到并验证 `Attempt` / `Transition` match，但旧 `New-ProbeResult` 返回形状没有携带这两个字段；`Get-NewTransitionWidth` 在旧脚本 line 1340 消费时因字段缺失而终止。

`18a8cb2ffe66de68cef12c856730f4309e9631e5` 已在生产端保留两个对象、在消费端严格要求并校验字段，同时新增正向、缺失/畸形负向和旧形状兼容性测试。修复没有修改失败 run，也没有通过默认值、忽略字段或放宽 validator 绕过问题。

这项修复继续保留，以便将来有人审计人工包；但它不构成重新启动人工 Gate A 的理由。人工包的历史状态仍是 `NOT_CLOSED`。

## 解释规则

允许的表述：

- “P1 自动验收通过。”
- “Manual UX Smoke 由 owner 豁免。”
- “历史人工 Gate A 未闭合，已不再作为 release blocker。”
- “P1 已关闭于自动验收范围。”

不允许的表述：

- “真人验收通过。”
- “物理 Gate A 已通过。”
- “历史人工证据已补齐。”
- “自动化证明了真人鼠标/键盘体验。”

## 后续边界

本次替代只关闭 P1 的自动验收门，不实现 P2～P6，也不改变产品数据边界。下一步可以另行创建 P2 分支；在新的明确任务开始前，不在本分支继续开发 P2。
