# RAW-REOPEN-001 家庭接续核验与 WIP 交付

2026-11-05，Asia/Shanghai。状态 **LOCAL_PREPARATION_PRESERVED / CHECKPOINT_CONTRACT_PARTIAL / NATIVE_TOOLCHAIN_UNAVAILABLE / NOT_FIXED**。

## 1. 核验实际本地，而非以远端推断

本次开始fetch成功。原远端diagnostic/raw-reopen001-20261104仍c7df75d4ec8dde31cb1bd6d33ce3726eb2db7c39；家庭独立树diagnostic/raw-reopen001-home-20261104实际HEAD为bb19e8e1326c108f538e60b33501ee229ba133f7，起始工作区干净，且该分支当时远端无ref。bb19e8e1确有工具/源码准备，是未推送成果，不是“尚未开始”。上轮推送连接重置收据继续原样留本地。

相关旧产品树仍f27f4409，原2个修改文件及2个未跟踪测试保留；研究树起始ef95df88、干净。审查期间研究HEAD推进至5a4f233d，只新增推送成功收据/交接，没有改检查点内容；本工具使用已存在的固定ef95df88 Git对象读取规范，不依赖随时可能移动的研究工作树HEAD。第一版把HEAD必须相等的guard失败单列保留，不当产品失败或native执行。

公司当前本地状态、未公开新增输出及执行次数均 **UNKNOWN_NOT_ACCESSIBLE**；不尝试访问/猜测公司E盘。Git中的历史公司观测只作为归档证据，不据其推算公司现在在做什么。

## 2. 已有成果与实际完成度

- 固定LibRaw源码cccb97647fcee56801fa68231fa8a38aa8b52ef7的唯一家庭共用根仍为raw-reopen001-shared-research/libraw-0.21.1；不再clone，不重新研究RT。已有三份隔离生成树、三次显式生成器自检、四次托管构建（1失败/3成功）及全部日志仍在。
- 已有源码：Prepare.mjs可逆插入、rr001_observer.h流式hash、Build-Isolated.ps1、PreparationHost.csproj/Program.cs无RAW ABI宿主、Inspect-Inputs.mjs。实际原生DLL **未生成**；上轮BUILD_STATUS明确NOT_BUILT_TOOLCHAIN_UNAVAILABLE。托管宿主成功编译不能改写为原生ABI/无RAW自检完成。
- 当前PATH依然缺cl.exe/nmake.exe/dumpbin.exe；常见VS目录只有COMMON/IDE，四个VS/Windows SDK Installed Roots注册表入口均未发现。本结论是已检查位置没有可用工具链，不声称扫描过所有磁盘。dotnet存在，但.NET MSBuild不代替C++/Windows SDK。
- 同一已知授权RAW存在，SHA52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865匹配；只读取该路径做SHA，没有换图/扫描其他盘。

## 3. 本轮新增的实质工具与结果

新增Audit-LocalState.ps1：只检查指定诊断根/工作树/授权文件，列出commit/dirty、构建/调用收据、consumed slot、原生DLL及工具链/SDK发现结果，不加载native、不分配新解码额度。实际运行结果为localConsumedSlots=0/localRunReceipts=0/localNativeDlls=0，已保存LOCAL_AUDIT_TOOL。

新增Bind-ResearchContract.mjs：绑定研究线固定报告/CHECKPOINT_SPEC/CHECKPOINTS/H1/INTERNAL_SOURCE_LOCK的Git blob SHA；使用共用完整LibRaw源码核对 **27份**内部锁文件，全部通过；核对现有生成CPP/header身份，并移除插入标记验证原算法字节恢复；六个主payload静态调用点存在。新增十项合成验证，包含源码/版本/重复记录/生成文件/header漂移拒绝、static presence不冒称runtime与缺失检查点拒绝，全部PASS、native/RAW调用0。

**发现并明确交接的未完成项：** 原补丁black-after在adjust_maximum等之前，不能冒称研究P2（pre_scalecolors回调后）；实际scale_mul/WB分支、out_cam有效列、完整布局/CFA/指针代数、回调/后处理事件等还未完整捕获。P5目前只有稀疏CFA样本hash，不能推出完整初始化输入状态相同，进而不能单凭P5/P6命名就确认H1。RESEARCH_CHECKPOINT_BINDING逐点列出缺项并返回executionReady=false。没有把静态调用点存在包装成可安全开跑。

## 4. 原授权次数与运行证据

本地上轮HOME_EXECUTION.json记录Open/Unpack/Process/MemoryImage/nativeInit/完整显影全部0；本次指定诊断根内没有consumed slot、calls/result运行收据、NO_RAW_SELF_TEST.json或新库产物。结论是**这份家庭计划没有执行证据，且原生库尚未构建，不能把它当已完成的两次观测**。公司现场UNKNOWN，不据此新发两次，也不把旧公司归档V1/V2混记为这份新库计划的消费。

本轮新增Open=0、Unpack=0、DcrawProcess=0、MemoryImage=0、native init=0、原生编译=0、原生ABI/无RAW自检=NOT_RUN。只运行文件/Git/hash/静态与合成检查。没有反复运行同一失败native构建，没有自动重试RAW，也没有原生产DLL对照。现有最多两个独立进程额度仍属于原任务，换机器不重置。

## 5. 精确阻塞与下一条操作

阻塞分开列出，不能缩写成“只差一次运行”：
1. 环境：未发现cl/nmake/dumpbin及已登记Windows SDK。需要已有可用x64 MSVC Developer shell、C++工具集和Windows SDK；不以.NET SDK代替。
2. 验证：新DLL/真实编译器、宏/flags/可选组件/依赖/hash尚无，offsetof/sizeof/固定binding/accessor/原生合成hash与生命周期自检均未执行。
3. 工具：完整研究检查点元数据/输入状态合同尚未满足；真正的bounded两槽RAW host/runner尚未实现/冻结。旧V2.1固定hash/ABI的runner不能删检查后换库开跑。研究合同和P5限制已通过新绑定工具列清。

当前可执行且已完成的是核对/合成绑定与提交推送WIP。工具链就绪后的第一条native操作是以下命令；只构建/无RAW自检，不会启动解码：

```powershell
$binding = Join-Path $HOME '.nuget/packages/sdcb.libraw/0.21.1.7/lib/net6.0'
& ./pixel-tart-source-private/raw-reopen001-home-native/tools/RawNativeStagePreparation/Build-Isolated.ps1 -PreparedRoot ./raw-reopen001-shared-research/isolated-source-03 -BindingRoot $binding -OutputRoot ./raw-reopen001-shared-research/native-build-next-20261105
```

必须NEW输出根；上轮失败/生成树不删除。若原生编译报观测工具错误，保存日志后修对应工具，不改原算法/初始化/运算顺序/线程。之后补合同与runner、固定完整身份和启动/binding后环境，才依据原任务已消费收据决定能否执行，不能让换机生成的新slot根绕过总次数。

## 6. 结论、交付与保存

没有新的首差观测。已证实边界仍有效Unpack相同、Process image已不同；更细raw2image/黑位、scale/pre_interpolate、实际插值入口/返回、convert_to_rgb首差及H1仍UNKNOWN/OPEN。不能重新笼统归因保存丢参数或MemoryImage单独引入。新构建来源/flags与原NuGet仍未证明等价；以后新两次相同也只能写新构建本次未复现。

产品源码38dd3f92bb2db7238f7f058008887929ec971c93保持，src/prototypes零差异；未重建应用包，未改生产native/默认/线程/quality，不用calloc/缓存/换插值/换decoder，不扩Sony/gamma/肤色/高光。旧树/失败/私有输出保留；无私人RAW、像素缓冲、数据库、第三方源码或受限二进制提交。

本地本轮结果raw-reopen001-shared-research/resume-20261105-01；Git仅工具、研究绑定数值/身份、审查与执行计划/交接。HANDOFF/R55大纲已更新。当前独立分支承载先前bb19e8e1和本轮增量，最终LOCAL_HEAD/REMOTE_HEAD与工作区状态以提交推送后单独读回为准；不自填尚不存在的提交SHA。
