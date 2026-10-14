# 夜间B冻结与科学结论

起点13b0efe，A本地ec20c77为其后裔；共同祖先13b0efe。B无A策划路径/错误修复，仅已有色彩整合代码加独立read-only实验库。A后续912fa82/ed1280f也不包含于B。

B1数值图和统计合同、B2合成sensor中间值探针、B3PV7默认关闭实验、B4小图独立进程5节点、B5非标定采集协议均有提交；后续六项新增测试全部required_automatic，旧逐名分类无改。旧72golden/EXIF/HQ/编码测试冻结，历史renderer、LibRawDecoder、ProfessionalRawPipeline、InstantFilm和UI零diff。

PV7新冻结记录包含SourceSha、DecoderIdentity、Domain、WB-baked、MatrixSha、OutputProfileSha，full cache/decode key独立；EncodedSrgb不得作为未WB相机RGB，工作值溢出拒绝。它是后端实验原型，未接用户新版本选择/UI/export编码器，不能把数学prototype称正式PV7产品实现。标准profile边界目前是合成数学描述，不是ICC文件解析/全ICC。

科学结论按四层：
1. 数学验证：合成灰阶/曝光/负值/超范围/高饱和有限性、single OETF、中性和D50/D65适应有对应单测。
2. 真实相机准确度：没有新增授权实拍/留出测量，未证明A7M4准确；无HDR真实纹理恢复。
3. Sony候选：历史固定官方0.22.2身份与源码检查，当前未找到本机二进制和同hash授权原件，本轮NOT_RUN；默认未启用且Release不带candidate。
4. 宝丽来：CreativeApproximationV1预设与采集协议，不是准确现代相纸LUT。

B2真实RAW探针/B4大图逐节点/原生allocator归属/自然释放系列/图像可视对照/真正ICC文件合成编码未完成，明确OPEN；不替换成小图PASS。本轮不声称性能改善，无证据生产优化零改。
