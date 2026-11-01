# 光镜v3数值差异：阶段归因闭环
2026-11-01 Europe/Helsinki。基线 home/r55-canvas-photo-preview-20261101 @17e9788f37926e52faeca2dd599b8b9efbbef2cf。

## 本轮结论
**不改产品源码，不重建发布包。** 两项差异均已定位到可重现的阶段行为，没有发现产品调用、通道、域、方向或处理顺序错误需最小修复。原照片、profile、阈值和FAIL原样保留。问题从“原因未知”结束为“不同数值算子的量化差异”；不是把旧验收强行变绿，也不是通用ICC/相机色准批准。

- JPEG：FFmpeg lowres2不是WIC/IJG式缩小的等价算子。2x2 reduced-IDCT使用不同频率贡献，加上4:2:0分量缩放不同，RGB阶段默认色度重建还引入次要误差。
- ICC：native精确、方向精确；同profile的Windows MSCMS BEST_MODE/BGRA路径精确复现WIC颜色结果，LCMS精确路径与独立matrix/TRC oracle精确相符。系统CMM的颜色/量化行为与LCMS不同，非产品通道/方向错误。WIC内部专有CMM数学和隐式选项不可观察，未编造其确切插值/黑点/适配实现。
- 旧门槛仍FAIL：原4:2:0 max40/mean1.633569/P95=4；ICC两例max3。旧数值脚本重跑仍exit1。

## 启动核对与不动范围
fetch后HEAD与origin均17e9788f，0/0、干净。读取AGENTS、POLICY、R55_IMPLEMENTATION、上一轮FINAL_REPORT/NONPASS/NODE_COMPARISON。沿用原树，所有历史输入/输出/失败留存。本轮只加tests及本批docs；灯台、RAW/ICC研究引擎不合入，不扩UI。src相对146c0fd2逐文件无差异；原保存v1/v2/v3快照和旧导出路径不变。旧树未reset/clean/stash，不合main/强推。

## 1. JPEG：分开看系数→平面→RGB
### 1.1 原输入身份及尺寸
原photo24-420.jpg SHA256=459440e7d5bfa41f49205eadff35c796b711f29d4718082f67f63bd762fcd225，6000×4000 baseline8、Y采样2×2/CbCr1×1。两条路径均factor4、输出1500×1000。native阶段不让ICC/EXIF参与，按原JPEG扫描坐标读；通道显式BGR→RGB。33MP同7008×4672→1752×1168，ICC后再方向6。

WIC默认Planar0的Cb/Cr=1500×1000，PreserveSubsampling1=750×500；FFmpeg lowres2的Cb/Cr=750×500，仍需上采样。不同分量并非共享相同IDCT尺度。实测WIC默认Cb与FF lowres1（较大4x4分量IDCT）比较max6/7、mean.232/.279，而FF lowres2的较低色度网格已经丢掉更高频信息。

### 1.2 不是“舍入几码”的证据
原创建系数夹具4096×64、Q=1、DC=128，仅一水平AC分量非零：
| 夹具 | WIC输出首行 | FFmpeg lowres2 |
|---|---|---|
| 全DC |128/128|128/128|
| Y AC1=80、444 |137/119|138/118|
| Y AC3=80、444 |125/131|128/128|
| Cb AC3=80、420 |133/117/139/123|128/128|
| Cr AC1=80、420 |141/133/123/115|138/118|

WIC与Pillow/libjpeg3.1.4.1完全相同；IJG固定源码jidctred.c的2x2是四相邻完整样本的面积均值，使用AC1/3/5/7贡献。FFmpeg固定idctdsp.c把lowres2路由ff_j_rev_dct2，jrevdct.c读取仅DC/AC10/AC01/AC11，(+4)>>3并clip，因此AC3全丢弃。AC3夹具证明区别在核而非仅最终整数舍入，未把这些GPL/IJG实现移入产品。

事先冻结合同后保留Y AC3=96：面积oracle与WIC在1码内，FFmpeg恒128；新CbAC3=64再次证明默认色度格和Preserve格宽度差。恒DC说明范围/零点不是主因。

### 1.3 YCbCr→RGB和位置/滤波
WIC导出的native平面用独立浮点全范围BT.601：R=Y+1.402(Cr-128)、G=Y-.344136(Cb-128)-.714136(Cr-128)、B=Y+1.772(Cb-128)，round/clip后，原24/33MP444输出max0，原420max1（mean2.22e-7）。所以不是通道翻转、709矩阵或limited范围错误。

FFmpeg原lowres2平面→默认RGB最接近nearest+full601：max2/mean.554，linear-center mean.762；错误limited601 mean10.10，709另差，不是被误用成主路径。仅设置neighbor/bilinear/bicubic而无full_chroma_int时，输出与默认完全相同，之前改变滤波标志不足以证明滤波参与；full_chroma_int+accurate_rnd后nearest与默认max2，中心bilinear与默认max19。

反事实保持WIC亮度平面与同BT.601不变，仅把高网格WIC色度替换为FF lowres2色度再nearest：max36/mean1.296/P95=4；中心bilinearmax39/mean1.356。说明仅改变色度阶段已产生大部分原40码差异。nearest与center/left模型都量测，不声称推断出了专有WIC全尺度滤波实现。原WIC Y vsFF lowres2 max15/mean.6047/P95=2是另一个IDCT核差异，不能混写成ICC。

## 2. ICC：同native→颜色→方向
### 2.1 同一字节与域
源ICC SHA256=aca1f6f3a5856485b39f917fdb151cb93da3c117e45f497af426f0ce46eb9596（588B、ICC4.4 matrix/parametricTRC）；WIC实际目标=2b3aa1645779a9e634744faf9b01e9102b0c9b88fd6deced7934df86b949af7e（3144B、ICC2.1 matrix/1024项TRC）。名称均sRGB不代表字节相同。源D50 PCS/chad与目标wtpt/TRC/primaries不同，不能以同名字跳过转换。没有更换目标profile或源profile来让结果通过。

三个原JPEGnative与Pillow同采样精确；取该已解码native作为颜色输入，没有把JPEG/ICC误差混合。ColorConvertedBitmap通过IWICColorTransform.Initialize按SourceColorContext/DestinationColorContext转Bgra32；该接口不暴露intent/BPC/adaptation/precision可调参数。

### 2.2 独立系统调用与具体参数审计
在同一8位BGRA输入，OpenColorProfileW同源/目标，CreateMultiProfileTransform BEST_MODE3，TranslateBitmapBits BM_xRGBQUADS0x08（实际BGRA字节序），输出与WIC颜色阶段原照片三份全max0。heldout17×23与23×17的两个新色块同max0。该结果只证明WIC现有系统CMM调用一致，不是独立色彩科学引擎真值。

系统intents0/1/2在这组矩阵profile给相同结果，absolute3明显变化；BEST_MODE3与NORMAL2/PROOF1不同，原灰阶出现0/0/0/0/3/3/3/6…阶梯，24位RGB与32位BGRA布局也会选不同量化路径。16位系统输出低码值更平滑，但不是产品路径，不用于悄改v3。USE_RELATIVE_COLORIMETRIC标志该组无影响。没有把WPF ColorTransformHelper的非位图调用选项冒充WIC内部实现。

LCMS Pillow2.19 intents0–3、BPC8192开关、NOOPTIMIZE256/高低预计算参数组合不能消除原max3。另用既有发布LCMS2.12诊断8/16/float，NOOPTIMIZE|NOCACHE=0x140、BPC开/关、adaptation0/1：该对profile按同量化都max3；float round→8与8位路径max0，float与16位差最大7.6e-6码。不是把LCMS先输出8位造成3码损失。relative意图下adaptation变化无效应属该域行为，不能据此断言任意profile均无关。Windows隐式BPC/adaptation无getter，不假称已读出其值。

独立仅限上述两SHA矩阵/TRC oracle：解析源para type3、D50 RGB→XYZ矩阵、目标1024曲线反查，clip/round，与LCMS的8×256灰阶/色阶夹具max0，与WICmax3。保留新两夹具再测同oracle vsLCMSmax0、vsWICmax3。这锁定颜色/CMM数值阶段，不把该受限oracle升级通用ICC parser或产品引擎。

### 2.3 方向及真实产品通路
ICC后方向6用NumPy clockwise90逐字节比较，原33MP和heldout旋转图max0。探针再次把分阶段最终字节与真实CanvasPhotoPreviewSession.Read输出相比较，原三个大JPEG精确一致，不只验证旁路。

### 2.4 授权原始ICC资料缺口
仅在既有授权RAW/Real、closure-final、closure-baseline查支持栅格扩展，共12文件，没有同具既有ICC+方向者。原TIFF无ICC、生成JPEG的profile/方向仍明记generated，不伪造原始性。该真实资料验证继续NOT_RUN；不扫描其他用户目录。

## 3. 比较合同及收尾判断
COMPARISON_CONTRACT_V1.json在保留夹具执行前冻结，合同SHA与HOLDOUT_RESULTS绑定，6条阶段关系通过；冻结文件时间先于holdoutTRX，原始执行顺序保留，不夸称密码学预注册。
- 可以做逐位等价：同WIC native与同Pillow IDCT路径；同profile/位深/字节格式/系统模式的WIC与MSCMS；方向纯排列；产品旧PNG/导出身份。
- 只做量化：FFmpeg不同核/色度网格 vsWIC，WindowsCMM vsLCMS精确matrix/TRC。**原失败数值未更改，旧原验收门槛未修改。**
- 诊断闭环不意味着POLICY独立数值门禁全过，能力verification_status保留FAIL。无需为消除合法不同数值算子差异重写产品；未来若要求一致数学，必须新渲染版本＋新科学准入，而非暗改v3或用整图解码凑结果。

## 4. 必要回归与身份
本轮发布DLL绑定：34相关回归通过、2独立重开通过、1分阶段提取方法通过（多个夹具运行不重复累加）、能力一致性1通过，合计38唯一方法，多个提取夹具调用不重复加总。覆盖旧v1/v2字节、现v3重开/更新、预算/缓存/取消/项目切换迟到/原子失败、全部模块策划15/教程退役。原符号链接权限FAIL、缺通用LCMS参考NOT_RUN不在本轮重跑，仍历史保留；新两profileoracle不能顶替缺失的Bradford通用输入。

产品src未变，产品源码仍146c0fd254de387ad5ccb9570cff6c54697389d4。不再次publish，不重打ZIP；原完整294文件/77,254,817B包SHA256 d394e3ab463531f66eb4ae74ef571becb26bec20754965ef07251a35c560cf5d复核通过。主DLL e24a07454a72f3ca286bae6cef22040dae97159189849f32ba89c93a2eda3319，AssetLibrary 0ab50ac55bd80b15ea1a062f56f5b41406cb6db37943a028214f2edbd8c16138。测试支持、最终HEAD/远端见IDENTITY/REMOTE_READBACK，与产品/发布身份不混用。

启动沿用 N:/pixart/Guangjing-R55-CanvasPhoto-20261101/Guangjing-R55-CanvasPhoto-Diagnostic-win-x64/Start-Guangjing-Diagnostic.cmd。仍完整win-x64自包含诊断包；不是单模块/Setup，物理鼠标/DPI/ACM/HDR未验，不启用Browser/WebView/ComputerUse/CDP。

## 5. 复现与资料边界
本机输出 N:/pixart/Guangjing-R55-V3-Numerical-20261101；私照各平面/像素仅这里，Git只收metrics/hash、原始日志、可分发确定性夹具与生成ICC；系统目标ICC仅本机提取、Git记录SHA而不新增分发Windows profile。测试探针KITAO_V3_NUMERIC_INPUTS为显式路径数组，KITAO_V3_NUMERIC_OUTPUT为新隔离目录。各analyze_*.py只做诊断，没有产品代码执行入口。FFmpeg/Pillow/LCMS均外部或已有组件，源码研究不复制GPL进产品。参考完整commit和hash见REFERENCE_PINS，系统DLL版本/hash见metrics/SYSTEM_BINARIES.json。

脚本修正/初次错误见PROBE_FAILURES.md与原raw日志。尤其手写MSCMS首轮诊断把BGRA格式错写0x10已纠正为0x08；这是探针错误，产品WIC调用没有这项PInvoke。不将其误报为产品修复。目录/stride推广错误亦仅测试子进程，并已保留失败后重跑，无删除失败或调整阈值。

原始数值验收重放：verify_photo_numerical.py同旧输入、ICC和阈值，仍exit1；冻结holdout关系verify_holdout.py exit0，二者用途严格不同。不存在“新合同通过所以旧FAIL消失”的结论。

保留ICC夹具落实：首轮hold-icc-dark-grid模块颜色网格未包括合同所述灰阶，保留该次结果为探索，不用于偷换合同；另建holdout-contract-fixtures，第一行明确0–16灰阶，其余模块色格，新目录/同名稳定夹具重跑。合同未改，MSCMS与WIC0、oracle与LCMS0、WIC与LCMS3。raw两次均留存。

最终测试支持提交 fa171e47f5d3b3921676fb11afa0bb5a996338a6（核心stage probe 02486324e95a55c90a230a1922864bf6a44e98ec），最终测试DLL SHA256 bd0747c81d3a5a0cb82478018b07564ec68a4bb9ddc4e758c492b0b77e6ef76b；388编译警告/0错误。已核实产品20共享DLL与旧发布SHA完全相同，包未重建。
