# P3 索引最小优化（非长尾根因已闭合声明）

真实观测：原ORDER BY包含CASE(AddedAt IS NULL)的空值桶，已有AddedAt索引不覆盖该表达式；且同名IF NOT EXISTS旧单列索引不会被后来的复合索引声明替换。原page计划TEMP B-TREE，需处理所有4999条匹配后才能LIMIT101。

新增单个命名索引IX_AssetItems_ActiveAddedOrder(IsArchived,CASE-null,AddedAt DESC,AssetId,Rating)，不重建原索引、不改SQL条件/排序/数据。COUNT可覆盖扫描，page按目标序读取，无TEMP B-TREE。遵循既有EnsureAssetIndexesAsync幂等初始化升级约定。

专项3/3通过：原必需性能、计划、范围分页/排序/字节/移除索引同语义/重启索引恢复。完整Core发现/选择/结果1709，执行1702，1702通过、0失败、7条件未执行、0缺失。范围cold28.344ms，warm9.943/16.695/8.048ms；最慢16.695ms<400ms，全部样本保留。100/500项57.392/69.558ms。

这证明消除已观测排序开销，不证明已定位历史2807ms长尾的全部来源。完整负载曾转到100项批量2818.8ms，首个失败保留；未把一次完整通过当所有环境抖动消失。GC停顿逐样本保留，不从门槛计时扣除；OS磁盘等待尚无权威分离，不能归因家用盘。最终实际发布组件复测在阶段六独立报告，UI选择/分类/大小另测。
