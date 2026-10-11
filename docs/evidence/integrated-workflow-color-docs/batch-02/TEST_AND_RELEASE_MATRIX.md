# 测试与 Release

## 自动测试

| 运行 | 通过 | 失败 | 跳过 | 范围 |
|---|---:|---:|---:|---|
| 修复前身份桥复现 | 0 | 3 | 0 | 保留真实失败证据摘要 |
| Core 全量最终运行 | 1607 | 0 | 6 | core-release.trx |
| 随后新增生产链测试 | 1 | 0 | 0 | production-handoff.trx |
| Core 最终受影响集 | 21 | 0 | 0 | core-release-focused.trx |
| WPF 全量通过运行 | 1511 | 0 | 13 | wpf-verified.trx |
| WPF 审查后受影响集 | 126 | 0 | 1 | wpf-postreview.trx |
| WPF 发布前聚焦集 | 15 | 0 | 0 | wpf-release-focused.trx |
| WPF 最终源码聚焦 | 49 | 0 | 0 | wpf-source-final.trx |
| Studio 最终身份复测 | 39 | 0 | 0 | studio-identity-final.trx |
| DOCX 独立验证 | 1 | 0 | 0 | docx-probe.trx |

计数有重叠，不相加成独立测试数。首次 WPF 有 2 个新增失败：损坏图修复重试、无障碍名称；均已修复，40 项针对性复测通过。第二次有按钮清单计数失败，新增真实 Studio 入口后更新清单并复测。最终新增失败为 0。

## 跳过原因

逐项见 SKIPPED_TESTS.md。RAW/JPEG 授权真实语料未显式启用；性能/GPU 测试缺少启用参数或已验证设备；截图/生产窗口测试受本轮桌面规则限制且没有运行。任何跳过都不算通过。

## 色彩边界

v1/v2 固定五像素输入，+0.5EV Develop 的字节黄金为 000000FFFFFF5485ACFF271214D242，容差 0 字节。候选 v3 只做显式线性 ACEScg 白平衡/曝光；+1EV 测试容差 1e-12，保留负值及超范围。旧正式栈仍拒绝 ProcessingVersion=3。没有相机特征化、显示变换或新专业 RAW 生产链。

## Release

构建身份、SHA256 和发布清单见 RELEASE_IDENTITY.json。构建物与 TRX 留在忽略的 artifacts/batch02 目录，不提交二进制/日志。RELEASE_RUNTIME=NOT_RUN，USER_ACCEPTANCE=NOT_APPROVED。
