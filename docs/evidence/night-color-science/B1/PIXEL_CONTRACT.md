# B1 真实路径与冻结合同 v1

B树从远端最新13b0efe689c0114f6f76b70c59a1059cabeddbef新建。A继承更晚本地ec20c77（祖先是13b0efe），未把A任何策划修改混入B。旧候选/公开数据分支的整合集成代码已在基线；不重做其算法。原保存/图像和UI不改。

|路径|输入至输出数值合同|裁切/未知|
|---|---|---|
|PV4 RAW|ProfessionalRawDecoder:容器→0.21.1 unpack（float转int选项）→元数据black/cblack/max→显式user_mul（auto/camera flags关闭）→AHD三色Bayer→相机内置/嵌入RgbCam→LibRaw ProPhoto原色D65、gamma1/1、16bit→float /65535→EV线性乘2^EV→LibRaw prophoto_rgb逆矩阵→线性sRGB D65→亮度肩/中性轴色域压缩→一次sRGB OETF→16bit sRGB输出|LibRaw black/scale/convert阶段有uint16裁切；本机现接口只能观察解码后，不能声称观察到了其矩阵前的负值。不得当ICC ProPhoto D50|
|PV5 JPEG|InstantFilmJpegInput:已渲染sRGB float+明确profile准入→可冻结当前编辑→RenderedSrgbIdentityV1→创作胶片/纸框（明确版本）→sRGB输出|已烘焙WB/传递函数，不再乘相机WB；8bit来源不获得真实16bit信息|
|PV6旧RAW|LibRawDecoder→旧sRGB RGB16（已有WB与默认高光/gamma）→LegacyRawInstantInput冻结旧stack/look/film及原几何→LegacyRawEditsIdentityV1→sRGB输出|旧RAW是编码显示域，不是PV4场景线性；无高光纹理恢复保证|
|HQ预览|ProfessionalRawPipeline RenderPreview:同一full显示变换后逐像素decode sRGB，box footprint线性平均，再encode|不能先平均RAW线性再tone-map，否则不同算法|
|TIFF16/PNG16|ProfessionalRawExport流式行输出同一冻结master/recipe，量化0–65535，StudioQuickExport.SrgbProfileBytes写ICC，输出重新读取验尺寸/方向/profile|只明确sRGB输出；不宣称全ICC/设备表征|

PixelPipelineDiagnostics合同v1记录输入/解码器/矩阵来源/白点/传递函数/已WB/输出profileSHA/recipe版本/原件SHA与各边界per-channel negative、>1、zero、>=1/nonfinite。ProbeTransform仅合成受控中间矩阵，不伪装LibRaw内部观测。资料缺原件hash/profilehash拒绝。

冻结72组PV456 golden（8 EXIF×3 PV×3 film状态）来自batch12固定JSON及LegacyPixelCorpus；保留现有灰阶/高饱和/HQ/1:1/全尺寸/16bit编码profile验证。数学样本仅所覆盖输入，CalibrationEligible=false，不是A7M4准确标定、HDR恢复或相纸真值。
