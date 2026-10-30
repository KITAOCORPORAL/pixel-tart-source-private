# 节点开源对照：胶片采样曲线下降来源（第五批）

状态 **VERIFIED_BOUNDED_CAUSE / PRODUCT_DISABLED**；没有新增曲线修复或像素算法版本。本轮只查明固定profile消费路径，不把数学重现升级为物理准确性/产品批准。

## 输入/输出和运行位置

- 输入：原有标准ProPhoto D50线性中性ramp经过光谱相机响应/曝光后，`node_expose_log`对每通道`max(exposure,0)+1e-10`取log10；旧tap已是float32 log10三通道，不是TIFF16显示域。
- 曲线：`Profile::from_json`取原`data.density_curves`，逐通道减nanmin；不重拟合/排序/平滑。`build_interp_tables`生成`log_exposure / density_curve_gamma`（本配置1），double算逆步长再上传float32；`spk_curves.comp:interpolate`右偏二分+分段线性、端点固定，不是三次插值。
- 输出：CMY dye density float32，无alpha、无最终显示clamp。之后进入enlarger spectral→paper exposure→paper CDF曲线→scan spectral→XYZ/RGB→gamut→scanner blur/unsharp→output CCTF→RGBA16最终clamp。film与paper曲线来源不同，不混写。

## 固定相关参考

|参考|固定源码|实际入口/配置|许可及边界|
|---|---|---|---|
|NumPy2.4.6|`b832a09cf2a169c833dd2371e7c07aa00b293242`|`numpy.interp`，`numpy/_core/src/multiarray/compiled_base.c:arr_interp`；在原double和上传float32表各执行float64插值，默认端点延伸|BSD-3-Clause，只外部研究。不是独立光谱胶片模型|
|SciPy1.18.1|`e4e854eaa8f18d807cd3496028e257e36caa93cc`|`scipy.interpolate.make_interp_spline(k=1)`→`BSpline.__call__`；对查询显式采用相同常数端点延伸，不用默认k3。另`scipy.special.ndtr`核查CDF模型|BSD-3-Clause，代码/二进制仅系统/隔离参考，不复制产品。独立B-spline评估路径，不复抄shader|

安装版本git_revision与tag读回一致。Python源与固定commit下载仅CRLF/LF字节不同，newline-normalized完全一致；保存两份原始hash、核心二进制hash。核心C/C++源入口也按固定commit读取，未声称从头重建这两包。

额外来源追溯：spektrafilm固定`3bb2c2d2801ff68b92019cf1dbcbb133d60832bc`；两个profile与Spektra运行资源**完整bytes一致**，最后profile提交`ad5c8d2b5bbf295f6d3912091f3b137b0aef805a`（refit/update license）。源码`model/density_curves.py:interpolate_exposure_to_density`→`utils/fast_interp.py:fast_interp`也消费原采样表。它是来源证据，不算第三个已实跑引擎；未装Numba，未运行其完整Python处理链。

## 复现与结果

- 固定engine `c1447c6ab618fa4b1cb11fd2fe12826855007059`；所有资源、源、shader、DLL、旧tap hash保留。`curve_neighbour_probe.cpp`直接链接已有真实engine loader/table builder及GPU shader，不复制实现；查询来自第四批17×1025旧trace，只保存小邻域+统计，不再全量大图落盘。
- Portra采样表下降区间R/G/B=25/21/27；Ektar=27/23/25。原double表已经非单调，归一减常数不会改变斜率。两点最小fixture取各stock R通道index11→12，查询为旧实际log输入，两个参考不经GPU也都下降。
- 所有被查旧下降邻域的GPU输出与旧film_curves_exact **0差异**。原double与已上传float32表的参考都保留下降；GPU相对原double受影响点最大误差Portra7.77e-10、Ektar1.65e-9，远小于对应1e-5级density下降。
- 两个线性参考最大相差≤8.89e-16。对原节点、全部256节点/255中点、域外与mixed-channel查询，GPU未越出相邻原表值范围（严格比较、无新增容差）；无“插值过冲”证据。全域参考最大差约1.23e-7是舍入误差，不可拿它替代受影响点统计。
- 同profile的正幅度/正sigma CDF模型在256点上单调；C++实际CDF评估与SciPy ndtr≤4.45e-16，sublayer和与CDF≤2.23e-16；但原sample-vs-fit最大残差约0.0036–0.00454，二者本就不是相同数据。grain关闭下film未调用CDF/layer替换sample表，不能说运行时“拟合过冲”。paper才调用自己的CDF拟合。

结论：这批已观测下降的直接来源是**固定原始profile采样表内局部负斜率，经正确的分段线性查表传递**；不是16bit量化、BT补丁、三次插值过冲或float舍入凭空生成。现有冻结资料不能确定上游测量/数字化/预处理为何产生该残差，也不能据1code反推物理色准失败。

## 决策和门禁

不改资源，不替换成CDF，不平滑/钳制，不放宽阈值。若以后要改sample表或选择拟合模型，须显式新版本，独立自然验证/负值/>1/灰阶/饱和色/尺寸/条带/预览重开导出及权利评估。本轮没有这种候选修复，故不虚报其影响矩阵完成。

PV4/5/6及产品src保持；本轮不重跑无关全套。CalibrationEligible=false；GPL/CC BY-SA和资料表权利继续独立门禁。数值/最小fixture见company-round5，外部源码/资源/图像只在本地研究根。
