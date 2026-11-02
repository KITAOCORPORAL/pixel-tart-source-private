# 家庭gray同版子阶段能力（2026-11-02，Asia/Shanghai）

|事项|状态|证据边界|
|---|---|---|
|家庭单次gray|EXECUTED_ONCE|17×33，engine1/open1/whole1，7被动reads，未重试|
|家庭完整capture|VALIDATED_COMPLETE|16numeric finite，17observations，真实home身份|
|观测不改家庭final|ALL_CHANNELS_EXACT0|原封存gray RGBA16逐通道0差，非新参考|
|家庭CPU→staging→device|ACTUAL_BITS_EXACT|matrix9/LUT110592逐bit0差|
|CPUdouble跨端|HASH_DIFFERENCE_CONFIRMED_ONLY|sensitivity/runtimeLUTdoubleSHA不同，完整公司数组缺失，count/max/坐标未知|
|floatLUT跨端|SEALED_HASH_IDENTITY_EQUAL|upload/device完整SHA一致；尚非两端完整文件直接字节比较|
|matrix/input跨端|DIRECT_BYTES_EXACT|CPU/upload/device小矩阵与输入实际读取比较0差|
|首个GPU子输出|TC_B_PAIRED_DIFFERENCE|tc487/1122、brightness82/561；同dispatch两输出，不能内部排序|
|upsample|DIFFERS_NOT_ONLY_PROPAGATION|1520/1683；同tc/b的158像素中156仍输出不同，LUT完整身份hash层|
|EV0曝光|ACTUAL_BIT_IDENTITY_BOTH_SIDES|各端upsample→exposure→原compound0差，不新增变化|
|公司完整capture|NOT_MIGRATED|仅publicsubset，明确拒绝completevalidator，未伪报fullpairedcompare|
|GPU硬件/驱动|NOT_ISOLATED|home5060Ti/WMI32.0.16.1088，company1650/572.70；NVML失败保留|
|算法修复需求|NOT_ESTABLISHED|数学/FMA/精度/优化/profile/clamp/driver未改|
|候选13|DEFAULT_OFF_NOT_ADMITTED|运行0，无第二锐化候选/产品入口|
|冻结边界|PRESERVED|RAW/20MP/natural6/stream/BT0，旧公司/家庭历史不改|
|完成条件|STOP_REACHED|本轮一次观测及可支持的首差边界已交付，不自动扩实验|

完整家庭输出：N:/pixart/ColorResearchHome-20261030/home-scale-localization-20261102/gray-once。公开17文件只是subset，完整LUT/sensitivity仅授权本地。报告HOME_FILM_SCALE_LOCALIZATION_20261102.md；证据home-scale-localization-20261102/；正常推送后给最终远端SHA。
