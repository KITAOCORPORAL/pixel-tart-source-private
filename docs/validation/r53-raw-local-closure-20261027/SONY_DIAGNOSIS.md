# DSC09916.ARW拒绝定位（不更换解码器）
- 原hash 6f596bb234ee5de206c66594bc84205349c363fd6a89ae188ff76dab7c5e53b9，24563712bytes；原文件与同hash副本、原生open_buffer均-2。排除仅managed路径/路径字符集问题。
- TIFF主元数据SONY ILCE-7M4，firmware v6.00，EXIF8。RAW SubIFD5120×3584、3通道、15/15/15位、YCbCr photometric6、compression7、70个512×512 JPEG-lossless tiles（FFD8FFC3）；所有tile区间在文件内，尾部1392bytes。结构未显示截断，但无相机原始校验和，不能证明每个压缩字节无损。
- LibRaw0.21.1 @ cccb97647fcee56801fa68231fa8a38aa8b52ef7 metadata/tiff.cpp以旧Sony mosaic/14bit特例选择解码；此15bit三通道YCbCr不在同合同。0.22.0 @ 0b56545a4f828743f28a4345cdfdd4c49f9f9a2a已出现sony_ycbcr_load_raw/对应识别，但不代表该compression7样本已被其验证。
- 结论：已定位到具体新固件/RAW编码类型与本机既有LibRaw识别准入不匹配；不是所有Sony RAW不可用，也无证据把相机型号本身判损坏。未盲目替换库、未通过修改标签强行解码。
- 该样本保持UNSUPPORTED_WITH_CURRENT_DECODER。即使未来解码支持，此三通道已非Bayer输入还须独立检查相机WB/矩阵已应用状态，不能直接灌入目前三色Bayer线性专业路径。
