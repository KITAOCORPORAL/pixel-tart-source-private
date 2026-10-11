# 测试与 Release 矩阵

| 验证 | 通过 | 失败 | 跳过 | 说明 |
|---|---:|---:|---:|---|
| Core 全量最终 | 1617 | 0 | 6 | core-delivery.trx |
| WPF 全量最终 | 1517 | 0 | 13 | wpf-verified.trx |
| Core 发布前相关 | 114 | 0 | 0 | core-release-verified.trx |
| 原子导出更新后相关 | 21 | 0 | 0 | atomic-export.trx |
| WPF 发布前相关 | 67 | 0 | 1 | delivery-check.trx |
| 原子导出更新后 WPF | 61 | 0 | 0 | final-export-ui.trx |
| 两 OS 进程竞争/ABA/崩溃释放 | PASS | — | — | PROCESS_RESULTS.json |
| 不同进程素材失败/重启/过期提交 | PASS | — | — | Run-ProcessChecks.ps1 |

测试集合重叠，不求和。完整历史运行计数/哈希见 TEST_SUMMARY.json。首轮失败包括新 sidecar 文件计数、版本目录测试隔离、旧快照提交冲突和失败后未释放的测试缩略图队列；修复后干净进程复测通过。旧 v1 元数据撤销因缺少 after-state 改为安全拒绝，相应断言按新策略更新。

代码测试、命令行进程测试、Release 启动、桌面操作与用户视觉批准严格分列。自动测试不代替桌面验收。BUILD=PASS；RELEASE_STARTUP=NOT_RUN；DESKTOP_RUNTIME=NOT_RUN；USER_VISUAL=NOT_RUN，NOT_APPROVED。

跳过逐项见 SKIPPED_TESTS.md：真实授权语料、GPU/性能门槛、生产窗口/截图类 opt-in 未启用。Release 身份见 RELEASE_IDENTITY.json，源片、数据库、bin/obj、日志和二进制不提交。

专业 RAW 本批不切换：相机输入特征化、真实 RAW 白平衡、高光处理、HDR 权重域、显示器 ICC、输出变换与真实样本验证仍是独立后续任务。候选数学契约不代表生产 RAW 已完成。
