# 第七批：完整桥的独立数值检查（不改变数学定义）

版本仍`ExistingResearchD65ToD50V1`；本轮没有换系数、clamp或把第六批结果重贴标签。

## 实际数据/阶段

LibRaw0.21.1特定prophoto_rgb P来自未改生产ProfessionalRawPipeline固定系数；先P逆到linear sRGB，接**引擎实际blob**的sRGB→XYZ D65矩阵，Bradford D65[.3127,.329]→D50[.3457,.3585]，最后实际blob ProPhoto RGB→XYZ矩阵的逆到engine输入。

blob SHA/实际系数、白点及生产源hash见company-round7/complete-bridge.json。特别引擎还存有另一份四位舍入XYZ→ProPhoto矩阵，它**不精确等于**RGB→XYZ的逆；本既定桥用后者的逆，不暗换成另一份常数。源matrix inverse是输出空间变换，不替代camera characterization。

## 两个固定实际参考入口

1. **Colour0.4.6 / commit `0c6f868c4b5c5ae861f868ba8015be3abf0f92f0`**：构造具有真实source/destination矩阵的`RGB_Colourspace`，实际调用`RGB_to_RGB(...,chromatic_adaptation_transform='Bradford',apply_cctf_decoding=False,apply_cctf_encoding=False)`。这是完整色空间path，不只比较CAT矩阵。
2. **OpenColorIO2.5.1 / commit `004f80009653fa322c1133ba2c57b8ed526599c9`**：原source P与目的E分别交`MatrixTransform(direction=INVERSE)`，其逆由OCIO实现；其余S/CAT以分段`GroupTransform`独立CPUProcessor.applyRGB执行，记录每段、组合、float32误差。OCIO不是另一个CAT估计器（CAT参数来源有声明），而是独立矩阵执行/求逆路径；不把同NumPy表达式重算称参考。

两项目BSD-3-Clause，本地隔离引用不进产品依赖；更具体许可证仍见LICENSE_ROUND7。OCIO仅安装到round7/reference-tools，无产品PATH/系统SDK更改。

## 覆盖和结果

中性0/.18/1/4、RGB原色、负/超范围/饱和mixed，及同一授权自然图稀疏全画面样本共1527个输入RGB，实际sampleCount亦记录JSON（不把样本点当场景）。分别记录每阶段输出和组合误差。

- Colour完整path vs既有桥最大差约1.78e-15；OCIO完整float path约1.78e-7。
- 分段double vs组合double≤8.89e-16；OCIO各阶段误差见JSON，未为通过放宽未知阈值。
- 完整自然数据按16行块审计，微负数量仍R/G/B `[0,942981,590359]`，double和float32数量相同，**最终float cast新增负值0**。double最小G−8.4338671e-7/B−2.4494540e-6。源近零/0通道经过已指定的舍入系数矩阵组合已经得到负号，不是float cast才凭空产生。它也不是sensor信号恢复。
- 固定第六批原有full/proxy文件保持；新的有界准备同数学得到全部10文件hash相同。独立参考审计因矩阵结合顺序不同可出现≤3.56e-15局部float差，不能把那份重算数据当原输入替换。

本轮只验证数学/实现一致性，不证明相机/胶片物理拟合、肤色或高光覆盖。需要任何输入数学改变则新显式版本+新hash，不能暗改V1。
