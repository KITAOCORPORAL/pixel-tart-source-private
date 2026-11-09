# RAW-REOPEN-001：稳定输入／独立RGB输出候选 V1

2026-11-09，Asia/Shanghai。从用户指定701306788438c1d295541479ea0b1359e7cc85d8独立接续，分支research/raw-xtrans-stable-input-v1-20261109。当前成果是**候选补丁已实现、无RAW合成验证通过；公司原生两次对照待执行，未宣称修复原故障或产品接纳**。

## 1. 本轮增量与停止重复窗口

70130678已在强调试扰动下支持具体H1机制：第二进程某跨tileload消费了邻tile重建G=62，而serialmin为34；未证明所有历史差异都由此产生。该阶段结束。本轮没有调试器、watchpoint、旧窗口重复、RAW读取或decoder执行。

按用户明确方案实现唯一候选 **XTransStableInputSeparateRgbV1**。它与已封存的SharedIncrementEnvelopeUnsharpV1无关；后者保持defaultoff/NOT_ADMITTED，无V2。新候选只允许显式生成到独立诊断源/build，不改产品src/UI/default/配方或原LibRaw发行件。

## 2. 实现：所有worker读取固定image，只写独立RGB3

源基线LibRaw0.21.1@cccb97647fcee56801fa68231fa8a38aa8b52ef7；原xtrans canonicalSHA1c4aa736…2045af。已有诊断xtrans canonicalSHA cf4616cc…e8c84fc，observerheader ce42b2ef…d1f718。两种输入均以完整SHA拒绝漂移；CRLF checkout与canonicalLF分别记录，不把换行当数学变更。

- 串行allhex/CIELab/green上下限准备保持原顺序。
- 该准备结束后，**原image本体成为稳定输入**，不再额外复制一份4槽输入。新增一份每像素3×U16的独立输出；所有RGB先从image填满，包括最终tile不写的外侧区域。
- 并行区的3处共享取址都转到`const ushort (*stable_image)[4]`；最终唯一RGBstore转到`final_rgb`。不会再有worker将重建green写回另一worker仍读取的greenbound槽。
- 原top/left循环、512/496tile尺寸/步长、passes/ndir、LIM/色彩估算/CIELab/导数/同质性/平均公式与整数除法全部保留。
- `parallel for schedule(dynamic)`未改，原firstprivate集合仅新增两个指针；管理输出的RAII对象不进入firstprivate，避免复制析构/双free。
- 等原并行for隐式join和原scratch释放完成，再统一提交RGB3到image，**不写slot3**，释放新输出，最后按原顺序调用`border_interpolate(8)`。未添加锁/末端clamp/清零整幅输入、未降低quality、未单线程化候选。
- 输出使用原LibRaw::malloc/free登记，size_t乘法溢出检查、局部RAII守卫处理后续分配失败。memmgr是owner跟踪，**不是完整process硬内存预算**，不能认为MaxRawMemoryMb自动覆盖新后处理内存。

补丁：tools/raw-xtrans-stable-v1/stable-input-v1.patch（上游）与stable-input-v1-observer.patch（原诊断6处标记版本）。后者candidateSHA **f11b4e4b46cf1c863f0af0fa469be05ad2e4a3f3d1c854ace4b66064528dbb18**；实际上游无RAW测试candidateSHA **b6500414c0c556c6a93ea25a26f59bb34056890ca93856d2a43b75da3a6116d1**。

worker逆向地址归一化后与原worker文本逐字节一致。这证明的是源级公式/控制流保留，**不声称新二进制机器指令完全相同**；额外缓冲和复制必然改变堆布局、时间及代码生成。

## 3. 共享读取完整清单与输出写区

|共享项|固定源码位置|候选约定|
|---|---|---|
|image RGB3 tile复制|xtrans205–209|只读stable_image|
|共享image的CFA色彩估算stencil、slot1/slot3界|220–232，及多遍249–257|两个const pix别名，所有派生offset仍指向同一stable_image；不是仅换LIM中心值|
|width/height/colors/CFA6×6|各循环、fcol、CIELab|原context独占调用，不允许并发参数变更；只读|
|allhex/passes/sgrow/sgcol/ndir|原firstprivate|原私有副本/值不变|
|orth/patt/dir|staticconst及shared(dir)|无写操作|
|CIELab cbrt/xyz_cam|serialcielab初始化→worker读|原准备不动，region内不写表|
|rgb/rix/lab/drv/homo scratch|每worker私有分配/别名|与原实现一致；本候选不趁机改插值或padding|
|最终输出|原417单store|独立RGB3；不同tile写区不交叠|

生成器对原worker访问清单作严格数量/锚点检查，candidateworker中没有剩余`image`直接访问。上下/左右所有邻域load通过上述3处地址继续使用固定输入，非只修已观察到的两个点。

原tail公式形成各轴无重叠连续半开写区[6, length−6)。纵横轴的笛卡尔积分区覆盖整个内矩形，**最终tile输出彼此不重叠**；旧故障不是两个tile最终写同一个RGB，而是邻块输出写进对方还在读的输入。新RGB输出全域先初始化，所以内矩形之外的提交是原值，不引入未写输出；边缘8仍在提交之后处理。

整数区间检查覆盖512..4096及5184/7752/8192/65535共**3589个长度**，9组二维边界含512最小图、518/519条件接点、1014/1015末端接点以及真实7752×5184。均覆盖连续且无重叠/越界。它是尺寸/所有权算术，不是RAW验证或完整内存sanitizer。

## 4. 额外内存与生命周期

新增请求量=6×width×height bytes。Fuji7752×5184为**241118208 bytes=229.9482421875 MiB**，原memmgr默认另有1024bytes尾部开销；具体allocator实际分配/驻留仍需公司测。没有额外全图4槽snapshot，也未新增tile scratch。

为保持外侧RGB和初始化契约，增加两次全图RGB复制（初始化、join后提交）；时间/带宽代价尚无真实RAW测量。noRAW输入稳定性断言额外保存4槽witness仅存在合成surrogate中，**不在诊断候选补丁中**，不得将其RSS当生产候选独占开销。

新输出RAII在后续scratch allocation异常时释放；正常commit后明确释放再border。保留原错误传播，不将allocationfailure当部分成功输出。合成OOM/过小尺寸拒绝检查见summary；没有为便利增加真实进程预算。

## 5. 无RAW编译与合成算法验证

家庭缺原MSVCcl/nmake/dumpbin，vswhere/VS安装显式位置也未发现；未新装工具链或用另一native构建代替公司基线。已有冻结LLVM clang/libomp用于**抽取原真实函数体**的syntheticstorage/allocator：

- 原xtrans完整worker、原CIELab、原border_interpolate、原malloc_omp_buffers函数体从固定源提取；不是自写简化插值。
- source-backed单tile隔离oracle：每个tile从同一未改合成CFA单独执行原算法，仅合并该tile原写区，再调用一次原border8。这是共同源码血缘的稳定输入对照，**不是第二独立色彩算法/物理真值**。
- 候选编译成无RAW合成exe并通过真实LibRaw头文件语法编译；fullLibRaw DLL/ABI和MSVC/VCOMPnative selftest尚未执行。真实头文件只出现原strcasecmp等宏重定义warning，保留。
- 实际候选40次成功调用：5个尺寸×passes1/3×请求线程1/2/6/重复6；两种scratch毒值交替，动态调度结构不改。syntheticonly设置threadrequest/dynamic不修改生产线程环境。这里记录的是请求线程数，非生产实测team。
- 56次单tileoracle、10次原串行算法调用；合成维度为512×512、521×519、1009×1015、1024×1024、1553×1007，覆盖平坦/阶跃/固定噪声/梯度CFA及相邻/末端块。
- **40次候选均对oracle四槽全值相等**，因此有效RGB和slot3/边缘均检查；join前image全值未变，border8恰好一次且在commit后；外图及分配canary正常、成功后live0。
- 另3个拒绝/释放条件通过：新增输出首次分配失败、后续scratch分配失败、宽511被原小图条件拒绝；合计43项C++检查。8个Python准备守卫测试通过（漂移/重复补丁/默认未启用/已存在输出/保护根嵌套拒绝/observer兼容等），不把3589尺寸检查合并叫数千个真实运行测试。
- 整个合成exe一次运行约13080ms，采样进程峰值238952448bytes，最大单context请求分配峰值157232490bytes。仅LLVMsurrogate和testside数据；不是公司原生RAM增量/速度保证。运行有240s/2GiB守卫，未触发。

对原串行合成结果（**不是已存Fuji基线**）的差异：

|夹具|不同RGB分量|max U16差|
|---|---:|---:|
|512x512-p1|0|0|
|512x512-p3|0|0|
|521x519-p1|696|8609|
|521x519-p3|1431|8609|
|1009x1015-p1|8940|13345|
|1009x1015-p3|11770|11496|
|1024x1024-p1|2026|685|
|1024x1024-p3|4588|1890|
|1553x1007-p1|13241|12401|
|1553x1007-p3|19350|14625|

单tile两种passes均0差；多tile出现差异在预期内：候选刻意取消原串行也会发生的跨块输入反馈。既不能强求多tile原bug字节保留，也不能拿差异幅度当真实照片变好或已修复根因。

## 6. 与现有诊断构建接线及公司后续

stage_diagnostic.py已用固定原Prepare-Internal的7份生成源/header重建静态材料并核验，其SHA严格匹配70130678的BASELINE_BINDING。新完整诊断源217文件准备通过，不构建/不执行。仅改变xtrans，原其他6份instrumentedsource、observerheader和检查点保留。

新CANDIDATE_PREPARATION单独记录故意改变的candidateSHA/公式证明与FILES清单。**不能伪称originalByteRestoration，也不能弱化旧Build-Isolated的原无像素更改门禁。** 专用Build-Candidate.ps1只对这一独立candidate源执行原Makefile.msvc DLL目标，COPT_OPT=/openmp保持，缺工具链即拒绝，不自动跑RAW。该脚本在家只做PowerShellAST语法检查，未声称MSVCbuild通过。

公司最小执行入口和状态见COMPANY_HANDOFF.md、COMPANY_NATIVE_PLAN.json。当前用户已经授权：编译/native noRAWselftest通过后，**独立新任务批次最大2个进程，同一Fuji各一次无调试器解码**。每次先持久预留，失败消耗并停止，不动旧consumed账本、不自动第三次，不重跑旧base解码。

需要比较：候选pair的P5/serial输入状态一致与P6全有效RGB逐bit一致性；每个candidateP6分别对公司已存4c322975的baselinefirst/second做差异计数/坐标/幅度/边界；实际线程、passes3/T512/ndir8、完整参数及资源开销。原F2完整比较器P8rawColor旁路限制保持，必要时沿已有verifiedprefix，不放宽全管线门禁。

**本轮原生结果：NOT_RUN_HOME_TOOLCHAIN_MISSING**。缺cl.exe/nmake.exe/dumpbin.exe及相应MSVC SDK开发环境；候选fullnativeDLL、自检、真实pairP6一致性、相对已存Fuji基线差异、native RSS和时间全部未测，字段null。并非缺源码或没有实现候选。未访问公司E盘取文件，未假造旧baseline像素，未读取同一RAF。

## 7. 许可证、失败与交付边界

LibRaw0.21.1为LGPL2.1或CDDL1.0；patch少量上下文及抽取实际算法仍受原许可边界，不能换名认定脱离。RT5.11既有相同血缘的rawData→独立RGB设计仅作结构参照，没有移植其GPL代码/浮点算法/114tile。原policy/源码对照结论继续保留，当前仅诊断候选，非默认像素节点/发布许可准入。

Git按本次用户明确“提交补丁与合成验证”提交自编生成/诊断构建/无RAW测试源、小型unifiedpatch、hash/统计；完整vendor源、exe/DLL、合成完整输出与任何真实私有像素仍留授权外部目录。

失败原样保留：文件创建helper初始引号错误未写出文件；首次generator拒绝CRLF时尚未生成候选，改为显式canonicalhash及rawhash分账；初build01因surrogate漏fcol无法编译，修support后新build02成功，算法patch未调参。无RAW重试，无调试器运行。

研究当前交付 **CANDIDATE_IMPLEMENTED_SYNTHETIC_PASS / NATIVE_PAIR_PENDING_COMPANY**。新增真实RAW/Open/Unpack/Process/MemoryImage/debugger/GPU/产品build均0；合成算法调用和编译已如实单列。原问题候选修复有效性尚待原生两次对照，不宣称完整摄影/产品通过。本批到handoff停止，不再抓H1窗口。
