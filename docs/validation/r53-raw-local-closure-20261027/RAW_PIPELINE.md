# 专业RAW局部编辑真实合同
日期：2026-10-27 America/New_York。

- LibRaw0.21.1既有解码：黑电平/WB/相机矩阵在解码应用一次；线性ProPhoto D65、uint16到float。解码内部已有饱和裁切，不宣称恢复传感器高光。
- PV4基础曝光在线性ProPhoto域，再沿原固定luminance shoulder + neutral-axis gamut compression + sRGB OETF。**新增ProfessionalRawLocalEdits Version1在此非线性display-sRGB边界之后**调用共享ColorStudio逐层工具与LayerCompositeV1；不在线性RAW套乘/滤色。
- 源RAW/master/DecodeKey不变，不生成JPEG代替源、不新建普通编辑变体。缺省LocalEdits=null/Geometry=null，旧PV4/PV5/PV6字节与算法路径不变；新扩展显式存入专业RAW定义，非PV7研究引擎。
- 局部层输出保留float负值/>1至最终编码；已有基础显示映射范围0..1并未被移除/新增提前clip。既有各工具自身domain/clamp策略不改；宽色域scene-linear局部层不是本版本能力。
- 现阶段支持共享点工具（基础影调、白平衡、曲线、色阶、色彩平衡、肤色、基础预设）；参考匹配/空间细节不支持，修改边界明确拒绝，不放空滑杆。
- Geometry V1为源图正向归一化crop/90度旋转/水平翻转。蒙版固定正向未裁源图归一化位置，按实际采样源像素中心求权重；输出orientation1。预览先执行同全分辨率显示和局部层，再display-linear精确area下采样；导出同逐行区域内核。
- 简易/专业共一Editor栈；编辑同步冻结定义供立即保存/切换，后台90ms合并预览以版本/目标/变体/取消校验。RAW局部/几何统一定义历史，复制冻结蒙版，不共享可变集合。
- 入ICC：专业RAW不读取任意输入ICC，沿既有LibRaw相机矩阵；普通图像WIC明确转换sRGB。工作域/输出：仅此sRGB边界及sRGB TIFF16/PNG16/JPEG8，非通用ICC引擎；未知工作空间/定义拒绝。未完成profile与参考完整准入前保留诊断限制，不简单删除开关。
