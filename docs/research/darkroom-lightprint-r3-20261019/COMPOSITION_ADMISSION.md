# LightPrint1窗口组合：既有节点复用边界

本次不是新的颜色公式。旧定义LightPrint=null，直接走旧CandidateRenderSession逐像素分支；旧V1/V2/PV4/PV5/PV6不迁移，序列化null省略。新LightPrint1只对现有V3空间块产生的encoded sRGB float区域应用已有ColorStudioToolProcessor基础影调/白平衡/曲线，然后走原Paper3双线性取样、圆/方覆盖、Surface和Scan。其他节点拒绝，不将局部滤波缺halo路径暗开放。

域：D65 display sRGB float32 RGB，原始光片响应→已有encoded域工具（各自内部变换不改）→现有Paper3 coverage→输出编码（PNG/TIFF16/JPEG）。画面窗口外coverage=0，不应用工具；新WindowAdjustments写入冻结definition，undo/redo在同变体内保存该definition，保存重开/预览/条带出口同一解释。新框均项目原创procedural geometry，不含ZIP角色图，不开放任意第三方路径导入。

成熟独立alpha参考沿已固定来源：Pillow12.3.0 commit bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d src/libImaging/AlphaComposite.c ImagingAlphaComposite、MIT-CMU LICENSE；FFmpeg9.0.2 commit946fcce07b6dcd0331c8cc609192aeff5e1924f8 libavfilter/vf_overlay.c，当前GPL/version3构建仅外部验证，不复制/链接/分发。源码/许可证精确SHA沿base docs/research/linear-chain-immediate-20261017/RECOVERED_REFERENCES.json保留。节点数学准入沿已有Core工具与既有V3准入，不把本次窗口组合声称为新RawTherapee/darktable算法等价。

新代码组合单独测试：边框outside像素在窗口调色前后完全相同，中心像素不同；相同冻结定义预览/重开/三格式独立输出验证。透明coverage的独立Pillow与FFmpeg验证作为composition测试，不冒充相纸标定或通用ICC准入。若底层工具公式变化仍需新准入。本次不改变底层工具算法。
