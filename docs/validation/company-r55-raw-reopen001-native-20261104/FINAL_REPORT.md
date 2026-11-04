# RAW-REOPEN-001：原生内部边界继续定位

2026-11-04，**Asia/Shanghai**。结论：**REPRODUCED_DCRAW_PROCESS_BOUNDARY / ROOT_CAUSE_OPEN / NOT_FIXED**。产品仍为 PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE，不用“观测器通过”冒充修复。

## 1. 本轮首个不同的有效阶段

同一授权 DSCF0347.RAF、同一随包 LibRaw/binding/依赖、相同实测参数和 native 调用前有效环境，串行两个独立进程，每进程一次默认解码：

|有效阶段|first / second|数值结果|
|---|---|---|
|Unpack 后 active mosaic|完整有效域 SHA 相同|40,186,368 个 U16 样本，0 差异|
|DcrawProcess 后 image 的 RGB 三通道|**首次观测到不同**|5,316 通道 / 3,042 像素，max **146 U16**；mean 0.00039521694 U16|
|MakeDcrawMemoryImage 返回 RGB16|仍不同|5,311 通道 / 3,042 像素，max **4,628 U16**；mean 0.01930339496 U16|

**由此收窄到：本次有效 RAW 解包结果相同，差异已在 DcrawProcess 返回前引入；不是仅由 MemoryImage 输出打包引入。** 结论限于已定义的 active RAW 域与已记录参数，不声称全部 native 隐藏状态相同，不将它进一步冒称为已确定的插值、竞态或未初始化读取根因。

Process 首差 `(4475,499,G)` 为129/120；最大点 `(2492,1990,B)` 为1600/1454。MemoryImage 最大点 `(7482,3485,B)` 为2552/7180。两阶段差异范围均 x507..7691、y499..4969，16px 边界变化数均0。这里是每个阶段分别比较两次运行，再按已核验的同尺寸/flip0坐标关联差异集合；**没有把不同布局缓冲逐字节横比**。MemoryImage 的全部5,311差异坐标包含在Process的5,316中，另外5个Process差异在输出后相同；不把非线性输出前后幅度不同当成新的初始化错误。

完整 hash、坐标/数量/幅度、逐通道统计与有效布局见 `receipts/STAGE_COMPARISON.json`。完整变化坐标CSV、六个像素缓冲只在本地，不进Git。

## 2. 身份与执行限制

- 起点：diagnostic/raw-reopen001-20261104 @ **b14f8381f29d30928a29103abe334ae0000cb301**，现场干净，远端起点相同；原报告/原失败/旧两进程/启动失败目录均未覆盖。
- 产品源码仍 **38dd3f92bb2db7238f7f058008887929ec971c93**；本轮 src/prototypes 零改动，不重建产品、不替换 native DLL。
- 观测源码冻结 **14e62f56b9a24131ed071f5d01e5547391c0882a**（实现5ac7a062…；随后仅runner EOF规范化）。V2.1观测DLL SHA **1CBD7FD8A1DA6878F2C5C3D1B1910BA272F78E98BA7C187CAFEB78BB50DFE4AA**。
- LibRaw **0.21.1-Release**，raw_r.dll SHA **F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9**；Sdcb.LibRaw0.21.1.7/net6.0 SHA **6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3**。每进程核验真实加载路径/hash，25项构建输入/运行文件执行前冻结、执行后复核未变。
- 唯一输入company-02 / X-T5 / DSCF0347.RAF，SHA **52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865**。没有增加照片、Canon复测或Sony研究。

|执行|first|second|
|---|---|---|
|PID|15284|4908|
|时间，Asia/Shanghai|17:14:23.457—17:14:29.713|17:14:29.720—17:14:36.059|
|Open / Unpack / Process / MemoryImage|各1次成功|各1次成功|
|OS峰值工作集|724,279,296 bytes|724,688,896 bytes|
|私有内存峰值，50ms采样|699,195,392 bytes|699,195,392 bytes|
|退出|0；观测完成、不是fix|0；观测完成、不是fix|

**本轮实际完整默认解码=2；Open=2 / Unpack=2 / DcrawProcess=2 / MakeDcrawMemoryImage=2。** 没有第三次、自动重试或App解码。另有4次无RAW自检，各仅 init/recycle/close 1次、上述四项均0；一次比较错环境时点的预检在创建解码进程前停止，四项均0。两次观测工具编译错误及全部早期构建/自检/错误预检保留，不混记为产品失败。仅合计V1.3与本轮诊断（含上轮保留的启动失败，不含原App测试），累计完整解码4次，Open=5 / Unpack=4 / Process=4 / MemoryImage=4。

每阶段只写一份有效数据，增量全域/64行hash；image四槽压成三通道只用一行46,512-byte缓冲，I/O缓冲64KiB。无托管全图复制/float master/方向归一复查。六个本地stage.bin合计 **1,125,218,304 bytes**。外部2GiB/600s上限未触发，native MaxRawMemoryMb仍2048。不把与V1.3耗时/内存差别当产品性能提升。

## 3. ABI 与有效布局依据

详见 `LAYOUT_REVIEW.md`、`receipts/ABI_PROOF.json`、无RAW自检。不是套用其他版本头文件。先核验随包实际字节与固定binding源码，再用实际DLL指令、native accessor返回地址/尺寸、C++ this/imgdata差8字节、saved/current sizes及可读页逐项交叉核对。无法还原native编译flags/补丁，故不宣称整个私有ABI或bit-reproducible构建已经证明。

本样本实际 selected unpacker 是 **unpacked_load_raw()**、flags4096，CFA filters9/colors3。不能因为型号是富士就写成压缩RAF解包分支。raw2image、scale、插值等内部有效buffer尚未单独捕获；实际quality/passes/fuji_width不根据机型或UserQual=-1猜测。

- Unpack：native7872×5196，pitch15744，active7752×5184，left60/top6；只取active单平面，80,372,736 bytes。无方向变换，raw margin/padding不进hash。
- Process：7752×5184，native四槽U16像素/pitch62016；只取R/G/B，第四槽不读作有效样本，紧排241,118,208 bytes。是输出曲线之前的processed RGB，不是sensor、也不是最终RGB16。
- MemoryImage：7752×5184×3×16bit，pitch46512、DataSize=有效字节241,118,208；flip0，保持原UserFlip0。预设raw inset crop元数据不是本轮另做裁切；实际width/height及原有默认cropbox保持。

## 4. 线程环境措辞纠正与参数

原V1.3收据中的 `OMP_NUM_THREADS=6` 是**binding初始化之后**的实测值。旧报告称它必然“继承而来”证据不足：本轮实际binding IL和固定源码表明 `LibRawNative..cctor` 在值为空时使用 ProcessorCount/2，通过它自己的setter设子进程变量。这是既有产品绑定行为，不是本轮线程策略修复。

新进程分别记录：**启动前null → 原绑定初始化后6**，12逻辑处理器，其余记录的OMP/KMP等均null；与旧收据在相同时点比较为0差。父进程/user/machine仍null。观测代码和runner均不设/清OMP/KMP变量，不绕过/替换既有initializer，不强制单线程；实际native worker数/调度仍未测。早期错误预检把新启动前null和旧初始化后6相比，误报环境阻塞；其收据保留并标为OBSOLETE_PHASE_MISMATCH，不删除或改写成真实环境漂移。

本轮两次configured及open/unpack/process参数逐字段一致；与旧V1.3全部共有output/nativeOutput/unpack/color/image字段也一致，只有XmpDataPtr地址不同。配置cameraWB=true、autoWB=false、cameraMatrix=true、sRGB16、HalfSize=false、Interpolation=true、UserFlip0；Gamma[.45,4.5]、NoAutoBright=false、UserQual=-1继承原值。不是用输入hash相同推断参数相同，亦不把XMP地址变化当像素不稳定。

## 5. 旧证据、研究协作与剩余问题

只读复用研究分支 **research/raw-reopen-001-contract-20261104 @ 0e0723df0c8eaf665d2bccdfe150c45621534d3b** 的LibRaw/Sdcb/RawTherapee固定源码、生命周期/许可对照；没有在产品线再搭或执行第二套参考实现。LibRaw cccb9764…、binding43e9c177…、RT5.11 db575c66…，完整SHA/文件hash见SOURCE_IDENTITIES。RT并非同域RGB16独立unpack oracle，成熟数值对照仍NOT_RUN；不能据此批准算法/线程/decoder修改或复制GPL代码。

旧native RGB16有效文件重新流式校验，hash与旧收据一致；它们与新两个输出均不完全相同。四个旧→新配对分别16,594 /18,119 /4,030 /5,392通道差，max5600/5600/4628/3589。旧两次14,275差，新两次5,311差；新增Unpack/Process读盘、采样时点、堆和调度条件确有变化，**差异数量改变不能证明改进，也不能证明新增观测是变化的唯一原因**。故保留全部旧失败，不宣称问题消失。

尚未找到明确可最小修复的ABI、所有权、初始化或生命周期违约。首差只收窄至DcrawProcess范围，具体仍包括raw2image/黑位与scale、pre-interpolate/插值、后处理及convert_to_rgb；内部哪一步首先分歧、是否写前读/边界/并行问题尚未知。下一步只提案，见 `REMAINING_AND_NEXT_PROPOSAL.md`；不再解码，不用单线程、calloc、降质量、改gamma/auto-bright/WB、缓存、换库或放宽断言掩盖。

## 6. 包、交接与结果位置

未修复，因此修复后原App预览、保存重开、TIFF验证 **NOT_RUN**。不重建/发布新应用；原完整诊断ZIP SHA357D0E9D…及入口SHA本轮重核一致，原293文件清单保留、不冒充重跑。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false、全模块、策划15项和教程退役保持。Sony/肤色/高光/gamma校准、20MP/六图等本轮均未扩展。

- 本地完整结果：**E:/Guangjing-RawReopen001-20261104/native-boundaries-v2**。
- 本次两进程：pair-v2_1；冻结：freeze-v2_1；离线同阶段比较：comparison-v2_1；旧输出对照：cross-generation-offline。
- 本报告及元数据收据进入Git，bin/CSV/照片/新native二进制不进入Git。
- HANDOFF.MD / docs/roadmap/R55_IMPLEMENTATION.md 已同步本轮结论与环境措辞纠正；最终LOCAL_HEAD/REMOTE_HEAD与干净状态在推送后读回，保存本地PUSH_READBACK.json并回传，不伪造自引用提交SHA。
