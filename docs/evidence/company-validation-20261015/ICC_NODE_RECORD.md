# ICC对照：节点模板映射

- 节点：独立研究input profile→PCS XYZ D50，不是生产节点修改。
- 处理链：设备RGB解码之后/工作空间编辑之前；RGB16/float→XYZ double/float。
- 原色/白点/TRC/范围/裁切：完整见ICC_NODE_COMPARISON；中性、原色、负/超范围点固定INPUT_POINTS；uint16明确clamp/round，float不做人为输出修正。
- 参考1：LCMS2.16，MIT，固定source commit和conda DLL hash，cmsCreateTransform/cmsDoTransform。
- 参考2：skcms固定commit及BSD3，portable独立exe，ParseWithA2BPriority/Transform。
- 参考3：Argyll3.5.0，icclib MIT/CLI AGPL3，独立process，v2支持/v4拒绝。
- 选择：隔离行为数值对照，不引产品库、不复制GPL/AGPL源码；未来优先评估LCMS但发布义务尚未批准。
- 合法样本：v2/v4 matrix线性/gamma2.2、LUT及v4显式M矩阵；生成命令/固定创建日期/profilehash，非研究/厂商原件。
- 意图/适配：LCMS4intents+adapt0/1、BPCoff、NOOPT/NOCACHE/NOWHITEFIX；skcms显式A2B优先级但absolute不可控制，标UNAVAILABLE；Argyll4intents v2。
- 差异：matrix近一致、gamma约1.3e-4、无M LUT约0.5、显式M relative约1.7e-5、其他意图约0.00349；先查PCS编码/A2B路径/白点/intent，未改参考或产品凑一致。
- 错误：截断拒绝、长度/越界LCMS宽容而skcms拒绝；产品仍须独立failclosed验证。
- 内存：参考进程48k点working/private；纯LCMS调用与进程端到端不可比较排名；native allocation ownership unavailable。
- PV4/5/6：72 golden在合并/发布Core真实Passed，色彩数学零变更。没有预览/保存/导出新ICC产品行为，所以该产品接入验证NOT_RUN。
- 校准：false，不要求色卡，真实相机绝对准确未证明。
- 准入：VERIFIED_RESEARCH_WITH_DIFFERENCES；不是EXPLICIT_VERSION_APPROVED。
