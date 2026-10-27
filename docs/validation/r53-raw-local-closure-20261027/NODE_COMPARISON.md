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

# 蒙版参考与V1语义
- 复用darktable5.2.1@be9171cf8bb2feab8bd115efd9b96a4ce7103039，src/develop/masks/brush.c::_brush_falloff：线性hardness/density径向羽化，MAX合并同笔栅格。实际编译抽取函数，15组参数/100样本；离散邻像素填补/整数solid导致连续公式差异，原始max0.040000029保留，非声称完全一致。
- GIMP3.0.6@a77e8fd05d3d08a58650531a3700100f6a3216b7，app/paint/gimppaintcore-loops.cc constant/stipple opacity累积，app/operations/layer-modes/gimpoperationerase.c擦除。参考标量表达式实际执行：可等价参数子集192组；全密度stipple和擦除max0。不是完整GIMP画笔引擎/pressure执行。
- 两者GPL3仅本地隔离研究，无源码/二进制复制产品。沿用独立LocalMaskV1折线最短距离和线性羽化。V1每笔一次：a=coverage*Flow；增加w+=max(0,Density-w)*a，擦除w*=1-a；反相1-w。同笔回折/停留不反复喷涂，多笔可累积。UI明确说明，不擅改成连续dab，旧Version1保存结果不变。
- 实现优化仅精确BVH分支剔除，不改公式；随机/回折/重叠/擦除/反相与原暴力段扫描逐样本1e-12核验。索引每冻结渲染编译，无全尺寸蒙版栅格缓存；覆盖max768^2×4，33ms合并+取消+目标/变体/节点/代次校验。
- 本参考补齐可复现语义/差异，不自动等于完整科学/ICC准入；诊断限制按POLICY仍保留，不能通过删条件编译宣称完成。

## 准入与遗留
沿用上轮darktable/ImageMagick固定混合kernel1200组结果，不重复自写公式当第二参考。新增GIMP/darktable蒙版执行含差异；真实Canon线性master保存重开/导出补证。仍无通用ICC或scene-linear局部引擎批准，未知profile/工作空间不伪装支持；诊断开关继续保留，不做发行结论。
