# LayerCompositeV1 / LocalMaskV1 节点对照（2026-10-27 America/New_York）

## 合同
- 独立新增 CompositeVersion=1；旧节点缺省0，未修改PV4/PV5/PV6数学。原项目不暗迁。叠加在各层已有效果之后、下一层之前；先求效果，再使用 Opacity × mask 做直RGB插值；alpha不变。
- 现有display-referred nonlinear sRGB/D65 float；不是线性光混合。Normal、Multiply、Screen；扩展浮点不裁切，最终编码器量化。WorkingSpace旧字段不意味着新建第二套OKLab混合引擎。
- 蒙版坐标为已正向源图归一化位置，半径相对短边；折线最短距离、线性羽化、每笔一次flow/density累积；不声称等同pressure/dab brush。反相后合成；隐藏不删除；冻结拷贝隔离变体。

## 固定参考及许可证
1. darktable-org/darktable release-5.2.1 @ be9171cf8bb2feab8bd115efd9b96a4ce7103039，src/develop/blends/blendif_rgb_hsl.c::_blend_normal_unbounded/_blend_multiply/_blend_screen；src/develop/masks/brush.c::_brush_falloff/_brush_falloff_roi。GPL-3.0参考仅本地隔离研究，不进产品/发布包；未复制到产品源码。darktable bounded screen/multiply和本产品扩展域语义不同。
2. ImageMagick/ImageMagick 7.1.2-34 @ a0c4bf729a26a5c2feb28b0365b47a8db736078a，MagickCore/composite.c::CompositeImage 的Over/Multiply/Screen颜色通道分支；MagickCore/paint.c仅研究。ImageMagick License，未引入或分发其代码/二进制；无新发布依赖。采用独立数学实现，不拼接两引擎。
- 原始文件、完整license、SHA、参考抽取/编译脚本留交付根 references/，不混入应用ZIP。tcc 0.9.27为隔离本地研究编译工具，ZIP SHA256 34a721949a2583fdff725312da092fa0f5f1f284b702e6f811c6954714faabb2，不分发编译器。

## 已执行和差异
- 实际编译并执行两套固定源码的标量函数分支，非Python自写公式替身；是kernel对照，不是完整darktable/ImageMagick图像pipeline。
- 1200组灰阶/饱和通道/黑白/高光/负值/>1 × 0/.25/.5/1强度 × 三模式；正常域最大差异darktable 1.16294e-7、ImageMagick 8.93307e-8。277组超范围差异保留，V1不裁切与参考bounded公式不等价。完整结果REFERENCE_RESULT.json。
- 真实TNAN8886.CR3 SHA256 b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401，5496×3670，现有公司命名测试资产；16位解码、全分辨率行带渲染、TIFF16独立读回与源hash复验。decode1051.2241ms，render4923.9866ms，encode1002.3409ms，TIFF逐样本差异0；采样全图/单点差异0。测试进程峰值2729193472bytes，包括解码、读回和断言副本，非硬性内存上限。
- DSC09916.ARW SHA256 6f596bb234ee5de206c66594bc84205349c363fd6a89ae188ff76dab7c5e53b9，原路径及独立同hash副本均失败；原生open_buffer=-2 Unsupported，不改解码器、不将其算PASS。

## 未通过门禁
- 蒙版两成熟实现的相同brush行为可复现数值执行未完成；线性羽化独立合同不等darktable Bezier/pressure raster。
- 不同ICC/profile矩阵、完整已批准RAW集合、真实鼠标/DPI及大规模笔迹性能未完成；CalibrationEligible=false。
- **准入OPEN / DIAGNOSTIC_ONLY**：完整诊断版可编辑，非诊断版新编辑入口关闭。不批准正式发行；不把数学PASS当相机/相纸色准。
