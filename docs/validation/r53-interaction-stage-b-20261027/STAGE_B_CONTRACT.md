# 阶段B首项：SceneExposureV1（诊断版）

输入RAW：既有LibRaw0.21线性ProPhoto D65、相机WB/黑电平/矩阵已应用一次。基础曝光ExposureEv在线性域；本轮SceneExposureV1在基础曝光后、原PV4固定显示映射之前，对源坐标蒙版区域应用无裁切线性增益1+w*(2^EV-1)。不使用非线性sRGB Multiply/Screen，不再应用相机WB。

收益：显示映射前负EV可以保留现有线性master中尚存的高光比例；显示后BasicTone曝光已有rational shoulder，不能等同场景曝光，UI明确分组。LibRaw先前已裁切的信号不能恢复，不称传感器高光重建。负值/>1保持至原显示映射；最终sRGB output仍受原显示映射范围限制。

版本：ProfessionalRawDefinition.SceneExposure缺省null，旧PV4/PV5/PV6路径不变；独立Version1，mask冻结；预览/几何/导出调用同RenderPixel，场景蒙版绑定正向未裁源。并非PV7宽色域引擎批准。

参考1：darktable5.2.1 @ be9171cf8bb2feab8bd115efd9b96a4ce7103039，src/iop/exposure.c exposure2white/process，black=0，scale=1/exp2(-EV)，out=(in-black)*scale。
参考2：RawTherapee5.12 @ a8a3d1bc7b419ed1c974c39276c97e618a4f9a1b，rtengine/curves.cc powf(2,ecomp)，rtengine/dcp.cc::step2ApplyTile no tone/look分支RGB*=exp_scale。
两者GPL3仅隔离研究，产品独立实现，未复制代码/引入第二引擎。实际编译固定scalar-stage执行56组（负值、黑、灰、饱和通道和16高光×EV），max绝对3.98e-7。tcc旧CRT缺exp2f/powf，以double pow后float cast shim明确记录；这是标量阶段对照，不是两个完整RAW/profile引擎等价。

科学边界：ICC/profile/宽色域/场景线性通用局部工具属于B后续，不再算A必须无限完成。当前B首项已启动，诊断限制保留，CalibrationEligible=false；保存重开/编码相同不证明色彩准确。

剖析计时为各并行阶段累计CPU区段，不能相加当wall时间；完整误差/真实样片数值以最终PERFORMANCE.json为准。
