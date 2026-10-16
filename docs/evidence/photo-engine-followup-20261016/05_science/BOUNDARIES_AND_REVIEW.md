# 类型化照片边界与本机参考/盲评状态

## 类型合同（新增但不替代历史解释）

PhotoBoundaryContract定义SensorUnpacked/RenderedWorking/AdjustedWorking/DisplayDevicePreview/FileExport，transfer SensorCodes/Linear/EncodedSrgb/DeviceProfile，whitepoint CameraUnspecified/D65/PCS-D50/DeviceProfile，位深/量化/裁切、WB与matrix已烘焙、白点适应次数及组件/源身份。

Validate拒绝将WB/matrix处理后RGB称sensor、把LibRaw Linear ProPhoto D65标ICC D50、重复WB/白点适应、把显示器device编码写入文件导出。它是可单测类型边界，不代表已把全部现有调用点替换为强制类型系统，也不是新的曲线批准。

|真实路径|已确认数值边界|不可忽略的损失/责任|
|---|---|---|
|旧RAW|sensor解包→black→WB→X-Trans/Bayer→camera/output matrix→gamma/自动亮度→RGB16 sRGB|rendered而非sensor，原生uint16已裁切/量化；Fuji去马赛克后不稳定|
|PV4|三色Bayer→显式userWB一次→gamma1/1 ProPhoto原色D65 convention uint16→managed float|不是标准ProPhoto ICC D50；渲染前已裁切；肩部/gamut及sRGB编码保持历史值|
|PV5/PV6|已渲染sRGB frozen recipe→历史成像|禁止重复RAW白平衡/适应，未改golden|
|显示研究|encoded sRGB16→ICC PCS→device RGB16|只预览用途，原生WPF/DWM/跨屏未闭，产品未连接|
|导出|冻结工作图像/配方→sRGB行码值→TIFF16/PNG16或JPEG95-4448bit+精确ICC|不得复用monitor device buffer；JPEG单独有损容差，PNG/TIFF逐码基准|

## 实际测试

PhotoBoundary2项、DisplayPreview3项、ProfessionalRawPipeline及RawPrecision专项合计15/15通过。Sony实际非默认PV4保存重开+导出见03_sony，不能称新Sony候选接产品。源照片只读，衍生图像不进Git。

本机此环境PyOpenColorIO、colour模块未安装；原上批OCIO/Colour/OpenDRT/darktable子阶段参考身份与结果继续保留，不把它们记为本轮重新运行。当前无可用已核验native编译器，未擅装或以重写参考公式冒充实际执行。ACES/OpenDRT输入线性域、OETF/EOTF、曝光、gamut设置尚未完全配平；darktable仍仅历史log/spline子阶段，不是完整filmic。**本轮未完成新三引擎实拍比较**。

## 私有匿名A/B包

已在本机ignored artifacts/followup/05-private-blind-pack生成Sony/Canon/Fuji三张授权照片各两种现有产品曝光参数，共6张1200px匿名Scene01–03 A/B PNG及私有键。只比较当前生产旧曲线的0与+.5EV，不伪称新ACES/OpenDRT/darktable版本。Git仅数值摘要和文件hash，不包含图片或可公开下载附件。

记录通道0/1裁切计数，不用灰阶级数排名。未从文件名/参数推测“肤色/逆光/高ISO”等场景实际存在：类别待摄影师标注，不适用写N/A。主观盲评NOT_RUN；不能为用户代填偏好/优胜者。Fuji默认不稳定仍阻断，盲评图不作为默认稳定证明。

CalibrationEligible=false；不要求购买色卡，不宣称A7M4绝对色准或宝丽来相纸精确复刻。所有历史PV4/PV5/PV6曲线、golden不改。
