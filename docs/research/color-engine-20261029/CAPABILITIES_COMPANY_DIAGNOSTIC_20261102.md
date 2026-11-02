# 公司公开合成小包：执行与跨设备比较能力（2026-11-02，Asia/Shanghai）

|能力|本次状态|边界|
|---|---|---|
|正确接续|93b553d1家庭已审查基线；无增量|公司旧树0f3204c0 clean/unpushed0；新诊断分支，未重置/覆盖旧树|
|当前公司机器|ACTUAL_GTX1650_572.70|Windows10build19045，i5-12400F；不借用家庭身份|
|固定构建|PRE_RUN_FROZEN|新外部main/trace构建，源码/命令/工具/runtime/DLL/resource全记录；完整engine资源和二进制不进Git|
|固定输入/参数|EXACT_PUBLIC_3_CASES|既有gray-boundaries、saturated-colors、edge-small；不是照片/相机色准|
|公司whole/stream|EXACT_ZERO_REQUIRED_OBSERVED|rows1/7/17/64，每case逐通道0差，不放宽|
|被动trace不改输出|EXACT_ZERO_REQUIRED_OBSERVED|whole-traced与stream-r7-traced均对原whole0差|
|完整阶段与来源|INDEPENDENTLY_VALIDATED|3×(12whole+36stream)144文件，shape/order/domain/length/hash/finite与build前身份匹配|
|跨设备量化|MEASURED_NO_PASS_THRESHOLD|whole变化17/31/31通道，max1U16；12stream组合相同，不以max1自动判通过或可见缺陷|
|首个分歧阶段|film_scale_and_expose|whole与r7stream都从第0stage不同；复合阶段尚未拆具体operator|
|混杂控制|MATCHED_AND_REMAINING_LISTED|工具字节/flags/资源/SPIRV/参数相同；授权根字面量、DLL/路径、Windows/Python/Pillow、GPU/driver不同；未隔离因果|
|第十批20MP/自然开发图|NOT_RUN_THIS_ROUND|旧证据保持，不重解码RAW、不重跑20MP/六张开发图|
|第十三批候选|FROZEN_DEFAULT_OFF_NOT_ADMITTED|SharedIncrementEnvelopeUnsharpV1本轮0次，没有候选复做/包络调参/第二候选|
|私人A/B人工偏好|NOT_RUN|家庭私人文件未迁移，不能用公司缺图重渲染六图或找照片替代|
|产品入口/接纳|DISABLED_NOT_REGISTERED|src/UI/项目模型/任务桥/default/PV4/5/6无修改|
|代码/资源许可|SEPARATE_OPEN_GATES|LightTable非商业条款不替代Spektra GPL/CC BY-SA/资料权利；外部进程/DLL不自动解决|

本次成功GPU输出API调用22（21Portra渲染＋1既有BTprobe）；底层dispatch/初始化探针未单独计数。3自然入口synthetic拒绝GPU0；RAW0/full20MP0/自然6图0/候选0。公司包一次run、自动重试0，所有验证/比较仅CPU读取。

旧home-round10/11/12/13中的公司NOT_RUN和旧网络/远端收据全部保留，含义是旧时点；本表只更新当前公开小包状态，不重写历史。完整量化分位数/首差/阶段变化见company-diagnostic-20261102/home-company-comparison.json。
