# 初始开源实现阅读清单（非数值验收）

检索日期2026-10-15。固定已知版本用于可复现，不声称它们是最新版本。源文件仅下载到本地artifacts/company-ab/oss-reference，不复制进产品或Git。完整commit及下载字节SHA见REFERENCE_INDEX。

|问题|实现/固定版本|已核对具体入口|拟用测试与判断|当前决定|
|---|---|---|---|---|
|RAW黑位/WB/处理顺序|LibRaw 0.22.2 / b93f6e45c194f5df9b02a43b1af9a54b4f41f33f|src/preprocessing/subtract_black.cpp::LibRaw::subtract_black_internal；src/postprocessing/dcraw_process.cpp::LibRaw::dcraw_process|固定原件hash、参数、unpack/raw2image/black/WB/matrix/RGB16各自实际域，禁用隐式输出profile；候选需hash/license独立核对|只读研究/独立候选探测；不替换旧0.21.1|
|RAW/WB第二实现|RawTherapee 5.11 / db575c6690d30ce6428c0e1383bec8b8f1fb8d21|rtengine/rawimagesource.cc::RawImageSource::getWBMults、colorSpaceConversion_；不能只对比UI温度值|同源RAW、明确相机profile/白点/WB与输出域对齐后比较中间统计和16bit/float；当前NOT_RUN|行为研究，GPL代码不入产品|
|曝光/高光|darktable 4.8.1 / fb0eb997e62c6dd52966cd35911c74afa150e209|src/iop/exposure.c::process；src/iop/filmicrgb.c::process/commit_params|固定线性中性/原色/高光负值，核对black/exposure与tone/gamut先后；diff不得掩盖不同算法目标；当前NOT_RUN|行为研究，GPL代码不入产品|
|曝光/高光第二实现|同上RawTherapee 5.11|rtengine/improcfun.cc::highlightToneCurve及rawimagesource.cc处理顺序；完整入口/所有依赖参数需再按具体节点追踪|相同域和浮点样本跑实际实现而非重写一份“参考公式”；记录逐阶段差异/CPU/内存；当前NOT_RUN|未完成具体节点追踪，禁止据此开写算法|
|ICC转换|Little CMS 2.16 / 453bafeb85b4ef96498866b7a8eadcc74dff9223|src/cmsxform.c::cmsCreateTransformTHR/cmsCreateTransform；testbed/testcms2.c::CheckGammaFloat、float interpolation tests|实际v2/v4 matrix/TRC/LUT、16bit/float、intent/adaptation/BPC/flags；与RawTherapee colorSpaceConversion_独立运行比较；当前NOT_RUN|优先评估复用，未引库；MIT LICENSE已读，归属义务仍需随包设计|
|显示/未来ACES|OpenColorIO 2.4.0 / 2831d2547d133b326c6e9a09726ceeb7fe46f997|src/OpenColorIO/ops/matrix/MatrixOpCPU.cpp::GetMatrixRenderer/apply；tests/cpu/ops/matrix/MatrixOpCPU_tests.cpp::scale_renderer/matrix_renderer|CPU float矩阵及offset，负/超范围；ACES需另固定config hash与ViewTransform，当前未选择config、NOT_RUN|评估库/架构，不将单矩阵比较冒充ACES完成|

许可证阅读：LibRaw文件头LGPL2.1或CDDL1.0；RawTherapee及所读darktable文件为GPL3-or-later；Little CMS LICENSE为MIT；OpenColorIO LICENSE为BSD-style（三条件文本，以固定文件为准）。只表明已读，不构成产品兼容与分发批准。
Little CMS初始COPYING路径404，随后准确取LICENSE成功；失败保留索引，不伪称所有路径都有效。

以上没有实际执行两个参考渲染器，因此没有数值比较PASS。后续每节点仍必须补完整运行方法、冻结输入、差异、性能/内存和历史golden，不可把这张表当开发通行证。
