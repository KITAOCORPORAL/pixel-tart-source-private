# XTransStableInputSeparateRgbV1：隔离修复候选

基线70130678；不运行tools/raw-h1-window/run_batch.py。默认关闭，**仅显式candidate诊断构建**，不替换产品库。

1. 只读源码审核/生成（旧输出存在即拒绝）：

    python -B tools/raw-xtrans-stable-v1/prepare_candidate.py --source <固定xtrans_demosaic.cpp> --output <新外部补丁目录> --enable-candidate XTransStableInputSeparateRgbV1

原始SHA1c4aa736…2045af、reviewedobserverSHA cf4616cc…e8c84fc两个入口固定；CRLF归一为LF但完整canonicalSHA必须匹配，原工作树字节hash另记。patch和candidateSHA可按本批GENERATION重建。

2. 无RAW自检复现（必须全新build根）：

    python -B tools/raw-xtrans-stable-v1/build_synthetic.py --source-root <固定LibRaw0.21.1全源码> --candidate <上步xtrans_demosaic.cpp> --compiler <既有LLVMclang++.exe> --output <新合成build>
    python -B tools/raw-xtrans-stable-v1/run_synthetic.py <新合成build> <原compiler的bin目录>
    python -B tools/raw-xtrans-stable-v1/audit_geometry.py <新整数几何报告.json>

这是exactfunctions/surrogatestorage，不是fullnativeLibRaw或RAW解码。现存build02已执行，**不要为交接重复运行**；可读SYNTHETIC_*材料。test_preparation.py只做生成器拒绝/源码检查，不启动C++或decoder。

3. 公司新诊断源（在已有原工具链的端执行）：

    python -B tools/raw-xtrans-stable-v1/stage_diagnostic.py --upstream <固定cleancccb源码> --reviewed-observer <原70130678匹配source> --output <全新candidateprepared根> --enable-candidate XTransStableInputSeparateRgbV1
    ./tools/raw-xtrans-stable-v1/Build-Candidate.ps1 -PreparedRoot <新根> -OutputRoot <新buildreceipt根> -EnableCandidate XTransStableInputSeparateRgbV1

脚本只buildnative，不开RAW；先运行既有原生/bindingnoRAWselftest和冻结新manifest，再由公司原统一host/资源守卫安排COMPANY_NATIVE_PLAN中的**新独立2进程无调试器批次**。旧原始无改动构建guard不适用于故意变更候选，不能设置假originalByteRestoration骗过旧工具。完整参数/quality/passes/线程策略不改，旧账本不重置。

家庭本次工具链缺cl/nmake/dumpbin，所以nativeRaw0。交接说明docs/research/raw-reopen-001-contract-20261104/stable-input-v1-20261109/REPORT.md及COMPANY_HANDOFF.md。

本地现有根：N:\pixart\raw-reopen001-shared-research\stable-input-v1-20261109；candidate-source为上游候选，observer-candidate-source为诊断同版候选，diagnostic-source01为完整新诊断源，synthetic-build02为已执行无RAWbuild/results。完整来源、二进制/合成buffer不进Git。无production入口、无后续窗口追差、旧锐化封存不动。
