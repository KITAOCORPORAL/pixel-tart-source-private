# film_scale_and_expose内部定位能力（2026-11-02，Asia/Shanghai）

|事项|状态|边界|
|---|---|---|
|已有gray离线分析|COMPLETE_GPU0|first1ULP/max代表4ULP，normal/negative/nearzero分开；无新阈值|
|真实固定操作图|SOURCE_CONFIRMED|CPUfilmSensitivity/runtimeLUT/CAT16矩阵→actualfloatupload→tc_b→MitchellLUT→EV0affine|
|公司最小观测|EXECUTED_ONCE|只gray17×33whole，engine1/open1/render1，7readbacks；stream/其它case/BT0|
|被动观测不改final|EXACT0_RGBA16|与封存公司gray全通道0差；shader/数学source/flags保持|
|公司常数链|ACTUAL_BYTES_EXACT|CPUcast→uploadedstaging→device矩阵/LUT逐bit0差；不是仅同配置|
|公司曝光|ACTUAL_IDENTITY|scale1/offset0，upsample→exposure逐bit0差|
|公司位置收窄|UPSAMPLE_OUTPUT_BEFORE_EXPOSURE|差异承载存在于spk_lut2d_cubic完成输出，但不是已证明跨端首差kernel|
|家庭成对子观察|MISSING_NOT_RUN|实际uploadedmatrix/LUT、tc/b/upsample/exposure与mathmode需home同版本gray一次|
|跨端首因|NOT_ISOLATED|CPUsetup vsGPUtc_b vsLUTinterpolation仍不能区分；driver/hardware未分离|
|算法修复需求|NOT_ESTABLISHED|不改数学/精度/曲线/profile/clamp/driver，不放宽阈值|
|候选13|FROZEN_DEFAULT_OFF_NOT_ADMITTED|无候选调用、包络调整、第二锐化候选或产品入口|
|范围保持|RAW0_20MP0_SIX_PHOTOS0|不跑完整三case/BT64/curve/WB；manualpreference仍NOT_RUN|
|产品/许可|DISABLED_SEPARATE_GATES|src/UI/任务桥/default/PV456不改，研究不等于许可/准入|

精确公司结果：`E:/Kitao-Research/color-engine-scale-localization-20261102-v2/gray-once`。公开subset和完整local-only setup hash分账，勿对Git子集调用complete-localvalidator后误称材料齐全。下一步唯一最小任务：家庭真实一次同tapgray，然后常数bytes优先、子输出其次成对比较。
