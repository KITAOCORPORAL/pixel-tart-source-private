# 测试与 Release

| 验证 | 通过 | 失败 | 跳过 | 记录 |
|---|---:|---:|---:|---|
| Core 全量最终 | 1625 | 0 | 6 | core-delivery.trx |
| WPF 全量最终 | 1520 | 0 | 13 | wpf-release.trx |
| 新源码命令专项 | 38 | 0 | 0 | release-current.trx |
| 标注/版本/恢复专项 | 8 | 0 | 0 | undo-boundary.trx |
| 两进程 CAS/ABA/锁释放/重启重试 | PASS | — | — | PROCESS_RESULTS.txt |
| 跨进程旧撤销/目录过期提交 | PASS | — | — | CRASH_PROCESS_RESULTS.txt |
| 五阶段导出终止重启 | PASS | — | — | CRASH_PROCESS_RESULTS.txt |
| 并发恢复 | PASS | — | — | 一进程 Running、一进程 Completed，重试 Completed |

计数重叠不相加。TEST_SUMMARY.json 保存本轮所有 TRX 的计数和 SHA256，不提交带机器私有信息的原始 TRX。

## 失败修复

- 连续 CAS 撤销/重做最初未推进前后历史栅栏，测试失败后修复。
- RTF 恢复规范化导致第二次撤销多记一步，恢复后重新建立规范快照后通过。
- 新后台保护使旧测试隐式色标清除/反复无快照编辑不再有效，测试改为显式用户 CAS/读取快照。
- 导出任务锁文件使旧成片计数断言增多，改为仅计图像扩展名。
- 篡改恢复路径最初抛异常而非 Failed 状态，补类型处理后负面测试通过。
- 一轮全量出现既有焦点路由和滑杆键盘测试失败，独立进程复测通过；随后最终全量无失败。相关失败 TRX 摘要保留。

## 运行状态

BUILD=PASS，CLI_PROCESS=PASS。RELEASE_STARTUP=NOT_RUN，DESKTOP_RUNTIME=NOT_RUN，USER_VISUAL=NOT_RUN / NOT_APPROVED。未启动 Release，也没有截图或 Installer。自动 WPF 测试不代替真人验收。

真实 RAW/ICC、GPU、性能 opt-in 和生产桌面截图等跳过逐项见 SKIPPED_TESTS.md。专业 RAW 正式处理版本保持不变，不用该长期范围掩盖本批结果。
