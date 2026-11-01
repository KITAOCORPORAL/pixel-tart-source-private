# 像素节点比较与许可边界

本轮仅诊断，不新增/改变像素节点，不将外部代码复制入产品。输入域是JPEG8位YCbCr、native reduced-IDCT到BGR24、同源ICC到系统sRGB Bgra32、EXIF纯排列；WB/RAW/黑白位不在本轮，encoded域与原v3一致。source/target SHA与版本在FINAL_REPORT与COMPARISON_CONTRACT_V1。

两个成熟参考：Pillow12.3.0/内置libjpeg-turbo3.1.4.1与FFmpeg9.0.2（固定源码idctdsp.c/jrevdct.c），ICC使用Pillow LCMS2.19＋既有外部LCMS2.12精度探针、原创新matrix/TRC受限oracle、系统MSCMS独立参数化调用。完整commit/文件哈希见REFERENCE_PINS，实际工具/系统版本见metrics。

FFmpeg当前build GPL，源码只读研究，未复制、翻译、改名放入产品/测试实现；fixture encoder为原创baseline Huffman写入，数学area oracle为独立公式，不是移植FFmpeg代码。libjpeg-turbo的IJG/BSD/zlib许可按文件评估；LCMS MIT；WPF MIT，研究源码文件仅留本机，不纳入Git/应用发布包。系统ICC为现有Windows系统profile诊断引用，不是新增商业分发许可结论。

所有新行为仅为探针；原v1/v2/v3保存/旧导出/预算/任务语义无变化。原门槛不放宽，照片原FAIL保留。阶段诊断通过不证明JPEG规范全合规或任意ICC科学正确；Windows内部算法不可见，明确保留未知，不对专有实现作源码级承诺。CalibrationEligible=false。
