# 第八批：处理域与版本合同

日期：2026-10-12。所有即时相纸为 **CreativeApproximationV1**，不是实测标定。

## 输入与输出

- RAW：既有 PV4 LibRaw 线性 ProPhoto D65 master。白平衡、相机矩阵只在原解码流程应用一次；曝光与 LuminanceShoulderNeutralGamutV1 保持历史数学。之后才进入 display-referred sRGB float 即时胶片层。
- JPEG：显式创建 PV5 / RenderedSrgbFloatV1 / RenderedSrgbIdentityV1 / WhiteBalance=Rendered 新版本。WIC 解码完整原尺寸，嵌入 ICC 转 sRGB 一次；无标记按 sRGB 假设。RAW 通道增益与曝光非默认值一律拒绝。不是将8-bit预览放大：输入确实是JPEG原生8-bit，浮点处理不宣称恢复丢失位深。
- 输出域：sRGB。新层按已旋转的原图归一化坐标处理，纸框扩大输出尺寸、方向归一化为1。TIFF/PNG为RGB16（JPEG源仍只有原8-bit源信息），JPEG为RGB8/WIC95/444。有损门限保持MAE≤5、RMSE≤15，未降低。
- 快速导出逐行使用同一冻结定义；Publishing先生成相同全尺寸TIFF16中间结果，传递完整definition、seed及输入合同，再应用发布缩放/水印。Publishing PNG16和非sRGB/不支持格式继续拒绝。

## 兼容与迁移

RAW基管线仍PV4，新即时层独立 AlgorithmVersion=1、ProfileVersion=1；不修改PV1/2/3/4的已有定义与PixelTartFilm SpatialVersion1/2。InstantFilm=null序列化时不添加属性，避免改变旧待恢复导出的fingerprint。Enabled=false像素身份测试覆盖EXIF1–8。新胶片生成创建新版本，来源版本保留；关闭新效果创建无即时层的基础版本。JPEG不静默改旧调色栈，用户显式生成后进入PV5，旧版本可切回。

存储包含 film id、ChemicalGeneration、profile/algorithm版本、Amount、纸框几何、独立纸色、显影温度、手动缺陷参数、骰子状态与snapshot。相机外观/动画只属展示，不影响处理定义或最终像素。会话内同一定义经过JSON两次独立反序列化后，按完整序列化值比较，不按Geometry集合引用比较。

## 空间与预览

新层为点运算+解析归一化缺陷，邻域0、halo0。不申请旧胶片的多个全尺寸临时数组。细节块包含相纸坐标；完整图与任意tile同坐标完全一致。不同输出尺寸使用同一归一化场；缩小预览是full显示线性sRGB精确box面积平均，而非逐点值相等。

HQ连续行带复用前一源行，保持每个输出像素加权累积顺序及float参照。两项缓存key含源SHA、decode key、尺寸、EXIF、完整definition、edge、质量版本。HeavyWork互斥后再次检查缓存，复用已完成相同请求；不共享带不同取消生命周期的进行中Task。工作区revision/target/version/definition检查阻止旧结果发布。

## 不包含的工作

不重写HDR、高光恢复、全ICC、GPU、完整Word、手机APP、浏览器扩展；保留在后续路线。未实现Green/Go/8×10/Time-Zero/Spectra/55/665/669只有研究注册，不作为可用UI预设。

