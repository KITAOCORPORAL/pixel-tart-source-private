# 第十二批能力状态：两个既有问题的原因定位

2026-11-01（Europe/Helsinki），不代替第十批冻结合同/第十一批门禁。

|项目|状态|已确认边界|
|---|---|---|
|第十批20MP链|FROZEN_UNCHANGED|未重跑；freeze/output/receipt/历史失败不动|
|第十一批诊断工具|COMPLETE_UNCHANGED|原工具不改，仅复用已保存数据验证|
|NEX5R零通道分类|CAUSE_CONFIRMED|11,562固定像素，22,322负值裁切、0精确零、4正小值量化零；全图量化重现0差|
|NEX5R负值来源|UNSHARP_UNDERSHOOT_CONFIRMED|sigma=.7px/amount=.7，半径2反射边界；CPU局部解析与实际负号位置全一致，数值非bit-exact|
|NEX5R画面对应|OBSERVED_NOT_AESTHETIC_APPROVAL|固定花卉ROI1,796个选中像素、640个RGB全黑；暗侧边缘对应，不自动判bug|
|R6曲线合并|PROFILE_LOW_ENDPOINT_CONFIRMED|固定ROI19,567零密度分量全部落在log曝光≤−3；242个不同曝光三元组变同一零密度|
|R6纸张/扫描底色|MODEL_RESPONSE_CONFIRMED|固定99参数+隐藏默认；非零纸张XYZ与关闭黑白校正解释底色；CPU解析误差保留|
|R6全局责任归因|BOUNDED_NOT_EXCLUSIVE|旧master端点覆盖54,321/75,000 ROI像素；profile低端/范围内非单调与纸张响应均参与，不全归于一环|
|物理准确性/偏好|NOT_RUN|无色卡/实物配对，不把0.132→1设目标|
|新观测DLL/GPU/RAW|NOT_NEEDED_ZERO_CALLS|新建CPU setup-only辅助1次，无新像素render或解码|
|像素修复候选|NOT_CREATED / NOT_ADMITTED|两个成熟实现本轮阅读/数值对照未完成；只给后续候选假设|
|公司公开小诊断|NOT_RUN_HOME_MACHINE|WMI实机RTX5060Ti；不伪造公司执行|
|产品接入/许可|DISABLED / OPEN|原GPL/CCBYSA/厂家数据边界不变；私照/ROI/邻域/外部源码二进制只本地|

六张照片始终为开发集；无新增stock、照片、相机、功能或C1拟合。因果报告HOME_ROUND12_CAUSE_REPORT.md，数值home-round12/；当前Git交接由remote-receipt.json（实际生成后）与最终消息读回为准。
