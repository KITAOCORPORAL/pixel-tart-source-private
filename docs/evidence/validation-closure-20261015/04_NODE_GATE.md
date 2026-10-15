# Structure节点对照门禁记录

目标：仅性能/分配观测，无像素变更准入。链位置：legacy Develop；输入/输出非线性sRGB float，内部linear sRGB计算，orientation1，无alpha；RAW WB/矩阵已应用。固定Canon来源hash见04_*。

参考1本项目full oracle不计成熟独立实现；政策要求darktable+RawTherapee固定commit/具体节点/配置/逐阶段对照，尚未完成，故两参考/许可证集成审阅/预览保存重开导出均OPEN或NOT_RUN。不复用GPL代码。决定：仅诊断，无新节点实现，不批准像素变更。

数值：同源full/tilefloat字节相同。时间/内存/取消见04_COMPARISON。历史PV456待本轮测试核验；新行为没有自动迁移。CalibrationEligible=false，不涉及A7M4绝对色准或物理胶片精确复刻。

准入 OPEN；不得当EXPLICIT_VERSION_APPROVED。
