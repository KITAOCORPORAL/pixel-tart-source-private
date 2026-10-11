# 高光输入可行性边界调查

本批仅调查，不实现HDR/高光重建，不以压暗已裁切RGB宣称恢复细节。

## 实际接口与域

项目仍用LibRaw0.21.1 / Sdcb.LibRaw0.21.1.7 x64。阅读第六批固定官方tag源码 `libraw.h`、`libraw_types.h`、`dcraw_process.cpp`、`postprocessing_utils*.cpp`。本批只读probe在 `Unpack` 后、`DcrawProcess` 前读取包公开LibRawRawData结构，保存指针是否存在/尺寸/黑白电平和前512字节digest，不输出传感器数据、不持久化裸像素。

| 相机 | 更早信号 | 实测域信息 | 限制 |
|---|---|---|---|
| Sony ILCE-7M4 | raw_image存在；float/color3/color4不存在 | 7040×4688、RAW14bit、black512、white16383、Bayer | 原始边界含margin/坏点/黑电平，未完成传感器scene-linear化/去马赛克 |
| Canon EOS5D III | raw_image存在；float/color3/color4不存在 | 5920×3950、RAW14bit、black2043、white15488、Bayer | active成片5796×3870，margin必须明确，非后期RGB |

EARLY_SIGNAL_*.json为真实file只读观察。裸马赛克比RGB16更早，但传感器已饱和通道仍不能保证恢复。数据指针由RawContext拥有，close/dispose后不可访问；未来必须拷贝有界strip或在native生命周期内处理，不能缓存悬空pointer。

## 候选路径与依赖

1. LibRaw C API `libraw_raw2image`/`libraw_subtract_black`、rawdata.raw_image/float_image及4通道image能分阶段访问；当前wrapper部分未暴露高级C++中间callbacks。C++接口包含pre_subtractblack/pre_scalecolors/pre_interpolate/pre_converttorgb callbacks，需要最小本地native shim和明确ABI/打包/许可证验证，不能用推测vtable调用。
2. `highlight=1`（unclip）、2（blend）和3–9重建模式在LibRaw的后处理中有现成行为，但uint16黑扣除、scale_colors和convert_to_rgb仍可能早裁切；必须调查每个阶段的归一及范围。不能仅换flag就保证保留浮点负值或完整scene headroom。
3. 原始马赛克线性化路线须核对raw_pitch/尺寸/margin/CFA相位、channel cblack和黑模式、white/per-channel saturation、坏点、厂商compression/线性化、WB、相机矩阵。之后浮点去马赛克/重建是独立新管线契约，不能悄悄替换既有PV4 RGB16 master。
4. float_image只用于某些native float格式/解码器，不因为字段存在就宣称现用Sony/Canon是float输入。本批明确两者实际为ushort mosaic。

## 后续样本需求

同机同场景标定曝光阶梯、近clip灰卡/色卡、单通道饱和与亮彩色灯、逆光肤色；亮部欠曝参考与正常曝配对，用于验证局部结构是否确实恢复。必须按每相机同时记录sensor saturation和RGB转换剪切。当前火焰人像只能证明现有输出饱和，不代替标定。未知机/已WB小RAW专项NOT_RUN。

状态：INTERFACE_INVENTORY=PASS、REAL_EARLY_SIGNAL_READ=PASS（2相机）、FLOAT_HEADROOM_OR_RECONSTRUCTION=NOT_RUN、HDR_IMPLEMENTATION=OUT_OF_BATCH。LibRaw LGPL/CDDL和wrapper MIT许可证仍沿既有分发；未来shim/new dependency另审查。不重复应用WB、不把sRGB inverse当sensor恢复，不推断Capture One私有顺序。
