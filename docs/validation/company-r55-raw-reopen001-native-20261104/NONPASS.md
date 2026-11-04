# NONPASS — RAW-REOPEN-001

2026-11-04，Asia/Shanghai。**ROOT_CAUSE_OPEN / NOT_FIXED**。

- 一致性仍失败；Unpack有效样本相同，DcrawProcess有效RGB首差5316通道/max146，最终MemoryImage5311/max4628。
- 具体内部子阶段、native worker/scratch/算法语句未知，不宣称竞态或未初始化已证。
- 无产品改动/修复/重建；修复后App预览/保存重开/TIFF验证NOT_RUN。原失败断言不放宽。
- 旧报告OMP“继承6”措辞证据不足；本轮分时点记录null→既有binding默认6。保留旧报告和早期误报预检；该纠正不是fix。
- 研究线成熟源码契约对照已复用；两个成熟实现可复现数值对照仍NOT_RUN，未批准新像素行为。
- 严格两次新完整解码，未第三次。观测器/ABI/离线比较完成不是产品修复通过。
