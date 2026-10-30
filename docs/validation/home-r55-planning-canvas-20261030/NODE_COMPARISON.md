# 策划画布快照v2 · 现有合成器复用门禁

目标不是新增调色/解码/滤镜。PlanningCanvasSnapshot旧WPF边界合成退役；新版本复用CanvasPixelRenderer已有逐层float预乘bilinear source-over。CanvasPixelRenderer原同尺寸PNG导出数学保持，改为可接受输出网格；尺寸相同 scaleX/Y=1。自由画布色板过去在界面重新按weight排序并减34高度，与导出不同，本批改为调用已有DrawObject/ObjectRaster内容合同，选框/文字标签留在UI层；不是第三套合成器。

## 既有证据覆盖审查
r53-canvas-tone-closure-20261028/REFERENCES和CANVAS_CONTRACT记录Fabric/Konva的文档/手势参考；它们不是本次alpha数值对照。已有画布导出160×120透明、双笔alpha128/192、自有副本/hash、ICC/方向与独立重开可继承回归，但旧策划Snapshot未参与。此次补Pillow+FFmpeg实际alpha参考、同尺寸旧DLL输出A/B、输出像素中心采样float64独立oracle、8方向、带sRGB profile8bit与原16bit不支持拒绝、固定版本/失败/重开。

## 输入/输出与位置
输入PNG/JPEG/TIFF按原ReadSource：嵌ICC经WIC至sRGB，方向1–8、straight BGRA8；源<=64MP/16384。保持当前encoded-sRGB合成（非线性光），白点/monitor不新增变换；非RAW/16f新引擎。文档固定PixelWidth/Height坐标，越界裁切，不包围所有对象。
中间预乘float32 bilinear、source-over，最后一次反预乘；WPF文字/软笔/色板栅格仍原ObjectRaster。缩小：输出整数尺寸round(W*s)/round(H*s)，输出中心映射原画板，逐图层双线性采样；不是先全尺寸成图再任意缩放，不保证与后者一致。Output PNG8 straight alpha、实际sRGB ICC；透明背景0 alpha，深灰衬底只UI Border。

## 两个固定成熟实现（仅外部执行，不复制/链接/分发）
1. Pillow 12.3.0 / python-pillow/Pillow @bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d；src/libImaging/AlphaComposite.c::ImagingAlphaComposite；RGBA8 source-over，含alpha0/1/2/16/64/128/192/254/255；MIT-CMU LICENSE。源SHA0063c136b8bb2be51e2fb76fac222a7ae96b438f3c09706834862d9c1ac62c93，LICENSE SHA15181e7363dca9aed78b79bebebc7fde7f1814b8bd311ea3b87ae8ccadfc185b。
2. FFmpeg9.0.2 @946fcce07b6dcd0331c8cc609192aeff5e1924f8；libavfilter/vf_overlay.c，overlay=format=rgb:alpha=straight。源SHA3e3c15dc3e207a5e990033d233912345d6826c92d955ba5d1205994000c0124a；LGPL2.1文本SHA246041b6ecf9bc32d718a62c57877c78b5eb397b6467e74ed7ae2626ab189c30。现机ffmpeg为GPL/version3 full build，整个二进制与源码仅N盘外部对照，不进入产品/本Git目录。不是通过独立进程消除许可义务；本轮未新引入该依赖。
上述固定源重新下载核对，沿已存在RECOVERED_REFERENCES固定commit，不以项目列表替代函数执行。工具/源码hash、运行参数、输入hash及完整数值由验证脚本输出。

## 数值结果与门槛
固定随机种子731，1024个RGBA像素，其中1011非全透明：产品对Pillow/FFmpeg可见RGBA最大1码、alpha最大0；预设上限1码，未放宽。全透明隐藏RGB不参与可见差异。混合画布420×300包含半透明、隐藏离线图、软笔、文字、色板、crop/rotate/flip、越界、自有图片；同尺寸快照与原导出完全一致（0码），与float64独立oracle最大1码/3分量；140×100直接预览与oracle最大0码。旧DLL混合导出像素hash E4C3A7DA87BAEE44D6A15318F706340479C77B02497DD7D438DA0C9BE01CDD11，PNG容器hash也相同；不把容器元数据差异当像素差异。

## 历史与保存版本
PV4/PV5/PV6的引擎源码未改；相关Core golden/版本化/无操作精度测试另列原始结果，不宣称全历史套件。PlanningCanvasRevision新增RenderVersion：缺省1保持已存PNG；新2带SamplingContract及尺寸，未知版本拒绝，不重渲染旧版本。.ptplan v1及依赖范围不变，画布工作区和源图仍未额外打包。

## 预算/错误/科学边界
预览边1400、1.96MP、1000层；工作估算 output32B/px + currentLayer32B/px <=256MiB；一条STA worker串行，渲染20s、捕获事务30s，逐行/笔/文件hash可取消；WIC原生解码不能强制抢占，超时后不发布迟到结果。大图层预算拒绝，不用缓存缩略或悄悄降导出精度。16000×4000空画板只分配1400×350输出float（7.84MB），不是1.024GB全板float。
范围：EXPLICIT_VERSION_APPROVED_FOR_DIAGNOSTIC_REUSE，CalibrationEligible=false。不能声称物理DPI/HDR/ACM、RAW色准、通用ICC或16bit全部支持；RGB48+嵌sRGB在现有WIC合同下NotSupported，保持导出与快照同拒绝，另记遗留。完整alpha/profile/方向/重开结果随最终发布绑定测试收据记录。
