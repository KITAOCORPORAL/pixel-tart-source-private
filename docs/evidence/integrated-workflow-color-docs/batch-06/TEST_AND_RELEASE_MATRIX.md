# 测试与 Release 矩阵

日期：2026-10-11（Europe/Berlin）；本机TRX原始时间Asia/Shanghai。同名测试集合重叠不相加。完整原始TRX/照片/机器日志只留ignored artifacts，脱敏汇总含digest。

## 最新完成的验证

| 验证 | 通过 | 失败 | 跳过 | 证据 |
|---|---:|---:|---:|---|
| Core最终源码全量 | 1633 | 0 | 6 | core-current.trx |
| WPF全量（兼容修复后） | 1523 | 0 | 14 | wpf-delivery.trx |
| 最终源码WPF命令/真实两相机/失败恢复专项 | 4 | 0 | 0 | wpf-source-verified.trx |
| Core新增/旧黄金专项 | 10 | 0 | 0 | core-source-final.trx；后续多加合成阶梯已被core-current覆盖 |
| 6实际RAW full/preview/redecode/增益/TIFF | 6文件PASS | 0 | — | REAL_RAW_RESULTS.json |
| 5配对JPEG类型/只读hash | PASS | 0 | — | REAL_RAW_RESULTS.json |
| 两实际相机Studio产品保存/重开/导出 | PASS | 0 | — | PRODUCT_REAL_RESULTS.json |
| 两独立Release进程性能 | MEASURED | — | — | PERFORMANCE_*.json |
| Release x64 self-contained publish | PASS | — | — | RELEASE_IDENTITY.json |

最终源码WPF全量 `wpf-current.trx` 已完成：**1523通过 / 0失败 / 14跳过**，10m58s。源码d2c20ab，与发布源码一致；Core `core-current.trx` 为1633/0/6。

## 失败、修复、未完成记录

- 输入草稿通道增益被UseCameraWb覆盖：官方scale_colors发现后改显式user_mul与双flag关闭；真实二相机normalized增益/像素变化验证。
- 最小ABI初编译访问内部LibRawDataX64失败：只读reflection获取包内结构用于偏移一致性门控，其余读取使用包公开结构，非硬编码偏移；core-focused-r3通过。
- decode失败测试发现InvalidDataException不在原过滤内（不是IOException）：补typed处理，失败保留旧版本。
- 无仓储的旧版测试目标尚未capture默认版本，错误测试断言count=1：改先创建旧编辑版本再验证失败不增版本。
- WPF首轮全量误判长运行为停滞，5m33s中止自己启动的testhost，TRX仅288通过/1跳过、run aborted，**不是PASS**。其后限时完整运行约7–8分钟证明旧全量本来耗时较长；保留记录，不归咎产品。
- WPF r2全量1失败：过早source hash绑定导致既有corrupt-file替换修复测试拒绝；恢复有效解码后绑定，专项及wpf-delivery全量通过。永久RAW版本发现仍先验证身份再选decoder。
- 与在跑WPF testhost共用Debug输出的并发build触发MSB3021/MSB3027文件锁：串行后重建，无删除/结束外部进程。
- 最终补测试FakeLinear缺Fail字段导致编译失败；补字段重新构建；wpf-source-final-r2使用旧二进制并缺语料skip，**不当最终源码证据**。wpf-source-verified重建并显式授权语料4/4通过。
- 旧MSTest建议式warning保留，不伪称零warning。没有失败用大幅提高容差掩盖。
- 测试自动生成三份旧ui-guardian报告，本地保留generated副本后恢复起点tracked字节，不提交这类覆盖历史证据的变更。

## 跳过和独立状态

Core6项：旧授权RAW/空间效果opt-in、旧film/productionbenchmark性能、CPU/GPU parity。WPF14项包括旧真实JPEG、生产桌面/视图审查、授权其他RAW gate；新增真实PV4 opt-in在全量未设置时skip，已由最终专项显式运行。完整名称见TEST_SUMMARY.json。不得把跳过升级成PASS。

BUILD=PASS；RAW_FILE_PROCESS=PASS；WPF_COMMAND=PASS；RELEASE_STARTUP=NOT_RUN；DESKTOP_RUNTIME=NOT_RUN；USER_VISUAL=NOT_RUN；USER_ACCEPTANCE=NOT_APPROVED。没有Browser/Computer Use/CDP、没有Release启动或Installer。相机色准/实拍灰卡与阶梯质量PARTIAL，JPEG/PNG导出质量NOT_RUN。

## 发布组件额外验证

将发布目录的Core/Sdcb/raw_r.dll复制到隔离命令probe，逐文件SHA256一致；两实际相机preview/full/TIFF/WB/redecode验证通过，见PACKAGED_CORE_RESULTS.json与PACKAGED_PROBE_IDENTITIES.json。这是发布组件CLI验证，**没有启动Release EXE或桌面**。
