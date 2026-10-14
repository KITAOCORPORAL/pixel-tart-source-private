# B5 采集协议与宝丽来分层研究

CameraChartCaptureV1机器合同/validator要求场景ID、逐RAW SHA、机身/RAW模式/镜头、光源及实测SPD或测量证据、EV阶梯、色卡版本/参考文件hash/测量白点/2或10度、ROI排反光阴影、Training/Holdout、DeltaE条件、不确定度。元数据完整仍CalibrationEligible=false；需独立物理采集/留出验证，不凭表格标定。

推荐实际采集：固定机身/镜头/焦距/光圈、光源稳定并记录SPD；每场景EV -3/-2/-1/0/+1/+2；RAW模式独立分组；暗部/高光/颜色ROI剔反光、阴影、通道饱和；灰卡不等于色卡真值；训练与留出按场景隔离而非同图ROI混用。CIEDE2000声明参考PCS/白点适应/观测者/曝光对齐，报告样本分布、重复拍摄与测量不确定度，不只均值。

复用ClassicCgatsV1严格来源/版次/未知条件拒绝和Cube++00_0010许可baseline。X-Rite原件白点/observer及再分发未确定：不纳Git，不用合成D50伪造厂商条件。公开Cube++传感器白平衡角误差不是Lab真值，也不是A7M4 profile。

I-2分层：①数字相机RAW输入域/WB/matrix；②现代相纸批次/储存/曝光/显影温度时间；③扫描仪独立响应/关闭自动增强/ICC身份→PCS。缺成对物理样本/独立scanner表征/留出验证，本轮不拟合精确i-Type/600 LUT。T-79历史曲线不是现代相纸真值。现有InstantFilmProfiles保持CreativeApproximationV1和可复现seed/profile算法版本，不改创作参数。

PairedCaptureImporter继续明确Accepted元数据QC与CalibrationEligible=false；实拍缺失。本轮仅协议和验证工具，不宣称实际摄像准确度、宝丽来校准或全ICC。
