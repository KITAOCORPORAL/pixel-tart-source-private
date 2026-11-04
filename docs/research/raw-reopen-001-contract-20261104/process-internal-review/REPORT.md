# RAW-REOPEN-001：DcrawProcess 内部源码审查与观测交接

2026-11-04，Asia/Shanghai。研究基线 `0e0723df0c8eaf665d2bccdfe150c45621534d3b`；产品新观测 `c7df75d4ec8dde31cb1bd6d33ce3726eb2db7c39`。状态：**INTERNAL_CONTRACT_REVIEW_COMPLETE / ROOT_CAUSE_OPEN / NOT_FIXED**。

## 结论先行

1. 接收并复用新事实：有效 Unpack mosaic 相同，**Process 后 image 已不同**。旧“先补 unpacker/外层复制观察”的计划已经被产品完成，本轮不再建议重跑。
2. 已实测 unpacker=`unpacked_load_raw()`，flags4096、filters9/colors3；OMP 启动前 null→原绑定初始化后6，有时点与绑定 IL 证据。**不是必然外部继承，也不是已观测实际worker数6。**
3. 给出 raw2image/黑位、scale、pre_interpolate、实际插值选择、插值返回、convert 前后精确检查点、域/布局/初始化合同（CHECKPOINT_SPEC.json、CHECKPOINTS.md）。这只是可落到产品隔离观测补丁的设计，研究线没有实现/运行 native 观测。
4. 找到一个具体、条件性的源级风险：**X-Trans 并行 tile 读取共享image的区域，与邻tile最终RGB回写区域交叠；非绿站点槽1由“green下限”变为“重建green”。** 有明确读写位置和地址实例，不能归结为“malloc未清零/有OMP”。这是唯一H1；实际生产分支、worker、首差阶段与消费值仍缺，**尚不足以认定本故障根因**。
5. RT5.11提供了只读rawData→独立RGB输出、局部green上下限的相关对照，但它同属Frank Markesteijn/dcraw血缘，且输入端复用LibRaw、浮点/尺度/边界/相机矩阵不同；不是生产同域独立真值。本轮不运行任一参考。

## 1. 最新事实替换旧待观察项

|事项|本次接收的实际证据|不能再作出的旧推断|
|---|---|---|
|输入/依赖|产品源码38dd3f92…；诊断源码14e62f56…；native0.21.1 SHA f500c073…；binding0.21.1.7/net6.0 SHA6df69864…；完整SHA见PRODUCT_EVIDENCE_BINDINGS|研究0.21.1源tag不等于已还原该nativeDLL编译flags/patch|
|有效Unpack|5184×7752单平面U16，40,186,368样本，0差|不再将解包输出不同作为当前观察结论|
|Process image|5316通道/3042像素差，max146 U16；首差(4475,499,G)129/120|不能只把MemoryImage或托管copy当首差|
|MemoryImage|5311通道/同3042像素差，max4628；其差异坐标包含于Process集合|不同域幅度变大不能直接证明另一个初始化bug|
|实际分支信息|unpacked_load_raw、filters9/colors3、flip0；raw7872×5196/pitch15744，activeleft60/top6|不能因为Fuji型号写compressedRAF路径|
|线程环境|parent/user/machinenull；child启动前null→bindingcctor后6；ProcessorCount12；native worker/team仍未知|不再称OMP6必然继承，不把绑定默认初始化当新bug/修复|

已有参数时点也要区分：unpacked-parameters的nativeOutput还是提交process参数之前的默认值（例如output_bps8/use_camera_wbfalse）；configured及processed才是本次sRGB16/cameraWBtrue/no_interpolationfalse。把这些时点混比会制造假差异。

实测pre/post状态支持但不代替内部捕获：Unpack black0、maximum16383、cblack6×6均1020、cam_mul[525,302,585,0]、white8×8全0；Process返回black0、cblack全0、maximum15363、data_maximum3724、pre_mul约[1.7384106,1,1.9370861,1]。后者不能倒推出中间具体哪个callback/branch被执行。

产品原布局报告已严格区分：
- Unpack地址raw_image+(y+6)*15744+60*2，单CFA样本；不读margin/padding。
- Process地址image+8*(y*7752+x)，只取槽0/1/2；行62016byte，紧排3通道241118208byte。
- MemoryImage3通道紧排pitch46512、后续gamma/autoBright。它不是Process image域。

本轮只读Git中的已有汇总/源码，未读取该RAF或完整自然像素CSV/buffer。产品完整source/receipt Git blob SHA单独绑定，未合入产品分支。

## 2. 固定源码与可靠性边界

- LibRaw0.21.1：`cccb97647fcee56801fa68231fa8a38aa8b52ef7`。
- RT5.11：`db575c6690d30ce6428c0e1383bec8b8f1fb8d21`。
- 复用家庭既有RT固定归档，仅解出明确源码文件；LibRaw旧锁指向公司E盘，本轮未访问E盘，按固定commit从官方源补齐相关文本。35份文件/35精确行范围绑定于INTERNAL_SOURCE_LOCK；17份已有锁文件SHA完全匹配。
- LibRaw为LGPL2.1或CDDL1.0；RT所读XTrans为GPL3-or-later。参考源完整字节只在本地授权资料根，不拷进产品/Git，不构建、不执行。
- native源码可追溯性仍有缺口：版本、实际DLL、部分ABI、输入/外层调用已实测，不等于该DLL的私有字段、LIBRAW_USE_OPENMP/LIBRAW_NOTHREADS宏和代码完全与tag对应。新instrumented binary须单独身份，不能把它说成原发布DLL被动捕获。

## 3. 内部路径与域/初始化审查结果

### 3.1 raw2image_ex 与黑位
`dcraw_process.cpp:39–45`中的is_bayer其实表示“有CFA或单色”，filters9同样为true，不可按变量名排除XTrans。subtract_inline取决于bad_pixels/dark_frame/zero_is_bad，后一个内部值未观测。

`raw2image.cpp:18–50`先恢复rawdata.saved color/sizes/iparams/ioparams，并由half_size/threshold/aber决定shrink。`395–402`对image全分配范围calloc或realloc后memset，**四槽不是因malloc而默认未初始化**。普通mosaic分支`262–296`按fcol写单槽，index由iwidth/shrink确定；行worker私有ldmax，dmax更新受critical保护。当前配置half=false/threshold0/aber1按源规则预计shrink0，但补丁仍记录实际值，不能把shrink碰撞路径套到未命中本例。

inline黑位后`538–545`清black和cblack前4项；repeat黑位网格要看adjust_bl是否已消除。`dcraw_process.cpp:79–89`会按subtract_inline/data_max决定是否再adjust/subtract，并adjust_maximum/user_sat。精确记录路径，不能两次都按“原始black1020”算，不能把未采样槽0当原生黑像素。

### 3.2 scale 与 pre_interpolate
`postprocessing_utils_dcrdefs.cpp:111–252`实际选择user_mul/autoWB/cameraWB/white8×8或cam_mul fallback，再执行可选wavelet，归一pre_mul并计算4个scale_mul；scale_mul在正常/回退分支均写全。当前cam_mul/white记录与走camera倍数路径相容，不冒充中间branch事件已存在。`postprocessing_utils.cpp:144–189`处理全部4槽，非零黑位时跳过0；float倍乘最终截断/clip回U16。这里仍是**相机域CFA/已缩放码值**，不是sRGB或displayfloat。

`misc_demosaic.cpp:21–78`可能half shrink、重新calloc展开、copy CFA并free旧image、重映射green/filter。对本例filters9且若shrink0/half0，该函数按源基本不改像素；实际前后pointer/shape/filters必须记录。若unshrink重新分配，后续width stride不能机械沿用旧iwidth；旧指针不得留到下一阶段。

### 3.3 实际插值选择
`dcraw_process.cpp:74–77`quality=2+!fuji_width，user_qual>=0才覆盖；143–181先执行pre_interpolate callback，再按CFA/no_interpolation/custom callback/quality顺序分支。

对“filters仍9/colors仍3/no_interpolationfalse、无替代callback、quality默认2或3”的条件，源代码走xtrans_interpolate，passes为1或3。但**实际fuji_width、quality、callback及入口/pass计数未采集**；UserQual=-1不是三遍实测证据。要在被选中的调用处记录分支，而不是另算一个预期算法名。

### 3.4 X-Trans scratch 与边界
`xtrans_demosaic.cpp:25–175`校验CFA、预填allhex哨兵、初始化CIELab表、构造共享image槽1minGreen/槽3maxGreen。源码T=512，步长496。每worker有独立malloc scratch，每tile只复制有效矩形，其余padding未保证初始化；这不是可直接判bug的事实。

可明确的写后读关系：RGB前4方向复制后才细化，pass1再复制至后4方向；CIELab只写local[2,mrow−2)；drv只写[3,mrow−3)，其±1 Lab读取落在上述写域；homo全ndir×T² memset后写[4,mrow−4)，其±1 drv读取也在写域；最终homo邻域和尾tile扩展仍以实际mrow/mcol区分。不能对整块未使用scratch作hash然后把差异叫有效像素首差。完整RGB邻域逐load定义覆盖本轮未动态验证，不宣称已证明所有scratch安全。

全图border_interpolate(8)在tile工作结束后执行（428），只取CFA实测站点插边。`borderChangedChannels=0`仅排除此次外16px的变化，不排除内部tile交叠带。源级具体共享image风险见第4节；不另提出“malloc统一清零”假设。

CIELab的tls是LibRaw对象持有的表指针，不因名字而假定每worker不同。cielab(0,0)在parallel前按sentinel初始化cbrt、并按当前colors填xyz_cam，之后worker只读；本次每进程独立context，无证据指向这个表的并发重初始化。

### 3.5 后处理与 convert_to_rgb
插值后仍有mix_green/post_interpolate callback或median、highlight、fuji_rotate、ICC及preconvert callback，最后convert、postconvert callback、stretch。配置median0/highlight0/profile空/ExpCorrecfalse能排除对应正常分支，但fuji_width/内部callback需实际记录。use_fuji_rotate=true不代表一定旋转：函数fuji_width0直接返回；stretch的pixel_aspect1也直接返回。

`convert_to_rgb:21–108`先memcpy/形成out_cam，raw_color可跳过矩阵。mode0 gamma_curve用于参数/profile计算，**这里没有给image逐样本应用MemoryImage的OETF曲线**。`convert_to_rgb_loop:79–142`先memset histogram，再串行读同像素相机通道，所有out[0..2]算完才存回，不存在这里“写R后用新R算G”的顺序错误。只记录实际colors列的矩阵；不要hash未使用stack列/padding。后的RGB U16已转到声明的输出原色并clip，尚非MemoryImage gamma/auto-bright查表域。

## 4. 唯一可证伪假设 H1：shared image 的 green下限被邻tile回写

**这是条件性源级读写重叠，生产根因未证。** 有以下具体关系：

- 写前准备：xtrans:103–147在非绿色站点将image[site][1]写为minGreen，slot3为maxGreen。
- 读取：xtrans:205–209复制共享image的RGB3；220–232/249–257还直接从同一共享image读取slot1/3作为LIM上下界。
- 回写：xtrans:388–417将最终RGB3写回共享image，包括非绿色站点slot1。它从“绿色下限”变成了“重建绿色”。
- 并行：168–180以top tile动态并行，worker局部scratch不等于全局image只读。单次top之间没有每tile同步，循环末屏障晚于这些访问。

只做源码下标算术、**没有读自然像素或运行插值**的实例：T512、stride496、width7752。tileA(top3,left3)读行3..514、写6..506；tileB(top499,left3)读499..1010、写507..1002；两者最终写区不交叠，但A读/B写在507..514相交，B读/A写在499..506相交。按产品6×6 CFA，(x500,y507)是蓝站点，image+31,446,114byte即slot1：A可把它当minGreen读取，B可把它写成重建green。**这不是该坐标发生差异的实测，也不能只凭首差y499靠近tile边界认定故障。**

最小检查与证伪逻辑：
1. 先捕获P5实际插值入口与有效image/state、P6真实插值返回；如果P5已经不同，H1不能解释“首差在插值”，先看更早P1/P3。若根本未走该XTrans并行路径，H1不适用于本例。
2. 只有确认P5同/P6差且该分支实际命中，才在产品的隔离观测里记录限定重叠站点的greenbound准备值、**实际消费的load值**、对应tile最终写值和top/left/worker/pass/phase；线程私有有界日志，join之后写盘，不并发扫描整幅image作hash。
3. 若消费的下限偏离阶段初始值，且与邻tile回写吻合，则支持该机制；仍需因果控制验证，不能凭地址交叠直接宣称修复。若读取值保持原界或差异更早，则该假设对该次执行不获支持；单次未出现也不证明所有调度都安全。

重要观察扰动：MIN/MAX/LIM宏可能重复求值参数。额外前后peek不等于表达式实际消费值；先把上下界缓存成一个local会改变重复load/竞态行为，**不能冒称完全被动等价**。更强日志或读写检测必须单独记录源码/编译/二进制及调度影响，由产品安排，不由研究另跑。不建议直接上锁、清零、单线程或替换decoder。

## 5. RT5.11具体对照与共同血缘

RT rawimage.cc:786–823复用LibRaw open/unpack/raw2image，并不是执行同配置dcraw_process；其conditionalmutex也不能证明此生产问题由OMP引起。其scaleColors(2742–2873)是在单平面float rawData上减黑、乘scale，且XTrans没有生产U16同样的上界clip；后续相机profile/输出变换不同。

RT xtrans_demosaic.cc:176明确Frank Markesteijn，658也注明dcraw CIELab来源，非完全独立算法。源码对照有价值的是：tile114、局部greenminmaxtile由rawData计算，408先清首方向tile、412只写CFA样本；944–946写独立red/green/blue数组，**不是回写作为本轮输入的rawData**。与LibRaw共享image读写风险在所有权结构上不同。

RT采用float候选、可选Lab/YUV判据、不同边界（passes>1为8否则11）、不同线程分块、明确方法参数；其CLIP宏在179–180处甚至为identity，最后输出只max(0)。所以“RT清零而LibRaw没清零”不能孤立作bug证据，RT也不是生产同域像素truth。本轮数值比较和两个参考执行均0。

## 6. 交付给产品的下一事实与停止边界

**从已有有效Unpack相同往下看，最早缺的是raw2image_ex成功返回时的有效CFA/image与黑位状态是否相同。** 为验证H1，另外必须有实际demosaic入口image/state同而返回不同的阶段事实。准确位置/格式见CHECKPOINTS.md和CHECKPOINT_SPEC.json，不回到重复Open/Unpack/MemoryImage外层两进程计划。

产品统一安排隔离instrumentation。固定源构建新DLL时必须声明“新二进制观测”，记录source/patch/flags/依赖、真实_OPENMP/LIBRAW_USE_OPENMP/LIBRAW_NOTHREADS、team调度；不能对原不明私有ABI增猜偏移，不能用custom interpolate callback替代实际算法，也不能因观测PASS称修复。进程与次数另由产品批准，本研究无运行指令自动排期。

本轮无足够根因/像素修复证据，ROOT_CAUSE_OPEN；没有产品默认、params、锁、清零、decoder修改。新增RAW/LibRaw init/unpack/process/memory/GPU/build/参考运行/产品包全部0。只做Git/source读取、文件hash/引用核验与静态索引检查。旧锐化V1继续封存、defaultoff/NOT_ADMITTED；不扩跨GPU、BT、20MP、胶片、Sony或新机型。

原研究REPORT/PRODUCT_DIAGNOSTIC_PLAN/SOURCE_LOCK及产品历史失败保持，当前检查点指向本增量；不改旧文档冒充其当时已知新事实。完整本地源码根见INTERNAL_SOURCE_LOCK，仅源码文本，Git仅合同、摘要、hash与观测计划。完成本次交接后等待产品内部阶段事实。
