# 宽域影调本轮具体结论
- 成熟参考不是产品公式替身：固定darktable get_contrast和RawTherapee CurveFactory原函数实际native执行，51+42点，域/参数/输出转换合同和差异TONE_NATIVE_REFERENCES.json / tone输出矩阵保存。RawTherapee部分不适用输入返回NaN原样null记录。
- V1高光/阴影是输入Y加权同RGB增益，不是前端高光恢复。单项单调，但负对比+正阴影等极端组合灰阶反转；因此新增WideToolVersion2，contrast/logEV系数.25、shadow/highlight系数.8，经27极端组合×3000灰阶单调；旧V1原结果保持且继续按V1编辑，新工具/显式另建新版变体使用V2。
- V1相对白平衡正temperature语义会冷偏；V2翻转xy源白偏移方向，正数变暖、负数变冷，正tint洋红方向测试。仍非相机Kelvin/WB。
- 快速预览原误差预算不变：强WB/tone、多节点、细/硬蒙版、旋转/拉直和高频边界保守回退精细。V1历史不变，RawWide继续defaultoff，X-Trans/内存门禁不改。
- 本轮对照是成熟tone-stage函数真实执行，不等完整相机引擎/ICC/物理校准；诊断/科学准入仍OPEN，不能仅删除开关。完整输出ICC另测，TIFF码差0不称色准。
