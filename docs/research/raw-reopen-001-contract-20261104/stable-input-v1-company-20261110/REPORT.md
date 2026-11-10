# 公司端 stable-input V1：原生已就绪，实际批次因宿主计时失败停止

2026-11-10，Europe/Helsinki。基线 `ea081ecb0930600fce9ee2e714c741fcd3c12b7e`。状态 **PARTIAL / NATIVE_BUILD_PASS / NATIVE_NO_RAW_PASS / CANDIDATE_PAIR_NOT_AVAILABLE / NOT_ADMITTED**。

## 结论和责任

公司既有 MSVC/OpenMP 已成功编译隔离候选，新的候选宿主已通过原生无 RAW 自检。实际候选首进程PID28684在调用OpenFile和Unpack后、进入DcrawProcess前失败：我新增的计时适配器每个阶段都用CreateNew写同一个stage-times.json，第二次写入引发IOException。该错误属于宿主回执逻辑，**不是候选算法的真实结果**。

本次按指定COMPANY_HANDOFF.md的“失败也消耗该slot并停止”处理：保留first.consumed，consumed=1、remaining=1，持久STOP_AFTER_FAILURE阻止自动启动第二槽。没有退款/改账、没有补第三次，也没有偷偷另开同任务空账本。原五份历史消费证据hash全部保持。后续只修宿主并运行无RAW测试，未追加RAW。

因此不能回答本候选两次P5/serial是否相同、P6是否逐位一致，也不能给出候选相对两份旧P6的实际差异。相关字段在OUTCOME与MISSING_COMPARISON中为null/UNKNOWN，不能以编译、自检或旧H1结果替代。

## 现场核验与原生身份

开始前fetch并检查本地refs、相关worktrees，以及E盘三个既有RAW诊断根、Kitao-Research/Worktrees的相关候选目录。已检查位置未发现stable-input新公司回执或产物，故新建E:/Guangjing-RawReopen001-StableV1-20261110；这不是全磁盘“绝无产物”的断言。LOCAL_AUDIT_BEFORE记录检查范围。

- 实际候选版本：XTransStableInputSeparateRgbV1，默认关闭、只留隔离诊断。
- 固定LibRaw源：cccb97647fcee56801fa68231fa8a38aa8b52ef7。
- 七份旧观察源/header校验后只更换候选xtrans；候选实际source SHA `f11b4e4b46cf1c863f0af0fa469be05ad2e4a3f3d1c854ace4b66064528dbb18`；header SHA `ce42b2efa23806da4f1edabbad4901c1a836ff319133507bac6e4315b0d1f718`。
- MSVC/NMake编译完整DLL一次成功，原Makefile目标、/O2 /openmp /MD等flags及optional components保持。
- 实际新DLL SHA **157BBA02166BDB78E171154E1F4B03AE58F7DF97A110282B37AB3B5988FDC67F**。
- 新CandidateNativeContractV1明确intentionalPixelChange=true、originalByteRestoration=false、productIntegration=false；没有伪填旧观察器“原样恢复”guard，也未修改旧Build-Isolated门禁。

SOURCE_PREPARATION是构建前不可变输入收据，其中NOT_BUILT/RAW0是当时状态；实际编译成功见NATIVE_BUILD_STATUS，实际尝试见CONSUMED/FAILED_PROCESS_RESULT。完整vendor源、DLL、PE/编译原日志均留本地，PROVENANCE绑定hash。

## 无RAW与修复的核验

无RAW候选ABI、hash、布局、文件生命周期导出均通过；固定binding SHA6DF69864...93D4A3。实测native Initialize/托管Dispose各1、Dispose返回和释放后访问拒绝；native Recycle/Close仍按固定binding释放逻辑推断，不写成直接计数。

首次参数自检先拦住了另一个适配问题：managed new OutputParams()为零值默认，并不等于真实LibRaw初始化默认，例如Brightness、AutoScale、Gamma、UserQual。失败日志和旧宿主副本均保留；改为从真实native Initialize句柄读参数后，与原实际configured-output全部字段严格一致。没有调参去凑一致，也未降低断言。

首个实际RAW失败后，计时器改为只在内存中累计，每进程退出时用CreateNew写一次汇总。最终无RAW回归实际执行两个连续scope，并捕获预期异常验证原action异常不被计时器掩盖；检查三项StageReturned状态、唯一stage-times.json以及原生自检均通过。FINAL_NO_RAW_SELFTEST/FINAL_NO_RAW_RESULT/FINAL_TIMER_TEST绑定这份修正后的宿主；旧失败runtime和源码另存，不能把新宿主说成首进程运行的版本。

预算回归12项通过，包含失败停止时remaining仍为1而launchAllowed=false、拒绝第二预留、原消费文件不变。离线RGB-U16比较器10项合成核验通过，覆盖通道/像素计数、65535差不溢出、first/max坐标、border/seam分账、逐位0差及截断拒绝。只对比现成数据，不启动decoder；其真实候选分支尚因缺数据未运行。

## 请求结果逐项状态

| 请求 | 本轮证据与结论 |
|---|---|
| 两次P5有效输入/状态相同 | **UNKNOWN**，first未到Process，second未启动 |
| 两次串行准备image4/allhex/CIELab相同 | **UNKNOWN**，无候选serial payload |
| P6 RGB含边缘逐位一致 | **UNKNOWN**，无候选P6 |
| 候选对旧baseline first/second的差异 | **UNKNOWN**，两份旧文件存在且逐份hash验证，但候选无P6不能算差异 |
| 实际parallel team/passes/T/ndir | **UNKNOWN**，xtrans未进入，不把有效OMP_NUM_THREADS=6当team事实 |
| 候选完整插值峰值RSS/PrivateBytes | **UNKNOWN**，候选算法未执行；不能用早退进程峰值代替 |
| 每阶段耗时 | 仅Open调用及Unpack调用范围有部分记录，Process/MemoryImage/插值内部未测 |
| 是否进入产品回归 | **暂不进入**；缺真实候选pair，不是判候选数学失败 |

两份已存4c322975基线P6：first SHA29679cc6...4109d7，second SHA2af6dd95...91003b，各241118208 bytes，已完整hash核验；完整值和路径见STORED_BASELINE_IDENTITIES。没有重跑旧decoder，也没有用带调试器的H1窗口当baseline。

离线分析已给出first/second缺失文件的精确路径，见MISSING_COMPARISON。准备好的比较器支持不同通道/像素、max/mean U16、first/max坐标、bounds、outer border8，以及tile seam±8分账；当前不填写任何候选数值。ReviewedCompare.mjs逐字节来自4c322975，原schema/身份/显式P8旁路规则保持，未为了候选降低门禁。

## 失败尝试的资源和耗时——不能冒充候选性能

进程PID28684，OpenFile调用1、Unpack调用1、DcrawProcess0、MemoryImage0；Unpack返回状态在日志写入异常前没有单独确认，不能推断其成功或native失败。

- 宿主OS累计峰值WorkingSet：117731328 bytes。
- 外部采样所读OS WorkingSet峰值：117452800 bytes。
- 50ms采样PrivateBytes峰值：94040064 bytes。
- Open调用范围：1.7154 ms；Unpack调用范围：95.2315 ms（完成状态未知）。
- 整个失败进程外部elapsed约594.62 ms，2GiB/600秒守卫未触发；失败原因是journal文件冲突。

这些仅代表启动/部分解包的失败尝试。不能据此相对旧完整解码RSS683MB报“减少内存”，也不能把源分析预计+241118208 bytes的RGB输出分配写成实测额外RSS；该分配所在xtrans本轮未进入。

修正宿主将测Open/Unpack/DcrawProcess/MakeDcrawMemoryImage和写出/hash的独立API时长，Process包含原内部stage捕获IO。交接要求保留的精确header没有内层QPC标记，所以P5→serial→P6独占耗时为NOT_INSTRUMENTED，不以mtime反推，不静默改候选源hash。这份限制在冻结合同和宿主source中明确写出。

## 批次与下一步边界

任务ID沿handoff为RAW-REOPEN-001-STABLE-INPUT-V1-20261109，实际公司执行日期2026-11-10，两者不混作时间。当前first消费记录和停止记录都保留，remaining1没有变成第二次自动尝试。

修正后的runtime-timerfix-final仅生成NO_RAW_MANIFEST_TIMERFIX_FINAL和无RAW回执，没有生成新的可解码清单。要取得两份成功候选输出，需明确授权新的独立批次/任务schema并重新冻结修正host及max2，不回滚本批失败slot；现有剩余单槽不足以形成两份新输出，本handoff也要求失败即停。

候选继续隔离、不替换产品DLL，默认gamma/profile/recipe和V1锐化均保持。没有修改原生候选公式、线程设置或预算阈值；本轮调试器/GPU调用0。旧产品回归、preview/reopen/TIFF及一般摄影质量准入均NOT_RUN。

本地证据根E:/Guangjing-RawReopen001-StableV1-20261110；失败完整runtime/source、217源inventory、native build/PE、manifest、五份旧消费绑定、一次实际尝试、修正host/selftest都保留。Git交付统计/哈希/小回执和自有适配代码；完整vendor/binary/私有像素不进Git。
