# 画布JPEG照片有界预览v3 · 节点/解码采样对照

2026-11-01 Asia/Shanghai（用户时间）；执行宿主原始时间+08保留。目标是1400边策划预览，不提高256MiB、不改变已有导出/RAW数学。

## v2障碍与分配分析
CanvasPixelRenderer.CheckLayerBudget以output×32+source×32拒绝24/33MP；ReadSource无edge时原WIC frame→ICC→orientation→BGRA CopyPixels+BitmapSource，RenderPixels再复制BGRA，可能多份完整源缓存。不能只删除门禁。旧Import/viewport有1024 edge，但那是查看代理，不可冒充新的固定版本来源。v3独立源验证、独立native JPEG帧，从同一已验证读lease建立解码器；没有GetThumbnail/GetPreview/API缓存读取。

## 策略与处理域
新CanvasJpegNativePreview：WIC IWICBitmapSourceTransform.GetClosestSize(request longest edge1024)→GetClosestPixelFormat(24BGR)→CopyPixels(closest size)。native JPEG支持1/2、1/4、1/8 IDCT缩小，拒绝未通过门禁的渐进/非baseline大JPEG；最大native解码3072边、4,194,304像素，再经原WIC ICC到sRGB、EXIF方向后生成straight BGRA8。非JPEG/small source继续原ReadSource及预算，有能力的中小PNG不回退。RGB48+ICC仍原NotSupported。输入<=64MP/16384，文件<=64MiB；源24MP输出1500×1000，33MP输出1752×1168，orientation6后1168×1752；不是原图细节，也不是固定一定1024输出。
小图source<=4,194,304完全保留v2路径；即使v3记录，已测小图/alpha/软笔/文本/色板与v2完全相同。大JPEG换采样所以必须RenderVersion3和CanvasJpegNative1024.ArtboardCenters.v3，PreviewParameters冻结阈值/native约束/ICC/EXIF/cache/output-center版本。旧v1/v2 PNG不重渲染，旧PNG导出数学不暗改，未知版本/参数拒绝。
颜色保持encoded-sRGB+D65文档，非线性光合成；WB/RAW/黑位/色域不新增。源ICC转换在native缩小后应用，可能不同于full-size色变换后缩小，明确版本，不称所有profile逐位等价。输出仍已有float预乘bilinear/source-over，最后一次反预乘和PNG sRGB ICC；透明、hidden、裁切/rotate/flip/层序保持。完全透明RGB不当可见误差。

## 工作预算与缓存
256MiB是本次渲染工作估算，不是整应用上限。output×32B/px + 最多32MiB8来源任务cache + native-sized temporary×32B/px <=256MiB；WIC native未申请整图CopyPixels，baseline JPEG未强制持有全图系数，渐进明确拒绝。不借关闭其他软件通过。
每次任务缓存32MiB/8个，FIFO淘汰，key=实际安全来源路径+内容hash；每次cache hit也重新打开同源校验lease。绝不使用外部缩略缓存、mtime当hash；任务结束/异常dispose全清，下一任务不继承旧代理。非图层栅格继续原预算减去cache预留；20s串行STA队列和30s事务/逐行取消保持，native CopyPixels不可抢占如实记录。连续输出PNG版本/CLR GC可使进程内存高于256MiB，需实测baseline/delta/peak/residual分别报告。

## 成熟参考（固定完整版本，不复制引擎）
1. Pillow12.3.0 @bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d：src/PIL/JpegImagePlugin.py::draft，src/libImaging/JpegDecode.c::ImagingJpegDecode中scale_num/scale_denom。实际Pillow .draft('RGB',native-size)执行与WIC同IDCT倍率；MIT-CMU LICENSE。固定source/license/tool SHA见REFERENCE_PINS与数值JSON。Pillow/libjpeg仅外部Python工具，不引入产品。
2. FFmpeg9.0.2 @946fcce07b6dcd0331c8cc609192aeff5e1924f8：libavcodec/mjpegdec.c::init_idct / lowres路径，ff_idctdsp_init；实际 -noautorotate -lowres 2 解码RGB24，不用全解码后scale冒充native IDCT对照。该本机full_build为GPL/version3，仅外部研究、绝不链接/分发；许可证/源码hash另存。不因独立进程自动解决许可。
API契约另外核验Microsoft WIC GetClosestSize/CopyPixels官方文档，JPEG native 8/4/2；COM ABI来自microsoft/win32metadata@64ed9177fb76eb244fc9051c74b8a93fdfe20873 wincodec.h。产品只依赖系统WindowsCodecs，不带第三方二进制。

## 初始数值与验收门槛
授权摄影来源既有TIFF派生24MP/33MP JPEG（明确resample衍生，不称相机原生24/33MP）；输入hash/profile/orientation/生成配置本地。Pillow同native尺寸对两份摄影图max0/mean0。独立FFmpeg reduced-IDCT默认舍入差max18/17、mean.856/.863 code，不能称逐位一致；预设边界max<=24、mean<=1.5、P95<=3，最终发布重复验证。该误差不是ICC科学校准；small output-v2门槛0，透明/裁切/条带对独立float64中心oracle门槛<=1，条带接缝按64行边界子集记录，不因native不同就放宽输出合成误差。
提供可分发6000×4000梯度/棋盘JPEG确定生成夹具；私人源和像素输出只留本机，Git只存身份/参数/指标，不存私照/代理/截图。已有alpha两个参考与旧export/golden也复跑，PV4/5/6源码不动。

## 许可与能力结论
本轮是版本化系统JPEG采样，不批准通用RAW/16bit/ICC或物理相纸色准。大PNG/渐进JPEG超当前原合同预算明确拒绝，不回退完整解码。研究GPL源码/程序只本地，产品依赖没有新增。CalibrationEligible=false，准入仅EXPLICIT_VERSION_DIAGNOSTIC，不是全量/实机/DPI/ACM/HDR验收。

## ICC不同已知结果（未放宽门槛）
Pillow LCMS独立转换与WIC现有ColorConvertedBitmap相差max3码，mean1.023/.660；已核对使用WIC真实目标ICC字节，并检查四种intent均相同，源sRGB built-in与目标sRGB IEC61966-2.1不同profile。预设1码未达到，原始两次数值失败保留；本轮不以放宽阈值或切换色彩数学强行PASS，记OBSERVED_NOT_EQUIVALENT_EXISTING_WIC_ICC。沿既有WIC合同重新编码native代理＋ICC＋EXIF的v2路径和v3逐像素比较作为“没有回退/漏掉颜色方向”的强回归，独立LCMS差异仍OPEN；不声称通用ICC科学门禁完成。
