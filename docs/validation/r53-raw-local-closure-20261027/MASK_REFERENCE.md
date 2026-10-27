# 蒙版参考与V1语义
- 复用darktable5.2.1@be9171cf8bb2feab8bd115efd9b96a4ce7103039，src/develop/masks/brush.c::_brush_falloff：线性hardness/density径向羽化，MAX合并同笔栅格。实际编译抽取函数，15组参数/100样本；离散邻像素填补/整数solid导致连续公式差异，原始max0.040000029保留，非声称完全一致。
- GIMP3.0.6@a77e8fd05d3d08a58650531a3700100f6a3216b7，app/paint/gimppaintcore-loops.cc constant/stipple opacity累积，app/operations/layer-modes/gimpoperationerase.c擦除。参考标量表达式实际执行：可等价参数子集192组；全密度stipple和擦除max0。不是完整GIMP画笔引擎/pressure执行。
- 两者GPL3仅本地隔离研究，无源码/二进制复制产品。沿用独立LocalMaskV1折线最短距离和线性羽化。V1每笔一次：a=coverage*Flow；增加w+=max(0,Density-w)*a，擦除w*=1-a；反相1-w。同笔回折/停留不反复喷涂，多笔可累积。UI明确说明，不擅改成连续dab，旧Version1保存结果不变。
- 实现优化仅精确BVH分支剔除，不改公式；随机/回折/重叠/擦除/反相与原暴力段扫描逐样本1e-12核验。索引每冻结渲染编译，无全尺寸蒙版栅格缓存；覆盖max768^2×4，33ms合并+取消+目标/变体/节点/代次校验。
- 本参考补齐可复现语义/差异，不自动等于完整科学/ICC准入；诊断限制按POLICY仍保留，不能通过删条件编译宣称完成。
