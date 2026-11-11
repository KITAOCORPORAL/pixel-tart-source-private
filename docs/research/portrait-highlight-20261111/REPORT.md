# 人像亮部第一轮定位与隔离候选

日期：2026-11-11，Europe/Berlin。分支 research/portrait-highlight-continuity-20261109；起点39e60d3feff52f156a72d16f7820f0b968bd9b5b。状态：FIRST_LOCALIZATION_COMPLETE / ISOLATED_CANDIDATE_VERIFIED / PRODUCT_NOT_ADMITTED。

## 接续与内存

首先执行git fetch --all --prune，检查本机未推送提交、工作区与全部引用。工作区干净，没有已完成人像亮部工具/报告；本分支仅原未推送39e60d3f来源字节身份文档，按第一轮继续。

读取6e57eb26报告并核对a42cb7cd、b86d0245实际diff：隐藏页禁止请求全尺寸、可见照片页自身拥有100%请求、完整RGB24 backing省去ToArray而切片仍复制、关闭释放持有者。细节见MEMORY_BOUNDARIES.md。本轮研究器不创建WPF、不请求视口、不解码RAW。

## 固定输入

复用已授权DSC08015既有PV4显影sRGB16 TIFF，SHA-256 5c06a14160c9b57015cfebd9cd1e882620ffc817cfdca061596746cdd82d1ca7。存储7028×4688、EXIF8后4688×7028；固定转正坐标(2400,3250,384,384)，无插值。裁片pixel hash d78d0e39ab53723e164906686dba862f3ee34dbcad8e9bef9e630b14033d620d。两次读取和TIFF16回读逐值一致，来源/ICC见INPUT_BINDING。

原照片是暗环境人像；肩颈/脸缘ROI以+5EV进入亮部作压力测，不是标定逆光人像或曝光对。“可重复”仅指冻结RGB16重复读取，不宣称本轮重验RAW解码。

已显影RGB16已过WB/矩阵/肩部/色域/OETF；逆sRGB只取得显示线性相对值，不能恢复sensor headroom。合成线性sRGB D65真值与该照片分开；RAW显示分支将合成真值转LibRaw ProPhoto D65再调用真实函数，不从RGB16反推RAW。sensor、去马赛克前、矩阵前阶段均未观测。

三条阶梯：中性、肤色近似(.76,.42,.26方向)、暖色(1,.3,.045方向)，归一Y=.18；-6至+6档、步长1/16，各193样本，共579。最高线性Y=11.52、单通道约26.765。已显影路径输入先将真值/32编码到[0,1]，实际产品+5EV还原目标，不预裁超白真值。

## 首个损失位置

真实RenderedSrgbExposure、ProfessionalRawPipeline.Display、BasicTone新增可选只读observer；同时保存原double与float32物化值。RESULTS给出通道范围、线性Y及对手色相/色度。中性/近中性色相undefined，不记作0°误差。

- 已显影曝光：EOTF→EV→OETF均保留超白；首个不可逆损失是encoded-preclip之后逐通道Math.Clamp。中性+2.5档整RGB平台；肤色+1.875档首次通道裁切且色角误差>1°，+3.375档整RGB平台；暖色+1.3125档首次裁切、+1.375档误差>1°、+5.75档整RGB平台。
- BasicTone：亮度有理肩部后，EncodeGamut以max(1,maxChannel)归一化，使彩色极亮的主通道固定1。double无整RGB完全相同步；实际float32输出肤色/暖色12/26个平台步，中性0。主通道归一化平台和有限精度整RGB平台不是逐通道clip的同一机制。
- RAW显示合成域：矩阵无平台；亮度肩部保持比例；中性轴压缩使色度趋零。仍有定义时肤色/暖色线性色角误差<5e-9°，不能把趋白记成色相突变。指数肩部mapped在double最终舍入1后，中性轴阶段+5.8125档整RGB相同；更早约+4.75至+4.8125档已在实际float32输出有平台。双精度与物化平台分别记录，不能倒置归因。

裁片+5EV红最大线性1.0935968、编码前裁1.0400728；最终45个红通道置1，G/B无裁切。原源在显影前是否饱和未知。

## 固定参考与隔离候选

实际执行两个原CPU函数，范围是阶段对照，不是完整应用：

- darktable 4.8.1，commit fb0eb997e62c6dd52966cd35911c74afa150e209；exposure.c::process，black=0、scale=2^EV、bias/deflicker关闭。
- RawTherapee 5.11，commit db575c6690d30ce6428c0e1383bec8b8f1fb8d21；highlightToneCurve、原CurveFactory LUT区及hlcurve，内部单位65535，hlcompr=0/60、阈值50，标量分支、autoExposure/重建关闭。

同一产品EOTF float32缓冲输入，两参考曝光与产品exposed-linear在阶梯/裁片上逐值一致，maxAbs=0，并保留>1。独立NumPy EOTF桥接早期误差：阶梯5.722e-6、裁片2.384e-7，原值保留；不能据此归因曝光算法差。RT高光60是独立行为对照，不是候选golden。GPL-3.0-or-later原源码、提取体及EXE只留本机；版本/配置/许可证/hash见REFERENCES和NODE_COMPARISON。

MaxChannelShoulderV1独立C#候选：线性EV后取最大通道m，knee=.75；m≤knee直通，否则映射到knee+(1-knee)*(m-knee)/(m-knee+1-knee)，RGB共同缩放再OETF。只编译进研究CLI与测试，没有产品Core/UI/recipe注册。只在本轮有限范围证明持续增量；不宣称任意巨大输入永不因浮点舍入平顶。

| 指标 | 原已显影曝光 | 候选V1 |
|---|---:|---:|
| 中性整RGB float平台步 | 56 | 0 |
| 肤色整RGB float平台步 | 42 | 0 |
| 暖色整RGB float平台步 | 4 | 0 |
| 三阶梯RGB16整RGB平台步 | 原值见曲线 | 均0 |
| 裁片+5EV红通道裁成1 | 45 | 0 |
| 裁片线性对手色角最大误差 | 2.8625124° | .00328281° |
| 肤色/暖色阶梯候选最大色角误差 | — | 2.148e-5° / 8.529e-6° |

保持线性RGB比例不等于所有观看条件下感知色相不变，也不证明肤色审美批准。EV0膝点以上仍改亮部，必须独立显式版本；候选未默认接入，不恢复原RGB16丢失细节。

真实裁片的新进程从保存JSON再跑float逐值一致；实际产品TiffExport.WriteRgb48与独立NumPy量化/外部TIFF读取逐值一致，ICC字节相同，encoded量化误差<1/65535。NaN/错误域/超尺寸拒绝；两个参考的负值/>1压力样本也已执行。

## 验证与交付范围

相关Core回归58 PASS、0 FAIL、1 SKIP，总59；跳过既有NativeLcmsBradfordMatchesIndependentPinnedReferenceCases，未配置opt-in外部原生参考。新增6项验证历史公式8000个随机向量、observer不改输出、候选斜率/比例、非法域、配方一致性及PV4/5/6预览/JSON重开/产品RGB16行写出。不是全solution/UI回归；内存WPF历史测试沿6e57eb26证据，未重跑真实整应用。

CLI单边≤512、面积≤512×512，本轮384×384。父进程10ms采样，子进程PrivateBytes超过512MiB杀停；每次耗时/采样值见BASELINE和RESULTS。采样峰值是下界，不是精密生命周期峰值或整个产品占用。null观察器无trace数组，研究trace仅ROI。

交付代码、配方、报告、579行CSV及PNG/PDF曲线。照片、对比渲染、f32/f64、GPL源码/EXE只留本机PortraitHighlights-20261111，LOCAL_ARTIFACTS只记hash/path。最终增量构建0警告0错误；初次Core4个既有警告及测试分析器提示保留在日志，未改无关代码。

公司新RAW批次仍PENDING且AuthorizedForHeavyDecode=false。本轮新增RAW解码0，未改草案、STOP或ledger。sensor上游亮部、标定曝光对、跨ICC、完整两应用对照、人审审美及产品接入均未执行/未批准。现有输入的第一轮定位及隔离候选数值验证已完成。

独立新目录复跑已完成：固定输入、579行CSV、生产输出、候选float与产品TIFF16的SHA均相同（REPLAY.json）。篡改参考源码负例在编译前因固定SHA不符拒绝。此处独立指新输出目录/进程，不冒称独立人员评价。
