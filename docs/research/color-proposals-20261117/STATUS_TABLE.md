# 色彩研究状态总表

|项目|已证明|被反证/不能推断|未知|已入当前产品源码/执行层|
|---|---|---|---|---|
|已显影曝光追踪|研究阶梯与ROI新损失在最终逐通道clip；静态核对当前公式未改|所有亮部发硬均由同一RAW阶段造成|源传感器是否已饱和、不同真实ICC/光照|既有曝光函数在；研究observer/候选不在产品|
|BasicTone与专业RAW显示|各自max归一化/中性轴压缩/float平台机制有研究证据|把它们当同一种clip或色相突变|所有真实组合配方审美/全显示链|既有节点在；本轮未动数学|
|MaxChannelShoulderV1|固定579行、EV0曲线、18组；白1→.875；有限新裁切减少|EV0恒等、贴顶消失=恢复、线性比例=感知无代价|白衣/金属/脸部/光照与相对既有工具净收益|研究CLI/tests；无Core/UI/recipe接纳；最终EXE未验收|
|XTransStableInputSeparateRgbV1|同Fuji两独立native进程P6全RGB逐位一致|跨相机稳定、正确性真值、原输出字节恢复|带外max870影响、更多相机/域、parent/缓存/重开/TIFF|a24诊断分支存在但未入主集成；旧一次导入后parent2GiB STOP；当前433未借验收|
|SharedIncrementEnvelopeUnsharpV1|6f1封存；人工30项无明确偏好|两版都可接受/等价/候选更好|独立验证/明确摄影净收益|不接入，封存；六图/GPU/参数/V2新增0|
|TIFF/sRGB/隐藏预览内存|e818已合入且fdf所测普通TIFF保存重开/像素/ICC通过|等于色彩算法进展或自动通过XTrans|433新关闭TIFF完整普通EXE仍按产品线PARTIAL|既有成果保留，不重跑；LC-B批准不含色彩|

源码/证据身份：高光review1bfdfbe9、实现b29bff91；XTrans native53e2355d、后继STOP820b4f0d、隔离适配a24fac65；锐化封存6f1ae27a；主线文档f665/功能e818；后继生命周期文档9dd7/功能433。完整SHA、祖先关系、默认与候选DLL hash见BASELINE_AND_ADMISSION、COMMIT_RELATIONSHIPS、NATIVE_LIBRARY_READBACK。

研究候选数值通过、源码定义存在、绑定产品入口、最终普通EXE通过是四个不同状态；不能靠同名DLL、环境变量、局部PNG或同树推定后两项。本轮属于研究与方案准备，新的算法实现/产品接入均为未批准。
