# 本轮状态与未关闭项

目标顺序：合并版完整验证/Release → P3诊断与最小修复 → ICC真实程序对照 → 桌面允许时实机。

## 已有确定证据
- 基线8c3986e含正常双亲合并dc67b44（A b1630f0/B419c9c1），没有改写A/B。
- 同一合并testhost中A7/B8逐项Passed，各一次；全Core1694/7skip、WPF1602/23skip；实际独立合并Release不是A/B包，Core1695/6skip、相关WPF26、发布身份1。
- P3 10,128仓储/SQL观测与ViewModel分段、有限3次原必需测试、修复前后同输入数据、保留历史峰值。SQL/元数据优化不改变像素。
- 真实ICC参考LCMS/Argyll/skcms均实际执行，包含合法v2/v4 matrix/TRC/LUT、16位/float/intents、负/超范围、损坏profile。发现跨实现差异，未接默认通路。
- 授权Canon/Fuji/Sony原件均按固定hash定位；只读finalRGB16观测、Sony旧拒绝、内部stage unavailable分开报告。
- 大图Structure独立full/tile/cancel；samefloat hash一致但tile更慢/内存更高，不修改节点。原Details预算拒绝历史保留；本次full可跑不代表所有尺寸可跑。

## 不能关闭/不能改写
1. 历史仓储673.8/2499.7ms峰值缺当时SQL/负载分段；本轮新的UI瓶颈有证据，但不能偷换成对历史峰值的唯一根因。
2. strict NotExecuted仍FAILED；review只允许已有逐名外部/人工条件。新增CompanyQueryAggregate/CompanyScopeSwitch必需。
3. ICC v4 absolute第二实现不足；skcms的LUT PCS标度/意图差异尚未完全解释和上游确认，拒绝把“跑过了”写成“普遍ICC正确”。Reader仍只接受声明7tag v2线性子集。
4. 合法合成profile不是实际相机/显示器测量；真实照片ICC预览/保存/导出、输出profile完整发布路径尚未验证。无产品ICC依赖变更。
5. RAW两成熟实现同阶段比较本轮未做；公司旧LibRaw最终RGB16不能反推出黑位前/矩阵前。候选固定身份找到但下轮对照，默认不变。
6. Native allocator归属、长时间自然释放/真实切图取消/OS回落未证明；3秒sample和账本为不同量，不混淆。
7. 共享桌面有其他应用近期输入，未争抢；Windows产品启动导航/拖动/DPI/WPS/视觉批准NOT_RUN。
8. A跨文件ACID/GC等旧OPEN及Grok布局限制继续保留，不改UI视觉算通过。
9. A7M4绝对色准/宝丽来准确复刻未证明，CalibrationEligible=false；不请求购买色卡。
