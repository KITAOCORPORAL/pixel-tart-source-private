# RAW-REOPEN-001 F2 比较合同修复

2026-11-09。基线 c156bb83e741e1fd61142431a203d62e62a9e44b；研究复审 fcd4f4f9b259bad933f1616a331c7ed615b60d8e。

Compare-Checkpoints.mjs 现在要求初始化的 CIELab lookup、紧随其后的三行有效矩阵和 allhex 唯一地位于 P5 payload 完成之后、serial-ready state/payload 之前。三行矩阵必须紧邻同次 lookup 记录，且在 allhex 之前；保留既有 schema、全局唯一记录、完整性与运行身份检查。错误阶段不再因两侧同时错位而相等。

新增 parseUniqueJson，在对象成员覆盖之前递归验证唯一键；键先按 JSON 字符串规则解码，嵌套对象及 Unicode 转义等价键同样拒绝。对象成员检查覆盖 native-events.jsonl、result.json、run-identity.json、loaded-native.json。校验后继续执行原有 JSON/schema/身份逻辑，没有改像素、线程或默认参数。

验收复用研究已冻结的原始文件，先验证其精确 hash。原 CIELab 矩阵在 P6 之后、allhex 在 P5 之前、重复 quality JSON 成员三个反例均拒绝；同时验证左右各自单侧拒绝。正常对照通过，四组 F3 均保留 XTRANS-serial-ready 首个观测差异且 H1 UNKNOWN。新增验收35项通过；既有38项回归通过。第一轮验收的测试字符串转义错误目录保留，修正该合成测试后使用新目录，未运行任何 native 或 RAW。

复现：node tools/RawNativeInternalProbe/F2-Comparison.Tests.mjs <原研究independent-tests-final目录> <SYNTHETIC_EVIDENCE_HASHES.json> <新输出目录>。加最后参数 review 时只运行正常对照和三个原反例，用于绑定实际修复提交的窄复核。

原生编译/无RAW自检由现有独立验证工作树推进；本修复不修改其现场。真正运行须绑定通过自检的库、host、合同和F2修复身份；后续新批次使用独立授权/账本，不覆盖旧consumed=2记录。此提交时新增RAW/GPU/native init/native build均0，故不将合成验收写成实际native或H1证据。F1/F3沿用fcd4f4f9通过结论，V1锐化继续封存。
