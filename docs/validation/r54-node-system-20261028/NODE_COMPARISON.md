# R5.4 图路由与合流准入记录
日期2026-10-28 Europe/Berlin。

## 数学与范围
- 旧Stack2/Graph absent完全走既有节点处理及量化；Graph1仅改变显式串行顺序，坐标不参与像素。新图保存为Stack schema3，旧版Normalize明确拒绝，不允许忽略Graph并错误打开。
- Graph2为有向无环依赖图。RGB从Source到输出，仅通过显式GraphMix的base/layer端口合流；base先、layer后。普通节点单rgb输入；未知端口/重复输入/环路拒绝。缺任一必需祖先连接=>输出为原始源图，draft与断开节点仍保留。删节点下游重接primary/base输入；旁路Mix=base，旁路工具=它的输入；锁定节点参数/位置/邻边不改，显隐仍可切换。
- 输入/输出encoded sRGB D65 [0,1] float，WB/曝光已在各自上游完成；调用现有StudioLayerComposite，Normal/Multiply/Screen/Darken/Lighten；`base + opacity*mask*(blend(base,layer)-base)`。这是不透明调整结果分支，不是新透明RGBA图层，源alpha由WPF/调用方保留、不重复相乘。不是线性光/scene RAW并行，不制造多次Display/WB/ICC。
- GroupEnabled/GroupAmount作用于完整Graph2结果与Source的同域混合。简易整栈“专业组”保守锁路由，不展开/压平后保存。进一步按复杂子图分组仍缺。
- 线性ProPhotoD50文档并行拒绝；已显影即时胶片窗口并行拒绝，原定义保持。预览、行带/空间导出、源坐标蒙版使用同graph定义。64节点、192边上限+分支buffer预算，可取消。

## 两个独立成熟实现：实际读取并执行
1. Pillow12.3.0 commit bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d：src/PIL/ImageChops.py、src/libImaging/Chops.c::ImagingChopLighter/Darker/Multiply/Screen、LICENSE（MIT-CMU）。实际运行同版本wheel ImageChops与Image.blend。
2. OpenCV4.10.0 peeled commit 71d3237a093b60a27601c20e9ee6c3e52154e8b1（tag object 67f3511f...不能当源码commit）：modules/core/src/arithm.cpp::cv::min/max/multiply/addWeighted、LICENSE（Apache2）。实际执行opencv-python-headless4.10.0.84。固定源入口与hash见BLEND_REFERENCE_FILES。

4,000固定输入（五模式×四强度×200 RGB对）由实际C# StudioLayerComposite生成；Python脚本对照两个独立库。预先固定上限2.1个8bit码值；Pillow最大0.9981、OpenCV最大0.7471，量化/舍入差异保留，不称float逐位同构。结果见BLEND_TWO_REFERENCE_RESULT。Mask/顺序/0&1/group/serialize/rowband/spatial逐项C#回归；不是色彩测量。负/>1和高阶混合沿现有准入边界，不宣称本批对它们新增批准。

代码决策：复用既有Core工具+合成，不复制任何GPL/参考C/Rust进产品，不引入OpenCV/Pillow依赖到发布。既有PhotoCraft/PDFium固定对照保留于home-r53-cms-closure，不用旧Pillow不同SoftLight定义冒充本批精确对照；GraphMix菜单只放上述五模式，高阶模式未新批准。

## 原创框
不是第三方LUT节点；仅12个用户设计里明确可发布的FRA_ORIG实际图像。原RGBA8数据解码一次并无损zlib嵌入Core，manifest记录源PNG与RGBA hash、授权原文字、窗口。配方保存FrameArtwork版本/ID/hash/几何，缺失或不匹配明确失败，无替换。图像效果amount不混frame；最后叠真实straight RGBA8框，固定窗口/比例，不烘焙monitor ICC。该版本使用确定性nearest资源采样，预览随后按完整结果下采样；不是新摄影光学拟合。

## 结论
EXPLICIT_DIAGNOSTIC_ONLY；旧串行golden/保存重开/实际导出已有受影响回归，非完整科学算法或原生交互批准。RAW spectral calibration=false；不得称i2/SX70实测。
