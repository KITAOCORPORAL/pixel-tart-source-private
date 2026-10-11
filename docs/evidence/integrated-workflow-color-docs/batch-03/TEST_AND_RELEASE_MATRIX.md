# 测试与发布矩阵

| 运行 | 通过 | 失败 | 跳过 | 说明 |
|---|---:|---:|---:|---|
| Core 全量通过轮 | 1613 | 0 | 6 | core-verified.trx |
| WPF 全量通过轮 | 1514 | 0 | 13 | wpf-verified.trx |
| 审查后 WPF | 65 | 0 | 1 | wpf-release.trx |
| 最终身份/恢复 | 5 | 0 | 0 | core-delivery.trx |
| 最终文档/版本/本地化 | 9 | 0 | 0 | wpf-delivery.trx |

重复测试不累加为独立数量。首轮失败保留：品牌固定标题断言、按钮数量、本地化硬编码、直接 MessageBox；均已修正。DOCX schema 校验发现 TableGrid 与 numPr 顺序错误，已修正并通过。一次并行构建被运行中的 testhost 锁文件阻止，顺序复跑成功，不归因产品功能失败。

追加发布前校验 release-checked.trx：3 通过/0 失败；修复 rFonts 顺序后再次通过。完整机器摘要及最终回归见 TEST_SUMMARY.json；逐项跳过见 SKIPPED_TESTS.md。真实 RAW/ICC/显示器、生产窗口、截图与 GPU/性能测试没有执行，不标通过。

Release：见 RELEASE_IDENTITY.json。BUILD 与自动 TEST 分开，STARTUP/RELEASE_RUNTIME/USER_VISUAL_REVIEW 均 NOT_RUN；USER_VISUAL_REVIEW 未批准。未制作 Installer。
