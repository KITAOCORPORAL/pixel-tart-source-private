# 稳定输入候选V1能力（2026-11-09，Asia/Shanghai）

|事项|状态|边界|
|---|---|---|
|H1旧窗口|CLOSED_REUSED_70130678|旧局部机制支持保留，未再调试/读取窗口|
|唯一修复候选|IMPLEMENTED_DIAGNOSTIC_DEFAULT_OFF|稳定image、独立RGB3、join后commit、原border8；无产品入口|
|共享读/写区审查|COMPLETE_SOURCE_SCOPED|3个共享取址全转constinput，唯一finalstore独立；原公式/控制流逆变换0差|
|末端几何|PASS_INTEGER_3589_LENGTHS|9组二维尺寸，内矩形连续/无重叠；不是RAW实验|
|候选合成编译|PASS_LLVM_ONLY|实际原算法函数+存储surrogate编译；原LibRawheader语法通过，非MSVCnativeDLL|
|无RAW算法自检|PASS_43_CHECKS|40candidate调用/56单tileoracle/10原串行；passes1/3、请求1/2/6重复、毒值、边缘/初始化/释放|
|准备守卫|PASS_8|源漂移/重复patch/默认未启用/既有输出及保护根嵌套拒绝、诊断源码兼容|
|原诊断接线|PREPARED_SOURCE_ONLY|7原源绑定/6源不变/header不变，217file新源准备；未伪造原样恢复|
|新增内存|ANALYTICAL_6_BYTES_PER_PIXEL|Fuji额外241118208bytes约229.95MiB+allocator1024；nativeRSS/time未测|
|家中MSVC原生链|MISSING|cl/nmake/dumpbin/SDK开发环境不可用，不换MinGWdecoder跑RAW|
|候选Fuji两次无调试器|NOT_RUN_COMPANY_HANDOFF|同RAW/新批次最多2；build+native selftest/预算冻结后公司执行|
|真实P6一致性/基线差异|UNKNOWN_NULL|不拿合成差异和历史数值填真实结果|
|真实RAW/GPU/debugger|0|无新自然图/旧六图/stock/BT/跨GPU或锐化V2|
|产品修复/接纳|NOT_FIXED_NOT_ADMITTED|只完成候选及合成；default/发行DLL不替换|

公司下一步见COMPANY_HANDOFF.md和COMPANY_NATIVE_PLAN.json，不重置旧ledger，不重复H1window。
