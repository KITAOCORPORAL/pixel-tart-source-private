# 照片数据类型与空间合同

SensorCFA:真实unpack raw_image uint16/pitch/rawsize/activeorigin，未扣黑/未WB；Sonycolor4不赋Bayer语义。PostBlack/CameraLinearRGB/WBPreMatrix只在真实可观测阶段记录，本轮UNAVAILABLE，不从RGB16反推。候选LibRaw gamma1/1输出RGB16ProPhoto是已WB/matrix/demosaic且量化的渲染图，不是rawsensor值。

PhotoSrgbLinear1首批subset是固定标准/RT编码sRGB ICC的JPEG8/单图TIFF16RGB，orientation1、无alpha；实际WIC/LCMS旧工具独立，不暗改。LCMS相对intent1/BPCoff/adaptation1将编码sRGB/D65→linear-sRGB/D65（profilePCSXYZD50），workfloat32 extendedfinite，冻结EV[-3,0]独立版本recipe，输出LCMSfloat保留供诊断，命名整数sink才饱和uint16。displayPNG8/JPEG8从同输出码值量化，profile嵌入/回读exact，lossyJPEG单列。无通用epsilon或额外EV/headroom，无V1/V2/PV456迁移。

OCIO/Colour仅独立命名链参考：linear-sRGBD65→Bradford→ACEScg/AP1D60，derivedprimaries矩阵一致误差4.64e-8；Colour默认四位表矩阵差6.93e-5单列不掩盖。ACES不是相机标定、不是照片源ICC替代，使用AP1只为工程参考。转换域/白点必须命名，不串接两个不等价默认矩阵。

Nativeexact2.16依赖精确来源/MIT/WindowscleanVM/产品入口门禁未全闭，工具模式为真实可审候选，NOT_APPROVED_FOR_DISTRIBUTION。RawSpeedWinbinaryNOT_RUN；OpenDRT GPLreferenceonly，任何代码复用需单独审查。ColorStudio照片专用，不扩展视频剪辑、视频Log、FFmpeg或BRAW。CalibrationEligible=false。
