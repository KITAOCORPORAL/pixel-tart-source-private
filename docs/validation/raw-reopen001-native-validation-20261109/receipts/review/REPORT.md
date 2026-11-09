# RAW-REOPEN-001 F1-F3 sidecar复审

时间：2026年11月9日星期一 GMT+8 14:04:30（Asia/Shanghai）。

结论：未识别到阻断首次x64 MSVC原生编译或无RAW自检的确定问题。此结论来自静态审查，不等于编译成功、ABI通过或不存在UB的证明。确认三项离线比较器门禁缺口；在使用自动首差结论前应补齐。原问题保持OPEN / NOT_FIXED。

## 范围与身份

- 只读工具树：E:/Kitao-Worktrees/raw-reopen001-noraw-20261109/tools/RawNativeInternalProbe。基线工具08bfae4a6e311609c95784e8373bead711d9997c；研究基线5a4f233dea5ba11622b18380fea137c8f3944e53；后续review71ba977ac2d2ca1b039a59fb3a4b2e3f28882a4f。
- generator/header/comparator/verifier/build-script/tests均与工具基线逐字节相同，身份见REVIEWED_IDENTITIES.json。
- source-r2-build01的七份生成CPP与20261106/frozen-source-v3-r2逐字节相同。本sidecar未修改任何工具或生成源码。
- 主线程在审查期间提交并切换到diagnostic/raw-reopen001-validation-20261109；本次末次读回HEAD=3e3c33d3b530af56212f729404c76066b2fff04b。Program处于主线程修订范围，native R2文件未漂移，不能把运行中整个工作树当作最初静态快照。
- 审查Program加载顺序的最新读回：先RawContext.Version触发原绑定cctor，再NativeLibrary.Load取得精确handle；未见手设OMP。Dispose测得字段与推断字段已区分。

## F1与有效域

F1静态满足：Prepare-Internal.mjs:7-9让P1写quality=-1；生成dcraw_process.cpp:74不读取局部quality；原赋值仍在102行，P2及后续观测位于其后。

P2静态落点在pre_scalecolors callback返回后、no_auto_scale分支之前。P5围绕实际XTrans调用，完整四槽覆盖初始化有效域及边缘；P6主要输出RGB，第四槽另作audit。raw2image.cpp:398-405对整块四槽memset/calloc；header:64-71拒绝不支持的尺寸、shrink、fuji布局、分配指针变化或不足extent。仅判断该受支持路径；不宣称普遍支持其他RAW布局。

并行记录header:91-106先在串行区分配workers；每worker只写自己的固定Tile数组，超界使用atomic标错，文件输出在并行join后。没有hash私有scratch或结构padding，也没有由此识别到新UB/共享日志竞态。实际OpenMP团队数、native运行和动态内存安全均未测。

F3所需serial-ready图、初始化allhex、CIELab有效表已被生成并纳入P5→serial-ready→P6比较序列；这仍不提供LIM等实际共享load的消费值，H1保持UNKNOWN。

## 实质发现及最小修订

### S1 [P2] F2 layout binding

位置：Compare-Checkpoints.mjs:19,22,52,57。

Each state and payload is validated independently, but their dimensions, pitch and initialized extent are never bound to one another. Full load accepts P5 state40x40 with payload80x40, marking requiredStateComplete/identityVerified true.

最小修订：For every exact state/payload pair require safe integer dimensions, payload.width/height===state.width/height, sourcePitch===state.iwidth*8, packedPitch===state.width*channels*2, and expected payload span within validated declared allocation. Reject before returning capture.

证据：SYNTHETIC_SCHEMA_RESULTS.json中的“F2 full loader accepts P5 state40x40 with payload80x40”。

### S2 [P2] F3 phase binding

位置：Compare-Checkpoints.mjs:35,41。

CIELab/allhex have only an upper boundary before serial payload, and P8/out_cam only before P9. The loader accepts P8/out_cam before P1; typed validator accepts allhex before P1.

最小修订：Bind indexed windows P5 payload < CIELab initialization/tables < allhex < serial-ready state/payload < P6 state/payload, and P7 payload < P8 state < its typed rows/out_cam < P9 state/payload. Bind histogram after P9 payload and before P10. Keep the existing primary order and uniqueness checks.

证据：SYNTHETIC_SCHEMA_RESULTS.json中的“F3 full loader accepts P8/out_cam before P1; F3 allhex before P1 accepted”。

### S3 [P2] F2 runtime evidence completeness

位置：Compare-Checkpoints.mjs:15,42。

The complete positive schema contains zero actual-xtrans-passes/T/ndir, omp runtime scalars or tile records and still passes. Runtime evidence being mandatory in the generator does not make it mandatory in the offline reader.

最小修订：Require exactly one actual-xtrans-passes/T/ndir record bound to the XTrans serial window and at least one valid tile for this supported large-frame path; validate team/worker/tile geometry/scratch extent and compile-conditional omp runtime records. Keep worker scheduling out of semantic pixel equality and H1 UNKNOWN without consumer-load evidence.

证据：SYNTHETIC_SCHEMA_RESULTS.json中的“F2 no tile/runtime xtrans metadata accepted”。


上述修订均可限定在比较器与合成负例。无需改变native算法、线程、质量、默认参数或R2库；可以对同一批本地输出重新离线比较，不能因此自动追加RAW。完整load()正例接受7个阶段，布局错配及P8错序负例也被接受；这些是门禁证据，不是RAW异常复现。另有fractional quality被接受，说明整数边界尚可收紧，未列为独立阻断问题。

## 生命周期证据层级

Program.cs:32的ManagedDispose与Dispose返回/拒绝后访问可以实测；nativeRecycle/Close现标directlyMeasured=false。推断计数应注明为绑定调用C导出入口的次数。绑定Dispose IL在32/43偏移调用Recycle/Close；固定源src/libraw_c_api.cpp:177-189把libraw_recycle转入成员recycle并在libraw_close执行delete；src/utils/init_close_utils.cpp:108-110的析构还调用recycle。因此“libraw_recycle导出入口一次”不能等同“所有内部LibRaw::recycle总共一次”。没有为此运行native。

## 执行与未测边界

本sidecar实际RAW Open/Unpack/Process/MemoryImage=0；native API=0；native编译=0；runner/预留/额度修改=0。只在独立review目录运行纯Node合成schema/文件loader检查；输出均为人工零值小型U16，不含真实照片。RR001_SYNTHETIC_ONLY.json使用既有预算器可识别的zeroRaw/tool-tests标记，防止合成负例被当作真实收据。

未测：MSVC实际编译/链接、PE/ABI/依赖加载、native无RAW自检、实际堆生命周期、真实RAW阶段值及H1。新20261109独立两次批次来自用户最新明确授权，本sidecar没有用旧consumed=2阻止它，也没有为它启动进程。

本报告只读交付，不修改A工具、不提交A文件、不等待主线程运行结果。
