# film_scale_and_expose 定位续接读回

记录时间：2026-11-02T17:30:28+08:00（Asia/Shanghai）。**仅核验已有定位，不重复公司GPU执行。首个跨端具体操作仍未确认，精确缺项是家庭同版gray子阶段tap。**

## 接续发现
用户审查基线7309911e1ad501b517877a073a86e468a9507941之后，研究分支已经包含4c4bf7889e66cd0d7b9489a5c257252ef55caf52（公司单次被动观测及分析）和6c47fb5bf1f8c6734aafe49209ac89ec03d95223（远端读回收据）。fetch/ls-remote与当前clean研究树一致。因此本续接只执行三个不加载引擎的既有CPU分析/验证器，没有再编译DLL或运行gray渲染。

已读AGENTS、POLICY、CHECKPOINT、公司公开合成诊断、build-confounds、failures-and-remaining、既有定位报告/能力表/家庭最小接续材料，并只读核对固定external source。产品src/UI/任务桥/default/PV4/PV5/PV6不改；旧公司灯台、产品线、家庭十至十三批和公司原失败资料均保留。

## 本次实际读回证据
- 既有定位payload的59文件哈希与原manifest一致；既有历史清单709文件哈希一致。该核验发生在追加本次CHECKPOINT前；旧manifest的CHECKPOINT身份绑定6c47fb5b的原Git内容，不能拿更新后的当前CHECKPOINT错误判旧证据漂移。
- 公司实际完整local capture重新通过verify_gray_scale_tap.py：16份finite数值数组、输入device逐bit一致、矩阵9项和runtime LUT110592项的CPU double窄化→实际staging→device逐bit0差。完整LUT/sensitivity仍只本地；公开subset不能冒充完整capture。
- 重新运行analyze_gray_scale_existing.py和analyze_gray_scale_taps.py，输出文件SHA与封存分析分别完全相同。无新输入、无新接受阈值。
- 实际observer的10个source hash、DLL SHA08b9e146263d6988cb962cb791ad786ca3301b872d471f2ad53e366b9e09cb63均与唯一gray运行前身份一致；35个shader source、9个数值CPU/旧入口文件与原diagnostic source保持一致。
- sealed final RGBA16与observed whole SHA同为214805479a6e9470c3bcd2bd18d27eb30e41309fc352494ba856114fbbebbf98；完整local validator按原公司结果确认逐通道0差，不是仅与某新生成参考比较。

## 实际数值路径与能缩小到哪里
CPU film_sensitivity（native double pow等）→runtime Hanatos LUT构建/光谱收缩/实际gamut remap→ProPhoto到XYZ/CAT16、D55参考的tc_b矩阵→double窄化后的实际float上传；GPU spk_tc_b（XYZ点积、brightness、除法和tc clamp）→spk_lut2d_cubic（Mitchell 4×4、镜像索引、累加与权重归一、乘brightness）→spk_affine3（EV0 scale1、offset0）。sceneLatitude分支实际关闭。Pillow未进入该数值路径，不能和native CPU准备/GPU算子列为同等嫌疑。

gray-boundaries为既有17×33×3，不扩大样本。复合阶段1683值中1520变化：

|位置|家庭值|公司值|绝对差|对称相对差|同号binary32 ULP|
|---|---|---|---|---|---|
|首差(2,0,R)|0.9905756115913391|0.9905756711959839|5.960464477539063e-8|6.017172287648274e-8|1|
|最大绝对差代表(32,0,G)|10.75505542755127|10.755059242248535|3.814697265625e-6|3.546886334795744e-7|4|

nearzero非零363值/345变化，maxabs1.13687e-13、max5ULP；正常负数3值/0变化；正常正数1317值/1175变化，maxabs3.81470e-6、max23ULP。近零1e-6只为描述分组，relative零值排除、异号不套同量级ULP，没有新增跨设备PASS阈值。最大4ULP是最大绝对差位置，不是全图最大ULP；不能混淆。

公司upsample→exposure与exposure→原封存compound stage均逐bit0差，实际曝光scale=[1,1,1]/offset=[0,0,0]。因此**公司差异承载存在于Hanatos upsample完成输出，早于曝光；这不是已证明跨端首差在LUT插值的结论**。同配置/resource/SPIRV hash一致不等于家庭实际上传矩阵/LUT字节一致。

数学模式仍为原引擎记录fused_fma_runtime=1；tc_b、lut2d_cubic、affine3三个shader不声明constant_id0，因此记录的specialization值不是这三个shader内切换另一数学分支的证据。没有修改默认数学模式/优化/精度/顺序。

## 精确缺项与停止条件
家庭没有对应FilmScaleSubstageTapV1。缺家庭实际CPU sensitivity/matrix/LUT、上传后float字节、tc与brightness（同一dispatch的两个输出）、upsample、exposure和真实mathmode/build provenance；现有材料无法在CPU准备、tc_b、LUT插值之间认定跨端首因，更不能分离硬件与驱动影响。

到此停止。下一步仅家庭一次同版本gray whole，先证明observed final对家庭封存结果逐通道0差，再先比实际上传常数字节、后比tc/b→upsample→exposure。使用现有REPRODUCE_SCALE_LOCALIZATION_20261102.md和工具，不伪造家庭常数，不再公司重复观测。完整runtime LUT/sensitivity不自动上传Git，许可边界沿用。

## 本续接调用与交付边界
新增engineCreate/open/render/stream/BT/API=0；新增GPU readback=0、数学dispatch=0、build=0、observer source/DLL版本=0。已有4c4bf788观测历史为engineCreate1/open1/wholeRender1、7被动readback、0新增数学dispatch，不能写成本续接又执行一次。底层历史初始化math-probe总dispatch本来未计数，仍未计数。

RAW/20MP/六张自然图/新锐化候选均0。SharedIncrementEnvelopeUnsharpV1 default-off、NOT_ADMITTED。算法修复需求仍NOT_ESTABLISHED，本次不实施像素修复；不改曲线/profile/clamp/FMA/驱动/阈值，不重开BT，不扩stock/相机/C1。三个小输入历史max1U16不外推所有照片、可见性或产品验收。研究定位不等于产品接入或分发许可。

本次CPU读回输出：E:/Kitao-Research/color-engine-scale-localization-readback-20261102。既有完整观测：E:/Kitao-Research/color-engine-scale-localization-20261102-v2/gray-once。新收据时间Asia/Shanghai，旧收据和NOT_RUN原样保留。
