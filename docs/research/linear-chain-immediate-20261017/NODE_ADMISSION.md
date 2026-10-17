# RenderedSrgbExposure1：实施前节点比较合同

节点/位置：已渲染sRGB输入→D65线性sRGB工作域→曝光乘2^EV→IEC舍入sRGB编码→最终0..1裁切。所有节点一个明确版本RenderedSrgbExposure1，不串旧V3/RAW。输入RGB float32/16bit、ICC仅既有WIC到sRGB、方向归一、alpha仅新版受控白底；负/>1数学样本保留到输出裁切。白平衡/相机矩阵不重做；不是场景RAW。

参考1 colour0.4.7 a3bfe349685f528100672e5c8ca2dfeeef64a273，colour/models/rgb/transfer_functions/srgb.py/eotf_sRGB、eotf_inverse_sRGB；明确只配置scalar decode cut=.04045，与规范目标匹配，其向量库公式不替换。参考2 scikit-image0.25.2 e8a42ba85aaf5fd9322ef9ca51bc21063b22fcae，skimage/color/colorconv.py/rgb2xyz、xyz2rgb原函数AST；identity矩阵用于观测transfer，末尾clip单独观测非裁切encode。都BSD3，仅外部验证，不复制/分发。源/许可证hash见SCIKIT_PIN与前轮REFERENCE_PINS。

保留原colour/OCIO不等价对照；OCIO连续moncurve不是目标精确独立真值。阶段数据含原.04045/两阈值邻域/double与float32相邻/中性/原色/负/>1，每阶段2e-10原门槛最大误差0。不删样本、不放宽2e-6 OCIO旧门槛；将不等价算子改为差异证据，不冒充通过。

独立实现仅调用已有PhotoColorMathV1严格舍入sRGB函数与pow曝光，PhotoColorMathV1历史不改；新链独立version/项目定义，可选可回退。曝光算术成熟DT/RT已核授权照片子阶段继承instant-v3-live/01_admission/BEHAVIOR和DECISION，明确不宣称创作tone或完整RT/DT等价。

准入范围：合成数学VERIFIED_RESEARCH，真实输入与产品入口/保存重开/导出独立比较完成才EXPLICIT_VERSION_APPROVED；未完成前不宣布生产验收。不同真实ICC、RAW场景宽域、DWG、显示器ICC未准入。负值输出最终clamp明确，不恢复剪裁。

产品验证计划：实际授权冻结Sony/Canon RGB16取样及代表/原尺寸，双参考逐阶段，真正项目重开/undo/redo/输出16bit逐码与JPEG既有门槛，预览线性面积oracle；未来未通过保留失败。

已补48个授权原冻结RGB16不同坐标样本（原件/冻结hash在授权manifest核对），边界与真实样本合并288组EV/RGB逐阶段两参考最大0。新链实际workspace命令/撤销重做/保存重开/三格式回读已通过synthetic，首次测试假设普通照片MatchedImage不为null错误，改用SourceImage fallback，原失败保留。产品仅声明sRGB输入解释/线性曝光/OETF+最终裁切；不碰旧PhotoColorMathV1/PV4/5/6。

原尺寸二源原WIC64N+文件缓冲预算会拒绝Canon/Sony（各76N≈1.53/2.50GB，未加总预算）。新增TIFF输入组件3只支持产品单条带未压缩RGB16、精确sRGB ICC，用TiffStreamValidator逐行解码，最终float12N+一行；不暗缩小，不改变1/2。真实多ICC/透明仍旧2，不以新3冒充通用TIFF。独立WIC旧1逐码比较及源指纹回归要求。

独立产品输出已跑：3授权冻结照片各64×48区域，经实际ProfessionalRawPipeline新节点和三格式编码，输出文件重新解码像素独立对scikit transfer（不是产品oracle）TIFF/PNG逐码0，JPEG最高MAE2.216/RMSE3.077，沿既有5/15门槛；INDEPENDENT_ENCODED_OUTPUT.json。范围升级EXPLICIT_VERSION_APPROVED仅Rendered sRGB exposure1；场景RAW/DWG/显示ICC不批准。
