# RAW-REOPEN-001：固定版本源码与生命周期契约对照

日期：2026-11-04（Asia/Shanghai）。状态：**CODE_CONTRACT_REVIEW_COMPLETE / ROOT_CAUSE_OPEN / NO_PRODUCT_MATH_CHANGE**。

## 1. 本轮边界与结论

研究分支从已封存 `6f1ae27aff72a7439c661e605a38c1702feea4ce` 独立接续；产品输入为 `docs/raw-default-chain-closure-20261104@e2437d2300b37872d5b66482a721e3b6463f1ce4`，产品源码 `38dd3f92bb2db7238f7f058008887929ec971c93`。产品分支仅用 `git show` 只读查看，未合入研究历史。

**可用于修复的当前结论：**

1. 已确认发布原生 DLL / 托管绑定身份，且与保留的 pass4 testhost 和 NuGet 缓存字节匹配；不能因此声称已还原 native 编译器、flags、补丁或当时实际加载模块路径。
2. 对现有成功路径，**未发现生产调用违反明确的上下文生命周期、调用次序或输出 buffer 所有权契约**：每次在同一个同步 Task.Run 工作体创建/使用/释放独立上下文；先 unpack，再提交输出参数并 process，再生成 memory image；复制成托管数组后才释放原生输出。没有证据支持用“已释放数组”“同一上下文跨线程”“遗漏初始化整个对象”直接解释本失败。
3. 默认生产调用确定是旧 `LibRawDecoder.DecodeCore`，不是 PV4、ReviewedLibRawCandidate 或研究引擎。但**现有产品收据没有保存实际 unpacker 名、CFA filters/colors、内部 fuji_width 或实际插值分支**，故本轮只能锁定分支选择规则，不能按型号或 UserQual=-1 把某个 demosaic 算法写成已观测事实。
4. RawTherapee 5.11 是成熟的相关源码/契约对照，但其默认 raw loading 复用 LibRaw，之后用自己的 float X-Trans demosaic；既不是完全独立的 unpack oracle，也不是与生产 LibRaw RGB16 显影链同域、可直接逐值比较的第二个数值 oracle。本轮未执行任一参考程序。
5. 最小建议是**补一次产品既定复现中的被动身份/阶段观测**，不是替换 decoder、修改 UserQual、关线程、改 gamma/profile/曲线、放宽重开断言或加持久像素缓存掩盖差异。具体计划见 PRODUCT_DIAGNOSTIC_PLAN.md；本研究线不实现或执行该计划。

## 2. 实际生产依赖，而非包名猜测

静态读取发布目录、已有 pass4 testhost 和明确 NuGet 路径；不加载 native、不调用 libraw_init/open/unpack/process、不读取 RAW。

|对象|本地实际字节 / 版本|证据强度|
|---|---|---|
|发布 `raw_r.dll`|1,102,848 bytes；SHA256 `f500c0732feb21b188d5b52cea05fd824d5b3c8016eb2ca68d8312acc9f914b9`|与缓存 `Sdcb.LibRaw.runtime.win64/0.21.1` 及原 pass4 testhost 的 runtimes/win-x64/native 资产相同；产品原收据也相同|
|native 版本|`0.21.1-Release`|复用产品已保留 native-initial-parameters.json，不冒充本轮新的 native API 调用|
|发布 `Sdcb.LibRaw.dll`|75,776 bytes；SHA256 `6df698647c79df05e754072ed393a02315d780af7287c455d765a767df93d4a3`|匹配 NuGet **net6.0** 资产及 pass4 testhost；不是字节不同的 netstandard2.0 资产|
|托管版本 / 源身份|Assembly/File 0.21.1.7；informational `0.21.1.7+43e9c1771c2b1387c2170e084e433d8bcef51cab`|文件元数据与 nuspec repository commit 相合；固定该源码，不用当前 master|
|native 直接依赖|PE imports 包含 lcms2.dll、zlib1.dll、jpeg8.dll、VCOMP140.DLL、MSVC/CRT、Windows DLL|PE import 表是实际字节证据。VCOMP 只证明有 OpenMP runtime 依赖，不证明原运行线程数、分支、竞态或必须全局串行化|

详细文件 SHA、包 nuspec SHA、依赖和发现范围见 PRODUCTION_DEPENDENCIES.json。Sdcb Windows resolver 先试 raw_r.dll，再 fallback libraw.dll（S07）；原报告没有记录最终 module handle 的绝对路径，故将它列为下次产品观测项，不把文件存在当成加载路径证明。

native runtime nuspec **没有 repository commit / 编译工具链 / vcpkg port patch / flags**。与版本对应的上游源码已固定为 LibRaw tag 0.21.1 的 peeled commit；这是相关源版本，不是该 DLL 的 bit-reproducible 构建证明。托管 commit 则由包元数据和 assembly informational version 直接绑定。

## 3. 三个固定源码身份与可检查函数

均为原项目官方 Git；源码仅留本地 E:/Kitao-Research/raw-reopen-001-contract-20261104，不复制到产品或 Git。不是最新版本推荐。

|参考|完整 commit|具体依据|
|---|---|---|
|LibRaw 0.21.1|`cccb97647fcee56801fa68231fa8a38aa8b52ef7`|L01-L14：libraw_init、LibRaw::LibRaw/recycle、dcraw_process、xtrans_interpolate、malloc_omp_buffers、dcraw_make_mem_image/clear_mem、线程/内存 API 文档、Fuji 元数据分支|
|Sdcb.LibRaw 0.21.1.7|`43e9c1771c2b1387c2170e084e433d8bcef51cab`|S01-S08：RawContext.OpenFile/Unpack/DcrawProcess/MakeDcrawMemoryImage/Dispose、OutputParams.FromLibRaw/Commit、ProcessedImage.AsSpan/Dispose、Windows resolver、DecoderInfo|
|RawTherapee 5.11|`db575c6690d30ce6428c0e1383bec8b8f1fb8d21`|R01-R16：RawImage::loadRaw、raw2image/生命周期、RawImageSource X-Trans dispatch、float xtrans_interpolate、XTransSensor 默认参数、OpenMP/LibRaw 配置|

SOURCE_LOCK.json 固定 39 个源码/许可文件的 Git blob SHA256 与 41 个精确行范围和官方 commit URL。产品源码/收据单独绑定于 PRODUCT_EVIDENCE_BINDINGS.json，避免把产品 SHA、研究 SHA、native artifact SHA 和参考 commit 混写。

## 4. 默认入口到底做了什么、还缺什么

产品调用链：RawMatchTiff16ProductPipeline.DecodeMasterAsync → IRawDecoder.DecodeAsync → Task.Run 中同步 DecodeCore → RawContext.OpenFile → Unpack → DcrawProcess(configure) → MakeDcrawMemoryImage → copy RGB48 → HighBitDepthImageBuffer.FromRaw → NormalizePixels。

**已由源码和原生初始化收据支持的有效设置：**full-size、camera WB=true / auto WB=false、camera matrix=true、OutputColor=sRGB、OutputBps=16、no_interpolation=0、UserFlip=0（产品稍后统一方向）。其余从新 context 的原生默认继承；已有单次初始收据包括 Gamma=[0.45,4.5]、UserQual=-1、NoAutoBright=false、Brightness=1、Highlight=0。它不是两次 producer/reopen 的完整 effective 参数对照，不能替代在每次 process commit 后记录。

**unpack 与插值不是一个概念：**Open/identify 选择压缩 RAW unpacker；Unpack 解包传感器数据；dcraw_process 随后执行预处理/缩放、插值与颜色处理。不能把 `fuji_compressed_load_raw`（如未来确实观测到）当成 X-Trans demosaic 算法名。下一步可通过绑定已有 RawContext.DecoderInfo 记录选中的 unpacker，当前产品材料没有该字段。

LibRaw 0.21.1（L04/L05）的静态选择是：

- 默认 quality 来自内部 Fuji 几何状态；非负 user_qual 才覆盖它。UserQual=-1 是“继承此选择”，不是 AHD、VNG 或“三遍 X-Trans”的枚举承诺。
- 是否插值先由 filters 与 no_interpolation 判断；Bayer/X-Trans 自定义回调有优先级；quality=0、quality=1/多颜色、quality=2+Bayer 等分支先于原生 X-Trans 分支。
- 到达 filters=LIBRAW_XTRANS 的分支后，passes 取决于最终 quality（大于 2 取 3，否则 1）。因此只有同时知道 filters/colors、fuji_width、完整参数、回调状态及 pre_interpolate 后状态，才能把具体算法/遍数从规则变成实际事实。
- 新 LibRaw 构造函数清零数据/内部状态/回调并设置明确默认（L02），但原生 **scratch** 不是“整个堆都清零”。对象初始化与 kernel 每个读取位置是否已被写入是不同命题。

当前 **selectedUnpacker / selectedInterpolation = NOT_CAPTURED**。这是本轮源码对照识别出的具体观测缺口，不通过推测补齐；计划交产品线统一采集，不另开 RAW 解码。

## 5. 如何读 RawTherapee 对照

详表见 CONTRACT_COMPARISON.md。关键点：

- 默认 enableLibRaw=true，RawImage::loadRaw 新建 LibRaw，identify / unpack / raw2image 后，将数据转入 RT 自有 float 存储；正常路径不是调用 LibRaw dcraw_process 生成生产同样的 RGB16 bitmap。仍有条件性的 dcraw fallback，不能把“有 LibRaw 成员”当成所有文件实际都走它。
- RT 默认构建选择其 vendored LibRaw；该 5.11 树的头文件标记为 0.21.0-Beta1，外加本树内容/改动，不是生产0.21.1。WITH_SYSTEM_LIBRAW 可换依赖；本轮固定源码默认配置，未构建任何 RT 变体，也不推断用户已安装 RT 的配置。
- RT 的 THREE_PASS 默认、float 原始域预处理/矩阵/黑白位、demosaic 后处理及输出链与生产并不相同。LibRaw 和 RT 的 X-Trans 实现均有 Markesteijn/dcraw 算法血缘；RT 有自己的 float 实现、边界/初始化/选择细节，不等于完全独立真值。
- 两者都有并行/worker scratch 的设计。RT 的某些 LibRaw unpack/raw2image 调用在 LIBRAW_USE_OPENMP 编译条件下有 mutex；这不是“所有 LibRaw 调用都必须全局锁”的 API 契约，更不是本失败已证实竞态的证据。
- RT 每 tile 对首 RGB plane 做显式清零，再填传感器颜色；LibRaw 路径从已有 image 拷贝 RGB 数据，并使用 malloc scratch。**不同写入策略本身不证明任一实现未初始化读取，也不构成给原生库添加 calloc 的修复依据。**

## 6. 已证实、实现特性、猜测分账

|项|结论 / 边界|
|---|---|
|跨进程 master 失败|复用产品证据：company-02 同源/栈/版本，master 对照16,344通道差、max5600 U16；+0.5 TIFF20,521通道差、max6844；Canon保留对照0差。本轮未再读TIFF或计算差量|
|“保存丢参数”已排除到什么程度|原件/stack/version等已相同；native effective 参数、native 内部状态尚未成对记录，不能把全部隐含初始化因素排除|
|生命周期/输出buffer违约|源码没有发现本成功路径的明确违约。AsSpan为借用视图，但产品在 processed/context.Dispose 前 ToArray；用 using 处理异常/取消后的释放；没有 native回调被该入口注册|
|原生取消|产品仅在原生调用之间检查 token，不会在同一原生函数执行期间回收 context；不承诺 native 中途取消。它是粒度边界，不是重开失败根因|
|实际X-Trans分支|静态选择已锁定，实际分支/遍数未捕获；不以相机型号直接认定|
|线程问题|原生有VCOMP导入、参考存在并行代码；原运行线程数/时序/竞态未知。不能据此建议关线程或直接照搬RT锁|
|内存初始化问题|构造函数初始化有明确源码；scratch写前读/越界没有运行证据；现有证据既不证明也不排除native内部缺陷|
|默认gamma/profile/黑白位|属于原显影域和同域比较的重要条件，不因发现它们而推导其导致随机重开差异；本轮不改|
|颜色品质/算法已知取舍|本轮不是锐化、色准或X-Trans画质评价；不得把一般demosaic伪色当作同输入重建不稳定的正常豁免|

## 7. 最小方案、准入与停止点

最小方案是“在产品既定 producer/reopen pair 上补被动诊断收据”，保留原 DLL/算法/线程策略和精确失败断言；分开记录实际加载模块/完整有效参数/上下文线程ID、unpacker和CFA，以及 native输出复制前、managed复制后、float转入/方向归一后的签名。先区分 native输出已差与托管后才差，再决定是否需要额外原生阶段观测。详见计划的分流表。

若只有外层阶段数据，不宣称已定位到插值；现有 public C ABI/绑定没有暴露所有内部 pre/post demosaic 缓冲或 fuji_width，不能随便猜内部结构 offset，也不能用“progress到了某步”冒充捕获真实分支。仅当现有 DLL 的观测仍不足时，由产品线另行安排固定源码的隔离 instrumentation；新构建不冒充原 DLL，GPL/双许可材料仍隔离。

**本轮没有基于明确违约可直接落地的产品修复补丁。** 不改产品 src、参数、默认数学、buffer策略、线程、缓存或断言。两个成熟项目的代码/契约相关性已建立，但独立同域数值对照 **NOT_RUN**，不能将此轮写成 POLICY 像素变更已准入。CalibrationEligible=false，不要求色卡。

本轮只交付研究文档/源码锁/依赖与旧证据绑定；新RAW解码、GPU、build、RT/LibRaw参考运行、像素计算全部0。V1锐化封存不重评、不改状态；SharedIncrementEnvelopeUnsharpV1继续默认关闭、NOT_ADMITTED，不创建V2或胶片候选。下一步仅由产品线执行最小复现计划并交回成对观测，研究线不重复该RAW。
