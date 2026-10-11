# RAW 生产管线实现

## 版本决策

PV1/PV2旧输出、旧LibRawDecoder/RawMatchTiff16ProductPipeline继续存在，未更改其数学实现。PV3已有实验 ACEScg / CameraLinear / DisplayEncoded 契约，旧调整栈 Normalize明确拒绝3；不能重新定义成LibRaw ProPhoto D65的已裁切RGB16输入，因此独立生产定义使用 **ProcessingVersion=4**。这是RAW处理版本，不等于既有 MatchV4Beta仿色引擎。

## 唯一处理定义

ProfessionalRawDefinition保存：PV4、固定输入配置 `LibRaw021LinearProPhotoD65V1`、Camera/CameraGains、RGB相对倍率、ExposureEv、固定显示变换 `LuminanceShoulderNeutralGamutV1`。Validate拒绝未知配置或版本、非有限数、越界。DecodeKey由输入配置和有效WB组成，曝光不触发重新解码。

ProfessionalRawContract保存源hash、实际解码版本、相机、黑白电平、输入/输出参数JSON、相机WB/应用WB/规范化WB、真实rgb_cam、矩阵来源说明、域/位深/gamma/高光策略、饱和/零计数。首期限制三色Bayer；缺配置、已WB、小RAW、X-Trans/Foveon、损坏/不支持文件明确失败，无成功标记回退。

```text
Studio“新 RAW 处理版本”
 -> ProfessionalRawDecoder: Open / explicit Unpack / validate camera / explicit DcrawProcess
 -> RGB16 LibRaw linear ProPhoto D65（已扣黑、WB、矩阵、量化和裁切）
 -> ProfessionalRawMaster(float32，readonly持有)
 -> ProfessionalRawPipeline.Render(同一个definition)
 -> exposure 2^EV
 -> inverse LibRaw ProPhoto D65 output matrix -> linear sRGB D65
 -> luminance shoulder + neutral-axis gamut compression
 -> sRGB encoding
 -> preview RGB8 / export TIFF16或既有JPEG/PNG编码
```

不是从sRGB逆编码恢复RAW；不重复WB或相机矩阵。显示变换是公开项目公式，不是复刻Capture One。

## 产品、版本、会话、任务

- 实际WPF header提供新入口；先解码校验成功，再捕获旧版本并创建独立ID。失败保留旧ID和预览。新版本记录 OriginVersionId/OriginIdentity，不迁移旧调整。
- 右栏RAW参数为相机WB、相机增益和EV；没有Kelvin/Tint。旧HDR/仿色/胶片/空间工具隐藏，参考操作栏禁用，旧RenderAsync在IndependentRawMode直接拒绝；旧同步/粘贴/批量历史不能写PV4。
- 会话v1增加可选字段，版本仓储继续CAS规则；活动RAW定义必须与选中版本一致，RAW和旧Look/Stack/Film不能混合。加载永久PV4目录先选择专业解码，不调用旧解码器。
- 参数提交先获取所需新master，解码失败保留上一有效定义。创建/应用前后检查活动照片、版本和activation revision；取消、切换、dispose/晚到输出不能改当前版本。缩略任务取消且发布时再次检查RAW状态。
- 保存、重开、版本切换、批量快速导出和Publishing中间输出都读同一RAW定义。导出冻结版本/参数；TIFF16原子写入及现有受管理恢复继续使用。导出sidecar保存完整RawDefinition/RawInput和参数fingerprint，旧fingerprint序列化不加null字段，保持既有恢复身份。
- 源文件只读并验证hash；真实样本仅副本，未提交照片/生产数据库/凭证/二进制。

## 浮点规则与显示边界

输入RGB16已在[0,1]，不可能保留此前被LibRaw裁掉的负值/高光。此后矩阵和曝光采用double计算/float32缓冲，不钳制负值或>1；非有限值明确拒绝。仅显示输出映射和编码在[0,1]内。

转换线性sRGB后 Y=.2126R+.7152G+.0722B；Y<=0输出黑；Y<=.75恒等，Y>.75用 `.75+.25*(1-exp(-(Y-.75)/.25))`；先同倍率保RGB关系，再沿中性轴压缩色度至立方体[0,1]，最后标准sRGB OETF。最后Clamp仅数值边界保护，不是逐通道硬裁切色调算法。负亮度显示为黑、极亮近白、饱和色降低色度均为有意策略，待视觉验收。

## 本批限制

预览最大1600边/nearest中心采样，不是100%细节预览；无抗混叠滤波。无自定义工作空间/显示或输出ICC转换、无高光重建、无GPU。旧工具未在8bit兼容转换后继续宣称宽色域。相机矩阵质量/亮度归一和肤色必须继续用授权图像人工评估。
