# 节点开源对照：MaxChannelShoulderV1

准入：**VERIFIED_RESEARCH / PRODUCT_NOT_ADMITTED**。本轮仅允许研究目录的显式候选；不改 PV4/5/6、recipe迁移、UI、默认RAW或缓存键。

- 目标：定位已显影sRGB曝光的逐通道裁切，验证独立最大通道有理肩部是否能保留亮部阶梯与线性RGB比例。不是高光重建。
- 处理链：已显影sRGB16 → ushort/65535f → EOTF一次 → EV → 候选肩部 → OETF一次 → float32预览／TIFF16一次量化。原WB/相机矩阵和显示映射已经发生，绝不重复WB、黑位扣除或当sensor输入。
- 输入：sRGB D65；32f encoded [0,1]；方向先以EXIF8确定无重采样ROI，ROI朝向1；RGB无alpha。ApplyLinear只接收显式非负、有限、线性sRGB D65相对值，支持>1；负值/未知域拒绝。两接口不能互换。
- 输出：sRGB D65 float，候选有限测域范围内最大值<1。膝点.75，最大通道m≤.75时为线性曝光；否则最大值 .75+.25*(m-.75)/(m-.75+.25)，RGB共同比例缩放。TIFF16用产品float×65535、nearest-even。此式由本轮独立设计，不复制GPL公式。

参考1：darktable 4.8.1，完整commit fb0eb997e62c6dd52966cd35911c74afa150e209。src/iop/exposure.c::process（约526行），black=0、scale=2^EV，bias/deflicker关闭。真实CPU函数原文放本机外部 harness，types/setup仅适配已固定参数，OpenMP宏关闭；-O2 -ffp-contract=off。测量曝光阶段不含filmic/色彩空间变换/ICC/导出。

参考2：RawTherapee 5.11，完整commit db575c6690d30ce6428c0e1383bec8b8f1fb8d21。rtengine/improcfun.cc::highlightToneCurve（109行），rtengine/curves.cc的CurveFactory LUT创建区（504行起），curves.h::hlcurve（339行）；实际原函数按标量分支编译。内部单位65535，hlcompr=0与60、hlcomprthresh=50、black/brightness/contrast=0、autoExposure/RAW重建关闭。LUT浮点索引逻辑取原LUT.h，整型读写/数组分配适配；不替换参考公式。原SSE分支未测。

数值：同一已观测EOTF float32输入，两个参考曝光与产品exposed-linear逐值相同，maxAbs=0。独立NumPy EOTF桥接下阶梯maxAbs=5.7220458984375e-6、真实裁片2.384185791015625e-7，已保留；不得据桥接误差认定曝光算法不同。RT高光压缩60另输出，用于行为差异，不是候选逐像素golden。实际配置、完整源码/提取函数/编译器/二进制SHA在REFERENCES.json。

固定输入：授权DSC08015既有PV4显影RGB16，source hash 5c06a14160c9b57015cfebd9cd1e882620ffc817cfdca061596746cdd82d1ca7；ROI(2400,3250,384,384)，完整来源/ICC/pixel hash见INPUT_BINDING.json。两次读取与RGB16 TIFF回读完全相同；照片不进Git。3条193步合成真值：中性、肤色近似、暖色；覆盖Y=.18*2^[-6,+6]，步长1/16，单通道最高约26.765。负值/>1原参考压力测试，非法域/NaN/尺寸拒绝已执行。单一sRGB ICC核对，无通用ICC/跨profile批准。

行为：候选三条阶梯float32及RGB16无整RGB平顶步；真实裁片+5EV新增红通道裁切45→0；对手线性色相角最大误差2.8625124°→.00328281°。此角度不是ΔE，也不是所有观看条件下感知色相严格不变。真实裁片较暗，本轮+5EV是压力测；没有把它包装为标定逆光高亮人像。

历史及冻结一致性：只为3个真实产品函数添加可选只读observer，null路径同原公式；相关回归58 PASS/1既有原生LCMS参考SKIP。新增PV4/5/6预览/JSON重开/产品TIFF16行写出一致性测试通过。候选真实裁片的新进程读取保存recipe后float逐值相同；产品TiffExport写出与独立NumPy量化/回读逐值一致，嵌入sRGB ICC字节相同。未测试产品UI会话接纳候选。

资源：最大512×512、10 ms父守卫512 MiB，读取/持有者边界见MEMORY_BOUNDARIES.md。每次进程耗时及采样内存见BASELINE.json、RESULTS.json。NaN/尺寸/未知域拒绝；候选点运算无异步取消入口，未宣称取消/整图吞吐已验收。

许可证：两者文件头均GPL-3.0-or-later，实际LICENSE已下载/hash；参考源码、提取体及二进制仅本机隔离研究，未提交/链接入产品。脚本下载后在外部生成工具。不批准GPL代码移植或产品分发；未来若分发须另审核完整对应源码/版权/许可证与安装信息等义务。本轮独立C#候选不含GPL实现文本。数学对照不自动成为产品准入。

CalibrationEligible=false。未观察sensor/去马赛克前/相机矩阵前中间值；已显影亮部损失不可逆。相机标定、真实曝光配对、一般ICC、完整darktable/RT应用对照、人审审美、公司新RAW批次均未执行/未批准。
