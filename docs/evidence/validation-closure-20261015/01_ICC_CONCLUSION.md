# 阶段 1：ICC 分歧归因（研究，不接产品）

基线 af78bb56b1eeec4e7c1a0b2de6f320dbbe33d458；原 COMPARISON.json、逐点文件、失败和七个 profile 原地保留。01_FIXED_IDENTITIES.json 对七 profile/三个程序及参考资料逐个重新 SHA256，全部匹配。56 个 intent/type 格的分类见 01_CELLS.json，不把能输出数值自动视为配置等价。

## 缩小样本与确定的局部原因
新 trace_icc.py 对三个原 LUT 和额外 identity-M 变体，逐个 intent 原输入点执行；LCMS 用原 DLL，skcms 从固定干净源码重新编译，不改其运算，不乘二或放宽容差。新变体只为诊断，不替换七个原 profile。A2B0/1/2 分别用于 p/r/s，absolute 用 A2B1。tag offset/type、每段 LCMS prefix 以及 skcms A+CLUT / M+matrix / B 输出都保存。

相对意图、原无 M v4、白输入：LCMS 最后归一化段 [0.4820019901,0.5000076294,0.4125123918]，最终 XYZ [0.9639892706,0.9999999998,0.8250121947]；skcms 最终停在前者。加 identity M+matrix 后，LCMS 不变，skcms 在 matrix 段变为 [0.9639892578,1,0.8250122070]。

ICC.1:2010 v4.3 §6.3.4.2 表11：XYZ 1 对应 0x8000，0xFFFF 对应 1.999969482421875；§10.10 允许 A/CLUT/B（无 M）和 A/CLUT/M/matrix/B。固定 skcms read_tag_mab 989–1023 只在 M/matrix 存在时将 65535/32768 折入 matrix；无 M 不走此路径。mft2 882–903 也没有该 PCS 还原因子。LCMS PackXYZDoubleFromFloat 3320–3345 在最终 XYZ pack 统一做 MAX_ENCODEABLE_XYZ 还原。这解释本批无 M 和 v2 mft2 的约半值：PCS 编码域到 XYZ 的转换位置/缺失，而非 WB、输入翻倍、CLUT 或 A2B 选择。v2 LCMS/Argyll 既有结果互证。结论限固定 skcms commit 和这些样本，尚未上游确认，不声称所有 profile 或后续版本有同一缺陷。

## 约 0.00349 残差不是同一问题
LCMS cmscnvrt.c 1122–1134 对 v4 perceptual/saturation 无论调用 BPC flag 均强制 BPC。故先前文档的“BPC off”只描述请求 flag，不描述有效状态。新 trace 明确改记 requested off / effective forced on。显式 M profile 黑输入：LCMS p/s = [-0.00337175,-0.00348520,-0.00288002]，r = [0,0,0]；skcms 都为零。不得把这种 intent 连接层差别判为同合同误差。skcms 只支持 A2B 优先级，不支持完整 absolute/adaptation/BPC 语义；absolute 格不比较。Argyll 对 v4 拒绝，格为 REFERENCE_UNSUPPORTED_V4。扩展负值/>1 的 LUT 输入超出规范表域，单列，不拿来批准引擎。

## 白点和未关闭项
matrix profile 的 D65→D50 chad 已封存在 profile；LUT 输出本来就是 PCS D50，不能再无条件套 Bradford。LCMS adaptation r/p/s=1、a=0；Argyll a 是其自身 absolute 行为，不预设和 LCMS 所有 adaptation 细节相同；skcms absolute 无对应开关。普通 TRC gamma 拟合、量化/插值残差仍保留，非本次通用 ICC 通过。一般真实 profile、非线性 B 曲线与 matrix 因子顺序、完整 absolute 白点矩阵逐段观察及上游确认仍 OPEN；不批准通用 reader。

规范注意：旧 followup 的 icc-v4.pdf 实为 ICC.1:2022，本轮另外从 ICC 官方 archive 获取真正 ICC1v43_2010-12.pdf，核对其内部 ICC.1:2010 身份，而非相信文件名。来源/sha 与引用页见 01_SPEC.json。第三方源码不进 Git；入口与短局部证据见 01_SOURCE_EVIDENCE.json。

复现：先按原 REFERENCE_COMPONENTS 固定本地参考，再按 01_BUILD.json 对应编译参数构建 tools/validation-closure/skcms_stages.cpp，运行 trace_icc.py。原程序参数、flags、数据未改。产品 src/tests 未改；PV456/PV7/reader 默认状态不变。此阶段是局部分歧归因，不是像素修改的两实现门禁批准。
