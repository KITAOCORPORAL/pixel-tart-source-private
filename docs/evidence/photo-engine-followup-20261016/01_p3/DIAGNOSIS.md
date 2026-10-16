# 阶段一 A：原失败保留与只读计时（尚非修复）

基准fe73d02cf85df518c4eaebf4a09f9a5157451eab。独立树/分支见00_baseline/BASELINE.json。未修改400ms或其他阈值，未串行化/隔离掉完整Core并行负载。

第一次独立原测试全部通过：范围cold18.930ms；warm19.661/17.769/17.438ms。
第一次完整Core1708发现/选择/结果，1700通过、1失败、7条件未执行、0缺失。范围cold59.426ms；warm52.317/46.878/60.527ms；实际失败转到100项批量2818.8ms>750ms，未隐藏。
只增加分段观测后的完整Core1708结果、1701通过/7未执行、无失败、无缺失。范围cold65.394ms；warm49.745/61.127/63.201ms；原runner仍FAILED（条件未执行）。一次通过不能关闭原抖动问题。

已观测SQL COUNT使用Rating索引，页面需要TEMP B-TREE排序；COUNT约8ms、page约9ms的独立样本，完整套查询约50–100ms。诊断新增初始化、文档归一、连接/PRAGMA、引用校验、COUNT/page SQL构建、COUNT、执行、行物化、cursor计时；采集GC pause/collection数，不扣除GC来改合格值。未取得OS磁盘ETW，disk wait仍UNMEASURED，不能称磁盘根因。

公司端修复仍在：QueryTotalBytesAsync普通P3文档使用SQL SUM(a.FileSize)；RefreshScopeAsync调用RefreshAsync(refreshSystemCounts:false)复用导航计数。该Core必需性能测试仅Repository.QueryAsync，不调用UI、大小聚合/分类计数；不能将公司端UI修复当该测试的新修复。

实际SQL/参数/EXPLAIN/SQLite/盘符见*_SQL_TRACE.json，原完整TRX保留。下一步只在确定的页面排序开销上做最小索引优化并证明语义与迁移；对于长尾调度/全局GC/池清理仍待证，不能将推测写成已修。
