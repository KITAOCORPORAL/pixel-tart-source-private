# 本批需求追踪

| ID | 要求/范围 | 实现/证据 | 状态 |
|---|---|---|---|
| R01 | N盘/工作树/AGENTS/脏修改/fetch | N存在，实际N:/pixart/...；无适用额外AGENTS；入场14 tracked+5 untracked RAW草稿备份；fetch远端未前进 | PASS |
| R02 | batch01–05颜色证据和调用链 | 根颜色审计、batch02黄金、batch03候选边界、batch04/05交付；保留旧math/人工状态 | PASS |
| R03 | LibRaw实际版本/API/默认 | 参数审计、官方确切源码digest、live defaults；独立配置和最小ABI bridge | PASS，版本限制已声明 |
| R04 | 真实线性空间/一次WB/曝光/显示边界 | PV4/ProPhoto D65→sRGB；2^EV；软肩+中性轴压色域；float负/超值不裁切直到输出 | CODE/TEST PASS；早期decode裁切限制 |
| R05 | 新RAW版本真实入口/不迁移旧 | Header按钮/参数；校验后新ID，来源ID；失败旧版本保留，JPG区别 | COMMAND PASS；DESKTOP NOT_RUN |
| R06 | 保存重开/版本/导出一致 | session/catalog/receipt RawDefinition+RawInput；2真实相机产品链，永久目录直接PV4；旧fingerprint不变 | PASS |
| R07 | 旧任务隔离 | revision/target/version/token/dispose；晚到创建、旧缩略job、旧批量/渲染/同步屏障 | 自动测试PASS；全部桌面竞态未遍历 |
| R08 | 至少两相机/Sony A7M4/RAW JPEG | 5授权Sony+1既有公开Canon本机语料副本；5 paired JPEG；来源见CORPUS_COVERAGE | 实际文件PASS |
| R09 | 中性灰/曝光阶梯/负值/黑/高光/饱和 | 合成公式/真实饱和计数/暗人像火焰混合光 | PARTIAL；实拍灰卡/标定曝光阶梯NOT_RUN |
| R10 | 未支持/缺配置/已WB | 真实损坏文件/JPG拒绝；合成missing cam_mul/已WB拒绝；产品失败不建版本 | PASS；真实已WB sRAW和未知新机NOT_RUN |
| R11 | preview/full tolerances/黄金 | 6文件same coordinate error0/readback0；2产品oracle/reopen exact；PV1/2黄金不变 | PASS；JPEG有损误差NOT_RUN |
| R12 | 全尺寸/预览/内存 | 两独立Release进程实测阶段，记录2GiB级写出峰值 | PASS measured；桌面端到端NOT_RUN |
| R13 | Core/WPF/Release | 见测试矩阵/身份JSON；失败/中止/文件锁均保留 | 按实测分列 |
| R14 | 原树/审计/素材安全 | 不reset/clean/force/merge；原树两审计目录不动；副本hash核验；不提交原片/db/exe | PASS |

## 限定批次评价

可执行生产接线已经完成，不再只是数学工具。总体 **PARTIAL / 待人工验收**，具体不足是实拍灰卡/曝光阶梯与相机色准、RAW→JPEG/PNG质量、桌面操作/Release启动、1600预览细节和内存，而不是“整个专业调色系统未完成”。第五批历史/冲突/临时文件既有策略不重开。

## 后续阶段（不冒充本批实现）

1. HDR/高光重建：在解码损失以前获取可信余量，独立float/mosaic方案与逐相机验证。
2. 选择性颜色与肤色：明确计算/选择域，调试现有仿色/胶片兼容，不隐式8bit降级。
3. 显示器ICC/输出ICC：白点/配置管理、软打样、不同输出空间与设备色准。
4. 空间处理/GPU：抗混叠预览、100%细节、去雾/清晰度/局部处理、流式TIFF与缓存预算；独立性能/视觉验收。
