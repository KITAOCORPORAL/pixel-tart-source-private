# 第十三批 V1 当前最终状态（2026-11-03，Asia/Shanghai）

**V1_RESEARCH_CLOSED_AND_FROZEN / DEFAULT_OFF / NOT_ADMITTED。** 技术结果保留；真实人工评价已收到并核验，30/30项完整，没有明确X/Y偏好，总体4无明显偏好/2无法评估，无文字原因、观看条件不足，未建立值得推荐的主观收益。独立验证仍NOT_RUN_DEVELOPMENT_ONLY。封存不接入，不自动调参、V2、新样本或重跑计算。

权威ROUND13_V1_CLOSURE_20261103.md、当前CAPABILITIES_ROUND13_HUMAN_REVIEW_20261103.md及round13-v1-closure-20261103/SUMMARY.json。下方为原技术批次历史能力，含当时的NOT_RUN/等待评价，不代表今天仍未收到答案。

---

## 历史技术能力（原文保留）

# 第十三批能力状态：唯一受约束锐化候选

2026-11-01（Europe/Helsinki），独立研究，不注册产品，不替代第十批冻结合同。

|能力|状态|边界|
|---|---|---|
|固定成熟实现阅读|COMPLETE_FIXED_FUNCTIONS|darktable指定process/threshold/border与RT sharpening/halo及真实Gaussian/Threshold/mask，文件/许可证hash已记|
|固定实现数值执行|PASS_45_LOCAL_CPU_CALLS|9夹具×5配置，原函数体真实编译；Lab域/RT缩放/标量/边界显式，不是完整app/线性RGB oracle|
|唯一候选|SharedIncrementEnvelopeUnsharpV1_DEFAULT_OFF|独立线性RGB共享增量包络，.7sigma/.7amount、5×5、reflect、halo2，仅ratio1|
|原PortraA结果保持|EXACT_6_SAVED_STAGES|legacy scan_finish/encoded/RGBA16与旧保存结果全通道0差；不改变默认|
|候选whole/stream|EXACT_FLOAT_AND_RGBA16|合成whole/r7；自然6图whole/r17/r37全通道0差，无放宽|
|暗侧下冲|REDUCED_ON_DEVELOPMENT|NEX5R负分量22324→0、最终anyzero11563→0；局部包络无新越界|
|细节/噪声取舍|REGRESSION_EXPOSED|阶跃/脉冲等于输入，正弦锐化增益削弱；噪声std仍1.248×；自然梯度增量保留44.7%–67.0%|
|色相/肤色|MEASURED_NOT_APPROVED|输出Lab响应差非色准；小肤色P95约.592°/max9.853°，无肤色识别或受控评价|
|研究保存读回|PASS_FLOAT_NPY_TIFF16|同字节读回通过；无ICC声明、无产品项目/显示链准入|
|预览尺度|RATIO1_ONLY|800/1600缩放preview未执行/未准入；halo精确不替代尺度验证|
|R6范围说明|DOCUMENTED_NO_PIXEL_CHANGE|只整理[-3,4]低端比例和hidden defaults，不自动曝光/外推/减黑|
|公司诊断|NOT_RUN_HOME_DEVICE|实机5060Ti；未建新诊断框架|
|产品接纳/独立偏好|NOT_ADMITTED / NOT_RUN|6图始终开发集，人工偏好/独立集/取消并发/跨GPU/许可OPEN|
|整体结论|KEEP_FROZEN_RESEARCH_ONLY|方向可保留，但V1过保守，不称效果已更好，不推荐直接产品接入|

执行：CPU参考45次；GPU阶段36进程/4088dispatch；RAW0、上游film/paper管线0、20MP0、新stock/图/相机0、候选1。15项专项测试通过。权威HOME_ROUND13_UNSHARP_REPORT.md、UNSHARP_NODE_COMPARISON_ROUND13.md、home-round13/；源/自然像素本地round13-unsharp-v1。
