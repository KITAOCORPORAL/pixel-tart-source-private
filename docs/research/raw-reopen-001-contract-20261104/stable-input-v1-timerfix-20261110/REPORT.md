# stable-input V1：计时修正后的独立原生两进程验证

2026-11-10。接续 `3ea52612ea39915cb3e8c0d9a2e33f27b0cb1026`。**本批完成：同一Fuji、两次无调试器独立进程，P5/串行准备有效状态相同，P6全部RGB样本逐位一致。候选继续隔离，产品验收尚未执行。**

## 新任务和旧失败证据

本批任务ID：RAW-REOPEN-001-STABLE-INPUT-V1-TIMERFIX-20261110，新账本/输出根E:/Guangjing-RawReopen001-StableV1-TimerFix-20261110。新授权、max2、manifest、源/host/native/binding/依赖、启动环境及旧失败证据都在预留前冻结。两个ticket在各自spawn前排他写入并fsync，PID20220和9948依次执行，每进程Open/Unpack/Process/MemoryImage各一次，均completed/exit0，无替代/退款/自动重试。

旧批次first.consumed、STOP_AFTER_FAILURE、失败result、stderr、资源回执和原manifest逐项hash绑定，执行后保持原样。旧批次仍consumed1/remaining1并停止；新批次consumed2/remaining0。这是新授权的独立任务，不是重置旧额度。OLD_FAILURE_BINDING和BUDGET_GATE/BUDGET_FINAL给出直接证据。

## 复用和无RAW门禁

候选DLL完全复用，没有重新编译native：SHA **157BBA02166BDB78E171154E1F4B03AE58F7DF97A110282B37AB3B5988FDC67F**。原候选源ea081ecb、observer xtrans SHA f11b4e4b...dbb18、header ce42b2ef...d1f718均不变。

3ea宿主把旧TaskId写成常量，因此只做托管准入版本化和无RAW测试补充；计时函数、参数配置、RAW调用链及单次汇总写入代码与3ea对应行精确相同，HOST_REUSE_PARITY绑定该子集SHA。没有重新设计计时器、改处理参数或替换decoder。

使用同一计时函数无RAW执行五个合成scope，顺序是OpenFile→Unpack→DcrawProcess→MakeDcrawMemoryImage→WriteAndHashMemoryRGB16；测试TimedValue返回、全部正常返回和预期异常传播，实际产生一个可解析汇总journal。scope名称有NoRawSequence前缀，actualDecoderCallsInSequenceTest=0，不冒充真实解码耗时。

原生ABI、hash/布局、文件生命周期、实际native初始化默认参数的全字段比对、Initialize/ManagedDispose及释放后拒绝访问均通过。12项新task预算回归通过；实际新账本audit0/2且旧停止批次不受影响后才运行RAW。NEW_NO_RAW_SELFTEST、NO_RAW_TIMING_JOURNAL、TIMING_ORDER_GATE、BUDGET_TESTS保存门禁。native Recycle/Close仍仅为固定binding释放逻辑推断，未改为直接实测计数。

## 真实路径和输入一致性

- 授权样本：同一DSCF0347.RAF，SHA52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865，前后身份校验通过。
- 实际unpacker：unpacked_load_raw()，flags4096；不是凭Fuji型号推断路径。
- UserQual=-1，实际quality3，xtrans passes3、T512、ndir8；每进程176条tile记录、actual team集合均[6]、worker0..5均被观测。
- 两次实际完整configured-output与旧验证快照逐字段相同；没有手动set/clear OMP或降质量。启动前OMP_NUM_THREADS为空，原binding初始化后有效6；team6来自tile日志而非环境推断。
- 两次176个tile里160个名义worker归属不同，输出仍一致；该统计不是跨线程全局执行顺序，也不是覆盖所有调度的证明。

原始40186368 active像素，7752×5184；P5和serial是四槽U16，P6是RGB三槽U16。沿4c322975原样ReviewedCompare读取完整schema、布局、extent、phase顺序、初始化表、逐文件hash、RGB/audit4投影及软件/进程身份，不放宽F2或P8显式旁路检查。

| 检查点 | 本批两次结果 |
|---|---|
| P5完整image4/有效状态 | 相同，全部行/边界/四槽 |
| XTRANS-serial-ready完整image4 | 相同 |
| serial allhex、CIELab lookup/有效矩阵及控制状态 | 相同 |
| P6全部有效RGB | **逐位一致**，120559104个通道样本，0差异像素/通道，max0 |
| P6外圈8像素及seam带 | 均0差 |

P6两文件SHA均为 **9266ca59bc012266a7423d4b5538f7250e1ccca92c2129f3218e80fcd5bdf024**。不只是比较收据字符串：数据文件长度和内容hash重新校验，并逐U16计算差异。TYPED_PAIR保留状态/原阶段比较；FULL_COMPARISON给出像素/边界分账。

另对两份已存旧baseline的P5/serial文件做完整hash读回与typed状态核验：两份候选分别对两份旧输入共8项同阶段检查全部相同。CROSS_INPUT_READBACK表明候选相对旧P6的数值变化不来自本次WB/参数/前插值输入变化。软件版本不同不被说成软件身份相同，旧decoder未重跑。

## 与两份已保存旧P6的差异

四组比较均独立逐份验证完整payload hash和长度；两候选P6已逐位一致，所以各自对同一baseline的统计相同。这里是相机RGB域U16数值差异，不是物理色准或DeltaE。

| 候选 | 已存baseline | 差异RGB像素 | 差异通道样本 | max abs U16 | mean abs U16/通道 |
|---|---|---:|---:|---:|---:|
| first | first | 197896 | 330309 | 870 | 0.0264767230 |
| first | second | 197705 | 329974 | 870 | 0.0264435691 |
| second | first | 197896 | 330309 | 870 | 0.0264767230 |
| second | second | 197705 | 329974 | 870 | 0.0264435691 |

两份baseline的首差均在(507,8,B)：candidate28、baseline25。最大绝对差均在(2501,792,G)：candidate3053、baseline2183，abs870。FULL_COMPARISON的signedDelta定义为second−first，即baseline−candidate，不与正负方向混淆。

对baseline first的bounds为x8..7690、y8..5175；对second为x8..7691、y8..5175。外圈8像素均无变化。seam带定义为行/列507+496*k两侧[seam−8,seam+8)，排除外圈8；该分区只是几何统计，不是放宽通过阈值或完整stencil依赖域。

| 相对baseline | seam带内差异像素 | 带外内部差异像素 | 带外max U16 |
|---|---:|---:|---:|
| first | 194727 | 3169 | 870 |
| second | 194538 | 3167 | 870 |

约98.4%的差异像素在所定义的seam带，但仍有上述带外差异，最大差也在带外，不能隐藏或用全图均值较小判断摄影影响可忽略。两份不稳定旧基线不是独立数值真值；候选相对它们变化约19.8万像素，不仅是消除旧两次之间随机不同的少量点。正确性和观感仍须下一阶段回归。

## 实测资源

| 指标 | first/PID20220 | second/PID9948 |
|---|---:|---:|
| OS累计峰值WorkingSet bytes | 831299584 | 831062016 |
| OS累计峰值WorkingSet MiB | 792.79 | 792.56 |
| 目标50ms采样PrivateBytes峰值 | 808312832 | 808128512 |
| Private峰值 MiB | 770.87 | 770.69 |
| 资源守卫触发 | 否 | 否 |

守卫仍为2GiB、600秒，无提高。采样wait配置50ms，实际211/376次采样，最长间隔80.95/80.23ms，故PrivateBytes是采样峰值而非声称连续精确峰值；WorkingSet还使用OS累计峰值，宿主/外部读回一致。

已存旧两baseline峰值WorkingSet683532288/683347968 bytes。本批对两份旧峰值分别做差，增量范围147529728..147951616 bytes（约140.70..141.10MiB），见RESOURCE_REPORT。源分析请求的额外RGB分配为241118208 bytes，加memmgr记账开销另算；不能把这两种数字当同一物理量。峰值阶段和对象生存区间不同，RSS差不等于单笔malloc大小。额外output pointer没有独立分配跟踪；现有日志实际给出了原image extent321490944、每worker scratch24641536和6个worker使用，未编造更细分配峰值。

## 实测耗时

单位ms；这是API调用范围，DcrawProcess含原native全阶段捕获/hash和磁盘IO，非纯xtrans计算时间。

| 阶段 | first | second |
|---|---:|---:|
| OpenFile | 1.8316 | 1.7002 |
| Unpack | 93.3955 | 96.4334 |
| DcrawProcess | 11365.2280 | 22566.1974 |
| MakeDcrawMemoryImage | 91.9814 | 95.8120 |
| WriteAndHashMemoryRGB16 | 1847.8691 | 1134.1040 |
| 外部完整进程wall | 14048.8009 | 24645.8866 |

所有五个真实scope的actionReturned均true，stage-times.json每进程只创建一次。P5→serial→P6内层独占耗时仍为NOT_INSTRUMENTED，因为复用了精确header；不拿文件mtime反推。两次Process范围差约11秒，本批含大量stage IO和负载差异，不能据两次宣称算法提速或稳定延迟。旧调试器窗口耗时也未作速度baseline。

## 判定与交付范围

本样本的真实原生pair技术门禁通过：相同输入/状态、真实parallel6、不同worker归属、全P6逐位0差、参数完全一致、两次都成功且未超限。**具备进入下一步受控产品回归评估的条件**，但本轮没有替换产品DLL或执行preview/cache/保存重开/TIFF、PV4/5/6历史golden、其它相机/场景的产品验收。候选继续留在隔离诊断构建，不将本例0差写成完整修复发布或摄影质量批准。

下一阶段应保留明确candidate版本与旧recipe边界，重点检查(2501,792,G)最大变化和带外差异，以及真实预览/重开/导出一致性。当前两个新槽已消费完成，不自动追加解码、扩照片或新候选。V1锐化继续封存。

OUTCOME/COMPARISON/RESOURCE/ACTUAL_XTRANS为权威小结，PROVENANCE绑定运行前后真实DLL、host、源、依赖、manifest、ticket和payload来源。完整stage像素/CSV、vendor/binary只留E:/Guangjing-RawReopen001-StableV1-TimerFix-20261110；Git只统计/hash/小回执与自有工具。analyze_candidate.py和cross_input_readback.mjs只读已存文件，不调用decoder。旧FAILED报告保留其历史状态，不能与本新任务混算。
