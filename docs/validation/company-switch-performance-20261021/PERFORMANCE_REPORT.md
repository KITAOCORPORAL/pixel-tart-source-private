# Scenario11公司定位与最小修复

2026-10-21，Europe/Berlin。基线产品99d7818e035310c9273565e73752ee0359ebb8f1；修复产品0e5c3ea7e25c0c3d1773480cdc351ca9dc94b80f。

## 原始合同不变

原测试Scenario11源码零diff，10000交替OpenAsync、30000ms门槛。建两库在计时外；100000条去重也在Stop后，预期90000不变。400ms查询性能门是另一测试。家里32139.0909ms失败原TRX保留，公司未复现同一超30秒失败，不把并发线索当已证原因。

## 原始同条件数据（ms，全部测次）

|条件|修前|修后|
|---|---|---|
|原Scenario11独立进程|3628.9637|1703.5297|
|发布Core冷进程同一fixture|3418.1982|1463.5954|
|预热后第1次|3270.0605|1497.4150|
|预热后第2次|3298.6967|1537.2916|
|预热后第3次|3324.9674|1416.1346|
|固定四WPF并发第1次|4075.6049|1836.5044|
|固定四WPF并发第2次|4012.2595|1738.3473|
|固定四WPF并发第3次|4013.8295|1686.1591|

每轮10000记录见calls-*.csv，CPU/逻辑I/O/分配/GC、p50/p95/p99/p999/max见round-*.json。固定四WPF类/每次进程起止/退出/TRX见clean-pair-baseline与clean-pair-fixed的load.json及load-*。该干净配对在自有构建/Core/全WPF回归结束后顺序执行，使用同一最终WPF负载程序集；两轮测量时间不等，完成的负载次数可不同，未声称硬件调度精确相同。原第一套JS并发、曾与构建或额外Core重叠的观察另外保留，不用其较快一次替换声明的对照。

预热中位总时长约3298.70→1497.42ms；每10k进程逻辑otherOps640000→280000（并非磁盘read/write），分配约65.3MB→44.9MB。发布Core均用复制后SHA核对，不把诊断镜像当发布。

## 分段证据与瓶颈

修前10000中9998命中cache，只有两次冷SQLite验证。PathNormalize26.84ms、RootFileChecks215.29ms、PathResolve11.68ms、MetadataFingerprint437.14ms、CachedPhysicalPathChecks2772.75ms，SQLite验证仅3.61ms。GetAttributes140000/FileExists70000/DirectoryExists60000（诊断计数）。物理路径检查约80%的已计阶段，确认重复元数据查询是公司热路径主要成本。

修复只改EnsureDescriptorPathsArePhysical：已验证固定布局逐个检查根及7组件，共享父目录不重复；每次调用重查，不跨调用缓存安全状态；用一次GetAttributes取代Exists+Attributes，缺少可选路径保留原允许行为，拒绝访问等其他IO不吞。冷manifest/指纹/SQLite验证/锁协议/数据schema未改。

修后诊断计数GetAttributes90010、FileExists14、DirectoryExists12，cached阶段909.97ms。固定诊断样本与原样本负载不同且有观察器开销，只用于工作量和分段定位，不作为门槛对比；逐调用字段包含未观察到的disk/busy-wait null。

## 锁与磁盘等待不能伪造

热cache路径不打开SQLite、不获取writer lease，writer等待不是该路径已证瓶颈。冷SqliteOpen/quick_check/schema有独立wall-time观测，但Sqlite busy-handler等待未单独归因。物理磁盘raw计数是盘总体，不能精确分摊到某次OpenAsync；没有ETW进程关联时，磁盘等待/过滤驱动/调度等待仍UNAVAILABLE，不能把wall-CPU写成磁盘等待或0。

## 正确性及剩余

新增测试交替两库验证LibraryId/DatabasePath、FileName排序、SearchText过滤；每次查询连接释放并再次拿writer lease。六个中间路径（database/assets/managed/previews/backups/locks）逐个替换成自有合成symlink，拒绝后还原重开；缺可选路径、破损manifest/恢复亦验。容器/包/原P35/范围排序专项29/29通过；最终发布绑定专项与完整Core/WPF/模块另列，不用此29抵消完整结果。

本次可确认公司热路径最小优化有效；不能声称家里32秒根因完全关闭。下一步应在家里同冻结修复组件和同盘TEMP复跑保留负载，必要时采集进程关联ETW；不提高30秒门槛、不删循环、不挑最快、不改400ms测试。
