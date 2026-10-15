# 阶段 2：同源 RAW 观测与边界

本轮只读使用已授权的 Canon TNAN8886.CR3、Fuji DSCF0347.RAF、Sony DSC09916.ARW，处理前/后 SHA256 全部吻合。原件不上传、不复制进 Git、不改 sidecar。所有新 TIFF 与参考依赖在独立 artifacts/validation-closure，不进入旧 Release。

固定 LibRaw 0.22.2-Release（tag 解引用 b93f6e45c194f5df9b02a43b1af9a54b4f41f33f）DLL/EXE 和 RawTherapee 5.11（AboutThisBuild 与 tag 解引用 db575c6690d30ce6428c0e1383bec8b8f1fb8d21）；完整组件 SHA/许可证/隔离环境见 02_REFERENCES.json。LibRaw LGPL2.1/CDDL1 双选及依赖，RT GPL3 与随附 notices；研究 CLI/C API 调用，不复制实现，不批准发布依赖。

## 实际可观察阶段
- LibRaw：open 容器几何/metadata；unpack 后真实 raw_image（Canon/Fuji）min/max/sum，black/white/WB/rgb_cam；process 后参数和最终 TIFF。Fuji 黑位不是零：cblack 6×6 pattern=1020，已通过 raw-identify 和结构直接确认。Canon unpack black=2048、white=16383。Sony 从 container black512/white32767，unpack 后变1024/17536，不能拿容器值充当解码后值。
- Sony 使用 sony_ycbcr_load_raw，unpack 是 color4_image，不是单平面 Bayer raw_image。因此单平面 sensor min/max 为 UNAVAILABLE，不将三通道 RGB16 反推为 sensor。真实 color4 缓冲存在已记下，但该缓冲的各通道语义及逐样本哈希仍 OPEN。
- 黑位处理后且矩阵前、WB后矩阵前的像素阶段：本轮两实现均 UNAVAILABLE；process 完成时保留的 raw_image 仍是原始缓冲，不冒充这些阶段。矩阵数值 LibRaw rgb_cam 已观察；精确选择来自内置表或嵌入 metadata 的运行时 provenance 尚未直接 hook，仍 OPEN。RawTherapee 的 source 路径/PP3/InputProfile=(camera) 可查，但内部 sensor、black、WB、matrix 实测输出此 CLI 不提供，均 UNAVAILABLE。
- RT 先保留 neutral 默认转换，再显式 profile 转换；实际展开 PP3 全文封存在 02_effective-pp3。WB=Camera、输入 camera matrix、working ProPhoto、output RTv4_sRGB relative/BPC false、零曝光、no crop、Bayer border0 / XTrans border0。LibRaw 第二组 no_auto_bright=1；相机WB、sRGB16、gamma .45/4.5、AHD3、user_flip0、FujiRotate1。RT AMaZE/XTrans3pass 和 LibRaw AHD、gamma/黑位缩放/矩阵及输出 profile 不等价，故最终RGB16统计是同阶段诊断，不是逐传感器精度或色准通过。

## 真实关闭：Fuji 5178/5184
原 0.21.1 输出7752×5184保留；本轮候选 container 阶段就为7752×5178，故不是最终保存或旋转造成。直接读到 FujiGeneration=4、CropModeMetadata=0、RAFData_ImageSizeTable=7752,5184。固定0.22.2 identify.cpp 2393–2400 对 raw_width7872 且 crop mode0，显式覆盖 top_margin6、left0、width7752、height5178；0.21.1同区没有该分支，保留尺寸表5184。FujiRotate=0/1 两次输出字节hash相同，排除该开关；没有裁图掩盖。此处是版本化 active-area 规则，不是可调显示高度。RT 默认额外削去2×7 XTrans边界成7738×5164，设置处理参数 Border=0 后自然为7752×5178；Canon Border4→0 自然从5488×3662变5496×3670，同样未做后裁图。

## Sony 新旧并列（非产品替换）
旧产品 LibRaw 0.21.1 在 OpenFile 报 RAW_NATIVE_FILE_UNSUPPORTED / FileUnsupported，原堆栈见继承 raw-inputs/DSC09916.ARW.json。候选0.22.2 open/unpack/process成功，输出4624×3080（禁旋转）；RT5.11成功，默认按EXIF方向为3080×4624。支持状态本轮提升仅适用于候选研究，不删除旧失败，不自动接入产品或Release。输出差异不可直接归成色差；Sony几何方向未作为精度比较强行对齐。

02_FINAL_COMPARISON / 02_NO_AUTO_COMPARISON保留同几何 Canon/Fuji 最终RGB16逐点与全帧max/MAE/RMSE，分类 CONFIGURATION_NOT_EQUIVALENT_NO_ACCURACY_PASS。消除自动提亮不是证明其余阶段等价；不能据此批准新像素实现。完整同阶段传感器参考/矩阵来源/预矩阵WB仍OPEN。CalibrationEligible=false。

失败保留：初次 C++ ABI 链接失败，随后只调用公开 C API 构建成功，不改DLL。02_BUILD_FAILED_CPP_ABI.json为原始失败。日期统一2026-10-15 Asia/Shanghai；01_START时区字段沿用了旧记录的Asia/Shanghai，是元数据标记错误，不代表此次用户本地时区；此处明确纠正。
