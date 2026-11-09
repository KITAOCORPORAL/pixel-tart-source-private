# RAW-REOPEN-001 新批次插值首差读回

2026-11-09。本报告复用原生验证线已完成的实际编译、自检和新批次输出；研究线没有重新编译、初始化或解码，GPU新增0。F2修复64d4dc6e、四项窄复核关闭记录见f2-closure-20261109；F1/F3沿用fcd4f4f9。V1锐化继续封存。

## 实际产物与批次

原生DLL SHA256：305EEC498644F01EC707E8AC3E58A4B5670043D35FBC853626FF13AC6FF15484。
Header SHA256：ce42b2efa23806da4f1edabbad4901c1a836ff319133507bac6e4315b0d1f718。
编译源：LibRaw cccb97647fcee56801fa68231fa8a38aa8b52ef7 的已审R2观测源码，非原生产DLL。实际工具cl19.44.35229、x64 MSVC、/O2 /openmp；实际编译1次，PE/依赖及源码/合同身份见RUNTIME_BINDING.json。

无RAW自检成功：ABI x64/libraw_data_t381256/processedData16；合成哈希、布局和文件生命周期导出返回0。实测Initialize与托管Dispose各1，释放后访问拒绝；Recycle/Close为固定绑定释放逻辑推断，未作直接导出计数。

新批次RAW-REOPEN-001-VALIDATION-20261109已消费2、剩余0；旧账本consumed2/remaining0原始文件hash不变。PID26928与20016、独立reservation均已对上执行清单和消费记录。每进程Open/Unpack/Process/MemoryImage各1，两个result均completed、error=null；进程峰值工作集分别683532288和683347968字节。使用同一已授权DSCF0347.RAF及原参数/线程环境，OMP_NUM_THREADS有效值6，不由研究线设置。两个进程均有176条tile记录，team实测均6。

## 首差裁定：P6

独立只读插值区间核验重新验证了阶段字段、尺寸/pitch/CFA/extent、四槽全域、WB/scale状态、CIELab/allhex初始化顺序和有效表、实际分支/quality/passes、运行/库/清单/自检身份。对定义明确的P1/P3b/P5/serial-ready/P6文件分块读回，实际内容hash与收据一致；跨进程只比较同阶段，不比较绝对地址。

| 阶段 | 结果 |
|---|---|
| P1 | image4及有效状态相同 |
| P3b | image4、WB/scale有效状态相同 |
| P5 | 完整四槽、边界和有效相机/CFA/矩阵/控制状态相同 |
| XTRANS-serial-ready | 完整四槽、allhex、CIELab lookup及有效矩阵相同 |
| P6 | 有效状态相同，RGB有5127个通道样本不同，max815 U16 |

首个实测差异为(x7451,y497,R)，第一值164、第二值150。变化范围x507..7691、y497..4480。原始坐标CSV和像素buffer留本地；精确首差结果和分块hash见PREFIX_RESULT.json。结论是本批**复现了插值区间非确定输出，最早已验证payload差异为P6**，不是串行准备已先差。

## 完整比较器的实际拒绝不能隐去

64d4dc6e完整比较器在P8拒绝了该批数据：实际rawColor=1，记录明确的out-cam not-consumed-raw-color-bypass事件，没有有效out_cam行，而原完整合同要求rawColor=0和3×3矩阵。没有放宽/删除这条断言，没有伪造矩阵或把旁路当作sRGB色彩转换完成。

为回答本轮P5→serial-ready→P6首差问题，使用单独只读InterpolationPrefix.mjs核验插值前后区间，仍调用已固定F2严格解析与compareStage，并显式记录不接纳下游完整输出合同。这个区间结果不冒充完整P7/P8/P9链准入，也不证明新库与原发行DLL颜色行为相同。完整比较的首次失败和本次有界读回是不同证据，均保留。

## H1：可以继续定位，尚未证明消费值机制

实际相应X-Trans、team6、完整P5和serial-ready相同而P6不同的前提现在成立。已用实测首差关联当前运行记录的tile(top3,left7443,worker0)及其相邻tile(top499,left7443,worker1)；两个进程都有这两条实际记录，T512/ndir8。对应源中的共享读域与相邻回写域存在既有交叠窗口（例如行507..514），现在有实际tile归属，不再以旧源码示例坐标冒充本次实测点。

但这些记录不是时间顺序或同步关系证据。P5/serial快照和tile归属都不提供LIM宏实际多次load的消费值；也没有记录共享槽被哪次写入覆盖后实际消费。空间窗口相邻、两个线程存在及P6首差不能排除其他内部未观测因素，H1仍待验证、ROOT_CAUSE_OPEN/NOT_FIXED。

本轮没有额外peek，没有缓存LIM参数、加锁、改单线程、改质量/decoder或提交像素修复。两个新槽均已消费，不追加第三次。后续要证明共享槽机制，须取得实际消费load与匹配write证据并单独定义观测效应和执行授权，不能由本批相同输入直接宣布根因。

## 交付与复核

RUNTIME_BINDING.json绑定实际编译/自检/输入清单/运行身份/旧账本/新reservation等文件hash；PREFIX_RESULT.json给出有限域结论；first/second-actual-tile-witness仅保存运行日志中的tile元数据，不含共享load值。所有RAW和完整像素文件/CSV留E盘。源码与复核脚本未接入产品像素链。
