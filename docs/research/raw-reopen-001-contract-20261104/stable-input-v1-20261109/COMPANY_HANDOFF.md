# 公司端：仅候选原生编译、自检及新两进程对照

这是家庭工具链缺项后的交接，**不是再抓H1窗口**。候选实现/原算法合成自检已完成；真实Fuji两次运行在家庭未执行，不能填写PASS。当前用户已授权新的独立candidate批次，任务名RAW-REOPEN-001-STABLE-INPUT-V1-20261109，与历史window/validation账本区分。

## A. 准备源与编译

在当前研究commit的独立checkout，只从公司现有固定upstream和与70130678匹配的preparedobserver源码准备。不要访问旧bin作为新源，不覆盖旧树。

    python -B tools/raw-xtrans-stable-v1/stage_diagnostic.py --upstream <固定cccb9764源码> --reviewed-observer <原cf4616cc-xtrans/ce42-header诊断源> --output <全新candidate-source> --enable-candidate XTransStableInputSeparateRgbV1
    ./tools/raw-xtrans-stable-v1/Build-Candidate.ps1 -PreparedRoot <全新candidate-source> -OutputRoot <全新candidate-build-receipt> -EnableCandidate XTransStableInputSeparateRgbV1

stage工具校验7份原生成源以及各include目录的headerSHA；只替换xtrans，别的6源、数值公式/参数/线程pragma均保留。固定observercandidateSHA=f11b4e4b46cf1c863f0af0fa469be05ad2e4a3f3d1c854ace4b66064528dbb18。生成到Gitcheckout/原source内部或旧输出目录会拒绝。原始无改动observer构建器的originalByteRestoration门禁**应当拒绝这个故意改变像素结果的候选**，不要伪填true或改旧收据。

Build脚本只对新的候选诊断源执行MSVC/NMake目标；COPT_OPT=/openmp及其它原optionalcomponents不变，输出新的DLL与PE/编译身份。家庭仅验证此脚本语法，未运行它的原生build。不要拿家庭LLVMsynthetic.exe当可用RAWdecoder。

## B. 原生无RAW门禁与运行前冻结

先沿公司已有正确版本的RawNativeInternalProbe host/ABI/noRAWselftest完成新DLL准入。用**新候选manifest/contract/批次任务标识**绑定新source/header/DLL/host/binding/runtime，不能把新DLL冒作原305EEC49，也不绕过旧host/contract哈希拒绝。若原host的“source原样恢复”guard与本candidate冲突，应版本化最小candidate准入适配并独立审核，不弱化旧原生观测器；未通过则停在此处，不启动RAW。

冻结实际loadedmodule、编译器/SDK/完整flags/OMP相关宏、binding及依赖、真实参数和P5/serial有效状态；禁止手动清/setOMP或替换decoder以取0差。不要启动raw-h1-window/debug_watch或重新读取旧窗口。

## C. 新批次、最多两次、无调试器

COMPANY_NATIVE_PLAN.json固定本批：同一DSCF0347.RAF，SHA52d054ed9a90306db0b7eb5d6c408a2e006532b976ac88ed792692f9f744e865。最多两个串行独立进程，各Open/Unpack/Process/MemoryImage一次，**无调试器/硬件watchpoints**。使用已有外部2GiB/600s守卫和50ms采样，不提高阈值。每次spawn前排他写新consumed预留；失败也消耗该slot并停止，不自动补第三次。旧两个历史批次consumed证据保持原hash。

当前request已经授权两个新slot，但不是旧taskguard的免费额度。既有固定旧task的runner不能不改schema就直接重用旧ledger；新candidate批次配置须明确任务、授权、maximum2、旧证据绑定和新manifest，先预算/自检通过，再最多启动剩余slot。

参数与原configured-output逐字段相同（不仅少量UI值），quality=-1继续由原分支得到实测passes3/T512/ndir8，记录真实team和动态分配；不把OMP_NUM_THREADS=6当team事实。候选仅改变所有权，不能改变WB、gamma、黑位、median/highlight、插值质量或编译优化/FMA。

## D. 要交回的结果

1. 两进程的P5和XTRANS-serial-ready完整有效输入/状态是否一致；P6有效RGB含边缘逐bit是否一致。尺寸7752×5184，输入4槽/输出RGB3分账，不横比不同布局。
2. 对**已存4c322975两个P6baseline**逐份hash核验后分别比较：different channels/pixels、max/mean U16、first/max坐标、bounds、边界统计。旧baseline缺文件则给精确缺项，不为补它重跑旧decoder。旧带debugger窗口无完整P6，不能当新baseline。
3. 原生候选实际峰值工作集、privatebytes采样峰值、每阶段耗时与限额是否触发；区分分析预计+241118208bytes与实测RSS差。原调试器耗时不作为速度baseline；外层不同扰动下不虚报提速。
4. 原F2全流程比较器在P8rawColor旁路的拒绝仍保持；插值prefix可用已有验证入口，不能把P6一致冒称defaultRAW/preview/projectreopen/export全链修复已通过。
5. 新DLL/host/source/日志/slot/输出manifest完整身份。真实RAW、完整stage像素/CSV仅授权本地；Git只统计/哈希/patch和小证据。

任何selftest/预算/执行/字段不匹配先停止并保留，候选两次后也停止，不自动调参或扩照片。候选P6是否一致与已存baseline差异当前为null/NOT_RUN；本handoff不预判真实修复结果。
