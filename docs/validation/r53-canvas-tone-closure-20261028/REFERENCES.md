# 固定源码与采用决定（2026-10-28 America/New_York）
## 画布
- fabricjs/fabric.js v5.3.0 @7db80e5bb674b52b0c2f39aa5a124e6c4a77627a，MIT，src/brushes/pencil_brush.class.ts::_finalizeAndAddPath（一次笔划收尾形成一个对象）、src/shapes/object.class.ts变换/序列化；独立复用设计，不引入JS引擎，不拷贝实现。
- konvajs/konva9.3.18 @6e28a2a93b5bb9be2f98fb4213127528d3241eb7，MIT，src/shapes/Transformer.ts transform/stopTransform、src/Node.ts toObject、src/shapes/Line.ts点数组；手势边界与文档状态分离。产品继续CanvasEditor/CanvasDocument/WPF，不起第二画布引擎。
- 对透明插值，采用premultiplied float source-over，最终一次unpremultiply；WPF PNG编码嵌sRGB，监视器profile不进入文档。已有规划自有副本的身份/路径/hash验证复用，不修改.ptplan v1。
## 调色
- darktable5.2.1 @be9171cf8bb2feab8bd115efd9b96a4ce7103039 src/iop/basicadj.c::get_contrast；实际抽取原函数编译执行51点（linear input、pivot.18、exponent.5/1/1.5），不是产品公式替身。
- RawTherapee5.12 @a8a3d1bc7b419ed1c974c39276c97e618a4f9a1b rtengine/curves.h::basecurve/cupper/clower/依赖，实际编译执行42点，a/b/D/hr/sr固定。原函数在部分域外返回NaN，原结果null保留，不硬凑同名参数等价。GPL3源码和研究exe仅本地隔离，不入产品/证据源码包。
- 这是实际成熟tone-stage执行对照，不是完整两个相机引擎运行；ICC输出转换使用现有LCMS单独测。完整科学准入保持OPEN_DIAGNOSTIC_ONLY，不以TIFF一致证明色准。
