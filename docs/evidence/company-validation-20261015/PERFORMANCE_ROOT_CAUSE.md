# P3性能诊断与最小修复

历史673.8ms/2499.7ms失败原件仍在A/B继承证据中；没有重写原TRX。本次先在合并8c3986e完整源码和发布组件执行原10,000项必需测试，均Passed。随后固定额外10,128条（10,000未归档+128归档）诊断，独立DB在D盘，SQLite3.53.3/WAL，实际SQL/参数/EXPLAIN/计数与首屏ms均保留。

## 三种不同指标不可混同
- 原Core必需400ms测试：预热后取三次最慢，阈值400原样。有限3轮，每轮新10k夹具；完整逐次输出见REQUIRED_REPETITIONS。均通过。不能把历史仓储673.8ms断言失败归结为当前UI瓶颈，因为历史没有SQL分段证据。
- 仓储10,128项探针：冷进程current165.7ms/all111.5ms（不驱逐OS cache）；三次warm all19.2/18.1/19.2ms；current49/all4999。Rating索引命中，ORDER BY使用临时B-tree，count约8–10ms/page约10–20ms。没有证据索引是本机主要瓶颈，不擅加索引。
- 真ViewModel+Dispatcher的范围切换：初版priming522.7ms，warm482.9/467.3/472.5ms，出现约458–466ms dispatcher间隙。计数/首屏总约20ms，但UpdateP2QuerySummary发起RefreshCollectionSizeAsync，UI线程完整分页读取所有4999条（每页重复COUNT）；之后刷新9个全库collection计数。此链路在同样数据上稳定复现，不归咎并行。

## 修复
1. SqliteAssetLibraryRepository.QueryTotalBytesAsync：canonical普通P3以相同BuildIndexedWhere+CompileP3Node做SUM(FileSize)，不每页取完整AssetItem/重复COUNT。复杂/无canonical/regex/smart-folder用原分页fallback保留语义。
2. RefreshCollectionSizeAsync：后台await该聚合，generation仍检查，避免UI线程同步扫描整库。
3. P3QueryScope范围导航只刷新可见query，不重新计算不受范围影响的全库collection计数；普通刷新/修改仍重算。非持久缓存，无schema变化。
4. opt-in SQL时间观测不改SQL结果；原性能测试增加每次输出，不改门槛或worst-of-three。

同数据先仅计数复用warm232.9/253.0/234.7ms，定位仍有集合大小扫描；加聚合后warm29.6/29.3/29.6ms，dispatcher间隙降至约16ms。结果数量/排序、scope和global counts不变；变更后普通Refresh仍重算。Core聚合对paged oracle跨scope/archive/rating12组合通过；WPF新合同与原P3相关34项通过。

失败迭代保留：首两个测试发现还有11/21次query，stack指向RefreshCollectionSizeAsync；第三轮数量assert因测试oracle120与真实首屏500不一致失败，修正oracle用同真实500，未删排序/数量断言。最终专项通过。UI探针只是后台WPF调度，不叫物理输入验收。

CPU/后台进程、剩余空间、SQLite PRAGMA及冷热标签见原始数据。没有无限重跑，OS冷缓存未强制清空。历史仓储异常仍是未完全归因的波动，本次并未证明所有机器永远<400ms；最终必需测试若Failed将阻断审阅。

已明确区分：SUM聚合修复避免集合大小统计完整分页；全库分类计数复用只作用于范围导航，普通刷新/变更仍重算。新Core合同和ViewModel数量/排序测试在修复后实际通过。下一轮完整回归曾暴露旧画布弹窗全局枚举的不稳定失败，原结果保留，修复精确owner/menu引用后重新完整执行，不能以专项涂绿。
