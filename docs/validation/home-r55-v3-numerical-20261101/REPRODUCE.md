# 本机复现顺序

所有临时/私照输出在新隔离目录，禁止覆盖本批旧输入/结果。固定工具和SHA见REFERENCE_PINS、metrics。产品src没有新增逻辑。

1. 编译WPF测试支持，BuildProjectReferences=false；复制已有发布绑定testhost到新目录，再覆盖测试DLL，保持产品20共享DLL原哈希。KITAO_V3_NUMERIC_INPUTS指定显式JSON路径数组，KITAO_V3_NUMERIC_OUTPUT指定新目录；PIXEL_TART_ISOLATED_RUNTIME=1与显式隔离root。运行V3NumericalStageTests.ExtractNativeColorAndOrientationStages。输出native/icc/oriented BGRA与planar opt0/1。大原照片会与真实session逐位对照；不得把私照stage文件入Git。
2. make_coefficient_fixtures.py生成原创系数JPEG（脚本Q=1、category7 amplitude64..127）；make_owned_fixtures.py生成探索/保留颜色夹具。另生成保留JPEG两项：hold-y-ac3-96、hold-cb-ac3-64；准确输入可直接用fixtures/已提交字节，不能替换原照片。
3. 阶段归因脚本analyze_jpeg_stages.py、analyze_coefficients.py、analyze_reconstruction.py、analyze_icc_stages.py、analyze_icc_precision.py、analyze_icc_mechanism.py接收本地root。它们默认沿本批具体输出结构，是诊断回放，不是产品API；原same-input路径身份记录于INPUT_IDENTITIES，私照不分发。ICC受限oracle只接受冻结的两个SHA，不是通用ICC转换器。
4. 先冻结COMPARISON_CONTRACT_V1，再运行holdout探针和verify_holdout.py；该脚本读取holdout-contract-stages-icc-analysis＋ICC_MECHANISM_hold-*。expected <=1/0关系未用结果拟合。脚本pass只表示归因关系，原数值脚本verify_photo_numerical.py仍要求原阈值并返回1。
5. 原38唯一方法汇总与测试支持身份见TEST_SUMMARY/IDENTITY；原FAIL/缺输入不删除、不混入新数量。产品未变不用再次publish，verify_delivery.py校验旧ZIP逐项hash即可。
