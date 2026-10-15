# ICC轨迹解释边界补记

LCMS prefix数据来自cmsReadTag管线的逐级dup/eval，不包含整个transform连接层；最终结果另用原cmsCreateTransform观察。skcms A/CLUT与M/matrix分段是同profile副本上将后段设identity/禁用matrix获得的诊断，非运行时内部hook；因此其中段值用于本批identity-B样本定位，不宣称一般非线性B能同样精确分解。raw stdout为真实参考执行，未对最终输出乘二。

01_CELLS列的COMPARABLE只表示适用本批数值域/请求参数，不表示精度通过。v2绝对意图LCMS/Argyll的适配等价仍OPEN，v4 Argyll不支持；skcms所有绝对格不支持。负/>1 LUT点不处于规范定义的0..1表域。矩阵profile若无A2B，实际走TRC+矩阵，并不凭空声称选中了A2B。

节点整体门禁仍OPEN。半值局部来源已用具体分支和恒等表示实验解释；上游确认、一般合法输入准入、全部白点/intent语义与真实照片输出链未关闭。
