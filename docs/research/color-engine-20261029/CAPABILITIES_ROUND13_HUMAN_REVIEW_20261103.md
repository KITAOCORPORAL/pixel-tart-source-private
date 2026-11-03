# 第十三批人工评价当前能力与V1封存状态

日期：2026-11-03，Asia/Shanghai。权威 ROUND13_V1_CLOSURE_20261103.md 与 round13-v1-closure-20261103/SUMMARY.json。此前准备阶段能力表原样保留在下方历史区。

|事项|当前状态|证据边界|
|---|---|---|
|真实评价材料|RECEIVED_HASH_UNCHANGED|复用已核验原JSON，SHA256 48bd60ff065eaadbb49576700fbf3c5f89ce85ea121d6b50747a9cb8a5dfd397，2791bytes；未修改/上传|
|页面及原材料校验|EXISTING_HOME_PREPARATION_VERIFIED|此前家庭61文件完整校验通过且已生成私有页面，本次不重生成/重新验六图；版本/来源/固定ROI与答案已核验|
|真人评价完整性|COMPLETED_30_OF_30|六场景各五项非空；未补答、未代填|
|明确版本偏好|NONE_REPORTED|没有场景明确偏好X/Y；总体4无明显偏好、2无法评估|
|文字原因/观看条件|LIMITED|无文字原因；具体观看倍率/显示设备/校准条件记录不足，不补造|
|值得推荐的主观收益|NOT_ESTABLISHED|不推断两版均可接受、完全不可区分或候选更好|
|技术结果|PRESERVED_WITH_TRADEOFFS|第十三批冻结技术结果和细节/噪声等代价原样保留，不重算|
|独立评价|NOT_RUN_DEVELOPMENT_ONLY|一名评价者、六开发样本；不改称独立验证|
|匿名/私有数据|LOCAL_ONLY_NO_UNBLIND|页面、照片、key、原JSON留本地，不上传/不揭示映射|
|候选决策|V1_RESEARCH_CLOSED_AND_FROZEN|SharedIncrementEnvelopeUnsharpV1默认关闭、NOT_ADMITTED；封存不接入|
|本次执行|DOCUMENTATION_ONLY_ZERO_COMPUTE|build/GPU/RAW/20MP/六图渲染/BT/算法调整/产品打包0|
|停止条件|REACHED|不自动调参/建V2/增样本，未有明确新问题不追逐零值或跨GPU位一致；完成交付后停止|

---

## 历史：入口准备阶段能力（当时未收到评价，不是当前状态）

# 第十三批人工评价准备能力表

日期：2026-11-03，Asia/Shanghai。权威 ROUND13_HUMAN_REVIEW_PREPARATION_20261103.md。

|事项|当前状态|证据边界|
|---|---|---|
|数值定位阶段|STOP_NUMERICAL_EXPANSION|保留 CPU double 身份差异及 tc_b / 相同输入子集 upsample 差异；无实现错误证据，不改数学|
|旧 CPU validator/hash/localcopy|NOT_REPEATED|本轮只做新入口所需文件检查与合成自测|
|公司授权第十三批文件|NOT_FOUND_IN_SCOPED_CHECK|E:/Kitao-Research 顶层/明确路径和原 N: 路径；不扫描其他盘|
|精确必需清单|READY_61_EXPLICIT_FILES|6 输入+36 输出+12 PNG+6 原裁片+1 key；公司两布局均 61 缺项|
|家庭只读入口|IMPLEMENTED_TESTED_WITH_SYNTHETIC_FILES|原文件 SHA/长度/finite/PNG 像素和映射；无渲染，无覆盖|
|同尺度/100%/既有 ROI/匿名|IMPLEMENTED_SYNTHETIC_UI_SMOKE_PASS|同滚动位置，X/Y，原 PNG 字节；不是实际摄影观看|
|真实六图校验/入口生成|NOT_RUN_MISSING_PRIVATE_FILES|公司无文件，不能用合成 smoke 代替|
|真人偏好|NOT_RUN_AWAITING_HUMAN|不预填、不代答；空白 30 项 null；等待实际反馈|
|开发与独立验证|DEVELOPMENT_ONLY / INDEPENDENT_NOT_RUN|六图不改名独立验证集；不新增样本实验|
|新增研究设备/构建调用|ZERO|GPU compute/readback、RAW、20MP、自然图、BT、build 全 0；合成浏览器 UI 绘制另计|
|专项文件测试|PASS_8|临时非摄影生成夹具，仅验证入口/拒绝行为|
|产品/默认链/旧证据|UNCHANGED|src、UI、PV4/5/6、配方、旧工具与历史不改|
|候选/许可|DEFAULT_OFF_NOT_ADMITTED|未摄影批准；研究不等于产品接入/代码资源分发批准|
|下一步|WAIT_FOR_ACTUAL_HUMAN_REVIEW|不自动调参、第二候选、独立样本或产品工作|
